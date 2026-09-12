using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace ReactorSim.Core
{
    /// <summary>
    /// Stable identities for the deterministic, time-independent equilibrium
    /// core solve used by the practice game. This contract deliberately has
    /// no kinetics, precursor, or poison data-pack dependency.
    /// </summary>
    public static class EquilibriumCoreSolverIdentityV1
    {
        public const uint CurrentSchemaVersion = 1;
        public const string ModelId = "candu6-two-group-equilibrium-core-v1";
        public const string SolverId = "full-core-diffusion-equilibrium-v1";
        public const string FormulationId = "two-group-static-equilibrium-core-v1";
        public const string ShapeMethodId = "deterministic-static-k-eigenmode-v1";
        public const string AmplitudeMethodId = "normalized-target-power-scale-v1";
        public const string ReactivityMethodId = "static-effective-k-rho-v1";
        public const string ReactivityIdentity = "static-rho-from-effective-k-v1";
    }

    /// <summary>
    /// Immutable projection of one unpoisoned full-core equilibrium solve.
    /// Node and bundle arrays use the model's canonical channel-major order;
    /// channel power is the corresponding channel reduction.
    /// </summary>
    public sealed class EquilibriumCoreProjectionV1
    {
        private readonly ReadOnlyCollection<double> _shapeGroup1;
        private readonly ReadOnlyCollection<double> _shapeGroup2;
        private readonly ReadOnlyCollection<double> _shapeNodePowerWatts;
        private readonly ReadOnlyCollection<double> _shapeChannelPowerWatts;
        private readonly ReadOnlyCollection<double> _shapeBundlePowerWatts;
        private readonly string _reactivityIdentity =
            EquilibriumCoreSolverIdentityV1.ReactivityIdentity;

        internal EquilibriumCoreProjectionV1(
            EquilibriumCoreSolverV1 owner,
            FullCoreDiffusionSolveResultV1 spatialSolve,
            double targetPowerWatts,
            ReadOnlyCollection<double> shapeGroup1,
            ReadOnlyCollection<double> shapeGroup2,
            ReadOnlyCollection<double> shapeNodePowerWatts,
            ReadOnlyCollection<double> shapeChannelPowerWatts,
            ReadOnlyCollection<double> shapeBundlePowerWatts,
            IqsSpatialCandidateV1 legacyPresentationProjection)
        {
            Owner = owner;
            SpatialSolve = spatialSolve;
            TargetPowerWatts = targetPowerWatts;
            _shapeGroup1 = shapeGroup1;
            _shapeGroup2 = shapeGroup2;
            _shapeNodePowerWatts = shapeNodePowerWatts;
            _shapeChannelPowerWatts = shapeChannelPowerWatts;
            _shapeBundlePowerWatts = shapeBundlePowerWatts;
            LegacyPresentationProjection = legacyPresentationProjection;
        }

        internal EquilibriumCoreSolverV1 Owner { get; }

        public FullCoreDiffusionSolveResultV1 SpatialSolve { get; }

        public FullCoreDiffusionDataPackV1 DataPack
        {
            get { return SpatialSolve.DataPack; }
        }

        public double TargetPowerWatts { get; }

        public double ShapePowerWatts
        {
            get { return SpatialSolve.TotalPowerWatts; }
        }

        public IReadOnlyList<double> ShapeGroup1
        {
            get { return _shapeGroup1; }
        }

        public IReadOnlyList<double> ShapeGroup2
        {
            get { return _shapeGroup2; }
        }

        public IReadOnlyList<double> ShapeNodePowerWatts
        {
            get { return _shapeNodePowerWatts; }
        }

        public IReadOnlyList<double> NodePowerWatts
        {
            get { return _shapeNodePowerWatts; }
        }

        public IReadOnlyList<double> ShapeChannelPowerWatts
        {
            get { return _shapeChannelPowerWatts; }
        }

        public IReadOnlyList<double> ChannelPowerWatts
        {
            get { return _shapeChannelPowerWatts; }
        }

        public IReadOnlyList<double> ShapeBundlePowerWatts
        {
            get { return _shapeBundlePowerWatts; }
        }

        public IReadOnlyList<double> BundlePowerWatts
        {
            get { return _shapeBundlePowerWatts; }
        }

        public double EffectiveK
        {
            get { return SpatialSolve.EffectiveK; }
        }

        public double RelativeReactivity
        {
            get { return SpatialSolve.Reactivity; }
        }

        public double Reactivity
        {
            get { return RelativeReactivity; }
        }

        public double StaticRelativeReactivity
        {
            get { return RelativeReactivity; }
        }

        public double WeightedPerturbationReactivity
        {
            get { return RelativeReactivity; }
        }

        public double ReactivityNumerator
        {
            get { return EffectiveK - 1.0; }
        }

        public double ReactivityDenominator
        {
            get { return EffectiveK; }
        }

        public string ReactivityIdentity
        {
            get { return _reactivityIdentity; }
        }

        public Digest32 ReactivityBindingDigest
        {
            get { return SpatialSolve.CoefficientBindingDigest; }
        }

        public string ReactivityBindingDigestHex
        {
            get { return ToHex(ReactivityBindingDigest); }
        }

        public double PowerBalanceRelativeError
        {
            get { return SpatialSolve.PowerBalanceRelativeError; }
        }

        /// <summary>
        /// The explicit static absorption overlay accepted by this
        /// projection, when present. It is a generic static composition
        /// input and is not a xenon or transient state.
        /// </summary>
        public StaticAbsorptionOverlayV1? StaticAbsorptionOverlay
        {
            get { return SpatialSolve.StaticAbsorptionOverlay; }
        }

        public bool HasStaticAbsorptionOverlay
        {
            get { return StaticAbsorptionOverlay != null; }
        }

        public string SolverIdentity
        {
            get
            {
                return EquilibriumCoreSolverIdentityV1.SolverId + "/" +
                       DataPack.Descriptor.DataPackVersion + "+" +
                       SpatialSolve.SolverIdentity;
            }
        }

        public int SolverIterationCount
        {
            get { return SpatialSolve.SpatialSolve.Diagnostics.IterationCount; }
        }

        public double SolverResidualRelativeInfinity
        {
            get { return SpatialSolve.SpatialSolve.Diagnostics.ResidualRelativeInfinity ?? 0.0; }
        }

        /// <summary>
        /// Compatibility-only carrier for the pre-existing Game/browser
        /// projection surface. It contains this same static solve and is not
        /// an owner of kinetics or xenon state.
        /// </summary>
        public IqsSpatialCandidateV1 LegacyPresentationProjection { get; }

        private static string ToHex(Digest32 digest)
        {
            var builder = new StringBuilder(digest.Bytes.Count * 2);
            foreach (byte value in digest.Bytes)
            {
                builder.Append(value.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }
    }

    internal sealed class EquilibriumCorePreparedCandidatesV1
    {
        internal EquilibriumCorePreparedCandidatesV1(
            EquilibriumCoreSolverV1 owner,
            FullCoreDiffusionPreparedSolveV1 spatialPreparedSolve)
        {
            Owner = owner;
            SpatialPreparedSolve = spatialPreparedSolve;
        }

        internal EquilibriumCoreSolverV1 Owner { get; }

        internal FullCoreDiffusionPreparedSolveV1 SpatialPreparedSolve { get; }
    }

    /// <summary>
    /// Deterministic full-core equilibrium solver facade. Solving a candidate
    /// is side-effect free; only TryCommitCandidate changes the current
    /// projection. The candidate solve uses the last accepted flux/eigenvalue
    /// as a warm start but always rebuilds the unpoisoned coefficient set from
    /// the supplied bundle inventory.
    /// </summary>
    public sealed class EquilibriumCoreSolverV1
    {
        private readonly FullCoreDiffusionModelV1 _spatialModel;
        private readonly double _targetPowerWatts;
        private EquilibriumCoreProjectionV1 _current;

        private EquilibriumCoreSolverV1(
            FullCoreDiffusionModelV1 spatialModel,
            double targetPowerWatts,
            FullCoreDiffusionSolveResultV1 initialSpatialSolve)
        {
            _spatialModel = spatialModel;
            _targetPowerWatts = targetPowerWatts;
            _current = BuildProjection(initialSpatialSolve);
        }

        public FullCoreDiffusionModelV1 SpatialModel
        {
            get { return _spatialModel; }
        }

        public FullCoreDiffusionDataPackV1 DataPack
        {
            get { return _spatialModel.DataPack; }
        }

        public double TargetPowerWatts
        {
            get { return _targetPowerWatts; }
        }

        public EquilibriumCoreProjectionV1 CurrentProjection
        {
            get { return _current; }
        }

        public FullCoreDiffusionSolveResultV1 CurrentSpatialSolve
        {
            get { return _current.SpatialSolve; }
        }

        public double EffectiveK
        {
            get { return _current.EffectiveK; }
        }

        public double RelativeReactivity
        {
            get { return _current.RelativeReactivity; }
        }

        public static ContractValidationResult<EquilibriumCoreSolverV1> TryCreate(
            FullCoreDiffusionModelV1 spatialModel,
            IEnumerable<BundleState> bundles,
            double targetPowerWatts)
        {
            if (spatialModel == null)
            {
                return Invalid(
                    "EquilibriumCoreSolver.SpatialModel.Missing",
                    "spatial_model",
                    "An equilibrium solver requires a validated full-core spatial model.");
            }

            if (bundles == null)
            {
                return Invalid(
                    "EquilibriumCoreSolver.Bundles.Missing",
                    "bundles",
                    "An equilibrium solver requires a full-core bundle inventory.");
            }

            if (!ContractValidation.IsFinite(targetPowerWatts) || targetPowerWatts <= 0.0)
            {
                return Invalid(
                    "EquilibriumCoreSolver.TargetPower.Invalid",
                    "target_power_w",
                    "The equilibrium target power must be finite and strictly positive SI watts.");
            }

            ContractValidationResult<FullCoreDiffusionSolveResultV1> spatial =
                spatialModel.TrySolve(bundles.ToArray(), targetPowerWatts);
            if (!spatial.IsValid)
            {
                return Invalid(
                    spatial.FirstDiagnostic.Code,
                    spatial.FirstDiagnostic.Path,
                    spatial.FirstDiagnostic.Message);
            }

            try
            {
                return ContractValidationResult<EquilibriumCoreSolverV1>.Valid(
                    new EquilibriumCoreSolverV1(
                        spatialModel,
                        targetPowerWatts,
                        spatial.Value));
            }
            catch (InvalidOperationException exception)
            {
                return Invalid(
                    "EquilibriumCoreSolver.Initialization.Invalid",
                    "initialization",
                    exception.Message);
            }
        }

        public ContractValidationResult<EquilibriumCoreProjectionV1> TrySolveCandidate(
            IEnumerable<BundleState> bundles)
        {
            return TrySolveCandidate(bundles, _current.SpatialSolve);
        }

        internal ContractValidationResult<EquilibriumCorePreparedCandidatesV1>
            TryPrepareCandidates(IEnumerable<BundleState> bundles)
        {
            ContractValidationResult<FullCoreDiffusionPreparedSolveV1> prepared =
                _spatialModel.TryPrepareSolve(bundles);
            if (!prepared.IsValid)
            {
                return ContractValidationResult<EquilibriumCorePreparedCandidatesV1>.Invalid(
                    prepared.FirstDiagnostic.Code,
                    prepared.FirstDiagnostic.Path,
                    prepared.FirstDiagnostic.Message);
            }

            return ContractValidationResult<EquilibriumCorePreparedCandidatesV1>.Valid(
                new EquilibriumCorePreparedCandidatesV1(this, prepared.Value));
        }

        internal ContractValidationResult<EquilibriumCoreProjectionV1> TrySolveCandidate(
            EquilibriumCorePreparedCandidatesV1 prepared,
            FullCoreDiffusionSolveResultV1 initialSpatialSolve,
            StaticAbsorptionOverlayV1? staticAbsorptionOverlay = null)
        {
            if (prepared == null || !ReferenceEquals(prepared.Owner, this))
            {
                return InvalidCandidate(
                    "EquilibriumCoreSolver.Prepared.OwnerMismatch",
                    "prepared_candidates",
                    "Prepared equilibrium candidates may only be used by their owning solver.");
            }

            if (initialSpatialSolve == null)
            {
                return InvalidCandidate(
                    "EquilibriumCoreSolver.InitialSpatialSolve.Missing",
                    "initial_spatial_solve",
                    "An equilibrium candidate requires an explicit accepted warm-start solve.");
            }

            if (!ReferenceEquals(initialSpatialSolve.DataPack, _spatialModel.DataPack))
            {
                return InvalidCandidate(
                    "EquilibriumCoreSolver.InitialSpatialSolve.DataPackMismatch",
                    "initial_spatial_solve.data_pack",
                    "An equilibrium candidate warm start must use this solver's exact diffusion data pack.");
            }

            ContractValidationResult<FullCoreDiffusionSolveResultV1> spatial =
                _spatialModel.TrySolvePrepared(
                    prepared.SpatialPreparedSolve,
                    _targetPowerWatts,
                    initialSpatialSolve.EffectiveK,
                    initialSpatialSolve.Group1Flux,
                    initialSpatialSolve.Group2Flux,
                    staticAbsorptionOverlay);
            if (!spatial.IsValid)
            {
                return InvalidCandidate(
                    spatial.FirstDiagnostic.Code,
                    spatial.FirstDiagnostic.Path,
                    spatial.FirstDiagnostic.Message);
            }

            try
            {
                return ContractValidationResult<EquilibriumCoreProjectionV1>.Valid(
                    BuildProjection(spatial.Value));
            }
            catch (InvalidOperationException exception)
            {
                return InvalidCandidate(
                    "EquilibriumCoreSolver.Candidate.Invalid",
                    "candidate",
                    exception.Message);
            }
        }

        public ContractValidationResult<EquilibriumCoreProjectionV1> TrySolveCandidate(
            IEnumerable<BundleState> bundles,
            StaticAbsorptionOverlayV1 staticAbsorptionOverlay)
        {
            return TrySolveCandidate(
                bundles,
                staticAbsorptionOverlay,
                _current.SpatialSolve);
        }

        public ContractValidationResult<EquilibriumCoreProjectionV1> TrySolveCandidate(
            IEnumerable<BundleState> bundles,
            StaticAbsorptionOverlayV1 staticAbsorptionOverlay,
            FullCoreDiffusionSolveResultV1 initialSpatialSolve)
        {
            if (bundles == null)
            {
                return InvalidCandidate(
                    "EquilibriumCoreSolver.Bundles.Missing",
                    "bundles",
                    "An equilibrium candidate requires a full-core bundle inventory.");
            }

            if (staticAbsorptionOverlay == null)
            {
                return InvalidCandidate(
                    "EquilibriumCoreSolver.StaticAbsorptionOverlay.Missing",
                    "static_absorption_overlay",
                    "A static-overlay equilibrium candidate requires an explicit overlay.");
            }

            if (initialSpatialSolve == null)
            {
                return InvalidCandidate(
                    "EquilibriumCoreSolver.InitialSpatialSolve.Missing",
                    "initial_spatial_solve",
                    "An equilibrium candidate requires an explicit accepted warm-start solve.");
            }

            if (!ReferenceEquals(initialSpatialSolve.DataPack, _spatialModel.DataPack))
            {
                return InvalidCandidate(
                    "EquilibriumCoreSolver.InitialSpatialSolve.DataPackMismatch",
                    "initial_spatial_solve.data_pack",
                    "An equilibrium candidate warm start must use this solver's exact diffusion data pack.");
            }

            ContractValidationResult<FullCoreDiffusionSolveResultV1> spatial =
                _spatialModel.TrySolve(
                    bundles.ToArray(),
                    staticAbsorptionOverlay,
                    _targetPowerWatts,
                    initialSpatialSolve.EffectiveK,
                    initialSpatialSolve.Group1Flux,
                    initialSpatialSolve.Group2Flux);
            if (!spatial.IsValid)
            {
                return InvalidCandidate(
                    spatial.FirstDiagnostic.Code,
                    spatial.FirstDiagnostic.Path,
                    spatial.FirstDiagnostic.Message);
            }

            try
            {
                return ContractValidationResult<EquilibriumCoreProjectionV1>.Valid(
                    BuildProjection(spatial.Value));
            }
            catch (InvalidOperationException exception)
            {
                return InvalidCandidate(
                    "EquilibriumCoreSolver.Candidate.Invalid",
                    "candidate",
                    exception.Message);
            }
        }

        public ContractValidationResult<EquilibriumCoreProjectionV1> TrySolveCandidate(
            IEnumerable<BundleState> bundles,
            FullCoreDiffusionSolveResultV1 initialSpatialSolve)
        {
            if (bundles == null)
            {
                return InvalidCandidate(
                    "EquilibriumCoreSolver.Bundles.Missing",
                    "bundles",
                    "An equilibrium candidate requires a full-core bundle inventory.");
            }

            if (initialSpatialSolve == null)
            {
                return InvalidCandidate(
                    "EquilibriumCoreSolver.InitialSpatialSolve.Missing",
                    "initial_spatial_solve",
                    "An equilibrium candidate requires an explicit accepted warm-start solve.");
            }

            if (!ReferenceEquals(initialSpatialSolve.DataPack, _spatialModel.DataPack))
            {
                return InvalidCandidate(
                    "EquilibriumCoreSolver.InitialSpatialSolve.DataPackMismatch",
                    "initial_spatial_solve.data_pack",
                    "An equilibrium candidate warm start must use this solver's exact diffusion data pack.");
            }

            ContractValidationResult<FullCoreDiffusionSolveResultV1> spatial =
                _spatialModel.TrySolve(
                    bundles.ToArray(),
                    _targetPowerWatts,
                    initialSpatialSolve.EffectiveK,
                    initialSpatialSolve.Group1Flux,
                    initialSpatialSolve.Group2Flux);
            if (!spatial.IsValid)
            {
                return InvalidCandidate(
                    spatial.FirstDiagnostic.Code,
                    spatial.FirstDiagnostic.Path,
                    spatial.FirstDiagnostic.Message);
            }

            try
            {
                return ContractValidationResult<EquilibriumCoreProjectionV1>.Valid(
                    BuildProjection(spatial.Value));
            }
            catch (InvalidOperationException exception)
            {
                return InvalidCandidate(
                    "EquilibriumCoreSolver.Candidate.Invalid",
                    "candidate",
                    exception.Message);
            }
        }

        public ContractValidationResult<bool> TryCommitCandidate(
            EquilibriumCoreProjectionV1 candidate)
        {
            if (candidate == null)
            {
                return ContractValidationResult<bool>.Invalid(
                    "EquilibriumCoreSolver.Candidate.Missing",
                    "candidate",
                    "An equilibrium projection candidate is required.");
            }

            if (!ReferenceEquals(candidate.Owner, this))
            {
                return ContractValidationResult<bool>.Invalid(
                    "EquilibriumCoreSolver.Candidate.OwnerMismatch",
                    "candidate",
                    "An equilibrium projection may only be committed by its owning solver.");
            }

            _current = candidate;
            return ContractValidationResult<bool>.Valid(true);
        }

        private EquilibriumCoreProjectionV1 BuildProjection(
            FullCoreDiffusionSolveResultV1 spatial)
        {
            if (spatial == null || spatial.NodePowerWatts.Count != _spatialModel.NodeCount)
            {
                throw new InvalidOperationException(
                    "An equilibrium projection requires one power value per full-core node.");
            }

            var channelPowerWatts = new double[_spatialModel.Topology.ChannelCount];
            for (int nodeIndex = 0; nodeIndex < _spatialModel.Stencil.Nodes.Count; nodeIndex++)
            {
                SpatialNodeStencil node = _spatialModel.Stencil.Nodes[nodeIndex];
                channelPowerWatts[(int)node.Node.ChannelId.Value] +=
                    spatial.NodePowerWatts[nodeIndex];
            }

            AdjointWeightedReactivityResultV1 compatibilityReactivity =
                new AdjointWeightedReactivityResultV1(
                    spatial.EffectiveK - 1.0,
                    spatial.EffectiveK,
                    spatial.Reactivity,
                    spatial.CoefficientBindingDigest,
                    spatial.InventoryBindingDigest,
                    spatial.CoefficientBindingDigest);
            IqsSpatialCandidateV1 legacyProjection =
                new IqsSpatialCandidateV1(
                    this,
                    spatial,
                    spatial.Group1FluxStorage,
                    spatial.Group2FluxStorage,
                    spatial.NodePowerWattsStorage,
                    spatial.TotalPowerWatts,
                    1.0,
                    spatial.Reactivity,
                    compatibilityReactivity);

            return new EquilibriumCoreProjectionV1(
                this,
                spatial,
                _targetPowerWatts,
                spatial.Group1FluxStorage,
                spatial.Group2FluxStorage,
                spatial.NodePowerWattsStorage,
                new ReadOnlyCollection<double>(channelPowerWatts),
                spatial.NodePowerWattsStorage,
                legacyProjection);
        }

        private static ContractValidationResult<EquilibriumCoreSolverV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<EquilibriumCoreSolverV1>.Invalid(
                code,
                path,
                message);
        }

        private static ContractValidationResult<EquilibriumCoreProjectionV1> InvalidCandidate(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<EquilibriumCoreProjectionV1>.Invalid(
                code,
                path,
                message);
        }
    }
}
