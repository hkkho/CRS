# Web game architecture

The only developed game is `web/candu-playtest`, deployed to
[GitHub Pages](https://hkkho.github.io/CRS/).

```text
Native DOM/SVG launcher and Reactor Studio
                 |
TypeScript controller / serialized Web Worker
                 |
ReactorSim.BrowserHost (single-threaded browser-WASM)
                 |
ReactorSim.Browser (instance runtime, protocol v2)
                 |
ReactorSim.Game (daily transactions, clock, score, snapshots)
                 |
ReactorSim.Core (inventory, refuelling, poison, diffusion, RRS)
                 |
Project-authored embedded physics packs
```

The browser drafts orders and displays immutable observations. All reactor rules
and terminal decisions belong to the shared simulation. Numerical failures preserve
accepted state; physical operating losses commit their observed outcome. Preserve
group ordering, channel-major indexing, SI units and canonical digests.

Daily turns are the default. A `commit-day` executes eight-bundle orders in canonical
channel order, integrates one large 86,400-second exposure/poison interval, then
solves final equilibrium and LZC response. Detached candidates make numerical
failure atomic. See the [daily contract](gameplay/daily-turn-mode.md).

The real-time path supports older saves and physics comparisons inside the same
web client. Release acceptance exercises daily turns. Publishing disables WASM
threads; obsolete GPU and alternate-host experiments have been removed. Optional
profiling uses the same solver. Native tools support numerical comparison.
Adjoint/diffusion metadata and the IQS metadata pack remain mathematical dependencies;
the separate research engine has been retired. External programs are never runtime
dependencies. See the [guide](IMPLEMENTATION_GUIDE.md), [tools](maintenance/research-tools.md)
and [physics library](maintenance/knowledge-library.md).
