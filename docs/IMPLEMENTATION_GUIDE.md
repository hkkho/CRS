# CANDU web game implementation guide

This guide defines the responsibilities and acceptance path for `web/candu-playtest`. The public launcher offers time-based free practice, a tutorial and Custom physics. Verify changes on the GitHub Pages path. Shutdown, scram, accidents, operator training and full plant operations are outside scope.

## Architecture and ownership

| Layer | Responsibility |
| --- | --- |
| `web/candu-playtest` | Launcher, DOM/SVG presentation, input, replay journal and serialized worker transport |
| `ReactorSim.BrowserHost` | Single-threaded .NET browser-WASM host and JavaScript exports |
| `ReactorSim.Browser` | Versioned wire protocol, instance runtime and full/compact snapshot mapping |
| `ReactorSim.Game` | Clock, transactions, scoring, terminal decisions and immutable presentation snapshots |
| `ReactorSim.Core` | Topology, fuel inventory, burnup, poison, diffusion and liquid-zone regulation |

The client fails closed without WASM. Keep reactor rules in the shared simulation and preserve SI units, fast/thermal group order, channel-major indexing and deterministic digests. Use the [wire protocol](spec/browser-playtest-protocol-v2.md) and [code walkthrough](code-library/index.md) when changing these boundaries.

## Time-based play

The public client initializes `pacingMode: demo`. `DemoLoop` submits the waiting fuel batch through `commit-day`. Game validates the batch, applies each eight-bundle move with channel flow in canonical order, then solves the combined inventory. If the core stays within its limits, Game integrates one 43,200-second interval from the post-refuelling power and flux and solves the endpoint. The browser interpolates solved endpoints over 7.5 seconds by default, 10 seconds at 0.5×, or 5 seconds at 1.5×. It solves one endpoint ahead of playback. Slow solves hold the last endpoint. Pause and hidden tabs freeze playback and preserve waiting orders. Players can add and remove waiting orders while paused. Submitted orders follow the shared transaction through to the displayed endpoint.

Game applies its operating limits to the solved readings. Numerical failure discards the detached candidate. A combined fuel batch that crosses a limit commits every order and ends the run at the current simulation time. A violation at the interval endpoint commits the elapsed time and stops play. See [shared step semantics](gameplay/daily-turn-mode.md) and [limits and scoring](gameplay/score-balance.md). Daily turns and real-time commands remain available for compatibility tests and comparison tools.

## Physics and custom runs

The [geometry fit](../data/calibration/diffusion-v10/README.md) uses circular radial leakage and axial zero-flux extrapolation 0.22 m beyond each end. [Core's solver](physics/active-two-group-solver.md) binds fuel coefficients by burnup and carries poison with bundle identity. The [xenon calibration](physics/xenon-calibration.md) fits capture and equilibrium worth separately. Startup solves the aged inventory against a [time-average reference](physics/channel-power-reference.md) derived with zones at half fill, then settles xenon and regulation without spending fuel or time.

Custom physics applies numeric overrides to the embedded pack and per-pack practice settings before startup. The shared loader validates the parameters and Game recomputes the reference and initial equilibrium. Custom runs have modified sandbox provenance and a content-based pack ID. They retain the settings across seed resets and local update checkpoints.

### Physics profile format and extension path

The editor loads and saves UTF-8 JSON profiles up to 64 KiB. A profile declares its format, schema version, model, base pack revision and units. It stores only edited values. The example changes nominal thermal power and the energy per fission at fuel knot 1 (zero-based).

```json
{
  "format": "crs-physics-profile",
  "version": 1,
  "model": "candu6-two-group-full-core-diffusion-v1",
  "basePack": "candu6-two-group-diffusion-v1-xenon-v11",
  "units": "SI-v1",
  "parameters": {
    "practice": { "thermal_power_multiplier": 0.98 },
    "fuel_rows": { "1": { "energy_per_fission_j": 3.2e-11 } }
  }
}
```

The reader for the profile format shown above accepts `practice`, `geometry`, `adjusters`, `solver`, `fuel_rows` and `xenon_rows`. Rows refer to zero-based indices in the pinned base pack. Geometry retains the pack's named subsections. A profile with no edits uses `"parameters": {}`. Duplicate properties, unknown fields, unsupported model or units, and mismatched base revisions are rejected. The Core loader resolves the same overrides as older clients and maintains derived xenon knots, modified provenance and content identity. A base pack change requires a revision bump and an explicit migration if older profile files are to be carried forward.

JSON lets the browser and .NET read small files with their existing parsers, and text diffs show the exact changes. YAML also supports structured text and adds features such as aliases and tags that this editor does not need. HDF5 supports typed multidimensional datasets and chunking, which becomes useful when data must be read in slices. [JSON specification](https://www.rfc-editor.org/rfc/rfc8259) · [YAML specification](https://yaml.org/spec/1.2.2/) · [HDF5 dataset layout](https://support.hdfgroup.org/documentation/hdf5/latest/_l_b_dset_layout.html)

When the simulator gains a second model, introduce a typed payload and Core reader for that model, with an explicit topology and solver compatibility check. Give meshes, energy groups and fuel tables stable IDs. Carry ordered axes and units with coefficient tables so group order, coordinate conventions and array shapes are verifiable. Connect each payload to a dedicated editor section or table backed by the same model contract.

Define scenario inputs separately when a supported time-dependent model needs them. A scenario should reference its compatible reactor and fuel data and identify its time axis, initial conditions and permitted inputs. Keep generated power and isotope histories with run outputs. For large mesh, kinetics or history arrays, evaluate an HDF5 dataset linked from a versioned manifest by content hash, dataset name, units and axis order. Agree on those datasets when a model needs them.

## Presentation and lifecycle

Interpolate continuous readings between accepted endpoints for display; do not treat the interpolation as a solved physical state. Show fuel movements and terminal outcomes at the accepted endpoint. Preserve keyboard access, 320px layouts, reduced motion and focus on recovery. Dispose worker, listeners and timers when leaving a run.

Update checkpoints store the latest solved endpoint as replayable inputs plus waiting orders. IndexedDB holds the journal and session storage holds its pointer and custom settings. Restore verifies pack, scoring policy, cadence, command acceptance and the final state digest before enabling Start. Failed writes prevent reload. Incompatible checkpoints and older daily saves remain stored.

## Verification

Use `tools/Test-DotNet.ps1` and `tools/Test-Browser.ps1` in Release. Follow the [Pages verification guide](maintenance/hosting.md) for gameplay, custom physics, update restore, documentation and direct WASM reproduction.
