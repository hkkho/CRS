import { dotnet } from "./_framework/dotnet.js";

const runtime = await dotnet.create();
const config = runtime.getConfig();
const assemblyExports = await runtime.getAssemblyExports(config.mainAssemblyName);
const exports = assemblyExports.ReactorSim.BrowserHost.BrowserExports;

globalThis.canduPlaytestWasm = {
  getCpuHostInfo: () => exports.GetCpuHostInfo(),
  getCapabilities: () => exports.GetCapabilities(),
  initialize: (requestJson) => exports.Initialize(requestJson),
  getSnapshotJson: () => exports.GetSnapshotJson(),
  dispatchJson: (commandJson) => exports.DispatchJson(commandJson),
  ...(typeof exports.DispatchProfileJson === "function" ? { dispatchProfileJson: commandJson => exports.DispatchProfileJson(commandJson) } : {}),
  ...(typeof exports.GetGpuPrototypeFixtureJson === "function" ? { getGpuPrototypeFixtureJson: requestJson => exports.GetGpuPrototypeFixtureJson(requestJson) } : {}),
};

await runtime.runMain();
