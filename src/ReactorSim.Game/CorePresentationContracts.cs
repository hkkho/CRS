using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ReactorSim.Core;

namespace ReactorSim.Game
{
    /// <summary>
    /// Explicit physics metrics exposed to presentation consumers. The
    /// current practice path is backed by deterministic static k-eigenmode
    /// shape recomputation plus a bounded static liquid-zone RRS projection.
    /// The legacy scalar compensation property names remain available for
    /// public v1 compatibility, but the live power amplitude is the operator
    /// equilibrium target rather than a transient response.
    /// </summary>
    public sealed class GamePhysicsPresentationSnapshot
    {
        private readonly string _staticReactivityMethodId;

        internal GamePhysicsPresentationSnapshot(
            string sourceId,
            string formulationId,
            string shapeMethodId,
            string amplitudeMethodId,
            string reactivityMethodId,
            string solveState,
            bool isAuthoritative,
            ulong bindingVersion,
            double referencePowerWatts,
            double powerAmplitude,
            double actualPowerFraction,
            double targetPowerWatts,
            double totalPowerWatts,
            double meanChannelPowerWatts,
            double meanBundlePowerWatts,
            double effectiveK,
            double reactivity,
            double weightedPerturbationReactivity,
            double reactivityNumerator,
            double reactivityDenominator,
            string reactivityIdentity,
            string reactivityBindingDigestHex,
            double powerBalanceRelativeError,
            string solverIdentity,
            int solverIterationCount,
            double solverResidualRelativeInfinity,
            double coreReactivity,
            double compensatedNetReactivity,
            double compensationState,
            double compensationCommand,
            double compensationLowerBound,
            double compensationUpperBound,
            bool compensationSaturated,
            double compensationResponseTimeSeconds,
            string cadenceIdentity,
            string adjointNormalizationIdentity,
            string adjointDigestHex,
            int adjointIterationCount,
            double adjointTransposeResidualRelativeInfinity)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                throw new ArgumentException("Physics metrics require a source identity.", nameof(sourceId));
            }

            RequireIdentity(formulationId, "formulation identity", nameof(formulationId));
            RequireIdentity(shapeMethodId, "shape method identity", nameof(shapeMethodId));
            RequireIdentity(amplitudeMethodId, "amplitude method identity", nameof(amplitudeMethodId));
            RequireIdentity(reactivityMethodId, "reactivity method identity", nameof(reactivityMethodId));

            if (string.IsNullOrWhiteSpace(solveState))
            {
                throw new ArgumentException("Physics metrics require a solve state.", nameof(solveState));
            }

            RequireFinitePositive(referencePowerWatts, nameof(referencePowerWatts));
            RequireFiniteNonnegative(powerAmplitude, nameof(powerAmplitude));
            RequireFiniteNonnegative(actualPowerFraction, nameof(actualPowerFraction));
            RequireFiniteNonnegative(targetPowerWatts, nameof(targetPowerWatts));
            RequireFiniteNonnegative(totalPowerWatts, nameof(totalPowerWatts));
            RequireFiniteNonnegative(meanChannelPowerWatts, nameof(meanChannelPowerWatts));
            RequireFiniteNonnegative(meanBundlePowerWatts, nameof(meanBundlePowerWatts));
            RequireFinitePositive(effectiveK, nameof(effectiveK));
            RequireFinite(reactivity, nameof(reactivity));
            RequireFinite(
                weightedPerturbationReactivity,
                nameof(weightedPerturbationReactivity));
            RequireFinite(reactivityNumerator, nameof(reactivityNumerator));
            RequireFinitePositive(reactivityDenominator, nameof(reactivityDenominator));
            RequireIdentity(
                reactivityIdentity,
                "reactivity identity",
                nameof(reactivityIdentity));
            RequireIdentity(
                reactivityBindingDigestHex,
                "reactivity binding digest",
                nameof(reactivityBindingDigestHex));
            RequireFinite(coreReactivity, nameof(coreReactivity));
            RequireFinite(compensatedNetReactivity, nameof(compensatedNetReactivity));
            RequireFinite(compensationState, nameof(compensationState));
            RequireFinite(compensationCommand, nameof(compensationCommand));
            RequireFinite(compensationLowerBound, nameof(compensationLowerBound));
            RequireFinite(compensationUpperBound, nameof(compensationUpperBound));
            if (compensationLowerBound >= compensationUpperBound ||
                compensationState < compensationLowerBound ||
                compensationState > compensationUpperBound ||
                compensationCommand < compensationLowerBound ||
                compensationCommand > compensationUpperBound)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(compensationState),
                    "Compensation state and command must remain within ordered explicit bounds.");
            }

            RequireFiniteNonnegative(
                compensationResponseTimeSeconds,
                nameof(compensationResponseTimeSeconds));
            RequireFiniteNonnegative(powerBalanceRelativeError, nameof(powerBalanceRelativeError));
            if (string.IsNullOrWhiteSpace(solverIdentity))
            {
                throw new ArgumentException(
                    "Physics metrics require a solver identity.",
                    nameof(solverIdentity));
            }

            if (string.IsNullOrWhiteSpace(cadenceIdentity))
            {
                throw new ArgumentException(
                    "Physics metrics require a deterministic cadence identity.",
                    nameof(cadenceIdentity));
            }

            if (string.IsNullOrWhiteSpace(adjointNormalizationIdentity))
            {
                throw new ArgumentException(
                    "Physics metrics require an adjoint normalization identity.",
                    nameof(adjointNormalizationIdentity));
            }

            if (string.IsNullOrWhiteSpace(adjointDigestHex))
            {
                throw new ArgumentException(
                    "Physics metrics require an adjoint digest.",
                    nameof(adjointDigestHex));
            }

            if (solverIterationCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(solverIterationCount));
            }

            if (adjointIterationCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(adjointIterationCount));
            }

            RequireFiniteNonnegative(
                solverResidualRelativeInfinity,
                nameof(solverResidualRelativeInfinity));
            RequireFiniteNonnegative(
                adjointTransposeResidualRelativeInfinity,
                nameof(adjointTransposeResidualRelativeInfinity));

            SourceId = sourceId;
            FormulationId = formulationId;
            ShapeMethodId = shapeMethodId;
            AmplitudeMethodId = amplitudeMethodId;
            ReactivityMethodId = reactivityMethodId;
            SolveState = solveState;
            IsAuthoritative = isAuthoritative;
            BindingVersion = bindingVersion;
            ReferencePowerWatts = referencePowerWatts;
            PowerAmplitude = powerAmplitude;
            ActualPowerFraction = actualPowerFraction;
            TargetPowerWatts = targetPowerWatts;
            TotalPowerWatts = totalPowerWatts;
            MeanChannelPowerWatts = meanChannelPowerWatts;
            MeanBundlePowerWatts = meanBundlePowerWatts;
            EffectiveK = effectiveK;
            Reactivity = reactivity;
            WeightedPerturbationReactivity = weightedPerturbationReactivity;
            ReactivityNumerator = reactivityNumerator;
            ReactivityDenominator = reactivityDenominator;
            ReactivityIdentity = reactivityIdentity;
            ReactivityBindingDigestHex = reactivityBindingDigestHex;
            _staticReactivityMethodId = EquilibriumCoreSolverIdentityV1.ReactivityMethodId;
            CoreReactivity = coreReactivity;
            CompensatedNetReactivity = compensatedNetReactivity;
            CompensationState = compensationState;
            CompensationCommand = compensationCommand;
            CompensationLowerBound = compensationLowerBound;
            CompensationUpperBound = compensationUpperBound;
            CompensationSaturated = compensationSaturated;
            CompensationResponseTimeSeconds = compensationResponseTimeSeconds;
            CadenceIdentity = cadenceIdentity;
            AdjointNormalizationIdentity = adjointNormalizationIdentity;
            AdjointDigestHex = adjointDigestHex;
            AdjointIterationCount = adjointIterationCount;
            AdjointTransposeResidualRelativeInfinity = adjointTransposeResidualRelativeInfinity;
            PowerBalanceRelativeError = powerBalanceRelativeError;
            SolverIdentity = solverIdentity;
            SolverIterationCount = solverIterationCount;
            SolverResidualRelativeInfinity = solverResidualRelativeInfinity;
        }

        public string SourceId { get; }

        public string FormulationId { get; }

        public string ShapeMethodId { get; }

        public string AmplitudeMethodId { get; }

        public string ReactivityMethodId { get; }

        public string SolveState { get; }

        public bool IsAuthoritative { get; }

        public ulong BindingVersion { get; }

        public double ReferencePowerWatts { get; }

        /// <summary>
        /// Scalar regulated steady-state practice amplitude, independent of
        /// the operator setpoint. The active gameplay cadence is identified
        /// by CadenceIdentity; this is not a sub-second transient claim.
        /// </summary>
        public double PowerAmplitude { get; }

        /// <summary>
        /// Normalized fission power after applying the regulated steady-state
        /// amplitude and normalized static-eigenmode shape to the operator
        /// setpoint.
        /// </summary>
        public double ActualPowerFraction { get; }

        public double TargetPowerWatts { get; }

        public double TotalPowerWatts { get; }

        public double MeanChannelPowerWatts { get; }

        public double MeanBundlePowerWatts { get; }

        public double EffectiveK { get; }

        public double Reactivity { get; }

        /// <summary>
        /// Legacy spatial state rho, derived only from EffectiveK as
        /// (k - 1) / k. It is intentionally separate from the operational
        /// first-order perturbation value below.
        /// </summary>
        public double StaticReactivity
        {
            get { return Reactivity; }
        }

        public string StaticReactivityMethodId
        {
            get { return _staticReactivityMethodId; }
        }

        /// <summary>
        /// B2's adjoint-weighted first-order perturbation reactivity. This is
        /// the value consumed by the regulated gameplay response.
        /// </summary>
        public double WeightedPerturbationReactivity { get; }

        public double ReactivityNumerator { get; }

        public double ReactivityDenominator { get; }

        public string ReactivityIdentity { get; }

        public string ReactivityBindingDigestHex { get; }

        /// <summary>
        /// Operational core reactivity supplied to the practice regulator.
        /// This is the B2 weighted perturbation value, not static k/rho.
        /// </summary>
        public double CoreReactivity { get; }

        /// <summary>
        /// Core reactivity plus the bounded scalar compensation state. This
        /// is the value the practice regulator is driving toward zero.
        /// </summary>
        public double CompensatedNetReactivity { get; }

        public double CompensationState { get; }

        /// <summary>
        /// Compatibility-facing physical-unit alias for CompensationState.
        /// </summary>
        public double CompensationReactivity
        {
            get { return CompensationState; }
        }

        public double CompensationCommand { get; }

        public double CompensationLowerBound { get; }

        public double CompensationUpperBound { get; }

        public bool CompensationSaturated { get; }

        public double CompensationResponseTimeSeconds { get; }

        public string CadenceIdentity { get; }

        public string AdjointNormalizationIdentity { get; }

        public string AdjointDigestHex { get; }

        public int AdjointIterationCount { get; }

        public double AdjointTransposeResidualRelativeInfinity { get; }

        public double PowerBalanceRelativeError { get; }

        public string SolverIdentity { get; }

        public int SolverIterationCount { get; }

        public double SolverResidualRelativeInfinity { get; }

        private static void RequireFinite(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(parameterName, "Physics metrics must be finite.");
            }
        }

        private static void RequireFiniteNonnegative(double value, string parameterName)
        {
            RequireFinite(value, parameterName);
            if (value < 0.0)
            {
                throw new ArgumentOutOfRangeException(parameterName, "Physics metrics must be nonnegative.");
            }
        }

        private static void RequireFinitePositive(double value, string parameterName)
        {
            RequireFinite(value, parameterName);
            if (value <= 0.0)
            {
                throw new ArgumentOutOfRangeException(parameterName, "This physics metric must be strictly positive.");
            }
        }

        private static void RequireIdentity(
            string value,
            string description,
            string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "Physics metrics require a " + description + ".",
                    parameterName);
            }
        }
    }

    /// <summary>
    /// Compact immutable I/Xe diagnostics for one selected channel. The
    /// authoritative node state remains in Core; presentation receives only
    /// channel aggregates and never a 4,560-node vector.
    /// </summary>
    public sealed class GameXenonChannelPresentationSnapshot
    {
        internal GameXenonChannelPresentationSnapshot(
            uint channelIndex,
            double meanI135NumberDensityM3,
            double maxI135NumberDensityM3,
            double meanXe135NumberDensityM3,
            double maxXe135NumberDensityM3,
            double meanDynamicAbsorptionGroup1PerM,
            double maxDynamicAbsorptionGroup1PerM,
            double meanDynamicAbsorptionGroup2PerM,
            double maxDynamicAbsorptionGroup2PerM)
        {
            RequireFiniteNonnegative(meanI135NumberDensityM3, nameof(meanI135NumberDensityM3));
            RequireFiniteNonnegative(maxI135NumberDensityM3, nameof(maxI135NumberDensityM3));
            RequireFiniteNonnegative(meanXe135NumberDensityM3, nameof(meanXe135NumberDensityM3));
            RequireFiniteNonnegative(maxXe135NumberDensityM3, nameof(maxXe135NumberDensityM3));
            RequireFiniteNonnegative(meanDynamicAbsorptionGroup1PerM, nameof(meanDynamicAbsorptionGroup1PerM));
            RequireFiniteNonnegative(maxDynamicAbsorptionGroup1PerM, nameof(maxDynamicAbsorptionGroup1PerM));
            RequireFiniteNonnegative(meanDynamicAbsorptionGroup2PerM, nameof(meanDynamicAbsorptionGroup2PerM));
            RequireFiniteNonnegative(maxDynamicAbsorptionGroup2PerM, nameof(maxDynamicAbsorptionGroup2PerM));
            ChannelIndex = channelIndex;
            MeanI135NumberDensityM3 = meanI135NumberDensityM3;
            MaxI135NumberDensityM3 = maxI135NumberDensityM3;
            MeanXe135NumberDensityM3 = meanXe135NumberDensityM3;
            MaxXe135NumberDensityM3 = maxXe135NumberDensityM3;
            MeanDynamicAbsorptionGroup1PerM = meanDynamicAbsorptionGroup1PerM;
            MaxDynamicAbsorptionGroup1PerM = maxDynamicAbsorptionGroup1PerM;
            MeanDynamicAbsorptionGroup2PerM = meanDynamicAbsorptionGroup2PerM;
            MaxDynamicAbsorptionGroup2PerM = maxDynamicAbsorptionGroup2PerM;
        }

        public uint ChannelIndex { get; }

        public double MeanI135NumberDensityM3 { get; }

        public double MaxI135NumberDensityM3 { get; }

        public double MeanXe135NumberDensityM3 { get; }

        public double MaxXe135NumberDensityM3 { get; }

        public double MeanDynamicAbsorptionGroup1PerM { get; }

        public double MaxDynamicAbsorptionGroup1PerM { get; }

        public double MeanDynamicAbsorptionGroup2PerM { get; }

        public double MaxDynamicAbsorptionGroup2PerM { get; }

        private static void RequireFiniteNonnegative(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "Xenon presentation metrics must be finite and nonnegative.");
            }
        }
    }

    /// <summary>
    /// Compact immutable spatial xenon diagnostics. Channel summaries are
    /// intentionally limited to 380 presentation channels; nodewise Core
    /// state and overlay arrays do not cross the Game boundary.
    /// </summary>
    public sealed class GameXenonPresentationSnapshot
    {
        internal GameXenonPresentationSnapshot(
            string stateIdentity,
            string stateDigestHex,
            ulong stateVersion,
            double simulationTimeSeconds,
            int nodeCount,
            string couplingIdentity,
            bool hasCoupling,
            string baseCoefficientDigestHex,
            string dynamicXenonDigestHex,
            string effectiveCoefficientDigestHex,
            double meanI135NumberDensityM3,
            double maxI135NumberDensityM3,
            double meanXe135NumberDensityM3,
            double maxXe135NumberDensityM3,
            double meanDynamicAbsorptionGroup1PerM,
            double maxDynamicAbsorptionGroup1PerM,
            double meanDynamicAbsorptionGroup2PerM,
            double maxDynamicAbsorptionGroup2PerM,
            IEnumerable<GameXenonChannelPresentationSnapshot> channels,
            int selectedChannelIndex)
        {
            if (string.IsNullOrWhiteSpace(stateIdentity) ||
                string.IsNullOrWhiteSpace(stateDigestHex) ||
                string.IsNullOrWhiteSpace(couplingIdentity))
            {
                throw new ArgumentException(
                    "Xenon presentation requires state and coupling identities/digests.");
            }

            RequireFiniteNonnegative(simulationTimeSeconds, nameof(simulationTimeSeconds));
            RequireFiniteNonnegative(meanI135NumberDensityM3, nameof(meanI135NumberDensityM3));
            RequireFiniteNonnegative(maxI135NumberDensityM3, nameof(maxI135NumberDensityM3));
            RequireFiniteNonnegative(meanXe135NumberDensityM3, nameof(meanXe135NumberDensityM3));
            RequireFiniteNonnegative(maxXe135NumberDensityM3, nameof(maxXe135NumberDensityM3));
            RequireFiniteNonnegative(meanDynamicAbsorptionGroup1PerM, nameof(meanDynamicAbsorptionGroup1PerM));
            RequireFiniteNonnegative(maxDynamicAbsorptionGroup1PerM, nameof(maxDynamicAbsorptionGroup1PerM));
            RequireFiniteNonnegative(meanDynamicAbsorptionGroup2PerM, nameof(meanDynamicAbsorptionGroup2PerM));
            RequireFiniteNonnegative(maxDynamicAbsorptionGroup2PerM, nameof(maxDynamicAbsorptionGroup2PerM));
            if (nodeCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(nodeCount));
            }

            StateIdentity = stateIdentity;
            StateDigestHex = stateDigestHex;
            StateVersion = stateVersion;
            SimulationTimeSeconds = simulationTimeSeconds;
            NodeCount = nodeCount;
            CouplingIdentity = couplingIdentity;
            HasCoupling = hasCoupling;
            BaseCoefficientDigestHex = baseCoefficientDigestHex;
            DynamicXenonDigestHex = dynamicXenonDigestHex;
            EffectiveCoefficientDigestHex = effectiveCoefficientDigestHex;
            MeanI135NumberDensityM3 = meanI135NumberDensityM3;
            MaxI135NumberDensityM3 = maxI135NumberDensityM3;
            MeanXe135NumberDensityM3 = meanXe135NumberDensityM3;
            MaxXe135NumberDensityM3 = maxXe135NumberDensityM3;
            MeanDynamicAbsorptionGroup1PerM = meanDynamicAbsorptionGroup1PerM;
            MaxDynamicAbsorptionGroup1PerM = maxDynamicAbsorptionGroup1PerM;
            MeanDynamicAbsorptionGroup2PerM = meanDynamicAbsorptionGroup2PerM;
            MaxDynamicAbsorptionGroup2PerM = maxDynamicAbsorptionGroup2PerM;

            if (channels == null)
            {
                throw new ArgumentNullException(nameof(channels));
            }

            GameXenonChannelPresentationSnapshot[] copy = channels.ToArray();
            if (copy.Length != GameCorePresentationConstants.ChannelCount)
            {
                throw new ArgumentException(
                    "Xenon presentation requires exactly 380 channel summaries.",
                    nameof(channels));
            }

            Channels = new ReadOnlyCollection<GameXenonChannelPresentationSnapshot>(copy);
            SelectedChannelIndex = selectedChannelIndex;
            SelectedChannel = selectedChannelIndex < 0
                ? null
                : GetChannel((uint)selectedChannelIndex);
        }

        public string StateIdentity { get; }

        public string StateDigestHex { get; }

        public ulong StateVersion { get; }

        public double SimulationTimeSeconds { get; }

        public int NodeCount { get; }

        public string CouplingIdentity { get; }

        public bool HasCoupling { get; }

        public string BaseCoefficientDigestHex { get; }

        public string DynamicXenonDigestHex { get; }

        public string EffectiveCoefficientDigestHex { get; }

        public double MeanI135NumberDensityM3 { get; }

        public double MaxI135NumberDensityM3 { get; }

        public double MeanXe135NumberDensityM3 { get; }

        public double MaxXe135NumberDensityM3 { get; }

        public double MeanDynamicAbsorptionGroup1PerM { get; }

        public double MaxDynamicAbsorptionGroup1PerM { get; }

        public double MeanDynamicAbsorptionGroup2PerM { get; }

        public double MaxDynamicAbsorptionGroup2PerM { get; }

        public IReadOnlyList<GameXenonChannelPresentationSnapshot> Channels { get; }

        public int SelectedChannelIndex { get; }

        public GameXenonChannelPresentationSnapshot? SelectedChannel { get; }

        private static void RequireFiniteNonnegative(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "Xenon presentation metrics must be finite and nonnegative.");
            }
        }

        public GameXenonChannelPresentationSnapshot GetChannel(uint channelIndex)
        {
            if (channelIndex >= Channels.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(channelIndex));
            }

            return Channels[(int)channelIndex];
        }
    }

    /// <summary>
    /// Compact presentation values for one logical practice liquid zone.
    /// Fractions are normalized against the accepted initial equilibrium;
    /// the fill is a bounded static absorber state, not a transient.
    /// </summary>
    public sealed class GameRrsZonePresentationSnapshot
    {
        internal GameRrsZonePresentationSnapshot(
            uint logicalZoneId,
            double fillFraction,
            double referencePowerFraction,
            double targetPowerFraction,
            double measuredPowerFraction,
            double shapeError)
        {
            if (logicalZoneId >= PracticeLiquidZoneRrsIdentityV1.LogicalZoneCount)
            {
                throw new ArgumentOutOfRangeException(nameof(logicalZoneId));
            }

            RequireFraction(fillFraction, nameof(fillFraction));
            RequireFraction(referencePowerFraction, nameof(referencePowerFraction));
            RequireFraction(targetPowerFraction, nameof(targetPowerFraction));
            RequireFraction(measuredPowerFraction, nameof(measuredPowerFraction));
            RequireFinite(shapeError, nameof(shapeError));

            LogicalZoneId = logicalZoneId;
            FillFraction = fillFraction;
            ReferencePowerFraction = referencePowerFraction;
            TargetPowerFraction = targetPowerFraction;
            MeasuredPowerFraction = measuredPowerFraction;
            ShapeError = shapeError;
        }

        public uint LogicalZoneId { get; }

        public double FillFraction { get; }

        public double ReferencePowerFraction { get; }

        public double TargetPowerFraction { get; }

        public double MeasuredPowerFraction { get; }

        public double ShapeError { get; }

        private static void RequireFraction(double value, string parameterName)
        {
            RequireFinite(value, parameterName);
            if (value < 0.0 || value > 1.0)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "RRS zonal fractions must remain within [0,1].");
            }
        }

        private static void RequireFinite(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }

    /// <summary>
    /// Compact, immutable practice RRS presentation contract. The complete
    /// node map and static overlay remain owned by Core; this surface exposes
    /// the fourteen fills and the controller diagnostics needed for playtest.
    /// </summary>
    public sealed class GameRrsPresentationSnapshot
    {
        internal GameRrsPresentationSnapshot(
            PracticeLiquidZoneRrsV1 state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            var zones = new List<GameRrsZonePresentationSnapshot>(
                (int)PracticeLiquidZoneRrsIdentityV1.LogicalZoneCount);
            for (uint zone = 0;
                 zone < PracticeLiquidZoneRrsIdentityV1.LogicalZoneCount;
                 zone++)
            {
                zones.Add(new GameRrsZonePresentationSnapshot(
                    zone,
                    state.ZoneFills[(int)zone],
                    state.ReferenceZonalPowerFractions[(int)zone],
                    state.TargetZonalPowerFractions[(int)zone],
                    state.MeasuredZonalPowerFractions[(int)zone],
                    state.ZonalShapeErrors[(int)zone]));
            }

            ControllerIdentity = state.ControllerIdentity;
            MappingIdentity = state.MappingIdentity;
            MappingDigestHex = DigestHex(state.MappingDigest);
            OverlayIdentity = state.AbsorptionOverlay.SourceIdentity;
            OverlayDigestHex = state.AbsorptionOverlay.OverlayDigestHex;
            StateDigestHex = state.StateDigestHex;
            SimulationTimeSeconds = state.SimulationTimeSeconds;
            NodeCount = checked((int)PracticeLiquidZoneRrsIdentityV1.NodeCount);
            AverageFillFraction = state.AverageFillFraction;
            MinimumFillFraction = state.MinimumFillFraction;
            MaximumFillFraction = state.MaximumFillFraction;
            MeasuredPowerWatts = state.MeasuredPowerWatts;
            TargetPowerWatts = state.TargetPowerWatts;
            PowerErrorWatts = state.PowerErrorWatts;
            CoreReactivity = state.CoreReactivity;
            CompensatedNetReactivity = state.CompensatedNetReactivity;
            CommonModeRhoCorrection = state.CommonModeRhoCorrection;
            ControllerIterationCount = state.ControllerIterationCount;
            ControllerConverged = state.ControllerConverged;
            LowExhaustion = state.LowExhaustion;
            HighExhaustion = state.HighExhaustion;
            IsGameOver = state.IsGameOver;
            GameOverReason = state.GameOverReason;
            CadenceIdentity = state.CadenceIdentity;
            Zones = new ReadOnlyCollection<GameRrsZonePresentationSnapshot>(zones);
        }

        public string ControllerIdentity { get; }

        public string MappingIdentity { get; }

        public string MappingDigestHex { get; }

        public string OverlayIdentity { get; }

        public string OverlayDigestHex { get; }

        public string StateDigestHex { get; }

        public double SimulationTimeSeconds { get; }

        public int NodeCount { get; }

        public IReadOnlyList<GameRrsZonePresentationSnapshot> Zones { get; }

        public double AverageFillFraction { get; }

        public double MinimumFillFraction { get; }

        public double MaximumFillFraction { get; }

        public double MeasuredPowerWatts { get; }

        public double TargetPowerWatts { get; }

        public double PowerErrorWatts { get; }

        public double CoreReactivity { get; }

        public double CompensatedNetReactivity { get; }

        public double CommonModeRhoCorrection { get; }

        public int ControllerIterationCount { get; }

        public bool ControllerConverged { get; }

        public bool LowExhaustion { get; }

        public bool HighExhaustion { get; }

        public bool IsGameOver { get; }

        public string GameOverReason { get; }

        public string CadenceIdentity { get; }

        public GameRrsZonePresentationSnapshot GetZone(uint logicalZoneId)
        {
            if (logicalZoneId >= Zones.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(logicalZoneId));
            }

            return Zones[(int)logicalZoneId];
        }

        private static string DigestHex(Digest32 digest)
        {
            var builder = new System.Text.StringBuilder(digest.Bytes.Count * 2 + 7);
            builder.Append("sha256:");
            foreach (byte value in digest.Bytes)
            {
                builder.Append(value.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }
    }

    /// <summary>
    /// Immutable presentation projection of the synthetic 380-channel core.
    /// It contains only values needed by the game surface; Core remains the
    /// owner of bundle transitions and physical state.
    /// </summary>
    public sealed class GameCorePresentationSnapshot
    {
        internal GameCorePresentationSnapshot(
            IEnumerable<GameChannelPresentationSnapshot> channels,
            GamePhysicsPresentationSnapshot physics,
            GameXenonPresentationSnapshot xenon,
            GameRrsPresentationSnapshot rrs)
        {
            if (channels == null)
            {
                throw new ArgumentNullException(nameof(channels));
            }

            Physics = physics ?? throw new ArgumentNullException(nameof(physics));
            Xenon = xenon ?? throw new ArgumentNullException(nameof(xenon));
            Rrs = rrs ?? throw new ArgumentNullException(nameof(rrs));

            GameChannelPresentationSnapshot[] copy = channels.ToArray();
            if (copy.Length != GameCorePresentationConstants.ChannelCount)
            {
                throw new ArgumentException(
                    "A core presentation requires exactly 380 channels.",
                    nameof(channels));
            }

            if (copy.Any(channel => channel == null))
            {
                throw new ArgumentException(
                    "A core presentation may not contain null channels.",
                    nameof(channels));
            }

            Channels = new ReadOnlyCollection<GameChannelPresentationSnapshot>(copy);
        }

        public IReadOnlyList<GameChannelPresentationSnapshot> Channels { get; }

        public GamePhysicsPresentationSnapshot Physics { get; }

        public GameXenonPresentationSnapshot Xenon { get; }

        public GameRrsPresentationSnapshot Rrs { get; }

        public uint ChannelCount
        {
            get { return checked((uint)Channels.Count); }
        }

        public GameChannelPresentationSnapshot GetChannel(uint channelIndex)
        {
            if (channelIndex >= ChannelCount)
            {
                throw new ArgumentOutOfRangeException(nameof(channelIndex));
            }

            return Channels[(int)channelIndex];
        }
    }

    public sealed class GameChannelPresentationSnapshot
    {
        internal GameChannelPresentationSnapshot(
            uint channelIndex,
            int gridColumn,
            int gridRow,
            double averageBurnupMwDayPerKg,
            double powerWatts,
            double localPowerFraction,
            double localTiltFraction,
            FlowDirection flowDirection,
            IEnumerable<GameBundlePresentationSnapshot> bundles,
            GameXenonChannelPresentationSnapshot xenon)
        {
            if (bundles == null)
            {
                throw new ArgumentNullException(nameof(bundles));
            }

            GameBundlePresentationSnapshot[] copy = bundles.ToArray();
            if (copy.Length != GameCorePresentationConstants.BundlePositionCount)
            {
                throw new ArgumentException(
                    "A channel presentation requires exactly 12 bundles.",
                    nameof(bundles));
            }

            ChannelIndex = channelIndex;
            GridColumn = gridColumn;
            GridRow = gridRow;
            AverageBurnupMwDayPerKg = averageBurnupMwDayPerKg;
            PowerWatts = powerWatts;
            LocalPowerFraction = localPowerFraction;
            LocalTiltFraction = localTiltFraction;
            FlowDirection = flowDirection;
            Xenon = xenon ?? throw new ArgumentNullException(nameof(xenon));
            Bundles = new ReadOnlyCollection<GameBundlePresentationSnapshot>(copy);
        }

        public uint ChannelIndex { get; }

        public int GridColumn { get; }

        public int GridRow { get; }

        public double AverageBurnupMwDayPerKg { get; }

        public double PowerWatts { get; }

        public double LocalPowerFraction { get; }

        public double LocalTiltFraction { get; }

        public FlowDirection FlowDirection { get; }

        public GameXenonChannelPresentationSnapshot Xenon { get; }

        public IReadOnlyList<GameBundlePresentationSnapshot> Bundles { get; }
    }

    public sealed class GameBundlePresentationSnapshot
    {
        internal GameBundlePresentationSnapshot(
            uint position,
            string bundleId,
            string fuelTypeId,
            double currentBurnupMwDayPerKg,
            double powerWatts,
            double insertedAtSeconds,
            ulong stateVersion)
        {
            if (string.IsNullOrWhiteSpace(bundleId))
            {
                throw new ArgumentException(
                    "A bundle presentation requires a bundle identity.",
                    nameof(bundleId));
            }

            if (string.IsNullOrWhiteSpace(fuelTypeId))
            {
                throw new ArgumentException(
                    "A bundle presentation requires a fuel type.",
                    nameof(fuelTypeId));
            }

            Position = position;
            BundleId = bundleId;
            FuelTypeId = fuelTypeId;
            CurrentBurnupMwDayPerKg = currentBurnupMwDayPerKg;
            PowerWatts = powerWatts;
            InsertedAtSeconds = insertedAtSeconds;
            StateVersion = stateVersion;
        }

        public uint Position { get; }

        public string BundleId { get; }

        public string FuelTypeId { get; }

        public double CurrentBurnupMwDayPerKg { get; }

        public double PowerWatts { get; }

        public double InsertedAtSeconds { get; }

        public ulong StateVersion { get; }

        public bool IsFresh
        {
            get { return Math.Abs(CurrentBurnupMwDayPerKg) < 1e-12; }
        }
    }

    public static class GameCorePresentationConstants
    {
        public const uint ChannelCount = 380;
        public const uint BundlePositionCount = 12;
        public const int GridWidth = 22;
        public const int GridHeight = 22;
        public const double JoulesPerMegaWattDayPerKilogram = 8.64e10;
    }

    /// <summary>
    /// Deterministic synthetic face layout. The row lengths describe a
    /// rounded 22x22 CANDU-6 face and sum to the approved 380 channels. The
    /// row lengths retain the stepped CANDU-6 outline rather than filling a
    /// rectangular 20-channel middle band.
    /// Channel identifiers are assigned row-major within this layout.
    /// </summary>
    internal static class PracticeCoreLayout
    {
        public static PracticeCoreGridPosition GetPosition(uint channelIndex)
        {
            Candu6GridPositionV1 position = Candu6CoreTopologyFactoryV1.GetPosition(channelIndex);
            return new PracticeCoreGridPosition(position.Column, position.DisplayRow);
        }

        public static bool TryGetChannelIndex(
            int column,
            int row,
            out uint channelIndex)
        {
            return Candu6CoreTopologyFactoryV1.TryGetChannelIndex(
                column,
                row,
                out channelIndex);
        }

        public static int GetRowLength(int row)
        {
            return Candu6CoreTopologyFactoryV1.GetRowLength(row);
        }

        public static FlowDirection GetFlowDirection(PracticeCoreGridPosition position)
        {
            return Candu6CoreTopologyFactoryV1.GetFlowDirection(
                position.Column,
                position.Row);
        }
    }

    internal readonly struct PracticeCoreGridPosition
    {
        internal PracticeCoreGridPosition(int column, int row)
        {
            Column = column;
            Row = row;
        }

        internal int Column { get; }

        internal int Row { get; }
    }
}
