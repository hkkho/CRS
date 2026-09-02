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
            return PlaytestBridgeV1.GetCapabilities();
        }

        [JSExport]
        public static string Initialize(string requestJson)
        {
            return PlaytestBridgeV1.Initialize(requestJson);
        }

        [JSExport]
        public static string GetSnapshotJson()
        {
            return PlaytestBridgeV1.GetSnapshotJson();
        }

        [JSExport]
        public static string DispatchJson(string commandJson)
        {
            return PlaytestBridgeV1.DispatchJson(commandJson);
        }
    }
}
