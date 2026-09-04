using System;

namespace ReactorSim.Core
{
    /// <summary>
    /// Homogeneous, no-leakage two-group diffusion model for a single CANDU
    /// fuel cell. This is a calibration and unit-conversion model; it is not
    /// a replacement for the finite-core transport or depletion calculation.
    /// Group 1 is fast and group 2 is thermal, matching the full-core pack.
    /// </summary>
    public sealed class InfiniteCellDiffusionModelV1
    {
        public const string ModelId = "infinite-cell-two-group-diffusion-v1";
        public const string BoundaryConditionId = "infinite-no-leakage-v1";
        public const double JoulesPerMegaWattDayPerKilogramHm = 8.64e10;
        public const double SecondsPerDay = 86400.0;
        public const double GramsPerKilogram = 1000.0;

        private readonly BurnupCoefficientValuesV1 _coefficients;
        private readonly double _specificPowerWattsPerGramHm;
        private readonly double _heavyMetalMassKg;
        private readonly double _dischargeBurnupMwDayPerT;

        private InfiniteCellDiffusionModelV1(
            BurnupCoefficientValuesV1 coefficients,
            double specificPowerWattsPerGramHm,
            double heavyMetalMassKg,
            double dischargeBurnupMwDayPerT)
        {
            _coefficients = coefficients;
            _specificPowerWattsPerGramHm = specificPowerWattsPerGramHm;
            _heavyMetalMassKg = heavyMetalMassKg;
            _dischargeBurnupMwDayPerT = dischargeBurnupMwDayPerT;
        }

        public BurnupCoefficientValuesV1 Coefficients
        {
            get { return _coefficients; }
        }

        public double SpecificPowerWattsPerGramHm
        {
            get { return _specificPowerWattsPerGramHm; }
        }

        public double HeavyMetalMassKg
        {
            get { return _heavyMetalMassKg; }
        }

        public double DischargeBurnupMwDayPerT
        {
            get { return _dischargeBurnupMwDayPerT; }
        }

        public static ContractValidationResult<InfiniteCellDiffusionModelV1> TryCreate(
            BurnupCoefficientValuesV1 coefficients,
            double specificPowerWattsPerGramHm,
            double heavyMetalMassKg,
            double dischargeBurnupMwDayPerT)
        {
            if (coefficients == null)
            {
                return Invalid(
                    "InfiniteCellDiffusion.Coefficients.Missing",
                    "coefficients",
                    "An infinite-cell model requires validated two-group coefficients.");
            }

            if (!ContractValidation.IsFinite(specificPowerWattsPerGramHm) ||
                specificPowerWattsPerGramHm <= 0.0)
            {
                return Invalid(
                    "InfiniteCellDiffusion.SpecificPower.Invalid",
                    "specific_power_w_per_g_hm",
                    "Specific power must be finite and strictly positive W/g_HM.");
            }

            if (!ContractValidation.IsFinite(heavyMetalMassKg) || heavyMetalMassKg <= 0.0)
            {
                return Invalid(
                    "InfiniteCellDiffusion.Mass.Invalid",
                    "heavy_metal_mass_kg",
                    "Heavy-metal mass must be finite and strictly positive kg_HM.");
            }

            if (!ContractValidation.IsFinite(dischargeBurnupMwDayPerT) ||
                dischargeBurnupMwDayPerT <= 0.0)
            {
                return Invalid(
                    "InfiniteCellDiffusion.DischargeBurnup.Invalid",
                    "discharge_burnup_mw_day_per_t",
                    "Discharge burnup must be finite and strictly positive MWd/t_HM.");
            }

            double fastRemoval = coefficients.AbsorptionGroup1PerM +
                                 coefficients.DownscatterGroup1To2PerM;
            if (!ContractValidation.IsFinite(fastRemoval) || fastRemoval <= 0.0)
            {
                return Invalid(
                    "InfiniteCellDiffusion.FastRemoval.Invalid",
                    "coefficients",
                    "The no-leakage fast-group removal must be finite and strictly positive.");
            }

            if (!ContractValidation.IsFinite(coefficients.AbsorptionGroup2PerM) ||
                coefficients.AbsorptionGroup2PerM <= 0.0)
            {
                return Invalid(
                    "InfiniteCellDiffusion.ThermalAbsorption.Invalid",
                    "coefficients.absorption_group2_per_m",
                    "The no-leakage thermal absorption must be finite and strictly positive.");
            }

            return ContractValidationResult<InfiniteCellDiffusionModelV1>.Valid(
                new InfiniteCellDiffusionModelV1(
                    coefficients,
                    specificPowerWattsPerGramHm,
                    heavyMetalMassKg,
                    dischargeBurnupMwDayPerT));
        }

        public ContractValidationResult<InfiniteCellDiffusionResultV1> TrySolve()
        {
            double fastRemoval = _coefficients.AbsorptionGroup1PerM +
                                  _coefficients.DownscatterGroup1To2PerM;
            double thermalAbsorption = _coefficients.AbsorptionGroup2PerM;

            // L is the no-leakage loss/removal matrix. The vector x=L^-1 chi
            // gives the flux shape produced by one unit of fission source.
            // The rank-one eigenvalue is k = nuSigmaF dot x.
            double sourceResponseGroup1 = _coefficients.ChiGroup1 / fastRemoval;
            double sourceResponseGroup2 =
                (_coefficients.DownscatterGroup1To2PerM * sourceResponseGroup1 +
                 _coefficients.ChiGroup2) / thermalAbsorption;
            double infiniteMultiplicationFactor =
                _coefficients.NuFissionGroup1PerM * sourceResponseGroup1 +
                _coefficients.NuFissionGroup2PerM * sourceResponseGroup2;
            if (!ContractValidation.IsFinite(sourceResponseGroup1) ||
                sourceResponseGroup1 < 0.0 ||
                !ContractValidation.IsFinite(sourceResponseGroup2) ||
                sourceResponseGroup2 < 0.0 ||
                !ContractValidation.IsFinite(infiniteMultiplicationFactor) ||
                infiniteMultiplicationFactor <= 0.0)
            {
                return ContractValidationResult<InfiniteCellDiffusionResultV1>.Invalid(
                    "InfiniteCellDiffusion.Solve.Invalid",
                    "coefficients",
                    "The homogeneous two-group eigenproblem produced an invalid source response.");
            }

            double infiniteReactivity =
                (infiniteMultiplicationFactor - 1.0) / infiniteMultiplicationFactor;
            double shapeScale = Math.Max(sourceResponseGroup1, sourceResponseGroup2);
            if (!ContractValidation.IsFinite(infiniteMultiplicationFactor) ||
                infiniteMultiplicationFactor <= 0.0 ||
                !ContractValidation.IsFinite(infiniteReactivity) ||
                !ContractValidation.IsFinite(shapeScale) ||
                shapeScale <= 0.0)
            {
                return ContractValidationResult<InfiniteCellDiffusionResultV1>.Invalid(
                    "InfiniteCellDiffusion.Solve.ResultInvalid",
                    "result",
                    "The homogeneous two-group eigenproblem produced an invalid result.");
            }

            double group1FluxShape = sourceResponseGroup1 / shapeScale;
            double group2FluxShape = sourceResponseGroup2 / shapeScale;
            double fissionRatePerUnitFluxPerM =
                _coefficients.FissionGroup1PerM * group1FluxShape +
                _coefficients.FissionGroup2PerM * group2FluxShape;
            double neutronProductionPerUnitFluxPerM =
                _coefficients.NuFissionGroup1PerM * group1FluxShape +
                _coefficients.NuFissionGroup2PerM * group2FluxShape;
            if (!ContractValidation.IsFinite(group1FluxShape) ||
                !ContractValidation.IsFinite(group2FluxShape) ||
                !ContractValidation.IsFinite(fissionRatePerUnitFluxPerM) ||
                fissionRatePerUnitFluxPerM <= 0.0 ||
                !ContractValidation.IsFinite(neutronProductionPerUnitFluxPerM) ||
                neutronProductionPerUnitFluxPerM <= 0.0)
            {
                return ContractValidationResult<InfiniteCellDiffusionResultV1>.Invalid(
                    "InfiniteCellDiffusion.Solve.ShapeInvalid",
                    "result.flux_shape",
                    "The homogeneous two-group flux shape and reaction rates must be finite and positive.");
            }

            double specificPowerWattsPerKgHm =
                _specificPowerWattsPerGramHm * GramsPerKilogram;
            double cellPowerWatts = specificPowerWattsPerKgHm * _heavyMetalMassKg;
            double dischargeBurnupMwDayPerKgHm = _dischargeBurnupMwDayPerT / GramsPerKilogram;
            double dischargeEnergyJPerKgHm =
                dischargeBurnupMwDayPerKgHm * JoulesPerMegaWattDayPerKilogramHm;
            double residenceTimeSeconds = dischargeEnergyJPerKgHm / specificPowerWattsPerKgHm;
            double residenceTimeDays = residenceTimeSeconds / SecondsPerDay;
            if (!ContractValidation.IsFinite(specificPowerWattsPerKgHm) ||
                !ContractValidation.IsFinite(cellPowerWatts) ||
                !ContractValidation.IsFinite(dischargeBurnupMwDayPerKgHm) ||
                !ContractValidation.IsFinite(dischargeEnergyJPerKgHm) ||
                !ContractValidation.IsFinite(residenceTimeSeconds) ||
                !ContractValidation.IsFinite(residenceTimeDays) ||
                cellPowerWatts <= 0.0 ||
                dischargeBurnupMwDayPerKgHm <= 0.0 ||
                residenceTimeSeconds <= 0.0 ||
                residenceTimeDays <= 0.0)
            {
                return ContractValidationResult<InfiniteCellDiffusionResultV1>.Invalid(
                    "InfiniteCellDiffusion.Burnup.ResultInvalid",
                    "result",
                    "The specific-power burnup conversion produced an invalid result.");
            }

            return ContractValidationResult<InfiniteCellDiffusionResultV1>.Valid(
                new InfiniteCellDiffusionResultV1(
                    ModelId,
                    BoundaryConditionId,
                    _coefficients,
                    group1FluxShape,
                    group2FluxShape,
                    fissionRatePerUnitFluxPerM,
                    neutronProductionPerUnitFluxPerM,
                    infiniteMultiplicationFactor,
                    infiniteReactivity,
                    _heavyMetalMassKg,
                    _specificPowerWattsPerGramHm,
                    specificPowerWattsPerKgHm,
                    cellPowerWatts,
                    _dischargeBurnupMwDayPerT,
                    dischargeBurnupMwDayPerKgHm,
                    residenceTimeSeconds,
                    residenceTimeDays));
        }

        private static ContractValidationResult<InfiniteCellDiffusionModelV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<InfiniteCellDiffusionModelV1>.Invalid(
                code,
                path,
                message);
        }
    }

    /// <summary>
    /// Auditable result of the homogeneous two-group infinite-cell solve and
    /// the associated constant-specific-power burnup conversion.
    /// </summary>
    public sealed class InfiniteCellDiffusionResultV1
    {
        internal InfiniteCellDiffusionResultV1(
            string modelId,
            string boundaryConditionId,
            BurnupCoefficientValuesV1 coefficients,
            double group1FluxShape,
            double group2FluxShape,
            double fissionRatePerUnitFluxPerM,
            double neutronProductionPerUnitFluxPerM,
            double infiniteMultiplicationFactor,
            double infiniteReactivity,
            double heavyMetalMassKg,
            double specificPowerWattsPerGramHm,
            double specificPowerWattsPerKgHm,
            double cellPowerWatts,
            double dischargeBurnupMwDayPerT,
            double dischargeBurnupMwDayPerKgHm,
            double residenceTimeSeconds,
            double residenceTimeDays)
        {
            ModelId = modelId;
            BoundaryConditionId = boundaryConditionId;
            Coefficients = coefficients;
            Group1FluxShape = group1FluxShape;
            Group2FluxShape = group2FluxShape;
            FissionRatePerUnitFluxPerM = fissionRatePerUnitFluxPerM;
            NeutronProductionPerUnitFluxPerM = neutronProductionPerUnitFluxPerM;
            InfiniteMultiplicationFactor = infiniteMultiplicationFactor;
            InfiniteReactivity = infiniteReactivity;
            HeavyMetalMassKg = heavyMetalMassKg;
            SpecificPowerWattsPerGramHm = specificPowerWattsPerGramHm;
            SpecificPowerWattsPerKgHm = specificPowerWattsPerKgHm;
            CellPowerWatts = cellPowerWatts;
            DischargeBurnupMwDayPerT = dischargeBurnupMwDayPerT;
            DischargeBurnupMwDayPerKgHm = dischargeBurnupMwDayPerKgHm;
            ResidenceTimeSeconds = residenceTimeSeconds;
            ResidenceTimeDays = residenceTimeDays;
        }

        public string ModelId { get; }

        public string BoundaryConditionId { get; }

        public BurnupCoefficientValuesV1 Coefficients { get; }

        public double Group1FluxShape { get; }

        public double Group2FluxShape { get; }

        public double FissionRatePerUnitFluxPerM { get; }

        public double NeutronProductionPerUnitFluxPerM { get; }

        public double InfiniteMultiplicationFactor { get; }

        public double InfiniteReactivity { get; }

        public double HeavyMetalMassKg { get; }

        public double SpecificPowerWattsPerGramHm { get; }

        public double SpecificPowerWattsPerKgHm { get; }

        public double CellPowerWatts { get; }

        public double DischargeBurnupMwDayPerT { get; }

        public double DischargeBurnupMwDayPerKgHm { get; }

        public double ResidenceTimeSeconds { get; }

        public double ResidenceTimeDays { get; }
    }
}
