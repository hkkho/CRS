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
        public string DispatchProfileJson(string commandJson)
        {
            if (!RuntimeProfile.ScopesEnabled)
                throw new NotSupportedException("Build with EnableRuntimeProfiling=true to collect runtime timings.");
            lock (Sync)
            {
                using var capture = RuntimeProfile.Begin();
                string result = Dispatch(commandJson);
                using var stream = new System.IO.MemoryStream();
                using (var writer = new Utf8JsonWriter(stream))
                {
                    writer.WriteStartObject();
                    writer.WriteString("resultJson", result);
                    writer.WriteStartArray("profile");
                    foreach (var row in capture.Rows)
                    {
                        writer.WriteStartObject();
                        writer.WriteString("name", row.Name);
                        writer.WriteNumber("calls", row.Calls);
                        writer.WriteNumber("inclusiveMs", row.InclusiveMs);
                        writer.WriteNumber("exclusiveMs", row.ExclusiveMs);
                        writer.WriteEndObject();
                    }
                    writer.WriteEndArray(); writer.WriteEndObject();
                }
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        /// <summary>Read-only developer fixture. GPU outputs are never accepted by the live session.</summary>
#if RESEARCH_EXPERIMENTS
        public string GetGpuPrototypeFixtureJson(string requestJson)
        {
            using var document = JsonDocument.Parse(requestJson);
            var root = document.RootElement;
            if (root.GetProperty("protocol").GetString() != GpuSpatialPrototypeFixtureV1.Identity)
                throw new ArgumentException("Unsupported GPU prototype protocol.", nameof(requestJson));
            if (root.TryGetProperty("kind", out var kind))
            {
                lock (Sync) { return CoupledGpuExperimentJson(root, kind.GetString()); }
            }
            int group = root.GetProperty("group").GetInt32();
            int iterations = root.GetProperty("iterations").GetInt32();
            lock (Sync)
            {
                var fixture = GpuSpatialPrototypeFixtureV1.Create(_runtime.PlaySession.CurrentSpatialCandidate.SpatialSolve,
                    (SpatialEnergyGroup)group, iterations);
                using var stream = new System.IO.MemoryStream();
                using (var writer = new Utf8JsonWriter(stream))
                {
                    writer.WriteStartObject(); writer.WriteString("protocol", GpuSpatialPrototypeFixtureV1.Identity);
                    writer.WriteNumber("nodeCount", fixture.NodeCount); writer.WriteNumber("group", fixture.Group);
                    writer.WriteNumber("iterations", fixture.Iterations); writer.WriteString("coefficientDigestHex", fixture.CoefficientDigestHex);
                    writer.WriteString("kernelDigestHex", fixture.KernelDigestHex); writer.WriteString("shaderSource", fixture.ShaderSource);
                    writer.WriteNumber("referenceRelativeResidual", fixture.ReferenceRelativeResidual);
                    writer.WriteNumber("requiredRelativeTolerance", fixture.RequiredRelativeTolerance);
                    writer.WriteNumber("cpuOperatorMs", fixture.CpuOperatorMilliseconds);
                    writer.WriteNumber("cpuIterationMs", fixture.CpuIterationMilliseconds);
                    writer.WriteStartArray("rowOffsets"); foreach (uint value in fixture.RowOffsets) writer.WriteNumberValue(value); writer.WriteEndArray();
                    writer.WriteStartArray("targets"); foreach (uint value in fixture.Targets) writer.WriteNumberValue(value); writer.WriteEndArray();
                    writer.WriteStartArray("nodeData"); foreach (float value in fixture.NodeData) writer.WriteNumberValue(value); writer.WriteEndArray();
                    writer.WriteStartArray("conductances"); foreach (float value in fixture.Conductances) writer.WriteNumberValue(value); writer.WriteEndArray();
                    writer.WriteStartArray("inputFlux"); foreach (float value in fixture.InputFlux) writer.WriteNumberValue(value); writer.WriteEndArray();
                    writer.WriteStartArray("referenceApplied"); foreach (double value in fixture.ReferenceApplied) writer.WriteNumberValue(value); writer.WriteEndArray();
                    writer.WriteStartArray("referenceFlux"); foreach (double value in fixture.ReferenceFlux) writer.WriteNumberValue(value); writer.WriteEndArray();
                    writer.WriteEndObject();
                }
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        private string CoupledGpuExperimentJson(JsonElement root, string? kind)
        {
            using var stream = new System.IO.MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                if (kind == "coupled")
                {
                    var fixture = GpuCoupledSpatialFixtureV1.Create(_runtime.PlaySession.CurrentSpatialCandidate.SpatialSolve,
                        root.TryGetProperty("cold", out var cold) && cold.GetBoolean(), _runtime.PlaySession.CurrentLiquidZoneRrs.Mapping);
                    _coupledExperiment = fixture; _coupledExperimentRuntime = _runtime; _coupledExperimentId = Guid.NewGuid().ToString("N");
                    writer.WriteString("protocol", GpuCoupledSpatialFixtureV1.Identity); writer.WriteString("experimentId", _coupledExperimentId);
                    writer.WriteString("coefficientDigest", fixture.CoefficientDigest); writer.WriteString("shaderSource", fixture.ShaderSource);
                    writer.WriteString("kernelDigest", fixture.KernelDigest);
                    writer.WriteNumber("nodeCount", fixture.NodeCount); writer.WriteNumber("initialK", fixture.InitialK);
                    writer.WriteNumber("targetPower", fixture.TargetPower); writer.WriteNumber("referenceK", fixture.ReferenceK);
                    writer.WriteNumber("cpuSolveMs", fixture.CpuSolveMilliseconds); writer.WriteNumber("cpuIterations", fixture.CpuIterations);
                    writer.WriteNumber("maxInner", fixture.InnerPolicy.MaximumInnerIterations); writer.WriteNumber("maxOuter", fixture.OuterPolicy.MaximumIterations);
                    writer.WriteNumber("innerRelative", fixture.InnerPolicy.RelativeResidualTolerance); writer.WriteNumber("innerAbsolute", fixture.InnerPolicy.AbsoluteResidualTolerance);
                    writer.WriteNumber("kAbsolute", fixture.OuterPolicy.KAbsoluteTolerance); writer.WriteNumber("kRelative", fixture.OuterPolicy.KRelativeTolerance);
                    writer.WriteNumber("outerResidual", fixture.OuterPolicy.ResidualTolerance); writer.WriteNumber("sourceShape", fixture.OuterPolicy.SourceShapeTolerance);
                    writer.WriteNumber("regionalAgreementTolerancePercentagePoints", PracticeLiquidZoneRrsIdentityV1.RegionalShapeTolerancePercentagePoints);
                    writer.WriteNumber("reactivityAgreementToleranceMk", PracticeLiquidZoneRrsIdentityV1.CriticalityToleranceMk);
                    writer.WriteStartArray("indices"); foreach (uint value in fixture.Indices) writer.WriteNumberValue(value); writer.WriteEndArray();
                    writer.WriteStartArray("nodes"); foreach (float value in fixture.Nodes) writer.WriteNumberValue(value); writer.WriteEndArray();
                    writer.WriteStartArray("conductances"); foreach (float value in fixture.Conductances) writer.WriteNumberValue(value); writer.WriteEndArray();
                    writer.WriteStartArray("initialFlux"); foreach (double value in fixture.InitialFlux) writer.WriteNumberValue(value); writer.WriteEndArray();
                }
                else if (kind == "verify-coupled")
                {
                    var fixture = _coupledExperiment ?? throw new InvalidOperationException("No pending GPU experiment.");
                    if (!ReferenceEquals(_runtime, _coupledExperimentRuntime) || root.GetProperty("experimentId").GetString() != _coupledExperimentId ||
                        fixture.CoefficientDigest != Convert.ToHexStringLower(_runtime.PlaySession.CurrentSpatialCandidate.SpatialSolve.CoefficientBindingDigest.Bytes.ToArray()))
                        throw new InvalidOperationException("Stale GPU experiment rejected.");
                    double[] ReadFlux(string name)
                    {
                        var array = root.GetProperty(name);
                        if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() != fixture.NodeCount * 2) throw new ArgumentException("GPU flux dimension mismatch.");
                        return array.EnumerateArray().Select(x => x.GetDouble()).ToArray();
                    }
                    var check = fixture.Validate(root.GetProperty("k").GetDouble(), root.GetProperty("previousK").GetDouble(),
                        ReadFlux("flux"), ReadFlux("previousFlux"), root.GetProperty("iterations").GetInt32());
                    writer.WriteBoolean("cpuVerifiedConverged", check.CpuVerifiedConverged); writer.WriteBoolean("agreementPassed", check.AgreementPassed);
                    writer.WriteNumber("residual", check.Residual); writer.WriteNumber("shapeChange", check.ShapeChange); writer.WriteNumber("powerBalance", check.PowerBalance);
                    writer.WriteNumber("fluxError", check.FluxError); writer.WriteNumber("reactivityErrorMk", check.ReactivityErrorMk);
                    writer.WriteNumber("group1FluxError", check.Group1FluxError); writer.WriteNumber("group2FluxError", check.Group2FluxError);
                    writer.WriteNumber("nodePowerError", check.NodePowerError); writer.WriteNumber("regionalFractionError", check.RegionalFractionError);
                    writer.WriteBoolean("regionsChecked", check.RegionsChecked);
                }
                else { throw new ArgumentException("Unknown GPU experiment kind."); }
                writer.WriteEndObject();
            }
            return Encoding.UTF8.GetString(stream.ToArray());
        }

#endif
    }
}
