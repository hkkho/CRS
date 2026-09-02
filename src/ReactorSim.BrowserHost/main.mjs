import { dotnet } from "./_framework/dotnet.js";

const runtime = await dotnet.create();
const config = runtime.getConfig();
const assemblyExports = await runtime.getAssemblyExports(config.mainAssemblyName);
const exports = assemblyExports.ReactorSim.BrowserHost.BrowserExports;

globalThis.canduPlaytestWasm = {
  getCapabilities: () => exports.GetCapabilities(),
  initialize: (requestJson) => exports.Initialize(requestJson),
  getSnapshotJson: () => exports.GetSnapshotJson(),
  dispatchJson: (commandJson) => exports.DispatchJson(commandJson),
};

await runtime.runMain();
