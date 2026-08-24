using System;

namespace ReactorSim.Core
{
    /// <summary>
    /// Caller-supplied outer source/eigen solve acceptance policy. Numeric
    /// thresholds are intentionally not selected by the runtime.
    /// </summary>
    public sealed class SpatialConvergencePolicy
    {
        private SpatialConvergencePolicy(
            double kAbsoluteTolerance,
            double kRelativeTolerance,
            double residualTolerance,
            double sourceShapeTolerance,
            double powerBalanceTolerance,
            int maximumIterations)
        {
            KAbsoluteTolerance = kAbsoluteTolerance;
            KRelativeTolerance = kRelativeTolerance;
            ResidualTolerance = residualTolerance;
            SourceShapeTolerance = sourceShapeTolerance;
            PowerBalanceTolerance = powerBalanceTolerance;
            MaximumIterations = maximumIterations;
        }

        public double KAbsoluteTolerance { get; }

        public double KRelativeTolerance { get; }

        public double ResidualTolerance { get; }

        public double SourceShapeTolerance { get; }

        public double PowerBalanceTolerance { get; }

        public int MaximumIterations { get; }

        public static ContractValidationResult<SpatialConvergencePolicy> TryCreate(
            double kAbsoluteTolerance,
            double kRelativeTolerance,
            double residualTolerance,
            double sourceShapeTolerance,
            double powerBalanceTolerance,
            int maximumIterations)
        {
            if (!IsPositiveFinite(kAbsoluteTolerance))
            {
                return ContractValidationResult<SpatialConvergencePolicy>.Invalid(
                    "SpatialConvergencePolicy.KAbsoluteTolerance.Invalid",
                    "convergence_policy.k_absolute_tolerance",
                    "The absolute eigenvalue tolerance must be finite and strictly positive.");
            }

            if (!IsPositiveFinite(kRelativeTolerance))
            {
                return ContractValidationResult<SpatialConvergencePolicy>.Invalid(
                    "SpatialConvergencePolicy.KRelativeTolerance.Invalid",
                    "convergence_policy.k_relative_tolerance",
                    "The relative eigenvalue tolerance must be finite and strictly positive.");
            }

            if (!IsPositiveFinite(residualTolerance))
            {
                return ContractValidationResult<SpatialConvergencePolicy>.Invalid(
                    "SpatialConvergencePolicy.ResidualTolerance.Invalid",
                    "convergence_policy.residual_tolerance",
                    "The outer residual tolerance must be finite and strictly positive.");
            }

            if (!IsPositiveFinite(sourceShapeTolerance))
            {
                return ContractValidationResult<SpatialConvergencePolicy>.Invalid(
                    "SpatialConvergencePolicy.SourceShapeTolerance.Invalid",
                    "convergence_policy.source_shape_tolerance",
                    "The source-shape tolerance must be finite and strictly positive.");
            }

            if (!IsPositiveFinite(powerBalanceTolerance))
            {
                return ContractValidationResult<SpatialConvergencePolicy>.Invalid(
                    "SpatialConvergencePolicy.PowerBalanceTolerance.Invalid",
                    "convergence_policy.power_balance_tolerance",
                    "The power-balance tolerance must be finite and strictly positive.");
            }

            if (maximumIterations <= 0)
            {
                return ContractValidationResult<SpatialConvergencePolicy>.Invalid(
                    "SpatialConvergencePolicy.IterationLimit.Invalid",
                    "convergence_policy.maximum_iterations",
                    "The maximum outer iteration count must be strictly positive.");
            }

            return ContractValidationResult<SpatialConvergencePolicy>.Valid(
                new SpatialConvergencePolicy(
                    kAbsoluteTolerance,
                    kRelativeTolerance,
                    residualTolerance,
                    sourceShapeTolerance,
                    powerBalanceTolerance,
                    maximumIterations));
        }

        private static bool IsPositiveFinite(double value)
        {
            return ContractValidation.IsFinite(value) && value > 0;
        }
    }

    public enum SpatialSolveStatus : byte
    {
        Converged = 1,
        Nonconverged = 2,
        Failed = 3
    }

    public enum SpatialInnerSolveStatus : byte
    {
        NotStarted = 0,
        Succeeded = 1,
        Failed = 2
    }

    /// <summary>
    /// Immutable diagnostics for one complete outer solve attempt. Nullable
    /// metrics are explicitly unavailable when no valid post-step state exists;
    /// they are never represented with NaN or infinity.
    /// </summary>
    public sealed class SpatialSolveDiagnostics
    {
        internal SpatialSolveDiagnostics(
            int iterationCount,
            double? eigenvalueChangeAbsolute,
            double? eigenvalueChangeRelative,
            double? residualAbsoluteInfinity,
            double? residualRelativeInfinity,
            double? sourceShapeChangeInfinity,
            double? powerBalanceRelative,
            SpatialInnerSolveStatus innerSolveStatus,
            SpatialConvergencePolicy policy,
            string convergenceReason,
            ContractDiagnostic? failureDiagnostic,
            int invalidCoefficientCount,
            int negativeFluxCount,
            int nonFiniteValueCount,
            int failedInnerSolveCount,
            int rejectedUpscatterCount,
            int clampCount,
            int forbiddenClampCount)
        {
            IterationCount = iterationCount;
            EigenvalueChangeAbsolute = eigenvalueChangeAbsolute;
            EigenvalueChangeRelative = eigenvalueChangeRelative;
            ResidualAbsoluteInfinity = residualAbsoluteInfinity;
            ResidualRelativeInfinity = residualRelativeInfinity;
            SourceShapeChangeInfinity = sourceShapeChangeInfinity;
            PowerBalanceRelative = powerBalanceRelative;
            InnerSolveStatus = innerSolveStatus;
            Policy = policy;
            ConvergenceReason = convergenceReason;
            FailureDiagnostic = failureDiagnostic;
            InvalidCoefficientCount = invalidCoefficientCount;
            NegativeFluxCount = negativeFluxCount;
            NonFiniteValueCount = nonFiniteValueCount;
            FailedInnerSolveCount = failedInnerSolveCount;
            RejectedUpscatterCount = rejectedUpscatterCount;
            ClampCount = clampCount;
            ForbiddenClampCount = forbiddenClampCount;
        }

        public int IterationCount { get; }

        public double? EigenvalueChangeAbsolute { get; }

        public double? EigenvalueChangeRelative { get; }

        public double? ResidualAbsoluteInfinity { get; }

        public double? ResidualRelativeInfinity { get; }

        public double? SourceShapeChangeInfinity { get; }

        public double? PowerBalanceRelative { get; }

        public SpatialInnerSolveStatus InnerSolveStatus { get; }

        public SpatialConvergencePolicy Policy { get; }

        public string ConvergenceReason { get; }

        public ContractDiagnostic? FailureDiagnostic { get; }

        public int InvalidCoefficientCount { get; }

        public int NegativeFluxCount { get; }

        public int NonFiniteValueCount { get; }

        public int FailedInnerSolveCount { get; }

        public int RejectedUpscatterCount { get; }

        public int ClampCount { get; }

        public int ForbiddenClampCount { get; }
    }

    /// <summary>
    /// Immutable result of an outer spatial solve. Failed and nonconverged
    /// results deliberately contain no usable final state, while retaining the
    /// deterministic diagnostics needed to explain the failure.
    /// </summary>
    public sealed class SpatialSolveResult
    {
        internal SpatialSolveResult(
            SpatialSolveStatus status,
            SpatialEigenIterationState? finalState,
            SpatialSolveDiagnostics diagnostics)
        {
            Status = status;
            FinalState = finalState;
            Diagnostics = diagnostics;
        }

        public SpatialSolveStatus Status { get; }

        public bool IsConverged
        {
            get { return Status == SpatialSolveStatus.Converged; }
        }

        public bool HasUsableState
        {
            get { return IsConverged && FinalState != null; }
        }

        public SpatialEigenIterationState? FinalState { get; }

        public SpatialSolveDiagnostics Diagnostics { get; }
    }

    /// <summary>
    /// Deterministic outer source/eigen convergence wrapper for P4-T03.
    /// </summary>
    public sealed class SpatialEigenSolve
    {
        private const string ConvergedReason = "converged";
        private const string MaximumIterationsReason = "maximum_iterations_exhausted";
        private const string InvalidStateReason = "invalid_state";
        private const string InnerSolveReason = "inner_solve_failed";
        private const string CounterOverflowReason = "iteration_counter_overflow";

        private readonly SpatialEigenIteration _iteration;
        private readonly SpatialConvergencePolicy _policy;
        private readonly SpatialOperator _operator;
        private readonly SpatialStencil _stencil;
        private readonly SpatialCoefficientSet _coefficients;
        private readonly double[] _group1Left;
        private readonly double[] _group2Left;
        private readonly double[] _group1Flux;
        private readonly double[] _group2Flux;
        private readonly double[] _fissionSource;
        private readonly double[] _previousSourceShape;
        private readonly double[] _nextSourceShape;

        private SpatialEigenSolve(
            SpatialEigenIteration iteration,
            SpatialConvergencePolicy policy)
        {
            _iteration = iteration;
            _policy = policy;
            _operator = iteration.Operator;
            _stencil = iteration.Stencil;
            _coefficients = iteration.Coefficients;

            int nodeCount = _stencil.NodeCount;
            _group1Left = new double[nodeCount];
            _group2Left = new double[nodeCount];
            _group1Flux = new double[nodeCount];
            _group2Flux = new double[nodeCount];
            _fissionSource = new double[nodeCount];
            _previousSourceShape = new double[nodeCount];
            _nextSourceShape = new double[nodeCount];
        }

        public int NodeCount
        {
            get { return _stencil.NodeCount; }
        }

        public SpatialConvergencePolicy Policy
        {
            get { return _policy; }
        }

        public static ContractValidationResult<SpatialEigenSolve> TryCreate(
            SpatialEigenIteration iteration,
            SpatialConvergencePolicy policy)
        {
            if (iteration == null)
            {
                return ContractValidationResult<SpatialEigenSolve>.Invalid(
                    "SpatialEigenSolve.Iteration.Missing",
                    "iteration",
                    "Outer convergence requires a validated one-step spatial iteration.");
            }

            if (policy == null)
            {
                return ContractValidationResult<SpatialEigenSolve>.Invalid(
                    "SpatialEigenSolve.ConvergencePolicy.Missing",
                    "convergence_policy",
                    "Outer convergence requires a complete caller-supplied policy.");
            }

            if (iteration.Stencil == null ||
                iteration.Coefficients == null ||
                iteration.Operator == null ||
                iteration.NodeCount <= 0)
            {
                return ContractValidationResult<SpatialEigenSolve>.Invalid(
                    "SpatialEigenSolve.Iteration.Invalid",
                    "iteration",
                    "The one-step iteration must retain a non-empty validated spatial context.");
            }

            return ContractValidationResult<SpatialEigenSolve>.Valid(
                new SpatialEigenSolve(iteration, policy));
        }

        /// <summary>
        /// Runs the caller-bounded outer solve. Invalid setup is returned as an
        /// invalid contract result; a numerical nonconvergence or runtime
        /// failure is a valid result object with status and diagnostics, never a
        /// usable last iterate.
        /// </summary>
        public ContractValidationResult<SpatialSolveResult> TrySolve()
        {
            ContractDiagnostic initialDiagnostic;
            SpatialIterationMetrics initialMetrics;
            if (!TryEvaluateState(
                    _iteration.InitialState,
                    _previousSourceShape,
                    out initialMetrics,
                    out initialDiagnostic))
            {
                return ContractValidationResult<SpatialSolveResult>.Valid(
                    CreateFailedResult(
                        0,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        SpatialInnerSolveStatus.NotStarted,
                        InvalidStateReason,
                        initialDiagnostic,
                        invalidCoefficientCount: 0,
                        negativeFluxCount: IsNegativeFluxDiagnostic(initialDiagnostic) ? 1 : 0,
                        nonFiniteValueCount: IsNonFiniteDiagnostic(initialDiagnostic)
                            ? 1
                            : 0,
                        failedInnerSolveCount: 0));
            }

            SpatialEigenIterationState current = _iteration.InitialState;
            double? lastDeltaKAbsolute = null;
            double? lastDeltaKRelative = null;
            double? lastResidualAbsolute = null;
            double? lastResidualRelative = null;
            double? lastShapeChange = null;
            double? lastPowerBalance = null;

            for (int outerIteration = 0;
                 outerIteration < _policy.MaximumIterations;
                 outerIteration++)
            {
                if (outerIteration == int.MaxValue)
                {
                    ContractDiagnostic overflowDiagnostic = new ContractDiagnostic(
                        "SpatialEigenSolve.Iteration.Overflow",
                        "convergence_policy.maximum_iterations",
                        "The outer iteration counter cannot advance beyond Int32.MaxValue.");
                    return ContractValidationResult<SpatialSolveResult>.Valid(
                        CreateFailedResult(
                            outerIteration,
                            lastDeltaKAbsolute,
                            lastDeltaKRelative,
                            lastResidualAbsolute,
                            lastResidualRelative,
                            lastShapeChange,
                            lastPowerBalance,
                            SpatialInnerSolveStatus.NotStarted,
                            CounterOverflowReason,
                            overflowDiagnostic,
                            invalidCoefficientCount: 0,
                            negativeFluxCount: 0,
                            nonFiniteValueCount: 0,
                            failedInnerSolveCount: 0));
                }

                ContractValidationResult<SpatialEigenIterationState> step =
                    _iteration.TryStep(current);
                if (!step.IsValid)
                {
                    bool innerFailure = IsInnerSolveDiagnostic(step.FirstDiagnostic);
                    return ContractValidationResult<SpatialSolveResult>.Valid(
                        CreateFailedResult(
                            outerIteration + 1,
                            lastDeltaKAbsolute,
                            lastDeltaKRelative,
                            lastResidualAbsolute,
                            lastResidualRelative,
                            lastShapeChange,
                            lastPowerBalance,
                            innerFailure
                                ? SpatialInnerSolveStatus.Failed
                                : SpatialInnerSolveStatus.Succeeded,
                            innerFailure ? InnerSolveReason : InvalidStateReason,
                            step.FirstDiagnostic,
                            invalidCoefficientCount: 0,
                            negativeFluxCount: IsNegativeFluxDiagnostic(step.FirstDiagnostic) ? 1 : 0,
                            nonFiniteValueCount: IsNonFiniteDiagnostic(step.FirstDiagnostic) ? 1 : 0,
                            failedInnerSolveCount: innerFailure ? 1 : 0));
                }

                SpatialEigenIterationState next = step.Value;
                ContractDiagnostic nextDiagnostic;
                SpatialIterationMetrics nextMetrics;
                if (!TryEvaluateState(
                        next,
                        _nextSourceShape,
                        out nextMetrics,
                        out nextDiagnostic))
                {
                    return ContractValidationResult<SpatialSolveResult>.Valid(
                        CreateFailedResult(
                            outerIteration + 1,
                            lastDeltaKAbsolute,
                            lastDeltaKRelative,
                            lastResidualAbsolute,
                            lastResidualRelative,
                            lastShapeChange,
                            lastPowerBalance,
                            SpatialInnerSolveStatus.Succeeded,
                            InvalidStateReason,
                            nextDiagnostic,
                            invalidCoefficientCount: 0,
                            negativeFluxCount: IsNegativeFluxDiagnostic(nextDiagnostic) ? 1 : 0,
                            nonFiniteValueCount: IsNonFiniteDiagnostic(nextDiagnostic) ? 1 : 0,
                            failedInnerSolveCount: 0));
                }

                double deltaKAbsolute = Math.Abs(next.Eigenvalue - current.Eigenvalue);
                double kScale = Math.Max(
                    Math.Abs(next.Eigenvalue),
                    Math.Abs(current.Eigenvalue));
                double deltaKRelative = kScale == 0.0
                    ? 0.0
                    : deltaKAbsolute / kScale;
                if (!ContractValidation.IsFinite(deltaKAbsolute) ||
                    !ContractValidation.IsFinite(deltaKRelative))
                {
                    ContractDiagnostic deltaDiagnostic = new ContractDiagnostic(
                        "SpatialEigenSolve.EigenvalueChange.NonFinite",
                        "convergence.delta_k",
                        "The eigenvalue change became non-finite.");
                    return ContractValidationResult<SpatialSolveResult>.Valid(
                        CreateFailedResult(
                            outerIteration + 1,
                            null,
                            null,
                            nextMetrics.ResidualAbsoluteInfinity,
                            nextMetrics.ResidualRelativeInfinity,
                            null,
                            nextMetrics.PowerBalanceRelative,
                            SpatialInnerSolveStatus.Succeeded,
                            InvalidStateReason,
                            deltaDiagnostic,
                            invalidCoefficientCount: 0,
                            negativeFluxCount: 0,
                            nonFiniteValueCount: 1,
                            failedInnerSolveCount: 0));
                }

                double sourceShapeChange = ComputeSourceShapeChange(
                    _previousSourceShape,
                    _nextSourceShape);
                if (!ContractValidation.IsFinite(sourceShapeChange))
                {
                    ContractDiagnostic shapeDiagnostic = new ContractDiagnostic(
                        "SpatialEigenSolve.SourceShapeChange.NonFinite",
                        "convergence.source_shape_change_inf",
                        "The source-shape change became non-finite.");
                    return ContractValidationResult<SpatialSolveResult>.Valid(
                        CreateFailedResult(
                            outerIteration + 1,
                            deltaKAbsolute,
                            deltaKRelative,
                            nextMetrics.ResidualAbsoluteInfinity,
                            nextMetrics.ResidualRelativeInfinity,
                            null,
                            nextMetrics.PowerBalanceRelative,
                            SpatialInnerSolveStatus.Succeeded,
                            InvalidStateReason,
                            shapeDiagnostic,
                            invalidCoefficientCount: 0,
                            negativeFluxCount: 0,
                            nonFiniteValueCount: 1,
                            failedInnerSolveCount: 0));
                }

                lastDeltaKAbsolute = deltaKAbsolute;
                lastDeltaKRelative = deltaKRelative;
                lastResidualAbsolute = nextMetrics.ResidualAbsoluteInfinity;
                lastResidualRelative = nextMetrics.ResidualRelativeInfinity;
                lastShapeChange = sourceShapeChange;
                lastPowerBalance = nextMetrics.PowerBalanceRelative;

                bool eigenvalueConverged =
                    deltaKAbsolute <= _policy.KAbsoluteTolerance ||
                    deltaKRelative <= _policy.KRelativeTolerance;
                bool converged =
                    eigenvalueConverged &&
                    nextMetrics.ResidualRelativeInfinity <= _policy.ResidualTolerance &&
                    sourceShapeChange <= _policy.SourceShapeTolerance &&
                    nextMetrics.PowerBalanceRelative <= _policy.PowerBalanceTolerance;

                if (converged)
                {
                    SpatialSolveDiagnostics diagnostics = CreateDiagnostics(
                        outerIteration + 1,
                        deltaKAbsolute,
                        deltaKRelative,
                        nextMetrics.ResidualAbsoluteInfinity,
                        nextMetrics.ResidualRelativeInfinity,
                        sourceShapeChange,
                        nextMetrics.PowerBalanceRelative,
                        SpatialInnerSolveStatus.Succeeded,
                        ConvergedReason,
                        null,
                        failedInnerSolveCount: 0);
                    return ContractValidationResult<SpatialSolveResult>.Valid(
                        new SpatialSolveResult(
                            SpatialSolveStatus.Converged,
                            next,
                            diagnostics));
                }

                Array.Copy(_nextSourceShape, _previousSourceShape, _nextSourceShape.Length);
                current = next;
            }

            ContractDiagnostic nonconvergenceDiagnostic = new ContractDiagnostic(
                "SpatialEigenSolve.Nonconverged",
                "convergence",
                "The outer solve exhausted its caller-supplied iteration limit without satisfying every convergence condition.");
            return ContractValidationResult<SpatialSolveResult>.Valid(
                CreateFailedResult(
                    _policy.MaximumIterations,
                    lastDeltaKAbsolute,
                    lastDeltaKRelative,
                    lastResidualAbsolute,
                    lastResidualRelative,
                    lastShapeChange,
                    lastPowerBalance,
                    SpatialInnerSolveStatus.Succeeded,
                    MaximumIterationsReason,
                    nonconvergenceDiagnostic,
                    invalidCoefficientCount: 0,
                    negativeFluxCount: 0,
                    nonFiniteValueCount: 0,
                    failedInnerSolveCount: 0,
                    status: SpatialSolveStatus.Nonconverged));
        }

        private SpatialSolveResult CreateFailedResult(
            int iterationCount,
            double? deltaKAbsolute,
            double? deltaKRelative,
            double? residualAbsolute,
            double? residualRelative,
            double? sourceShapeChange,
            double? powerBalance,
            SpatialInnerSolveStatus innerSolveStatus,
            string reason,
            ContractDiagnostic failureDiagnostic,
            int invalidCoefficientCount,
            int negativeFluxCount,
            int nonFiniteValueCount,
            int failedInnerSolveCount,
            SpatialSolveStatus status = SpatialSolveStatus.Failed)
        {
            return new SpatialSolveResult(
                status,
                null,
                CreateDiagnostics(
                    iterationCount,
                    deltaKAbsolute,
                    deltaKRelative,
                    residualAbsolute,
                    residualRelative,
                    sourceShapeChange,
                    powerBalance,
                    innerSolveStatus,
                    reason,
                    failureDiagnostic,
                    failedInnerSolveCount,
                    invalidCoefficientCount,
                    negativeFluxCount,
                    nonFiniteValueCount));
        }

        private SpatialSolveDiagnostics CreateDiagnostics(
            int iterationCount,
            double? deltaKAbsolute,
            double? deltaKRelative,
            double? residualAbsolute,
            double? residualRelative,
            double? sourceShapeChange,
            double? powerBalance,
            SpatialInnerSolveStatus innerSolveStatus,
            string reason,
            ContractDiagnostic? failureDiagnostic,
            int failedInnerSolveCount,
            int invalidCoefficientCount = 0,
            int negativeFluxCount = 0,
            int nonFiniteValueCount = 0)
        {
            return new SpatialSolveDiagnostics(
                iterationCount,
                deltaKAbsolute,
                deltaKRelative,
                residualAbsolute,
                residualRelative,
                sourceShapeChange,
                powerBalance,
                innerSolveStatus,
                _policy,
                reason,
                failureDiagnostic,
                invalidCoefficientCount,
                negativeFluxCount,
                nonFiniteValueCount,
                failedInnerSolveCount,
                rejectedUpscatterCount: 0,
                clampCount: 0,
                forbiddenClampCount: 0);
        }

        private bool TryEvaluateState(
            SpatialEigenIterationState state,
            double[] sourceShape,
            out SpatialIterationMetrics metrics,
            out ContractDiagnostic diagnostic)
        {
            metrics = default(SpatialIterationMetrics);
            if (state == null)
            {
                diagnostic = new ContractDiagnostic(
                    "SpatialEigenSolve.State.Missing",
                    "state",
                    "A spatial state is required for outer diagnostics.");
                return false;
            }

            if (!ReferenceEquals(state.Owner, _iteration))
            {
                diagnostic = new ContractDiagnostic(
                    "SpatialEigenSolve.State.OwnerMismatch",
                    "state",
                    "The state must belong to the wrapped one-step iteration.");
                return false;
            }

            if (state.Iteration < 0)
            {
                diagnostic = new ContractDiagnostic(
                    "SpatialEigenSolve.State.Iteration.Invalid",
                    "state.iteration",
                    "The outer state iteration must be nonnegative.");
                return false;
            }

            if (state.Group1Flux == null ||
                state.Group2Flux == null ||
                state.Group1Flux.Count != _stencil.NodeCount ||
                state.Group2Flux.Count != _stencil.NodeCount)
            {
                diagnostic = new ContractDiagnostic(
                    "SpatialEigenSolve.State.DimensionMismatch",
                    "state.flux",
                    "Both state flux vectors must match the stencil node count.");
                return false;
            }

            if (!ContractValidation.IsFinite(state.Eigenvalue) ||
                !ContractValidation.IsFinite(state.TotalPowerW) ||
                !ContractValidation.IsFinite(state.FissionProductionRate) ||
                !ContractValidation.IsFinite(state.NormalizationScale))
            {
                diagnostic = new ContractDiagnostic(
                    "SpatialEigenSolve.State.Scalar.NonFinite",
                    "state",
                    "State eigenvalue, production, power, and normalization scale must be finite.");
                return false;
            }

            if (state.Eigenvalue <= 0 ||
                state.TotalPowerW <= 0 ||
                state.FissionProductionRate <= 0 ||
                state.NormalizationScale <= 0)
            {
                diagnostic = new ContractDiagnostic(
                    "SpatialEigenSolve.State.Scalar.Invalid",
                    "state",
                    "State eigenvalue, production, power, and normalization scale must be strictly positive.");
                return false;
            }

            double productionTotal = 0.0;
            for (int nodeIndex = 0; nodeIndex < _stencil.NodeCount; nodeIndex++)
            {
                double group1Flux = state.Group1Flux[nodeIndex];
                double group2Flux = state.Group2Flux[nodeIndex];
                if (!ContractValidation.IsFinite(group1Flux) ||
                    !ContractValidation.IsFinite(group2Flux))
                {
                    diagnostic = new ContractDiagnostic(
                        "SpatialEigenSolve.State.Flux.NonFinite",
                        ContractValidation.NodePath(_stencil.Nodes[nodeIndex].Node, ".flux"),
                        "State flux must be finite.");
                    return false;
                }

                if (group1Flux < 0 || group2Flux < 0)
                {
                    diagnostic = new ContractDiagnostic(
                        "SpatialEigenSolve.State.Flux.Negative",
                        ContractValidation.NodePath(_stencil.Nodes[nodeIndex].Node, ".flux"),
                        "State flux must be componentwise nonnegative.");
                    return false;
                }

                _group1Flux[nodeIndex] = group1Flux;
                _group2Flux[nodeIndex] = group2Flux;
                SpatialNodeCoefficients coefficients = _coefficients.Nodes[nodeIndex];
                double fissionSource =
                    coefficients.NuFissionGroup1PerM * group1Flux +
                    coefficients.NuFissionGroup2PerM * group2Flux;
                double volumeProduction = coefficients.VolumeM3 * fissionSource;
                if (!ContractValidation.IsFinite(fissionSource) || fissionSource < 0 ||
                    !ContractValidation.IsFinite(volumeProduction) || volumeProduction < 0)
                {
                    diagnostic = new ContractDiagnostic(
                        "SpatialEigenSolve.State.Production.NonFinite",
                        ContractValidation.NodePath(_stencil.Nodes[nodeIndex].Node, ".fission_source"),
                        "Fission production must remain finite and nonnegative.");
                    return false;
                }

                _fissionSource[nodeIndex] = fissionSource;
                productionTotal += volumeProduction;
                if (!ContractValidation.IsFinite(productionTotal))
                {
                    diagnostic = new ContractDiagnostic(
                        "SpatialEigenSolve.State.Production.NonFinite",
                        "state.fission_production_rate",
                        "The ordered fission-production reduction became non-finite.");
                    return false;
                }
            }

            if (!ContractValidation.IsFinite(productionTotal) || productionTotal <= 0)
            {
                diagnostic = new ContractDiagnostic(
                    "SpatialEigenSolve.State.Production.Invalid",
                    "state.fission_production_rate",
                    "The state must have strictly positive finite fission production.");
                return false;
            }

            for (int nodeIndex = 0; nodeIndex < _stencil.NodeCount; nodeIndex++)
            {
                double sourceShapeValue =
                    _coefficients.Nodes[nodeIndex].VolumeM3 * _fissionSource[nodeIndex] /
                    productionTotal;
                if (!ContractValidation.IsFinite(sourceShapeValue) || sourceShapeValue < 0)
                {
                    diagnostic = new ContractDiagnostic(
                        "SpatialEigenSolve.SourceShape.Invalid",
                        ContractValidation.NodePath(_stencil.Nodes[nodeIndex].Node, ".source_shape"),
                        "The normalized source shape must be finite and nonnegative.");
                    return false;
                }

                sourceShape[nodeIndex] = sourceShapeValue;
            }

            if (!_operator.TryApply(
                    SpatialEnergyGroup.Group1,
                    _group1Flux,
                    _group1Left,
                    out diagnostic))
            {
                return false;
            }

            if (!_operator.TryApply(
                    SpatialEnergyGroup.Group2,
                    _group2Flux,
                    _group2Left,
                    out diagnostic))
            {
                return false;
            }

            double residualAbsolute = 0.0;
            double residualScale = 0.0;
            double inverseEigenvalue = 1.0 / state.Eigenvalue;
            if (!ContractValidation.IsFinite(inverseEigenvalue))
            {
                diagnostic = new ContractDiagnostic(
                    "SpatialEigenSolve.Residual.NonFinite",
                    "state.eigenvalue",
                    "The inverse eigenvalue used to construct residual sources became non-finite.");
                return false;
            }

            for (int nodeIndex = 0; nodeIndex < _stencil.NodeCount; nodeIndex++)
            {
                SpatialNodeCoefficients coefficients = _coefficients.Nodes[nodeIndex];
                double fissionOverK = _fissionSource[nodeIndex] * inverseEigenvalue;
                double group1Right = coefficients.ChiGroup1 * fissionOverK;
                double group2Right =
                    coefficients.DownscatterGroup1To2PerM * state.Group1Flux[nodeIndex] +
                    coefficients.ChiGroup2 * fissionOverK;
                if (!ContractValidation.IsFinite(group1Right) || group1Right < 0 ||
                    !ContractValidation.IsFinite(group2Right) || group2Right < 0)
                {
                    diagnostic = new ContractDiagnostic(
                        "SpatialEigenSolve.Residual.RightHandSide.Invalid",
                        ContractValidation.NodePath(_stencil.Nodes[nodeIndex].Node, ".rhs"),
                        "The residual source terms must be finite and nonnegative.");
                    return false;
                }

                if (!AccumulateResidual(
                        _group1Left[nodeIndex],
                        group1Right,
                        ref residualAbsolute,
                        ref residualScale) ||
                    !AccumulateResidual(
                        _group2Left[nodeIndex],
                        group2Right,
                        ref residualAbsolute,
                        ref residualScale))
                {
                    diagnostic = new ContractDiagnostic(
                        "SpatialEigenSolve.Residual.NonFinite",
                        ContractValidation.NodePath(_stencil.Nodes[nodeIndex].Node, ".residual"),
                        "The ordered residual reduction became non-finite.");
                    return false;
                }
            }

            double residualRelative = residualScale == 0.0
                ? 0.0
                : residualAbsolute / residualScale;
            double powerBalanceRelative =
                Math.Abs(state.TotalPowerW - _iteration.TargetPowerW) /
                _iteration.TargetPowerW;
            if (!ContractValidation.IsFinite(residualRelative) ||
                !ContractValidation.IsFinite(powerBalanceRelative))
            {
                diagnostic = new ContractDiagnostic(
                    "SpatialEigenSolve.Diagnostics.NonFinite",
                    "state.diagnostics",
                    "The outer residual or power-balance diagnostic became non-finite.");
                return false;
            }

            metrics = new SpatialIterationMetrics(
                residualAbsolute,
                residualRelative,
                powerBalanceRelative);
            diagnostic = null!;
            return true;
        }

        private static bool AccumulateResidual(
            double left,
            double right,
            ref double maximumAbsolute,
            ref double maximumScale)
        {
            double difference = left - right;
            double absoluteDifference = Math.Abs(difference);
            double scale = Math.Abs(left) + Math.Abs(right);
            if (!ContractValidation.IsFinite(difference) ||
                !ContractValidation.IsFinite(absoluteDifference) ||
                !ContractValidation.IsFinite(scale))
            {
                return false;
            }

            maximumAbsolute = Math.Max(maximumAbsolute, absoluteDifference);
            maximumScale = Math.Max(maximumScale, scale);
            return ContractValidation.IsFinite(maximumAbsolute) &&
                   ContractValidation.IsFinite(maximumScale);
        }

        private static double ComputeSourceShapeChange(
            double[] previous,
            double[] next)
        {
            double maximum = 0.0;
            for (int nodeIndex = 0; nodeIndex < previous.Length; nodeIndex++)
            {
                double difference = Math.Abs(next[nodeIndex] - previous[nodeIndex]);
                if (!ContractValidation.IsFinite(difference))
                {
                    return double.NaN;
                }

                maximum = Math.Max(maximum, difference);
            }

            return maximum;
        }

        private static bool IsInnerSolveDiagnostic(ContractDiagnostic diagnostic)
        {
            return diagnostic.Code.StartsWith(
                "SpatialEigenIteration.Inner",
                StringComparison.Ordinal) ||
                   diagnostic.Code.StartsWith(
                       "SpatialOperator.",
                       StringComparison.Ordinal);
        }

        private static bool IsNegativeFluxDiagnostic(ContractDiagnostic diagnostic)
        {
            return diagnostic.Code.Contains("Flux.Negative", StringComparison.Ordinal);
        }

        private static bool IsNonFiniteDiagnostic(ContractDiagnostic diagnostic)
        {
            return diagnostic.Code.Contains("NonFinite", StringComparison.Ordinal);
        }

        private readonly struct SpatialIterationMetrics
        {
            public SpatialIterationMetrics(
                double residualAbsoluteInfinity,
                double residualRelativeInfinity,
                double powerBalanceRelative)
            {
                ResidualAbsoluteInfinity = residualAbsoluteInfinity;
                ResidualRelativeInfinity = residualRelativeInfinity;
                PowerBalanceRelative = powerBalanceRelative;
            }

            public double ResidualAbsoluteInfinity { get; }

            public double ResidualRelativeInfinity { get; }

            public double PowerBalanceRelative { get; }
        }
    }
}
