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

        internal IqsSpatialCandidateV1(
            object owner,
            FullCoreDiffusionSolveResultV1 spatialSolve,
            ReadOnlyCollection<double> shapeGroup1,
            ReadOnlyCollection<double> shapeGroup2,
            ReadOnlyCollection<double> shapeNodePowerWatts,
            double shapePowerWatts,
            double constraint,
            double staticRelativeReactivity,
            AdjointWeightedReactivityResultV1 weightedReactivity)
        {
            Owner = owner;
            SpatialSolve = spatialSolve;
            _shapeGroup1 = shapeGroup1;
            _shapeGroup2 = shapeGroup2;
            _shapeNodePowerWatts = shapeNodePowerWatts;
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
