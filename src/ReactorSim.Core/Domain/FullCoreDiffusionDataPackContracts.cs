using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ReactorSim.Core
{
    /// <summary>
    /// Two-group leakage conductance in SI square metres. The values are
    /// geometry/material-interface data, not cross sections, and are kept
    /// separate from the burnup table by design.
    /// </summary>
    public sealed class TwoGroupConductanceV1
    {
        internal TwoGroupConductanceV1(double group1M2, double group2M2)
        {
            Group1M2 = group1M2;
            Group2M2 = group2M2;
        }

        public double Group1M2 { get; }

        public double Group2M2 { get; }
    }

    /// <summary>
    /// Versioned full-core two-group diffusion data. The pack is intentionally
    /// compact: one material table per material variant plus uniform geometry
    /// conductances for the fixed CANDU-6 lattice. The active embedded pack is
    /// project-authored surrogate data consumed directly by the runtime solver.
    /// </summary>
    public sealed class FullCoreDiffusionDataPackV1
    {
        public const uint CurrentSchemaVersion = 1;
        public const string SupportedUnitsProfileId = "SI-v1";
        public const string SupportedModelId = "candu6-two-group-full-core-diffusion-v1";
        public const string SupportedSolverId = "spatial-eigen-jacobi-v1";
        public const string EmbeddedResourceName =
            "ReactorSim.Core.Data.candu6-two-group-diffusion-pack-v1.json";

        private readonly ReadOnlyCollection<string> _energyGroupOrder;
        private readonly ReadOnlyCollection<BurnupCoefficientTableV1> _coefficientTables;

        private FullCoreDiffusionDataPackV1(
            DataPackDescriptor descriptor,
            string modelId,
            string solverId,
            IEnumerable<string> energyGroupOrder,
            string evidenceClass,
            string sourceProvenance,
            string sourceToolchain,
            string transformId,
            double nodeVolumeM3,
            TwoGroupConductanceV1 axialConductance,
            TwoGroupConductanceV1 transverseConductance,
            TwoGroupConductanceV1 vacuumBoundaryConductance,
            TwoGroupConductanceV1 axialVacuumBoundaryConductance,
            string axialBoundaryConditionId,
            double axialCellLengthM,
            SpatialLinearSolvePolicy linearSolvePolicy,
            SpatialConvergencePolicy convergencePolicy,
            IEnumerable<BurnupCoefficientTableV1> coefficientTables,
            StaticAbsorptionOverlayV1? adjusters,
            PracticeXenonReferenceV1? xenonReference,
            XenonBasisV1 declaredXenonBasis)
        {
            DeclaredXenonBasis = declaredXenonBasis;
            XenonReference = xenonReference;
            Adjusters = adjusters;
            Descriptor = descriptor;
            ModelId = modelId;
            SolverId = solverId;
            _energyGroupOrder = new ReadOnlyCollection<string>(energyGroupOrder.ToArray());
            EvidenceClass = evidenceClass;
            SourceProvenance = sourceProvenance;
            SourceToolchain = sourceToolchain;
            TransformId = transformId;
            NodeVolumeM3 = nodeVolumeM3;
            AxialConductance = axialConductance;
            TransverseConductance = transverseConductance;
            VacuumBoundaryConductance = vacuumBoundaryConductance;
            AxialVacuumBoundaryConductance = axialVacuumBoundaryConductance;
            AxialBoundaryConditionId = axialBoundaryConditionId;
            AxialCellLengthM = axialCellLengthM;
            LinearSolvePolicy = linearSolvePolicy;
            ConvergencePolicy = convergencePolicy;
            _coefficientTables = new ReadOnlyCollection<BurnupCoefficientTableV1>(
                coefficientTables.ToArray());
        }

        public DataPackDescriptor Descriptor { get; }

        public string ModelId { get; }

        public string SolverId { get; }

        public IReadOnlyList<string> EnergyGroupOrder
        {
            get { return _energyGroupOrder; }
        }

        public string EvidenceClass { get; }

        public string SourceProvenance { get; }

        public string SourceToolchain { get; }

        public string TransformId { get; }

        /// <summary>Fixed nominal adjuster absorption, absent in archived packs.</summary>
        public StaticAbsorptionOverlayV1? Adjusters { get; }

        public PracticeXenonReferenceV1? XenonReference { get; }
        public XenonBasisV1 DeclaredXenonBasis { get; }

        public double NodeVolumeM3 { get; }

        public TwoGroupConductanceV1 AxialConductance { get; }

        public TwoGroupConductanceV1 TransverseConductance { get; }

        public TwoGroupConductanceV1 VacuumBoundaryConductance { get; }

        /// <summary>End-face leakage; radial faces retain the effective reflector conductance.</summary>
        public TwoGroupConductanceV1 AxialVacuumBoundaryConductance { get; }
        public string AxialBoundaryConditionId { get; }
        public double AxialCellLengthM { get; }

        public SpatialLinearSolvePolicy LinearSolvePolicy { get; }

        public SpatialConvergencePolicy ConvergencePolicy { get; }

        public IReadOnlyList<BurnupCoefficientTableV1> CoefficientTables
        {
            get { return _coefficientTables; }
        }

        public static ContractValidationResult<FullCoreDiffusionDataPackV1> TryLoadEmbeddedCandu6()
        {
            Assembly assembly = typeof(FullCoreDiffusionDataPackV1).Assembly;
            using (Stream? stream = assembly.GetManifestResourceStream(EmbeddedResourceName))
            {
                if (stream == null)
                {
                    return Invalid(
                        "FullCoreDiffusionDataPack.EmbeddedResource.Missing",
                        "resource",
                        "The embedded CANDU-6 diffusion pack could not be found in ReactorSim.Core.");
                }

                using (var reader = new StreamReader(stream, Encoding.UTF8, true))
                {
                    return TryLoadJson(reader.ReadToEnd());
                }
            }
        }

        public static ContractValidationResult<FullCoreDiffusionDataPackV1> TryLoadJson(
            string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return Invalid(
                    "FullCoreDiffusionDataPack.Json.Empty",
                    "json",
                    "A full-core diffusion data pack requires a non-empty JSON document.");
            }

            JObject root;
            try
            {
                root = JObject.Parse(json);
            }
            catch (JsonException exception)
            {
                return Invalid(
                    "FullCoreDiffusionDataPack.Json.Invalid",
                    "json",
                    "The full-core diffusion data pack is not valid JSON: " + exception.Message);
            }

            ContractDiagnostic failure;
            if (!TryReadUInt32(root, "schema_version", "schema_version", out uint schemaVersion, out failure))
            {
                return Invalid(failure);
            }

            if (schemaVersion != CurrentSchemaVersion)
            {
                return Invalid(
                    "FullCoreDiffusionDataPack.SchemaVersion.Unsupported",
                    "schema_version",
                    "Only full-core diffusion data-pack schema version 1 is supported.");
            }

            if (!TryReadStableId(root, "data_pack_id", "data_pack_id", out StableId dataPackId, out failure) ||
                !TryReadString(root, "data_pack_version", "data_pack_version", out string dataPackVersion, out failure) ||
                !TryReadString(root, "topology_schema_id", "topology_schema_id", out string topologySchemaId, out failure) ||
                !TryReadUInt32(root, "channel_count", "channel_count", out uint channelCount, out failure) ||
                !TryReadUInt32(root, "bundle_position_count", "bundle_position_count", out uint bundlePositionCount, out failure) ||
                !TryReadString(root, "units_profile_id", "units_profile_id", out string unitsProfileId, out failure) ||
                !TryReadString(root, "model_id", "model_id", out string modelId, out failure) ||
                !TryReadString(root, "solver_id", "solver_id", out string solverId, out failure) ||
                !TryReadString(root, "evidence_class", "evidence_class", out string evidenceClass, out failure) ||
                !TryReadString(root, "source_provenance", "source_provenance", out string sourceProvenance, out failure) ||
                !TryReadString(root, "source_toolchain", "source_toolchain", out string sourceToolchain, out failure) ||
                !TryReadString(root, "transform_id", "transform_id", out string transformId, out failure))
            {
                return Invalid(failure);
            }

            if (LooksLikePrivateRuntimePath(sourceProvenance))
            {
                return Invalid(
                    "FullCoreDiffusionDataPack.SourceProvenance.Path",
                    "source_provenance",
                    "Pack provenance may not carry a private runtime file path.");
            }

            if (!string.Equals(
                    unitsProfileId,
                    SupportedUnitsProfileId,
                    StringComparison.Ordinal))
            {
                return Invalid(
                    "FullCoreDiffusionDataPack.Units.Unsupported",
                    "units_profile_id",
                    "The two-group runtime solver requires the canonical SI-v1 units profile.");
            }

            if (!string.Equals(modelId, SupportedModelId, StringComparison.Ordinal))
            {
                return Invalid(
                    "FullCoreDiffusionDataPack.Model.Unsupported",
                    "model_id",
                    "The pack model identity must match the CANDU-6 two-group full-core solver.");
            }

            if (!string.Equals(solverId, SupportedSolverId, StringComparison.Ordinal))
            {
                return Invalid(
                    "FullCoreDiffusionDataPack.Solver.Unsupported",
                    "solver_id",
                    "The pack solver identity must match the deterministic spatial-eigen-jacobi-v1 runtime.");
            }

            if (channelCount != Candu6CoreTopologyFactoryV1.ChannelCount ||
                bundlePositionCount != Candu6CoreTopologyFactoryV1.BundlePositionCount)
            {
                return Invalid(
                    "FullCoreDiffusionDataPack.Dimensions.Unsupported",
                    "dimensions",
                    "The full-core pack must declare the CANDU-6 380 by 12 bundle grid.");
            }

            if (!TryReadGroupOrder(root, out IReadOnlyList<string> groupOrder, out failure))
            {
                return Invalid(failure);
            }

            if (!TryReadObject(root, "geometry", "geometry", out JObject geometry, out failure) ||
                !TryReadDouble(geometry, "node_volume_m3", "geometry.node_volume_m3", out double nodeVolumeM3, out failure) ||
                !TryReadConductance(geometry, "axial_edge_conductance_m2", "geometry.axial_edge_conductance_m2", out TwoGroupConductanceV1 axial, out failure) ||
                !TryReadConductance(geometry, "transverse_edge_conductance_m2", "geometry.transverse_edge_conductance_m2", out TwoGroupConductanceV1 transverse, out failure) ||
                !TryReadConductance(geometry, "vacuum_boundary_conductance_m2", "geometry.vacuum_boundary_conductance_m2", out TwoGroupConductanceV1 vacuum, out failure))
            {
                return Invalid(failure);
            }

            var axialVacuum = vacuum;
            string axialBoundaryId = "legacy-fitted-conductance-v1";
            double axialCellLength = 0;
            if (geometry.TryGetValue("axial_boundary", StringComparison.Ordinal, out JToken? boundaryToken))
            {
                if (!(boundaryToken is JObject axialBoundary) ||
                    !TryReadString(axialBoundary, "condition_id", "geometry.axial_boundary.condition_id", out axialBoundaryId, out failure) ||
                    !TryReadDouble(axialBoundary, "cell_length_m", "geometry.axial_boundary.cell_length_m", out axialCellLength, out failure))
                    return Invalid("FullCoreDiffusionDataPack.AxialBoundary.Invalid", "geometry.axial_boundary", "Supply a valid axial boundary condition and cell length.");
                if (axialBoundaryId != "zero-incoming-current-v1" || axialCellLength <= 0 || nodeVolumeM3 <= 0)
                    return Invalid("FullCoreDiffusionDataPack.AxialBoundary.Invalid", "geometry.axial_boundary", "Only zero-incoming-current-v1 with positive SI geometry is supported.");
                // Marshak: phi + 2D*d(phi)/dn = 0. Node-centred C = DA/(h/2+2D).
                // D = C_interior*h^2/V, A = V/h. No material absorption is added.
                double Boundary(double interior)
                {
                    double diffusion = interior * axialCellLength * axialCellLength / nodeVolumeM3;
                    return interior * axialCellLength / (axialCellLength / 2 + 2 * diffusion);
                }
                double fast = Boundary(axial.Group1M2), thermal = Boundary(axial.Group2M2);
                if (!ContractValidation.IsFinite(fast) || !ContractValidation.IsFinite(thermal) || fast <= 0 || thermal <= 0)
                    return Invalid("FullCoreDiffusionDataPack.AxialBoundary.Invalid", "geometry.axial_boundary", "Derived axial boundary conductance must be finite and positive.");
                axialVacuum = new TwoGroupConductanceV1(fast, thermal);
            }

            if (!TryReadObject(root, "solver", "solver", out JObject solver, out failure) ||
                !TryReadString(solver, "linear_method_id", "solver.linear_method_id", out string linearMethodId, out failure) ||
                !TryReadString(solver, "linear_method_version", "solver.linear_method_version", out string linearMethodVersion, out failure) ||
                !TryReadDouble(solver, "absolute_residual_tolerance", "solver.absolute_residual_tolerance", out double absoluteResidualTolerance, out failure) ||
                !TryReadDouble(solver, "relative_residual_tolerance", "solver.relative_residual_tolerance", out double relativeResidualTolerance, out failure) ||
                !TryReadInt32(solver, "maximum_inner_iterations", "solver.maximum_inner_iterations", out int maximumInnerIterations, out failure) ||
                !TryReadDouble(solver, "k_absolute_tolerance", "solver.k_absolute_tolerance", out double kAbsoluteTolerance, out failure) ||
                !TryReadDouble(solver, "k_relative_tolerance", "solver.k_relative_tolerance", out double kRelativeTolerance, out failure) ||
                !TryReadDouble(solver, "residual_tolerance", "solver.residual_tolerance", out double residualTolerance, out failure) ||
                !TryReadDouble(solver, "source_shape_tolerance", "solver.source_shape_tolerance", out double sourceShapeTolerance, out failure) ||
                !TryReadDouble(solver, "power_balance_tolerance", "solver.power_balance_tolerance", out double powerBalanceTolerance, out failure) ||
                !TryReadInt32(solver, "maximum_iterations", "solver.maximum_iterations", out int maximumIterations, out failure))
            {
                return Invalid(failure);
            }

            ContractValidationResult<SpatialLinearSolvePolicy> linearPolicyResult =
                SpatialLinearSolvePolicy.TryCreate(
                    linearMethodId,
                    linearMethodVersion,
                    absoluteResidualTolerance,
                    relativeResidualTolerance,
                    maximumInnerIterations);
            if (!linearPolicyResult.IsValid)
            {
                return Invalid(linearPolicyResult.FirstDiagnostic);
            }

            ContractValidationResult<SpatialConvergencePolicy> convergencePolicyResult =
                SpatialConvergencePolicy.TryCreate(
                    kAbsoluteTolerance,
                    kRelativeTolerance,
                    residualTolerance,
                    sourceShapeTolerance,
                    powerBalanceTolerance,
                    maximumIterations);
            if (!convergencePolicyResult.IsValid)
            {
                return Invalid(convergencePolicyResult.FirstDiagnostic);
            }

            if (!TryReadArray(root, "coefficient_tables", "coefficient_tables", out JArray tableArray, out failure))
            {
                return Invalid(failure);
            }

            var tables = new List<BurnupCoefficientTableV1>(tableArray.Count);
            var materialIds = new HashSet<string>(StringComparer.Ordinal);
            for (int tableIndex = 0; tableIndex < tableArray.Count; tableIndex++)
            {
                JToken tableToken = tableArray[tableIndex];
                if (!(tableToken is JObject tableObject))
                {
                    return Invalid(
                        "FullCoreDiffusionDataPack.Table.Invalid",
                        "coefficient_tables[" + tableIndex.ToString(CultureInfo.InvariantCulture) + "]",
                        "Every coefficient table must be a JSON object.");
                }

                ContractValidationResult<BurnupCoefficientTableV1> tableResult =
                    TryReadTable(tableObject, tableIndex);
                if (!tableResult.IsValid)
                {
                    return Invalid(tableResult.FirstDiagnostic);
                }

                BurnupCoefficientTableV1 table = tableResult.Value;
                if (!materialIds.Add(table.MaterialVariantId.Value))
                {
                    return Invalid(
                        "FullCoreDiffusionDataPack.Table.MaterialDuplicate",
                        "coefficient_tables[" + tableIndex.ToString(CultureInfo.InvariantCulture) + "]",
                        "Each material variant may have only one burnup coefficient table.");
                }

                if (!string.Equals(
                        table.UnitsProfileId,
                        unitsProfileId,
                        StringComparison.Ordinal))
                {
                    return Invalid(
                        "FullCoreDiffusionDataPack.Table.UnitsMismatch",
                        "coefficient_tables[" + tableIndex.ToString(CultureInfo.InvariantCulture) + "].units_profile_id",
                        "Every coefficient table must use the root pack units profile.");
                }

                tables.Add(table);
            }

            if (tables.Count == 0)
            {
                return Invalid(
                    "FullCoreDiffusionDataPack.Table.Empty",
                    "coefficient_tables",
                    "At least one burnup coefficient table is required.");
            }

            StaticAbsorptionOverlayV1? adjusters = null;
            if (root["adjusters"] != null)
            {
                if (!TryReadObject(root, "adjusters", "adjusters", out JObject device, out failure) ||
                    !TryReadString(device, "layout_id", "adjusters.layout_id", out string layout, out failure) ||
                    !TryReadDouble(device, "inner_absorption_group1_per_m", "adjusters.inner_absorption_group1_per_m", out double a1, out failure) ||
                    !TryReadDouble(device, "inner_absorption_group2_per_m", "adjusters.inner_absorption_group2_per_m", out double a2, out failure))
                    return Invalid(failure);
                if (layout != PracticeAdjustersV1.LayoutId || a1 < 0 || a2 < 0 ||
                    (a1 == 0 && BitConverter.DoubleToInt64Bits(a1) < 0) ||
                    (a2 == 0 && BitConverter.DoubleToInt64Bits(a2) < 0))
                    return Invalid("FullCoreDiffusionDataPack.Adjusters.Invalid", "adjusters",
                        "Adjusters require the supported layout and nonnegative SI absorption strengths.");
                adjusters = PracticeAdjustersV1.CreateOverlay(a1, a2);
            }

            PracticeXenonReferenceV1? xenonReference = null;
            XenonBasisV1 declaredXenonBasis = XenonBasisV1.Unspecified;
            if (root["xenon_basis"]?.Type == JTokenType.String && root["xenon_basis"]!.Value<string>() == "excluded" && root["xenon_reference"] == null)
                declaredXenonBasis = XenonBasisV1.Excluded;
            else if (root["xenon_reference"] != null || root["xenon_basis"] != null)
            {
                if (!TryReadString(root, "xenon_basis", "xenon_basis", out string basis, out failure) ||
                    !TryReadObject(root, "xenon_reference", "xenon_reference", out JObject reference, out failure) ||
                    !TryReadString(reference, "model_id", "xenon_reference.model_id", out string referenceId, out failure) ||
                    !TryReadString(reference, "poison_data_identity", "xenon_reference.poison_data_identity", out string poisonId, out failure) ||
                    !TryReadDouble(reference, "reference_specific_power_w_per_kg_hm", "xenon_reference.reference_specific_power_w_per_kg_hm", out double referencePower, out failure) ||
                    !TryReadArray(reference, "rows", "xenon_reference.rows", out JArray referenceRows, out failure))
                    return Invalid(failure);
                if (basis != PracticeXenonReferenceV1.BasisId || referenceId != PracticeXenonReferenceV1.Identity ||
                    poisonId != PracticeXenonDataV1.Identity || referencePower <= 0 || tables.Count != 1 ||
                    referenceRows.Count != tables[0].Rows.Count)
                    return Invalid("FullCoreDiffusionDataPack.XenonReference.Invalid", "xenon_reference",
                        "The included Xe reference requires a supported identity and exactly the fuel table burnup knots.");
                var referenceBurnup = new double[referenceRows.Count];
                var referenceXe = new double[referenceRows.Count];
                for (int n = 0; n < referenceRows.Count; n++)
                {
                    if (!(referenceRows[n] is JObject row) ||
                        !TryReadDouble(row, "burnup_j_per_kg_hm", "xenon_reference.rows.burnup_j_per_kg_hm", out referenceBurnup[n], out failure) ||
                        !TryReadDouble(row, "xe135_number_density_m3", "xenon_reference.rows.xe135_number_density_m3", out referenceXe[n], out failure))
                        return Invalid("FullCoreDiffusionDataPack.XenonReference.Row.Invalid", "xenon_reference.rows",
                            "Every Xe reference row must contain finite numeric burnup and number density.");
                    if (referenceBurnup[n] != tables[0].Rows[n].BurnupJPerKgHm || referenceXe[n] < 0 ||
                        (referenceXe[n] == 0 && BitConverter.DoubleToInt64Bits(referenceXe[n]) < 0) ||
                        tables[0].Rows[n].Coefficients.AbsorptionGroup2PerM - PracticeXenonDataV1.SigmaGroup2M2 * referenceXe[n] <
                        tables[0].Rows[n].Coefficients.FissionGroup2PerM)
                        return Invalid("FullCoreDiffusionDataPack.XenonReference.Row.Invalid", "xenon_reference.rows",
                            "Reference knots must match fuel knots and permit nonnegative Xe-free absorption support.");
                }
                if (referenceBurnup[0] != 0 || referenceXe[0] != 0)
                    return Invalid("FullCoreDiffusionDataPack.XenonReference.Fresh.Invalid", "xenon_reference.rows",
                        "Fresh fuel must carry zero included reference xenon.");
                xenonReference = new PracticeXenonReferenceV1(referencePower, referenceBurnup, referenceXe);
                declaredXenonBasis = XenonBasisV1.Included;
            }

            byte[] topologyDigest = Sha256(Candu6CoreTopologyFactoryV1.GetTopologyIdentity());
            byte[] contentDigest = Sha256(json);
            ContractValidationResult<DataPackDescriptor> descriptorResult =
                DataPackDescriptor.TryCreate(
                    DataPackDescriptor.CurrentSchemaVersion,
                    dataPackId,
                    dataPackVersion,
                    topologySchemaId,
                    channelCount,
                    bundlePositionCount,
                    unitsProfileId,
                    topologyDigest,
                    contentDigest);
            if (!descriptorResult.IsValid)
            {
                return Invalid(descriptorResult.FirstDiagnostic);
            }

            return ContractValidationResult<FullCoreDiffusionDataPackV1>.Valid(
                new FullCoreDiffusionDataPackV1(
                    descriptorResult.Value,
                    modelId,
                    solverId,
                    groupOrder,
                    evidenceClass,
                    sourceProvenance,
                    sourceToolchain,
                    transformId,
                    nodeVolumeM3,
                    axial,
                    transverse,
                    vacuum,
                    axialVacuum,
                    axialBoundaryId,
                    axialCellLength,
                    linearPolicyResult.Value,
                    convergencePolicyResult.Value,
                    tables,
                    adjusters,
                    xenonReference,
                    declaredXenonBasis));
        }

        private static ContractValidationResult<BurnupCoefficientTableV1> TryReadTable(
            JObject tableObject,
            int tableIndex)
        {
            string prefix = "coefficient_tables[" + tableIndex.ToString(CultureInfo.InvariantCulture) + "]";
            ContractDiagnostic failure;
            if (!TryReadStableId(tableObject, "table_id", prefix + ".table_id", out StableId tableId, out failure) ||
                !TryReadUInt32(tableObject, "schema_version", prefix + ".schema_version", out uint schemaVersion, out failure) ||
                !TryReadString(tableObject, "data_version", prefix + ".data_version", out string dataVersion, out failure) ||
                !TryReadString(tableObject, "material_variant_id", prefix + ".material_variant_id", out string materialVariant, out failure) ||
                !TryReadString(tableObject, "units_profile_id", prefix + ".units_profile_id", out string unitsProfileId, out failure) ||
                !TryReadString(tableObject, "source_provenance", prefix + ".source_provenance", out string sourceProvenance, out failure) ||
                !TryReadDigest(tableObject, "checksum", prefix + ".checksum", out Digest32 checksum, out failure) ||
                !TryReadArray(tableObject, "rows", prefix + ".rows", out JArray rows, out failure))
            {
                return InvalidTable(failure);
            }

            var parsedRows = new List<BurnupCoefficientRowV1>(rows.Count);
            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                if (!(rows[rowIndex] is JObject row))
                {
                    return InvalidTable(
                        new ContractDiagnostic(
                            "FullCoreDiffusionDataPack.Table.Row.Invalid",
                            prefix + ".rows[" + rowIndex.ToString(CultureInfo.InvariantCulture) + "]",
                            "Every burnup row must be a JSON object."));
                }

                string rowPrefix = prefix + ".rows[" + rowIndex.ToString(CultureInfo.InvariantCulture) + "]";
                if (!TryReadDouble(row, "burnup_j_per_kg_hm", rowPrefix + ".burnup_j_per_kg_hm", out double burnup, out failure) ||
                    !TryReadDouble(row, "absorption_group1_per_m", rowPrefix + ".absorption_group1_per_m", out double absorptionGroup1, out failure) ||
                    !TryReadDouble(row, "absorption_group2_per_m", rowPrefix + ".absorption_group2_per_m", out double absorptionGroup2, out failure) ||
                    !TryReadDouble(row, "fission_group1_per_m", rowPrefix + ".fission_group1_per_m", out double fissionGroup1, out failure) ||
                    !TryReadDouble(row, "fission_group2_per_m", rowPrefix + ".fission_group2_per_m", out double fissionGroup2, out failure) ||
                    !TryReadDouble(row, "nu_fission_group1_per_m", rowPrefix + ".nu_fission_group1_per_m", out double nuFissionGroup1, out failure) ||
                    !TryReadDouble(row, "nu_fission_group2_per_m", rowPrefix + ".nu_fission_group2_per_m", out double nuFissionGroup2, out failure) ||
                    !TryReadDouble(row, "downscatter_group1_to_2_per_m", rowPrefix + ".downscatter_group1_to_2_per_m", out double downscatter, out failure) ||
                    !TryReadDouble(row, "chi_group1", rowPrefix + ".chi_group1", out double chiGroup1, out failure) ||
                    !TryReadDouble(row, "energy_per_fission_j", rowPrefix + ".energy_per_fission_j", out double energyPerFission, out failure))
                {
                    return InvalidTable(failure);
                }

                ContractValidationResult<BurnupCoefficientValuesV1> values =
                    BurnupCoefficientValuesV1.TryCreate(
                        absorptionGroup1,
                        absorptionGroup2,
                        fissionGroup1,
                        fissionGroup2,
                        nuFissionGroup1,
                        nuFissionGroup2,
                        downscatter,
                        chiGroup1,
                        energyPerFission);
                if (!values.IsValid)
                {
                    return InvalidTable(values.FirstDiagnostic);
                }

                ContractValidationResult<BurnupCoefficientRowV1> parsedRow =
                    BurnupCoefficientRowV1.TryCreate(burnup, values.Value);
                if (!parsedRow.IsValid)
                {
                    return InvalidTable(parsedRow.FirstDiagnostic);
                }

                parsedRows.Add(parsedRow.Value);
            }

            return BurnupCoefficientTableV1.TryCreate(
                tableId,
                schemaVersion,
                dataVersion,
                new MaterialVariantId(materialVariant),
                unitsProfileId,
                sourceProvenance,
                checksum,
                parsedRows);
        }

        private static bool TryReadConductance(
            JObject parent,
            string propertyName,
            string path,
            out TwoGroupConductanceV1 conductance,
            out ContractDiagnostic failure)
        {
            conductance = null!;
            if (!TryReadObject(parent, propertyName, path, out JObject objectValue, out failure) ||
                !TryReadDouble(objectValue, "group1_m2", path + ".group1_m2", out double group1, out failure) ||
                !TryReadDouble(objectValue, "group2_m2", path + ".group2_m2", out double group2, out failure))
            {
                return false;
            }

            if (group1 <= 0.0 || group2 <= 0.0)
            {
                failure = new ContractDiagnostic(
                    "FullCoreDiffusionDataPack.Conductance.NonPositive",
                    path,
                    "Two-group conductances must be finite and strictly positive SI square metres.");
                return false;
            }

            conductance = new TwoGroupConductanceV1(group1, group2);
            failure = null!;
            return true;
        }

        private static bool TryReadGroupOrder(
            JObject root,
            out IReadOnlyList<string> groupOrder,
            out ContractDiagnostic failure)
        {
            groupOrder = Array.Empty<string>();
            if (!TryReadArray(root, "energy_group_order", "energy_group_order", out JArray array, out failure))
            {
                return false;
            }

            if (array.Count != 2 ||
                array[0].Type != JTokenType.String ||
                array[1].Type != JTokenType.String)
            {
                failure = new ContractDiagnostic(
                    "FullCoreDiffusionDataPack.EnergyGroupOrder.Invalid",
                    "energy_group_order",
                    "The pack must declare exactly two ordered energy groups.");
                return false;
            }

            string first = array[0].Value<string>()!;
            string second = array[1].Value<string>()!;
            if (!string.Equals(first, "fast", StringComparison.Ordinal) ||
                !string.Equals(second, "thermal", StringComparison.Ordinal))
            {
                failure = new ContractDiagnostic(
                    "FullCoreDiffusionDataPack.EnergyGroupOrder.Unsupported",
                    "energy_group_order",
                    "The canonical two-group order is fast followed by thermal.");
                return false;
            }

            groupOrder = new[] { first, second };
            failure = null!;
            return true;
        }

        private static bool TryReadString(
            JObject parent,
            string propertyName,
            string path,
            out string value,
            out ContractDiagnostic failure)
        {
            value = string.Empty;
            JToken? token = parent[propertyName];
            if (token == null || token.Type != JTokenType.String ||
                string.IsNullOrWhiteSpace(token.Value<string>()))
            {
                failure = new ContractDiagnostic(
                    "FullCoreDiffusionDataPack.String.Required",
                    path,
                    "A non-empty string value is required.");
                return false;
            }

            value = token.Value<string>()!;
            failure = null!;
            return true;
        }

        private static bool TryReadUInt32(
            JObject parent,
            string propertyName,
            string path,
            out uint value,
            out ContractDiagnostic failure)
        {
            value = 0;
            JToken? token = parent[propertyName];
            if (token == null || token.Type != JTokenType.Integer)
            {
                failure = new ContractDiagnostic(
                    "FullCoreDiffusionDataPack.Integer.Required",
                    path,
                    "A nonnegative integer value is required.");
                return false;
            }

            try
            {
                long parsed = token.Value<long>();
                if (parsed < 0 || parsed > uint.MaxValue)
                {
                    throw new OverflowException();
                }

                value = (uint)parsed;
            }
            catch (Exception exception) when (exception is FormatException || exception is OverflowException)
            {
                failure = new ContractDiagnostic(
                    "FullCoreDiffusionDataPack.Integer.Invalid",
                    path,
                    "The integer value is outside the supported range.");
                return false;
            }

            failure = null!;
            return true;
        }

        private static bool TryReadInt32(
            JObject parent,
            string propertyName,
            string path,
            out int value,
            out ContractDiagnostic failure)
        {
            value = 0;
            JToken? token = parent[propertyName];
            if (token == null || token.Type != JTokenType.Integer)
            {
                failure = new ContractDiagnostic(
                    "FullCoreDiffusionDataPack.Integer.Required",
                    path,
                    "A signed integer value is required.");
                return false;
            }

            try
            {
                value = token.Value<int>();
            }
            catch (Exception exception) when (exception is FormatException || exception is OverflowException)
            {
                failure = new ContractDiagnostic(
                    "FullCoreDiffusionDataPack.Integer.Invalid",
                    path,
                    "The signed integer value is outside the supported range.");
                return false;
            }

            failure = null!;
            return true;
        }

        private static bool TryReadDouble(
            JObject parent,
            string propertyName,
            string path,
            out double value,
            out ContractDiagnostic failure)
        {
            value = 0.0;
            JToken? token = parent[propertyName];
            if (token == null ||
                (token.Type != JTokenType.Integer && token.Type != JTokenType.Float))
            {
                failure = new ContractDiagnostic(
                    "FullCoreDiffusionDataPack.Number.Required",
                    path,
                    "A finite JSON number is required.");
                return false;
            }

            try
            {
                value = token.Value<double>();
            }
            catch (Exception exception) when (exception is FormatException || exception is OverflowException)
            {
                failure = new ContractDiagnostic(
                    "FullCoreDiffusionDataPack.Number.Invalid",
                    path,
                    "The JSON number could not be represented as a double.");
                return false;
            }

            if (!ContractValidation.IsFinite(value))
            {
                failure = new ContractDiagnostic(
                    "FullCoreDiffusionDataPack.Number.NonFinite",
                    path,
                    "Pack numbers must be finite doubles.");
                return false;
            }

            failure = null!;
            return true;
        }

        private static bool TryReadStableId(
            JObject parent,
            string propertyName,
            string path,
            out StableId value,
            out ContractDiagnostic failure)
        {
            value = StableId.Empty;
            if (!TryReadString(parent, propertyName, path, out string text, out failure) ||
                !StableId.TryParse(text, out value))
            {
                failure = new ContractDiagnostic(
                    "FullCoreDiffusionDataPack.StableId.Invalid",
                    path,
                    "The value must be a canonical UUID in D format.");
                return false;
            }

            failure = null!;
            return true;
        }

        private static bool TryReadDigest(
            JObject parent,
            string propertyName,
            string path,
            out Digest32 value,
            out ContractDiagnostic failure)
        {
            value = null!;
            if (!TryReadString(parent, propertyName, path, out string text, out failure) ||
                text.Length != 64)
            {
                failure = new ContractDiagnostic(
                    "FullCoreDiffusionDataPack.Digest.Invalid",
                    path,
                    "A digest must be exactly 64 hexadecimal characters.");
                return false;
            }

            var bytes = new byte[32];
            for (int index = 0; index < bytes.Length; index++)
            {
                if (!byte.TryParse(
                        text.AsSpan(index * 2, 2),
                        NumberStyles.AllowHexSpecifier,
                        CultureInfo.InvariantCulture,
                        out bytes[index]))
                {
                    failure = new ContractDiagnostic(
                        "FullCoreDiffusionDataPack.Digest.Invalid",
                        path,
                        "A digest must contain only hexadecimal characters.");
                    return false;
                }
            }

            value = new Digest32(bytes);
            failure = null!;
            return true;
        }

        private static bool TryReadObject(
            JObject parent,
            string propertyName,
            string path,
            out JObject value,
            out ContractDiagnostic failure)
        {
            value = null!;
            if (!(parent[propertyName] is JObject objectValue))
            {
                failure = new ContractDiagnostic(
                    "FullCoreDiffusionDataPack.Object.Required",
                    path,
                    "A JSON object value is required.");
                return false;
            }

            value = objectValue;
            failure = null!;
            return true;
        }

        private static bool TryReadArray(
            JObject parent,
            string propertyName,
            string path,
            out JArray value,
            out ContractDiagnostic failure)
        {
            value = null!;
            if (!(parent[propertyName] is JArray arrayValue))
            {
                failure = new ContractDiagnostic(
                    "FullCoreDiffusionDataPack.Array.Required",
                    path,
                    "A JSON array value is required.");
                return false;
            }

            value = arrayValue;
            failure = null!;
            return true;
        }

        private static ContractValidationResult<FullCoreDiffusionDataPackV1> Invalid(
            ContractDiagnostic diagnostic)
        {
            return Invalid(diagnostic.Code, diagnostic.Path, diagnostic.Message);
        }

        private static ContractValidationResult<FullCoreDiffusionDataPackV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<FullCoreDiffusionDataPackV1>.Invalid(code, path, message);
        }

        private static ContractValidationResult<BurnupCoefficientTableV1> InvalidTable(
            ContractDiagnostic diagnostic)
        {
            return ContractValidationResult<BurnupCoefficientTableV1>.Invalid(
                diagnostic.Code,
                diagnostic.Path,
                diagnostic.Message);
        }

        private static byte[] Sha256(string value)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                return sha256.ComputeHash(Encoding.UTF8.GetBytes(value));
            }
        }

        private static bool LooksLikePrivateRuntimePath(string value)
        {
            if (value.StartsWith("file:", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("\\\\", StringComparison.Ordinal) ||
                value.StartsWith('/') ||
                (value.Length >= 3 && char.IsLetter(value[0]) && value[1] == ':' &&
                 (value[2] == '\\' || value[2] == '/')))
            {
                return true;
            }

            bool hasUriScheme = value.IndexOf("://", StringComparison.Ordinal) > 0;
            return !hasUriScheme && (value.Contains('\\') || value.Contains('/'));
        }
    }
}
