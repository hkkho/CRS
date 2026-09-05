using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ReactorSim.Core
{
    /// <summary>
    /// Validated synthetic kinetics metadata for the adiabatic point-kinetics /
    /// static-eigenmode adapter. The historical IQS type name is kept for
    /// compatibility; this pack does not describe a fixed-source IQS shape
    /// solve. Spatial coefficients remain in the separately versioned
    /// diffusion pack.
    /// </summary>
    public sealed class IqsKineticsDataPackV1
    {
        public const uint CurrentSchemaVersion = 1;
        public const string SupportedTopologySchemaId = "candu6-380x12-grid-v1";
        public const string SupportedUnitsProfileId = "SI-v1";
        public const string SupportedModelId = "candu6-two-group-adiabatic-point-kinetics-full-core-v1";
        public const string SupportedSolverId = "adiabatic-point-kinetics-static-eigenmode-v1";
        public const string SupportedFormulationId = "adiabatic-static-eigenmode-plus-point-kinetics-v1";
        public const string SupportedShapeMethodId = "static-eigenmode-diffusion-source-iteration-v1";
        public const string SupportedAmplitudeMethodId = "point-kinetics-semi-implicit-precursor-exponential-v1";
        public const string SupportedReactivityDiagnosticId = "adjoint-weighted-first-order-perturbation-v1";
        public const string LegacySupportedModelId = "candu6-two-group-iqs-full-core-v1";
        public const string LegacySupportedSolverId = "spatial-eigen-iqs-v1";
        public const string EmbeddedResourceName =
            "ReactorSim.Core.Data.candu6-two-group-iqs-pack-v1.json";

        private readonly ReadOnlyCollection<string> _energyGroupOrder;
        private readonly ReadOnlyCollection<double> _betaGroups;
        private readonly ReadOnlyCollection<double> _decayConstants;
        private readonly ReadOnlyCollection<DelayedNeutronFamilyV1> _groupFamilies;
        private readonly ReadOnlyCollection<double> _groupVelocities;

        private IqsKineticsDataPackV1(
            string dataPackVersion,
            string evidenceClass,
            string sourceProvenance,
            IEnumerable<string> energyGroupOrder,
            double betaTotal,
            IEnumerable<double> betaGroups,
            IEnumerable<double> decayConstants,
            IEnumerable<DelayedNeutronFamilyV1> groupFamilies,
            IEnumerable<double> groupVelocities,
            double generationTimeSeconds,
            double maximumMicroStepSeconds,
            double shapeRecomputeIntervalSeconds)
        {
            DataPackVersion = dataPackVersion;
            EvidenceClass = evidenceClass;
            SourceProvenance = sourceProvenance;
            _energyGroupOrder = new ReadOnlyCollection<string>(energyGroupOrder.ToArray());
            BetaTotal = betaTotal;
            _betaGroups = new ReadOnlyCollection<double>(betaGroups.ToArray());
            _decayConstants = new ReadOnlyCollection<double>(decayConstants.ToArray());
            _groupFamilies = new ReadOnlyCollection<DelayedNeutronFamilyV1>(groupFamilies.ToArray());
            _groupVelocities = new ReadOnlyCollection<double>(groupVelocities.ToArray());
            double fissionBetaTotal = 0.0;
            double photoneutronBetaTotal = 0.0;
            for (int index = 0; index < _betaGroups.Count; index++)
            {
                if (_groupFamilies[index] == DelayedNeutronFamilyV1.Photoneutron)
                {
                    photoneutronBetaTotal += _betaGroups[index];
                }
                else
                {
                    fissionBetaTotal += _betaGroups[index];
                }
            }

            FissionBetaTotal = fissionBetaTotal;
            PhotoneutronBetaTotal = photoneutronBetaTotal;
            GenerationTimeSeconds = generationTimeSeconds;
            MaximumMicroStepSeconds = maximumMicroStepSeconds;
            ShapeRecomputeIntervalSeconds = shapeRecomputeIntervalSeconds;
            TopologySchemaId = SupportedTopologySchemaId;
            UnitsProfileId = SupportedUnitsProfileId;
            ModelId = SupportedModelId;
            SolverId = SupportedSolverId;
            FormulationId = SupportedFormulationId;
            ShapeMethodId = SupportedShapeMethodId;
            AmplitudeMethodId = SupportedAmplitudeMethodId;
            ReactivityDiagnosticId = SupportedReactivityDiagnosticId;
        }

        public string DataPackVersion { get; }
        public string TopologySchemaId { get; }
        public string UnitsProfileId { get; }
        public string ModelId { get; }
        public string SolverId { get; }
        public string FormulationId { get; }
        public string ShapeMethodId { get; }
        public string AmplitudeMethodId { get; }
        public string ReactivityDiagnosticId { get; }
        public string EvidenceClass { get; }
        public string SourceProvenance { get; }
        public IReadOnlyList<string> EnergyGroupOrder { get { return _energyGroupOrder; } }
        public double BetaTotal { get; }
        public IReadOnlyList<double> BetaGroups { get { return _betaGroups; } }
        public IReadOnlyList<double> DecayConstantsPerSecond { get { return _decayConstants; } }
        public IReadOnlyList<DelayedNeutronFamilyV1> GroupFamilies { get { return _groupFamilies; } }
        public IReadOnlyList<double> GroupVelocitiesMPerSecond { get { return _groupVelocities; } }
        public double FissionBetaTotal { get; }
        public double PhotoneutronBetaTotal { get; }
        public double GenerationTimeSeconds { get; }
        public double MaximumMicroStepSeconds { get; }
        public double ShapeRecomputeIntervalSeconds { get; }

        public static ContractValidationResult<IqsKineticsDataPackV1> TryLoadEmbeddedCandu6()
        {
            Assembly assembly = typeof(IqsKineticsDataPackV1).Assembly;
            using (Stream? stream = assembly.GetManifestResourceStream(EmbeddedResourceName))
            {
                if (stream == null)
                {
                    return Invalid(
                        "AdiabaticPointKineticsDataPack.EmbeddedResource.Missing",
                        "resource",
                        "The embedded CANDU-6 adiabatic point-kinetics pack could not be found in ReactorSim.Core.");
                }

                using (var reader = new StreamReader(stream, Encoding.UTF8, true))
                {
                    return TryLoadJson(reader.ReadToEnd());
                }
            }
        }

        public static ContractValidationResult<IqsKineticsDataPackV1> TryLoadJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return Invalid("AdiabaticPointKineticsDataPack.Json.Empty", "json", "An adiabatic point-kinetics data pack JSON document is required.");
            }

            JObject root;
            try
            {
                root = JObject.Parse(json);
            }
            catch (JsonException exception)
            {
                return Invalid("AdiabaticPointKineticsDataPack.Json.Invalid", "json", "The adiabatic point-kinetics data pack is not valid JSON: " + exception.Message);
            }

            // Parse through JObject rather than Newtonsoft's reflection-based
            // object materializer. The latter is trimmed out of the Release
            // browser-WASM build and can no longer construct private DTOs.
            IqsPackDto dto = new IqsPackDto
            {
                SchemaVersion = ReadUInt32(root["schema_version"]),
                DataPackVersion = ReadString(root["data_pack_version"]),
                TopologySchemaId = ReadString(root["topology_schema_id"]),
                UnitsProfileId = ReadString(root["units_profile_id"]),
                ModelId = ReadString(root["model_id"]),
                SolverId = ReadString(root["solver_id"]),
                FormulationId = ReadString(root["formulation_id"]),
                ShapeMethodId = ReadString(root["shape_method_id"]),
                AmplitudeMethodId = ReadString(root["amplitude_method_id"]),
                ReactivityDiagnosticId = ReadString(root["reactivity_diagnostic_id"]),
                EnergyGroupOrder = ReadStringArray(root["energy_group_order"]),
                EvidenceClass = ReadString(root["evidence_class"]),
                SourceProvenance = ReadString(root["source_provenance"]),
                DelayedNeutronData = ReadDelayedNeutronData(root["delayed_neutron_data"]),
                TimeIntegration = ReadTimeIntegration(root["time_integration"])
            };

            if (dto.SchemaVersion != CurrentSchemaVersion)
            {
                return Invalid("AdiabaticPointKineticsDataPack.SchemaVersion.Unsupported", "schema_version", "Only adiabatic point-kinetics data-pack schema version 1 is supported.");
            }

            ContractValidationResult<bool> identity = ValidateIdentity(dto);
            if (!identity.IsValid)
            {
                return Invalid(identity.FirstDiagnostic.Code, identity.FirstDiagnostic.Path, identity.FirstDiagnostic.Message);
            }

            if (dto.DelayedNeutronData == null || dto.TimeIntegration == null)
            {
                return Invalid("AdiabaticPointKineticsDataPack.Section.Missing", "delayed_neutron_data", "Delayed-neutron and time-integration sections are required.");
            }

            double[] beta = dto.DelayedNeutronData.GroupFractions ?? Array.Empty<double>();
            double[] lambda = dto.DelayedNeutronData.DecayConstantsPerSec ?? Array.Empty<double>();
            double[] velocities = dto.DelayedNeutronData.GroupVelocitiesMPerS ?? Array.Empty<double>();
            if (beta.Length == 0 || lambda.Length != beta.Length)
            {
                return Invalid("AdiabaticPointKineticsDataPack.DelayedGroups.Dimension", "delayed_neutron_data", "Delayed-neutron fractions and decay constants must contain the same positive number of groups.");
            }

            if (velocities.Length != 2)
            {
                return Invalid("AdiabaticPointKineticsDataPack.Velocities.Dimension", "delayed_neutron_data.group_velocities_m_per_s", "Exactly two energy-group velocities are required.");
            }

            if (!IsFiniteNonnegative(dto.DelayedNeutronData.BetaTotal) ||
                dto.DelayedNeutronData.BetaTotal >= 1.0 ||
                !AllFiniteNonnegative(beta) || !AllFinitePositive(lambda) || !AllFinitePositive(velocities))
            {
                return Invalid("AdiabaticPointKineticsDataPack.Kinetics.Invalid", "delayed_neutron_data", "Kinetics values must be finite; fractions must be nonnegative and decay constants and velocities strictly positive.");
            }

            double betaSum = beta.Sum();
            if (Math.Abs(betaSum - dto.DelayedNeutronData.BetaTotal) > 1e-12)
            {
                return Invalid("AdiabaticPointKineticsDataPack.BetaTotal.Mismatch", "delayed_neutron_data.beta_total", "The total delayed fraction must equal the ordered group-fraction sum.");
            }

            DelayedNeutronFamilyV1[] groupFamilies;
            if (!dto.DelayedNeutronData.GroupFamiliesProvided)
            {
                groupFamilies = Enumerable.Repeat(
                    DelayedNeutronFamilyV1.Fission,
                    beta.Length).ToArray();
            }
            else if (dto.DelayedNeutronData.GroupFamilies == null ||
                     dto.DelayedNeutronData.GroupFamilies.Length != beta.Length)
            {
                return Invalid(
                    "AdiabaticPointKineticsDataPack.DelayedGroups.Family.Dimension",
                    "delayed_neutron_data.group_families",
                    "Explicit delayed-neutron family metadata must match the ordered group count.");
            }
            else if (!TryParseGroupFamilies(
                         dto.DelayedNeutronData.GroupFamilies,
                         out groupFamilies))
            {
                return Invalid(
                    "AdiabaticPointKineticsDataPack.DelayedGroups.Family.Invalid",
                    "delayed_neutron_data.group_families",
                    "Delayed-neutron family metadata must contain only fission or photoneutron values in serialized group order.");
            }

            if (!IsFinitePositive(dto.TimeIntegration.GenerationTimeSeconds) ||
                !IsFinitePositive(dto.TimeIntegration.MaximumMicroStepSeconds) ||
                !IsFinitePositive(dto.TimeIntegration.ShapeRecomputeIntervalSeconds) ||
                dto.TimeIntegration.MaximumMicroStepSeconds > dto.TimeIntegration.ShapeRecomputeIntervalSeconds)
            {
                return Invalid("AdiabaticPointKineticsDataPack.TimeIntegration.Invalid", "time_integration", "Adiabatic point-kinetics time constants must be finite, positive, and ordered micro-step <= macro-step.");
            }

            return ContractValidationResult<IqsKineticsDataPackV1>.Valid(
                new IqsKineticsDataPackV1(
                    dto.DataPackVersion!,
                    dto.EvidenceClass!,
                    dto.SourceProvenance!,
                    dto.EnergyGroupOrder!,
                    dto.DelayedNeutronData.BetaTotal,
                    beta,
                    lambda,
                    groupFamilies,
                    velocities,
                    dto.TimeIntegration.GenerationTimeSeconds,
                    dto.TimeIntegration.MaximumMicroStepSeconds,
                    dto.TimeIntegration.ShapeRecomputeIntervalSeconds));
        }

        private static string? ReadString(JToken? token)
        {
            return token == null || token.Type != JTokenType.String
                ? null
                : token.Value<string>();
        }

        private static uint ReadUInt32(JToken? token)
        {
            if (token == null || token.Type != JTokenType.Integer)
            {
                return 0;
            }

            try
            {
                long value = token.Value<long>();
                return value >= 0 && value <= uint.MaxValue ? (uint)value : 0;
            }
            catch (Exception exception) when (exception is FormatException || exception is OverflowException)
            {
                return 0;
            }
        }

        private static double ReadDouble(JToken? token)
        {
            if (token == null ||
                (token.Type != JTokenType.Integer && token.Type != JTokenType.Float))
            {
                return 0.0;
            }

            try
            {
                return token.Value<double>();
            }
            catch (Exception exception) when (exception is FormatException || exception is OverflowException)
            {
                return 0.0;
            }
        }

        private static string[]? ReadStringArray(JToken? token)
        {
            if (!(token is JArray array))
            {
                return null;
            }

            var values = new string[array.Count];
            for (int index = 0; index < array.Count; index++)
            {
                string? value = ReadString(array[index]);
                if (value == null)
                {
                    return null;
                }

                values[index] = value;
            }

            return values;
        }

        private static double[]? ReadDoubleArray(JToken? token)
        {
            if (!(token is JArray array))
            {
                return null;
            }

            var values = new double[array.Count];
            for (int index = 0; index < array.Count; index++)
            {
                JToken item = array[index];
                if (item.Type != JTokenType.Integer && item.Type != JTokenType.Float)
                {
                    return null;
                }

                values[index] = ReadDouble(item);
            }

            return values;
        }

        private static DelayedNeutronDto? ReadDelayedNeutronData(JToken? token)
        {
            if (!(token is JObject section))
            {
                return null;
            }

            return new DelayedNeutronDto
            {
                BetaTotal = ReadDouble(section["beta_total"]),
                GroupFractions = ReadDoubleArray(section["group_fractions"]),
                DecayConstantsPerSec = ReadDoubleArray(section["decay_constants_per_sec"]),
                GroupFamilies = ReadStringArray(section["group_families"]),
                GroupFamiliesProvided = section["group_families"] != null,
                GroupVelocitiesMPerS = ReadDoubleArray(section["group_velocities_m_per_s"])
            };
        }

        private static TimeIntegrationDto? ReadTimeIntegration(JToken? token)
        {
            if (!(token is JObject section))
            {
                return null;
            }

            return new TimeIntegrationDto
            {
                GenerationTimeSeconds = ReadDouble(section["generation_time_seconds"]),
                MaximumMicroStepSeconds = ReadDouble(section["maximum_micro_step_seconds"]),
                ShapeRecomputeIntervalSeconds = ReadDouble(section["shape_recompute_interval_seconds"])
            };
        }

        private static ContractValidationResult<bool> ValidateIdentity(IqsPackDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.DataPackVersion) ||
                string.IsNullOrWhiteSpace(dto.EvidenceClass) ||
                string.IsNullOrWhiteSpace(dto.SourceProvenance))
            {
                return ContractValidationResult<bool>.Invalid("AdiabaticPointKineticsDataPack.Identity.Missing", "identity", "Pack version, evidence class, and provenance are required.");
            }

            bool currentIdentity =
                string.Equals(dto.ModelId, SupportedModelId, StringComparison.Ordinal) &&
                string.Equals(dto.SolverId, SupportedSolverId, StringComparison.Ordinal);
            bool legacyIdentity =
                string.Equals(dto.ModelId, LegacySupportedModelId, StringComparison.Ordinal) &&
                string.Equals(dto.SolverId, LegacySupportedSolverId, StringComparison.Ordinal);
            if (!string.Equals(dto.TopologySchemaId, SupportedTopologySchemaId, StringComparison.Ordinal) ||
                !string.Equals(dto.UnitsProfileId, SupportedUnitsProfileId, StringComparison.Ordinal) ||
                (!currentIdentity && !legacyIdentity))
            {
                return ContractValidationResult<bool>.Invalid("AdiabaticPointKineticsDataPack.Identity.Unsupported", "identity", "The adiabatic point-kinetics topology, units, model, or solver identity is unsupported.");
            }

            if ((dto.FormulationId != null &&
                 !string.Equals(dto.FormulationId, SupportedFormulationId, StringComparison.Ordinal)) ||
                (dto.ShapeMethodId != null &&
                 !string.Equals(dto.ShapeMethodId, SupportedShapeMethodId, StringComparison.Ordinal)) ||
                (dto.AmplitudeMethodId != null &&
                 !string.Equals(dto.AmplitudeMethodId, SupportedAmplitudeMethodId, StringComparison.Ordinal)) ||
                (dto.ReactivityDiagnosticId != null &&
                 !string.Equals(dto.ReactivityDiagnosticId, SupportedReactivityDiagnosticId, StringComparison.Ordinal)))
            {
                return ContractValidationResult<bool>.Invalid(
                    "AdiabaticPointKineticsDataPack.Formulation.Unsupported",
                    "formulation",
                    "An explicit formulation, shape, amplitude, or reactivity diagnostic identity is unsupported.");
            }

            if (dto.EnergyGroupOrder == null || dto.EnergyGroupOrder.Length != 2 ||
                !string.Equals(dto.EnergyGroupOrder[0], "fast", StringComparison.Ordinal) ||
                !string.Equals(dto.EnergyGroupOrder[1], "thermal", StringComparison.Ordinal))
            {
                return ContractValidationResult<bool>.Invalid("AdiabaticPointKineticsDataPack.EnergyGroupOrder.Unsupported", "energy_group_order", "The supported energy-group order is [fast, thermal].");
            }

            return ContractValidationResult<bool>.Valid(true);
        }

        private static bool IsFinitePositive(double value)
        {
            return ContractValidation.IsFinite(value) && value > 0.0;
        }

        private static bool IsFiniteNonnegative(double value)
        {
            return ContractValidation.IsFinite(value) && value >= 0.0;
        }

        private static bool AllFinitePositive(IEnumerable<double> values)
        {
            return values.All(IsFinitePositive);
        }

        private static bool AllFiniteNonnegative(IEnumerable<double> values)
        {
            return values.All(value => ContractValidation.IsFinite(value) && value >= 0.0);
        }

        private static bool TryParseGroupFamilies(
            IEnumerable<string> serializedFamilies,
            out DelayedNeutronFamilyV1[] groupFamilies)
        {
            string[] values = serializedFamilies.ToArray();
            groupFamilies = new DelayedNeutronFamilyV1[values.Length];
            for (int index = 0; index < values.Length; index++)
            {
                if (string.Equals(values[index], "fission", StringComparison.OrdinalIgnoreCase))
                {
                    groupFamilies[index] = DelayedNeutronFamilyV1.Fission;
                }
                else if (string.Equals(values[index], "photoneutron", StringComparison.OrdinalIgnoreCase))
                {
                    groupFamilies[index] = DelayedNeutronFamilyV1.Photoneutron;
                }
                else
                {
                    groupFamilies = Array.Empty<DelayedNeutronFamilyV1>();
                    return false;
                }
            }

            return true;
        }

        private static ContractValidationResult<IqsKineticsDataPackV1> Invalid(string code, string path, string message)
        {
            return ContractValidationResult<IqsKineticsDataPackV1>.Invalid(code, path, message);
        }

        private sealed class IqsPackDto
        {
            [JsonProperty("schema_version")] public uint SchemaVersion { get; set; }
            [JsonProperty("data_pack_version")] public string? DataPackVersion { get; set; }
            [JsonProperty("topology_schema_id")] public string? TopologySchemaId { get; set; }
            [JsonProperty("units_profile_id")] public string? UnitsProfileId { get; set; }
            [JsonProperty("model_id")] public string? ModelId { get; set; }
            [JsonProperty("solver_id")] public string? SolverId { get; set; }
            [JsonProperty("formulation_id")] public string? FormulationId { get; set; }
            [JsonProperty("shape_method_id")] public string? ShapeMethodId { get; set; }
            [JsonProperty("amplitude_method_id")] public string? AmplitudeMethodId { get; set; }
            [JsonProperty("reactivity_diagnostic_id")] public string? ReactivityDiagnosticId { get; set; }
            [JsonProperty("energy_group_order")] public string[]? EnergyGroupOrder { get; set; }
            [JsonProperty("evidence_class")] public string? EvidenceClass { get; set; }
            [JsonProperty("source_provenance")] public string? SourceProvenance { get; set; }
            [JsonProperty("delayed_neutron_data")] public DelayedNeutronDto? DelayedNeutronData { get; set; }
            [JsonProperty("time_integration")] public TimeIntegrationDto? TimeIntegration { get; set; }
        }

        private sealed class DelayedNeutronDto
        {
            [JsonProperty("beta_total")] public double BetaTotal { get; set; }
            [JsonProperty("group_fractions")] public double[]? GroupFractions { get; set; }
            [JsonProperty("decay_constants_per_sec")] public double[]? DecayConstantsPerSec { get; set; }
            [JsonProperty("group_families")] public string[]? GroupFamilies { get; set; }
            public bool GroupFamiliesProvided { get; set; }
            [JsonProperty("group_velocities_m_per_s")] public double[]? GroupVelocitiesMPerS { get; set; }
        }

        private sealed class TimeIntegrationDto
        {
            [JsonProperty("generation_time_seconds")] public double GenerationTimeSeconds { get; set; }
            [JsonProperty("maximum_micro_step_seconds")] public double MaximumMicroStepSeconds { get; set; }
            [JsonProperty("shape_recompute_interval_seconds")] public double ShapeRecomputeIntervalSeconds { get; set; }
        }
    }

    /// <summary>
    /// Immutable normalized static-eigenmode shape candidate. Candidate construction never
    /// mutates the owning solver, allowing previews and failed refuelling solves
    /// to remain atomic.
    /// </summary>
    public sealed class IqsSpatialCandidateV1
    {
        private readonly ReadOnlyCollection<double> _shapeGroup1;
        private readonly ReadOnlyCollection<double> _shapeGroup2;
        private readonly ReadOnlyCollection<double> _shapeNodePowerWatts;

        internal IqsSpatialCandidateV1(
            IqsFullCoreSolver owner,
            FullCoreDiffusionSolveResultV1 spatialSolve,
            IEnumerable<double> shapeGroup1,
            IEnumerable<double> shapeGroup2,
            IEnumerable<double> shapeNodePowerWatts,
            double shapePowerWatts,
            double constraint,
            double relativeReactivity,
            double staticEigenvalueDeltaReactivity,
            double adjointWeightedDenominator)
        {
            Owner = owner;
            SpatialSolve = spatialSolve;
            _shapeGroup1 = new ReadOnlyCollection<double>(shapeGroup1.ToArray());
            _shapeGroup2 = new ReadOnlyCollection<double>(shapeGroup2.ToArray());
            _shapeNodePowerWatts = new ReadOnlyCollection<double>(shapeNodePowerWatts.ToArray());
            ShapePowerWatts = shapePowerWatts;
            Constraint = constraint;
            RelativeReactivity = relativeReactivity;
            StaticEigenvalueDeltaReactivity = staticEigenvalueDeltaReactivity;
            AdjointWeightedDenominator = adjointWeightedDenominator;
        }

        internal IqsFullCoreSolver Owner { get; }
        public FullCoreDiffusionSolveResultV1 SpatialSolve { get; }
        public IReadOnlyList<double> ShapeGroup1 { get { return _shapeGroup1; } }
        public IReadOnlyList<double> ShapeGroup2 { get { return _shapeGroup2; } }
        public IReadOnlyList<double> ShapeNodePowerWatts { get { return _shapeNodePowerWatts; } }
        public double ShapePowerWatts { get; }
        public double Constraint { get; }
        /// <summary>
        /// First-order perturbation estimate used by point kinetics. It is an
        /// adjoint-weighted static estimate, not exact dynamic IQS reactivity.
        /// </summary>
        public double RelativeReactivity { get; }
        public double FirstOrderAdjointWeightedReactivity { get { return RelativeReactivity; } }
        public double StaticEigenvalueDeltaReactivity { get; }
        public double AdjointWeightedDenominator { get; }
    }

    /// <summary>
    /// Adiabatic/quasi-static adapter: the existing deterministic full-core
    /// diffusion eigenvalue solve supplies the slow shape, while an arbitrary
    /// ordered-group point-kinetics model advances the scalar amplitude. A
    /// reference importance vector is solved from the transposed static
    /// operator at initialization. The historical IQS class name is retained
    /// for compatibility; this is not a full fixed-source IQS or Krylov shape
    /// solve.
    /// </summary>
    public sealed class IqsFullCoreSolver
    {
        private readonly FullCoreDiffusionModelV1 _spatialModel;
        private readonly IqsKineticsDataPackV1 _dataPack;
        private readonly double _targetPowerWatts;
        private readonly double[] _precursors;
        private IqsSpatialCandidateV1 _current;
        private double _amplitude;
        private readonly double _referenceReactivity;
        private readonly double _referenceEigenvalue;
        private readonly double _shapeConstraint;
        private readonly SpatialCoefficientSet _referenceCoefficients;
        private readonly ReadOnlyCollection<double> _referenceGroup1Flux;
        private readonly ReadOnlyCollection<double> _referenceGroup2Flux;
        private readonly SpatialAdjointReferenceSolutionV1 _referenceAdjoint;

        private IqsFullCoreSolver(
            FullCoreDiffusionModelV1 spatialModel,
            IqsKineticsDataPackV1 dataPack,
            double targetPowerWatts,
            FullCoreDiffusionSolveResultV1 initialSpatialSolve,
            SpatialAdjointReferenceSolutionV1 referenceAdjoint)
        {
            _spatialModel = spatialModel;
            _dataPack = dataPack;
            _targetPowerWatts = targetPowerWatts;
            _precursors = new double[dataPack.BetaGroups.Count];
            _amplitude = 1.0;
            _referenceReactivity = initialSpatialSolve.Reactivity;
            _referenceEigenvalue = initialSpatialSolve.EffectiveK;
            _referenceCoefficients = initialSpatialSolve.Coefficients;
            _referenceGroup1Flux = new ReadOnlyCollection<double>(initialSpatialSolve.Group1Flux.ToArray());
            _referenceGroup2Flux = new ReadOnlyCollection<double>(initialSpatialSolve.Group2Flux.ToArray());
            _referenceAdjoint = referenceAdjoint;
            _shapeConstraint = ComputeConstraint(initialSpatialSolve.Group1Flux, initialSpatialSolve.Group2Flux);
            _current = BuildCandidate(initialSpatialSolve);
            for (int group = 0; group < _precursors.Length; group++)
            {
                _precursors[group] = _dataPack.BetaGroups[group] * _amplitude /
                                     (_dataPack.GenerationTimeSeconds * _dataPack.DecayConstantsPerSecond[group]);
            }
        }

        public IqsKineticsDataPackV1 DataPack { get { return _dataPack; } }
        public double Amplitude { get { return _amplitude; } }
        public IReadOnlyList<double> Precursors { get { return new ReadOnlyCollection<double>((double[])_precursors.Clone()); } }
        public IqsSpatialCandidateV1 CurrentProjection { get { return _current; } }
        public FullCoreDiffusionSolveResultV1 CurrentSpatialSolve { get { return _current.SpatialSolve; } }
        public double RelativeReactivity { get { return _current.RelativeReactivity; } }
        public double StaticEigenvalueDeltaReactivity
        {
            get { return _current.StaticEigenvalueDeltaReactivity; }
        }
        public double GenerationTimeSeconds { get { return _dataPack.GenerationTimeSeconds; } }
        public double ShapeConstraint { get { return _shapeConstraint; } }
        public IReadOnlyList<double> ReferenceAdjointGroup1
        {
            get { return _referenceAdjoint.Group1Importance; }
        }
        public IReadOnlyList<double> ReferenceAdjointGroup2
        {
            get { return _referenceAdjoint.Group2Importance; }
        }
        public string ReferenceAdjointSolverIdentity
        {
            get { return _referenceAdjoint.Identity; }
        }
        public double ReferenceAdjointEigenvalue
        {
            get { return _referenceAdjoint.Eigenvalue; }
        }
        public double ReferenceAdjointResidualRelativeInfinity
        {
            get { return _referenceAdjoint.ResidualRelativeInfinity; }
        }
        public int ReferenceAdjointIterationCount
        {
            get { return _referenceAdjoint.IterationCount; }
        }
        public string ReferenceAdjointTopologySchemaId
        {
            get { return _referenceAdjoint.TopologySchemaId; }
        }
        public string ReferenceAdjointDataPackVersion
        {
            get { return _referenceAdjoint.DataPackVersion; }
        }
        public Digest32? ReferenceAdjointDataPackDigest
        {
            get { return _referenceAdjoint.DataPackDigest; }
        }
        public string SolverIdentity
        {
            get { return _dataPack.SolverId + "/" + _dataPack.DataPackVersion + "+" + _current.SpatialSolve.SolverIdentity; }
        }

        public static ContractValidationResult<IqsFullCoreSolver> TryCreate(
            FullCoreDiffusionModelV1 spatialModel,
            IqsKineticsDataPackV1 dataPack,
            IEnumerable<BundleState> bundles,
            double targetPowerWatts)
        {
            if (spatialModel == null)
            {
                return Invalid("IqsFullCoreSolver.SpatialModel.Missing", "spatial_model", "An IQS solver requires a full-core spatial model.");
            }

            if (dataPack == null)
            {
                return Invalid("IqsFullCoreSolver.DataPack.Missing", "data_pack", "An IQS solver requires a validated kinetics pack.");
            }

            if (dataPack.GroupFamilies.Any(family => family != DelayedNeutronFamilyV1.Fission))
            {
                return Invalid(
                    "AdiabaticPointKineticsSolver.DelayedGroups.Family.Unsupported",
                    "data_pack.delayed_neutron_data.group_families",
                    "The current point-kinetics adapter admits supplied fission groups only; future photoneutron metadata is schema capability until its source law is admitted.");
            }

            if (bundles == null)
            {
                return Invalid("IqsFullCoreSolver.Bundles.Missing", "bundles", "An IQS solver requires a full-core bundle inventory.");
            }

            if (!ContractValidation.IsFinite(targetPowerWatts) || targetPowerWatts <= 0.0)
            {
                return Invalid("IqsFullCoreSolver.TargetPower.Invalid", "target_power_w", "The IQS reference power must be finite and positive SI watts.");
            }

            BundleState[] bundleRecords = bundles.ToArray();
            ContractValidationResult<FullCoreDiffusionSolveResultV1> spatial =
                spatialModel.TrySolve(bundleRecords, targetPowerWatts);
            if (!spatial.IsValid)
            {
                return Invalid(spatial.FirstDiagnostic.Code, spatial.FirstDiagnostic.Path, spatial.FirstDiagnostic.Message);
            }

            ContractValidationResult<SpatialAdjointReferenceSolutionV1> referenceAdjoint =
                spatialModel.TrySolveReferenceAdjoint(spatial.Value);
            if (!referenceAdjoint.IsValid)
            {
                return Invalid(
                    referenceAdjoint.FirstDiagnostic.Code,
                    referenceAdjoint.FirstDiagnostic.Path,
                    referenceAdjoint.FirstDiagnostic.Message);
            }

            try
            {
                var solver = new IqsFullCoreSolver(
                    spatialModel,
                    dataPack,
                    targetPowerWatts,
                    spatial.Value,
                    referenceAdjoint.Value);
                if (!ContractValidation.IsFinite(solver.ShapeConstraint) || solver.ShapeConstraint <= 0.0)
                {
                    return Invalid("IqsFullCoreSolver.Constraint.Invalid", "shape_constraint", "The initial adjoint-weighted shape constraint must be finite and positive.");
                }

                return ContractValidationResult<IqsFullCoreSolver>.Valid(solver);
            }
            catch (InvalidOperationException exception)
            {
                return Invalid("IqsFullCoreSolver.Initialization.Invalid", "initialization", exception.Message);
            }
        }

        public ContractValidationResult<IqsSpatialCandidateV1> TrySolveCandidate(IEnumerable<BundleState> bundles)
        {
            if (bundles == null)
            {
                return ContractValidationResult<IqsSpatialCandidateV1>.Invalid("IqsFullCoreSolver.Bundles.Missing", "bundles", "A shape solve requires a full-core bundle inventory.");
            }

            ContractValidationResult<FullCoreDiffusionSolveResultV1> spatial =
                _spatialModel.TrySolve(
                    bundles,
                    _targetPowerWatts,
                    _current.SpatialSolve.EffectiveK,
                    _current.SpatialSolve.Group1Flux,
                    _current.SpatialSolve.Group2Flux);
            if (!spatial.IsValid)
            {
                return ContractValidationResult<IqsSpatialCandidateV1>.Invalid(spatial.FirstDiagnostic.Code, spatial.FirstDiagnostic.Path, spatial.FirstDiagnostic.Message);
            }

            try
            {
                return ContractValidationResult<IqsSpatialCandidateV1>.Valid(BuildCandidate(spatial.Value));
            }
            catch (InvalidOperationException exception)
            {
                return ContractValidationResult<IqsSpatialCandidateV1>.Invalid("IqsFullCoreSolver.Candidate.Invalid", "candidate", exception.Message);
            }
        }

        /// <summary>
        /// Optional dynamic-Xe seam.  The coupling result already contains a
        /// validated effective coefficient set; this method only asks the
        /// existing static eigenmode model to solve it and then applies the
        /// same candidate normalization and first-order reactivity estimate.
        /// </summary>
        public ContractValidationResult<IqsSpatialCandidateV1> TrySolveCandidate(
            IEnumerable<BundleState> bundles,
            XenonSpatialCouplingResultV1 coupling)
        {
            if (bundles == null)
            {
                return ContractValidationResult<IqsSpatialCandidateV1>.Invalid(
                    "AdiabaticPointKineticsSolver.Bundles.Missing",
                    "bundles",
                    "A shape solve requires a full-core bundle inventory.");
            }

            if (coupling == null)
            {
                return ContractValidationResult<IqsSpatialCandidateV1>.Invalid(
                    "AdiabaticPointKineticsSolver.XenonCoupling.Missing",
                    "coupling",
                    "A dynamic-Xe shape solve requires a validated coupling result.");
            }

            ContractValidationResult<FullCoreDiffusionSolveResultV1> spatial =
                _spatialModel.TrySolveWithXenonCoupling(
                    bundles,
                    coupling,
                    _targetPowerWatts,
                    _current.SpatialSolve.EffectiveK,
                    _current.SpatialSolve.Group1Flux,
                    _current.SpatialSolve.Group2Flux);
            if (!spatial.IsValid)
            {
                return ContractValidationResult<IqsSpatialCandidateV1>.Invalid(
                    spatial.FirstDiagnostic.Code,
                    spatial.FirstDiagnostic.Path,
                    spatial.FirstDiagnostic.Message);
            }

            try
            {
                return ContractValidationResult<IqsSpatialCandidateV1>.Valid(BuildCandidate(spatial.Value));
            }
            catch (InvalidOperationException exception)
            {
                return ContractValidationResult<IqsSpatialCandidateV1>.Invalid(
                    "AdiabaticPointKineticsSolver.Candidate.Invalid",
                    "candidate",
                    exception.Message);
            }
        }

        public ContractValidationResult<bool> TryCommitCandidate(IqsSpatialCandidateV1 candidate)
        {
            if (candidate == null)
            {
                return ContractValidationResult<bool>.Invalid("IqsFullCoreSolver.Candidate.Missing", "candidate", "A shape candidate is required.");
            }

            if (!ReferenceEquals(candidate.Owner, this))
            {
                return ContractValidationResult<bool>.Invalid("IqsFullCoreSolver.Candidate.OwnerMismatch", "candidate", "A shape candidate may only be committed by its owning solver.");
            }

            _current = candidate;
            return ContractValidationResult<bool>.Valid(true);
        }

        /// <summary>
        /// Advances one point-kinetics step using the current spatial solve's
        /// relative reactivity.
        /// </summary>
        public ContractValidationResult<double> TryAdvancePointKinetics(double dtSeconds)
        {
            return TryAdvancePointKinetics(dtSeconds, RelativeReactivity);
        }

        /// <summary>
        /// Advances one point-kinetics step using a caller-supplied effective
        /// relative reactivity. The override is scoped to this step only and
        /// does not replace the candidate's stored spatial reactivity.
        /// </summary>
        public ContractValidationResult<double> TryAdvancePointKinetics(
            double dtSeconds,
            double effectiveRelativeReactivity)
        {
            if (!ContractValidation.IsFinite(dtSeconds) || dtSeconds <= 0.0 ||
                dtSeconds > _dataPack.MaximumMicroStepSeconds)
            {
                return ContractValidationResult<double>.Invalid("IqsFullCoreSolver.TimeStep.Invalid", "dt_seconds", "The point-kinetics step must be finite, positive, and no greater than the pack micro-step limit.");
            }

            if (!ContractValidation.IsFinite(effectiveRelativeReactivity))
            {
                return ContractValidationResult<double>.Invalid("IqsFullCoreSolver.RelativeReactivity.Invalid", "effective_relative_reactivity", "The effective relative reactivity override must be finite.");
            }

            var nextPrecursors = new double[_precursors.Length];
            double precursorSource = 0.0;
            for (int group = 0; group < nextPrecursors.Length; group++)
            {
                double lambda = _dataPack.DecayConstantsPerSecond[group];
                double decay = Math.Exp(-lambda * dtSeconds);
                nextPrecursors[group] = _precursors[group] * decay +
                    (_dataPack.BetaGroups[group] / _dataPack.GenerationTimeSeconds) *
                    _amplitude * (1.0 - decay) / lambda;
                precursorSource += lambda * nextPrecursors[group];
            }

            double denominator = 1.0 -
                dtSeconds * (effectiveRelativeReactivity - _dataPack.BetaTotal) /
                _dataPack.GenerationTimeSeconds;
            if (!ContractValidation.IsFinite(denominator) || denominator <= 1e-12)
            {
                return ContractValidationResult<double>.Invalid("IqsFullCoreSolver.PointKinetics.Denominator", "point_kinetics", "The semi-implicit point-kinetics denominator is nonpositive or non-finite.");
            }

            double nextAmplitude = (_amplitude + dtSeconds * precursorSource) / denominator;
            if (!ContractValidation.IsFinite(nextAmplitude) || nextAmplitude < 0.0 ||
                nextPrecursors.Any(value => !ContractValidation.IsFinite(value) || value < 0.0))
            {
                return ContractValidationResult<double>.Invalid("IqsFullCoreSolver.PointKinetics.State", "point_kinetics", "The point-kinetics update produced a non-finite or negative state.");
            }

            Array.Copy(nextPrecursors, _precursors, nextPrecursors.Length);
            _amplitude = nextAmplitude;
            return ContractValidationResult<double>.Valid(_amplitude);
        }

        private IqsSpatialCandidateV1 BuildCandidate(FullCoreDiffusionSolveResultV1 spatial)
        {
            double rawConstraint = ComputeConstraint(spatial.Group1Flux, spatial.Group2Flux);
            if (!ContractValidation.IsFinite(rawConstraint) || rawConstraint <= 0.0)
            {
                throw new InvalidOperationException("The candidate adjoint-weighted shape constraint is invalid.");
            }

            double factor = _shapeConstraint / rawConstraint;
            double[] group1 = spatial.Group1Flux.Select(value => value * factor).ToArray();
            double[] group2 = spatial.Group2Flux.Select(value => value * factor).ToArray();
            double[] powers = spatial.NodePowerWatts.Select(value => value * factor).ToArray();
            double powerTotal = powers.Sum();
            double constraint = ComputeConstraint(group1, group2);
            double firstOrderReactivity = ComputeFirstOrderAdjointWeightedReactivity(
                spatial,
                out double adjointWeightedDenominator);
            if (!ContractValidation.IsFinite(factor) || factor <= 0.0 ||
                !ContractValidation.IsFinite(powerTotal) || powerTotal <= 0.0 ||
                !ContractValidation.IsFinite(constraint) || constraint <= 0.0)
            {
                throw new InvalidOperationException("The normalized candidate shape is invalid.");
            }

            return new IqsSpatialCandidateV1(
                this,
                spatial,
                group1,
                group2,
                powers,
                powerTotal,
                constraint,
                firstOrderReactivity,
                spatial.Reactivity - _referenceReactivity,
                adjointWeightedDenominator);
        }

        private double ComputeConstraint(IReadOnlyList<double> group1, IReadOnlyList<double> group2)
        {
            if (group1 == null || group2 == null ||
                group1.Count != _spatialModel.NodeCount ||
                group2.Count != _spatialModel.NodeCount ||
                _referenceAdjoint.Group1Importance.Count != _spatialModel.NodeCount ||
                _referenceAdjoint.Group2Importance.Count != _spatialModel.NodeCount ||
                _referenceCoefficients.NodeCount != _spatialModel.NodeCount)
            {
                throw new InvalidOperationException("The adiabatic shape dimensions do not match the full-core reference model.");
            }

            double inverseVelocity1 = 1.0 / _dataPack.GroupVelocitiesMPerSecond[0];
            double inverseVelocity2 = 1.0 / _dataPack.GroupVelocitiesMPerSecond[1];
            if (!ContractValidation.IsFinite(inverseVelocity1) ||
                !ContractValidation.IsFinite(inverseVelocity2) ||
                inverseVelocity1 <= 0.0 || inverseVelocity2 <= 0.0)
            {
                throw new InvalidOperationException("The adiabatic shape velocities are invalid.");
            }

            double constraint = 0.0;
            for (int index = 0; index < group1.Count; index++)
            {
                SpatialNodeCoefficients node = _referenceCoefficients.Nodes[index];
                double importance1 = _referenceAdjoint.Group1Importance[index];
                double importance2 = _referenceAdjoint.Group2Importance[index];
                double term = node.VolumeM3 * (
                    importance1 * group1[index] * inverseVelocity1 +
                    importance2 * group2[index] * inverseVelocity2);
                if (!ContractValidation.IsFinite(node.VolumeM3) || node.VolumeM3 <= 0.0 ||
                    !ContractValidation.IsFinite(importance1) || importance1 < 0.0 ||
                    !ContractValidation.IsFinite(importance2) || importance2 < 0.0 ||
                    !ContractValidation.IsFinite(group1[index]) || group1[index] < 0.0 ||
                    !ContractValidation.IsFinite(group2[index]) || group2[index] < 0.0 ||
                    !ContractValidation.IsFinite(term))
                {
                    throw new InvalidOperationException("The reference-adjoint shape constraint is non-finite or negative.");
                }

                constraint += term;
                if (!ContractValidation.IsFinite(constraint))
                {
                    throw new InvalidOperationException("The reference-adjoint shape constraint reduction became non-finite.");
                }
            }

            return constraint;
        }

        private double ComputeFirstOrderAdjointWeightedReactivity(
            FullCoreDiffusionSolveResultV1 spatial,
            out double denominator)
        {
            if (spatial == null || spatial.Coefficients == null ||
                spatial.Coefficients.NodeCount != _spatialModel.NodeCount ||
                _referenceCoefficients.NodeCount != _spatialModel.NodeCount ||
                _referenceGroup1Flux.Count != _spatialModel.NodeCount ||
                _referenceGroup2Flux.Count != _spatialModel.NodeCount ||
                _referenceAdjoint.Group1Importance.Count != _spatialModel.NodeCount ||
                _referenceAdjoint.Group2Importance.Count != _spatialModel.NodeCount)
            {
                throw new InvalidOperationException("The first-order reactivity inputs do not match the full-core reference model.");
            }

            if (!ContractValidation.IsFinite(_referenceEigenvalue) || _referenceEigenvalue <= 0.0)
            {
                throw new InvalidOperationException("The reference static eigenvalue is invalid for adjoint perturbation weighting.");
            }

            denominator = 0.0;
            double numerator = 0.0;
            for (int index = 0; index < _spatialModel.NodeCount; index++)
            {
                SpatialNodeCoefficients reference = _referenceCoefficients.Nodes[index];
                SpatialNodeCoefficients candidate = spatial.Coefficients.Nodes[index];
                double referenceGroup1 = _referenceGroup1Flux[index];
                double referenceGroup2 = _referenceGroup2Flux[index];
                double candidateGroup1 = spatial.Group1Flux[index];
                double candidateGroup2 = spatial.Group2Flux[index];
                double referenceFissionAtCandidateShape =
                    reference.NuFissionGroup1PerM * candidateGroup1 +
                    reference.NuFissionGroup2PerM * candidateGroup2;
                double referenceFissionAtReferenceShape =
                    reference.NuFissionGroup1PerM * referenceGroup1 +
                    reference.NuFissionGroup2PerM * referenceGroup2;
                double candidateFission =
                    candidate.NuFissionGroup1PerM * candidateGroup1 +
                    candidate.NuFissionGroup2PerM * candidateGroup2;
                double deltaFissionGroup1 =
                    candidate.ChiGroup1 * candidateFission -
                    reference.ChiGroup1 * referenceFissionAtCandidateShape;
                double deltaFissionGroup2 =
                    candidate.ChiGroup2 * candidateFission -
                    reference.ChiGroup2 * referenceFissionAtCandidateShape;
                double deltaRemovalGroup1 =
                    (candidate.AbsorptionGroup1PerM + candidate.DownscatterGroup1To2PerM) -
                    (reference.AbsorptionGroup1PerM + reference.DownscatterGroup1To2PerM);
                double deltaRemovalGroup2 =
                    candidate.AbsorptionGroup2PerM - reference.AbsorptionGroup2PerM;
                double deltaDownscatter =
                    candidate.DownscatterGroup1To2PerM - reference.DownscatterGroup1To2PerM;
                double deltaOperatorGroup1 = deltaRemovalGroup1 * candidateGroup1;
                double deltaOperatorGroup2 =
                    deltaRemovalGroup2 * candidateGroup2 -
                    deltaDownscatter * candidateGroup1;
                double nodeDenominator = reference.VolumeM3 * (
                    _referenceAdjoint.Group1Importance[index] * reference.ChiGroup1 * referenceFissionAtReferenceShape +
                    _referenceAdjoint.Group2Importance[index] * reference.ChiGroup2 * referenceFissionAtReferenceShape);
                double nodeNumerator = reference.VolumeM3 * (
                    _referenceAdjoint.Group1Importance[index] *
                        (deltaFissionGroup1 / _referenceEigenvalue - deltaOperatorGroup1) +
                    _referenceAdjoint.Group2Importance[index] *
                        (deltaFissionGroup2 / _referenceEigenvalue - deltaOperatorGroup2));
                if (reference.Node != candidate.Node ||
                    !ContractValidation.IsFinite(reference.VolumeM3) || reference.VolumeM3 <= 0.0 ||
                    !ContractValidation.IsFinite(referenceGroup1) || referenceGroup1 < 0.0 ||
                    !ContractValidation.IsFinite(referenceGroup2) || referenceGroup2 < 0.0 ||
                    !ContractValidation.IsFinite(candidateGroup1) || candidateGroup1 < 0.0 ||
                    !ContractValidation.IsFinite(candidateGroup2) || candidateGroup2 < 0.0 ||
                    !ContractValidation.IsFinite(_referenceAdjoint.Group1Importance[index]) ||
                    _referenceAdjoint.Group1Importance[index] < 0.0 ||
                    !ContractValidation.IsFinite(_referenceAdjoint.Group2Importance[index]) ||
                    _referenceAdjoint.Group2Importance[index] < 0.0 ||
                    !ContractValidation.IsFinite(referenceFissionAtCandidateShape) || referenceFissionAtCandidateShape < 0.0 ||
                    !ContractValidation.IsFinite(referenceFissionAtReferenceShape) || referenceFissionAtReferenceShape < 0.0 ||
                    !ContractValidation.IsFinite(candidateFission) || candidateFission < 0.0 ||
                    !ContractValidation.IsFinite(deltaFissionGroup1) ||
                    !ContractValidation.IsFinite(deltaFissionGroup2) ||
                    !ContractValidation.IsFinite(deltaRemovalGroup1) ||
                    !ContractValidation.IsFinite(deltaRemovalGroup2) ||
                    !ContractValidation.IsFinite(deltaDownscatter) ||
                    !ContractValidation.IsFinite(deltaOperatorGroup1) ||
                    !ContractValidation.IsFinite(deltaOperatorGroup2) ||
                    !ContractValidation.IsFinite(nodeDenominator) ||
                    !ContractValidation.IsFinite(nodeNumerator))
                {
                    throw new InvalidOperationException("The first-order adjoint perturbation term became non-finite.");
                }

                denominator += nodeDenominator;
                numerator += nodeNumerator;
                if (!ContractValidation.IsFinite(denominator) ||
                    !ContractValidation.IsFinite(numerator))
                {
                    throw new InvalidOperationException("The first-order adjoint perturbation reduction became non-finite.");
                }
            }

            if (denominator <= 0.0)
            {
                throw new InvalidOperationException("The reference fission-production adjoint denominator must be finite and positive.");
            }

            double result = numerator / denominator;
            if (!ContractValidation.IsFinite(result))
            {
                throw new InvalidOperationException("The first-order adjoint-weighted reactivity became non-finite.");
            }

            return result;
        }

        private static ContractValidationResult<IqsFullCoreSolver> Invalid(string code, string path, string message)
        {
            return ContractValidationResult<IqsFullCoreSolver>.Invalid(code, path, message);
        }
    }
}
