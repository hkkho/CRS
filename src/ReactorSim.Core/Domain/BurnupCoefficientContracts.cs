using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ReactorSim.Core
{
    /// <summary>
    /// The material-dependent scalar coefficients required by the approved
    /// P2-T02 two-group model. Geometry volume is intentionally not included:
    /// it belongs to the validated spatial node, not the burnup table row.
    /// All cross sections are SI m^-1 and energy per fission is SI joules.
    /// </summary>
    public sealed class BurnupCoefficientValuesV1
    {
        private BurnupCoefficientValuesV1(
            double absorptionGroup1PerM,
            double absorptionGroup2PerM,
            double fissionGroup1PerM,
            double fissionGroup2PerM,
            double nuFissionGroup1PerM,
            double nuFissionGroup2PerM,
            double downscatterGroup1To2PerM,
            double chiGroup1,
            double energyPerFissionJ)
        {
            AbsorptionGroup1PerM = absorptionGroup1PerM;
            AbsorptionGroup2PerM = absorptionGroup2PerM;
            FissionGroup1PerM = fissionGroup1PerM;
            FissionGroup2PerM = fissionGroup2PerM;
            NuFissionGroup1PerM = nuFissionGroup1PerM;
            NuFissionGroup2PerM = nuFissionGroup2PerM;
            DownscatterGroup1To2PerM = downscatterGroup1To2PerM;
            ChiGroup1 = chiGroup1;
            EnergyPerFissionJ = energyPerFissionJ;
        }

        public double AbsorptionGroup1PerM { get; }

        public double AbsorptionGroup2PerM { get; }

        public double FissionGroup1PerM { get; }

        public double FissionGroup2PerM { get; }

        public double NuFissionGroup1PerM { get; }

        public double NuFissionGroup2PerM { get; }

        public double DownscatterGroup1To2PerM { get; }

        /// <summary>
        /// Canonical primary fission-spectrum component. ChiGroup2 is always
        /// derived from this value and is never independently interpolated.
        /// </summary>
        public double ChiGroup1 { get; }

        public double ChiGroup2
        {
            get { return 1.0 - ChiGroup1; }
        }

        public double EnergyPerFissionJ { get; }

        public static ContractValidationResult<BurnupCoefficientValuesV1> TryCreate(
            double absorptionGroup1PerM,
            double absorptionGroup2PerM,
            double fissionGroup1PerM,
            double fissionGroup2PerM,
            double nuFissionGroup1PerM,
            double nuFissionGroup2PerM,
            double downscatterGroup1To2PerM,
            double chiGroup1,
            double energyPerFissionJ)
        {
            double[] values =
            {
                absorptionGroup1PerM,
                absorptionGroup2PerM,
                fissionGroup1PerM,
                fissionGroup2PerM,
                nuFissionGroup1PerM,
                nuFissionGroup2PerM,
                downscatterGroup1To2PerM,
                chiGroup1,
                energyPerFissionJ
            };
            if (values.Any(value => !ContractValidation.IsFinite(value)))
            {
                return Invalid(
                    "BurnupCoefficientValues.NonFinite",
                    "coefficients",
                    "Every burnup coefficient must be a finite double.");
            }

            if (energyPerFissionJ <= 0)
            {
                return Invalid(
                    "BurnupCoefficientValues.EnergyPerFission.Invalid",
                    "coefficients.energy_per_fission_j",
                    "Energy per fission must be strictly positive SI joules.");
            }

            double[] nonnegativeValues =
            {
                absorptionGroup1PerM,
                absorptionGroup2PerM,
                fissionGroup1PerM,
                fissionGroup2PerM,
                nuFissionGroup1PerM,
                nuFissionGroup2PerM,
                downscatterGroup1To2PerM
            };
            if (nonnegativeValues.Any(value => value < 0))
            {
                return Invalid(
                    "BurnupCoefficientValues.Negative",
                    "coefficients",
                    "Cross sections and downscatter must be nonnegative SI m^-1 values.");
            }

            if (absorptionGroup1PerM < fissionGroup1PerM ||
                absorptionGroup2PerM < fissionGroup2PerM)
            {
                return Invalid(
                    "BurnupCoefficientValues.AbsorptionBelowFission",
                    "coefficients",
                    "Total absorption must be greater than or equal to fission absorption in each group.");
            }

            if (chiGroup1 < 0 || chiGroup1 > 1)
            {
                return Invalid(
                    "BurnupCoefficientValues.Chi1.OutOfRange",
                    "coefficients.chi_1",
                    "The canonical primary fission-spectrum component must lie in [0,1].");
            }

            double chiGroup2 = 1.0 - chiGroup1;
            if (!ContractValidation.IsFinite(chiGroup2) || chiGroup2 < 0 || chiGroup2 > 1)
            {
                return Invalid(
                    "BurnupCoefficientValues.Chi2.Invalid",
                    "coefficients.chi_2",
                    "The derived fission-spectrum component must be finite and lie in [0,1].");
            }

            bool group1FissionZero = fissionGroup1PerM == 0;
            bool group1NuFissionZero = nuFissionGroup1PerM == 0;
            bool group2FissionZero = fissionGroup2PerM == 0;
            bool group2NuFissionZero = nuFissionGroup2PerM == 0;
            if (group1FissionZero != group1NuFissionZero ||
                group2FissionZero != group2NuFissionZero)
            {
                return Invalid(
                    "BurnupCoefficientValues.FissionSupportMismatch",
                    "coefficients",
                    "Sigma_f must be zero if and only if nuSigma_f is zero in each group.");
            }

            if ((fissionGroup1PerM > 0 && !HasFinitePositiveYield(
                    nuFissionGroup1PerM,
                    fissionGroup1PerM)) ||
                (fissionGroup2PerM > 0 && !HasFinitePositiveYield(
                    nuFissionGroup2PerM,
                    fissionGroup2PerM)))
            {
                return Invalid(
                    "BurnupCoefficientValues.ImpliedYield.Invalid",
                    "coefficients.nu_sigma_f",
                    "A nonzero fission field must imply a finite, strictly positive neutron yield.");
            }

            return ContractValidationResult<BurnupCoefficientValuesV1>.Valid(
                new BurnupCoefficientValuesV1(
                    absorptionGroup1PerM,
                    absorptionGroup2PerM,
                    fissionGroup1PerM,
                    fissionGroup2PerM,
                    nuFissionGroup1PerM,
                    nuFissionGroup2PerM,
                    downscatterGroup1To2PerM,
                    chiGroup1,
                    energyPerFissionJ));
        }

        private static bool HasFinitePositiveYield(double nuFission, double fission)
        {
            double yield = nuFission / fission;
            return ContractValidation.IsFinite(yield) && yield > 0;
        }

        private static ContractValidationResult<BurnupCoefficientValuesV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<BurnupCoefficientValuesV1>.Invalid(code, path, message);
        }
    }

    /// <summary>
    /// One strictly ordered burnup knot and its validated material
    /// coefficients. Burnup is authoritative SI J/kg_HM.
    /// </summary>
    public sealed class BurnupCoefficientRowV1
    {
        private BurnupCoefficientRowV1(
            double burnupJPerKgHm,
            BurnupCoefficientValuesV1 coefficients)
        {
            BurnupJPerKgHm = burnupJPerKgHm;
            Coefficients = coefficients;
        }

        public double BurnupJPerKgHm { get; }

        public BurnupCoefficientValuesV1 Coefficients { get; }

        public static ContractValidationResult<BurnupCoefficientRowV1> TryCreate(
            double burnupJPerKgHm,
            BurnupCoefficientValuesV1 coefficients)
        {
            if (!ContractValidation.IsFinite(burnupJPerKgHm) || burnupJPerKgHm < 0)
            {
                return ContractValidationResult<BurnupCoefficientRowV1>.Invalid(
                    "BurnupCoefficientRow.Burnup.Invalid",
                    "burnup_j_per_kg_hm",
                    "A table knot must be finite and nonnegative SI J/kg_HM.");
            }

            if (coefficients == null)
            {
                return ContractValidationResult<BurnupCoefficientRowV1>.Invalid(
                    "BurnupCoefficientRow.Coefficients.Missing",
                    "coefficients",
                    "Every burnup knot requires a validated coefficient set.");
            }

            return ContractValidationResult<BurnupCoefficientRowV1>.Valid(
                new BurnupCoefficientRowV1(burnupJPerKgHm, coefficients));
        }
    }

    /// <summary>
    /// Immutable, versioned material table with strict burnup knots. The
    /// checksum is an opaque data-pack identity supplied by the loader; this
    /// bounded contract does not invent a canonical table-byte serializer.
    /// </summary>
    public sealed class BurnupCoefficientTableV1
    {
        public const uint CurrentSchemaVersion = 1;

        private readonly ReadOnlyCollection<BurnupCoefficientRowV1> _rows;

        private BurnupCoefficientTableV1(
            StableId tableId,
            uint schemaVersion,
            string dataVersion,
            MaterialVariantId materialVariantId,
            string unitsProfileId,
            string sourceProvenance,
            Digest32 checksum,
            IEnumerable<BurnupCoefficientRowV1> rows)
        {
            TableId = tableId;
            SchemaVersion = schemaVersion;
            DataVersion = dataVersion;
            MaterialVariantId = materialVariantId;
            UnitsProfileId = unitsProfileId;
            SourceProvenance = sourceProvenance;
            Checksum = checksum;
            _rows = new ReadOnlyCollection<BurnupCoefficientRowV1>(rows.ToArray());
        }

        public StableId TableId { get; }

        public uint SchemaVersion { get; }

        public string DataVersion { get; }

        public MaterialVariantId MaterialVariantId { get; }

        public string UnitsProfileId { get; }

        public string SourceProvenance { get; }

        public Digest32 Checksum { get; }

        public IReadOnlyList<BurnupCoefficientRowV1> Rows
        {
            get { return _rows; }
        }

        public static ContractValidationResult<BurnupCoefficientTableV1> TryCreate(
            StableId tableId,
            uint schemaVersion,
            string dataVersion,
            MaterialVariantId materialVariantId,
            string unitsProfileId,
            string sourceProvenance,
            Digest32 checksum,
            IEnumerable<BurnupCoefficientRowV1> rows)
        {
            if (tableId.IsEmpty)
            {
                return Invalid(
                    "BurnupCoefficientTable.Id.Empty",
                    "table_id",
                    "A coefficient table requires an explicit stable identity.");
            }

            if (schemaVersion != CurrentSchemaVersion)
            {
                return Invalid(
                    "BurnupCoefficientTable.SchemaVersion.Unsupported",
                    "schema_version",
                    "Only burnup coefficient table schema version 1 is supported.");
            }

            if (string.IsNullOrWhiteSpace(dataVersion))
            {
                return Invalid(
                    "BurnupCoefficientTable.DataVersion.Empty",
                    "data_version",
                    "A coefficient table requires an explicit data version.");
            }

            if (string.IsNullOrWhiteSpace(materialVariantId.Value))
            {
                return Invalid(
                    "BurnupCoefficientTable.MaterialVariant.Empty",
                    "material_variant_id",
                    "A coefficient table requires an explicit material variant key.");
            }

            if (string.IsNullOrWhiteSpace(unitsProfileId))
            {
                return Invalid(
                    "BurnupCoefficientTable.UnitsProfile.Empty",
                    "units_profile_id",
                    "A coefficient table requires explicit unit metadata.");
            }

            if (string.IsNullOrWhiteSpace(sourceProvenance))
            {
                return Invalid(
                    "BurnupCoefficientTable.SourceProvenance.Empty",
                    "source_provenance",
                    "A coefficient table requires explicit source provenance.");
            }

            if (LooksLikePrivateRuntimePath(sourceProvenance))
            {
                return Invalid(
                    "BurnupCoefficientTable.SourceProvenance.Path",
                    "source_provenance",
                    "Source provenance may not carry a runtime file path to a private listing.");
            }

            if (checksum == null)
            {
                return Invalid(
                    "BurnupCoefficientTable.Checksum.Missing",
                    "checksum",
                    "A coefficient table requires an explicit 32-byte checksum identity.");
            }

            if (rows == null)
            {
                return Invalid(
                    "BurnupCoefficientTable.Rows.Missing",
                    "rows",
                    "A coefficient table requires its ordered burnup rows.");
            }

            BurnupCoefficientRowV1[] records = rows.ToArray();
            if (records.Length == 0)
            {
                return Invalid(
                    "BurnupCoefficientTable.Rows.Empty",
                    "rows",
                    "A coefficient table requires at least one burnup row.");
            }

            double previousBurnup = 0;
            for (int index = 0; index < records.Length; index++)
            {
                BurnupCoefficientRowV1 row = records[index];
                string path = "rows[" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]";
                if (row == null)
                {
                    return Invalid(
                        "BurnupCoefficientTable.Row.Null",
                        path,
                        "A coefficient row may not be null.");
                }

                if (!ContractValidation.IsFinite(row.BurnupJPerKgHm) || row.BurnupJPerKgHm < 0)
                {
                    return Invalid(
                        "BurnupCoefficientTable.Row.Burnup.Invalid",
                        path + ".burnup_j_per_kg_hm",
                        "Every table knot must be finite and nonnegative SI J/kg_HM.");
                }

                if (index > 0 && row.BurnupJPerKgHm <= previousBurnup)
                {
                    return Invalid(
                        "BurnupCoefficientTable.Knots.NotStrictlyIncreasing",
                        path + ".burnup_j_per_kg_hm",
                        "Burnup knots must be strictly increasing in canonical input order.");
                }

                previousBurnup = row.BurnupJPerKgHm;
            }

            return ContractValidationResult<BurnupCoefficientTableV1>.Valid(
                new BurnupCoefficientTableV1(
                    tableId,
                    schemaVersion,
                    dataVersion,
                    materialVariantId,
                    unitsProfileId,
                    sourceProvenance,
                    checksum,
                    records));
        }

        public ContractValidationResult<BurnupCoefficientLookupResultV1> TryLookup(
            double burnupJPerKgHm)
        {
            if (!ContractValidation.IsFinite(burnupJPerKgHm) || burnupJPerKgHm < 0)
            {
                return LookupInvalid(
                    "BurnupCoefficientLookup.Burnup.Invalid",
                    "burnup_j_per_kg_hm",
                    "Lookup burnup must be finite and nonnegative SI J/kg_HM.");
            }

            if (burnupJPerKgHm < _rows[0].BurnupJPerKgHm ||
                burnupJPerKgHm > _rows[_rows.Count - 1].BurnupJPerKgHm)
            {
                return LookupInvalid(
                    "BurnupCoefficientLookup.OutOfRange",
                    "burnup_j_per_kg_hm",
                    "Burnup outside the table domain must be rejected without extrapolation or clamping.");
            }

            int exactIndex = FindExactRow(burnupJPerKgHm);
            if (exactIndex >= 0)
            {
                double exactFraction = exactIndex == _rows.Count - 1 && _rows.Count > 1 ? 1.0 : 0.0;
                return LookupResult(
                    exactIndex,
                    exactIndex,
                    exactFraction,
                    burnupJPerKgHm,
                    _rows[exactIndex].Coefficients);
            }

            int upperIndex = FindUpperRow(burnupJPerKgHm);
            int lowerIndex = upperIndex - 1;
            double lowerBurnup = _rows[lowerIndex].BurnupJPerKgHm;
            double upperBurnup = _rows[upperIndex].BurnupJPerKgHm;
            double denominator = upperBurnup - lowerBurnup;
            double alpha = (burnupJPerKgHm - lowerBurnup) / denominator;
            if (!ContractValidation.IsFinite(denominator) || denominator <= 0 ||
                !ContractValidation.IsFinite(alpha) || alpha <= 0 || alpha >= 1)
            {
                return LookupInvalid(
                    "BurnupCoefficientLookup.Bracket.Invalid",
                    "bracket",
                    "The selected burnup bracket must produce a finite fraction strictly between zero and one.");
            }

            BurnupCoefficientValuesV1 lower = _rows[lowerIndex].Coefficients;
            BurnupCoefficientValuesV1 upper = _rows[upperIndex].Coefficients;
            ContractValidationResult<BurnupCoefficientValuesV1> interpolated =
                BurnupCoefficientValuesV1.TryCreate(
                    Interpolate(lower.AbsorptionGroup1PerM, upper.AbsorptionGroup1PerM, alpha),
                    Interpolate(lower.AbsorptionGroup2PerM, upper.AbsorptionGroup2PerM, alpha),
                    Interpolate(lower.FissionGroup1PerM, upper.FissionGroup1PerM, alpha),
                    Interpolate(lower.FissionGroup2PerM, upper.FissionGroup2PerM, alpha),
                    Interpolate(lower.NuFissionGroup1PerM, upper.NuFissionGroup1PerM, alpha),
                    Interpolate(lower.NuFissionGroup2PerM, upper.NuFissionGroup2PerM, alpha),
                    Interpolate(lower.DownscatterGroup1To2PerM, upper.DownscatterGroup1To2PerM, alpha),
                    Interpolate(lower.ChiGroup1, upper.ChiGroup1, alpha),
                    Interpolate(lower.EnergyPerFissionJ, upper.EnergyPerFissionJ, alpha));
            if (!interpolated.IsValid)
            {
                return LookupInvalid(
                    "BurnupCoefficientLookup.InterpolatedValues.Invalid",
                    interpolated.FirstDiagnostic.Path,
                    interpolated.FirstDiagnostic.Message);
            }

            return LookupResult(
                lowerIndex,
                upperIndex,
                alpha,
                burnupJPerKgHm,
                interpolated.Value);
        }

        private ContractValidationResult<BurnupCoefficientLookupResultV1> LookupResult(
            int lowerIndex,
            int upperIndex,
            double interpolationFraction,
            double burnupJPerKgHm,
            BurnupCoefficientValuesV1 coefficients)
        {
            return ContractValidationResult<BurnupCoefficientLookupResultV1>.Valid(
                new BurnupCoefficientLookupResultV1(
                    TableId,
                    MaterialVariantId,
                    UnitsProfileId,
                    Checksum,
                    lowerIndex,
                    upperIndex,
                    interpolationFraction,
                    burnupJPerKgHm,
                    coefficients));
        }

        private int FindExactRow(double burnupJPerKgHm)
        {
            int low = 0;
            int high = _rows.Count - 1;
            while (low <= high)
            {
                int middle = low + ((high - low) / 2);
                double candidate = _rows[middle].BurnupJPerKgHm;
                if (candidate == burnupJPerKgHm)
                {
                    return middle;
                }

                if (candidate < burnupJPerKgHm)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle - 1;
                }
            }

            return -1;
        }

        private int FindUpperRow(double burnupJPerKgHm)
        {
            int low = 0;
            int high = _rows.Count - 1;
            while (low < high)
            {
                int middle = low + ((high - low) / 2);
                if (_rows[middle].BurnupJPerKgHm < burnupJPerKgHm)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle;
                }
            }

            return low;
        }

        private static double Interpolate(double lower, double upper, double alpha)
        {
            return ((1.0 - alpha) * lower) + (alpha * upper);
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

        private static ContractValidationResult<BurnupCoefficientTableV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<BurnupCoefficientTableV1>.Invalid(code, path, message);
        }

        private static ContractValidationResult<BurnupCoefficientLookupResultV1> LookupInvalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<BurnupCoefficientLookupResultV1>.Invalid(code, path, message);
        }
    }

    /// <summary>
    /// Immutable lookup result with the exact input burnup, canonical bracket,
    /// interpolation fraction, units, table identity, and checksum identity.
    /// </summary>
    public sealed class BurnupCoefficientLookupResultV1
    {
        internal BurnupCoefficientLookupResultV1(
            StableId tableId,
            MaterialVariantId materialVariantId,
            string unitsProfileId,
            Digest32 checksum,
            int bracketLowerIndex,
            int bracketUpperIndex,
            double interpolationFraction,
            double inputBurnupJPerKgHm,
            BurnupCoefficientValuesV1 coefficients)
        {
            TableId = tableId;
            MaterialVariantId = materialVariantId;
            UnitsProfileId = unitsProfileId;
            Checksum = checksum;
            BracketLowerIndex = bracketLowerIndex;
            BracketUpperIndex = bracketUpperIndex;
            InterpolationFraction = interpolationFraction;
            InputBurnupJPerKgHm = inputBurnupJPerKgHm;
            Coefficients = coefficients;
        }

        public StableId TableId { get; }

        public MaterialVariantId MaterialVariantId { get; }

        public string UnitsProfileId { get; }

        public Digest32 Checksum { get; }

        public int BracketLowerIndex { get; }

        public int BracketUpperIndex { get; }

        public double InterpolationFraction { get; }

        public double InputBurnupJPerKgHm { get; }

        public BurnupCoefficientValuesV1 Coefficients { get; }
    }
}
