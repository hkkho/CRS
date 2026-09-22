using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ReactorSim.Core
{
    public enum SpatialEnergyGroup : byte
    {
        Group1 = 1,
        Group2 = 2
    }

    /// <summary>
    /// Optional direct flux-to-power response for a node.  The reference
    /// lattice table supplies H factors, but does not supply Sigma_f.  The
    /// factors are stored in SI watts per (neutron m^-2 s^-1); unlike the
    /// Sigma_f/E_fission path, they already represent the node power response
    /// and are therefore not multiplied by node volume.
    /// </summary>
    public sealed class SpatialFluxPowerResponseV1
    {
        private SpatialFluxPowerResponseV1(
            double group1WattsPerFluxDensity,
            double group2WattsPerFluxDensity)
        {
            Group1WattsPerFluxDensity = group1WattsPerFluxDensity;
            Group2WattsPerFluxDensity = group2WattsPerFluxDensity;
        }

        public double Group1WattsPerFluxDensity { get; }

        public double Group2WattsPerFluxDensity { get; }

        public static ContractValidationResult<SpatialFluxPowerResponseV1> TryCreate(
            double group1WattsPerFluxDensity,
            double group2WattsPerFluxDensity)
        {
            if (!ContractValidation.IsFinite(group1WattsPerFluxDensity) ||
                !ContractValidation.IsFinite(group2WattsPerFluxDensity) ||
                group1WattsPerFluxDensity < 0.0 ||
                group2WattsPerFluxDensity < 0.0)
            {
                return ContractValidationResult<SpatialFluxPowerResponseV1>.Invalid(
                    "SpatialFluxPowerResponse.NonFiniteOrNegative",
                    "power_response",
                    "Direct flux-to-power factors must be finite and nonnegative SI values.");
            }

            if (group1WattsPerFluxDensity <= 0.0 &&
                group2WattsPerFluxDensity <= 0.0)
            {
                return ContractValidationResult<SpatialFluxPowerResponseV1>.Invalid(
                    "SpatialFluxPowerResponse.Zero",
                    "power_response",
                    "A direct flux-to-power response must have positive support in at least one group.");
            }

            return ContractValidationResult<SpatialFluxPowerResponseV1>.Valid(
                new SpatialFluxPowerResponseV1(
                    group1WattsPerFluxDensity,
                    group2WattsPerFluxDensity));
        }

        internal ContractDiagnostic? Validate(string path)
        {
            if (!ContractValidation.IsFinite(Group1WattsPerFluxDensity) ||
                !ContractValidation.IsFinite(Group2WattsPerFluxDensity) ||
                Group1WattsPerFluxDensity < 0.0 ||
                Group2WattsPerFluxDensity < 0.0)
            {
                return new ContractDiagnostic(
                    "SpatialFluxPowerResponse.NonFiniteOrNegative",
                    path,
                    "Direct flux-to-power factors must be finite and nonnegative SI values.");
            }

            if (Group1WattsPerFluxDensity <= 0.0 &&
                Group2WattsPerFluxDensity <= 0.0)
            {
                return new ContractDiagnostic(
                    "SpatialFluxPowerResponse.Zero",
                    path,
                    "A direct flux-to-power response must have positive support in at least one group.");
            }

            return null;
        }
    }

    /// <summary>
    /// In-memory node coefficients for the two-group static diffusion model.
    /// Loading, versioning, hashing, and serialization are handled by the
    /// full-core data-pack adapter at the composition boundary.
    /// </summary>
    public sealed class SpatialNodeCoefficients
    {
        public SpatialNodeCoefficients(
            NodeKey node,
            double volumeM3,
            double absorptionGroup1PerM,
            double absorptionGroup2PerM,
            double downscatterGroup1To2PerM,
            double fissionGroup1PerM,
            double fissionGroup2PerM,
            double nuFissionGroup1PerM,
            double nuFissionGroup2PerM,
            double chiGroup1,
            double chiGroup2,
            double energyPerFissionJ)
            : this(
                node,
                volumeM3,
                absorptionGroup1PerM,
                absorptionGroup2PerM,
                downscatterGroup1To2PerM,
                fissionGroup1PerM,
                fissionGroup2PerM,
                nuFissionGroup1PerM,
                nuFissionGroup2PerM,
                chiGroup1,
                chiGroup2,
                energyPerFissionJ,
                null,
                true)
        {
        }

        public SpatialNodeCoefficients(
            NodeKey node,
            double volumeM3,
            double absorptionGroup1PerM,
            double absorptionGroup2PerM,
            double downscatterGroup1To2PerM,
            double fissionGroup1PerM,
            double fissionGroup2PerM,
            double nuFissionGroup1PerM,
            double nuFissionGroup2PerM,
            double chiGroup1,
            double chiGroup2,
            double energyPerFissionJ,
            SpatialFluxPowerResponseV1? powerResponse)
            : this(
                node,
                volumeM3,
                absorptionGroup1PerM,
                absorptionGroup2PerM,
                downscatterGroup1To2PerM,
                fissionGroup1PerM,
                fissionGroup2PerM,
                nuFissionGroup1PerM,
                nuFissionGroup2PerM,
                chiGroup1,
                chiGroup2,
                energyPerFissionJ,
                powerResponse,
                powerResponse == null)
        {
        }

        public SpatialNodeCoefficients(
            NodeKey node,
            double volumeM3,
            double absorptionGroup1PerM,
            double absorptionGroup2PerM,
            double downscatterGroup1To2PerM,
            double fissionGroup1PerM,
            double fissionGroup2PerM,
            double nuFissionGroup1PerM,
            double nuFissionGroup2PerM,
            double chiGroup1,
            double chiGroup2,
            double energyPerFissionJ,
            SpatialFluxPowerResponseV1? powerResponse,
            bool hasFissionCrossSections)
        {
            Node = node;
            VolumeM3 = volumeM3;
            AbsorptionGroup1PerM = absorptionGroup1PerM;
            AbsorptionGroup2PerM = absorptionGroup2PerM;
            DownscatterGroup1To2PerM = downscatterGroup1To2PerM;
            FissionGroup1PerM = fissionGroup1PerM;
            FissionGroup2PerM = fissionGroup2PerM;
            NuFissionGroup1PerM = nuFissionGroup1PerM;
            NuFissionGroup2PerM = nuFissionGroup2PerM;
            ChiGroup1 = chiGroup1;
            ChiGroup2 = chiGroup2;
            EnergyPerFissionJ = energyPerFissionJ;
            PowerResponse = powerResponse;
            HasFissionCrossSections = hasFissionCrossSections;
        }

        public NodeKey Node { get; }

        public double VolumeM3 { get; }

        public double AbsorptionGroup1PerM { get; }

        public double AbsorptionGroup2PerM { get; }

        public double DownscatterGroup1To2PerM { get; }

        public double FissionGroup1PerM { get; }

        public double FissionGroup2PerM { get; }

        public double NuFissionGroup1PerM { get; }

        public double NuFissionGroup2PerM { get; }

        public double ChiGroup1 { get; }

        public double ChiGroup2 { get; }

        public double EnergyPerFissionJ { get; }

        /// <summary>
        /// When present, this is the explicit power path for a row that does
        /// not provide Sigma_f.  It is mutually exclusive with the legacy
        /// Sigma_f times energy-per-fission path.
        /// </summary>
        public SpatialFluxPowerResponseV1? PowerResponse { get; }

        /// <summary>
        /// True for legacy rows that provide Sigma_f explicitly. A reference
        /// lattice row may instead provide nuSigma_f and H factors while
        /// leaving Sigma_f absent.
        /// </summary>
        public bool HasFissionCrossSections { get; }
    }

    /// <summary>
    /// One shared conductance for a canonical reciprocal interior edge.
    /// </summary>
    public sealed class SpatialEdgeConductance
    {
        public SpatialEdgeConductance(
            NodeKey endpointA,
            NodeKey endpointB,
            double group1M2,
            double group2M2)
        {
            EndpointA = endpointA;
            EndpointB = endpointB;
            Group1M2 = group1M2;
            Group2M2 = group2M2;
        }

        public NodeKey EndpointA { get; }

        public NodeKey EndpointB { get; }

        public double Group1M2 { get; }

        public double Group2M2 { get; }
    }

    /// <summary>
    /// Conductances for one explicit topology boundary face.
    /// </summary>
    public sealed class SpatialBoundaryConductance
    {
        public SpatialBoundaryConductance(
            NodeKey node,
            TopologyFace face,
            double group1M2,
            double group2M2)
        {
            Node = node;
            Face = face;
            Group1M2 = group1M2;
            Group2M2 = group2M2;
        }

        public NodeKey Node { get; }

        public TopologyFace Face { get; }

        public double Group1M2 { get; }

        public double Group2M2 { get; }
    }

    /// <summary>
    /// Validated immutable in-memory coefficients bound to one stencil.
    /// </summary>
    public sealed class SpatialCoefficientSet
    {
        private readonly ReadOnlyCollection<SpatialNodeCoefficients> _nodes;
        private readonly Dictionary<SpatialEdgeKey, SpatialConductancePair> _edges;
        private readonly Dictionary<SpatialBoundaryKey, SpatialConductancePair> _boundaries;
        private readonly SpatialStencil _stencil;
        private readonly XenonBasisV1 _xenonBasis;
        private readonly double _referenceXeNumberDensityM3;

        private SpatialCoefficientSet(
            SpatialStencil stencil,
            IReadOnlyList<SpatialNodeCoefficients> nodes,
            Dictionary<SpatialEdgeKey, SpatialConductancePair> edges,
            Dictionary<SpatialBoundaryKey, SpatialConductancePair> boundaries,
            XenonBasisV1 xenonBasis,
            double referenceXeNumberDensityM3)
        {
            _stencil = stencil;
            _nodes = new ReadOnlyCollection<SpatialNodeCoefficients>(nodes.ToArray());
            _edges = edges;
            _boundaries = boundaries;
            _xenonBasis = xenonBasis;
            _referenceXeNumberDensityM3 = referenceXeNumberDensityM3;
        }

        public int NodeCount
        {
            get { return _nodes.Count; }
        }

        public int EdgeCount
        {
            get { return _edges.Count; }
        }

        public int BoundaryCount
        {
            get { return _boundaries.Count; }
        }

        public IReadOnlyList<SpatialNodeCoefficients> Nodes
        {
            get { return _nodes; }
        }

        /// <summary>
        /// Explicit xenon basis marker carried with the coefficient identity.
        /// Existing callers that do not provide P7 metadata remain
        /// <see cref="XenonBasisV1.Unspecified"/> and cannot enter the
        /// dynamic-Xe coupling boundary.
        /// </summary>
        public XenonBasisV1 XenonBasis
        {
            get { return _xenonBasis; }
        }

        public double ReferenceXeNumberDensityM3
        {
            get { return _referenceXeNumberDensityM3; }
        }

        public static ContractValidationResult<SpatialCoefficientSet> TryCreate(
            SpatialStencil stencil,
            IEnumerable<SpatialNodeCoefficients> nodeCoefficients,
            IEnumerable<SpatialEdgeConductance> edgeConductances,
            IEnumerable<SpatialBoundaryConductance> boundaryConductances)
        {
            return TryCreateCore(
                stencil,
                nodeCoefficients,
                edgeConductances,
                boundaryConductances,
                XenonBasisV1.Unspecified,
                0.0);
        }

        public static ContractValidationResult<SpatialCoefficientSet> TryCreateWithXenonBasis(
            SpatialStencil stencil,
            IEnumerable<SpatialNodeCoefficients> nodeCoefficients,
            IEnumerable<SpatialEdgeConductance> edgeConductances,
            IEnumerable<SpatialBoundaryConductance> boundaryConductances,
            XenonBasisV1 xenonBasis,
            double referenceXeNumberDensityM3)
        {
            return TryCreateCore(
                stencil,
                nodeCoefficients,
                edgeConductances,
                boundaryConductances,
                xenonBasis,
                referenceXeNumberDensityM3);
        }

        private static ContractValidationResult<SpatialCoefficientSet> TryCreateCore(
            SpatialStencil stencil,
            IEnumerable<SpatialNodeCoefficients> nodeCoefficients,
            IEnumerable<SpatialEdgeConductance> edgeConductances,
            IEnumerable<SpatialBoundaryConductance> boundaryConductances,
            XenonBasisV1 xenonBasis,
            double referenceXeNumberDensityM3)
        {
            if (xenonBasis != XenonBasisV1.Excluded &&
                xenonBasis != XenonBasisV1.Included &&
                xenonBasis != XenonBasisV1.Equilibrium &&
                xenonBasis != XenonBasisV1.Unspecified)
            {
                return ContractValidationResult<SpatialCoefficientSet>.Invalid(
                    "SpatialCoefficients.XenonBasis.Invalid",
                    "xenon_basis",
                    "The coefficient set requires an explicit supported xenon basis marker.");
            }

            if (!ContractValidation.IsFinite(referenceXeNumberDensityM3) ||
                referenceXeNumberDensityM3 < 0.0 ||
                BitConverter.DoubleToInt64Bits(referenceXeNumberDensityM3) < 0)
            {
                return ContractValidationResult<SpatialCoefficientSet>.Invalid(
                    "SpatialCoefficients.ReferenceXe.Invalid",
                    "reference_xe_number_density_m3",
                    "Reference Xe number density must be finite, canonical, and nonnegative SI m^-3.");
            }

            if (stencil == null)
            {
                return ContractValidationResult<SpatialCoefficientSet>.Invalid(
                    "SpatialCoefficients.Stencil.Missing",
                    "stencil",
                    "Coefficient binding requires an assembled stencil.");
            }

            if (nodeCoefficients == null)
            {
                return ContractValidationResult<SpatialCoefficientSet>.Invalid(
                    "SpatialCoefficients.Nodes.Missing",
                    "node_coefficients",
                    "Node coefficients are required for every stencil node.");
            }

            if (edgeConductances == null)
            {
                return ContractValidationResult<SpatialCoefficientSet>.Invalid(
                    "SpatialCoefficients.Edges.Missing",
                    "edge_conductances",
                    "Interior edge conductances are required for every stencil edge.");
            }

            if (boundaryConductances == null)
            {
                return ContractValidationResult<SpatialCoefficientSet>.Invalid(
                    "SpatialCoefficients.Boundaries.Missing",
                    "boundary_conductances",
                    "Boundary conductances are required for every stencil boundary face.");
            }

            SpatialNodeCoefficients[] nodeRecords = nodeCoefficients.ToArray();
            SpatialEdgeConductance[] edgeRecords = edgeConductances.ToArray();
            SpatialBoundaryConductance[] boundaryRecords = boundaryConductances.ToArray();

            if (nodeRecords.Any(record => record == null))
            {
                return ContractValidationResult<SpatialCoefficientSet>.Invalid(
                    "SpatialCoefficients.Node.Null",
                    "node_coefficients",
                    "A node coefficient record may not be null.");
            }

            if (edgeRecords.Any(record => record == null))
            {
                return ContractValidationResult<SpatialCoefficientSet>.Invalid(
                    "SpatialCoefficients.Edge.Null",
                    "edge_conductances",
                    "An edge conductance record may not be null.");
            }

            if (boundaryRecords.Any(record => record == null))
            {
                return ContractValidationResult<SpatialCoefficientSet>.Invalid(
                    "SpatialCoefficients.Boundary.Null",
                    "boundary_conductances",
                    "A boundary conductance record may not be null.");
            }

            var nodeSet = new HashSet<NodeKey>(stencil.Nodes.Select(node => node.Node));
            var nodeMap = new Dictionary<NodeKey, SpatialNodeCoefficients>();
            foreach (SpatialNodeCoefficients record in nodeRecords.OrderBy(record => record.Node))
            {
                if (!nodeSet.Contains(record.Node))
                {
                    return ContractValidationResult<SpatialCoefficientSet>.Invalid(
                        "SpatialCoefficients.Node.Unknown",
                        "nodes[" + record.Node + "]",
                        "A node coefficient record must match a stencil node.");
                }

                if (nodeMap.ContainsKey(record.Node))
                {
                    return ContractValidationResult<SpatialCoefficientSet>.Invalid(
                        "SpatialCoefficients.Node.Duplicate",
                        ContractValidation.NodePath(record.Node, ".coefficients"),
                        "Exactly one node coefficient record is required.");
                }

                nodeMap.Add(record.Node, record);
            }

            var orderedNodes = new SpatialNodeCoefficients[stencil.NodeCount];
            for (int nodeIndex = 0; nodeIndex < stencil.NodeCount; nodeIndex++)
            {
                NodeKey node = stencil.Nodes[nodeIndex].Node;
                SpatialNodeCoefficients record;
                if (!nodeMap.TryGetValue(node, out record!))
                {
                    return ContractValidationResult<SpatialCoefficientSet>.Invalid(
                        "SpatialCoefficients.Node.Missing",
                        ContractValidation.NodePath(node, ".coefficients"),
                        "Exactly one node coefficient record is required.");
                }

                ContractDiagnostic? failure = ValidateNodeCoefficients(record);
                if (failure != null)
                {
                    return ContractValidationResult<SpatialCoefficientSet>.Invalid(
                        failure.Code,
                        failure.Path,
                        failure.Message);
                }

                orderedNodes[nodeIndex] = record;
            }

            var expectedEdges = new Dictionary<SpatialEdgeKey, int>();
            foreach (SpatialNodeStencil node in stencil.Nodes)
            {
                foreach (SpatialNeighborTerm neighbor in node.NeighborTerms)
                {
                    SpatialEdgeKey key = new SpatialEdgeKey(node.Node, neighbor.TargetNode);
                    int count;
                    expectedEdges.TryGetValue(key, out count);
                    expectedEdges[key] = count + 1;
                }
            }

            foreach (KeyValuePair<SpatialEdgeKey, int> expected in expectedEdges.OrderBy(pair => pair.Key))
            {
                if (expected.Value != 2)
                {
                    return ContractValidationResult<SpatialCoefficientSet>.Invalid(
                        "SpatialCoefficients.Edge.TopologyMismatch",
                        "edges[" + expected.Key + "]",
                        "Every interior conductance key must bind exactly one reciprocal pair.");
                }
            }

            var boundEdges = new Dictionary<SpatialEdgeKey, SpatialConductancePair>();
            foreach (SpatialEdgeConductance record in edgeRecords
                         .OrderBy(record => new SpatialEdgeKey(record.EndpointA, record.EndpointB)))
            {
                SpatialEdgeKey key = new SpatialEdgeKey(record.EndpointA, record.EndpointB);
                if (!nodeSet.Contains(key.First) || !nodeSet.Contains(key.Second))
                {
                    return ContractValidationResult<SpatialCoefficientSet>.Invalid(
                        "SpatialCoefficients.Edge.UnknownNode",
                        "edges[" + key + "]",
                        "An edge conductance endpoint must match a stencil node.");
                }

                if (key.First == key.Second)
                {
                    return ContractValidationResult<SpatialCoefficientSet>.Invalid(
                        "SpatialCoefficients.Edge.SelfReference",
                        "edges[" + key + "]",
                        "An interior conductance may not bind a node to itself.");
                }

                if (!expectedEdges.ContainsKey(key))
                {
                    return ContractValidationResult<SpatialCoefficientSet>.Invalid(
                        "SpatialCoefficients.Edge.Extra",
                        "edges[" + key + "]",
                        "Every conductance key must correspond to a validated reciprocal stencil edge.");
                }

                if (boundEdges.ContainsKey(key))
                {
                    return ContractValidationResult<SpatialCoefficientSet>.Invalid(
                        "SpatialCoefficients.Edge.Duplicate",
                        "edges[" + key + "]",
                        "Exactly one shared conductance record is required per reciprocal edge.");
                }

                ContractDiagnostic? failure = ValidatePositiveConductances(
                    record.Group1M2,
                    record.Group2M2,
                    "edges[" + key + "]");
                if (failure != null)
                {
                    return ContractValidationResult<SpatialCoefficientSet>.Invalid(
                        failure.Code,
                        failure.Path,
                        failure.Message);
                }

                boundEdges.Add(key, new SpatialConductancePair(record.Group1M2, record.Group2M2));
            }

            foreach (SpatialEdgeKey key in expectedEdges.Keys.OrderBy(value => value))
            {
                if (!boundEdges.ContainsKey(key))
                {
                    return ContractValidationResult<SpatialCoefficientSet>.Invalid(
                        "SpatialCoefficients.Edge.Missing",
                        "edges[" + key + "]",
                        "Exactly one shared conductance record is required per reciprocal edge.");
                }
            }

            var expectedBoundaries = new Dictionary<SpatialBoundaryKey, BoundaryClassification>();
            foreach (SpatialNodeStencil node in stencil.Nodes)
            {
                foreach (SpatialBoundaryTerm boundary in node.BoundaryTerms)
                {
                    expectedBoundaries.Add(
                        new SpatialBoundaryKey(node.Node, boundary.Face),
                        boundary.Classification);
                }
            }

            var boundBoundaries = new Dictionary<SpatialBoundaryKey, SpatialConductancePair>();
            foreach (SpatialBoundaryConductance record in boundaryRecords
                         .OrderBy(record => new SpatialBoundaryKey(record.Node, record.Face)))
            {
                SpatialBoundaryKey key = new SpatialBoundaryKey(record.Node, record.Face);
                BoundaryClassification classification;
                if (!expectedBoundaries.TryGetValue(key, out classification))
                {
                    return ContractValidationResult<SpatialCoefficientSet>.Invalid(
                        "SpatialCoefficients.Boundary.Extra",
                        "boundaries[" + key + "]",
                        "Every boundary conductance key must match a validated stencil face.");
                }

                if (boundBoundaries.ContainsKey(key))
                {
                    return ContractValidationResult<SpatialCoefficientSet>.Invalid(
                        "SpatialCoefficients.Boundary.Duplicate",
                        "boundaries[" + key + "]",
                        "Exactly one conductance record is required per boundary face.");
                }

                ContractDiagnostic? failure = ValidateBoundaryConductances(
                    classification,
                    record.Group1M2,
                    record.Group2M2,
                    "boundaries[" + key + "]");
                if (failure != null)
                {
                    return ContractValidationResult<SpatialCoefficientSet>.Invalid(
                        failure.Code,
                        failure.Path,
                        failure.Message);
                }

                boundBoundaries.Add(key, new SpatialConductancePair(record.Group1M2, record.Group2M2));
            }

            foreach (SpatialBoundaryKey key in expectedBoundaries.Keys.OrderBy(value => value))
            {
                if (!boundBoundaries.ContainsKey(key))
                {
                    return ContractValidationResult<SpatialCoefficientSet>.Invalid(
                        "SpatialCoefficients.Boundary.Missing",
                        "boundaries[" + key + "]",
                        "Exactly one conductance record is required per boundary face.");
                }
            }

            return ContractValidationResult<SpatialCoefficientSet>.Valid(
                new SpatialCoefficientSet(
                    stencil,
                    orderedNodes,
                    boundEdges,
                    boundBoundaries,
                    xenonBasis,
                    referenceXeNumberDensityM3));
        }

        internal SpatialStencil Stencil
        {
            get { return _stencil; }
        }

        internal bool TryGetEdge(
            SpatialEdgeKey key,
            out SpatialConductancePair conductance)
        {
            return _edges.TryGetValue(key, out conductance);
        }

        internal bool TryGetBoundary(
            SpatialBoundaryKey key,
            out SpatialConductancePair conductance)
        {
            return _boundaries.TryGetValue(key, out conductance);
        }

        /// <summary>
        /// Rebinds only the node coefficients while retaining the already
        /// validated topology conductances. This is an internal composition
        /// boundary for state-dependent overlays; the complete coefficient
        /// set is still validated by <see cref="TryCreate"/>.
        /// </summary>
        internal ContractValidationResult<SpatialCoefficientSet> TryRebindNodeCoefficients(
            IEnumerable<SpatialNodeCoefficients> nodeCoefficients)
        {
            if (nodeCoefficients == null)
            {
                return ContractValidationResult<SpatialCoefficientSet>.Invalid(
                    "SpatialCoefficients.Rebind.Nodes.Missing",
                    "node_coefficients",
                    "A node coefficient collection is required for a rebind.");
            }

            SpatialEdgeConductance[] edges = _edges
                .OrderBy(pair => pair.Key)
                .Select(pair => new SpatialEdgeConductance(
                    pair.Key.First,
                    pair.Key.Second,
                    pair.Value.Group1,
                    pair.Value.Group2))
                .ToArray();
            SpatialBoundaryConductance[] boundaries = _boundaries
                .OrderBy(pair => pair.Key)
                .Select(pair => new SpatialBoundaryConductance(
                    pair.Key.Node,
                    pair.Key.Face,
                    pair.Value.Group1,
                    pair.Value.Group2))
                .ToArray();

            return TryCreateCore(
                _stencil,
                nodeCoefficients,
                edges,
                boundaries,
                _xenonBasis,
                _referenceXeNumberDensityM3);
        }

        /// <summary>
        /// Rebinds node rows that were produced by an already validated
        /// coefficient set in its canonical stencil order. The immutable
        /// edge and boundary dictionaries retain their original validation;
        /// only the replacement physical rows need to be checked again.
        /// </summary>
        internal ContractValidationResult<SpatialCoefficientSet>
            TryRebindCanonicalNodeCoefficients(
                IEnumerable<SpatialNodeCoefficients> nodeCoefficients)
        {
            if (nodeCoefficients == null)
            {
                return ContractValidationResult<SpatialCoefficientSet>.Invalid(
                    "SpatialCoefficients.Rebind.Nodes.Missing",
                    "node_coefficients",
                    "A node coefficient collection is required for a canonical rebind.");
            }

            SpatialNodeCoefficients[] records = nodeCoefficients.ToArray();
            if (records.Length != _stencil.NodeCount)
            {
                return ContractValidationResult<SpatialCoefficientSet>.Invalid(
                    "SpatialCoefficients.Rebind.Nodes.CountMismatch",
                    "node_coefficients",
                    "A canonical rebind requires exactly one coefficient row per stencil node.");
            }

            for (int nodeIndex = 0; nodeIndex < records.Length; nodeIndex++)
            {
                SpatialNodeCoefficients record = records[nodeIndex];
                if (record == null)
                {
                    return ContractValidationResult<SpatialCoefficientSet>.Invalid(
                        "SpatialCoefficients.Node.Null",
                        "node_coefficients",
                        "A node coefficient record may not be null.");
                }

                NodeKey expectedNode = _stencil.Nodes[nodeIndex].Node;
                if (record.Node != expectedNode)
                {
                    return ContractValidationResult<SpatialCoefficientSet>.Invalid(
                        "SpatialCoefficients.Rebind.Nodes.OrderMismatch",
                        ContractValidation.NodePath(record.Node, ".coefficients"),
                        "Canonical replacement coefficient rows must follow stencil node order.");
                }

                ContractDiagnostic? failure = ValidateNodeCoefficients(record);
                if (failure != null)
                {
                    return ContractValidationResult<SpatialCoefficientSet>.Invalid(
                        failure.Code,
                        failure.Path,
                        failure.Message);
                }
            }

            return ContractValidationResult<SpatialCoefficientSet>.Valid(
                new SpatialCoefficientSet(
                    _stencil,
                    records,
                    _edges,
                    _boundaries,
                    _xenonBasis,
                    _referenceXeNumberDensityM3));
        }

        private static ContractDiagnostic? ValidateNodeCoefficients(SpatialNodeCoefficients record)
        {
            string path = ContractValidation.NodePath(record.Node, ".coefficients");
            if (!ContractValidation.IsFinite(record.VolumeM3) ||
                !ContractValidation.IsFinite(record.AbsorptionGroup1PerM) ||
                !ContractValidation.IsFinite(record.AbsorptionGroup2PerM) ||
                !ContractValidation.IsFinite(record.DownscatterGroup1To2PerM) ||
                !ContractValidation.IsFinite(record.FissionGroup1PerM) ||
                !ContractValidation.IsFinite(record.FissionGroup2PerM) ||
                !ContractValidation.IsFinite(record.NuFissionGroup1PerM) ||
                !ContractValidation.IsFinite(record.NuFissionGroup2PerM) ||
                !ContractValidation.IsFinite(record.ChiGroup1) ||
                !ContractValidation.IsFinite(record.ChiGroup2) ||
                !ContractValidation.IsFinite(record.EnergyPerFissionJ))
            {
                return new ContractDiagnostic(
                    "SpatialCoefficients.Node.NonFinite",
                    path,
                    "All node coefficients must be finite doubles.");
            }

            if (record.VolumeM3 <= 0)
            {
                return new ContractDiagnostic(
                    "SpatialCoefficients.Volume.Invalid",
                    path + ".volume_m3",
                    "Node volume must be strictly positive SI cubic metres.");
            }

            if (record.HasFissionCrossSections && record.EnergyPerFissionJ <= 0)
            {
                return new ContractDiagnostic(
                    "SpatialCoefficients.EnergyPerFission.Invalid",
                    path + ".energy_per_fission_j",
                    "Energy per fission must be strictly positive SI joules.");
            }

            if (record.PowerResponse != null)
            {
                ContractDiagnostic? powerResponseFailure = record.PowerResponse.Validate(
                    path + ".power_response");
                if (powerResponseFailure != null)
                {
                    return powerResponseFailure;
                }

                if (record.EnergyPerFissionJ != 0.0 ||
                    record.FissionGroup1PerM != 0.0 ||
                    record.FissionGroup2PerM != 0.0)
                {
                    return new ContractDiagnostic(
                        "SpatialCoefficients.Node.PowerResponse.MixedPath",
                        path,
                        "A direct flux-to-power response cannot be combined with legacy Sigma_f or energy-per-fission power terms.");
                }
            }
            else if (!record.HasFissionCrossSections)
            {
                return new ContractDiagnostic(
                    "SpatialCoefficients.Node.FissionCrossSections.Missing",
                    path,
                    "A Sigma_f-free row must provide an explicit H power response.");
            }

            if (record.AbsorptionGroup1PerM < 0 ||
                record.AbsorptionGroup2PerM < 0 ||
                record.DownscatterGroup1To2PerM < 0 ||
                record.FissionGroup1PerM < 0 ||
                record.FissionGroup2PerM < 0 ||
                record.NuFissionGroup1PerM < 0 ||
                record.NuFissionGroup2PerM < 0 ||
                record.ChiGroup1 < 0 ||
                record.ChiGroup2 < 0)
            {
                return new ContractDiagnostic(
                    "SpatialCoefficients.Node.Negative",
                    path,
                    "Cross sections, fission spectrum, and downscatter must be nonnegative.");
            }

            if (record.HasFissionCrossSections &&
                (record.AbsorptionGroup1PerM < record.FissionGroup1PerM ||
                 record.AbsorptionGroup2PerM < record.FissionGroup2PerM))
            {
                return new ContractDiagnostic(
                    "SpatialCoefficients.AbsorptionBelowFission",
                    path,
                    "Total absorption must be greater than or equal to fission absorption in each group.");
            }

            if (record.ChiGroup1 + record.ChiGroup2 != 1.0)
            {
                return new ContractDiagnostic(
                    "SpatialCoefficients.FissionSpectrum.SumInvalid",
                    path,
                    "The local fission spectrum must sum exactly to one.");
            }

            bool group1FissionZero = record.FissionGroup1PerM == 0;
            bool group1NuFissionZero = record.NuFissionGroup1PerM == 0;
            bool group2FissionZero = record.FissionGroup2PerM == 0;
            bool group2NuFissionZero = record.NuFissionGroup2PerM == 0;
            if (record.HasFissionCrossSections &&
                (group1FissionZero != group1NuFissionZero ||
                 group2FissionZero != group2NuFissionZero))
            {
                return new ContractDiagnostic(
                    "SpatialCoefficients.FissionSupportMismatch",
                    path,
                    "Sigma_f must be zero if and only if nuSigma_f is zero in each group.");
            }

            return null;
        }

        private static ContractDiagnostic? ValidatePositiveConductances(
            double group1,
            double group2,
            string path)
        {
            if (!ContractValidation.IsFinite(group1) || !ContractValidation.IsFinite(group2))
            {
                return new ContractDiagnostic(
                    "SpatialCoefficients.Conductance.NonFinite",
                    path,
                    "Interior conductances must be finite doubles.");
            }

            if (group1 <= 0 || group2 <= 0)
            {
                return new ContractDiagnostic(
                    "SpatialCoefficients.Conductance.NonPositive",
                    path,
                    "Interior conductances must be strictly positive SI square metres.");
            }

            return null;
        }

        private static ContractDiagnostic? ValidateBoundaryConductances(
            BoundaryClassification classification,
            double group1,
            double group2,
            string path)
        {
            if (!ContractValidation.IsFinite(group1) || !ContractValidation.IsFinite(group2))
            {
                return new ContractDiagnostic(
                    "SpatialCoefficients.Boundary.NonFinite",
                    path,
                    "Boundary conductances must be finite doubles.");
            }

            if (classification == BoundaryClassification.Reflective)
            {
                if (group1 != 0 || group2 != 0)
                {
                    return new ContractDiagnostic(
                        "SpatialCoefficients.Boundary.ReflectiveNonZero",
                        path,
                        "A reflective boundary must have zero conductance in both groups.");
                }

                return null;
            }

            if (group1 <= 0 || group2 <= 0)
            {
                return new ContractDiagnostic(
                    "SpatialCoefficients.Boundary.NonPositive",
                    path,
                    "Vacuum and specified-leakage conductances must be strictly positive SI square metres.");
            }

            return null;
        }
    }

    /// <summary>
    /// Matrix-free application of the approved removal-plus-leakage operator.
    /// The caller owns and reuses the input/output arrays.
    /// </summary>
    public sealed class SpatialOperator
    {
        private readonly SpatialStencil _stencil;
        private readonly CompiledNode[] _nodes;

        private SpatialOperator(SpatialStencil stencil, CompiledNode[] nodes)
        {
            _stencil = stencil;
            _nodes = nodes;
        }

        public int NodeCount
        {
            get { return _nodes.Length; }
        }

        public static ContractValidationResult<SpatialOperator> TryCreate(
            SpatialStencil stencil,
            SpatialCoefficientSet coefficients)
        {
            if (stencil == null)
            {
                return ContractValidationResult<SpatialOperator>.Invalid(
                    "SpatialOperator.Stencil.Missing",
                    "stencil",
                    "Operator construction requires an assembled stencil.");
            }

            if (coefficients == null)
            {
                return ContractValidationResult<SpatialOperator>.Invalid(
                    "SpatialOperator.Coefficients.Missing",
                    "coefficients",
                    "Operator construction requires validated coefficients.");
            }

            if (!ReferenceEquals(stencil, coefficients.Stencil))
            {
                return ContractValidationResult<SpatialOperator>.Invalid(
                    "SpatialOperator.Binding.Mismatch",
                    "coefficients",
                    "Coefficients must be bound to the exact stencil used to build the operator.");
            }

            var compiledNodes = new CompiledNode[stencil.NodeCount];
            for (int nodeIndex = 0; nodeIndex < stencil.NodeCount; nodeIndex++)
            {
                SpatialNodeStencil node = stencil.Nodes[nodeIndex];
                var compiledNeighbors = new CompiledNeighbor[node.NeighborTerms.Count];
                for (int termIndex = 0; termIndex < node.NeighborTerms.Count; termIndex++)
                {
                    SpatialNeighborTerm term = node.NeighborTerms[termIndex];
                    SpatialConductancePair conductance;
                    if (!coefficients.TryGetEdge(
                            new SpatialEdgeKey(node.Node, term.TargetNode),
                            out conductance))
                    {
                        return ContractValidationResult<SpatialOperator>.Invalid(
                            "SpatialOperator.EdgeBinding.Missing",
                            ContractValidation.NodePath(node.Node, ".neighbors"),
                            "Every stencil neighbor must have a bound conductance.");
                    }

                    compiledNeighbors[termIndex] = new CompiledNeighbor(
                        term.TargetFlatIndex,
                        conductance.Group1,
                        conductance.Group2);
                }

                var compiledBoundaries = new CompiledBoundary[node.BoundaryTerms.Count];
                for (int termIndex = 0; termIndex < node.BoundaryTerms.Count; termIndex++)
                {
                    SpatialBoundaryTerm term = node.BoundaryTerms[termIndex];
                    SpatialConductancePair conductance;
                    if (!coefficients.TryGetBoundary(
                            new SpatialBoundaryKey(node.Node, term.Face),
                            out conductance))
                    {
                        return ContractValidationResult<SpatialOperator>.Invalid(
                            "SpatialOperator.BoundaryBinding.Missing",
                            ContractValidation.NodePath(node.Node, ".boundary_faces"),
                            "Every stencil boundary face must have a bound conductance.");
                    }

                    compiledBoundaries[termIndex] = new CompiledBoundary(
                        conductance.Group1,
                        conductance.Group2);
                }

                compiledNodes[nodeIndex] = new CompiledNode(
                    coefficients.Nodes[nodeIndex],
                    compiledNeighbors,
                    compiledBoundaries);
            }

            return ContractValidationResult<SpatialOperator>.Valid(
                new SpatialOperator(stencil, compiledNodes));
        }

        public bool TryApply(
            SpatialEnergyGroup group,
            double[] flux,
            double[] destination,
            out ContractDiagnostic diagnostic)
        {
            if (group != SpatialEnergyGroup.Group1 && group != SpatialEnergyGroup.Group2)
            {
                ClearIfSupplied(destination);
                diagnostic = new ContractDiagnostic(
                    "SpatialOperator.Group.Invalid",
                    "group",
                    "The energy group must be Group1 or Group2.");
                return false;
            }

            if (flux == null)
            {
                ClearIfSupplied(destination);
                diagnostic = new ContractDiagnostic(
                    "SpatialOperator.Flux.Missing",
                    "flux",
                    "A flux vector is required.");
                return false;
            }

            if (destination == null)
            {
                diagnostic = new ContractDiagnostic(
                    "SpatialOperator.Destination.Missing",
                    "destination",
                    "A destination vector is required.");
                return false;
            }

            if (flux.Length != NodeCount)
            {
                ClearIfSupplied(destination);
                diagnostic = new ContractDiagnostic(
                    "SpatialOperator.Flux.DimensionMismatch",
                    "flux",
                    "The flux vector length must equal the stencil node count.");
                return false;
            }

            if (destination.Length != NodeCount)
            {
                ClearIfSupplied(destination);
                diagnostic = new ContractDiagnostic(
                    "SpatialOperator.Destination.DimensionMismatch",
                    "destination",
                    "The destination vector length must equal the stencil node count.");
                return false;
            }

            if (ReferenceEquals(flux, destination))
            {
                diagnostic = new ContractDiagnostic(
                    "SpatialOperator.Buffers.Alias",
                    "destination",
                    "The input and destination buffers must be distinct for deterministic application.");
                Array.Clear(destination, 0, destination.Length);
                return false;
            }

            for (int nodeIndex = 0; nodeIndex < flux.Length; nodeIndex++)
            {
                if (!ContractValidation.IsFinite(flux[nodeIndex]) || flux[nodeIndex] < 0)
                {
                    diagnostic = new ContractDiagnostic(
                        "SpatialOperator.Flux.Invalid",
                        ContractValidation.NodePath(_stencil.Nodes[nodeIndex].Node, ".flux"),
                        "Flux values must be finite and componentwise nonnegative.");
                    Array.Clear(destination, 0, destination.Length);
                    return false;
                }
            }

            for (int nodeIndex = 0; nodeIndex < _nodes.Length; nodeIndex++)
            {
                CompiledNode node = _nodes[nodeIndex];
                double removal = group == SpatialEnergyGroup.Group1
                    ? node.Coefficients.AbsorptionGroup1PerM + node.Coefficients.DownscatterGroup1To2PerM
                    : node.Coefficients.AbsorptionGroup2PerM;
                double result = removal * flux[nodeIndex];
                double leakage = 0.0;

                if (!ContractValidation.IsFinite(removal) || !ContractValidation.IsFinite(result))
                {
                    return FailAndClear(
                        destination,
                        ContractValidation.NodePath(_stencil.Nodes[nodeIndex].Node, ".operator"),
                        "SpatialOperator.Result.NonFinite",
                        "The removal contribution became non-finite.",
                        out diagnostic);
                }

                foreach (CompiledNeighbor neighbor in node.Neighbors)
                {
                    double difference = flux[nodeIndex] - flux[neighbor.TargetFlatIndex];
                    double conductance = group == SpatialEnergyGroup.Group1
                        ? neighbor.Group1
                        : neighbor.Group2;
                    double term = conductance * difference;
                    leakage += term;
                    if (!ContractValidation.IsFinite(term) || !ContractValidation.IsFinite(leakage))
                    {
                        return FailAndClear(
                            destination,
                            ContractValidation.NodePath(_stencil.Nodes[nodeIndex].Node, ".operator"),
                            "SpatialOperator.Leakage.NonFinite",
                            "The interior leakage contribution became non-finite.",
                            out diagnostic);
                    }
                }

                foreach (CompiledBoundary boundary in node.Boundaries)
                {
                    double conductance = group == SpatialEnergyGroup.Group1
                        ? boundary.Group1
                        : boundary.Group2;
                    double term = conductance * flux[nodeIndex];
                    leakage += term;
                    if (!ContractValidation.IsFinite(term) || !ContractValidation.IsFinite(leakage))
                    {
                        return FailAndClear(
                            destination,
                            ContractValidation.NodePath(_stencil.Nodes[nodeIndex].Node, ".operator"),
                            "SpatialOperator.BoundaryLeakage.NonFinite",
                            "The boundary leakage contribution became non-finite.",
                            out diagnostic);
                    }
                }

                double leakageContribution = leakage / node.Coefficients.VolumeM3;
                result += leakageContribution;
                if (!ContractValidation.IsFinite(leakageContribution) || !ContractValidation.IsFinite(result))
                {
                    return FailAndClear(
                        destination,
                        ContractValidation.NodePath(_stencil.Nodes[nodeIndex].Node, ".operator"),
                        "SpatialOperator.Result.NonFinite",
                        "The removal-plus-leakage result became non-finite.",
                        out diagnostic);
                }

                destination[nodeIndex] = result;
            }

            diagnostic = null!;
            return true;
        }

        /// <summary>
        /// Applies the Euclidean transpose of the removal-plus-leakage
        /// operator.  The primal operator stores conductances divided by the
        /// destination row volume, so the transpose accumulates each
        /// off-diagonal contribution into its target using the source row
        /// volume.  This is intentionally a separate API from
        /// <see cref="TryApply"/> so an adjoint solve cannot silently use the
        /// primal operator.
        /// </summary>
        public bool TryApplyTranspose(
            SpatialEnergyGroup group,
            double[] importance,
            double[] destination,
            out ContractDiagnostic diagnostic)
        {
            if (group != SpatialEnergyGroup.Group1 && group != SpatialEnergyGroup.Group2)
            {
                ClearIfSupplied(destination);
                diagnostic = new ContractDiagnostic(
                    "SpatialOperator.Group.Invalid",
                    "group",
                    "The energy group must be Group1 or Group2.");
                return false;
            }

            if (importance == null)
            {
                ClearIfSupplied(destination);
                diagnostic = new ContractDiagnostic(
                    "SpatialOperator.TransposeImportance.Missing",
                    "importance",
                    "An adjoint importance vector is required.");
                return false;
            }

            if (destination == null)
            {
                diagnostic = new ContractDiagnostic(
                    "SpatialOperator.Destination.Missing",
                    "destination",
                    "A destination vector is required.");
                return false;
            }

            if (importance.Length != NodeCount)
            {
                ClearIfSupplied(destination);
                diagnostic = new ContractDiagnostic(
                    "SpatialOperator.TransposeImportance.DimensionMismatch",
                    "importance",
                    "The adjoint importance vector length must equal the stencil node count.");
                return false;
            }

            if (destination.Length != NodeCount)
            {
                ClearIfSupplied(destination);
                diagnostic = new ContractDiagnostic(
                    "SpatialOperator.Destination.DimensionMismatch",
                    "destination",
                    "The destination vector length must equal the stencil node count.");
                return false;
            }

            if (ReferenceEquals(importance, destination))
            {
                diagnostic = new ContractDiagnostic(
                    "SpatialOperator.Buffers.Alias",
                    "destination",
                    "The input and destination buffers must be distinct for deterministic application.");
                Array.Clear(destination, 0, destination.Length);
                return false;
            }

            for (int nodeIndex = 0; nodeIndex < importance.Length; nodeIndex++)
            {
                if (!ContractValidation.IsFinite(importance[nodeIndex]) || importance[nodeIndex] < 0)
                {
                    diagnostic = new ContractDiagnostic(
                        "SpatialOperator.TransposeImportance.Invalid",
                        ContractValidation.NodePath(_stencil.Nodes[nodeIndex].Node, ".importance"),
                        "Adjoint importance values must be finite and componentwise nonnegative.");
                    Array.Clear(destination, 0, destination.Length);
                    return false;
                }
            }

            Array.Clear(destination, 0, destination.Length);
            for (int nodeIndex = 0; nodeIndex < _nodes.Length; nodeIndex++)
            {
                CompiledNode node = _nodes[nodeIndex];
                double removal = group == SpatialEnergyGroup.Group1
                    ? node.Coefficients.AbsorptionGroup1PerM + node.Coefficients.DownscatterGroup1To2PerM
                    : node.Coefficients.AbsorptionGroup2PerM;
                double conductanceSum = 0.0;
                foreach (CompiledNeighbor neighbor in node.Neighbors)
                {
                    conductanceSum += group == SpatialEnergyGroup.Group1
                        ? neighbor.Group1
                        : neighbor.Group2;
                }

                foreach (CompiledBoundary boundary in node.Boundaries)
                {
                    conductanceSum += group == SpatialEnergyGroup.Group1
                        ? boundary.Group1
                        : boundary.Group2;
                }

                double diagonal = removal + conductanceSum / node.Coefficients.VolumeM3;
                double diagonalContribution = diagonal * importance[nodeIndex];
                if (!ContractValidation.IsFinite(diagonal) ||
                    !ContractValidation.IsFinite(diagonalContribution))
                {
                    return FailAndClear(
                        destination,
                        ContractValidation.NodePath(_stencil.Nodes[nodeIndex].Node, ".transpose_operator"),
                        "SpatialOperator.Transpose.Result.NonFinite",
                        "The transpose diagonal contribution became non-finite.",
                        out diagnostic);
                }

                destination[nodeIndex] += diagonalContribution;
                if (!ContractValidation.IsFinite(destination[nodeIndex]))
                {
                    return FailAndClear(
                        destination,
                        ContractValidation.NodePath(_stencil.Nodes[nodeIndex].Node, ".transpose_operator"),
                        "SpatialOperator.Transpose.Result.NonFinite",
                        "The transpose result became non-finite.",
                        out diagnostic);
                }

                double inverseVolume = 1.0 / node.Coefficients.VolumeM3;
                foreach (CompiledNeighbor neighbor in node.Neighbors)
                {
                    double conductance = group == SpatialEnergyGroup.Group1
                        ? neighbor.Group1
                        : neighbor.Group2;
                    double contribution = -conductance * inverseVolume * importance[nodeIndex];
                    if (!ContractValidation.IsFinite(contribution))
                    {
                        return FailAndClear(
                            destination,
                            ContractValidation.NodePath(_stencil.Nodes[nodeIndex].Node, ".transpose_operator"),
                            "SpatialOperator.Transpose.Leakage.NonFinite",
                            "The transpose leakage contribution became non-finite.",
                            out diagnostic);
                    }

                    destination[neighbor.TargetFlatIndex] += contribution;
                    if (!ContractValidation.IsFinite(destination[neighbor.TargetFlatIndex]))
                    {
                        return FailAndClear(
                            destination,
                            ContractValidation.NodePath(_stencil.Nodes[nodeIndex].Node, ".transpose_operator"),
                            "SpatialOperator.Transpose.Result.NonFinite",
                            "The transpose result became non-finite.",
                            out diagnostic);
                    }
                }
            }

            diagnostic = null!;
            return true;
        }

        private static bool FailAndClear(
            double[] destination,
            string path,
            string code,
            string message,
            out ContractDiagnostic diagnostic)
        {
            Array.Clear(destination, 0, destination.Length);
            diagnostic = new ContractDiagnostic(code, path, message);
            return false;
        }

        private static void ClearIfSupplied(double[] destination)
        {
            if (destination != null)
            {
                Array.Clear(destination, 0, destination.Length);
            }
        }

        private sealed class CompiledNode
        {
            public CompiledNode(
                SpatialNodeCoefficients coefficients,
                CompiledNeighbor[] neighbors,
                CompiledBoundary[] boundaries)
            {
                Coefficients = coefficients;
                Neighbors = neighbors;
                Boundaries = boundaries;
            }

            public SpatialNodeCoefficients Coefficients { get; }

            public CompiledNeighbor[] Neighbors { get; }

            public CompiledBoundary[] Boundaries { get; }
        }

        private sealed class CompiledNeighbor
        {
            public CompiledNeighbor(int targetFlatIndex, double group1, double group2)
            {
                TargetFlatIndex = targetFlatIndex;
                Group1 = group1;
                Group2 = group2;
            }

            public int TargetFlatIndex { get; }

            public double Group1 { get; }

            public double Group2 { get; }
        }

        private sealed class CompiledBoundary
        {
            public CompiledBoundary(double group1, double group2)
            {
                Group1 = group1;
                Group2 = group2;
            }

            public double Group1 { get; }

            public double Group2 { get; }
        }
    }

    internal readonly struct SpatialConductancePair
    {
        public SpatialConductancePair(double group1, double group2)
        {
            Group1 = group1;
            Group2 = group2;
        }

        public double Group1 { get; }

        public double Group2 { get; }
    }

    internal readonly struct SpatialEdgeKey : IEquatable<SpatialEdgeKey>, IComparable<SpatialEdgeKey>
    {
        public SpatialEdgeKey(NodeKey endpointA, NodeKey endpointB)
        {
            if (endpointA.CompareTo(endpointB) <= 0)
            {
                First = endpointA;
                Second = endpointB;
            }
            else
            {
                First = endpointB;
                Second = endpointA;
            }
        }

        public NodeKey First { get; }

        public NodeKey Second { get; }

        public int CompareTo(SpatialEdgeKey other)
        {
            int first = First.CompareTo(other.First);
            return first != 0 ? first : Second.CompareTo(other.Second);
        }

        public bool Equals(SpatialEdgeKey other)
        {
            return First == other.First && Second == other.Second;
        }

        public override bool Equals(object? obj)
        {
            return obj is SpatialEdgeKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (First.GetHashCode() * 397) ^ Second.GetHashCode();
            }
        }

        public override string ToString()
        {
            return First + "-" + Second;
        }
    }

    internal readonly struct SpatialBoundaryKey : IEquatable<SpatialBoundaryKey>, IComparable<SpatialBoundaryKey>
    {
        public SpatialBoundaryKey(NodeKey node, TopologyFace face)
        {
            Node = node;
            Face = face;
        }

        public NodeKey Node { get; }

        public TopologyFace Face { get; }

        public int CompareTo(SpatialBoundaryKey other)
        {
            int node = Node.CompareTo(other.Node);
            return node != 0 ? node : ((byte)Face).CompareTo((byte)other.Face);
        }

        public bool Equals(SpatialBoundaryKey other)
        {
            return Node == other.Node && Face == other.Face;
        }

        public override bool Equals(object? obj)
        {
            return obj is SpatialBoundaryKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Node.GetHashCode() * 397) ^ (byte)Face;
            }
        }

        public override string ToString()
        {
            return Node + "/" + Face;
        }
    }
}
