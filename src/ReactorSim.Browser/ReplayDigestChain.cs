using ReactorSim.Game;

namespace ReactorSim.Browser
{
    /// <summary>
    /// Constant retained state. Replay commands are streamed in dispatch responses;
    /// callers that need a replay must record initialization and those responses.
    /// This digest commits to the stream but cannot reconstruct it.
    /// </summary>
    internal sealed class ReplayDigestChain
    {
        public ReplayDigestChain(string mode, string initializationJson)
        {
            Digest = PlaytestProtocolV2.ComputeDigest(
                PlaytestProtocolV2.ReplayDigestAlgorithm + "|" +
                PracticeScoring.PolicyId + "|" + mode + "|" + initializationJson);
        }

        public string Digest { get; private set; }

        public void Append(string canonicalCommand)
        {
#if RUNTIME_PROFILE
            using var profileScope = ReactorSim.Core.RuntimeProfile.Measure("replay-digest");
#endif
            Digest = PlaytestProtocolV2.ComputeDigest(Digest + "|" + canonicalCommand);
        }
    }
}
