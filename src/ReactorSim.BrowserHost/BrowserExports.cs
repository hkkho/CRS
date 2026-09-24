using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using ReactorSim.Browser;

namespace ReactorSim.BrowserHost
{
    [SupportedOSPlatform("browser")]
    public static partial class BrowserExports
    {
        [JSExport]
        public static string GetCapabilities()
        {
            return PlaytestBridgeV2.GetCapabilities();
        }

        [JSExport]
        public static string Initialize(string requestJson)
        {
            return PlaytestBridgeV2.Initialize(requestJson);
        }

        [JSExport]
        public static string GetSnapshotJson()
        {
            return PlaytestBridgeV2.GetSnapshotJson();
        }

        [JSExport]
        public static string DispatchJson(string commandJson)
        {
            return PlaytestBridgeV2.DispatchJson(commandJson);
        }
    }
}
