using System;
using System.Collections;
using System.Collections.Generic;

namespace ReactorSim.Core
{
    /// <summary>
    /// An immutable Phase 7 explicit-Euler stability policy. Every value is
    /// supplied by the caller; this type does not select a production step,
    /// rate, tolerance, or nuclear constant.
    /// </summary>
    public sealed class IntegrationStabilityPolicyV1
    {
        public const uint CurrentSchemaVersion = 1;

        private readonly uint _schemaVersion;

        private IntegrationStabilityPolicyV1(
            string policyVersion,
            Digest32 digest,
            double maximumKineticStep,
            double maximumNuclideStep,
            double maximumRrsStep,
            double maximumDecayRate,
            double maximumPromptRate,
            double maximumDimensionlessStep,
            double allowedDurationSeconds)
        {
            _schemaVersion = CurrentSchemaVersion;
            PolicyVersion = policyVersion;
            Digest = digest;
            MaximumKineticStep = maximumKineticStep;
            MaximumNuclideStep = maximumNuclideStep;
            MaximumRRSStep = maximumRrsStep;
            MaximumDecayRate = maximumDecayRate;
            MaximumPromptRate = maximumPromptRate;
            MaximumDimensionlessStep = maximumDimensionlessStep;
            AllowedDurationSeconds = allowedDurationSeconds;
        }

        public uint SchemaVersion
        {
            get { return _schemaVersion; }
        }

        public string PolicyVersion { get; }

        public Digest32 Digest { get; }

        public double MaximumKineticStep { get; }

        public double MaximumNuclideStep { get; }

        public double MaximumRRSStep { get; }

        public double MaximumDecayRate { get; }

        public double MaximumPromptRate { get; }

        public double MaximumDimensionlessStep { get; }

        /// <summary>
        /// The authoritative allowed explicit substep duration H, in SI
        /// seconds, computed from the five approved policy terms.
        /// </summary>
        public double AllowedDurationSeconds { get; }

        public static ContractValidationResult<IntegrationStabilityPolicyV1> TryCreate(
            string policyVersion,
            Digest32 digest,
            double maximumKineticStep,
            double maximumNuclideStep,
            double maximumRrsStep,
            double maximumDecayRate,
            double maximumPromptRate,
            double maximumDimensionlessStep)
        {
            if (string.IsNullOrWhiteSpace(policyVersion))
            {
                return Invalid(
                    "IntegrationStabilityPolicy.Version.Empty",
                    "policy_version",
                    "The stability policy requires an explicit version identity.");
            }

            if (digest == null)
            {
                return Invalid(
                    "IntegrationStabilityPolicy.Digest.Missing",
                    "digest",
                    "The stability policy requires an explicit 32-byte digest identity.");
            }

            if (!KineticContractValidation.IsPositiveFinite(maximumKineticStep))
            {
                return Invalid(
                    "IntegrationStabilityPolicy.MaximumKineticStep.Invalid",
                    "maximum_kinetic_step_s",
                    "The maximum kinetic step must be finite, canonical, and strictly positive SI seconds.");
            }

            if (!KineticContractValidation.IsPositiveFinite(maximumNuclideStep))
            {
                return Invalid(
                    "IntegrationStabilityPolicy.MaximumNuclideStep.Invalid",
                    "maximum_nuclide_step_s",
                    "The maximum nuclide step must be finite, canonical, and strictly positive SI seconds.");
            }

            if (!KineticContractValidation.IsPositiveFinite(maximumRrsStep))
            {
                return Invalid(
                    "IntegrationStabilityPolicy.MaximumRRSStep.Invalid",
                    "maximum_rrs_step_s",
                    "The maximum RRS step must be finite, canonical, and strictly positive SI seconds.");
            }

            if (!KineticContractValidation.IsPositiveFinite(maximumDecayRate))
            {
                return Invalid(
                    "IntegrationStabilityPolicy.MaximumDecayRate.Invalid",
                    "maximum_decay_rate_s_inv",
                    "The maximum decay rate must be finite, canonical, and strictly positive SI s^-1.");
            }

            if (!KineticContractValidation.IsPositiveFinite(maximumPromptRate))
            {
                return Invalid(
                    "IntegrationStabilityPolicy.MaximumPromptRate.Invalid",
                    "maximum_prompt_rate_s_inv",
                    "The maximum prompt rate must be finite, canonical, and strictly positive SI s^-1.");
            }

            if (!KineticContractValidation.IsPositiveFinite(maximumDimensionlessStep))
            {
                return Invalid(
                    "IntegrationStabilityPolicy.MaximumDimensionlessStep.Invalid",
                    "maximum_dimensionless_step",
                    "The maximum dimensionless step must be finite, canonical, and strictly positive.");
            }

            double decayDuration = maximumDimensionlessStep / maximumDecayRate;
            if (!KineticContractValidation.IsPositiveFinite(decayDuration))
            {
                return Invalid(
                    "IntegrationStabilityPolicy.DecayBound.Invalid",
                    "maximum_dimensionless_step_over_decay_rate_s",
                    "The dimensionless decay bound must be representable as finite positive SI seconds.");
            }

            double promptDuration = maximumDimensionlessStep / maximumPromptRate;
            if (!KineticContractValidation.IsPositiveFinite(promptDuration))
            {
                return Invalid(
                    "IntegrationStabilityPolicy.PromptBound.Invalid",
                    "maximum_dimensionless_step_over_prompt_rate_s",
                    "The dimensionless prompt bound must be representable as finite positive SI seconds.");
            }

            double allowedDuration = Math.Min(
                Math.Min(maximumKineticStep, maximumNuclideStep),
                Math.Min(
                    maximumRrsStep,
                    Math.Min(decayDuration, promptDuration)));
            if (!KineticContractValidation.IsPositiveFinite(allowedDuration))
            {
                return Invalid(
                    "IntegrationStabilityPolicy.AllowedDuration.Invalid",
                    "allowed_duration_s",
                    "The authoritative minimum of all five stability terms must be finite and strictly positive SI seconds.");
            }

            return ContractValidationResult<IntegrationStabilityPolicyV1>.Valid(
                new IntegrationStabilityPolicyV1(
                    policyVersion,
                    digest,
                    maximumKineticStep,
                    maximumNuclideStep,
                    maximumRrsStep,
                    maximumDecayRate,
                    maximumPromptRate,
                    maximumDimensionlessStep,
                    allowedDuration));
        }

        /// <summary>
        /// Validates one explicit substep against the three direct bounds,
        /// both dimensionless envelope inequalities, and the active-rate
        /// coverage supplied by the caller.
        /// </summary>
        public ContractValidationResult<bool> TryValidateSubstep(
            double deltaTimeSeconds,
            double activeMaximumDecayRate,
            double activeMaximumPromptRate)
        {
            if (!KineticContractValidation.IsPositiveFinite(deltaTimeSeconds))
            {
                return InvalidStep(
                    "IntegrationStability.Substep.Invalid",
                    "delta_time_s",
                    "An explicit substep duration must be finite, canonical, and strictly positive SI seconds.");
            }

            ContractValidationResult<bool> rateCoverage = ValidateRateCoverage(
                activeMaximumDecayRate,
                activeMaximumPromptRate);
            if (!rateCoverage.IsValid)
            {
                return rateCoverage;
            }

            if (deltaTimeSeconds > AllowedDurationSeconds)
            {
                return InvalidStep(
                    "IntegrationStability.Substep.AllowedBoundExceeded",
                    "delta_time_s",
                    "The explicit substep exceeds the authoritative five-term minimum H.");
            }

            if (deltaTimeSeconds > MaximumKineticStep)
            {
                return InvalidStep(
                    "IntegrationStability.Substep.KineticBoundExceeded",
                    "delta_time_s",
                    "The explicit substep exceeds MaximumKineticStep.");
            }

            if (deltaTimeSeconds > MaximumNuclideStep)
            {
                return InvalidStep(
                    "IntegrationStability.Substep.NuclideBoundExceeded",
                    "delta_time_s",
                    "The explicit substep exceeds MaximumNuclideStep.");
            }

            if (deltaTimeSeconds > MaximumRRSStep)
            {
                return InvalidStep(
                    "IntegrationStability.Substep.RRSBoundExceeded",
                    "delta_time_s",
                    "The explicit substep exceeds MaximumRRSStep.");
            }

            double decayProduct = deltaTimeSeconds * MaximumDecayRate;
            if (!KineticContractValidation.IsCanonicalFinite(decayProduct) ||
                decayProduct > MaximumDimensionlessStep)
            {
                return InvalidStep(
                    "IntegrationStability.Substep.DecayEnvelopeExceeded",
                    "delta_time_s",
                    "The explicit substep violates the declared decay-rate dimensionless envelope.");
            }

            double promptProduct = deltaTimeSeconds * MaximumPromptRate;
            if (!KineticContractValidation.IsCanonicalFinite(promptProduct) ||
                promptProduct > MaximumDimensionlessStep)
            {
                return InvalidStep(
                    "IntegrationStability.Substep.PromptEnvelopeExceeded",
                    "delta_time_s",
                    "The explicit substep violates the declared prompt-rate dimensionless envelope.");
            }

            return ContractValidationResult<bool>.Valid(true);
        }

        /// <summary>
        /// Constructs the exact deterministic ceil/remainder schedule for a
        /// positive scheduled gap. The active rates are required so a gap
        /// cannot be accepted when the declared policy does not cover them.
        /// </summary>
        public ContractValidationResult<IntegrationSubstepScheduleV1> TryPartition(
            double deltaTimeGapSeconds,
            double activeMaximumDecayRate,
            double activeMaximumPromptRate)
        {
            ContractValidationResult<bool> rateCoverage = ValidateRateCoverage(
                activeMaximumDecayRate,
                activeMaximumPromptRate);
            if (!rateCoverage.IsValid)
            {
                return InvalidSchedule(
                    rateCoverage.FirstDiagnostic.Code,
                    rateCoverage.FirstDiagnostic.Path,
                    rateCoverage.FirstDiagnostic.Message);
            }

            if (!KineticContractValidation.IsPositiveFinite(deltaTimeGapSeconds))
            {
                return InvalidSchedule(
                    "IntegrationStability.Partition.Gap.Invalid",
                    "delta_time_gap_s",
                    "A scheduled gap must be finite, canonical, and strictly positive SI seconds.");
            }

            double quotient = deltaTimeGapSeconds / AllowedDurationSeconds;
            if (!KineticContractValidation.IsCanonicalFinite(quotient) || quotient <= 0.0)
            {
                return InvalidSchedule(
                    "IntegrationStability.Partition.Count.Invalid",
                    "substep_count",
                    "The ceil(gap/H) subdivision count must be representable as a finite positive double.");
            }

            double countAsDouble = Math.Ceiling(quotient);
            if (!KineticContractValidation.IsCanonicalFinite(countAsDouble) ||
                countAsDouble < 1.0 ||
                countAsDouble > int.MaxValue)
            {
                return InvalidSchedule(
                    "IntegrationStability.Partition.Count.Invalid",
                    "substep_count",
                    "The deterministic subdivision count must be representable as a positive Int32.");
            }

            int count = (int)countAsDouble;
            double completedDuration = (count - 1.0) * AllowedDurationSeconds;
            if (!KineticContractValidation.IsCanonicalFinite(completedDuration))
            {
                return InvalidSchedule(
                    "IntegrationStability.Partition.CompletedDuration.Invalid",
                    "completed_duration_s",
                    "The completed deterministic substep duration is not representable as finite canonical SI seconds.");
            }

            double finalDuration = deltaTimeGapSeconds - completedDuration;
            if (!KineticContractValidation.IsPositiveFinite(finalDuration) ||
                finalDuration > AllowedDurationSeconds)
            {
                return InvalidSchedule(
                    "IntegrationStability.Partition.Remainder.Invalid",
                    "final_substep_s",
                    "The deterministic remainder must be finite, canonical, positive, and no larger than H.");
            }

            if (count > 1)
            {
                ContractValidationResult<bool> allowedStep = TryValidateSubstep(
                    AllowedDurationSeconds,
                    activeMaximumDecayRate,
                    activeMaximumPromptRate);
                if (!allowedStep.IsValid)
                {
                    return InvalidSchedule(
                        allowedStep.FirstDiagnostic.Code,
                        allowedStep.FirstDiagnostic.Path,
                        allowedStep.FirstDiagnostic.Message);
                }
            }

            ContractValidationResult<bool> finalStep = TryValidateSubstep(
                finalDuration,
                activeMaximumDecayRate,
                activeMaximumPromptRate);
            if (!finalStep.IsValid)
            {
                return InvalidSchedule(
                    finalStep.FirstDiagnostic.Code,
                    finalStep.FirstDiagnostic.Path,
                    finalStep.FirstDiagnostic.Message);
            }

            return ContractValidationResult<IntegrationSubstepScheduleV1>.Valid(
                new IntegrationSubstepScheduleV1(
                    count,
                    AllowedDurationSeconds,
                    finalDuration));
        }

        private ContractValidationResult<bool> ValidateRateCoverage(
            double activeMaximumDecayRate,
            double activeMaximumPromptRate)
        {
            if (!KineticContractValidation.IsCanonicalNonnegativeFinite(activeMaximumDecayRate))
            {
                return InvalidStep(
                    "IntegrationStability.DecayRate.Invalid",
                    "active_maximum_decay_rate_s_inv",
                    "The active maximum decay rate must be finite, canonical, and nonnegative SI s^-1.");
            }

            if (activeMaximumDecayRate > MaximumDecayRate)
            {
                return InvalidStep(
                    "IntegrationStability.DecayRate.Uncovered",
                    "active_maximum_decay_rate_s_inv",
                    "The active maximum decay rate exceeds the policy envelope.");
            }

            if (!KineticContractValidation.IsCanonicalNonnegativeFinite(activeMaximumPromptRate))
            {
                return InvalidStep(
                    "IntegrationStability.PromptRate.Invalid",
                    "active_maximum_prompt_rate_s_inv",
                    "The active maximum prompt rate must be finite, canonical, and nonnegative SI s^-1.");
            }

            if (activeMaximumPromptRate > MaximumPromptRate)
            {
                return InvalidStep(
                    "IntegrationStability.PromptRate.Uncovered",
                    "active_maximum_prompt_rate_s_inv",
                    "The active maximum prompt rate exceeds the policy envelope.");
            }

            return ContractValidationResult<bool>.Valid(true);
        }

        private static ContractValidationResult<IntegrationStabilityPolicyV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<IntegrationStabilityPolicyV1>.Invalid(code, path, message);
        }

        private static ContractValidationResult<bool> InvalidStep(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<bool>.Invalid(code, path, message);
        }

        private static ContractValidationResult<IntegrationSubstepScheduleV1> InvalidSchedule(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<IntegrationSubstepScheduleV1>.Invalid(code, path, message);
        }
    }

    /// <summary>
    /// A read-only logical sequence of explicit substep durations. The first
    /// Count-1 entries are H and the final entry is the deterministic gap
    /// remainder; durations are not inferred from wall-clock or frame timing.
    /// </summary>
    public sealed class IntegrationSubstepScheduleV1 : IReadOnlyList<double>
    {
        private readonly int _count;
        private readonly double _allowedDurationSeconds;
        private readonly double _finalDurationSeconds;

        internal IntegrationSubstepScheduleV1(
            int count,
            double allowedDurationSeconds,
            double finalDurationSeconds)
        {
            _count = count;
            _allowedDurationSeconds = allowedDurationSeconds;
            _finalDurationSeconds = finalDurationSeconds;
        }

        public int Count
        {
            get { return _count; }
        }

        public double this[int index]
        {
            get
            {
                if (index < 0 || index >= _count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                return index == _count - 1 ? _finalDurationSeconds : _allowedDurationSeconds;
            }
        }

        public IEnumerator<double> GetEnumerator()
        {
            for (int index = 0; index < _count; index++)
            {
                yield return this[index];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
