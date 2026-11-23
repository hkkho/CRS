# CRS browser client

The DOM/SVG browser client displays the shared C# simulation through a dedicated Web Worker. The public entry point offers time-based play and the Custom physics sandbox. The simulation, rather than the client, owns reactor rules and accepted state.

- [Local setup](../../README.md#develop-and-verify)
- [Controls and scoring](../../docs/code-library/manual/index.md)
- [Worker, transactions and presentation](../../docs/code-library/index.md)
- [Pages build and verification](../../docs/maintenance/hosting.md)

From the repository root, run `tools/Build-BrowserWasm.ps1` to generate `public/wasm`. From this directory, run `npm test` and `npm run build`, or run `tools/Test-Browser.ps1` from the root to include shared bridge tests.
