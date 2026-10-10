using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using ReactorSim.Browser;

namespace ReactorSim.BrowserHost
{
    [SupportedOSPlatform("browser")]
    public static partial class BrowserExports
    {
        [JSExport]
        public static string GetCapabilities() => PlaytestBridgeV2.GetCapabilities();
        [JSExport]
        public static string Initialize(string requestJson) => PlaytestBridgeV2.Initialize(requestJson);
        [JSExport]
        public static string GetSnapshotJson() => PlaytestBridgeV2.GetSnapshotJson();
        [JSExport]
        public static string DispatchJson(string commandJson) => PlaytestBridgeV2.DispatchJson(commandJson);
        [JSExport]
        public static string BeginDailyDispatchJson(string commandJson) => PlaytestBridgeV2.BeginDailyDispatchJson(commandJson);
        [JSExport]
        public static string ContinueDailyDispatchJson() => PlaytestBridgeV2.ContinueDailyDispatchJson();
#if RUNTIME_PROFILE
        [JSExport]
        public static string DispatchProfileJson(string commandJson) => PlaytestBridgeV2.DispatchProfileJson(commandJson);
#endif
    }
}
