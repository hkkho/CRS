using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ReactorSim.Core
{
    /// <summary>
    /// Caller-supplied acceptance policy for the deterministic scalar inner solve.
    /// </summary>
    public sealed class SpatialLinearSolvePolicy
    {
        public const string DeterministicJacobiMethodId = "jacobi-v1";

        public const string DeterministicJacobiMethodVersion = "1";

        private SpatialLinearSolvePolicy(
            string methodId,
            string methodVersion,
            double absoluteResidualTolerance,
            double relativeResidualTolerance,
            int maximumInnerIterations)
        {
            MethodId = methodId;
            MethodVersion = methodVersion;
            AbsoluteResidualTolerance = absoluteResidualTolerance;
            RelativeResidualTolerance = relativeResidualTolerance;
            MaximumInnerIterations = maximumInnerIterations;
        }

        public string MethodId { get; }

        public string MethodVersion { get; }

        public double AbsoluteResidualTolerance { get; }

        public double RelativeResidualTolerance { get; }

        public int MaximumInnerIterations { get; }

        public static ContractValidationResult<SpatialLinearSolvePolicy> TryCreate(
            string methodId,
            string methodVersion,
            double absoluteResidualTolerance,
            double relativeResidualTolerance,
            int maximumInnerIterations)
        {
            if (string.IsNullOrWhiteSpace(methodId) ||
                string.IsNullOrWhiteSpace(methodVersion))
            {
                return ContractValidationResult<SpatialLinearSolvePolicy>.Invalid(
                    "SpatialLinearSolvePolicy.Method.Missing",
                    "linear_solve_policy.method",
                    "A deterministic inner-solver method ID and version are required.");
            }

            if (!string.Equals(
                    methodId,
                    DeterministicJacobiMethodId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    methodVersion,
                    DeterministicJacobiMethodVersion,
                    StringComparison.Ordinal))
            {
                return ContractValidationResult<SpatialLinearSolvePolicy>.Invalid(
                    "SpatialLinearSolvePolicy.Method.Unsupported",
                    "linear_solve_policy.method",
                    "Only the approved deterministic jacobi-v1 method is available in P4-T03.");
            }

            if (!ContractValidation.IsFinite(absoluteResidualTolerance) ||
                absoluteResidualTolerance <= 0)
            {
                return ContractValidationResult<SpatialLinearSolvePolicy>.Invalid(
                    "SpatialLinearSolvePolicy.AbsoluteTolerance.Invalid",
                    "linear_solve_policy.absolute_residual_tolerance",
                    "The absolute inner residual tolerance must be finite and strictly positive.");
            }

            if (!ContractValidation.IsFinite(relativeResidualTolerance) ||
                relativeResidualTolerance <= 0)
            {
                return ContractValidationResult<SpatialLinearSolvePolicy>.Invalid(
                    "SpatialLinearSolvePolicy.RelativeTolerance.Invalid",
                    "linear_solve_policy.relative_residual_tolerance",
                    "The relative inner residual tolerance must be finite and strictly positive.");
            }

            if (maximumInnerIterations <= 0)
            {
                return ContractValidationResult<SpatialLinearSolvePolicy>.Invalid(
                    "SpatialLinearSolvePolicy.IterationLimit.Invalid",
                    "linear_solve_policy.maximum_inner_iterations",
                    "The maximum inner iteration count must be strictly positive.");
            }

            return ContractValidationResult<SpatialLinearSolvePolicy>.Valid(
                new SpatialLinearSolvePolicy(
                    methodId,
                    methodVersion,
                    absoluteResidualTolerance,
                    relativeResidualTolerance,
                    maximumInnerIterations));
        }
    }

    /// <summary>
    /// Immutable, power-normalized state produced by the P4-T03 source iteration.
    /// Outer convergence diagnostics are added by a later Phase 4 task.
    /// </summary>
    public sealed class SpatialEigenIterationState
    {
        private readonly ReadOnlyCollection<double> _group1Flux;
        private readonly ReadOnlyCollection<double> _group2Flux;

        internal SpatialEigenIterationState(
            SpatialEigenIteration owner,
            int iteration,
            double eigenvalue,
            double normalizationScale,
            double totalPowerW,
            double fissionProductionRate,
            double[] group1Flux,
            double[] group2Flux)
        {
            Owner = owner;
            Iteration = iteration;
            Eigenvalue = eigenvalue;
            NormalizationScale = normalizationScale;
            TotalPowerW = totalPowerW;
            FissionProductionRate = fissionProductionRate;
            _group1Flux = new ReadOnlyCollection<double>((double[])group1Flux.Clone());
            _group2Flux = new ReadOnlyCollection<double>((double[])group2Flux.Clone());
        }

        public int Iteration { get; }

        public double Eigenvalue { get; }

        public double NormalizationScale { get; }

        public double TotalPowerW { get; }

        public double FissionProductionRate { get; }

        public IReadOnlyList<double> Group1Flux
        {
            get { return _group1Flux; }
        }

        public IReadOnlyList<double> Group2Flux
        {
            get { return _group2Flux; }
        }

        internal SpatialEigenIteration Owner { get; }
    }

    /// <summary>
    /// Deterministic P2-T02 source/eigen iteration and fission-power normalization.
    /// This class deliberately does not decide when the outer solve converges.
    /// </summary>
    public sealed class SpatialEigenIteration
    {
        private readonly SpatialStencil _stencil;
        private readonly SpatialCoefficientSet _coefficients;
        private readonly SpatialOperator _operator;
        private readonly SpatialLinearSolvePolicy _linearSolvePolicy;
        private readonly double _targetPowerW;
        private readonly double[] _group1Diagonal;
        private readonly double[] _group2Diagonal;
        private readonly double[] _fissionSource;
        private readonly double[] _group1Source;
        private readonly double[] _group2Source;
        private readonly double[] _group1Solution;
        private readonly double[] _group1Candidate;
        private readonly double[] _group1Applied;
        private readonly double[] _group2Solution;
        private readonly double[] _group2Candidate;
        private readonly double[] _group2Applied;
        private readonly double[] _trialGroup1Flux;
        private readonly double[] _trialGroup2Flux;
        private readonly double[] _normalizedGroup1Flux;
        private readonly double[] _normalizedGroup2Flux;
        private readonly double[] _normalizedFissionSource;
        private SpatialEigenIterationState _initialState = null!;

        private SpatialEigenIteration(
            SpatialStencil stencil,
            SpatialCoefficientSet coefficients,
            SpatialOperator spatialOperator,
            SpatialLinearSolvePolicy linearSolvePolicy,
            double targetPowerW,
            double[] group1Diagonal,
            double[] group2Diagonal)
        {
            _stencil = stencil;
            _coefficients = coefficients;
            _operator = spatialOperator;
            _linearSolvePolicy = linearSolvePolicy;
            _targetPowerW = targetPowerW;
            _group1Diagonal = group1Diagonal;
            _group2Diagonal = group2Diagonal;

            int nodeCount = stencil.NodeCount;
            _fissionSource = new double[nodeCount];
            _group1Source = new double[nodeCount];
            _group2Source = new double[nodeCount];
            _group1Solution = new double[nodeCount];
            _group1Candidate = new double[nodeCount];
            _group1Applied = new double[nodeCount];
            _group2Solution = new double[nodeCount];
            _group2Candidate = new double[nodeCount];
            _group2Applied = new double[nodeCount];
            _trialGroup1Flux = new double[nodeCount];
            _trialGroup2Flux = new double[nodeCount];
            _normalizedGroup1Flux = new double[nodeCount];
            _normalizedGroup2Flux = new double[nodeCount];
            _normalizedFissionSource = new double[nodeCount];
        }

        public int NodeCount
        {
            get { return _stencil.NodeCount; }
        }

        public double TargetPowerW
        {
            get { return _targetPowerW; }
        }

        public SpatialLinearSolvePolicy LinearSolvePolicy
        {
            get { return _linearSolvePolicy; }
        }

        internal SpatialStencil Stencil
        {
            get { return _stencil; }
        }

        internal SpatialCoefficientSet Coefficients
        {
            get { return _coefficients; }
        }

        internal SpatialOperator Operator
        {
            get { return _operator; }
        }

        public SpatialEigenIterationState InitialState
        {
            get { return _initialState; }
        }

        public static ContractValidationResult<SpatialEigenIteration> TryCreate(
            SpatialStencil stencil,
            SpatialCoefficientSet coefficients,
            SpatialLinearSolvePolicy linearSolvePolicy,
            double targetPowerW,
            double initialEigenvalue,
            double[]? initialGroup1Flux = null,
            double[]? initialGroup2Flux = null)
        {
            if (stencil == null)
            {
                return ContractValidationResult<SpatialEigenIteration>.Invalid(
                    "SpatialEigenIteration.Stencil.Missing",
                    "stencil",
                    "Source iteration requires an assembled stencil.");
            }

            if (coefficients == null)
            {
                return ContractValidationResult<SpatialEigenIteration>.Invalid(
                    "SpatialEigenIteration.Coefficients.Missing",
                    "coefficients",
                    "Source iteration requires validated coefficients.");
            }

            if (linearSolvePolicy == null)
            {
                return ContractValidationResult<SpatialEigenIteration>.Invalid(
                    "SpatialEigenIteration.LinearSolvePolicy.Missing",
                    "linear_solve_policy",
                    "Source iteration requires an explicit inner-solve policy.");
            }

            if (!ContractValidation.IsFinite(targetPowerW) || targetPowerW <= 0)
            {
                return ContractValidationResult<SpatialEigenIteration>.Invalid(
                    "SpatialEigenIteration.TargetPower.Invalid",
                    "target_power_w",
                    "The requested normalization target must be finite and strictly positive SI watts.");
            }

            if (!ContractValidation.IsFinite(initialEigenvalue) || initialEigenvalue <= 0)
            {
                return ContractValidationResult<SpatialEigenIteration>.Invalid(
                    "SpatialEigenIteration.InitialEigenvalue.Invalid",
                    "initial_eigenvalue",
                    "The initial eigenvalue must be finite and strictly positive.");
            }

            if ((initialGroup1Flux == null) != (initialGroup2Flux == null))
            {
                return ContractValidationResult<SpatialEigenIteration>.Invalid(
                    "SpatialEigenIteration.InitialFlux.Incomplete",
                    "initial_flux",
                    "Both group flux vectors must be supplied together or both omitted.");
            }

            if (initialGroup1Flux != null &&
                (initialGroup1Flux.Length != stencil.NodeCount ||
                 initialGroup2Flux!.Length != stencil.NodeCount))
            {
                return ContractValidationResult<SpatialEigenIteration>.Invalid(
                    "SpatialEigenIteration.InitialFlux.DimensionMismatch",
                    "initial_flux",
                    "Both initial flux vectors must match the stencil node count.");
            }

            ContractValidationResult<SpatialOperator> operatorResult =
                SpatialOperator.TryCreate(stencil, coefficients);
            if (!operatorResult.IsValid)
            {
                return ContractValidationResult<SpatialEigenIteration>.Invalid(
                    operatorResult.FirstDiagnostic.Code,
                    operatorResult.FirstDiagnostic.Path,
                    operatorResult.FirstDiagnostic.Message);
            }

            ContractValidationResult<double[]> group1DiagonalResult =
                BuildDiagonal(stencil, coefficients, SpatialEnergyGroup.Group1);
            if (!group1DiagonalResult.IsValid)
            {
                return ContractValidationResult<SpatialEigenIteration>.Invalid(
                    group1DiagonalResult.FirstDiagnostic.Code,
                    group1DiagonalResult.FirstDiagnostic.Path,
                    group1DiagonalResult.FirstDiagnostic.Message);
            }

            ContractValidationResult<double[]> group2DiagonalResult =
                BuildDiagonal(stencil, coefficients, SpatialEnergyGroup.Group2);
            if (!group2DiagonalResult.IsValid)
            {
                return ContractValidationResult<SpatialEigenIteration>.Invalid(
                    group2DiagonalResult.FirstDiagnostic.Code,
                    group2DiagonalResult.FirstDiagnostic.Path,
                    group2DiagonalResult.FirstDiagnostic.Message);
            }

            var iteration = new SpatialEigenIteration(
                stencil,
                coefficients,
                operatorResult.Value,
                linearSolvePolicy,
                targetPowerW,
                group1DiagonalResult.Value,
                group2DiagonalResult.Value);

            double[] group1Flux = initialGroup1Flux == null
                ? CreateUnitFlux(stencil.NodeCount)
                : (double[])initialGroup1Flux.Clone();
            double[] group2Flux = initialGroup2Flux == null
                ? CreateUnitFlux(stencil.NodeCount)
                : (double[])initialGroup2Flux!.Clone();

            ContractValidationResult<SpatialEigenIterationState> initialState =
                iteration.CreateInitialState(
                    initialEigenvalue,
                    group1Flux,
                    group2Flux);
            if (!initialState.IsValid)
            {
                return ContractValidationResult<SpatialEigenIteration>.Invalid(
                    initialState.FirstDiagnostic.Code,
                    initialState.FirstDiagnostic.Path,
                    initialState.FirstDiagnostic.Message);
            }

            iteration._initialState = initialState.Value;
            return ContractValidationResult<SpatialEigenIteration>.Valid(iteration);
        }

        public ContractValidationResult<SpatialEigenIterationState> TryStep(
            SpatialEigenIterationState state)
        {
            if (state == null)
            {
                return ContractValidationResult<SpatialEigenIterationState>.Invalid(
                    "SpatialEigenIteration.State.Missing",
                    "state",
                    "A prior source-iteration state is required.");
            }

            if (!ReferenceEquals(state.Owner, this))
            {
                return ContractValidationResult<SpatialEigenIterationState>.Invalid(
                    "SpatialEigenIteration.State.OwnerMismatch",
                    "state",
                    "The state must belong to this source-iteration instance.");
            }

            if (state.Iteration == int.MaxValue)
            {
                return ContractValidationResult<SpatialEigenIterationState>.Invalid(
                    "SpatialEigenIteration.Iteration.Overflow",
                    "state.iteration",
                    "The source-iteration counter cannot advance beyond Int32.MaxValue.");
            }

            int nodeCount = _stencil.NodeCount;
            for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
            {
                double group1 = state.Group1Flux[nodeIndex];
                double group2 = state.Group2Flux[nodeIndex];
                if (!IsValidFlux(group1) || !IsValidFlux(group2))
                {
                    return ContractValidationResult<SpatialEigenIterationState>.Invalid(
                        "SpatialEigenIteration.State.Invalid",
                        ContractValidation.NodePath(_stencil.Nodes[nodeIndex].Node, ".flux"),
                        "A source-iteration state must contain finite componentwise nonnegative flux.");
                }
            }

            double currentProduction = ComputeFissionProduction(
                state.Group1Flux,
                state.Group2Flux,
                _fissionSource);
            if (!ContractValidation.IsFinite(currentProduction) || currentProduction <= 0)
            {
                return ContractValidationResult<SpatialEigenIterationState>.Invalid(
                    "SpatialEigenIteration.Source.ZeroProduction",
                    "state.fission_production_rate",
                    "The current normalized state must have strictly positive finite fission production.");
            }

            double currentEigenvalue = state.Eigenvalue;
            for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
            {
                SpatialNodeCoefficients coefficients = _coefficients.Nodes[nodeIndex];
                double fissionOverK = _fissionSource[nodeIndex] / currentEigenvalue;
                double group1Source = coefficients.ChiGroup1 * fissionOverK;
                if (!ContractValidation.IsFinite(group1Source) || group1Source < 0)
                {
                    return ContractValidationResult<SpatialEigenIterationState>.Invalid(
                        "SpatialEigenIteration.Source.Group1.Invalid",
                        ContractValidation.NodePath(_stencil.Nodes[nodeIndex].Node, ".group1_source"),
                        "The group 1 fission source must be finite and componentwise nonnegative.");
                }

                _group1Source[nodeIndex] = group1Source;
            }

            ContractDiagnostic solveDiagnostic;
            if (!TrySolve(
                    SpatialEnergyGroup.Group1,
                    _group1Source,
                    _group1Diagonal,
                    _group1Solution,
                    _group1Candidate,
                    _group1Applied,
                    out solveDiagnostic))
            {
                return ContractValidationResult<SpatialEigenIterationState>.Invalid(
                    solveDiagnostic.Code,
                    solveDiagnostic.Path,
                    solveDiagnostic.Message);
            }

            for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
            {
                SpatialNodeCoefficients coefficients = _coefficients.Nodes[nodeIndex];
                double fissionOverK = _fissionSource[nodeIndex] / currentEigenvalue;
                double group2Source =
                    coefficients.DownscatterGroup1To2PerM * _group1Solution[nodeIndex] +
                    coefficients.ChiGroup2 * fissionOverK;
                if (!ContractValidation.IsFinite(group2Source) || group2Source < 0)
                {
                    return ContractValidationResult<SpatialEigenIterationState>.Invalid(
                        "SpatialEigenIteration.Source.Group2.Invalid",
                        ContractValidation.NodePath(_stencil.Nodes[nodeIndex].Node, ".group2_source"),
                        "The group 2 downscatter and fission source must be finite and componentwise nonnegative.");
                }

                _group2Source[nodeIndex] = group2Source;
            }

            if (!TrySolve(
                    SpatialEnergyGroup.Group2,
                    _group2Source,
                    _group2Diagonal,
                    _group2Solution,
                    _group2Candidate,
                    _group2Applied,
                    out solveDiagnostic))
            {
                return ContractValidationResult<SpatialEigenIterationState>.Invalid(
                    solveDiagnostic.Code,
                    solveDiagnostic.Path,
                    solveDiagnostic.Message);
            }

            for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
            {
                _trialGroup1Flux[nodeIndex] = _group1Solution[nodeIndex];
                _trialGroup2Flux[nodeIndex] = _group2Solution[nodeIndex];
            }

            double trialProduction = ComputeFissionProduction(
                _trialGroup1Flux,
                _trialGroup2Flux,
                _normalizedFissionSource);
            if (!ContractValidation.IsFinite(trialProduction) || trialProduction <= 0)
            {
                return ContractValidationResult<SpatialEigenIterationState>.Invalid(
                    "SpatialEigenIteration.Trial.ZeroProduction",
                    "trial.fission_production_rate",
                    "The trial state must have strictly positive finite fission production.");
            }

            double nextEigenvalue = currentEigenvalue * (trialProduction / currentProduction);
            if (!ContractValidation.IsFinite(nextEigenvalue) || nextEigenvalue <= 0)
            {
                return ContractValidationResult<SpatialEigenIterationState>.Invalid(
                    "SpatialEigenIteration.Eigenvalue.NonFinite",
                    "trial.eigenvalue",
                    "The updated eigenvalue must be finite and strictly positive.");
            }

            double trialPower = ComputePower(
                _trialGroup1Flux,
                _trialGroup2Flux);
            if (!ContractValidation.IsFinite(trialPower) || trialPower <= 0)
            {
                return ContractValidationResult<SpatialEigenIterationState>.Invalid(
                    "SpatialEigenIteration.Trial.PowerInvalid",
                    "trial.total_power_w",
                    "The trial state must have strictly positive finite fission power.");
            }

            double normalizationScale = _targetPowerW / trialPower;
            if (!ContractValidation.IsFinite(normalizationScale) || normalizationScale <= 0)
            {
                return ContractValidationResult<SpatialEigenIterationState>.Invalid(
                    "SpatialEigenIteration.NormalizationScale.Invalid",
                    "trial.normalization_scale",
                    "The trial normalization scale must be finite and strictly positive.");
            }

            for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
            {
                double normalizedGroup1 = _trialGroup1Flux[nodeIndex] * normalizationScale;
                double normalizedGroup2 = _trialGroup2Flux[nodeIndex] * normalizationScale;
                if (!IsValidFlux(normalizedGroup1) || !IsValidFlux(normalizedGroup2))
                {
                    return ContractValidationResult<SpatialEigenIterationState>.Invalid(
                        "SpatialEigenIteration.NormalizedFlux.Invalid",
                        ContractValidation.NodePath(_stencil.Nodes[nodeIndex].Node, ".flux"),
                        "Normalized flux must remain finite and componentwise nonnegative.");
                }

                _normalizedGroup1Flux[nodeIndex] = normalizedGroup1;
                _normalizedGroup2Flux[nodeIndex] = normalizedGroup2;
            }

            double normalizedProduction = ComputeFissionProduction(
                _normalizedGroup1Flux,
                _normalizedGroup2Flux,
                _normalizedFissionSource);
            double normalizedPower = ComputePower(
                _normalizedGroup1Flux,
                _normalizedGroup2Flux);
            if (!ContractValidation.IsFinite(normalizedProduction) ||
                normalizedProduction <= 0 ||
                !ContractValidation.IsFinite(normalizedPower) ||
                normalizedPower <= 0)
            {
                return ContractValidationResult<SpatialEigenIterationState>.Invalid(
                    "SpatialEigenIteration.NormalizedState.Invalid",
                    "normalized_state",
                    "The normalized state must have strictly positive finite production and power.");
            }

            return ContractValidationResult<SpatialEigenIterationState>.Valid(
                new SpatialEigenIterationState(
                    this,
                    checked(state.Iteration + 1),
                    nextEigenvalue,
                    normalizationScale,
                    normalizedPower,
                    normalizedProduction,
                    _normalizedGroup1Flux,
                    _normalizedGroup2Flux));
        }

        private ContractValidationResult<SpatialEigenIterationState> CreateInitialState(
            double initialEigenvalue,
            double[] group1Flux,
            double[] group2Flux)
        {
            for (int nodeIndex = 0; nodeIndex < _stencil.NodeCount; nodeIndex++)
            {
                if (!IsValidFlux(group1Flux[nodeIndex]) ||
                    !IsValidFlux(group2Flux[nodeIndex]))
                {
                    return ContractValidationResult<SpatialEigenIterationState>.Invalid(
                        "SpatialEigenIteration.InitialFlux.Invalid",
                        ContractValidation.NodePath(_stencil.Nodes[nodeIndex].Node, ".initial_flux"),
                        "Initial flux must be finite and componentwise nonnegative.");
                }
            }

            double initialPower = ComputePower(group1Flux, group2Flux);
            if (!ContractValidation.IsFinite(initialPower) || initialPower <= 0)
            {
                return ContractValidationResult<SpatialEigenIterationState>.Invalid(
                    "SpatialEigenIteration.InitialPower.Invalid",
                    "initial_flux",
                    "Initial flux must have strictly positive finite fission power.");
            }

            double normalizationScale = _targetPowerW / initialPower;
            if (!ContractValidation.IsFinite(normalizationScale) || normalizationScale <= 0)
            {
                return ContractValidationResult<SpatialEigenIterationState>.Invalid(
                    "SpatialEigenIteration.InitialNormalization.Invalid",
                    "initial_flux",
                    "The initial normalization scale must be finite and strictly positive.");
            }

            double[] normalizedGroup1 = new double[_stencil.NodeCount];
            double[] normalizedGroup2 = new double[_stencil.NodeCount];
            for (int nodeIndex = 0; nodeIndex < _stencil.NodeCount; nodeIndex++)
            {
                normalizedGroup1[nodeIndex] = group1Flux[nodeIndex] * normalizationScale;
                normalizedGroup2[nodeIndex] = group2Flux[nodeIndex] * normalizationScale;
                if (!IsValidFlux(normalizedGroup1[nodeIndex]) ||
                    !IsValidFlux(normalizedGroup2[nodeIndex]))
                {
                    return ContractValidationResult<SpatialEigenIterationState>.Invalid(
                        "SpatialEigenIteration.InitialNormalization.Invalid",
                        ContractValidation.NodePath(_stencil.Nodes[nodeIndex].Node, ".initial_flux"),
                        "Initial normalization must produce finite componentwise nonnegative flux.");
                }
            }

            double normalizedProduction = ComputeFissionProduction(
                normalizedGroup1,
                normalizedGroup2,
                _normalizedFissionSource);
            double normalizedPower = ComputePower(normalizedGroup1, normalizedGroup2);
            if (!ContractValidation.IsFinite(normalizedProduction) ||
                normalizedProduction <= 0 ||
                !ContractValidation.IsFinite(normalizedPower) ||
                normalizedPower <= 0)
            {
                return ContractValidationResult<SpatialEigenIterationState>.Invalid(
                    "SpatialEigenIteration.InitialState.Invalid",
                    "initial_state",
                    "The initial normalized state must have strictly positive finite production and power.");
            }

            return ContractValidationResult<SpatialEigenIterationState>.Valid(
                new SpatialEigenIterationState(
                    this,
                    0,
                    initialEigenvalue,
                    normalizationScale,
                    normalizedPower,
                    normalizedProduction,
                    normalizedGroup1,
                    normalizedGroup2));
        }

        private bool TrySolve(
            SpatialEnergyGroup group,
            double[] source,
            double[] diagonal,
            double[] solution,
            double[] candidate,
            double[] applied,
            out ContractDiagnostic diagnostic)
        {
            for (int nodeIndex = 0; nodeIndex < source.Length; nodeIndex++)
            {
                if (!ContractValidation.IsFinite(source[nodeIndex]) || source[nodeIndex] < 0)
                {
                    diagnostic = new ContractDiagnostic(
                        "SpatialEigenIteration.InnerSource.Invalid",
                        ContractValidation.NodePath(_stencil.Nodes[nodeIndex].Node, ".inner_source"),
                        "An inner source must be finite and componentwise nonnegative.");
                    return false;
                }
            }

            Array.Clear(solution, 0, solution.Length);
            for (int innerIteration = 0;
                 innerIteration < _linearSolvePolicy.MaximumInnerIterations;
                 innerIteration++)
            {
                if (!_operator.TryApply(group, solution, applied, out ContractDiagnostic operatorDiagnostic))
                {
                    diagnostic = operatorDiagnostic;
                    return false;
                }

                for (int nodeIndex = 0; nodeIndex < source.Length; nodeIndex++)
                {
                    double correction = (source[nodeIndex] - applied[nodeIndex]) / diagonal[nodeIndex];
                    double next = solution[nodeIndex] + correction;
                    if (!ContractValidation.IsFinite(correction) ||
                        !ContractValidation.IsFinite(next))
                    {
                        diagnostic = new ContractDiagnostic(
                            "SpatialEigenIteration.InnerFlux.NonFinite",
                            ContractValidation.NodePath(_stencil.Nodes[nodeIndex].Node, ".inner_flux"),
                            "The deterministic Jacobi update produced a non-finite flux.");
                        return false;
                    }

                    if (next < 0)
                    {
                        diagnostic = new ContractDiagnostic(
                            "SpatialEigenIteration.InnerFlux.Negative",
                            ContractValidation.NodePath(_stencil.Nodes[nodeIndex].Node, ".inner_flux"),
                            "The deterministic Jacobi update produced a negative flux.");
                        return false;
                    }

                    candidate[nodeIndex] = next;
                }

                if (!_operator.TryApply(group, candidate, applied, out operatorDiagnostic))
                {
                    diagnostic = operatorDiagnostic;
                    return false;
                }

                double absoluteResidual = 0.0;
                double scale = 0.0;
                for (int nodeIndex = 0; nodeIndex < source.Length; nodeIndex++)
                {
                    double difference = applied[nodeIndex] - source[nodeIndex];
                    double absoluteDifference = Math.Abs(difference);
                    double rowScale = Math.Abs(applied[nodeIndex]) + Math.Abs(source[nodeIndex]);
                    if (!ContractValidation.IsFinite(absoluteDifference) ||
                        !ContractValidation.IsFinite(rowScale))
                    {
                        diagnostic = new ContractDiagnostic(
                            "SpatialEigenIteration.InnerResidual.NonFinite",
                            ContractValidation.NodePath(_stencil.Nodes[nodeIndex].Node, ".inner_residual"),
                            "The deterministic inner residual became non-finite.");
                        return false;
                    }

                    absoluteResidual = Math.Max(absoluteResidual, absoluteDifference);
                    scale = Math.Max(scale, rowScale);
                }

                double relativeResidual = scale == 0.0
                    ? 0.0
                    : absoluteResidual / scale;
                if (!ContractValidation.IsFinite(relativeResidual))
                {
                    diagnostic = new ContractDiagnostic(
                        "SpatialEigenIteration.InnerResidual.NonFinite",
                        "linear_solve",
                        "The deterministic inner relative residual became non-finite.");
                    return false;
                }

                if (absoluteResidual <= _linearSolvePolicy.AbsoluteResidualTolerance ||
                    relativeResidual <= _linearSolvePolicy.RelativeResidualTolerance)
                {
                    Array.Copy(candidate, solution, solution.Length);
                    diagnostic = null!;
                    return true;
                }

                Array.Copy(candidate, solution, solution.Length);
            }

            diagnostic = new ContractDiagnostic(
                "SpatialEigenIteration.InnerSolve.Nonconverged",
                group == SpatialEnergyGroup.Group1
                    ? "linear_solve.group1"
                    : "linear_solve.group2",
                "The deterministic inner solve exhausted its caller-supplied iteration limit.");
            return false;
        }

        private double ComputeFissionProduction(
            IReadOnlyList<double> group1Flux,
            IReadOnlyList<double> group2Flux,
            double[] destination)
        {
            double production = 0.0;
            for (int nodeIndex = 0; nodeIndex < _stencil.NodeCount; nodeIndex++)
            {
                SpatialNodeCoefficients coefficients = _coefficients.Nodes[nodeIndex];
                double value =
                    coefficients.NuFissionGroup1PerM * group1Flux[nodeIndex] +
                    coefficients.NuFissionGroup2PerM * group2Flux[nodeIndex];
                if (!ContractValidation.IsFinite(value) || value < 0)
                {
                    return double.NaN;
                }

                destination[nodeIndex] = value;
                double volumeContribution = coefficients.VolumeM3 * value;
                production += volumeContribution;
                if (!ContractValidation.IsFinite(volumeContribution) ||
                    !ContractValidation.IsFinite(production))
                {
                    return double.NaN;
                }
            }

            return production;
        }

        private double ComputePower(
            double[] group1Flux,
            double[] group2Flux)
        {
            double power = 0.0;
            for (int nodeIndex = 0; nodeIndex < _stencil.NodeCount; nodeIndex++)
            {
                SpatialNodeCoefficients coefficients = _coefficients.Nodes[nodeIndex];
                double fissionRate =
                    coefficients.FissionGroup1PerM * group1Flux[nodeIndex] +
                    coefficients.FissionGroup2PerM * group2Flux[nodeIndex];
                double localPower = coefficients.VolumeM3 *
                                    coefficients.EnergyPerFissionJ *
                                    fissionRate;
                if (!ContractValidation.IsFinite(fissionRate) ||
                    fissionRate < 0 ||
                    !ContractValidation.IsFinite(localPower) ||
                    localPower < 0)
                {
                    return double.NaN;
                }

                power += localPower;
                if (!ContractValidation.IsFinite(power))
                {
                    return double.NaN;
                }
            }

            return power;
        }

        private static ContractValidationResult<double[]> BuildDiagonal(
            SpatialStencil stencil,
            SpatialCoefficientSet coefficients,
            SpatialEnergyGroup group)
        {
            double[] diagonal = new double[stencil.NodeCount];
            for (int nodeIndex = 0; nodeIndex < stencil.NodeCount; nodeIndex++)
            {
                SpatialNodeStencil node = stencil.Nodes[nodeIndex];
                SpatialNodeCoefficients nodeCoefficients = coefficients.Nodes[nodeIndex];
                double removal = group == SpatialEnergyGroup.Group1
                    ? nodeCoefficients.AbsorptionGroup1PerM +
                      nodeCoefficients.DownscatterGroup1To2PerM
                    : nodeCoefficients.AbsorptionGroup2PerM;
                double conductanceSum = 0.0;

                foreach (SpatialNeighborTerm neighbor in node.NeighborTerms)
                {
                    if (!coefficients.TryGetEdge(
                            new SpatialEdgeKey(node.Node, neighbor.TargetNode),
                            out SpatialConductancePair conductance))
                    {
                        return ContractValidationResult<double[]>.Invalid(
                            "SpatialEigenIteration.Diagonal.EdgeMissing",
                            ContractValidation.NodePath(node.Node, ".neighbors"),
                            "Every stencil neighbor requires a bound conductance.");
                    }

                    conductanceSum += group == SpatialEnergyGroup.Group1
                        ? conductance.Group1
                        : conductance.Group2;
                    if (!ContractValidation.IsFinite(conductanceSum))
                    {
                        return ContractValidationResult<double[]>.Invalid(
                            "SpatialEigenIteration.Diagonal.NonFinite",
                            ContractValidation.NodePath(node.Node, ".diagonal"),
                            "The diagonal conductance sum must be finite.");
                    }
                }

                foreach (SpatialBoundaryTerm boundary in node.BoundaryTerms)
                {
                    if (!coefficients.TryGetBoundary(
                            new SpatialBoundaryKey(node.Node, boundary.Face),
                            out SpatialConductancePair conductance))
                    {
                        return ContractValidationResult<double[]>.Invalid(
                            "SpatialEigenIteration.Diagonal.BoundaryMissing",
                            ContractValidation.NodePath(node.Node, ".boundary_faces"),
                            "Every stencil boundary face requires a bound conductance.");
                    }

                    conductanceSum += group == SpatialEnergyGroup.Group1
                        ? conductance.Group1
                        : conductance.Group2;
                    if (!ContractValidation.IsFinite(conductanceSum))
                    {
                        return ContractValidationResult<double[]>.Invalid(
                            "SpatialEigenIteration.Diagonal.NonFinite",
                            ContractValidation.NodePath(node.Node, ".diagonal"),
                            "The diagonal conductance sum must be finite.");
                    }
                }

                double diagonalValue = removal + conductanceSum / nodeCoefficients.VolumeM3;
                if (!ContractValidation.IsFinite(diagonalValue) || diagonalValue <= 0)
                {
                    return ContractValidationResult<double[]>.Invalid(
                        "SpatialEigenIteration.Diagonal.Invalid",
                        ContractValidation.NodePath(node.Node, ".diagonal"),
                        "Every source-iteration diagonal must be finite and strictly positive.");
                }

                diagonal[nodeIndex] = diagonalValue;
            }

            return ContractValidationResult<double[]>.Valid(diagonal);
        }

        private static bool IsValidFlux(double value)
        {
            return ContractValidation.IsFinite(value) && value >= 0;
        }

        private static double[] CreateUnitFlux(int count)
        {
            var values = new double[count];
            for (int index = 0; index < values.Length; index++)
            {
                values[index] = 1.0;
            }

            return values;
        }
    }
}
