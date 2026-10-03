import { test } from "node:test";
import assert from "node:assert/strict";
import { validateFixture, createGpuSpatialPrototype } from "../../../src/ReactorSim.BrowserHost/gpuSpatialPrototype.mjs";
import { validateCoupledFixture, createGpuCoupledSpatial } from "../../../src/ReactorSim.BrowserHost/gpuCoupledSpatial.mjs";

const fixture = () => ({ protocol: "candu-spatial-gpu-prototype-v1", nodeCount: 1, group: 1, iterations: 0,
  rowOffsets: [0, 1], targets: [0xffffffff], conductances: [0], nodeData: [1, 1, 1, 1], inputFlux: [1], shaderSource: "Core test kernel" });

test("missing WebGPU or adapter produces an explicit unsupported result", async () => {
  assert.equal((await createGpuSpatialPrototype(null)).supported, false);
  assert.equal((await createGpuSpatialPrototype({ requestAdapter: async () => null })).supported, false);
});

test("coupled solver validates bounds, topology and coefficients before dispatch", async () => {
  assert.equal((await createGpuCoupledSpatial(null)).supported, false);
  assert.equal((await createGpuCoupledSpatial({requestAdapter: async () => null})).supported, false);
  const f = {protocol:"candu-coupled-gpu-experiment-v1",nodeCount:1,maxInner:128,maxOuter:1600,
    indices:[0,1,0xffffffff],nodes:[1,1,1,0,1,1,1,1,1,0,1,1],initialFlux:[1,1],conductances:[0,0],
    targetPower:1,initialK:1,innerRelative:1e-7,innerAbsolute:1e-11,kAbsolute:2e-7,kRelative:2e-6,
    outerResidual:2e-5,sourceShape:1e-5,shaderSource:"Core test kernel"};
  assert.doesNotThrow(() => validateCoupledFixture(f));
  for (const change of [{maxInner:1},{maxOuter:1601},{indices:[0,1,1]}, {initialFlux:[1,-1]},
    {initialK:Infinity},{targetPower:0},{innerRelative:0},{nodes:[1]},{conductances:[NaN,0]}])
    assert.throws(() => validateCoupledFixture({...f,...change}));
});

test("device request failure rejects rather than reporting success", async () => {
  await assert.rejects(createGpuSpatialPrototype({ requestAdapter: async () => ({ requestDevice: async () => { throw new Error("device failure"); } }) }), /device failure/);
});

test("zero-iteration fixture and reflective boundary sentinel are valid", () => {
  assert.doesNotThrow(() => validateFixture(fixture()));
});

test("malformed dimensions, offsets, neighbors, groups and iterations reject before GPU allocation", () => {
  for (const change of [
    { nodeCount: 0 }, { rowOffsets: [1, 1] }, { rowOffsets: [0, 2] }, { targets: [1] },
    { group: 0 }, { iterations: -1 }, { iterations: 129 }, { shaderSource: "" },
    { nodeData: [1, 0, 1, 1] }, { nodeData: [1, 1, -1, 1] }, { inputFlux: [Infinity] },
    { conductances: [NaN] }, { inputFlux: [-1] }, { inputFlux: [1e50] },
  ]) assert.throws(() => validateFixture({ ...fixture(), ...change }));
});
