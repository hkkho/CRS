using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;

namespace ReactorSim.Core
{
    /// <summary>
    /// Validated synthetic kinetics metadata for the full-core IQS adapter.
    /// The spatial coefficients remain in the separately versioned diffusion pack.
    /// </summary>
    public sealed class IqsKineticsDataPackV1
    {
        public const uint CurrentSchemaVersion = 1;
        public const string SupportedTopologySchemaId = "candu6-380x12-grid-v1";
        public const string SupportedUnitsProfileId = "SI-v1";
        public const string SupportedModelId = "candu6-two-group-iqs-full-core-v1";
        public const string SupportedSolverId = "spatial-eigen-iqs-v1";
        public const string EmbeddedResourceName =
            "ReactorSim.Core.Data.candu6-two-group-iqs-pack-v1.json";

        private readonly ReadOnlyCollection<string> _energyGroupOrder;
        private readonly ReadOnlyCollection<double> _betaGroups;
        private readonly ReadOnlyCollection<double> _decayConstants;
        private readonly ReadOnlyCollection<double> _groupVelocities;

        private IqsKineticsDataPackV1(
            string dataPackVersion,
            string evidenceClass,
            string sourceProvenance,
            IEnumerable<string> energyGroupOrder,
            double betaTotal,
            IEnumerable<double> betaGroups,
            IEnumerable<double> decayConstants,
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
            _groupVelocities = new ReadOnlyCollection<double>(groupVelocities.ToArray());
            GenerationTimeSeconds = generationTimeSeconds;
            MaximumMicroStepSeconds = maximumMicroStepSeconds;
            ShapeRecomputeIntervalSeconds = shapeRecomputeIntervalSeconds;
            TopologySchemaId = SupportedTopologySchemaId;
            UnitsProfileId = SupportedUnitsProfileId;
            ModelId = SupportedModelId;
            SolverId = SupportedSolverId;
        }

        public string DataPackVersion { get; }
        public string TopologySchemaId { get; }
        public string UnitsProfileId { get; }
        public string ModelId { get; }
        public string SolverId { get; }
        public string EvidenceClass { get; }
        public string SourceProvenance { get; }
        public IReadOnlyList<string> EnergyGroupOrder { get { return _energyGroupOrder; } }
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
                        "The embedded CANDU-6 IQS pack could not be found in ReactorSim.Core.");
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
                return Invalid("IqsDataPack.Json.Empty", "json", "An IQS data pack JSON document is required.");
            }

            IqsPackDto? dto;
            try
            {
                dto = JsonConvert.DeserializeObject<IqsPackDto>(json);
            }
            catch (JsonException exception)
            {
                return Invalid("IqsDataPack.Json.Invalid", "json", "The IQS data pack is not valid JSON: " + exception.Message);
            }

            if (dto == null)
            {
                return Invalid("IqsDataPack.Json.Invalid", "json", "The IQS data pack did not contain an object.");
            }

            if (dto.SchemaVersion != CurrentSchemaVersion)
            {
                return Invalid("IqsDataPack.SchemaVersion.Unsupported", "schema_version", "Only IQS schema version 1 is supported.");
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
            if (beta.Length != 6 || lambda.Length != 6)
            {
                return Invalid("IqsDataPack.DelayedGroups.Dimension", "delayed_neutron_data", "Exactly six delayed-neutron fractions and decay constants are required.");
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

            if (!IsFinitePositive(dto.TimeIntegration.GenerationTimeSeconds) ||
                !IsFinitePositive(dto.TimeIntegration.MaximumMicroStepSeconds) ||
                !IsFinitePositive(dto.TimeIntegration.ShapeRecomputeIntervalSeconds) ||
                dto.TimeIntegration.MaximumMicroStepSeconds > dto.TimeIntegration.ShapeRecomputeIntervalSeconds)
            {
                return Invalid("IqsDataPack.TimeIntegration.Invalid", "time_integration", "IQS time constants must be finite, positive, and ordered micro-step <= macro-step.");
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
                    velocities,
                    dto.TimeIntegration.GenerationTimeSeconds,
                    dto.TimeIntegration.MaximumMicroStepSeconds,
                    dto.TimeIntegration.ShapeRecomputeIntervalSeconds));
        }

        private static ContractValidationResult<bool> ValidateIdentity(IqsPackDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.DataPackVersion) ||
                string.IsNullOrWhiteSpace(dto.EvidenceClass) ||
                string.IsNullOrWhiteSpace(dto.SourceProvenance))
            {
                return ContractValidationResult<bool>.Invalid("IqsDataPack.Identity.Missing", "identity", "Pack version, evidence class, and provenance are required.");
            }

            if (!string.Equals(dto.TopologySchemaId, SupportedTopologySchemaId, StringComparison.Ordinal) ||
                !string.Equals(dto.UnitsProfileId, SupportedUnitsProfileId, StringComparison.Ordinal) ||
                !string.Equals(dto.ModelId, SupportedModelId, StringComparison.Ordinal) ||
                !string.Equals(dto.SolverId, SupportedSolverId, StringComparison.Ordinal))
            {
                return ContractValidationResult<bool>.Invalid("IqsDataPack.Identity.Unsupported", "identity", "The IQS topology, units, model, or solver identity is unsupported.");
            }

            if (dto.EnergyGroupOrder == null || dto.EnergyGroupOrder.Length != 2 ||
                !string.Equals(dto.EnergyGroupOrder[0], "fast", StringComparison.Ordinal) ||
                !string.Equals(dto.EnergyGroupOrder[1], "thermal", StringComparison.Ordinal))
            {
                return ContractValidationResult<bool>.Invalid("IqsDataPack.EnergyGroupOrder.Unsupported", "energy_group_order", "The supported energy-group order is [fast, thermal].");
            }

            return ContractValidationResult<bool>.Valid(true);
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
            [JsonProperty("topology_schema_id")] public string? TopologySchemaId { get; set; }
            [JsonProperty("units_profile_id")] public string? UnitsProfileId { get; set; }
            [JsonProperty("model_id")] public string? ModelId { get; set; }
            [JsonProperty("solver_id")] public string? SolverId { get; set; }
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
        }

        private sealed class TimeIntegrationDto
        {
            [JsonProperty("generation_time_seconds")] public double GenerationTimeSeconds { get; set; }
            [JsonProperty("maximum_micro_step_seconds")] public double MaximumMicroStepSeconds { get; set; }
            [JsonProperty("shape_recompute_interval_seconds")] public double ShapeRecomputeIntervalSeconds { get; set; }
        }
    }

    /// <summary>
    /// Immutable normalized IQS shape candidate. Candidate construction never
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
            double relativeReactivity)
        {
            Owner = owner;
            SpatialSolve = spatialSolve;
            _shapeGroup1 = new ReadOnlyCollection<double>(shapeGroup1.ToArray());
            _shapeGroup2 = new ReadOnlyCollection<double>(shapeGroup2.ToArray());
            _shapeNodePowerWatts = new ReadOnlyCollection<double>(shapeNodePowerWatts.ToArray());
            ShapePowerWatts = shapePowerWatts;
            Constraint = constraint;
            RelativeReactivity = relativeReactivity;
        }

        internal IqsFullCoreSolver Owner { get; }
        public FullCoreDiffusionSolveResultV1 SpatialSolve { get; }
        public IReadOnlyList<double> ShapeGroup1 { get { return _shapeGroup1; } }
        public IReadOnlyList<double> ShapeGroup2 { get { return _shapeGroup2; } }
        public IReadOnlyList<double> ShapeNodePowerWatts { get { return _shapeNodePowerWatts; } }
        public double ShapePowerWatts { get; }
        public double Constraint { get; }
        public double RelativeReactivity { get; }
    }

    /// <summary>
    /// Improved quasi-static adapter: the existing deterministic full-core
    /// diffusion solve supplies the slow shape, while six-group point kinetics
    /// advances the scalar amplitude. A fixed flat synthetic adjoint is used only
    /// for the uniqueness constraint until an admitted adjoint data pack exists.
    /// </summary>
    public sealed class IqsFullCoreSolver
    {
        private readonly FullCoreDiffusionModelV1 _spatialModel;
        private readonly IqsKineticsDataPackV1 _dataPack;
        private readonly double _targetPowerWatts;
        private readonly double[] _precursors = new double[6];
        private IqsSpatialCandidateV1 _current;
        private double _amplitude;
        private readonly double _referenceReactivity;
        private readonly double _shapeConstraint;

        private IqsFullCoreSolver(
            FullCoreDiffusionModelV1 spatialModel,
            IqsKineticsDataPackV1 dataPack,
            double targetPowerWatts,
            FullCoreDiffusionSolveResultV1 initialSpatialSolve)
        {
            _spatialModel = spatialModel;
            _dataPack = dataPack;
            _targetPowerWatts = targetPowerWatts;
            _amplitude = 1.0;
            _referenceReactivity = initialSpatialSolve.Reactivity;
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
        public double GenerationTimeSeconds { get { return _dataPack.GenerationTimeSeconds; } }
        public double ShapeConstraint { get { return _shapeConstraint; } }
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

            if (bundles == null)
            {
                return Invalid("IqsFullCoreSolver.Bundles.Missing", "bundles", "An IQS solver requires a full-core bundle inventory.");
            }

            if (!ContractValidation.IsFinite(targetPowerWatts) || targetPowerWatts <= 0.0)
            {
                return Invalid("IqsFullCoreSolver.TargetPower.Invalid", "target_power_w", "The IQS reference power must be finite and positive SI watts.");
            }

            ContractValidationResult<FullCoreDiffusionSolveResultV1> spatial =
                spatialModel.TrySolve(bundles, targetPowerWatts);
            if (!spatial.IsValid)
            {
                return Invalid(spatial.FirstDiagnostic.Code, spatial.FirstDiagnostic.Path, spatial.FirstDiagnostic.Message);
            }

            try
            {
                var solver = new IqsFullCoreSolver(spatialModel, dataPack, targetPowerWatts, spatial.Value);
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
                return ContractValidationResult<double>.Invalid("IqsFullCoreSolver.TimeStep.Invalid", "dt_seconds", "The point-kinetics step must be finite, positive, and no greater than the pack micro-step limit.");
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
                spatial.Reactivity - _referenceReactivity);
        }

        private double ComputeConstraint(IReadOnlyList<double> group1, IReadOnlyList<double> group2)
        {
            if (group1.Count != _spatialModel.NodeCount || group2.Count != _spatialModel.NodeCount)
            {
                throw new InvalidOperationException("The IQS shape dimensions do not match the full-core model.");
            }

            double inverseVelocity1 = 1.0 / _dataPack.GroupVelocitiesMPerSecond[0];
            double inverseVelocity2 = 1.0 / _dataPack.GroupVelocitiesMPerSecond[1];
            double nodeVolume = _spatialModel.DataPack.NodeVolumeM3;
            double constraint = 0.0;
            for (int index = 0; index < group1.Count; index++)
            {
                constraint += (inverseVelocity1 * group1[index] + inverseVelocity2 * group2[index]) * nodeVolume;
            }

            return constraint;
        }

        private static ContractValidationResult<IqsFullCoreSolver> Invalid(string code, string path, string message)
        {
            return ContractValidationResult<IqsFullCoreSolver>.Invalid(code, path, message);
        }
    }
}
