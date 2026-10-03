// Core-owned experimental numerical kernel. No game state is committed by this kernel.
struct Params { count: u32, targetStart: u32, unused1: u32, unused2: u32 }
struct Node { removal: f32, volume: f32, diagonal: f32, source: f32 }
@group(0) @binding(0) var<uniform> params: Params;
@group(0) @binding(1) var<storage, read> indices: array<u32>;
@group(0) @binding(2) var<storage, read> nodes: array<Node>;
@group(0) @binding(3) var<storage, read> conductances: array<f32>;
@group(0) @binding(4) var<storage, read> flux: array<f32>;
@group(0) @binding(5) var<storage, read_write> applied: array<f32>;
@group(0) @binding(6) var<storage, read_write> nextFlux: array<f32>;
@group(0) @binding(7) var<storage, read_write> status: atomic<u32>;

fn finite(value: f32) -> bool { return abs(value) <= 3.4028234663852886e38; }

@compute @workgroup_size(64)
fn applyOperator(@builtin(global_invocation_id) id: vec3<u32>) {
    let i = id.x;
    if (i >= params.count) { return; }
    let x = flux[i];
    if (!finite(x) || x < 0.0) { atomicOr(&status, 1u); return; }
    var leakage = 0.0;
    for (var edge = indices[i]; edge < indices[i + 1u]; edge++) {
        let neighborIndex = indices[params.targetStart + edge];
        var difference = x;
        if (neighborIndex != 0xffffffffu) { difference = x - flux[neighborIndex]; }
        let term = conductances[edge] * difference;
        leakage = leakage + term;
        if (!finite(term) || !finite(leakage)) { atomicOr(&status, 2u); return; }
    }
    let result = nodes[i].removal * x + leakage / nodes[i].volume;
    if (!finite(result)) { atomicOr(&status, 2u); return; }
    applied[i] = result;
}

@compute @workgroup_size(64)
fn jacobiStep(@builtin(global_invocation_id) id: vec3<u32>) {
    let i = id.x;
    if (i >= params.count) { return; }
    let value = flux[i] + (nodes[i].source - applied[i]) / nodes[i].diagonal;
    if (!finite(value) || value < 0.0) { atomicOr(&status, 4u); return; }
    nextFlux[i] = value;
}
