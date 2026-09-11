using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ReactorSim.Core
{
    /// <summary>
    /// Describes how one expected two-group quantity is represented by a
    /// coefficient pack. This is an audit classification, not a physics
    /// calibration claim.
    /// </summary>
    public enum CoefficientAuditClassificationV1 : byte
    {
        Missing = 0,
        ExplicitBurnupDependent = 1,
        ExplicitConstant = 2,
        Derived = 3,
        IndirectConstant = 4
    }

    /// <summary>
    /// Stable names for the two-group coefficient quantities and the two
    /// pack-level metadata fields covered by the Slice 0 audit.
    /// </summary>
    public enum DiffusionAuditFieldV1 : byte
    {
        D1 = 1,
        D2 = 2,
        SigmaA1 = 3,
        SigmaA2 = 4,
        SigmaS1To2 = 5,
        SigmaF1 = 6,
        SigmaF2 = 7,
        NuSigmaF1 = 8,
        NuSigmaF2 = 9,
        Chi1 = 10,
        Chi2 = 11,
        EnergyPerFission = 12,
        XenonBasis = 13
    }

    /// <summary>
    /// Deterministic checks applied to one burnup coefficient table and its
    /// containing pack geometry.
    /// </summary>
    public enum DiffusionPackAuditCheckIdV1 : byte
    {
        BurnupSiConversion = 1,
        TableDomain = 2,
        ExactKnotLookup = 3,
        PiecewiseLinearInterpolation = 4,
        NoExtrapolation = 5,
        FiniteNonnegativeValues = 6,
        AbsorptionAtLeastFission = 7,
        NuSigmaFToSigmaFRatios = 8,
        ChiSum = 9,
        ConstantGeometryConductances = 10
    }

    /// <summary>
    /// Immutable classification of one audited field for one material table.
    /// </summary>
    public sealed class DiffusionPackAuditFindingV1
    {
        internal DiffusionPackAuditFindingV1(
            DiffusionAuditFieldV1 field,
            CoefficientAuditClassificationV1 classification,
            StableId tableId,
            string materialVariantId,
            string message)
        {
            Field = field;
            Classification = classification;
            TableId = tableId;
            MaterialVariantId = materialVariantId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public DiffusionAuditFieldV1 Field { get; }

        public CoefficientAuditClassificationV1 Classification { get; }

        public StableId TableId { get; }

        public string MaterialVariantId { get; }

        public bool IsPackLevel
        {
            get { return TableId.IsEmpty && string.IsNullOrEmpty(MaterialVariantId); }
        }

        public string Message { get; }
    }

    /// <summary>
    /// Immutable result of one deterministic audit check.
    /// </summary>
    public sealed class DiffusionPackAuditCheckV1
    {
        internal DiffusionPackAuditCheckV1(
            DiffusionPackAuditCheckIdV1 checkId,
            StableId tableId,
            string materialVariantId,
            bool passed,
            string message)
        {
            CheckId = checkId;
            TableId = tableId;
            MaterialVariantId = materialVariantId ?? string.Empty;
            Passed = passed;
            Message = message ?? string.Empty;
        }

        public DiffusionPackAuditCheckIdV1 CheckId { get; }

        public StableId TableId { get; }

        public string MaterialVariantId { get; }

        public bool Passed { get; }

        public string Message { get; }
    }

    /// <summary>
    /// Immutable audit result for one material's burnup table.
    /// </summary>
    public sealed class DiffusionCoefficientTableAuditV1
    {
        private readonly ReadOnlyCollection<double> _burnupKnotsJPerKgHm;
        private readonly ReadOnlyCollection<double> _burnupKnotsMwDayPerKgHm;
        private readonly ReadOnlyCollection<DiffusionPackAuditFindingV1> _findings;
        private readonly ReadOnlyCollection<DiffusionPackAuditCheckV1> _checks;

        internal DiffusionCoefficientTableAuditV1(
            StableId tableId,
            MaterialVariantId materialVariantId,
            IEnumerable<double> burnupKnotsJPerKgHm,
            IEnumerable<double> burnupKnotsMwDayPerKgHm,
            IEnumerable<DiffusionPackAuditFindingV1> findings,
            IEnumerable<DiffusionPackAuditCheckV1> checks)
        {
            TableId = tableId;
            MaterialVariantId = materialVariantId;
            _burnupKnotsJPerKgHm = new ReadOnlyCollection<double>(
                (burnupKnotsJPerKgHm ?? Array.Empty<double>()).ToArray());
            _burnupKnotsMwDayPerKgHm = new ReadOnlyCollection<double>(
                (burnupKnotsMwDayPerKgHm ?? Array.Empty<double>()).ToArray());
            _findings = new ReadOnlyCollection<DiffusionPackAuditFindingV1>(
                (findings ?? Array.Empty<DiffusionPackAuditFindingV1>()).ToArray());
            _checks = new ReadOnlyCollection<DiffusionPackAuditCheckV1>(
                (checks ?? Array.Empty<DiffusionPackAuditCheckV1>()).ToArray());
        }

        public StableId TableId { get; }

        public MaterialVariantId MaterialVariantId { get; }

        public IReadOnlyList<double> BurnupKnotsJPerKgHm
        {
            get { return _burnupKnotsJPerKgHm; }
        }

        public IReadOnlyList<double> BurnupKnotsMwDayPerKgHm
        {
            get { return _burnupKnotsMwDayPerKgHm; }
        }

        public IReadOnlyList<DiffusionPackAuditFindingV1> Findings
        {
            get { return _findings; }
        }

        public IReadOnlyList<DiffusionPackAuditCheckV1> Checks
        {
            get { return _checks; }
        }

        public bool Passed
        {
            get { return _checks.Count > 0 && _checks.All(check => check.Passed); }
        }

        public DiffusionPackAuditFindingV1 GetFinding(DiffusionAuditFieldV1 field)
        {
            DiffusionPackAuditFindingV1? finding = _findings.FirstOrDefault(
                candidate => candidate.Field == field);
            if (finding == null)
            {
                throw new KeyNotFoundException(
                    "The audited table does not contain field " + field + ".");
            }

            return finding;
        }
    }

    /// <summary>
    /// Report-only, deterministic audit of a full-core diffusion pack. A
    /// missing xenon basis and synthetic provenance are findings in this
    /// report; they do not reject the current gameplay pack.
    /// </summary>
    public sealed class DiffusionDataPackAuditV1
    {
        private readonly ReadOnlyCollection<DiffusionCoefficientTableAuditV1> _tables;
        private readonly ReadOnlyCollection<DiffusionPackAuditFindingV1> _findings;
        private readonly ReadOnlyCollection<DiffusionPackAuditCheckV1> _checks;

        private DiffusionDataPackAuditV1(
            FullCoreDiffusionDataPackV1 dataPack,
            bool syntheticOrProvisional,
            IEnumerable<DiffusionCoefficientTableAuditV1> tables,
            IEnumerable<DiffusionPackAuditFindingV1> findings,
            IEnumerable<DiffusionPackAuditCheckV1> checks)
        {
            DataPackId = dataPack.Descriptor.DataPackId;
            DataPackVersion = dataPack.Descriptor.DataPackVersion;
            IsSyntheticOrProvisional = syntheticOrProvisional;
            _tables = new ReadOnlyCollection<DiffusionCoefficientTableAuditV1>(
                (tables ?? Array.Empty<DiffusionCoefficientTableAuditV1>()).ToArray());
            _findings = new ReadOnlyCollection<DiffusionPackAuditFindingV1>(
                (findings ?? Array.Empty<DiffusionPackAuditFindingV1>()).ToArray());
            _checks = new ReadOnlyCollection<DiffusionPackAuditCheckV1>(
                (checks ?? Array.Empty<DiffusionPackAuditCheckV1>()).ToArray());
        }

        public StableId DataPackId { get; }

        public string DataPackVersion { get; }

        public bool IsSyntheticOrProvisional { get; }

        public IReadOnlyList<DiffusionCoefficientTableAuditV1> Tables
        {
            get { return _tables; }
        }

        public IReadOnlyList<DiffusionPackAuditFindingV1> Findings
        {
            get { return _findings; }
        }

        public IReadOnlyList<DiffusionPackAuditCheckV1> Checks
        {
            get { return _checks; }
        }

        /// <summary>
        /// True when deterministic coefficient and lookup checks pass. The
        /// missing xenon basis is intentionally not a gameplay blocker in
        /// this migration slice.
        /// </summary>
        public bool IsCurrentGameplayAdmissible
        {
            get
            {
                return _tables.Count > 0 &&
                       _tables.All(table => table.Passed) &&
                       _findings.All(finding =>
                           finding.Field != DiffusionAuditFieldV1.XenonBasis ||
                           finding.Classification == CoefficientAuditClassificationV1.Missing);
            }
        }

        public bool HasMissingXenonBasis
        {
            get
            {
                return _findings.Any(finding =>
                    finding.Field == DiffusionAuditFieldV1.XenonBasis &&
                    finding.Classification == CoefficientAuditClassificationV1.Missing);
            }
        }

        public bool RequiresReplacementForFinalCalibratedPhysics
        {
            get
            {
                return IsSyntheticOrProvisional ||
                       HasMissingXenonBasis ||
                       !IsCurrentGameplayAdmissible;
            }
        }

        public bool IsFinalCalibratedPhysicsReady
        {
            get { return IsCurrentGameplayAdmissible && !RequiresReplacementForFinalCalibratedPhysics; }
        }

        public DiffusionPackAuditFindingV1 GetFinding(DiffusionAuditFieldV1 field)
        {
            DiffusionPackAuditFindingV1? finding = _findings.FirstOrDefault(
                candidate => candidate.Field == field);
            if (finding == null)
            {
                throw new KeyNotFoundException("The audit does not contain field " + field + ".");
            }

            return finding;
        }

        public static ContractValidationResult<DiffusionDataPackAuditV1> TryAudit(
            FullCoreDiffusionDataPackV1 dataPack)
        {
            if (dataPack == null)
            {
                return ContractValidationResult<DiffusionDataPackAuditV1>.Invalid(
                    "DiffusionDataPackAudit.DataPack.Missing",
                    "data_pack",
                    "A diffusion data-pack audit requires a loaded immutable data pack.");
            }

            if (dataPack.CoefficientTables == null || dataPack.CoefficientTables.Count == 0)
            {
                return ContractValidationResult<DiffusionDataPackAuditV1>.Invalid(
                    "DiffusionDataPackAudit.Tables.Empty",
                    "coefficient_tables",
                    "A diffusion data-pack audit requires at least one coefficient table.");
            }

            var tables = new List<DiffusionCoefficientTableAuditV1>(
                dataPack.CoefficientTables.Count);
            var findings = new List<DiffusionPackAuditFindingV1>();
            var checks = new List<DiffusionPackAuditCheckV1>();
            foreach (BurnupCoefficientTableV1 table in dataPack.CoefficientTables)
            {
                DiffusionCoefficientTableAuditV1 tableAudit = AuditTable(dataPack, table);
                tables.Add(tableAudit);
                findings.AddRange(tableAudit.Findings);
                checks.AddRange(tableAudit.Checks);
            }

            // The v1 full-core pack schema carries no declared xenon basis.
            // Do not infer Excluded, Included, or Equilibrium from the solver
            // or from the table provenance.
            findings.Add(new DiffusionPackAuditFindingV1(
                DiffusionAuditFieldV1.XenonBasis,
                CoefficientAuditClassificationV1.Missing,
                StableId.Empty,
                string.Empty,
                "The full-core diffusion pack schema declares no xenon basis; no basis was inferred."));

            bool syntheticOrProvisional =
                ContainsProvisionalMarker(dataPack.EvidenceClass) ||
                ContainsProvisionalMarker(dataPack.SourceProvenance) ||
                ContainsProvisionalMarker(dataPack.SourceToolchain) ||
                ContainsProvisionalMarker(dataPack.TransformId) ||
                dataPack.CoefficientTables.Any(table =>
                    ContainsProvisionalMarker(table.DataVersion) ||
                    ContainsProvisionalMarker(table.SourceProvenance));

            return ContractValidationResult<DiffusionDataPackAuditV1>.Valid(
                new DiffusionDataPackAuditV1(
                    dataPack,
                    syntheticOrProvisional,
                    tables,
                    findings,
                    checks));
        }

        public static ContractValidationResult<DiffusionDataPackAuditV1> TryCreate(
            FullCoreDiffusionDataPackV1 dataPack)
        {
            return TryAudit(dataPack);
        }

        private static DiffusionCoefficientTableAuditV1 AuditTable(
            FullCoreDiffusionDataPackV1 dataPack,
            BurnupCoefficientTableV1 table)
        {
            BurnupCoefficientRowV1[] rows = table.Rows == null
                ? Array.Empty<BurnupCoefficientRowV1>()
                : table.Rows.ToArray();
            double[] burnupKnotsJPerKgHm = rows
                .Where(row => row != null)
                .Select(row => row.BurnupJPerKgHm)
                .ToArray();
            double[] burnupKnotsMwDayPerKgHm = burnupKnotsJPerKgHm
                .Select(burnup => burnup / InfiniteCellDiffusionModelV1.JoulesPerMegaWattDayPerKilogramHm)
                .ToArray();

            string materialVariantId = table.MaterialVariantId.Value;
            var findings = new List<DiffusionPackAuditFindingV1>();
            AddFinding(
                findings,
                table,
                DiffusionAuditFieldV1.D1,
                HasUsableGeometryConductances(dataPack),
                CoefficientAuditClassificationV1.IndirectConstant,
                CoefficientAuditClassificationV1.Missing,
                "D1 is not an explicit burnup-table field; group-1 leakage is represented by constant geometry conductances.");
            AddFinding(
                findings,
                table,
                DiffusionAuditFieldV1.D2,
                HasUsableGeometryConductances(dataPack),
                CoefficientAuditClassificationV1.IndirectConstant,
                CoefficientAuditClassificationV1.Missing,
                "D2 is not an explicit burnup-table field; group-2 leakage is represented by constant geometry conductances.");
            AddExplicitFinding(findings, table, DiffusionAuditFieldV1.SigmaA1, rows, values => values.AbsorptionGroup1PerM);
            AddExplicitFinding(findings, table, DiffusionAuditFieldV1.SigmaA2, rows, values => values.AbsorptionGroup2PerM);
            AddExplicitFinding(findings, table, DiffusionAuditFieldV1.SigmaS1To2, rows, values => values.DownscatterGroup1To2PerM);
            AddExplicitFinding(findings, table, DiffusionAuditFieldV1.SigmaF1, rows, values => values.FissionGroup1PerM);
            AddExplicitFinding(findings, table, DiffusionAuditFieldV1.SigmaF2, rows, values => values.FissionGroup2PerM);
            AddExplicitFinding(findings, table, DiffusionAuditFieldV1.NuSigmaF1, rows, values => values.NuFissionGroup1PerM);
            AddExplicitFinding(findings, table, DiffusionAuditFieldV1.NuSigmaF2, rows, values => values.NuFissionGroup2PerM);
            AddExplicitFinding(findings, table, DiffusionAuditFieldV1.Chi1, rows, values => values.ChiGroup1);
            findings.Add(new DiffusionPackAuditFindingV1(
                DiffusionAuditFieldV1.Chi2,
                CoefficientAuditClassificationV1.Derived,
                table.TableId,
                materialVariantId,
                "chi2 is derived as 1 - chi1 and is not independently interpolated or stored."));
            AddExplicitFinding(findings, table, DiffusionAuditFieldV1.EnergyPerFission, rows, values => values.EnergyPerFissionJ);

            var checks = new List<DiffusionPackAuditCheckV1>
            {
                Check(
                    table,
                    DiffusionPackAuditCheckIdV1.BurnupSiConversion,
                    HasValidBurnupConversion(rows),
                    "Every SI burnup knot converts to a finite nonnegative MWd/kg_HM value using 8.64e10 J/kg_HM per MWd/kg_HM."),
                Check(
                    table,
                    DiffusionPackAuditCheckIdV1.TableDomain,
                    HasValidTableDomain(rows),
                    "Burnup knots are finite, nonnegative, and strictly increasing with a closed lookup domain."),
                Check(
                    table,
                    DiffusionPackAuditCheckIdV1.ExactKnotLookup,
                    HasExactKnotLookups(table, rows),
                    "Every stored knot resolves exactly without interpolation."),
                Check(
                    table,
                    DiffusionPackAuditCheckIdV1.PiecewiseLinearInterpolation,
                    HasPiecewiseLinearInterpolation(table, rows),
                    "Interior lookups match deterministic piecewise-linear interpolation between adjacent knots."),
                Check(
                    table,
                    DiffusionPackAuditCheckIdV1.NoExtrapolation,
                    RejectsExtrapolation(table, rows),
                    "Lookups outside the closed table domain are rejected without extrapolation or clamping."),
                Check(
                    table,
                    DiffusionPackAuditCheckIdV1.FiniteNonnegativeValues,
                    HasFiniteNonnegativeValues(rows),
                    "Cross sections and derived chi values are finite and nonnegative; energy per fission is finite and positive."),
                Check(
                    table,
                    DiffusionPackAuditCheckIdV1.AbsorptionAtLeastFission,
                    AbsorptionDominatesFission(rows),
                    "Sigma_a is greater than or equal to Sigma_f in both energy groups at every knot."),
                Check(
                    table,
                    DiffusionPackAuditCheckIdV1.NuSigmaFToSigmaFRatios,
                    HasCredibleNuRatios(rows),
                    "Each nonzero nuSigma_f/Sigma_f ratio is finite and strictly positive, with zero fission support matched by zero nuSigma_f."),
                Check(
                    table,
                    DiffusionPackAuditCheckIdV1.ChiSum,
                    HasUnitChiSum(rows),
                    "The fission-spectrum components are finite, nonnegative, and sum to one."),
                Check(
                    table,
                    DiffusionPackAuditCheckIdV1.ConstantGeometryConductances,
                    HasUsableGeometryConductances(dataPack),
                    "Group diffusion/leakage is represented by finite positive pack-level geometry conductances, not burnup rows.")
            };

            return new DiffusionCoefficientTableAuditV1(
                table.TableId,
                table.MaterialVariantId,
                burnupKnotsJPerKgHm,
                burnupKnotsMwDayPerKgHm,
                findings,
                checks);
        }

        private static void AddExplicitFinding(
            List<DiffusionPackAuditFindingV1> findings,
            BurnupCoefficientTableV1 table,
            DiffusionAuditFieldV1 field,
            IReadOnlyList<BurnupCoefficientRowV1> rows,
            Func<BurnupCoefficientValuesV1, double> selector)
        {
            CoefficientAuditClassificationV1 classification = ClassifyExplicit(rows, selector);
            string message = classification == CoefficientAuditClassificationV1.ExplicitBurnupDependent
                ? "The field is stored explicitly in SI burnup rows and changes across the table."
                : classification == CoefficientAuditClassificationV1.ExplicitConstant
                    ? "The field is stored explicitly in SI burnup rows and is constant across the table."
                    : "The field is not available in every validated burnup row.";
            findings.Add(new DiffusionPackAuditFindingV1(
                field,
                classification,
                table.TableId,
                table.MaterialVariantId.Value,
                message));
        }

        private static void AddFinding(
            List<DiffusionPackAuditFindingV1> findings,
            BurnupCoefficientTableV1 table,
            DiffusionAuditFieldV1 field,
            bool represented,
            CoefficientAuditClassificationV1 representedClassification,
            CoefficientAuditClassificationV1 missingClassification,
            string message)
        {
            findings.Add(new DiffusionPackAuditFindingV1(
                field,
                represented ? representedClassification : missingClassification,
                table.TableId,
                table.MaterialVariantId.Value,
                message));
        }

        private static CoefficientAuditClassificationV1 ClassifyExplicit(
            IReadOnlyList<BurnupCoefficientRowV1> rows,
            Func<BurnupCoefficientValuesV1, double> selector)
        {
            if (rows == null || rows.Count == 0 || rows.Any(row => row == null || row.Coefficients == null))
            {
                return CoefficientAuditClassificationV1.Missing;
            }

            double first = selector(rows[0].Coefficients);
            for (int index = 1; index < rows.Count; index++)
            {
                if (selector(rows[index].Coefficients) != first)
                {
                    return CoefficientAuditClassificationV1.ExplicitBurnupDependent;
                }
            }

            return CoefficientAuditClassificationV1.ExplicitConstant;
        }

        private static DiffusionPackAuditCheckV1 Check(
            BurnupCoefficientTableV1 table,
            DiffusionPackAuditCheckIdV1 checkId,
            bool passed,
            string message)
        {
            return new DiffusionPackAuditCheckV1(
                checkId,
                table.TableId,
                table.MaterialVariantId.Value,
                passed,
                message);
        }

        private static bool HasValidBurnupConversion(BurnupCoefficientRowV1[] rows)
        {
            if (rows == null || rows.Length == 0)
            {
                return false;
            }

            foreach (BurnupCoefficientRowV1 row in rows)
            {
                if (row == null)
                {
                    return false;
                }

                double converted = row.BurnupJPerKgHm /
                                   InfiniteCellDiffusionModelV1.JoulesPerMegaWattDayPerKilogramHm;
                if (!IsFinite(converted) || converted < 0.0)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasValidTableDomain(BurnupCoefficientRowV1[] rows)
        {
            if (rows == null || rows.Length == 0)
            {
                return false;
            }

            double previous = -1.0;
            foreach (BurnupCoefficientRowV1 row in rows)
            {
                if (row == null || !IsFinite(row.BurnupJPerKgHm) ||
                    row.BurnupJPerKgHm < 0.0 || row.BurnupJPerKgHm <= previous)
                {
                    return false;
                }

                previous = row.BurnupJPerKgHm;
            }

            return true;
        }

        private static bool HasExactKnotLookups(
            BurnupCoefficientTableV1 table,
            BurnupCoefficientRowV1[] rows)
        {
            if (table == null || rows == null || rows.Length == 0)
            {
                return false;
            }

            for (int index = 0; index < rows.Length; index++)
            {
                ContractValidationResult<BurnupCoefficientLookupResultV1> lookup =
                    table.TryLookup(rows[index].BurnupJPerKgHm);
                if (!lookup.IsValid || lookup.Value.BracketLowerIndex != index ||
                    lookup.Value.BracketUpperIndex != index ||
                    !IsAllowedExactFraction(lookup.Value.InterpolationFraction, index, rows.Length) ||
                    !SameValues(lookup.Value.Coefficients, rows[index].Coefficients))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasPiecewiseLinearInterpolation(
            BurnupCoefficientTableV1 table,
            BurnupCoefficientRowV1[] rows)
        {
            if (table == null || rows == null || rows.Length < 2)
            {
                return rows != null && rows.Length == 1;
            }

            for (int index = 0; index + 1 < rows.Length; index++)
            {
                double lowerBurnup = rows[index].BurnupJPerKgHm;
                double upperBurnup = rows[index + 1].BurnupJPerKgHm;
                double midpoint = lowerBurnup + ((upperBurnup - lowerBurnup) * 0.5);
                ContractValidationResult<BurnupCoefficientLookupResultV1> lookup =
                    table.TryLookup(midpoint);
                if (!lookup.IsValid || lookup.Value.BracketLowerIndex != index ||
                    lookup.Value.BracketUpperIndex != index + 1 ||
                    !NearlyEqual(lookup.Value.InterpolationFraction, 0.5) ||
                    !SameValues(
                        lookup.Value.Coefficients,
                        Interpolate(rows[index].Coefficients, rows[index + 1].Coefficients, 0.5)))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool RejectsExtrapolation(
            BurnupCoefficientTableV1 table,
            BurnupCoefficientRowV1[] rows)
        {
            if (table == null || rows == null || rows.Length == 0)
            {
                return false;
            }

            double lowerProbe = rows[0].BurnupJPerKgHm -
                                 Math.Max(1.0, Math.Abs(rows[0].BurnupJPerKgHm) * 1e-6);
            double upperProbe = rows[rows.Length - 1].BurnupJPerKgHm +
                                Math.Max(1.0, Math.Abs(rows[rows.Length - 1].BurnupJPerKgHm) * 1e-6);
            ContractValidationResult<BurnupCoefficientLookupResultV1> lower = table.TryLookup(lowerProbe);
            ContractValidationResult<BurnupCoefficientLookupResultV1> upper = table.TryLookup(upperProbe);
            return !lower.IsValid && !upper.IsValid;
        }

        private static bool HasFiniteNonnegativeValues(BurnupCoefficientRowV1[] rows)
        {
            if (rows == null || rows.Length == 0)
            {
                return false;
            }

            foreach (BurnupCoefficientRowV1 row in rows)
            {
                if (row == null || row.Coefficients == null)
                {
                    return false;
                }

                BurnupCoefficientValuesV1 values = row.Coefficients;
                double[] nonnegative =
                {
                    values.AbsorptionGroup1PerM,
                    values.AbsorptionGroup2PerM,
                    values.FissionGroup1PerM,
                    values.FissionGroup2PerM,
                    values.NuFissionGroup1PerM,
                    values.NuFissionGroup2PerM,
                    values.DownscatterGroup1To2PerM,
                    values.ChiGroup1,
                    values.ChiGroup2
                };
                if (nonnegative.Any(value => !IsFinite(value) || value < 0.0) ||
                    !IsFinite(values.ChiGroup1) || values.ChiGroup1 > 1.0 ||
                    !IsFinite(values.ChiGroup2) || values.ChiGroup2 > 1.0 ||
                    !IsFinite(values.EnergyPerFissionJ) || values.EnergyPerFissionJ <= 0.0)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool AbsorptionDominatesFission(BurnupCoefficientRowV1[] rows)
        {
            return rows != null && rows.Length > 0 && rows.All(row =>
                row != null && row.Coefficients != null &&
                row.Coefficients.AbsorptionGroup1PerM >= row.Coefficients.FissionGroup1PerM &&
                row.Coefficients.AbsorptionGroup2PerM >= row.Coefficients.FissionGroup2PerM);
        }

        private static bool HasCredibleNuRatios(BurnupCoefficientRowV1[] rows)
        {
            if (rows == null || rows.Length == 0)
            {
                return false;
            }

            foreach (BurnupCoefficientRowV1 row in rows)
            {
                if (row == null || row.Coefficients == null ||
                    !HasCredibleNuRatio(
                        row.Coefficients.FissionGroup1PerM,
                        row.Coefficients.NuFissionGroup1PerM) ||
                    !HasCredibleNuRatio(
                        row.Coefficients.FissionGroup2PerM,
                        row.Coefficients.NuFissionGroup2PerM))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasCredibleNuRatio(double fission, double nuFission)
        {
            if (!IsFinite(fission) || !IsFinite(nuFission) || fission < 0.0 || nuFission < 0.0)
            {
                return false;
            }

            if (fission == 0.0)
            {
                return nuFission == 0.0;
            }

            double ratio = nuFission / fission;
            return IsFinite(ratio) && ratio > 0.0;
        }

        private static bool HasUnitChiSum(BurnupCoefficientRowV1[] rows)
        {
            return rows != null && rows.Length > 0 && rows.All(row =>
                row != null && row.Coefficients != null &&
                IsFinite(row.Coefficients.ChiGroup1) &&
                IsFinite(row.Coefficients.ChiGroup2) &&
                row.Coefficients.ChiGroup1 >= 0.0 &&
                row.Coefficients.ChiGroup2 >= 0.0 &&
                NearlyEqual(row.Coefficients.ChiGroup1 + row.Coefficients.ChiGroup2, 1.0));
        }

        private static bool HasUsableGeometryConductances(FullCoreDiffusionDataPackV1 dataPack)
        {
            if (dataPack == null || dataPack.AxialConductance == null ||
                dataPack.TransverseConductance == null || dataPack.VacuumBoundaryConductance == null)
            {
                return false;
            }

            double[] conductances =
            {
                dataPack.AxialConductance.Group1M2,
                dataPack.AxialConductance.Group2M2,
                dataPack.TransverseConductance.Group1M2,
                dataPack.TransverseConductance.Group2M2,
                dataPack.VacuumBoundaryConductance.Group1M2,
                dataPack.VacuumBoundaryConductance.Group2M2
            };
            return conductances.All(value => IsFinite(value) && value > 0.0);
        }

        private static BurnupCoefficientValuesV1 Interpolate(
            BurnupCoefficientValuesV1 lower,
            BurnupCoefficientValuesV1 upper,
            double alpha)
        {
            ContractValidationResult<BurnupCoefficientValuesV1> result =
                BurnupCoefficientValuesV1.TryCreate(
                    Blend(lower.AbsorptionGroup1PerM, upper.AbsorptionGroup1PerM, alpha),
                    Blend(lower.AbsorptionGroup2PerM, upper.AbsorptionGroup2PerM, alpha),
                    Blend(lower.FissionGroup1PerM, upper.FissionGroup1PerM, alpha),
                    Blend(lower.FissionGroup2PerM, upper.FissionGroup2PerM, alpha),
                    Blend(lower.NuFissionGroup1PerM, upper.NuFissionGroup1PerM, alpha),
                    Blend(lower.NuFissionGroup2PerM, upper.NuFissionGroup2PerM, alpha),
                    Blend(lower.DownscatterGroup1To2PerM, upper.DownscatterGroup1To2PerM, alpha),
                    Blend(lower.ChiGroup1, upper.ChiGroup1, alpha),
                    Blend(lower.EnergyPerFissionJ, upper.EnergyPerFissionJ, alpha));
            return result.Value;
        }

        private static bool SameValues(
            BurnupCoefficientValuesV1 left,
            BurnupCoefficientValuesV1 right)
        {
            return left != null && right != null &&
                   NearlyEqual(left.AbsorptionGroup1PerM, right.AbsorptionGroup1PerM) &&
                   NearlyEqual(left.AbsorptionGroup2PerM, right.AbsorptionGroup2PerM) &&
                   NearlyEqual(left.FissionGroup1PerM, right.FissionGroup1PerM) &&
                   NearlyEqual(left.FissionGroup2PerM, right.FissionGroup2PerM) &&
                   NearlyEqual(left.NuFissionGroup1PerM, right.NuFissionGroup1PerM) &&
                   NearlyEqual(left.NuFissionGroup2PerM, right.NuFissionGroup2PerM) &&
                   NearlyEqual(left.DownscatterGroup1To2PerM, right.DownscatterGroup1To2PerM) &&
                   NearlyEqual(left.ChiGroup1, right.ChiGroup1) &&
                   NearlyEqual(left.ChiGroup2, right.ChiGroup2) &&
                   NearlyEqual(left.EnergyPerFissionJ, right.EnergyPerFissionJ);
        }

        private static bool IsAllowedExactFraction(double fraction, int index, int count)
        {
            return index == count - 1
                ? NearlyEqual(fraction, 0.0) || NearlyEqual(fraction, 1.0)
                : NearlyEqual(fraction, 0.0);
        }

        private static bool NearlyEqual(double left, double right)
        {
            if (left == right)
            {
                return true;
            }

            if (!IsFinite(left) || !IsFinite(right))
            {
                return false;
            }

            double scale = Math.Max(1.0, Math.Max(Math.Abs(left), Math.Abs(right)));
            return Math.Abs(left - right) <= scale * 1e-12;
        }

        private static double Blend(double lower, double upper, double alpha)
        {
            return ((1.0 - alpha) * lower) + (alpha * upper);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static bool ContainsProvisionalMarker(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string lowered = value.ToLowerInvariant();
            return lowered.Contains("synthetic") ||
                   lowered.Contains("provisional") ||
                   lowered.Contains("surrogate") ||
                   lowered.Contains("replace with");
        }
    }
}
