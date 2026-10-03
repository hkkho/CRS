/** Read-only GPU kernel experiment; never commits GPU outputs into the C# game. */
import { chromium } from "playwright";
import { readFile } from "node:fs/promises";
const url = process.argv[2];
if (!url) throw new Error("Pass the production preview URL.");
const software = process.argv.includes("--software");
const hardware = process.argv.includes("--hardware");
const coupled = process.argv.includes("--coupled");
const probe = process.argv.includes("--probe");
const developmentKernel = process.argv.includes("--development-kernel")
  ? await readFile(new URL("../../../src/ReactorSim.Core/EmbeddedData/coupled-spatial-v1.wgsl", import.meta.url), "utf8") : null;
if (developmentKernel && !coupled) throw new Error("Development kernel override requires --coupled.");
if (software && hardware) throw new Error("Choose software or hardware adapter request, not both.");
const browser = await chromium.launch({ headless: true, channel: "chromium",
  args: software ? ["--enable-unsafe-webgpu", "--use-webgpu-adapter=swiftshader", "--enable-features=Vulkan", "--use-vulkan=swiftshader"]
    : hardware ? ["--enable-unsafe-webgpu", "--use-angle=d3d11"] : [] });
const errors = [];
try {
  const page = await browser.newPage();
  page.on("pageerror", error => errors.push(error.message));
  page.on("console", message => { if (message.text().startsWith("gpu:")) process.stderr.write(message.text() + "\n"); });
  await page.goto(url, { waitUntil: "domcontentloaded" });
  const buildInfo = JSON.parse((await (await page.request.get(new URL("/wasm/build-info.json", url).href)).text()).replace(/^\uFEFF/, ""));
  const result = await page.evaluate(async ({ coupled, probe, developmentKernel }) => {
    const workerSource = `
      import { ${coupled ? "createGpuCoupledSpatial" : "createGpuSpatialPrototype"} as createBackend } from ${JSON.stringify(new URL(coupled ? "/wasm/gpuCoupledSpatial.mjs" : "/wasm/gpuSpatialPrototype.mjs", location.href).href)};
      let backend;
      let queue = Promise.resolve();
      self.onmessage = ({data}) => { queue = queue.then(async () => {
        try {
          const result = data.type === "initialize" ? (backend = await createBackend(), {supported:backend.supported,reason:backend.reason,adapter:backend.adapter}) : await backend.run(data.fixture);
          self.postMessage({id:data.id,result});
        } catch(error) { self.postMessage({id:data.id,error:String(error?.stack ?? error)}); }
      }); };
      self.postMessage({type:"ready"});`;
    const blob = URL.createObjectURL(new Blob([workerSource], { type: "text/javascript" }));
    const gpuWorker = new Worker(blob, { type: "module" });
    let cpuWorker;
    const connect = worker => {
      let nextId = 0, resolveReady, rejectReady;
      const pending = new Map();
      const ready = new Promise((resolve,reject) => {resolveReady=resolve;rejectReady=reject;});
      worker.onmessage = ({data}) => {
        if(data.type === "ready") return resolveReady();
        if(data.type === "load-error") return rejectReady(new Error(data.error));
        const item = pending.get(data.id); if(!item) return;
        pending.delete(data.id); clearTimeout(item.timer);
        data.error ? item.reject(new Error(data.error)) : item.resolve(data);
      };
      worker.onerror = error => {rejectReady(new Error(error.message));for(const item of pending.values()){clearTimeout(item.timer);item.reject(new Error(error.message));}pending.clear();};
      return {ready,request: payload => new Promise((resolve,reject) => {
        const id=++nextId;
        const timer=setTimeout(()=>{pending.delete(id);reject(new Error(payload.type+" timed out."));},120000);
        pending.set(id,{resolve,reject,timer});worker.postMessage({...payload,id});
      })};
    };
    try {
      const gpu = connect(gpuWorker); await gpu.ready;
      const capability = (await gpu.request({type:"initialize"})).result;
      if(!capability.supported) return capability;
      console.info("gpu: adapter/device ready");
      const app = document.querySelector('script[type="module"][src]');
      const scriptUrl = new URL(app.getAttribute("src"),location.href);
      const source = await (await fetch(scriptUrl)).text();
      const match = source.match(/(?:\/assets\/)?wasmWorker-[A-Za-z0-9_-]+\.js/);
      if(!match) throw new Error("Production WASM worker asset not found.");
      cpuWorker = new Worker(new URL(match[0],scriptUrl),{type:"module"});
      const cpu = connect(cpuWorker); await cpu.ready;
      const initial = JSON.parse((await cpu.request({type:"initialize",mode:"play"})).resultJson);
      if(!initial.accepted) throw new Error("CPU reference initialization rejected.");
      console.info("gpu: CPU reference initialized");
      const dispatch = async payload => {
        const response=JSON.parse((await cpu.request({type:"dispatch",commandJson:JSON.stringify({protocol:"candu-playtest-v2",responseMode:"compact",payload})})).resultJson);
        if(!response.accepted) throw new Error(payload.type+": "+response.message);
      };
      const error = (actual,expected) => {
        let delta=0,scale=0;
        for(let n=0;n<actual.length;n++){delta=Math.max(delta,Math.abs(actual[n]-expected[n]));scale=Math.max(scale,Math.abs(expected[n]));}
        return delta/Math.max(scale,1e-30);
      };
      const samples=[],scenarios=[];
      for(const scenario of (probe ? ["aged"] : ["aged","refuel","poison-1h","moderator-reflective"])){
        console.info("gpu: "+scenario);
        if(scenario === "refuel") await dispatch({type:"commit-refuel",request:{channelIndex:210,directionId:"toward-end-b",shiftCount:4,fuelTypeId:"NAT-U-SYNTHETIC"}});
        if(scenario === "poison-1h"){await dispatch({type:"resume"});await dispatch({type:"queue-power-target",targetFraction:.95});await dispatch({type:"advance",wallMilliseconds:2000});await dispatch({type:"pause"});}
        if(scenario === "moderator-reflective") await dispatch({type:"configure-cell",channelIndex:210,position:5,hasFuel:false,reflectiveFaces:["end-a"]});
        const before=(await cpu.request({type:"get-snapshot"})).resultJson;
        if (coupled) {
          for (const cold of (probe ? [false] : [false, true])) {
            const started = performance.now();
            const fixture = JSON.parse((await cpu.request({type:"gpu-fixture",requestJson:JSON.stringify({protocol:"candu-spatial-gpu-prototype-v1",kind:"coupled",cold})})).resultJson);
            if (developmentKernel) fixture.shaderSource = developmentKernel;
            const exportMs = performance.now() - started;
            for (let repeat = 0; repeat < (probe ? 1 : 2); repeat++) {
              console.info(`gpu: ${scenario} ${cold ? "cold" : "warm"} ${repeat}`);
              const requestStart = performance.now();
              let output, check = null, error = null;
              try {
                output = (await gpu.request({type:"run",fixture})).result;
                if (output.converged) check = JSON.parse((await cpu.request({type:"gpu-fixture",requestJson:JSON.stringify({
                  protocol:"candu-spatial-gpu-prototype-v1",kind:"verify-coupled",experimentId:fixture.experimentId,
                  k:output.k,previousK:output.previousK,flux:output.flux,previousFlux:output.previousFlux,iterations:output.iterations})})).resultJson);
              } catch (failure) { error = String(failure); }
              const {flux,previousFlux,...metrics} = output ?? {};
              samples.push({scenario,cold,repeat,...metrics,error,check,exportMs,kernelDigest:developmentKernel ? null : fixture.kernelDigest,coefficientDigest:fixture.coefficientDigest,cpuSolveMs:fixture.cpuSolveMs,cpuIterations:fixture.cpuIterations,
                tolerances:{regionalAgreementPercentagePoints:fixture.regionalAgreementTolerancePercentagePoints,
                  reactivityAgreementMk:fixture.reactivityAgreementToleranceMk,innerRelative:fixture.innerRelative,
                  outerResidual:fixture.outerResidual,kAbsolute:fixture.kAbsolute,kRelative:fixture.kRelative,sourceShape:fixture.sourceShape},
                fullRoundTripAndVerificationMs:performance.now()-requestStart,
                agreementPassed:!!check?.agreementPassed, cpuVerifiedConverged:!!check?.cpuVerifiedConverged});
            }
          }
        } else {
        for(const group of [1,2]) for(const iterations of [0,3,32,128]){
          const started=performance.now();
          const fixture=JSON.parse((await cpu.request({type:"gpu-fixture",requestJson:JSON.stringify({protocol:"candu-spatial-gpu-prototype-v1",group,iterations})})).resultJson);
          const fixtureExportMs=performance.now()-started;
          const cold=(await gpu.request({type:"run",fixture})).result;
          for(let repeat=0;repeat<3;repeat++){
            const requestStart=performance.now();
            const result=(await gpu.request({type:"run",fixture})).result;
            const gpuWorkerRoundTripMs=performance.now()-requestStart;
            let residual=0,scale=0;
            for(let n=0;n<fixture.nodeCount;n++){const source=fixture.nodeData[n*4+3];residual=Math.max(residual,Math.abs(result.finalApplied[n]-source));scale=Math.max(scale,Math.abs(result.finalApplied[n])+Math.abs(source));}
            const relativeResidual=scale===0?0:residual/scale;
            const operatorError=error(result.initialApplied,fixture.referenceApplied),fluxError=error(result.flux,fixture.referenceFlux);
            samples.push({scenario,group,iterations,repeat,nodeCount:fixture.nodeCount,coefficientDigest:fixture.coefficientDigestHex,kernelDigest:fixture.kernelDigestHex,
              operatorError,fluxError,relativeResidual,referenceRelativeResidual:fixture.referenceRelativeResidual,requiredRelativeTolerance:fixture.requiredRelativeTolerance,
              currentPolicyResidualPassed:relativeResidual<=fixture.requiredRelativeTolerance,referenceCurrentPolicyPassed:fixture.referenceRelativeResidual<=fixture.requiredRelativeTolerance,
              agreementPassed:operatorError<=2e-6&&fluxError<=2e-6,gpuCompileMs:cold.compileMs,gpuUploadMs:result.uploadMs,
              gpuSubmitToReadbackMs:result.submitToReadbackMs,gpuTotalMs:result.totalMs,gpuWorkerRoundTripMs,fixtureExportMs,cpuOperatorMs:fixture.cpuOperatorMs,cpuIterationMs:fixture.cpuIterationMs});
          }
        }
        }
        const unchanged=(await cpu.request({type:"get-snapshot"})).resultJson===before;
        if(!unchanged) throw new Error("GPU prototype changed live CPU state.");
        scenarios.push({scenario,liveStateUnchanged:unchanged});
      }
      return {...capability,seed:initial.snapshot.seed,samples,scenarios};
    } finally {gpuWorker.terminate();cpuWorker?.terminate();URL.revokeObjectURL(blob);}
  }, { coupled, probe, developmentKernel });
  if (errors.length) throw new Error(errors.join(" | "));
  const agreementPassed = result.supported ? result.samples.every(sample => sample.agreementPassed) : null;
  const stats = values => {
    const sorted = [...values].sort((a, b) => a - b);
    return { count: sorted.length, median: sorted[Math.floor(sorted.length / 2)], p95: sorted[Math.ceil(sorted.length * .95) - 1] };
  };
  console.log(JSON.stringify({ format: "candu-gpu-prototype-benchmark-v1", url, browserVersion: browser.version(),
    buildInfo, softwareAdapterRequested: software, hardwareAdapterRequested: hardware, developmentKernelOverride:!!developmentKernel, consoleErrors: errors, agreementPassed,
    ...(result.supported && coupled ? { experiment:"complete-coupled-solver", completeEigenSolverImplemented:true,
      gpuEnabledForGameplay:false, cpuVerificationPassed:result.samples.every(s=>s.cpuVerifiedConverged),
      acceptedSamples:result.samples.filter(s=>s.cpuVerifiedConverged&&s.agreementPassed).length } : {}),
    ...(result.supported && !coupled ? { gpuTotalMs: stats(result.samples.map(s => s.gpuTotalMs)),
      maxOperatorError: Math.max(...result.samples.map(s => s.operatorError)), maxFluxError: Math.max(...result.samples.map(s => s.fluxError)),
      all128StepResidualsPassCurrentPolicy: result.samples.filter(s => s.iterations === 128).every(s => s.currentPolicyResidualPassed),
      completeEigenSolverImplemented: false, gpuEnabledForGameplay: false } : {}), ...result }, null, 2));
  if (agreementPassed === false || (coupled && result.supported && result.samples.some(s => !s.cpuVerifiedConverged))) process.exitCode = 1;
} finally { await browser.close(); }
