// Experimental host executor. Core supplies the numerical kernel, data and f64 reference.
// This module has no session commands, controller rules, or mechanism to commit GPU results.
async function bounded(promise, label) {
  let timer;
  try {
    return await Promise.race([promise, new Promise((_, reject) => {
      timer = setTimeout(() => reject(new Error(label + " timed out; prototype result rejected.")), 15000);
    })]);
  } finally { clearTimeout(timer); }
}
export function validateFixture(fixture) {
  const n = fixture.nodeCount;
  if (fixture.protocol !== "candu-spatial-gpu-prototype-v1" || !Number.isInteger(n) || n < 1 || n > 100000)
    throw new Error("Invalid GPU fixture identity/dimension.");
  if (!Number.isInteger(fixture.iterations) || fixture.iterations < 0 || fixture.iterations > 128)
    throw new Error("Invalid fixed iteration count.");
  if (fixture.group !== 1 && fixture.group !== 2) throw new Error("Invalid energy group.");
  if (fixture.rowOffsets.length !== n + 1 || fixture.nodeData.length !== n * 4 || fixture.inputFlux.length !== n ||
      fixture.targets.length !== fixture.conductances.length || fixture.rowOffsets[0] !== 0 ||
      fixture.rowOffsets[n] !== fixture.targets.length) throw new Error("Incomplete GPU fixture arrays.");
  for (let i = 0; i <= n; i++) {
    const offset = fixture.rowOffsets[i];
    if (!Number.isInteger(offset) || offset < 0 || offset > fixture.targets.length || (i > 0 && offset < fixture.rowOffsets[i - 1]))
      throw new Error("Invalid CSR row offsets.");
  }
  for (const target of fixture.targets)
    if (!Number.isInteger(target) || target < 0 || (target >= n && target !== 0xffffffff)) throw new Error("Invalid neighbor target.");
  for (const values of [fixture.nodeData, fixture.conductances, fixture.inputFlux])
    if (values.some(value => !Number.isFinite(value) || value < 0 || !Number.isFinite(Math.fround(value))))
      throw new Error("GPU input is outside nonnegative finite f32 range.");
  for (let i = 0; i < n; i++)
    if (Math.fround(fixture.nodeData[i * 4 + 1]) <= 0 || Math.fround(fixture.nodeData[i * 4 + 2]) <= 0)
      throw new Error("GPU volume/diagonal must remain positive.");
  if (typeof fixture.shaderSource !== "string" || fixture.shaderSource.length === 0) throw new Error("Missing Core kernel.");
}

export async function createGpuSpatialPrototype(gpu = globalThis.navigator?.gpu) {
  if (!gpu) return { supported: false, reason: "WebGPU is unavailable." };
  const adapter = await bounded(gpu.requestAdapter({ powerPreference: "high-performance" }), "GPU adapter request");
  if (!adapter) return { supported: false, reason: "No WebGPU adapter is available." };
  const device = await bounded(adapter.requestDevice(), "GPU device request");
  let lost = false;
  device.lost.then(() => { lost = true; });
  const pipelines = new Map();
  const layout = device.createBindGroupLayout({ entries: [
    { binding: 0, visibility: GPUShaderStage.COMPUTE, buffer: { type: "uniform" } },
    ...[1, 2, 3, 4].map(binding => ({ binding, visibility: GPUShaderStage.COMPUTE, buffer: { type: "read-only-storage" } })),
    ...[5, 6, 7].map(binding => ({ binding, visibility: GPUShaderStage.COMPUTE, buffer: { type: "storage" } })),
  ] });
  const pipelineLayout = device.createPipelineLayout({ bindGroupLayouts: [layout] });
  return {
    supported: true,
    adapter: { vendor: adapter.info?.vendor ?? "", architecture: adapter.info?.architecture ?? "",
      device: adapter.info?.device ?? "", description: adapter.info?.description ?? "",
      isFallbackAdapter: adapter.info?.isFallbackAdapter ?? adapter.isFallbackAdapter ?? null },
    async run(fixture) {
      validateFixture(fixture);
      if (lost) throw new Error("GPU device was lost; prototype result rejected.");
      const buffers = [];
      const start = performance.now();
      device.pushErrorScope("validation");
      const make = (values, usage) => {
        const buffer = device.createBuffer({ size: Math.max(4, values.byteLength), usage, mappedAtCreation: true });
        new Uint8Array(buffer.getMappedRange()).set(new Uint8Array(values.buffer, values.byteOffset, values.byteLength));
        buffer.unmap(); buffers.push(buffer); return buffer;
      };
      let result, failure;
      try {
        // Cache by actual kernel text, not an unverified externally supplied digest.
        let compiled = pipelines.get(fixture.shaderSource);
        const compileStart = performance.now();
        if (!compiled) {
          const module = device.createShaderModule({ code: fixture.shaderSource });
          const info = await bounded(module.getCompilationInfo(), "GPU compilation diagnostics");
          const errors = info.messages.filter(message => message.type === "error");
          if (errors.length) throw new Error(errors.map(message => message.message).join(" | "));
          compiled = await bounded(Promise.all(["applyOperator", "jacobiStep"].map(entryPoint =>
            device.createComputePipelineAsync({ layout: pipelineLayout, compute: { module, entryPoint } }))), "GPU pipeline compilation");
          pipelines.set(fixture.shaderSource, compiled);
        }
        const compileMs = performance.now() - compileStart;
        const uploadStart = performance.now();
        const storage = GPUBufferUsage.STORAGE;
        const params = make(new Uint32Array([fixture.nodeCount, fixture.rowOffsets.length, 0, 0]), GPUBufferUsage.UNIFORM);
        const indices = make(new Uint32Array([...fixture.rowOffsets, ...fixture.targets]), storage);
        const nodes = make(new Float32Array(fixture.nodeData), storage);
        const conductances = make(new Float32Array(fixture.conductances), storage);
        const a = make(new Float32Array(fixture.inputFlux), storage | GPUBufferUsage.COPY_SRC);
        const b = make(new Float32Array(fixture.nodeCount), storage | GPUBufferUsage.COPY_SRC);
        const applied = make(new Float32Array(fixture.nodeCount), storage | GPUBufferUsage.COPY_SRC);
        const status = make(new Uint32Array(1), storage | GPUBufferUsage.COPY_SRC);
        const groups = [[a, b], [b, a]].map(([input, output]) => device.createBindGroup({ layout, entries:
          [params, indices, nodes, conductances, input, applied, output, status].map((buffer, binding) => ({ binding, resource: { buffer } })) }));
        const bytes = fixture.nodeCount * 4;
        const readback = device.createBuffer({ size: bytes * 3 + 4, usage: GPUBufferUsage.COPY_DST | GPUBufferUsage.MAP_READ });
        buffers.push(readback);
        const uploadMs = performance.now() - uploadStart;
        const submitStart = performance.now();
        const encoder = device.createCommandEncoder();
        const pass = encoder.beginComputePass();
        const dispatch = (pipeline, parity) => { pass.setPipeline(pipeline); pass.setBindGroup(0, groups[parity]); pass.dispatchWorkgroups(Math.ceil(fixture.nodeCount / 64)); };
        dispatch(compiled[0], 0); pass.end();
        encoder.copyBufferToBuffer(applied, 0, readback, 0, bytes);
        // All fixed iterations stay GPU-resident, with no intermediate CPU readback.
        const iterations = encoder.beginComputePass();
        for (let step = 0; step < fixture.iterations; step++) {
          iterations.setPipeline(compiled[1]); iterations.setBindGroup(0, groups[step % 2]); iterations.dispatchWorkgroups(Math.ceil(fixture.nodeCount / 64));
          iterations.setPipeline(compiled[0]); iterations.setBindGroup(0, groups[(step + 1) % 2]); iterations.dispatchWorkgroups(Math.ceil(fixture.nodeCount / 64));
        }
        iterations.end();
        encoder.copyBufferToBuffer(fixture.iterations % 2 === 0 ? a : b, 0, readback, bytes, bytes);
        encoder.copyBufferToBuffer(applied, 0, readback, bytes * 2, bytes);
        encoder.copyBufferToBuffer(status, 0, readback, bytes * 3, 4);
        device.queue.submit([encoder.finish()]);
        await bounded(readback.mapAsync(GPUMapMode.READ), "GPU readback");
        const completedMs = performance.now() - submitStart;
        const mapped = readback.getMappedRange();
        const flags = new Uint32Array(mapped, bytes * 3, 1)[0];
        const initialApplied = new Float32Array(mapped, 0, fixture.nodeCount).slice();
        const flux = new Float32Array(mapped, bytes, fixture.nodeCount).slice();
        const finalApplied = new Float32Array(mapped, bytes * 2, fixture.nodeCount).slice();
        readback.unmap();
        if (lost || flags || [...flux].some(value => !Number.isFinite(value) || value < 0) ||
            [...initialApplied, ...finalApplied].some(value => !Number.isFinite(value)))
          throw new Error(`GPU output rejected (deviceLost=${lost}, flags=${flags}).`);
        result = { initialApplied: [...initialApplied], flux: [...flux], finalApplied: [...finalApplied],
          compileMs, uploadMs, submitToReadbackMs: completedMs, totalMs: performance.now() - start };
      } catch (error) { failure = error; }
      finally {
        for (const buffer of buffers) buffer.destroy();
        const validationError = await bounded(device.popErrorScope(), "GPU validation diagnostics");
        if (validationError && !failure) failure = new Error(validationError.message);
      }
      if (failure) throw failure;
      return result;
    },
    dispose() { pipelines.clear(); device.destroy(); },
  };
}
