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
    /// Explicit identity for the active adiabatic kinetics path. The
    /// <c>Iqs*</c> type and resource names are retained as legacy API names,
    /// but the active formulation is a static k-eigenmode shape solve with a
    /// scalar point-kinetics amplitude; it is not a fixed-source IQS solve.
    /// </summary>
    public static class AdiabaticKineticsIdentityV1
    {
        public const string FormulationId =
            "adiabatic-static-k-eigenmode-plus-point-kinetics-v1";
        public const string ShapeMethodId =
            "deterministic-static-k-eigenmode-recompute-v1";
        public const string AmplitudeMethodId =
            "semi-implicit-point-kinetics-v1";
        public const string ReactivityMethodId =
            AdjointWeightedReactivityIdentityV1.MethodId;
        public const string StaticReactivityMethodId =
            "static-effective-k-rho-v1";
        public const string LegacyReactivityMethodId =
            StaticReactivityMethodId;
        public const string ModelId =
            "candu6-two-group-adiabatic-point-kinetics-v1";
        public const string SolverId =
            "static-k-eigenmode-point-kinetics-v1";

        // These IDs were published by the v1 compatibility surface. They
        // remain accepted on input so old packs continue to load, but they
        // are not the identity of the active formulation.
        public const string LegacyModelId = "candu6-two-group-iqs-full-core-v1";
        public const string LegacySolverId = "spatial-eigen-iqs-v1";
    }

    /// <summary>
    /// Validated synthetic kinetics metadata for the full-core adiabatic
    /// static-eigenmode/point-kinetics adapter. The spatial coefficients
    /// remain in the separately versioned diffusion pack. The class name is a
    /// legacy public API name retained for v1 compatibility.
    /// </summary>
    public sealed class IqsKineticsDataPackV1
    {
        public const uint CurrentSchemaVersion = 1;
        public const string SupportedTopologySchemaId = "candu6-380x12-grid-v1";
        public const string SupportedUnitsProfileId = "SI-v1";
        public const string SupportedModelId = AdiabaticKineticsIdentityV1.ModelId;
        public const string SupportedSolverId = AdiabaticKineticsIdentityV1.SolverId;
        public const string LegacyModelId = AdiabaticKineticsIdentityV1.LegacyModelId;
        public const string LegacySolverId = AdiabaticKineticsIdentityV1.LegacySolverId;
        public const string FissionGroupFamily = "fission";
        public const string PhotoneutronGroupFamily = "photoneutron";
        public const string EmbeddedResourceName =
            "ReactorSim.Core.Data.candu6-two-group-iqs-pack-v1.json";

        private readonly ReadOnlyCollection<string> _energyGroupOrder;
        private readonly ReadOnlyCollection<string> _groupFamilies;
        private readonly ReadOnlyCollection<int> _delayedGroupOrder;
        private readonly ReadOnlyCollection<double> _betaGroups;
        private readonly ReadOnlyCollection<double> _decayConstants;
        private readonly ReadOnlyCollection<double> _groupVelocities;

        private IqsKineticsDataPackV1(
            string dataPackVersion,
            string sourceIdentity,
            string evidenceClass,
            string sourceProvenance,
            string modelId,
            string solverId,
            string formulationId,
            string shapeMethodId,
            string amplitudeMethodId,
            string reactivityMethodId,
            IEnumerable<string> energyGroupOrder,
            IEnumerable<string> groupFamilies,
            IEnumerable<int> delayedGroupOrder,
            double betaTotal,
            IEnumerable<double> betaGroups,
            IEnumerable<double> decayConstants,
            IEnumerable<double> groupVelocities,
            double generationTimeSeconds,
            double maximumMicroStepSeconds,
            double shapeRecomputeIntervalSeconds,
            Digest32 contentDigest,
            Digest32 topologyDigest)
        {
            DataPackVersion = dataPackVersion;
            SourceIdentity = sourceIdentity;
            EvidenceClass = evidenceClass;
            SourceProvenance = sourceProvenance;
            ModelId = modelId;
            SolverId = solverId;
            FormulationId = formulationId;
            ShapeMethodId = shapeMethodId;
            AmplitudeMethodId = amplitudeMethodId;
            ReactivityMethodId = reactivityMethodId;
            _energyGroupOrder = new ReadOnlyCollection<string>(energyGroupOrder.ToArray());
            _groupFamilies = new ReadOnlyCollection<string>(groupFamilies.ToArray());
            _delayedGroupOrder = new ReadOnlyCollection<int>(delayedGroupOrder.ToArray());
            BetaTotal = betaTotal;
            _betaGroups = new ReadOnlyCollection<double>(betaGroups.ToArray());
            _decayConstants = new ReadOnlyCollection<double>(decayConstants.ToArray());
            _groupVelocities = new ReadOnlyCollection<double>(groupVelocities.ToArray());
            GenerationTimeSeconds = generationTimeSeconds;
            MaximumMicroStepSeconds = maximumMicroStepSeconds;
            ShapeRecomputeIntervalSeconds = shapeRecomputeIntervalSeconds;
            TopologySchemaId = SupportedTopologySchemaId;
            UnitsProfileId = SupportedUnitsProfileId;
            ContentDigest = contentDigest;
            TopologyDigest = topologyDigest;
            EnergyGroupOrderIdentity = string.Join("|", _energyGroupOrder);
        }

        public string DataPackVersion { get; }
        public string SourceIdentity { get; }
        public string TopologySchemaId { get; }
        public string UnitsProfileId { get; }
        public Digest32 ContentDigest { get; }
        public Digest32 TopologyDigest { get; }
        public string EnergyGroupOrderIdentity { get; }
        public string ModelId { get; }
        public string SolverId { get; }
        public string FormulationId { get; }
        public string ShapeMethodId { get; }
        public string AmplitudeMethodId { get; }
        public string ReactivityMethodId { get; }
        public bool UsesLegacyIdentity
        {
            get
            {
                return string.Equals(ModelId, LegacyModelId, StringComparison.Ordinal) ||
                       string.Equals(SolverId, LegacySolverId, StringComparison.Ordinal);
            }
        }
        public string EvidenceClass { get; }
        public string SourceProvenance { get; }
        public IReadOnlyList<string> EnergyGroupOrder { get { return _energyGroupOrder; } }
        public IReadOnlyList<string> GroupFamilies { get { return _groupFamilies; } }
        public IReadOnlyList<string> DelayedGroupFamilies { get { return _groupFamilies; } }
        public IReadOnlyList<int> DelayedGroupOrder { get { return _delayedGroupOrder; } }
        public int DelayedGroupCount { get { return _betaGroups.Count; } }
        public double BetaTotal { get; }
        public IReadOnlyList<double> BetaGroups { get { return _betaGroups; } }
        public IReadOnlyList<double> DecayConstantsPerSecond { get { return _decayConstants; } }
        public IReadOnlyList<double> GroupVelocitiesMPerSecond { get { return _groupVelocities; } }
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
                        "IqsDataPack.EmbeddedResource.Missing",
                        "resource",
                        "The embedded CANDU-6 adiabatic kinetics pack could not be found in ReactorSim.Core.");
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
                return Invalid("IqsDataPack.Json.Empty", "json", "An adiabatic kinetics data-pack JSON document is required.");
            }

            JObject root;
            try
            {
                root = JObject.Parse(json);
            }
            catch (JsonException exception)
            {
                return Invalid("IqsDataPack.Json.Invalid", "json", "The adiabatic kinetics data pack is not valid JSON: " + exception.Message);
            }

            // Parse through JObject rather than Newtonsoft's reflection-based
            // object materializer. The latter is trimmed out of the Release
            // browser-WASM build and can no longer construct private DTOs.
            IqsPackDto dto = new IqsPackDto
            {
                SchemaVersion = ReadUInt32(root["schema_version"]),
                DataPackVersion = ReadString(root["data_pack_version"]),
                SourceIdentity = ReadString(root["source_identity"]),
                SourceIdentitySpecified = root["source_identity"] != null,
                TopologySchemaId = ReadString(root["topology_schema_id"]),
                UnitsProfileId = ReadString(root["units_profile_id"]),
                ModelId = ReadString(root["model_id"]),
                SolverId = ReadString(root["solver_id"]),
                FormulationId = ReadString(root["formulation_id"]),
                FormulationIdSpecified = root["formulation_id"] != null,
                ShapeMethodId = ReadString(root["shape_method_id"]),
                ShapeMethodIdSpecified = root["shape_method_id"] != null,
                AmplitudeMethodId = ReadString(root["amplitude_method_id"]),
                AmplitudeMethodIdSpecified = root["amplitude_method_id"] != null,
                ReactivityMethodId = ReadString(root["reactivity_method_id"]),
                ReactivityMethodIdSpecified = root["reactivity_method_id"] != null,
                EnergyGroupOrder = ReadStringArray(root["energy_group_order"]),
                EvidenceClass = ReadString(root["evidence_class"]),
                SourceProvenance = ReadString(root["source_provenance"]),
                DelayedNeutronData = ReadDelayedNeutronData(root["delayed_neutron_data"]),
                TimeIntegration = ReadTimeIntegration(root["time_integration"])
            };

            if (dto.SchemaVersion != CurrentSchemaVersion)
            {
                return Invalid("IqsDataPack.SchemaVersion.Unsupported", "schema_version", "Only adiabatic kinetics schema version 1 is supported.");
            }

            ContractValidationResult<bool> identity = ValidateIdentity(dto);
            if (!identity.IsValid)
            {
                return Invalid(identity.FirstDiagnostic.Code, identity.FirstDiagnostic.Path, identity.FirstDiagnostic.Message);
            }

            if (dto.DelayedNeutronData == null || dto.TimeIntegration == null)
            {
                return Invalid("IqsDataPack.Section.Missing", "delayed_neutron_data", "Delayed-neutron and time-integration sections are required.");
            }

            double[] beta = dto.DelayedNeutronData.GroupFractions ?? Array.Empty<double>();
            double[] lambda = dto.DelayedNeutronData.DecayConstantsPerSec ?? Array.Empty<double>();
            double[] velocities = dto.DelayedNeutronData.GroupVelocitiesMPerS ?? Array.Empty<double>();
            if (beta.Length == 0 || beta.Length != lambda.Length)
            {
                return Invalid("IqsDataPack.DelayedGroups.Dimension", "delayed_neutron_data", "A positive delayed-neutron group count requires equal fraction and decay-constant array lengths.");
            }

            if (velocities.Length != 2)
            {
                return Invalid("IqsDataPack.Velocities.Dimension", "delayed_neutron_data.group_velocities_m_per_s", "Exactly two energy-group velocities are required.");
            }

            if (!IsFinitePositive(dto.DelayedNeutronData.BetaTotal) ||
                dto.DelayedNeutronData.BetaTotal >= 1.0 ||
                !AllFinitePositive(beta) || !AllFinitePositive(lambda) || !AllFinitePositive(velocities))
            {
                return Invalid("IqsDataPack.Kinetics.Invalid", "delayed_neutron_data", "Kinetics values must be finite and strictly positive.");
            }

            double betaSum = beta.Sum();
            if (Math.Abs(betaSum - dto.DelayedNeutronData.BetaTotal) > 1e-12)
            {
                return Invalid("IqsDataPack.BetaTotal.Mismatch", "delayed_neutron_data.beta_total", "The total delayed fraction must equal the ordered group-fraction sum.");
            }

            string[] groupFamilies;
            if (!dto.DelayedNeutronData.GroupFamiliesSpecified)
            {
                groupFamilies = Enumerable.Repeat(FissionGroupFamily, beta.Length).ToArray();
            }
            else if (dto.DelayedNeutronData.GroupFamilies == null)
            {
                return Invalid("IqsDataPack.GroupFamilies.Invalid", "delayed_neutron_data.group_families", "Delayed-source group families must be a string array when supplied.");
            }
            else if (dto.DelayedNeutronData.GroupFamilies.Length != beta.Length)
            {
                return Invalid("IqsDataPack.GroupFamilies.Dimension", "delayed_neutron_data.group_families", "Delayed-source group families must match the delayed-neutron group count.");
            }
            else
            {
                groupFamilies = dto.DelayedNeutronData.GroupFamilies;
            }

            for (int index = 0; index < groupFamilies.Length; index++)
            {
                if (!IsSupportedGroupFamily(groupFamilies[index]))
                {
                    return Invalid(
                        "IqsDataPack.GroupFamilies.Unsupported",
                        "delayed_neutron_data.group_families[" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]",
                        "Delayed-source group families must be the supported canonical values fission or photoneutron.");
                }
            }

            if (groupFamilies.Any(family => string.Equals(family, PhotoneutronGroupFamily, StringComparison.Ordinal)) &&
                !dto.SourceIdentitySpecified)
            {
                return Invalid(
                    "IqsDataPack.SourceIdentity.Missing",
                    "source_identity",
                    "A delayed-source pack containing photoneutron groups requires an explicit source identity.");
            }

            int[] delayedGroupOrder;
            if (!dto.DelayedNeutronData.GroupOrderSpecified)
            {
                delayedGroupOrder = Enumerable.Range(0, beta.Length).ToArray();
            }
            else if (dto.DelayedNeutronData.GroupOrder == null)
            {
                return Invalid("IqsDataPack.DelayedGroups.Order.Invalid", "delayed_neutron_data.group_order", "Delayed-source group order must be an integer array when supplied.");
            }
            else if (dto.DelayedNeutronData.GroupOrder.Length != beta.Length)
            {
                return Invalid("IqsDataPack.DelayedGroups.Order.Dimension", "delayed_neutron_data.group_order", "Delayed-source group order must match the delayed-neutron group count.");
            }
            else
            {
                delayedGroupOrder = dto.DelayedNeutronData.GroupOrder;
            }

            for (int index = 0; index < delayedGroupOrder.Length; index++)
            {
                if (delayedGroupOrder[index] != index)
                {
                    return Invalid(
                        "IqsDataPack.DelayedGroups.Order.Invalid",
                        "delayed_neutron_data.group_order[" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]",
                        "Delayed-source groups must preserve the explicit serialized order 0..G-1.");
                }
            }

            if (!IsFinitePositive(dto.TimeIntegration.GenerationTimeSeconds) ||
                !IsFinitePositive(dto.TimeIntegration.MaximumMicroStepSeconds) ||
                !IsFinitePositive(dto.TimeIntegration.ShapeRecomputeIntervalSeconds) ||
                dto.TimeIntegration.MaximumMicroStepSeconds > dto.TimeIntegration.ShapeRecomputeIntervalSeconds)
            {
                return Invalid("IqsDataPack.TimeIntegration.Invalid", "time_integration", "Adiabatic point-kinetics time constants must be finite, positive, and ordered micro-step <= shape-recompute interval.");
            }

            Digest32 contentDigest = new Digest32(
                Phase5CanonicalBytesV1.Sha256(Encoding.UTF8.GetBytes(json)));
            Digest32 topologyDigest = new Digest32(
                Phase5CanonicalBytesV1.Sha256(
                    Encoding.UTF8.GetBytes(Candu6CoreTopologyFactoryV1.GetTopologyIdentity())));

            return ContractValidationResult<IqsKineticsDataPackV1>.Valid(
                new IqsKineticsDataPackV1(
                    dto.DataPackVersion!,
                    dto.SourceIdentity ?? dto.DataPackVersion!,
                    dto.EvidenceClass!,
                    dto.SourceProvenance!,
                    dto.ModelId!,
                    dto.SolverId!,
                    dto.FormulationId ?? AdiabaticKineticsIdentityV1.FormulationId,
                    dto.ShapeMethodId ?? AdiabaticKineticsIdentityV1.ShapeMethodId,
                    dto.AmplitudeMethodId ?? AdiabaticKineticsIdentityV1.AmplitudeMethodId,
                    AdiabaticKineticsIdentityV1.ReactivityMethodId,
                    dto.EnergyGroupOrder!,
                    groupFamilies,
                    delayedGroupOrder,
                    dto.DelayedNeutronData.BetaTotal,
                    beta,
                    lambda,
                    velocities,
                    dto.TimeIntegration.GenerationTimeSeconds,
                    dto.TimeIntegration.MaximumMicroStepSeconds,
                    dto.TimeIntegration.ShapeRecomputeIntervalSeconds,
                    contentDigest,
                    topologyDigest));
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

        private static int[]? ReadIntArray(JToken? token)
        {
            if (!(token is JArray array))
            {
                return null;
            }

            var values = new int[array.Count];
            for (int index = 0; index < array.Count; index++)
            {
                JToken item = array[index];
                if (item.Type != JTokenType.Integer)
                {
                    return null;
                }

                try
                {
                    values[index] = item.Value<int>();
                }
                catch (Exception exception) when (exception is FormatException || exception is OverflowException)
                {
                    return null;
                }
            }

            return values;
        }

        private static DelayedNeutronDto? ReadDelayedNeutronData(JToken? token)
        {
            if (!(token is JObject section))
            {
                return null;
            }

            JToken? groupFamiliesToken = section["group_families"];
            JToken? groupOrderToken = section["group_order"];

            return new DelayedNeutronDto
            {
                BetaTotal = ReadDouble(section["beta_total"]),
                GroupFractions = ReadDoubleArray(section["group_fractions"]),
                DecayConstantsPerSec = ReadDoubleArray(section["decay_constants_per_sec"]),
                GroupVelocitiesMPerS = ReadDoubleArray(section["group_velocities_m_per_s"]),
                GroupFamilies = ReadStringArray(groupFamiliesToken),
                GroupFamiliesSpecified = groupFamiliesToken != null,
                GroupOrder = ReadIntArray(groupOrderToken),
                GroupOrderSpecified = groupOrderToken != null
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
                return ContractValidationResult<bool>.Invalid("IqsDataPack.Identity.Missing", "identity", "Pack version, evidence class, and provenance are required.");
            }

            if (dto.SourceIdentitySpecified && string.IsNullOrWhiteSpace(dto.SourceIdentity))
            {
                return ContractValidationResult<bool>.Invalid("IqsDataPack.SourceIdentity.Invalid", "source_identity", "A supplied source identity must be a nonempty string.");
            }

            if (!string.Equals(dto.TopologySchemaId, SupportedTopologySchemaId, StringComparison.Ordinal) ||
                !string.Equals(dto.UnitsProfileId, SupportedUnitsProfileId, StringComparison.Ordinal) ||
                (!string.Equals(dto.ModelId, SupportedModelId, StringComparison.Ordinal) &&
                 !string.Equals(dto.ModelId, LegacyModelId, StringComparison.Ordinal)) ||
                (!string.Equals(dto.SolverId, SupportedSolverId, StringComparison.Ordinal) &&
                 !string.Equals(dto.SolverId, LegacySolverId, StringComparison.Ordinal)))
            {
                return ContractValidationResult<bool>.Invalid("IqsDataPack.Identity.Unsupported", "identity", "The adiabatic kinetics topology, units, model, or solver identity is unsupported.");
            }

            if ((dto.FormulationIdSpecified &&
                 !string.Equals(dto.FormulationId, AdiabaticKineticsIdentityV1.FormulationId, StringComparison.Ordinal)) ||
                (dto.ShapeMethodIdSpecified &&
                 !string.Equals(dto.ShapeMethodId, AdiabaticKineticsIdentityV1.ShapeMethodId, StringComparison.Ordinal)) ||
                (dto.AmplitudeMethodIdSpecified &&
                 !string.Equals(dto.AmplitudeMethodId, AdiabaticKineticsIdentityV1.AmplitudeMethodId, StringComparison.Ordinal)) ||
                (dto.ReactivityMethodIdSpecified &&
                 !string.Equals(dto.ReactivityMethodId, AdiabaticKineticsIdentityV1.ReactivityMethodId, StringComparison.Ordinal) &&
                 !string.Equals(dto.ReactivityMethodId, AdiabaticKineticsIdentityV1.LegacyReactivityMethodId, StringComparison.Ordinal)))
            {
                return ContractValidationResult<bool>.Invalid(
                    "IqsDataPack.Formulation.Unsupported",
                    "formulation_id",
                    "The active pack must identify the adiabatic static-k-eigenmode shape, point-kinetics amplitude, and adjoint-weighted first-order reactivity methods.");
            }

            if (dto.EnergyGroupOrder == null || dto.EnergyGroupOrder.Length != 2 ||
                !string.Equals(dto.EnergyGroupOrder[0], "fast", StringComparison.Ordinal) ||
                !string.Equals(dto.EnergyGroupOrder[1], "thermal", StringComparison.Ordinal))
            {
                return ContractValidationResult<bool>.Invalid("IqsDataPack.EnergyGroupOrder.Unsupported", "energy_group_order", "The supported energy-group order is [fast, thermal].");
            }

            return ContractValidationResult<bool>.Valid(true);
        }

        private static bool IsSupportedGroupFamily(string family)
        {
            return string.Equals(family, FissionGroupFamily, StringComparison.Ordinal) ||
                   string.Equals(family, PhotoneutronGroupFamily, StringComparison.Ordinal);
        }

        private static bool IsFinitePositive(double value)
        {
            return ContractValidation.IsFinite(value) && value > 0.0;
        }

        private static bool AllFinitePositive(IEnumerable<double> values)
        {
            return values.All(IsFinitePositive);
        }

        private static ContractValidationResult<IqsKineticsDataPackV1> Invalid(string code, string path, string message)
        {
            return ContractValidationResult<IqsKineticsDataPackV1>.Invalid(code, path, message);
        }

        private sealed class IqsPackDto
        {
            [JsonProperty("schema_version")] public uint SchemaVersion { get; set; }
            [JsonProperty("data_pack_version")] public string? DataPackVersion { get; set; }
            [JsonProperty("source_identity")] public string? SourceIdentity { get; set; }
            public bool SourceIdentitySpecified { get; set; }
            [JsonProperty("topology_schema_id")] public string? TopologySchemaId { get; set; }
            [JsonProperty("units_profile_id")] public string? UnitsProfileId { get; set; }
            [JsonProperty("model_id")] public string? ModelId { get; set; }
            [JsonProperty("solver_id")] public string? SolverId { get; set; }
            [JsonProperty("formulation_id")] public string? FormulationId { get; set; }
            public bool FormulationIdSpecified { get; set; }
            [JsonProperty("shape_method_id")] public string? ShapeMethodId { get; set; }
            public bool ShapeMethodIdSpecified { get; set; }
            [JsonProperty("amplitude_method_id")] public string? AmplitudeMethodId { get; set; }
            public bool AmplitudeMethodIdSpecified { get; set; }
            [JsonProperty("reactivity_method_id")] public string? ReactivityMethodId { get; set; }
            public bool ReactivityMethodIdSpecified { get; set; }
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
            [JsonProperty("group_velocities_m_per_s")] public double[]? GroupVelocitiesMPerS { get; set; }
            [JsonProperty("group_families")] public string[]? GroupFamilies { get; set; }
            public bool GroupFamiliesSpecified { get; set; }
            [JsonProperty("group_order")] public int[]? GroupOrder { get; set; }
            public bool GroupOrderSpecified { get; set; }
        }

        private sealed class TimeIntegrationDto
        {
            [JsonProperty("generation_time_seconds")] public double GenerationTimeSeconds { get; set; }
            [JsonProperty("maximum_micro_step_seconds")] public double MaximumMicroStepSeconds { get; set; }
            [JsonProperty("shape_recompute_interval_seconds")] public double ShapeRecomputeIntervalSeconds { get; set; }
        }
    }

    /// <summary>
    /// Immutable normalized static-eigenmode shape candidate. Candidate
    /// construction never mutates the owning solver, allowing previews and
    /// failed refuelling solves to remain atomic. The type name is retained as
    /// a legacy public API name.
    /// </summary>
    public sealed class IqsSpatialCandidateV1
    {
        private readonly ReadOnlyCollection<double> _shapeGroup1;
        private readonly ReadOnlyCollection<double> _shapeGroup2;
        private readonly ReadOnlyCollection<double> _shapeNodePowerWatts;

        internal IqsSpatialCandidateV1(
            object owner,
            FullCoreDiffusionSolveResultV1 spatialSolve,
            IEnumerable<double> shapeGroup1,
            IEnumerable<double> shapeGroup2,
            IEnumerable<double> shapeNodePowerWatts,
            double shapePowerWatts,
            double constraint,
            double staticRelativeReactivity,
            AdjointWeightedReactivityResultV1 weightedReactivity)
        {
            Owner = owner;
            SpatialSolve = spatialSolve;
            _shapeGroup1 = new ReadOnlyCollection<double>(shapeGroup1.ToArray());
            _shapeGroup2 = new ReadOnlyCollection<double>(shapeGroup2.ToArray());
            _shapeNodePowerWatts = new ReadOnlyCollection<double>(shapeNodePowerWatts.ToArray());
            ShapePowerWatts = shapePowerWatts;
            Constraint = constraint;
            StaticRelativeReactivity = staticRelativeReactivity;
            WeightedReactivity = weightedReactivity;
        }

        internal object Owner { get; }
        public FullCoreDiffusionSolveResultV1 SpatialSolve { get; }
        public IReadOnlyList<double> ShapeGroup1 { get { return _shapeGroup1; } }
        public IReadOnlyList<double> ShapeGroup2 { get { return _shapeGroup2; } }
        public IReadOnlyList<double> ShapeNodePowerWatts { get { return _shapeNodePowerWatts; } }
        public double ShapePowerWatts { get; }
        public double Constraint { get; }
        public double StaticReactivity
        {
            get { return SpatialSolve.Reactivity; }
        }

        /// <summary>
        /// Compatibility-facing operational value. It is the B2
        /// adjoint-weighted first-order perturbation reactivity; the static
        /// k/rho diagnostic remains available as StaticReactivity.
        /// </summary>
        public double RelativeReactivity
        {
            get { return WeightedReactivity.Reactivity; }
        }

        public double StaticRelativeReactivity { get; }

        public AdjointWeightedReactivityResultV1 WeightedReactivity { get; }

        public double WeightedPerturbationReactivity
        {
            get { return WeightedReactivity.Reactivity; }
        }

        public double ReactivityNumerator
        {
            get { return WeightedReactivity.Numerator; }
        }

        public double ReactivityDenominator
        {
            get { return WeightedReactivity.Denominator; }
        }

        public string ReactivityIdentity
        {
            get { return WeightedReactivity.Identity; }
        }

        public Digest32 ReactivityBindingDigest
        {
            get { return WeightedReactivity.BindingDigest; }
        }

        public string ReactivityBindingDigestHex
        {
            get { return WeightedReactivity.BindingDigestHex; }
        }
    }

    /// <summary>
    /// Legacy <c>IqsFullCoreSolver</c> API entry point for the active adiabatic
    /// path. The deterministic full-core static k-eigenmode solve supplies the
    /// recomputed shape, while the ordered delayed-source groups advance a
    /// scalar point-kinetics amplitude. It does not solve a time-dependent
    /// fixed-source IQS shape equation. The uniqueness constraint is bound to
    /// the deterministic reference transpose adjoint carried by
    /// <see cref="ReferenceAdjoint"/>.
    /// </summary>
    public sealed class IqsFullCoreSolver
    {
        private readonly FullCoreDiffusionModelV1 _spatialModel;
        private readonly IqsKineticsDataPackV1 _dataPack;
        private readonly double _targetPowerWatts;
        private readonly double[] _precursors;
        private IqsSpatialCandidateV1 _current;
        private double _amplitude;
        private readonly FullCoreDiffusionSolveResultV1 _referenceSpatialSolve;
        private readonly double _referenceStaticReactivity;
        private readonly string _staticReactivityMethodId;
        private readonly FullCoreAdjointImportanceV1 _referenceAdjoint;
        private readonly double _shapeConstraint;

        private IqsFullCoreSolver(
            FullCoreDiffusionModelV1 spatialModel,
            IqsKineticsDataPackV1 dataPack,
            double targetPowerWatts,
            FullCoreDiffusionSolveResultV1 initialSpatialSolve,
            FullCoreAdjointImportanceV1 referenceAdjoint)
        {
            _spatialModel = spatialModel;
            _dataPack = dataPack;
            _targetPowerWatts = targetPowerWatts;
            _amplitude = 1.0;
            _precursors = new double[_dataPack.DelayedGroupCount];
            _referenceSpatialSolve = initialSpatialSolve;
            _referenceAdjoint = referenceAdjoint;
            _referenceStaticReactivity = initialSpatialSolve.Reactivity;
            _staticReactivityMethodId = AdiabaticKineticsIdentityV1.StaticReactivityMethodId;
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
        public FullCoreAdjointImportanceV1 ReferenceAdjoint { get { return _referenceAdjoint; } }
        public Digest32 ReferenceAdjointDigest { get { return _referenceAdjoint.Digest; } }
        public string AdjointNormalizationIdentity { get { return _referenceAdjoint.NormalizationIdentity; } }
        public int AdjointIterationCount { get { return _referenceAdjoint.IterationCount; } }
        public double AdjointTransposeResidualRelativeInfinity
        {
            get { return _referenceAdjoint.TransposeResidualRelativeInfinity; }
        }
        public double RelativeReactivity { get { return _current.RelativeReactivity; } }
        public double WeightedPerturbationReactivity
        {
            get { return _current.WeightedPerturbationReactivity; }
        }
        public double StaticReactivity { get { return _current.StaticReactivity; } }
        public double StaticRelativeReactivity
        {
            get { return _current.StaticRelativeReactivity; }
        }
        public double ReactivityNumerator
        {
            get { return _current.ReactivityNumerator; }
        }
        public double ReactivityDenominator
        {
            get { return _current.ReactivityDenominator; }
        }
        public string ReactivityIdentity
        {
            get { return _current.ReactivityIdentity; }
        }
        public string ReactivityBindingDigestHex
        {
            get { return _current.ReactivityBindingDigestHex; }
        }
        public double GenerationTimeSeconds { get { return _dataPack.GenerationTimeSeconds; } }
        public double ShapeConstraint { get { return _shapeConstraint; } }
        public string FormulationId { get { return _dataPack.FormulationId; } }
        public string ShapeMethodId { get { return _dataPack.ShapeMethodId; } }
        public string AmplitudeMethodId { get { return _dataPack.AmplitudeMethodId; } }
        public string ReactivityMethodId { get { return _dataPack.ReactivityMethodId; } }
        public string StaticReactivityMethodId
        {
            get { return _staticReactivityMethodId; }
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
                return Invalid("IqsFullCoreSolver.SpatialModel.Missing", "spatial_model", "An adiabatic solver requires a full-core spatial model.");
            }

            if (dataPack == null)
            {
                return Invalid("IqsFullCoreSolver.DataPack.Missing", "data_pack", "An adiabatic solver requires a validated kinetics pack.");
            }

            if (bundles == null)
            {
                return Invalid("IqsFullCoreSolver.Bundles.Missing", "bundles", "An adiabatic solver requires a full-core bundle inventory.");
            }

            if (!ContractValidation.IsFinite(targetPowerWatts) || targetPowerWatts <= 0.0)
            {
                return Invalid("IqsFullCoreSolver.TargetPower.Invalid", "target_power_w", "The adiabatic reference power must be finite and positive SI watts.");
            }

            BundleState[] bundleRecords = bundles.ToArray();
            ContractValidationResult<FullCoreDiffusionSolveResultV1> spatial =
                spatialModel.TrySolve(bundleRecords, targetPowerWatts);
            if (!spatial.IsValid)
            {
                return Invalid(spatial.FirstDiagnostic.Code, spatial.FirstDiagnostic.Path, spatial.FirstDiagnostic.Message);
            }

            if (!spatialModel.DataPack.EnergyGroupOrder.SequenceEqual(
                    dataPack.EnergyGroupOrder,
                    StringComparer.Ordinal))
            {
                return Invalid(
                    "IqsFullCoreSolver.EnergyGroupOrder.Mismatch",
                    "energy_group_order",
                    "The kinetics and diffusion packs must use the exact same ordered energy groups.");
            }

            ContractValidationResult<FullCoreAdjointImportanceV1> referenceAdjoint =
                spatialModel.TrySolveReferenceAdjoint(
                    bundleRecords,
                    dataPack,
                    spatial.Value.EffectiveK);
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
                return ContractValidationResult<IqsSpatialCandidateV1>.Invalid("IqsFullCoreSolver.Bundles.Missing", "bundles", "A static-eigenmode shape solve requires a full-core bundle inventory.");
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
        /// Builds a side-effect-free adiabatic candidate from one validated
        /// dynamic-Xe coefficient overlay. The owning solver is changed only
        /// by <see cref="TryCommitCandidate"/> after the caller has accepted
        /// the complete transaction.
        /// </summary>
        public ContractValidationResult<IqsSpatialCandidateV1> TrySolveCandidate(
            IEnumerable<BundleState> bundles,
            XenonSpatialCouplingResultV1 xenonCoupling)
        {
            return TrySolveCandidate(
                bundles,
                xenonCoupling,
                _current.SpatialSolve);
        }

        /// <summary>
        /// Builds a dynamic-Xe candidate using an explicit side-effect-free
        /// warm start. The caller can carry the last staged candidate through
        /// several scheduled boundaries without mutating the authoritative
        /// solver projection before the enclosing transaction commits.
        /// </summary>
        public ContractValidationResult<IqsSpatialCandidateV1> TrySolveCandidate(
            IEnumerable<BundleState> bundles,
            XenonSpatialCouplingResultV1 xenonCoupling,
            FullCoreDiffusionSolveResultV1 initialSpatialSolve)
        {
            if (bundles == null)
            {
                return ContractValidationResult<IqsSpatialCandidateV1>.Invalid(
                    "IqsFullCoreSolver.Bundles.Missing",
                    "bundles",
                    "A dynamic-Xe static-eigenmode shape solve requires a full-core bundle inventory.");
            }

            if (xenonCoupling == null)
            {
                return ContractValidationResult<IqsSpatialCandidateV1>.Invalid(
                    "IqsFullCoreSolver.XenonCoupling.Missing",
                    "xenon_coupling",
                    "A dynamic-Xe static-eigenmode shape solve requires a validated coupling result.");
            }

            if (initialSpatialSolve == null)
            {
                return ContractValidationResult<IqsSpatialCandidateV1>.Invalid(
                    "IqsFullCoreSolver.InitialSpatialSolve.Missing",
                    "initial_spatial_solve",
                    "A dynamic-Xe static-eigenmode shape solve requires an explicit warm-start spatial solve.");
            }

            ContractValidationResult<FullCoreDiffusionSolveResultV1> spatial =
                _spatialModel.TrySolve(
                    bundles,
                    xenonCoupling,
                    _targetPowerWatts,
                    initialSpatialSolve.EffectiveK,
                    initialSpatialSolve.Group1Flux,
                    initialSpatialSolve.Group2Flux);
            if (!spatial.IsValid)
            {
                return ContractValidationResult<IqsSpatialCandidateV1>.Invalid(
                    spatial.FirstDiagnostic.Code,
                    spatial.FirstDiagnostic.Path,
                    spatial.FirstDiagnostic.Message);
            }

            try
            {
                return ContractValidationResult<IqsSpatialCandidateV1>.Valid(
                    BuildCandidate(spatial.Value));
            }
            catch (InvalidOperationException exception)
            {
                return ContractValidationResult<IqsSpatialCandidateV1>.Invalid(
                    "IqsFullCoreSolver.Candidate.Invalid",
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

        public ContractValidationResult<double> TryAdvancePointKinetics(double dtSeconds)
        {
            if (!ContractValidation.IsFinite(dtSeconds) || dtSeconds <= 0.0 ||
                dtSeconds > _dataPack.MaximumMicroStepSeconds)
            {
                return ContractValidationResult<double>.Invalid("IqsFullCoreSolver.TimeStep.Invalid", "dt_seconds", "The adiabatic point-kinetics step must be finite, positive, and no greater than the pack micro-step limit.");
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
                dtSeconds * (RelativeReactivity - _dataPack.BetaTotal) /
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
            ContractValidationResult<AdjointWeightedReactivityResultV1> weightedReactivity =
                AdjointWeightedReactivityV1.TryCompute(
                    _referenceAdjoint,
                    _referenceSpatialSolve,
                    spatial,
                    _dataPack);
            if (!weightedReactivity.IsValid)
            {
                throw new InvalidOperationException(weightedReactivity.FirstDiagnostic.ToString());
            }

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
                spatial.Reactivity - _referenceStaticReactivity,
                weightedReactivity.Value);
        }

        private double ComputeConstraint(IReadOnlyList<double> group1, IReadOnlyList<double> group2)
        {
            if (group1.Count != _spatialModel.NodeCount ||
                group2.Count != _spatialModel.NodeCount ||
                _referenceAdjoint.Group1Importance.Count != _spatialModel.NodeCount ||
                _referenceAdjoint.Group2Importance.Count != _spatialModel.NodeCount)
            {
                throw new InvalidOperationException("The static-eigenmode shape dimensions do not match the full-core model.");
            }

            double inverseVelocity1 = 1.0 / _dataPack.GroupVelocitiesMPerSecond[0];
            double inverseVelocity2 = 1.0 / _dataPack.GroupVelocitiesMPerSecond[1];
            double nodeVolume = _spatialModel.DataPack.NodeVolumeM3;
            double constraint = 0.0;
            for (int index = 0; index < group1.Count; index++)
            {
                double contribution = nodeVolume * (
                    _referenceAdjoint.Group1Importance[index] * group1[index] * inverseVelocity1 +
                    _referenceAdjoint.Group2Importance[index] * group2[index] * inverseVelocity2);
                if (!ContractValidation.IsFinite(contribution) || contribution < 0.0)
                {
                    throw new InvalidOperationException("The reference adjoint-weighted shape constraint is invalid.");
                }

                constraint += contribution;
                if (!ContractValidation.IsFinite(constraint))
                {
                    throw new InvalidOperationException("The reference adjoint-weighted shape constraint became non-finite.");
                }
            }

            return constraint;
        }

        private static ContractValidationResult<IqsFullCoreSolver> Invalid(string code, string path, string message)
        {
            return ContractValidationResult<IqsFullCoreSolver>.Invalid(code, path, message);
        }
    }
}
