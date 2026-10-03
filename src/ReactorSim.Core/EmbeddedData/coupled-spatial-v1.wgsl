// Core-owned complete two-group source/eigen iteration. SI units, fast then thermal.
// GPU convergence is provisional: C# independently checks returned states in f64.
struct Parameters {
    count: u32, targetStart: u32, blocks: u32, maxInner: u32,
    targetPower: f32, initialK: f32, innerRelative: f32, innerAbsolute: f32,
    kAbsolute: f32, kRelative: f32, outerResidual: f32, sourceShape: f32,
}
struct Material { a: vec4<f32>, b: vec4<f32>, c: vec4<f32> }
struct Partial { a: vec4<f32>, b: vec4<f32> }
struct Control {
    k: f32, oldK: f32, scale: f32, oldProduction: f32,
    production: f32, power: f32, deltaK: f32, shape: f32,
    residual: f32, innerResidual: f32, iteration: u32, done: u32,
    failure: u32, group: u32, innerDone: u32, pad: u32,
}
@group(0) @binding(0) var<uniform> p: Parameters;
@group(0) @binding(1) var<storage, read> indices: array<u32>;
@group(0) @binding(2) var<storage, read> materials: array<Material>;
@group(0) @binding(3) var<storage, read> edges: array<vec2<f32>>;
// Per node: current[2], scratch[2], source[2], previous[2], frozen fission, padding[3].
@group(0) @binding(4) var<storage, read_write> state: array<f32>;
@group(0) @binding(5) var<storage, read_write> partials: array<Partial>;
@group(0) @binding(6) var<storage, read_write> control: Control;
@group(0) @binding(7) var<storage, read_write> flags: atomic<u32>;
var<workgroup> sharedA: array<vec4<f32>, 64>;
var<workgroup> sharedB: array<vec4<f32>, 64>;
fn finite(x: f32) -> bool { return abs(x) <= 3.4028234663852886e38; }
fn isRunning() -> bool { return control.done == 0u && control.failure == 0u; }
fn production(i: u32) -> f32 { return fma(materials[i].b.z, state[i*12u], materials[i].b.w * state[i*12u+1u]); }
fn power(i: u32) -> f32 { return fma(materials[i].c.z, state[i*12u], materials[i].c.w * state[i*12u+1u]); }
fn left(i: u32, slot: u32, groupIndex: u32) -> f32 {
    let x = state[i*12u+slot+groupIndex]; var leakage = 0.0; var correction = 0.0;
    for (var edge = indices[i]; edge < indices[i+1u]; edge++) {
        let neighbor = indices[p.targetStart+edge]; var difference = x;
        if (neighbor != 0xffffffffu) { difference = x - state[neighbor*12u+slot+groupIndex]; }
        // Compensated accumulation reduces cancellation without changing acceptance thresholds.
        let adjusted = edges[edge][groupIndex] * difference - correction;
        let next = leakage + adjusted;
        correction = (next - leakage) - adjusted;
        leakage = next;
    }
    return fma(materials[i].a[groupIndex], x, leakage / materials[i].a.z);
}
fn sumPart(id: u32, block: u32, a: vec4<f32>) {
    sharedA[id] = a; workgroupBarrier();
    for (var width = 32u; width > 0u; width /= 2u) {
        if (id < width) { sharedA[id] += sharedA[id+width]; } workgroupBarrier();
    }
    if (id == 0u) { partials[block].a = sharedA[0]; }
}
fn maxPart(id: u32, block: u32, a: vec4<f32>, b: vec4<f32>) {
    sharedA[id] = a; sharedB[id] = b; workgroupBarrier();
    for (var width = 32u; width > 0u; width /= 2u) {
        if (id < width) { sharedA[id] = max(sharedA[id], sharedA[id+width]); sharedB[id] = max(sharedB[id], sharedB[id+width]); }
        workgroupBarrier();
    }
    if (id == 0u) { partials[block] = Partial(sharedA[0], sharedB[0]); }
}
@compute @workgroup_size(1) fn initialize() { control.k = p.initialK; control.oldK = p.initialK; control.scale = 1.0; }
@compute @workgroup_size(64) fn beginOuter(@builtin(global_invocation_id) gid: vec3<u32>, @builtin(local_invocation_index) lane: u32, @builtin(workgroup_id) block: vec3<u32>) {
    let i = gid.x; var totals = vec4<f32>(0.0);
    if (i < p.count && isRunning()) {
        let f = production(i); state[i*12u+8u] = f;
        state[i*12u+6u] = state[i*12u]; state[i*12u+7u] = state[i*12u+1u];
        state[i*12u+4u] = materials[i].c.x * f / control.k;
        totals = vec4<f32>(f*materials[i].a.z, power(i), 0.0, 0.0);
        if (!finite(f) || f < 0.0) { atomicOr(&flags, 1u); }
    }
    sumPart(lane, block.x, totals);
}
@compute @workgroup_size(1) fn reduceBegin() {
    if (!isRunning()) { return; }
    var total = vec4<f32>(0.0); for (var i = 0u; i < p.blocks; i++) { total += partials[i].a; }
    control.oldProduction = total.x; control.oldK = control.k; control.group = 0u; control.innerDone = 0u;
    if (!finite(total.x) || total.x <= 0.0 || atomicLoad(&flags) != 0u) { control.failure = 1u; }
}
fn jacobi(i: u32, inputSlot: u32, outputSlot: u32) {
    if (!isRunning() || control.innerDone != 0u || i >= p.count) { return; }
    let g = control.group; let x = state[i*12u+inputSlot+g];
    let value = x + (state[i*12u+4u+g] - left(i,inputSlot,g)) / materials[i].b[g];
    if (!finite(value) || value < 0.0) { atomicOr(&flags, 2u); return; }
    state[i*12u+outputSlot+g] = value;
}
@compute @workgroup_size(64) fn jacobiEven(@builtin(global_invocation_id) gid: vec3<u32>) { jacobi(gid.x,0u,2u); }
@compute @workgroup_size(64) fn jacobiOdd(@builtin(global_invocation_id) gid: vec3<u32>) { jacobi(gid.x,2u,0u); }
@compute @workgroup_size(64) fn innerMetrics(@builtin(global_invocation_id) gid: vec3<u32>, @builtin(local_invocation_index) lane: u32, @builtin(workgroup_id) block: vec3<u32>) {
    let i = gid.x; var m = vec4<f32>(0.0);
    if (i < p.count && isRunning()) { let l = left(i,0u,control.group); let r = state[i*12u+4u+control.group]; m = vec4<f32>(abs(l-r),abs(l)+abs(r),0.0,0.0); }
    maxPart(lane,block.x,m,vec4<f32>(0.0));
}
@compute @workgroup_size(1) fn reduceInner() {
    if (!isRunning()) { return; }
    var m = vec4<f32>(0.0); for (var i=0u; i<p.blocks; i++) { m=max(m,partials[i].a); }
    var relative = 0.0; if (m.y > 0.0) { relative = m.x/m.y; }
    control.innerResidual = relative;
    if (!finite(relative) || atomicLoad(&flags) != 0u) { control.failure = 2u; }
    if (m.x <= p.innerAbsolute || relative <= p.innerRelative) { control.innerDone = 1u; }
}
@compute @workgroup_size(1) fn beginGroup2() {
    if (!isRunning()) { return; }
    if (control.innerDone == 0u) { control.failure = 3u; return; }
    control.group = 1u; control.innerDone = 0u;
}
@compute @workgroup_size(64) fn sourceGroup2(@builtin(global_invocation_id) gid: vec3<u32>) {
    let i = gid.x; if (!isRunning() || i >= p.count) { return; }
    state[i*12u+5u] = materials[i].a.w * state[i*12u] + materials[i].c.y * state[i*12u+8u]/control.k;
}
@compute @workgroup_size(64) fn trialMetrics(@builtin(global_invocation_id) gid: vec3<u32>, @builtin(local_invocation_index) lane: u32, @builtin(workgroup_id) block: vec3<u32>) {
    let i = gid.x; var m = vec4<f32>(0.0);
    if (i < p.count && isRunning()) { m=vec4<f32>(production(i)*materials[i].a.z,power(i),0.0,0.0); }
    sumPart(lane,block.x,m);
}
@compute @workgroup_size(1) fn reduceTrial() {
    if (!isRunning()) { return; }
    if (control.innerDone == 0u) { control.failure = 4u; return; }
    var m = vec4<f32>(0.0); for (var i=0u;i<p.blocks;i++) { m+=partials[i].a; }
    control.production=m.x; control.power=m.y;
    control.k=control.oldK*(m.x/control.oldProduction); control.scale=p.targetPower/m.y;
    if (!finite(control.k) || control.k<=0.0 || !finite(control.scale) || control.scale<=0.0) { control.failure=5u; }
}
@compute @workgroup_size(64) fn normalize(@builtin(global_invocation_id) gid: vec3<u32>) {
    let i=gid.x; if (!isRunning() || i>=p.count) { return; }
    state[i*12u] *= control.scale; state[i*12u+1u] *= control.scale;
    if (!finite(state[i*12u]) || !finite(state[i*12u+1u])) { atomicOr(&flags,4u); }
}
@compute @workgroup_size(64) fn outerMetrics(@builtin(global_invocation_id) gid: vec3<u32>, @builtin(local_invocation_index) lane: u32, @builtin(workgroup_id) block: vec3<u32>) {
    let i=gid.x; var m=vec4<f32>(0.0);
    if (i<p.count && isRunning()) {
        let f=production(i); let rhs1=materials[i].c.x*f/control.k;
        let rhs2=materials[i].a.w*state[i*12u]+materials[i].c.y*f/control.k;
        let lhs1=left(i,0u,0u); let lhs2=left(i,0u,1u);
        let oldShape=materials[i].a.z*state[i*12u+8u]/control.oldProduction;
        let newShape=materials[i].a.z*f/(control.production*control.scale);
        m=vec4<f32>(max(abs(lhs1-rhs1),abs(lhs2-rhs2)),max(abs(lhs1)+abs(rhs1),abs(lhs2)+abs(rhs2)),abs(newShape-oldShape),0.0);
        if (!finite(m.x) || !finite(m.y) || !finite(m.z)) { atomicOr(&flags,8u); }
    }
    maxPart(lane,block.x,m,vec4<f32>(0.0));
}
@compute @workgroup_size(1) fn reduceOuter() {
    if (!isRunning()) { return; }
    var m=vec4<f32>(0.0); for (var i=0u;i<p.blocks;i++) { m=max(m,partials[i].a); }
    control.iteration++; control.residual=m.x/m.y; control.shape=m.z; control.deltaK=abs(control.k-control.oldK);
    if (!finite(control.residual) || atomicLoad(&flags)!=0u) { control.failure=6u; return; }
    let kPassed=control.deltaK<=p.kAbsolute || control.deltaK/max(control.k,control.oldK)<=p.kRelative;
    if (kPassed && control.residual<=p.outerResidual && control.shape<=p.sourceShape) { control.done=1u; }
}
