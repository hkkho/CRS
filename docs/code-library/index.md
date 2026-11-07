# CRS documentation

Start with the [player manual](manual/index.md) for time-based play and Custom physics. The [reactor theory](theory/index.md) explains the readings and the numerical model.

## Code walkthrough

| Chapter | What to trace |
| --- | --- |
| [Startup](01-startup.md) | Launcher, seed, custom setup and the Start gate |
| [Transport](02-browser-transport.md) | Worker queues, managed exports and full or compact responses |
| [A half-day step](03-daily-turn.md) | Fuel batch, exposure and the detached simulation transaction |
| [Core physics](04-core-physics.md) | Topology, bundle identities, material binding, diffusion and regulation |
| [Presentation](05-presentation.md) | From authoritative powers and inventory to the map and trends |
| [Checkpoints](06-saves-lifecycle.md) | Replay, custom settings and automatic updates |
| [Development](07-development.md) | Build, verify and maintain these pages |

The walkthrough embeds source excerpts from the checked-out revision. Each passage links to the corresponding source lines. On narrow screens its explanation appears below the code.

See the [implementation guide](../IMPLEMENTATION_GUIDE.md) for architecture and the [browser protocol](../spec/browser-playtest-protocol-v2.md) for wire compatibility.

[Play CRS](https://hkkho.github.io/CRS/)
