// Generic dispatch/transport for the Core-owned coupled numerical experiment.
// No gameplay rules and no route for committing results to a game session.
async function bounded(promise, label) {
  let timer;
  try { return await Promise.race([promise, new Promise((_, reject) => {
    timer = setTimeout(() => reject(new Error(`${label} timed out.`)), 30000);
  })]); } finally { clearTimeout(timer); }
}
export function validateCoupledFixture(f) {
  const n = f.nodeCount;
  if (f.protocol !== "candu-coupled-gpu-experiment-v1" || !Number.isInteger(n) || n < 1 || n > 100000)
    throw new Error("Invalid coupled fixture identity/dimension.");
  if (!Number.isInteger(f.maxInner) || f.maxInner < 32 || f.maxInner > 128 || f.maxInner % 32 ||
      !Number.isInteger(f.maxOuter) || f.maxOuter < 1 || f.maxOuter > 1600) throw new Error("Unsupported iteration bounds.");
  const edgeCount = f.conductances.length / 2;
  if (!Number.isInteger(edgeCount) || f.nodes.length !== n * 12 || f.initialFlux.length !== n * 2 ||
      f.indices.length !== n + 1 + edgeCount || f.indices[0] !== 0 || f.indices[n] !== edgeCount)
    throw new Error("Invalid coupled fixture array dimensions.");
  for (let i = 0; i <= n; i++) {
    if (!Number.isInteger(f.indices[i]) || f.indices[i] < 0 || f.indices[i] > edgeCount ||
        (i && f.indices[i] < f.indices[i - 1])) throw new Error("Invalid CSR offsets.");
  }
  for (const target of f.indices.slice(n + 1))
    if (!Number.isInteger(target) || target < 0 || (target >= n && target !== 0xffffffff)) throw new Error("Invalid CSR target.");
  for (const values of [f.nodes, f.initialFlux, f.conductances])
    if (values.some(x => !Number.isFinite(x) || x < 0 || !Number.isFinite(Math.fround(x)))) throw new Error("Invalid f32 values.");
  for (let i = 0; i < n; i++)
    if ([2, 4, 5].some(j => Math.fround(f.nodes[i * 12 + j]) <= 0)) throw new Error("Invalid volume/diagonal.");
  for (const key of ["targetPower", "initialK", "innerRelative", "innerAbsolute", "kAbsolute", "kRelative", "outerResidual", "sourceShape"])
    if (!Number.isFinite(f[key]) || Math.fround(f[key]) <= 0 || !Number.isFinite(Math.fround(f[key]))) throw new Error(`Invalid ${key}.`);
  if (typeof f.shaderSource !== "string" || !f.shaderSource) throw new Error("Missing Core kernel.");
}
export async function createGpuCoupledSpatial(gpu = globalThis.navigator?.gpu) {
  if (!gpu) return { supported: false, reason: "WebGPU unavailable." };
  const adapter = await bounded(gpu.requestAdapter({ powerPreference: "high-performance" }), "Adapter request");
  if (!adapter) return { supported: false, reason: "No GPU adapter." };
  const device = await bounded(adapter.requestDevice(), "Device request");
  let lost = false; device.lost.then(() => { lost = true; });
  const pipelines = new Map();
  const entries = [
    { binding: 0, visibility: GPUShaderStage.COMPUTE, buffer: { type: "uniform" } },
    ...[1, 2, 3].map(binding => ({ binding, visibility: GPUShaderStage.COMPUTE, buffer: { type: "read-only-storage" } })),
    ...[4, 5, 6, 7].map(binding => ({ binding, visibility: GPUShaderStage.COMPUTE, buffer: { type: "storage" } })),
  ];
  const layout = device.createBindGroupLayout({entries});
  const pipelineLayout = device.createPipelineLayout({ bindGroupLayouts: [layout] });
  return {
    supported: true,
    adapter: { vendor: adapter.info?.vendor ?? "", architecture: adapter.info?.architecture ?? "",
      isFallbackAdapter: adapter.info?.isFallbackAdapter ?? adapter.isFallbackAdapter ?? null },
    async run(f) {
      validateCoupledFixture(f);
      if (lost) throw new Error("GPU device lost.");
      const start = performance.now(), buffers = [];
      device.pushErrorScope("validation");
      let result, failure;
      try {
        const names = ["initialize", "beginOuter", "reduceBegin", "jacobiEven", "jacobiOdd", "innerMetrics", "reduceInner",
          "beginGroup2", "sourceGroup2", "trialMetrics", "reduceTrial", "normalize", "outerMetrics", "reduceOuter"];
        const compileStart = performance.now();
        let compiled = pipelines.get(f.shaderSource);
        if (!compiled) {
          const module = device.createShaderModule({ code: f.shaderSource });
          const info = await bounded(module.getCompilationInfo(), "Compilation diagnostics");
          const errors = info.messages.filter(x => x.type === "error");
          if (errors.length) throw new Error(errors.map(x => x.message).join(" | "));
          compiled = Object.fromEntries(await bounded(Promise.all(names.map(async entryPoint => [entryPoint,
            await device.createComputePipelineAsync({ layout: pipelineLayout, compute: { module, entryPoint } })])), "Pipeline compilation"));
          pipelines.set(f.shaderSource, compiled);
        }
        const compileMs = performance.now() - compileStart;
        const make = (values, usage) => {
          const buffer = device.createBuffer({ size: Math.max(4, values.byteLength), usage, mappedAtCreation: true });
          new Uint8Array(buffer.getMappedRange()).set(new Uint8Array(values.buffer, values.byteOffset, values.byteLength));
          buffer.unmap(); buffers.push(buffer); return buffer;
        };
        const blocks = Math.ceil(f.nodeCount / 64), paramsData = new ArrayBuffer(48);
        new Uint32Array(paramsData).set([f.nodeCount, f.nodeCount + 1, blocks, f.maxInner]);
        new Float32Array(paramsData).set([f.targetPower, f.initialK, f.innerRelative, f.innerAbsolute,
          f.kAbsolute, f.kRelative, f.outerResidual, f.sourceShape], 4);
        const stateData = new Float32Array(f.nodeCount * 12);
        for (let i = 0; i < f.nodeCount; i++) { stateData[i * 12] = f.initialFlux[i * 2]; stateData[i * 12 + 1] = f.initialFlux[i * 2 + 1]; }
        const storage = GPUBufferUsage.STORAGE, copy = GPUBufferUsage.COPY_SRC;
        const params = make(new Uint8Array(paramsData), GPUBufferUsage.UNIFORM);
        const indices = make(new Uint32Array(f.indices), storage), nodes = make(new Float32Array(f.nodes), storage);
        const edges = make(new Float32Array(f.conductances), storage), state = make(stateData, storage | copy);
        const partials = make(new Float32Array(blocks * 8), storage), control = make(new Uint32Array(16), storage | copy);
        const flags = make(new Uint32Array(1), storage | copy);
        const group = device.createBindGroup({ layout, entries: [params, indices, nodes, edges, state, partials, control, flags]
          .map((buffer, binding) => ({ binding, resource: { buffer } })) });
        const summary = device.createBuffer({ size: 68, usage: GPUBufferUsage.COPY_DST | GPUBufferUsage.MAP_READ }); buffers.push(summary);
        let controlFloats, controlInts, gpuFlags = 0, initialized = false, batches = 0, encodeMs = 0;
        const executionStart = performance.now();
        for (let count = 0; count < f.maxOuter; count += 8) {
          if (performance.now() - executionStart > 30000) throw new Error("Coupled solve exceeded time budget.");
          const encodeStart = performance.now(), encoder = device.createCommandEncoder(), pass = encoder.beginComputePass();
          pass.setBindGroup(0, group);
          const dispatch = name => {
            pass.setPipeline(compiled[name]);
            pass.dispatchWorkgroups(name.startsWith("reduce") || name === "initialize" || name === "beginGroup2" ? 1 : blocks);
          };
          if (!initialized) { dispatch("initialize"); initialized = true; }
          const inner = () => {
            for (let chunk = 0; chunk < f.maxInner; chunk += 32) {
              for (let step = 0; step < 32; step++) dispatch(step % 2 ? "jacobiOdd" : "jacobiEven");
              dispatch("innerMetrics"); dispatch("reduceInner");
            }
          };
          for (let iteration = count; iteration < Math.min(count + 8, f.maxOuter); iteration++) {
            dispatch("beginOuter"); dispatch("reduceBegin"); inner(); dispatch("beginGroup2"); dispatch("sourceGroup2"); inner();
            dispatch("trialMetrics"); dispatch("reduceTrial"); dispatch("normalize"); dispatch("outerMetrics"); dispatch("reduceOuter");
          }
          pass.end(); encoder.copyBufferToBuffer(control, 0, summary, 0, 64); encoder.copyBufferToBuffer(flags, 0, summary, 64, 4);
          device.queue.submit([encoder.finish()]); encodeMs += performance.now() - encodeStart; batches++;
          await bounded(summary.mapAsync(GPUMapMode.READ), "Coupled summary readback");
          const bytes = summary.getMappedRange().slice(0); controlFloats = new Float32Array(bytes, 0, 16); controlInts = new Uint32Array(bytes, 0, 16);
          gpuFlags = new Uint32Array(bytes, 64, 1)[0]; summary.unmap();
          if (!controlFloats[0]) throw new Error("GPU command submission did not produce a valid state.");
          if (lost || gpuFlags) throw new Error(`Coupled GPU output rejected (lost=${lost}, flags=${gpuFlags}).`);
          if (controlInts[11] || controlInts[12]) break;
        }
        const executionMs = performance.now() - executionStart;
        const converged = controlInts[11] === 1 && !controlInts[12];
        let flux = [], previousFlux = [];
        if (converged) {
          const readback = device.createBuffer({ size: stateData.byteLength, usage: GPUBufferUsage.COPY_DST | GPUBufferUsage.MAP_READ }); buffers.push(readback);
          const encoder = device.createCommandEncoder(); encoder.copyBufferToBuffer(state, 0, readback, 0, stateData.byteLength); device.queue.submit([encoder.finish()]);
          await bounded(readback.mapAsync(GPUMapMode.READ), "Coupled flux readback");
          const data = new Float32Array(readback.getMappedRange());
          for (let i = 0; i < f.nodeCount; i++) { flux.push(data[i * 12], data[i * 12 + 1]); previousFlux.push(data[i * 12 + 6], data[i * 12 + 7]); }
          readback.unmap();
          if ([...flux, ...previousFlux].some(x => !Number.isFinite(x) || x < 0)) throw new Error("Invalid coupled flux.");
        }
        result = { converged, failureCode: controlInts[12], iterations: controlInts[10], k: controlFloats[0], previousK: controlFloats[1],
          gpuResidual: controlFloats[8], lastInnerResidual: controlFloats[9], flux, previousFlux, compileMs,
          executionMs, encodeMs, batches, totalMs: performance.now() - start };
      } catch (error) { failure = error; }
      finally {
        for (const buffer of buffers) buffer.destroy();
        const error = await bounded(device.popErrorScope(), "Coupled validation diagnostics");
        if (error && !failure) failure = new Error(error.message);
      }
      if (failure) throw failure;
      return result;
    },
    dispose() { pipelines.clear(); device.destroy(); },
  };
}
