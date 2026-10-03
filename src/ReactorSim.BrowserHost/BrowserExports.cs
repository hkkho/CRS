using System;
using System.Threading.Tasks;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using ReactorSim.Browser;

namespace ReactorSim.BrowserHost
{
    [SupportedOSPlatform("browser")]
    public static partial class BrowserExports
    {
#if CPU_PARALLEL
        private static readonly SerializedCommandQueue Queue = new SerializedCommandQueue();

        [JSExport]
        public static Task<string> GetCpuHostInfo()
        {
            int entryThread = Environment.CurrentManagedThreadId;
            return Queue.Enqueue(() => System.FormattableString.Invariant(
                $"{{\"entryThreadId\":{entryThread},\"commandThreadId\":{Environment.CurrentManagedThreadId},\"partitions\":{ReactorSim.Core.SpatialOperator.DefaultWorkerCount}}}"));
        }
        [JSExport]
        public static Task<string> GetCapabilities()
        {
            return Queue.Enqueue(() => PlaytestBridgeV2.GetCapabilities());
        }

        [JSExport]
        public static Task<string> Initialize(string requestJson)
        {
            return Queue.Enqueue(() => PlaytestBridgeV2.Initialize(requestJson));
        }

        [JSExport]
        public static Task<string> GetSnapshotJson()
        {
            return Queue.Enqueue(() => PlaytestBridgeV2.GetSnapshotJson());
        }

        [JSExport]
        public static Task<string> DispatchJson(string commandJson)
        {
            return Queue.Enqueue(() => PlaytestBridgeV2.DispatchJson(commandJson));
        }

#if RUNTIME_PROFILE
        [JSExport]
        public static Task<string> DispatchProfileJson(string commandJson)
        {
            return Queue.Enqueue(() => PlaytestBridgeV2.DispatchProfileJson(commandJson));
        }
#endif

#if RESEARCH_EXPERIMENTS
        [JSExport]
        public static Task<string> GetGpuPrototypeFixtureJson(string requestJson)
        {
            return Queue.Enqueue(() => PlaytestBridgeV2.GetGpuPrototypeFixtureJson(requestJson));
        }
#endif
#else
        [JSExport]
        public static string GetCpuHostInfo()
        {
            return System.FormattableString.Invariant(
                $"{{\"entryThreadId\":{Environment.CurrentManagedThreadId},\"commandThreadId\":{Environment.CurrentManagedThreadId},\"partitions\":1}}");
        }

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

#if RUNTIME_PROFILE
        [JSExport]
        public static string DispatchProfileJson(string commandJson)
        {
            return PlaytestBridgeV2.DispatchProfileJson(commandJson);
        }
#endif

#if RESEARCH_EXPERIMENTS
        [JSExport]
        public static string GetGpuPrototypeFixtureJson(string requestJson)
        {
            return PlaytestBridgeV2.GetGpuPrototypeFixtureJson(requestJson);
        }
#endif
#endif
    }
}
