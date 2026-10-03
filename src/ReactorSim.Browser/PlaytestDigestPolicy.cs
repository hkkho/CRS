using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Text.Json;
using System.Text.Json.Serialization;
using ReactorSim.Core;
using ReactorSim.Game;

namespace ReactorSim.Browser
{
    public sealed partial class PlaytestRuntime
    {
        private static string ComputeStateDigest(
            BridgeRuntime runtime,
            PlaytestSnapshotDto snapshot)
        {
            string snapshotJson = PlaytestProtocolV2.Serialize(snapshot);
            using JsonDocument document = JsonDocument.Parse(snapshotJson);
            string canonical = PlaytestProtocolV2.CanonicalizeJson(document.RootElement);
            return PlaytestProtocolV2.ComputeDigest(canonical);
        }

        private static string ComputeCompactStateDigest(
            BridgeRuntime runtime,
            GameSessionSnapshot game,
            PlaytestSnapshotPatchDto patch)
        {
#if RUNTIME_PROFILE
            using var profileScope = ReactorSim.Core.RuntimeProfile.Measure("bridge-digest");
#endif
            string patchJson = PlaytestProtocolV2.Serialize(patch);
            using JsonDocument document = JsonDocument.Parse(patchJson);
            string canonical = PlaytestProtocolV2.CompactStateDigestAlgorithm +
                "|sequence=" + runtime.Sequence.ToString(CultureInfo.InvariantCulture) +
                "|mode=" + runtime.Mode +
                "|patch=" + PlaytestProtocolV2.CanonicalizeJson(document.RootElement) +
                "|core=" + ComputeCompactCoreIdentity(runtime, game);
            return PlaytestProtocolV2.ComputeDigest(canonical);
        }

        private static string ComputeCompactCoreIdentity(
            BridgeRuntime runtime,
            GameSessionSnapshot game)
        {
            EquilibriumCoreProjectionV1 candidate = runtime.PlaySession.CurrentSpatialCandidate;
            StringBuilder identity = new StringBuilder();
            identity.Append("binding-version=")
                .Append(game.Physics.BindingVersion.ToString(CultureInfo.InvariantCulture))
                .Append("|physics-binding=")
                .Append(game.Physics.ReactivityBindingDigestHex)
                .Append("|xenon-version=")
                .Append(game.Xenon.StateVersion.ToString(CultureInfo.InvariantCulture))
                .Append("|xenon=")
                .Append(game.Xenon.StateDigestHex)
                .Append("|inventory=")
                .Append(FormatDigest(candidate.SpatialSolve.InventoryBindingDigest))
                .Append("|coefficients=")
                .Append(FormatDigest(candidate.SpatialSolve.CoefficientBindingDigest))
                .Append("|candidate-reactivity=")
                .Append(candidate.ReactivityBindingDigestHex);
            return identity.ToString();
        }

        private static string FormatDigest(Digest32 digest)
        {
            StringBuilder result = new StringBuilder(digest.Bytes.Count * 2);
            foreach (byte value in digest.Bytes)
            {
                result.Append(value.ToString("x2", CultureInfo.InvariantCulture));
            }

            return result.ToString();
        }

        private static string ComputeReplayDigest(BridgeRuntime runtime)
        {
            return runtime.Replay.Digest;
        }

    }
}
