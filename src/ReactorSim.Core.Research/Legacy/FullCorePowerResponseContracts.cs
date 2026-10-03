using System;

namespace ReactorSim.Core
{
    /// <summary>
    /// Maps the operator power setpoint to the power shown by the practice
    /// session after a static full-core solve. The diffusion solve continues
    /// to normalize its flux shape to a reference power; this response
    /// exposes changes in that power relative to the initial criticality
    /// calibration without pretending to be a point-kinetics calculation.
    /// </summary>
    public static class FullCorePowerResponseV1
    {
        public const string ModelId = "full-core-relative-criticality-power-response-v1";

        public static ContractValidationResult<double> TryComputeNormalizedPowerFraction(
            double requestedPowerFraction,
            double currentEffectiveK,
            double referenceEffectiveK)
        {
            if (!ContractValidation.IsFinite(requestedPowerFraction) ||
                requestedPowerFraction < 0.0)
            {
                return Invalid(
                    "FullCorePowerResponse.Request.Invalid",
                    "requested_power_fraction",
                    "The requested power fraction must be finite and nonnegative.");
            }

            if (!ContractValidation.IsFinite(currentEffectiveK) || currentEffectiveK <= 0.0)
            {
                return Invalid(
                    "FullCorePowerResponse.CurrentK.Invalid",
                    "current_effective_k",
                    "The current effective multiplication factor must be finite and strictly positive.");
            }

            if (!ContractValidation.IsFinite(referenceEffectiveK) || referenceEffectiveK <= 0.0)
            {
                return Invalid(
                    "FullCorePowerResponse.ReferenceK.Invalid",
                    "reference_effective_k",
                    "The reference effective multiplication factor must be finite and strictly positive.");
            }

            double normalizedPowerFraction = requestedPowerFraction *
                                             (currentEffectiveK / referenceEffectiveK);
            if (!ContractValidation.IsFinite(normalizedPowerFraction) ||
                normalizedPowerFraction < 0.0)
            {
                return Invalid(
                    "FullCorePowerResponse.Result.Invalid",
                    "normalized_power_fraction",
                    "The relative criticality power response must be finite and nonnegative.");
            }

            return ContractValidationResult<double>.Valid(normalizedPowerFraction);
        }

        private static ContractValidationResult<double> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<double>.Invalid(code, path, message);
        }
    }
}
