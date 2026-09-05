using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ReactorSim.Core
{
    /// <summary>
    /// Identifies the source family represented by one delayed-neutron group.
    /// The explicit family keeps supplied fission and future heavy-water
    /// photoneutron data distinguishable in a versioned data pack.  The
    /// current adiabatic adapter admits fission groups only; photoneutron
    /// source laws remain a schema capability until separately admitted.
    /// </summary>
    public enum DelayedNeutronFamilyV1 : byte
    {
        Fission = 0,
        Photoneutron = 1
    }

    /// <summary>
    /// One caller-supplied delayed-neutron precursor group. The group index and
    /// family are part of the serialized order; this type does not supply
    /// nuclear data.
    /// </summary>
    public sealed class DelayedNeutronGroupV1
    {
        private DelayedNeutronGroupV1(
            int groupIndex,
            double betaFraction,
            double decayConstantPerSecond,
            DelayedNeutronFamilyV1 family)
        {
            GroupIndex = groupIndex;
            BetaFraction = betaFraction;
            DecayConstantPerSecond = decayConstantPerSecond;
            Family = family;
        }

        public int GroupIndex { get; }

        public double BetaFraction { get; }

        public double DecayConstantPerSecond { get; }

        public DelayedNeutronFamilyV1 Family { get; }

        /// <summary>
        /// Creates a fission delayed-neutron group for compatibility with the
        /// original three-argument API.
        /// </summary>
        public static ContractValidationResult<DelayedNeutronGroupV1> TryCreate(
            int groupIndex,
            double betaFraction,
            double decayConstantPerSecond)
        {
            return TryCreate(
                groupIndex,
                betaFraction,
                decayConstantPerSecond,
                DelayedNeutronFamilyV1.Fission);
        }

        public static ContractValidationResult<DelayedNeutronGroupV1> TryCreate(
            int groupIndex,
            double betaFraction,
            double decayConstantPerSecond,
            DelayedNeutronFamilyV1 family)
        {
            if (groupIndex < 0)
            {
                return Invalid(
                    "DelayedNeutronGroup.GroupIndex.Invalid",
                    "group_index",
                    "The delayed-neutron group index must be nonnegative.");
            }

            if (!KineticContractValidation.IsCanonicalNonnegativeFinite(betaFraction) ||
                betaFraction >= 1.0)
            {
                return Invalid(
                    "DelayedNeutronGroup.BetaFraction.Invalid",
                    "beta_fraction",
                    "The delayed-neutron fraction must be finite, canonical, and in [0,1).");
            }

            if (!Enum.IsDefined(typeof(DelayedNeutronFamilyV1), family))
            {
                return Invalid(
                    "DelayedNeutronGroup.Family.Invalid",
                    "family",
                    "The delayed-neutron group family must be a supported fission or photoneutron value.");
            }

            if (!KineticContractValidation.IsPositiveFinite(decayConstantPerSecond))
            {
                return Invalid(
                    "DelayedNeutronGroup.DecayConstant.Invalid",
                    "decay_constant_s_inv",
                    "The precursor decay constant must be finite and strictly positive SI s^-1.");
            }

            return ContractValidationResult<DelayedNeutronGroupV1>.Valid(
                new DelayedNeutronGroupV1(
                    groupIndex,
                    betaFraction,
                    decayConstantPerSecond,
                    family));
        }

        private static ContractValidationResult<DelayedNeutronGroupV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<DelayedNeutronGroupV1>.Invalid(code, path, message);
        }
    }

    /// <summary>
    /// Versioned, identity-bound delayed-neutron coefficients. All values are
    /// supplied by the caller; no production constants or initial conditions
    /// are inferred here. The ordered collection accepts any positive group
    /// count, and each group's family remains explicit in the collection.
    /// </summary>
    public sealed class DelayedNeutronDataV1
    {
        public const uint CurrentSchemaVersion = 1;

        private DelayedNeutronDataV1(
            StableId dataId,
            string dataVersion,
            Digest32 dataDigest,
            double promptGenerationTimeSeconds,
            IEnumerable<DelayedNeutronGroupV1> groups,
            double totalDelayedFraction)
        {
            DataId = dataId;
            DataVersion = dataVersion;
            DataDigest = dataDigest;
            PromptGenerationTimeSeconds = promptGenerationTimeSeconds;
            Groups = new ReadOnlyCollection<DelayedNeutronGroupV1>(groups.ToArray());
            TotalDelayedFraction = totalDelayedFraction;
        }

        public StableId DataId { get; }

        public string DataVersion { get; }

        public Digest32 DataDigest { get; }

        public double PromptGenerationTimeSeconds { get; }

        public IReadOnlyList<DelayedNeutronGroupV1> Groups { get; }

        public double TotalDelayedFraction { get; }

        public static ContractValidationResult<DelayedNeutronDataV1> TryCreate(
            StableId dataId,
            string dataVersion,
            Digest32 dataDigest,
            double promptGenerationTimeSeconds,
            IEnumerable<DelayedNeutronGroupV1> groups)
        {
            if (dataId.IsEmpty)
            {
                return Invalid(
                    "DelayedNeutronData.Id.Empty",
                    "data_id",
                    "Delayed-neutron data requires an explicit nonempty identity.");
            }

            if (string.IsNullOrWhiteSpace(dataVersion))
            {
                return Invalid(
                    "DelayedNeutronData.Version.Empty",
                    "data_version",
                    "Delayed-neutron data requires an explicit version.");
            }

            if (dataDigest == null)
            {
                return Invalid(
                    "DelayedNeutronData.Digest.Missing",
                    "data_digest",
                    "Delayed-neutron data requires an explicit 32-byte digest.");
            }

            if (!KineticContractValidation.IsPositiveFinite(promptGenerationTimeSeconds))
            {
                return Invalid(
                    "DelayedNeutronData.PromptGenerationTime.Invalid",
                    "prompt_generation_time_s",
                    "Prompt generation time must be finite and strictly positive SI seconds.");
            }

            if (groups == null)
            {
                return Invalid(
                    "DelayedNeutronData.Groups.Missing",
                    "groups",
                    "Delayed-neutron data requires an explicit ordered group collection.");
            }

            DelayedNeutronGroupV1[] orderedGroups = groups.ToArray();
            if (orderedGroups.Length == 0)
            {
                return Invalid(
                    "DelayedNeutronData.Groups.Empty",
                    "groups",
                    "Delayed-neutron data requires at least one precursor group.");
            }

            double totalDelayedFraction = 0.0;
            for (int index = 0; index < orderedGroups.Length; index++)
            {
                DelayedNeutronGroupV1 group = orderedGroups[index];
                if (group == null)
                {
                    return Invalid(
                        "DelayedNeutronData.Groups.Null",
                        "groups[" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]",
                        "A delayed-neutron group record may not be null.");
                }

                if (group.GroupIndex != index)
                {
                    return Invalid(
                        "DelayedNeutronData.Groups.Order.Invalid",
                        "groups[" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + "].group_index",
                        "Delayed-neutron groups must use the explicit serialized order 0..G-1.");
                }

                totalDelayedFraction += group.BetaFraction;
                if (!KineticContractValidation.IsCanonicalNonnegativeFinite(totalDelayedFraction))
                {
                    return Invalid(
                        "DelayedNeutronData.BetaFractionSum.NonFinite",
                        "groups.beta_fraction_sum",
                        "The ordered delayed-neutron fraction sum must remain finite.");
                }
            }

            if (totalDelayedFraction >= 1.0)
            {
                return Invalid(
                    "DelayedNeutronData.BetaFractionSum.Invalid",
                    "groups.beta_fraction_sum",
                    "The ordered delayed-neutron fraction sum must be strictly less than one.");
            }

            return ContractValidationResult<DelayedNeutronDataV1>.Valid(
                new DelayedNeutronDataV1(
                    dataId,
                    dataVersion,
                    dataDigest,
                    promptGenerationTimeSeconds,
                    orderedGroups,
                    totalDelayedFraction));
        }

        private static ContractValidationResult<DelayedNeutronDataV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<DelayedNeutronDataV1>.Invalid(code, path, message);
        }
    }

    /// <summary>
    /// Immutable global point-kinetics state. A state may be created before a
    /// spatial solve is bound, but positive-duration integration requires all
    /// three spatial binding fields to be applicable.
    /// </summary>
    public sealed class KineticStateV1
    {
        public const uint CurrentSchemaVersion = 1;

        private readonly ReadOnlyCollection<double> _precursor;
        private readonly ReadOnlyCollection<double> _initialPrecursor;

        private KineticStateV1(
            double simulationTimeSeconds,
            double amplitude,
            IEnumerable<double> precursor,
            double initialAmplitude,
            IEnumerable<double> initialPrecursor,
            double referencePowerWatts,
            DelayedNeutronDataV1 data,
            OptionalStableId spatialSolveId,
            OptionalUInt64 spatialStateVersion,
            double spatialReactivity,
            OptionalDigest32 feedbackOverlayDigest,
            ulong kineticStepIndex)
        {
            SimulationTimeSeconds = simulationTimeSeconds;
            Amplitude = amplitude;
            _precursor = new ReadOnlyCollection<double>(precursor.ToArray());
            InitialAmplitude = initialAmplitude;
            _initialPrecursor = new ReadOnlyCollection<double>(initialPrecursor.ToArray());
            ReferencePowerWatts = referencePowerWatts;
            Data = data;
            DelayedNeutronDataId = data.DataId;
            DelayedNeutronDataVersion = data.DataVersion;
            DelayedNeutronDataDigest = data.DataDigest;
            SpatialSolveId = spatialSolveId;
            SpatialStateVersion = spatialStateVersion;
            SpatialReactivity = spatialReactivity;
            FeedbackOverlayDigest = feedbackOverlayDigest;
            KineticStepIndex = kineticStepIndex;
        }

        public double SimulationTimeSeconds { get; }

        public double Amplitude { get; }

        public IReadOnlyList<double> Precursor
        {
            get { return _precursor; }
        }

        public double InitialAmplitude { get; }

        public IReadOnlyList<double> InitialPrecursor
        {
            get { return _initialPrecursor; }
        }

        public double ReferencePowerWatts { get; }

        public DelayedNeutronDataV1 Data { get; }

        public StableId DelayedNeutronDataId { get; }

        public string DelayedNeutronDataVersion { get; }

        public Digest32 DelayedNeutronDataDigest { get; }

        public OptionalStableId SpatialSolveId { get; }

        public OptionalUInt64 SpatialStateVersion { get; }

        public double SpatialReactivity { get; }

        public OptionalDigest32 FeedbackOverlayDigest { get; }

        public ulong KineticStepIndex { get; }

        public bool HasSpatialBinding
        {
            get
            {
                return SpatialSolveId.IsApplicable &&
                       SpatialStateVersion.IsApplicable &&
                       FeedbackOverlayDigest.IsApplicable;
            }
        }

        public static ContractValidationResult<KineticStateV1> TryCreate(
            double simulationTimeSeconds,
            double amplitude,
            IEnumerable<double> precursor,
            double initialAmplitude,
            IEnumerable<double> initialPrecursor,
            double referencePowerWatts,
            DelayedNeutronDataV1 data,
            OptionalStableId spatialSolveId,
            OptionalUInt64 spatialStateVersion,
            double spatialReactivity,
            OptionalDigest32 feedbackOverlayDigest,
            ulong kineticStepIndex)
        {
            if (data == null)
            {
                return Invalid(
                    "KineticState.Data.Missing",
                    "data",
                    "A kinetic state requires validated delayed-neutron data.");
            }

            if (spatialSolveId == null || spatialStateVersion == null || feedbackOverlayDigest == null)
            {
                return Invalid(
                    "KineticState.SpatialBinding.Missing",
                    "spatial_binding",
                    "A kinetic state requires explicit applicability wrappers for spatial binding.");
            }

            if (!KineticContractValidation.IsCanonicalNonnegativeFinite(simulationTimeSeconds))
            {
                return Invalid(
                    "KineticState.SimulationTime.Invalid",
                    "simulation_time_s",
                    "Simulation time must be finite, canonical, and nonnegative SI seconds.");
            }

            if (!KineticContractValidation.IsCanonicalNonnegativeFinite(amplitude))
            {
                return Invalid(
                    "KineticState.Amplitude.Invalid",
                    "amplitude",
                    "Amplitude must be finite, canonical, and nonnegative.");
            }

            if (!KineticContractValidation.IsCanonicalNonnegativeFinite(initialAmplitude))
            {
                return Invalid(
                    "KineticState.InitialAmplitude.Invalid",
                    "initial_amplitude",
                    "Initial amplitude must be finite, canonical, and nonnegative.");
            }

            if (!KineticContractValidation.IsPositiveFinite(referencePowerWatts))
            {
                return Invalid(
                    "KineticState.ReferencePower.Invalid",
                    "reference_power_w",
                    "Reference power must be finite and strictly positive SI watts.");
            }

            if (!KineticContractValidation.IsCanonicalFinite(spatialReactivity))
            {
                return Invalid(
                    "KineticState.SpatialReactivity.Invalid",
                    "spatial_reactivity",
                    "Spatial reactivity must be finite and use canonical binary64 zero/sign encoding.");
            }

            if (precursor == null || initialPrecursor == null)
            {
                return Invalid(
                    "KineticState.Precursor.Missing",
                    "precursor",
                    "Current and initial precursor vectors are both required.");
            }

            double[] currentPrecursors = precursor.ToArray();
            double[] initialPrecursors = initialPrecursor.ToArray();
            if (currentPrecursors.Length != data.Groups.Count ||
                initialPrecursors.Length != data.Groups.Count)
            {
                return Invalid(
                    "KineticState.Precursor.CountMismatch",
                    "precursor",
                    "Current and initial precursor vectors must match the delayed-neutron group count.");
            }

            if (currentPrecursors.Any(value =>
                    !KineticContractValidation.IsCanonicalNonnegativeFinite(value)) ||
                initialPrecursors.Any(value =>
                    !KineticContractValidation.IsCanonicalNonnegativeFinite(value)))
            {
                return Invalid(
                    "KineticState.Precursor.Invalid",
                    "precursor",
                    "All precursor values must be finite, canonical, and nonnegative.");
            }

            bool anyBinding = spatialSolveId.IsApplicable ||
                              spatialStateVersion.IsApplicable ||
                              feedbackOverlayDigest.IsApplicable;
            bool completeBinding = spatialSolveId.IsApplicable &&
                                   spatialStateVersion.IsApplicable &&
                                   feedbackOverlayDigest.IsApplicable;
            if (anyBinding != completeBinding)
            {
                return Invalid(
                    "KineticState.SpatialBinding.Incomplete",
                    "spatial_binding",
                    "Spatial solve identity, state version, and overlay digest must be all applicable or all NotApplicable.");
            }

            return ContractValidationResult<KineticStateV1>.Valid(
                new KineticStateV1(
                    simulationTimeSeconds,
                    amplitude,
                    currentPrecursors,
                    initialAmplitude,
                    initialPrecursors,
                    referencePowerWatts,
                    data,
                    spatialSolveId,
                    spatialStateVersion,
                    spatialReactivity,
                    feedbackOverlayDigest,
                    kineticStepIndex));
        }

        public ContractValidationResult<KineticStateV1> TryBindSpatialSolve(
            StableId spatialSolveId,
            ulong spatialStateVersion,
            double spatialReactivity,
            Digest32 feedbackOverlayDigest)
        {
            if (spatialSolveId.IsEmpty || feedbackOverlayDigest == null ||
                !KineticContractValidation.IsCanonicalFinite(spatialReactivity))
            {
                return Invalid(
                    "KineticState.SpatialBinding.Invalid",
                    "spatial_binding",
                    "A spatial binding requires a nonempty solve identity, finite reactivity, and overlay digest.");
            }

            return TryCreate(
                SimulationTimeSeconds,
                Amplitude,
                Precursor,
                InitialAmplitude,
                InitialPrecursor,
                ReferencePowerWatts,
                Data,
                OptionalStableId.Applicable(spatialSolveId),
                OptionalUInt64.Applicable(spatialStateVersion),
                spatialReactivity,
                OptionalDigest32.Applicable(feedbackOverlayDigest),
                KineticStepIndex);
        }

        private static ContractValidationResult<KineticStateV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<KineticStateV1>.Invalid(code, path, message);
        }

    }

    /// <summary>
    /// Deterministic audit record for one explicit point-kinetics Euler step.
    /// The record is immutable and carries the exact spatial/data binding used.
    /// </summary>
    public sealed class KineticStepRecordV1
    {
        private readonly ReadOnlyCollection<double> _precursorBefore;
        private readonly ReadOnlyCollection<double> _precursorAfter;
        private readonly ReadOnlyCollection<double> _precursorDerivative;

        internal KineticStepRecordV1(
            StableId dataId,
            string dataVersion,
            Digest32 dataDigest,
            double simulationTimeSeconds,
            double simulationTimeAfterSeconds,
            double deltaTimeSeconds,
            double referencePowerWatts,
            double spatialReactivity,
            double promptDerivative,
            double delayedSource,
            double amplitudeDerivative,
            double amplitudeBefore,
            double amplitudeAfter,
            IEnumerable<double> precursorBefore,
            IEnumerable<double> precursorAfter,
            IEnumerable<double> precursorDerivative,
            OptionalStableId spatialSolveId,
            OptionalUInt64 spatialStateVersion,
            OptionalDigest32 feedbackOverlayDigest,
            ulong kineticStepIndexBefore,
            ulong kineticStepIndexAfter)
        {
            DataId = dataId;
            DataVersion = dataVersion;
            DataDigest = dataDigest;
            SimulationTimeSeconds = simulationTimeSeconds;
            SimulationTimeAfterSeconds = simulationTimeAfterSeconds;
            DeltaTimeSeconds = deltaTimeSeconds;
            ReferencePowerWatts = referencePowerWatts;
            SpatialReactivity = spatialReactivity;
            PromptDerivative = promptDerivative;
            DelayedSource = delayedSource;
            AmplitudeDerivative = amplitudeDerivative;
            AmplitudeBefore = amplitudeBefore;
            AmplitudeAfter = amplitudeAfter;
            _precursorBefore = new ReadOnlyCollection<double>(precursorBefore.ToArray());
            _precursorAfter = new ReadOnlyCollection<double>(precursorAfter.ToArray());
            _precursorDerivative = new ReadOnlyCollection<double>(precursorDerivative.ToArray());
            SpatialSolveId = spatialSolveId;
            SpatialStateVersion = spatialStateVersion;
            FeedbackOverlayDigest = feedbackOverlayDigest;
            KineticStepIndexBefore = kineticStepIndexBefore;
            KineticStepIndexAfter = kineticStepIndexAfter;
        }

        public StableId DataId { get; }

        public string DataVersion { get; }

        public Digest32 DataDigest { get; }

        public double SimulationTimeSeconds { get; }

        public double SimulationTimeAfterSeconds { get; }

        public double DeltaTimeSeconds { get; }

        public double ReferencePowerWatts { get; }

        public double SpatialReactivity { get; }

        public double PromptDerivative { get; }

        public double DelayedSource { get; }

        public double AmplitudeDerivative { get; }

        public double AmplitudeBefore { get; }

        public double AmplitudeAfter { get; }

        public IReadOnlyList<double> PrecursorBefore
        {
            get { return _precursorBefore; }
        }

        public IReadOnlyList<double> PrecursorAfter
        {
            get { return _precursorAfter; }
        }

        public IReadOnlyList<double> PrecursorDerivative
        {
            get { return _precursorDerivative; }
        }

        public OptionalStableId SpatialSolveId { get; }

        public OptionalUInt64 SpatialStateVersion { get; }

        public OptionalDigest32 FeedbackOverlayDigest { get; }

        public ulong KineticStepIndexBefore { get; }

        public ulong KineticStepIndexAfter { get; }
    }

    public sealed class KineticIntegrationResultV1
    {
        internal KineticIntegrationResultV1(
            KineticStateV1 resultingState,
            KineticStepRecordV1 step)
        {
            ResultingState = resultingState;
            Step = step;
        }

        public KineticStateV1 ResultingState { get; }

        public KineticStepRecordV1 Step { get; }
    }

    /// <summary>
    /// Applies the P2-T04 explicit left-endpoint Euler point-kinetics update.
    /// This transition owns amplitude and precursor state only; it does not
    /// solve spatial coefficients or introduce a second reactivity term.
    /// </summary>
    public static class KineticIntegrationTransitionV1
    {
        public static ContractValidationResult<KineticIntegrationResultV1> TryApply(
            KineticStateV1 current,
            DelayedNeutronDataV1 data,
            double deltaTimeSeconds)
        {
            if (current == null || data == null)
            {
                return Invalid(
                    "KineticIntegration.StateOrData.Missing",
                    "integration",
                    "Integration requires a current kinetic state and validated delayed-neutron data.");
            }

            if (current.DelayedNeutronDataId != data.DataId ||
                !string.Equals(current.DelayedNeutronDataVersion, data.DataVersion, StringComparison.Ordinal) ||
                !current.DelayedNeutronDataDigest.Equals(data.DataDigest))
            {
                return Invalid(
                    "KineticIntegration.Data.Stale",
                    "data",
                    "Integration data must match the exact identity and digest bound to the current kinetic state.");
            }

            if (!current.HasSpatialBinding)
            {
                return Invalid(
                    "KineticIntegration.SpatialBinding.Missing",
                    "spatial_binding",
                    "Positive-duration kinetics requires an exact accepted spatial solve binding.");
            }

            if (!KineticContractValidation.IsPositiveFinite(deltaTimeSeconds))
            {
                return Invalid(
                    "KineticIntegration.Interval.Invalid",
                    "delta_time_s",
                    "The explicit Euler interval must be finite and strictly positive SI seconds.");
            }

            if (current.KineticStepIndex == ulong.MaxValue)
            {
                return Invalid(
                    "KineticIntegration.StepIndex.Overflow",
                    "kinetic_step_index",
                    "The kinetic step index cannot advance beyond UInt64.MaxValue.");
            }

            double nextTime = current.SimulationTimeSeconds + deltaTimeSeconds;
            if (!KineticContractValidation.IsCanonicalNonnegativeFinite(nextTime) ||
                nextTime <= current.SimulationTimeSeconds)
            {
                return Invalid(
                    "KineticIntegration.Time.Invalid",
                    "simulation_time_s",
                    "The explicit Euler interval must advance representable simulation time.");
            }

            double delayedSource = 0.0;
            for (int index = 0; index < data.Groups.Count; index++)
            {
                DelayedNeutronGroupV1 group = data.Groups[index];
                double delayedTerm = group.DecayConstantPerSecond * current.Precursor[index];
                if (!KineticContractValidation.IsCanonicalFinite(delayedTerm))
                {
                    return Invalid(
                        "KineticIntegration.DelayedSource.Invalid",
                        "precursor[" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]",
                        "The delayed-neutron source term must remain finite.");
                }

                delayedSource += delayedTerm;
                if (!KineticContractValidation.IsCanonicalFinite(delayedSource))
                {
                    return Invalid(
                        "KineticIntegration.DelayedSource.Invalid",
                        "delayed_source",
                        "The ordered delayed-neutron source sum must remain finite.");
                }
            }

            double promptCoefficient =
                (current.SpatialReactivity - data.TotalDelayedFraction) /
                data.PromptGenerationTimeSeconds;
            if (!KineticContractValidation.IsCanonicalFinite(promptCoefficient))
            {
                return Invalid(
                    "KineticIntegration.PromptCoefficient.Invalid",
                    "prompt_coefficient_s_inv",
                    "The prompt kinetics coefficient must remain finite.");
            }

            double promptDerivative = promptCoefficient * current.Amplitude;
            if (!KineticContractValidation.IsCanonicalFinite(promptDerivative))
            {
                return Invalid(
                    "KineticIntegration.PromptDerivative.Invalid",
                    "prompt_derivative_s_inv",
                    "The prompt amplitude derivative must remain finite.");
            }

            double amplitudeDerivative = promptDerivative + delayedSource;
            if (!KineticContractValidation.IsCanonicalFinite(amplitudeDerivative))
            {
                return Invalid(
                    "KineticIntegration.AmplitudeDerivative.Invalid",
                    "amplitude_derivative_s_inv",
                    "The amplitude derivative must remain finite.");
            }

            double amplitudeDelta = deltaTimeSeconds * amplitudeDerivative;
            double nextAmplitude = current.Amplitude + amplitudeDelta;
            if (!KineticContractValidation.IsCanonicalFinite(amplitudeDelta))
            {
                return Invalid(
                    "KineticIntegration.AmplitudeDelta.Invalid",
                    "amplitude_delta",
                    "The explicit amplitude increment must remain finite.");
            }

            if (!KineticContractValidation.IsCanonicalNonnegativeFinite(nextAmplitude))
            {
                return Invalid(
                    "KineticIntegration.AmplitudeResult.Invalid",
                    "amplitude_next",
                    "The explicit Euler amplitude result must remain finite and nonnegative without clamping.");
            }

            var nextPrecursors = new double[data.Groups.Count];
            var precursorDerivatives = new double[data.Groups.Count];
            for (int index = 0; index < data.Groups.Count; index++)
            {
                DelayedNeutronGroupV1 group = data.Groups[index];
                double productionCoefficient =
                    group.BetaFraction / data.PromptGenerationTimeSeconds;
                double production = productionCoefficient * current.Amplitude;
                double decay = group.DecayConstantPerSecond * current.Precursor[index];
                double derivative = production - decay;
                double delta = deltaTimeSeconds * derivative;
                double next = current.Precursor[index] + delta;
                if (!KineticContractValidation.IsCanonicalFinite(productionCoefficient) ||
                    !KineticContractValidation.IsCanonicalFinite(production) ||
                    !KineticContractValidation.IsCanonicalFinite(decay) ||
                    !KineticContractValidation.IsCanonicalFinite(derivative) ||
                    !KineticContractValidation.IsCanonicalFinite(delta) ||
                    !KineticContractValidation.IsCanonicalNonnegativeFinite(next))
                {
                    return Invalid(
                        "KineticIntegration.PrecursorResult.Invalid",
                        "precursor[" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]",
                        "The explicit Euler precursor result must remain finite and nonnegative without clamping.");
                }

                precursorDerivatives[index] = derivative;
                nextPrecursors[index] = next;
            }

            ContractValidationResult<KineticStateV1> nextState = KineticStateV1.TryCreate(
                nextTime,
                nextAmplitude,
                nextPrecursors,
                current.InitialAmplitude,
                current.InitialPrecursor,
                current.ReferencePowerWatts,
                data,
                current.SpatialSolveId,
                current.SpatialStateVersion,
                current.SpatialReactivity,
                current.FeedbackOverlayDigest,
                current.KineticStepIndex + 1);
            if (!nextState.IsValid)
            {
                return Invalid(
                    nextState.FirstDiagnostic.Code,
                    nextState.FirstDiagnostic.Path,
                    nextState.FirstDiagnostic.Message);
            }

            var record = new KineticStepRecordV1(
                data.DataId,
                data.DataVersion,
                data.DataDigest,
                current.SimulationTimeSeconds,
                nextTime,
                deltaTimeSeconds,
                current.ReferencePowerWatts,
                current.SpatialReactivity,
                promptDerivative,
                delayedSource,
                amplitudeDerivative,
                current.Amplitude,
                nextAmplitude,
                current.Precursor,
                nextPrecursors,
                precursorDerivatives,
                current.SpatialSolveId,
                current.SpatialStateVersion,
                current.FeedbackOverlayDigest,
                current.KineticStepIndex,
                current.KineticStepIndex + 1);

            return ContractValidationResult<KineticIntegrationResultV1>.Valid(
                new KineticIntegrationResultV1(nextState.Value, record));
        }

        private static ContractValidationResult<KineticIntegrationResultV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<KineticIntegrationResultV1>.Invalid(code, path, message);
        }
    }

    internal static class KineticContractValidation
    {
        internal static bool IsPositiveFinite(double value)
        {
            return IsCanonicalFinite(value) && value > 0.0;
        }

        internal static bool IsCanonicalNonnegativeFinite(double value)
        {
            return IsCanonicalFinite(value) && value >= 0.0;
        }

        internal static bool IsCanonicalFinite(double value)
        {
            return ContractValidation.IsFinite(value) &&
                   BitConverter.DoubleToInt64Bits(value) != long.MinValue;
        }
    }
}
