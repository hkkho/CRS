using System;
using System.Collections.Generic;
using System.Linq;

namespace ReactorSim.Core
{
    /// <summary>
    /// Experimental accepted snapshot. Uncompensated reactivity is deliberately
    /// absent: these controlled solves do not measure it.
    /// </summary>
    public sealed class PracticeSingleSolveStateV1
    {
        internal PracticeSingleSolveStateV1(EquilibriumCoreSolverV1 owner,
            PracticeLiquidZoneRrsMappingV1 mapping, EquilibriumCoreProjectionV1 projection,
            IEnumerable<double> fills, IEnumerable<double> targets, IEnumerable<double> fractions,
            IEnumerable<double> errors, PracticeLiquidZoneRrsResponseModelV1 response,
            double time, int solveCount, EquilibriumCoreProjectionV1? warmStartProjection,
            bool controllerConverged, EquilibriumCoreProjectionV1? predictedProjection = null,
            EquilibriumCoreProjectionV1? fineTunedProjection = null, bool fineTuningAccepted = false)
        {
            Owner = owner;
            Mapping = mapping;
            Projection = projection;
            ZoneFills = Array.AsReadOnly(fills.ToArray());
            TargetZonalPowerFractions = Array.AsReadOnly(targets.ToArray());
            MeasuredZonalPowerFractions = Array.AsReadOnly(fractions.ToArray());
            ZonalShapeErrors = Array.AsReadOnly(errors.ToArray());
            ResponseModel = response;
            SimulationTimeSeconds = time;
            CandidateSolveCount = solveCount;
            WarmStartProjection = warmStartProjection;
            ControllerConverged = controllerConverged;
            PredictedProjection = predictedProjection;
            FineTunedProjection = fineTunedProjection;
            FineTuningAccepted = fineTuningAccepted;
        }

        internal EquilibriumCoreSolverV1 Owner { get; }
        public PracticeLiquidZoneRrsMappingV1 Mapping { get; }
        public EquilibriumCoreProjectionV1 Projection { get; }
        public IReadOnlyList<double> ZoneFills { get; }
        public IReadOnlyList<double> TargetZonalPowerFractions { get; }
        public IReadOnlyList<double> MeasuredZonalPowerFractions { get; }
        public IReadOnlyList<double> ZonalShapeErrors { get; }
        public PracticeLiquidZoneRrsResponseModelV1 ResponseModel { get; }
        public double SimulationTimeSeconds { get; }
        public int CandidateSolveCount { get; }
        public EquilibriumCoreProjectionV1? WarmStartProjection { get; }
        public bool ControllerConverged { get; }
        public EquilibriumCoreProjectionV1? PredictedProjection { get; }
        public EquilibriumCoreProjectionV1? FineTunedProjection { get; }
        public bool FineTuningAccepted { get; }
        public int SpatialIterationCount => (PredictedProjection ?? Projection).SolverIterationCount +
            (FineTunedProjection?.SolverIterationCount ?? 0);
    }

    public sealed partial class PracticeLiquidZoneRrsV1
    {
        /// <summary>
        /// No-history bootstrap: settle criticality and regional shape at a frozen inventory/time.
        /// Nothing is committed
        /// to the owning solver until the caller accepts the complete transaction.
        /// </summary>
        public static ContractValidationResult<PracticeSingleSolveStateV1> TryInitializeSingleSolve(
            EquilibriumCoreSolverV1 solver, IEnumerable<BundleState> bundles,
            PracticeLiquidZoneRrsV1 initialState, double simulationTimeSeconds = 0,
            StaticAbsorptionOverlayV1? backgroundOverlay = null, int maximumPasses = 8)
        {
            if (solver == null || bundles == null || initialState == null ||
                maximumPasses < 1 || maximumPasses > 32)
                return SingleSolveInvalid("Initialization", "Initialization requires a solver, inventory, RRS state and 1..32 passes.");

            BundleState[] inventory = bundles.ToArray();
            PracticeLiquidZoneRrsV1 state = initialState;
            EquilibriumCoreProjectionV1 projection = solver.CurrentProjection;
            int solves = 0;
            for (int pass = 0; pass < maximumPasses; pass++)
            {
                var result = TryRunEquilibrium(solver, inventory, state, simulationTimeSeconds,
                    projection.SpatialSolve, backgroundOverlay);
                if (!result.IsValid)
                    return ContractValidationResult<PracticeSingleSolveStateV1>.Invalid(result.FirstDiagnostic.Code,
                        result.FirstDiagnostic.Path, result.FirstDiagnostic.Message);
                state = result.Value.State;
                projection = result.Value.Projection;
                solves += state.TotalCandidateSolveCount;
                if (state.ControllerConverged)
                    return ContractValidationResult<PracticeSingleSolveStateV1>.Valid(
                        new PracticeSingleSolveStateV1(solver, state.Mapping, projection, state.ZoneFills,
                            state.TargetZonalPowerFractions, state.MeasuredZonalPowerFractions,
                            state.ZonalShapeErrors, state.CorrectionResponseModel ?? state.ResponseModel,
                            simulationTimeSeconds, solves, null, state.ControllerConverged));
            }
            return SingleSolveInvalid("Initialization.NotSettled",
                "The frozen initial state did not reach the criticality and regional-shape tolerances within the bootstrap pass limit.");
        }

        /// <summary>
        /// Predict fills from the last accepted observations, then solve exactly
        /// one candidate, seeded with that snapshot's fast/thermal flux and k.
        /// The current inventory/poison changes coefficients, never the seed.
        /// Failed candidates leave both the seed and owning solver untouched.
        /// </summary>
        public static ContractValidationResult<PracticeSingleSolveStateV1> TryRunSingleSolve(
            EquilibriumCoreSolverV1 solver, IEnumerable<BundleState> bundles,
            PracticeSingleSolveStateV1 previousState, double simulationTimeSeconds,
            StaticAbsorptionOverlayV1? backgroundOverlay = null) =>
            TryRunSnapshotChecks(solver, bundles, previousState, simulationTimeSeconds, backgroundOverlay, false);

        /// <summary>
        /// Check the analytical fill prediction, fine-tune from the measured
        /// flux/criticality residual, then check again with the first flux as seed.
        /// Both checks share frozen fuel/poison coefficients and one movement budget.
        /// Retain the prediction when the checked correction does not improve it.
        /// </summary>
        public static ContractValidationResult<PracticeSingleSolveStateV1> TryRunTwoSolve(
            EquilibriumCoreSolverV1 solver, IEnumerable<BundleState> bundles,
            PracticeSingleSolveStateV1 previousState, double simulationTimeSeconds,
            StaticAbsorptionOverlayV1? backgroundOverlay = null) =>
            TryRunSnapshotChecks(solver, bundles, previousState, simulationTimeSeconds, backgroundOverlay, true);

        /// <summary>
        /// Check an instantaneous inventory edit, including sequential refuels at
        /// one timestamp. Uses the existing event movement allowance, not a time
        /// interval; advancing fuel energy or isotopes is the caller's separate step.
        /// </summary>
        public static ContractValidationResult<PracticeSingleSolveStateV1> TryRunTwoSolveInventoryEvent(
            EquilibriumCoreSolverV1 solver, IEnumerable<BundleState> bundles,
            PracticeSingleSolveStateV1 previousState, double simulationTimeSeconds,
            StaticAbsorptionOverlayV1? backgroundOverlay = null) =>
            TryRunSnapshotChecks(solver, bundles, previousState, simulationTimeSeconds, backgroundOverlay, true, true);

        private static ContractValidationResult<PracticeSingleSolveStateV1> TryRunSnapshotChecks(
            EquilibriumCoreSolverV1 solver, IEnumerable<BundleState> bundles,
            PracticeSingleSolveStateV1 previousState, double simulationTimeSeconds,
            StaticAbsorptionOverlayV1? backgroundOverlay, bool fineTune, bool inventoryEvent = false)
        {
            if (solver == null || bundles == null || previousState == null ||
                !ReferenceEquals(solver, previousState.Owner))
                return SingleSolveInvalid("PreviousState", "A snapshot step requires the last accepted snapshot from this solver; initialize first.");
            if (!IsCanonicalTime(simulationTimeSeconds) || simulationTimeSeconds < previousState.SimulationTimeSeconds ||
                (!inventoryEvent && simulationTimeSeconds == previousState.SimulationTimeSeconds))
                return SingleSolveInvalid("Time", "A routine step must follow the accepted snapshot; inventory events may share its time.");

            double[] fills = previousState.ZoneFills.ToArray();
            var command = TrySolveBoundedFillCommand(previousState.ResponseModel,
                previousState.ZonalShapeErrors.ToArray(), previousState.Projection.RelativeReactivity,
                fills, fills);
            if (!command.IsValid)
                return ContractValidationResult<PracticeSingleSolveStateV1>.Invalid(command.FirstDiagnostic.Code,
                    command.FirstDiagnostic.Path, command.FirstDiagnostic.Message);
            double[] predicted = ApplyFillCommand(fills, fills, command.Value);
            // Preserve the existing 0.08/1800 s movement budget for shorter steps.
            double movementScale = inventoryEvent ? 1 : Math.Min(1, (simulationTimeSeconds - previousState.SimulationTimeSeconds) / 1800);
            for (int zone = 0; zone < predicted.Length; zone++)
                predicted[zone] = fills[zone] + (predicted[zone] - fills[zone]) * movementScale;
            var overlay = previousState.Mapping.TryBuildOverlay(predicted);
            if (!overlay.IsValid)
                return ContractValidationResult<PracticeSingleSolveStateV1>.Invalid(overlay.FirstDiagnostic.Code,
                    overlay.FirstDiagnostic.Path, overlay.FirstDiagnostic.Message);
            var prepared = solver.TryPrepareCandidates(bundles, backgroundOverlay);
            if (!prepared.IsValid)
                return ContractValidationResult<PracticeSingleSolveStateV1>.Invalid(prepared.FirstDiagnostic.Code,
                    prepared.FirstDiagnostic.Path, prepared.FirstDiagnostic.Message);

            var candidate = solver.TrySolveCandidate(prepared.Value,
                previousState.Projection.SpatialSolve, overlay.Value);
            if (!candidate.IsValid)
                return ContractValidationResult<PracticeSingleSolveStateV1>.Invalid(candidate.FirstDiagnostic.Code,
                    candidate.FirstDiagnostic.Path, candidate.FirstDiagnostic.Message);
            var measurement = MeasureZonalPower(previousState.Mapping, candidate.Value.ShapeNodePowerWatts,
                previousState.TargetZonalPowerFractions);
            if (!measurement.IsValid)
                return ContractValidationResult<PracticeSingleSolveStateV1>.Invalid(measurement.FirstDiagnostic.Code,
                    measurement.FirstDiagnostic.Path, measurement.FirstDiagnostic.Message);

            var response = previousState.ResponseModel.WithBaselineFractions(measurement.Value.Fractions);
            var final = new RrsCandidate(candidate.Value, measurement.Value, predicted, overlay.Value,
                ComputeWeightedResidual(response, measurement.Value.Errors, candidate.Value.RelativeReactivity), false);
            EquilibriumCoreProjectionV1? fineTuned = null;
            bool fineTuningAccepted = false;
            if (fineTune)
            {
                // Correct using the first check's actual flux-derived shape and k,
                // not the preceding timestep's observations. The inventory and
                // xenon remain frozen until BOTH spatial checks have completed.
                var correctionCommand = TrySolveBoundedFillCommand(response, measurement.Value.Errors,
                    candidate.Value.RelativeReactivity, predicted, fills);
                if (!correctionCommand.IsValid)
                    return ContractValidationResult<PracticeSingleSolveStateV1>.Invalid(correctionCommand.FirstDiagnostic.Code,
                        correctionCommand.FirstDiagnostic.Path, correctionCommand.FirstDiagnostic.Message);
                double[] corrected = ApplyFillCommand(predicted, fills, correctionCommand.Value);
                double movementBudget = PracticeLiquidZoneRrsIdentityV1.MaxFillMovementPerEvent * movementScale;
                for (int zone = 0; zone < corrected.Length; zone++)
                    corrected[zone] = CanonicalizeZero(Clamp(corrected[zone],
                        Math.Max(0, fills[zone] - movementBudget), Math.Min(1, fills[zone] + movementBudget)));
                var correctionOverlay = previousState.Mapping.TryBuildOverlay(corrected);
                if (!correctionOverlay.IsValid)
                    return ContractValidationResult<PracticeSingleSolveStateV1>.Invalid(correctionOverlay.FirstDiagnostic.Code,
                        correctionOverlay.FirstDiagnostic.Path, correctionOverlay.FirstDiagnostic.Message);
                var correction = solver.TrySolveCandidate(prepared.Value,
                    candidate.Value.SpatialSolve, correctionOverlay.Value);
                if (!correction.IsValid)
                    return ContractValidationResult<PracticeSingleSolveStateV1>.Invalid(correction.FirstDiagnostic.Code,
                        correction.FirstDiagnostic.Path, correction.FirstDiagnostic.Message);
                fineTuned = correction.Value;
                var correctionMeasurement = MeasureZonalPower(previousState.Mapping, correction.Value.ShapeNodePowerWatts,
                    previousState.TargetZonalPowerFractions);
                if (!correctionMeasurement.IsValid)
                    return ContractValidationResult<PracticeSingleSolveStateV1>.Invalid(correctionMeasurement.FirstDiagnostic.Code,
                        correctionMeasurement.FirstDiagnostic.Path, correctionMeasurement.FirstDiagnostic.Message);
                var checkedCorrection = new RrsCandidate(correction.Value, correctionMeasurement.Value,
                    corrected, correctionOverlay.Value, ComputeWeightedResidual(response,
                        correctionMeasurement.Value.Errors, correction.Value.RelativeReactivity), true);
                fineTuningAccepted = IsBetterCandidate(checkedCorrection, final);
                if (fineTuningAccepted) final = checkedCorrection;

                // A fill secant is valid here: both checks used the same prepared
                // material/poison state. Never fit across different timesteps.
                response = response.WithMeasuredResponse(Difference(corrected, predicted),
                    measurement.Value.Errors, candidate.Value.RelativeReactivity,
                    correctionMeasurement.Value.Errors, correction.Value.RelativeReactivity);
            }

            return ContractValidationResult<PracticeSingleSolveStateV1>.Valid(
                new PracticeSingleSolveStateV1(solver, previousState.Mapping, final.Projection, final.Fills,
                    previousState.TargetZonalPowerFractions, final.Measurement.Fractions,
                    final.Measurement.Errors, response.WithBaselineFractions(final.Measurement.Fractions),
                    simulationTimeSeconds, fineTune ? 2 : 1, previousState.Projection,
                    IsControllerConverged(final.Measurement.Errors, final.Projection.RelativeReactivity),
                    candidate.Value, fineTuned, fineTuningAccepted));
        }

        private static ContractValidationResult<PracticeSingleSolveStateV1> SingleSolveInvalid(string code, string message) =>
            ContractValidationResult<PracticeSingleSolveStateV1>.Invalid("PracticeSingleSolve." + code, "single_solve", message);
    }
}
