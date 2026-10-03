namespace ReactorSim.Browser
{
    /// <summary>WASM exports use one explicit runtime; tests/tools may own independent runtimes.</summary>
    public static class PlaytestBridgeV2
    {
        private static readonly PlaytestRuntime DefaultRuntime = new PlaytestRuntime();
        public static string GetCapabilities() => PlaytestRuntime.GetCapabilities();
        public static string Initialize(string requestJson) => DefaultRuntime.Initialize(requestJson);
        public static string GetSnapshotJson() => DefaultRuntime.GetSnapshotJson();
        public static string Dispatch(string commandJson) => DefaultRuntime.Dispatch(commandJson);
        public static string DispatchJson(string commandJson) => DefaultRuntime.DispatchJson(commandJson);
        public static string DispatchProfileJson(string commandJson) => DefaultRuntime.DispatchProfileJson(commandJson);
#if RESEARCH_EXPERIMENTS
        public static string GetGpuPrototypeFixtureJson(string requestJson) => DefaultRuntime.GetGpuPrototypeFixtureJson(requestJson);
#endif
        internal static int CoreSnapshotMaterializationCount => DefaultRuntime.CoreSnapshotMaterializationCount;
        internal static void ResetCoreSnapshotMaterializationCount() => DefaultRuntime.ResetCoreSnapshotMaterializationCount();
    }
}
