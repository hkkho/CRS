using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace ReactorSim.Core
{
    /// <summary>
    /// The supplied CANDU lattice reference uses centimetre-based lattice
    /// constants and reports H factors as kW per (n cm^-2 s^-1).  The Core
    /// solver consumes the explicit SI projections exposed by each state.
    /// Sigma_f and a separate neutron yield are intentionally not members of
    /// this contract: nuSigma_f is the authoritative eigenproblem source.
    /// </summary>
    public sealed class CanduTwoGroupLatticeReferenceStateV1
    {
        public const double HKilowattCmFluxToWattMFlux = 0.1;
        public const double CentimetresPerMetre = 100.0;

        private CanduTwoGroupLatticeReferenceStateV1(
            double irradiationNkb,
            double diffusionGroup1Cm,
            double diffusionGroup2Cm,
            double transportGroup1PerCm,
            double transportGroup2PerCm,
            double absorptionGroup1PerCm,
            double absorptionGroup2PerCm,
            double nuFissionGroup1PerCm,
            double nuFissionGroup2PerCm,
            double downscatterGroup1To2PerCm,
            double hGroup1KilowattPerFluxCm2Second,
            double hGroup2KilowattPerFluxCm2Second)
        {
            IrradiationNkb = irradiationNkb;
            DiffusionGroup1Cm = diffusionGroup1Cm;
            DiffusionGroup2Cm = diffusionGroup2Cm;
            TransportGroup1PerCm = transportGroup1PerCm;
            TransportGroup2PerCm = transportGroup2PerCm;
            AbsorptionGroup1PerCm = absorptionGroup1PerCm;
            AbsorptionGroup2PerCm = absorptionGroup2PerCm;
            NuFissionGroup1PerCm = nuFissionGroup1PerCm;
            NuFissionGroup2PerCm = nuFissionGroup2PerCm;
            DownscatterGroup1To2PerCm = downscatterGroup1To2PerCm;
            HGroup1KilowattPerFluxCm2Second = hGroup1KilowattPerFluxCm2Second;
            HGroup2KilowattPerFluxCm2Second = hGroup2KilowattPerFluxCm2Second;
        }

        public double IrradiationNkb { get; }

        public double DiffusionGroup1Cm { get; }

        public double DiffusionGroup2Cm { get; }

        public double TransportGroup1PerCm { get; }

        public double TransportGroup2PerCm { get; }

        public double AbsorptionGroup1PerCm { get; }

        public double AbsorptionGroup2PerCm { get; }

        public double NuFissionGroup1PerCm { get; }

        public double NuFissionGroup2PerCm { get; }

        public double DownscatterGroup1To2PerCm { get; }

        public double HGroup1KilowattPerFluxCm2Second { get; }

        public double HGroup2KilowattPerFluxCm2Second { get; }

        public double DiffusionGroup1M
        {
            get { return DiffusionGroup1Cm / CentimetresPerMetre; }
        }

        public double DiffusionGroup2M
        {
            get { return DiffusionGroup2Cm / CentimetresPerMetre; }
        }

        public double TransportGroup1PerM
        {
            get { return TransportGroup1PerCm * CentimetresPerMetre; }
        }

        public double TransportGroup2PerM
        {
            get { return TransportGroup2PerCm * CentimetresPerMetre; }
        }

        public double AbsorptionGroup1PerM
        {
            get { return AbsorptionGroup1PerCm * CentimetresPerMetre; }
        }

        public double AbsorptionGroup2PerM
        {
            get { return AbsorptionGroup2PerCm * CentimetresPerMetre; }
        }

        public double NuFissionGroup1PerM
        {
            get { return NuFissionGroup1PerCm * CentimetresPerMetre; }
        }

        public double NuFissionGroup2PerM
        {
            get { return NuFissionGroup2PerCm * CentimetresPerMetre; }
        }

        public double DownscatterGroup1To2PerM
        {
            get { return DownscatterGroup1To2PerCm * CentimetresPerMetre; }
        }

        /// <summary>
        /// H in W per (n m^-2 s^-1).  The factor is 0.1, not 10 or 0.01:
        /// kW to W contributes 1000 and cm^-2 to m^-2 contributes 10^-4.
        /// </summary>
        public double HGroup1WattsPerFluxM2Second
        {
            get { return HGroup1KilowattPerFluxCm2Second * HKilowattCmFluxToWattMFlux; }
        }

        public double HGroup2WattsPerFluxM2Second
        {
            get { return HGroup2KilowattPerFluxCm2Second * HKilowattCmFluxToWattMFlux; }
        }

        public double FastRemovalPerCm
        {
            get { return AbsorptionGroup1PerCm + DownscatterGroup1To2PerCm; }
        }

        public double ThermalFluxToFastFluxRatio
        {
            get { return DownscatterGroup1To2PerCm / AbsorptionGroup2PerCm; }
        }

        /// <summary>
        /// Four-factor fast-fission factor from the supplied extraction model.
        /// </summary>
        public double EpsilonFastFission
        {
            get
            {
                return 1.0 +
                       NuFissionGroup1PerCm /
                       (NuFissionGroup2PerCm * ThermalFluxToFastFluxRatio);
            }
        }

        /// <summary>
        /// Resonance escape probability in the supplied extraction model.
        /// </summary>
        public double ResonanceEscapeProbability
        {
            get { return DownscatterGroup1To2PerCm / FastRemovalPerCm; }
        }

        /// <summary>
        /// The supplied table labels this factor eta*f. It is the thermal
        /// nu-fission production divided by thermal absorption.
        /// </summary>
        public double EtaFThermalUtilization
        {
            get { return NuFissionGroup2PerCm / AbsorptionGroup2PerCm; }
        }

        public double L1SquaredCm2
        {
            get { return DiffusionGroup1Cm / FastRemovalPerCm; }
        }

        public double L2SquaredCm2
        {
            get { return DiffusionGroup2Cm / AbsorptionGroup2PerCm; }
        }

        /// <summary>
        /// k-infinity for chi_1 = 1 and chi_2 = 0:
        /// (nuSigma_f1 + nuSigma_f2 * Sigma_1_to_2 / Sigma_a2) /
        /// (Sigma_a1 + Sigma_1_to_2).
        /// </summary>
        public double KInfinite
        {
            get
            {
                return EpsilonFastFission *
                       ResonanceEscapeProbability *
                       EtaFThermalUtilization;
            }
        }

        public static ContractValidationResult<CanduTwoGroupLatticeReferenceStateV1> TryCreate(
            double irradiationNkb,
            double diffusionGroup1Cm,
            double diffusionGroup2Cm,
            double transportGroup1PerCm,
            double transportGroup2PerCm,
            double absorptionGroup1PerCm,
            double absorptionGroup2PerCm,
            double nuFissionGroup1PerCm,
            double nuFissionGroup2PerCm,
            double downscatterGroup1To2PerCm,
            double hGroup1KilowattPerFluxCm2Second,
            double hGroup2KilowattPerFluxCm2Second)
        {
            double[] values =
            {
                irradiationNkb,
                diffusionGroup1Cm,
                diffusionGroup2Cm,
                transportGroup1PerCm,
                transportGroup2PerCm,
                absorptionGroup1PerCm,
                absorptionGroup2PerCm,
                nuFissionGroup1PerCm,
                nuFissionGroup2PerCm,
                downscatterGroup1To2PerCm,
                hGroup1KilowattPerFluxCm2Second,
                hGroup2KilowattPerFluxCm2Second
            };
            if (values.Any(value => !ContractValidation.IsFinite(value)))
            {
                return Invalid(
                    "CanduLatticeReference.State.NonFinite",
                    "state",
                    "Every supplied lattice state value must be finite.");
            }

            if (irradiationNkb < 0.0 ||
                diffusionGroup1Cm <= 0.0 || diffusionGroup2Cm <= 0.0 ||
                transportGroup1PerCm <= 0.0 || transportGroup2PerCm <= 0.0 ||
                absorptionGroup1PerCm <= 0.0 || absorptionGroup2PerCm <= 0.0 ||
                nuFissionGroup1PerCm <= 0.0 || nuFissionGroup2PerCm <= 0.0 ||
                downscatterGroup1To2PerCm <= 0.0 ||
                hGroup1KilowattPerFluxCm2Second < 0.0 ||
                hGroup2KilowattPerFluxCm2Second < 0.0)
            {
                return Invalid(
                    "CanduLatticeReference.State.NegativeOrNonPositive",
                    "state",
                    "Diffusion, absorption, transport, source, and H values must satisfy their physical sign constraints.");
            }

            if (hGroup1KilowattPerFluxCm2Second <= 0.0 &&
                hGroup2KilowattPerFluxCm2Second <= 0.0)
            {
                return Invalid(
                    "CanduLatticeReference.State.H.Zero",
                    "state.h_factors",
                    "At least one H factor must have positive power support.");
            }

            double fastRemoval = absorptionGroup1PerCm + downscatterGroup1To2PerCm;
            if (!ContractValidation.IsFinite(fastRemoval) || fastRemoval <= 0.0)
            {
                return Invalid(
                    "CanduLatticeReference.State.FastRemoval.Invalid",
                    "state",
                    "Fast removal must be finite and strictly positive.");
            }

            return ContractValidationResult<CanduTwoGroupLatticeReferenceStateV1>.Valid(
                new CanduTwoGroupLatticeReferenceStateV1(
                    irradiationNkb,
                    diffusionGroup1Cm,
                    diffusionGroup2Cm,
                    transportGroup1PerCm,
                    transportGroup2PerCm,
                    absorptionGroup1PerCm,
                    absorptionGroup2PerCm,
                    nuFissionGroup1PerCm,
                    nuFissionGroup2PerCm,
                    downscatterGroup1To2PerCm,
                    hGroup1KilowattPerFluxCm2Second,
                    hGroup2KilowattPerFluxCm2Second));
        }

        internal ContractValidationResult<SpatialFluxPowerResponseV1> TryCreatePowerResponse()
        {
            return SpatialFluxPowerResponseV1.TryCreate(
                HGroup1WattsPerFluxM2Second,
                HGroup2WattsPerFluxM2Second);
        }

        internal static ContractValidationResult<CanduTwoGroupLatticeReferenceStateV1> Interpolate(
            CanduTwoGroupLatticeReferenceStateV1 lower,
            CanduTwoGroupLatticeReferenceStateV1 upper,
            double fraction,
            double irradiationNkb)
        {
            double Blend(double left, double right)
            {
                return ((1.0 - fraction) * left) + (fraction * right);
            }

            return TryCreate(
                irradiationNkb,
                Blend(lower.DiffusionGroup1Cm, upper.DiffusionGroup1Cm),
                Blend(lower.DiffusionGroup2Cm, upper.DiffusionGroup2Cm),
                Blend(lower.TransportGroup1PerCm, upper.TransportGroup1PerCm),
                Blend(lower.TransportGroup2PerCm, upper.TransportGroup2PerCm),
                Blend(lower.AbsorptionGroup1PerCm, upper.AbsorptionGroup1PerCm),
                Blend(lower.AbsorptionGroup2PerCm, upper.AbsorptionGroup2PerCm),
                Blend(lower.NuFissionGroup1PerCm, upper.NuFissionGroup1PerCm),
                Blend(lower.NuFissionGroup2PerCm, upper.NuFissionGroup2PerCm),
                Blend(lower.DownscatterGroup1To2PerCm, upper.DownscatterGroup1To2PerCm),
                Blend(lower.HGroup1KilowattPerFluxCm2Second, upper.HGroup1KilowattPerFluxCm2Second),
                Blend(lower.HGroup2KilowattPerFluxCm2Second, upper.HGroup2KilowattPerFluxCm2Second));
        }

        private static ContractValidationResult<CanduTwoGroupLatticeReferenceStateV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<CanduTwoGroupLatticeReferenceStateV1>.Invalid(
                code,
                path,
                message);
        }
    }

    public sealed class CanduTwoGroupLatticeLookupResultV1
    {
        internal CanduTwoGroupLatticeLookupResultV1(
            int lowerIndex,
            int upperIndex,
            double interpolationFraction,
            CanduTwoGroupLatticeReferenceStateV1 state)
        {
            BracketLowerIndex = lowerIndex;
            BracketUpperIndex = upperIndex;
            InterpolationFraction = interpolationFraction;
            State = state;
        }

        public int BracketLowerIndex { get; }

        public int BracketUpperIndex { get; }

        public double InterpolationFraction { get; }

        public CanduTwoGroupLatticeReferenceStateV1 State { get; }
    }

    public sealed class CanduTwoGroupLatticeReferenceV1
    {
        public const uint CurrentSchemaVersion = 1;
        public const double RequiredThermalCutoffElectronVolts = 0.625;
        public const double ReferenceGeometricBucklingInverseCmSquared = 0.0003;
        public const string EnergyGroupOrderId = "fast,thermal";

        // The supplied D and Sigma_tr values are independently published to four
        // decimal places; 5e-4 is a conservative relative bound for that rounding.
        private const double RoundedTransportDiffusionRelativeTolerance = 5.0e-4;

        private readonly ReadOnlyCollection<CanduTwoGroupLatticeReferenceStateV1> _states;
        private readonly string _energyGroupOrder;

        private CanduTwoGroupLatticeReferenceV1(
            string referenceId,
            string sourceProvenance,
            double thermalCutoffElectronVolts,
            IEnumerable<CanduTwoGroupLatticeReferenceStateV1> states)
        {
            ReferenceId = referenceId;
            SourceProvenance = sourceProvenance;
            ThermalCutoffElectronVolts = thermalCutoffElectronVolts;
            _energyGroupOrder = EnergyGroupOrderId;
            _states = new ReadOnlyCollection<CanduTwoGroupLatticeReferenceStateV1>(
                states.ToArray());
        }

        public string ReferenceId { get; }

        public string SourceProvenance { get; }

        public double ThermalCutoffElectronVolts { get; }

        public string EnergyGroupOrder
        {
            get { return _energyGroupOrder; }
        }

        public IReadOnlyList<CanduTwoGroupLatticeReferenceStateV1> States
        {
            get { return _states; }
        }

        /// <summary>
        /// Builds the exact four-row reference supplied for this task. It is
        /// kept separate from the active game pack and remains unverified.
        /// </summary>
        public static ContractValidationResult<CanduTwoGroupLatticeReferenceV1>
            TryCreateUserSupplied()
        {
            var states = new List<CanduTwoGroupLatticeReferenceStateV1>();
            double[][] rows =
            {
                new[] { 0.0, 1.2850, 0.8240, 0.2594, 0.4045, 0.00845, 0.00495, 0.00385, 0.00582, 0.01420, 1.25e-12, 2.11e-11 },
                new[] { 0.3, 1.2852, 0.8190, 0.2594, 0.4070, 0.00848, 0.00512, 0.00382, 0.00624, 0.01418, 1.24e-12, 2.26e-11 },
                new[] { 1.0, 1.2855, 0.8120, 0.2593, 0.4105, 0.00850, 0.00538, 0.00378, 0.00545, 0.01415, 1.23e-12, 1.97e-11 },
                new[] { 1.8, 1.2858, 0.8060, 0.2592, 0.4136, 0.00853, 0.00562, 0.00374, 0.00488, 0.01412, 1.22e-12, 1.76e-11 }
            };

            foreach (double[] row in rows)
            {
                ContractValidationResult<CanduTwoGroupLatticeReferenceStateV1> state =
                    CanduTwoGroupLatticeReferenceStateV1.TryCreate(
                        row[0], row[1], row[2], row[3], row[4], row[5],
                        row[6], row[7], row[8], row[9], row[10], row[11]);
                if (!state.IsValid)
                {
                    return Invalid(
                        state.FirstDiagnostic.Code,
                        state.FirstDiagnostic.Path,
                        state.FirstDiagnostic.Message);
                }

                states.Add(state.Value);
            }

            return TryCreate(
                "candu-two-group-lattice-user-reference-v1",
                "user-supplied; unverified",
                RequiredThermalCutoffElectronVolts,
                states);
        }

        public static ContractValidationResult<CanduTwoGroupLatticeReferenceV1> TryCreate(
            string referenceId,
            string sourceProvenance,
            double thermalCutoffElectronVolts,
            IEnumerable<CanduTwoGroupLatticeReferenceStateV1> states)
        {
            if (string.IsNullOrWhiteSpace(referenceId) || string.IsNullOrWhiteSpace(sourceProvenance))
            {
                return Invalid(
                    "CanduLatticeReference.Metadata.Missing",
                    "metadata",
                    "The lattice reference requires an ID and source provenance.");
            }

            if (!ContractValidation.IsFinite(thermalCutoffElectronVolts) ||
                thermalCutoffElectronVolts != RequiredThermalCutoffElectronVolts)
            {
                return Invalid(
                    "CanduLatticeReference.Cutoff.Unsupported",
                    "thermal_cutoff_eV",
                    "The supplied reference requires the exact 0.625 eV group cutoff.");
            }

            if (states == null)
            {
                return Invalid(
                    "CanduLatticeReference.States.Missing",
                    "states",
                    "The four irradiation states are required.");
            }

            CanduTwoGroupLatticeReferenceStateV1[] records = states.ToArray();
            double[] expectedIrradiations = { 0.0, 0.3, 1.0, 1.8 };
            if (records.Length != expectedIrradiations.Length)
            {
                return Invalid(
                    "CanduLatticeReference.States.Count",
                    "states",
                    "The supplied reference requires exactly four irradiation states.");
            }

            for (int index = 0; index < records.Length; index++)
            {
                if (records[index] == null || records[index].IrradiationNkb != expectedIrradiations[index])
                {
                    return Invalid(
                        "CanduLatticeReference.States.Order",
                        "states[" + index.ToString(CultureInfo.InvariantCulture) + "]",
                        "States must be ordered at 0.0, 0.3, 1.0, and 1.8 nkb.");
                }
            }

            return ContractValidationResult<CanduTwoGroupLatticeReferenceV1>.Valid(
                new CanduTwoGroupLatticeReferenceV1(
                    referenceId,
                    sourceProvenance,
                    thermalCutoffElectronVolts,
                    records));
        }

        public ContractValidationResult<CanduTwoGroupLatticeLookupResultV1> TryLookup(
            double irradiationNkb)
        {
            if (!ContractValidation.IsFinite(irradiationNkb) || irradiationNkb < 0.0)
            {
                return LookupInvalid(
                    "CanduLatticeReference.Lookup.Invalid",
                    "irradiation_nkb",
                    "Lookup irradiation must be finite and nonnegative.");
            }

            int exact = _states
                .Select((state, index) => new { state, index })
                .Where(value => value.state.IrradiationNkb == irradiationNkb)
                .Select(value => value.index)
                .DefaultIfEmpty(-1)
                .First();
            if (exact >= 0)
            {
                return ContractValidationResult<CanduTwoGroupLatticeLookupResultV1>.Valid(
                    new CanduTwoGroupLatticeLookupResultV1(exact, exact, 0.0, _states[exact]));
            }

            if (irradiationNkb < _states[0].IrradiationNkb ||
                irradiationNkb > _states[_states.Count - 1].IrradiationNkb)
            {
                return LookupInvalid(
                    "CanduLatticeReference.Lookup.OutOfRange",
                    "irradiation_nkb",
                    "Lookup outside the four-state reference domain is rejected without extrapolation.");
            }

            int upper = 1;
            while (upper < _states.Count && _states[upper].IrradiationNkb < irradiationNkb)
            {
                upper++;
            }

            int lower = upper - 1;
            double denominator = _states[upper].IrradiationNkb - _states[lower].IrradiationNkb;
            double fraction = (irradiationNkb - _states[lower].IrradiationNkb) / denominator;
            ContractValidationResult<CanduTwoGroupLatticeReferenceStateV1> interpolated =
                CanduTwoGroupLatticeReferenceStateV1.Interpolate(
                    _states[lower],
                    _states[upper],
                    fraction,
                    irradiationNkb);
            if (!interpolated.IsValid)
            {
                return LookupInvalid(
                    "CanduLatticeReference.Lookup.Interpolation.Invalid",
                    interpolated.FirstDiagnostic.Path,
                    interpolated.FirstDiagnostic.Message);
            }

            return ContractValidationResult<CanduTwoGroupLatticeLookupResultV1>.Valid(
                new CanduTwoGroupLatticeLookupResultV1(
                    lower,
                    upper,
                    fraction,
                    interpolated.Value));
        }

        public ContractValidationResult<CanduTwoGroupLatticeAuditV1> TryAudit()
        {
            var checks = new List<CanduTwoGroupLatticeAuditCheckV1>();
            AddCheck(
                checks,
                "cutoff",
                ThermalCutoffElectronVolts == RequiredThermalCutoffElectronVolts,
                ThermalCutoffElectronVolts,
                ThermalCutoffElectronVolts,
                "The two-group cutoff is exactly 0.625 eV.");

            foreach (CanduTwoGroupLatticeReferenceStateV1 state in _states)
            {
                double expectedD1 = 1.0 / (3.0 * state.TransportGroup1PerCm);
                double expectedD2 = 1.0 / (3.0 * state.TransportGroup2PerCm);
                AddCheck(
                    checks,
                    "state." + state.IrradiationNkb.ToString(CultureInfo.InvariantCulture) + ".D1",
                    RelativeDifference(expectedD1, state.DiffusionGroup1Cm) <=
                        RoundedTransportDiffusionRelativeTolerance,
                    state.DiffusionGroup1Cm,
                    expectedD1,
                    "D1 agrees with 1/(3 Sigma_tr1) within the supplied table precision.");
                AddCheck(
                    checks,
                    "state." + state.IrradiationNkb.ToString(CultureInfo.InvariantCulture) + ".D2",
                    RelativeDifference(expectedD2, state.DiffusionGroup2Cm) <=
                        RoundedTransportDiffusionRelativeTolerance,
                    state.DiffusionGroup2Cm,
                    expectedD2,
                    "D2 agrees with 1/(3 Sigma_tr2) within the supplied table precision.");
                AddCheck(
                    checks,
                    "state." + state.IrradiationNkb.ToString(CultureInfo.InvariantCulture) + ".H1",
                    state.HGroup1WattsPerFluxM2Second ==
                        state.HGroup1KilowattPerFluxCm2Second *
                        CanduTwoGroupLatticeReferenceStateV1.HKilowattCmFluxToWattMFlux,
                    state.HGroup1WattsPerFluxM2Second,
                    state.HGroup1KilowattPerFluxCm2Second *
                    CanduTwoGroupLatticeReferenceStateV1.HKilowattCmFluxToWattMFlux,
                    "H1 uses the explicit 0.1 SI flux/unit conversion.");
                AddCheck(
                    checks,
                    "state." + state.IrradiationNkb.ToString(CultureInfo.InvariantCulture) + ".H2",
                    state.HGroup2WattsPerFluxM2Second ==
                        state.HGroup2KilowattPerFluxCm2Second *
                        CanduTwoGroupLatticeReferenceStateV1.HKilowattCmFluxToWattMFlux,
                    state.HGroup2WattsPerFluxM2Second,
                    state.HGroup2KilowattPerFluxCm2Second *
                    CanduTwoGroupLatticeReferenceStateV1.HKilowattCmFluxToWattMFlux,
                    "H2 uses the explicit 0.1 SI flux/unit conversion.");
                double directK =
                    (state.NuFissionGroup1PerCm +
                     state.NuFissionGroup2PerCm * state.DownscatterGroup1To2PerCm /
                     state.AbsorptionGroup2PerCm) /
                    (state.AbsorptionGroup1PerCm + state.DownscatterGroup1To2PerCm);
                AddCheck(
                    checks,
                    "state." + state.IrradiationNkb.ToString(CultureInfo.InvariantCulture) + ".k_infinite",
                    RelativeDifference(directK, state.KInfinite) <= 1.0e-12,
                    state.KInfinite,
                    directK,
                    "k_infinite uses nuSigma_f directly and no invented Sigma_f or nu.");
            }

            return ContractValidationResult<CanduTwoGroupLatticeAuditV1>.Valid(
                new CanduTwoGroupLatticeAuditV1(checks));
        }

        private static void AddCheck(
            List<CanduTwoGroupLatticeAuditCheckV1> checks,
            string id,
            bool passed,
            double actual,
            double expected,
            string message)
        {
            checks.Add(new CanduTwoGroupLatticeAuditCheckV1(id, passed, actual, expected, message));
        }

        private static double RelativeDifference(double left, double right)
        {
            double scale = Math.Max(Math.Abs(left), Math.Abs(right));
            return scale == 0.0 ? 0.0 : Math.Abs(left - right) / scale;
        }

        private static ContractValidationResult<CanduTwoGroupLatticeReferenceV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<CanduTwoGroupLatticeReferenceV1>.Invalid(
                code,
                path,
                message);
        }

        private static ContractValidationResult<CanduTwoGroupLatticeLookupResultV1> LookupInvalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<CanduTwoGroupLatticeLookupResultV1>.Invalid(
                code,
                path,
                message);
        }
    }

    public sealed class CanduTwoGroupLatticeAuditCheckV1
    {
        internal CanduTwoGroupLatticeAuditCheckV1(
            string id,
            bool passed,
            double actual,
            double expected,
            string message)
        {
            Id = id;
            Passed = passed;
            Actual = actual;
            Expected = expected;
            Message = message;
        }

        public string Id { get; }

        public bool Passed { get; }

        public double Actual { get; }

        public double Expected { get; }

        public string Message { get; }
    }

    public sealed class CanduTwoGroupLatticeAuditV1
    {
        private readonly ReadOnlyCollection<CanduTwoGroupLatticeAuditCheckV1> _checks;

        internal CanduTwoGroupLatticeAuditV1(
            IEnumerable<CanduTwoGroupLatticeAuditCheckV1> checks)
        {
            _checks = new ReadOnlyCollection<CanduTwoGroupLatticeAuditCheckV1>(checks.ToArray());
        }

        public IReadOnlyList<CanduTwoGroupLatticeAuditCheckV1> Checks
        {
            get { return _checks; }
        }

        public bool Passed
        {
            get { return _checks.All(check => check.Passed); }
        }
    }

    public sealed class CanduTwoGroupLeakageResultV1
    {
        internal CanduTwoGroupLeakageResultV1(
            double geometricBucklingPerM2,
            double fastNonLeakage,
            double thermalNonLeakage,
            double kInfinite,
            double kEffective)
        {
            GeometricBucklingPerM2 = geometricBucklingPerM2;
            FastNonLeakage = fastNonLeakage;
            ThermalNonLeakage = thermalNonLeakage;
            KInfinite = kInfinite;
            KEffective = kEffective;
        }

        public double GeometricBucklingPerM2 { get; }

        public double FastNonLeakage { get; }

        public double ThermalNonLeakage { get; }

        public double KInfinite { get; }

        public double KEffective { get; }
    }

    public sealed class CanduTwoGroupReflectiveLatticeV1
    {
        private CanduTwoGroupReflectiveLatticeV1(
            CanduTwoGroupLatticeReferenceStateV1 state,
            CoreTopology topology,
            SpatialStencil stencil,
            SpatialCoefficientSet coefficients)
        {
            State = state;
            Topology = topology;
            Stencil = stencil;
            Coefficients = coefficients;
        }

        public CanduTwoGroupLatticeReferenceStateV1 State { get; }

        public CoreTopology Topology { get; }

        public SpatialStencil Stencil { get; }

        public SpatialCoefficientSet Coefficients { get; }

        public static ContractValidationResult<CanduTwoGroupReflectiveLatticeV1> TryCreate(
            CanduTwoGroupLatticeReferenceStateV1 state,
            double nodeVolumeM3 = 1.0)
        {
            if (state == null)
            {
                return Invalid(
                    "CanduReflectiveLattice.State.Missing",
                    "state",
                    "A reflective lattice requires one validated reference state.");
            }

            if (!ContractValidation.IsFinite(nodeVolumeM3) || nodeVolumeM3 <= 0.0)
            {
                return Invalid(
                    "CanduReflectiveLattice.Volume.Invalid",
                    "node_volume_m3",
                    "The reflective lattice node volume must be finite and positive SI m^3.");
            }

            ChannelId channelId = new ChannelId(0);
            BundlePosition position = new BundlePosition(0);
            NodeKey node = new NodeKey(channelId, position);
            var boundaries = new[]
            {
                new BoundaryFaceRecord(channelId, position, TopologyFace.North, BoundaryClassification.Reflective),
                new BoundaryFaceRecord(channelId, position, TopologyFace.East, BoundaryClassification.Reflective),
                new BoundaryFaceRecord(channelId, position, TopologyFace.South, BoundaryClassification.Reflective),
                new BoundaryFaceRecord(channelId, position, TopologyFace.West, BoundaryClassification.Reflective),
                new BoundaryFaceRecord(channelId, position, TopologyFace.EndA, BoundaryClassification.Reflective),
                new BoundaryFaceRecord(channelId, position, TopologyFace.EndB, BoundaryClassification.Reflective)
            };
            var channel = new ChannelTopology(
                channelId,
                0,
                0,
                FlowDirection.EndAtoEndB,
                position,
                position,
                Array.Empty<NeighborRecord>(),
                boundaries);
            ContractValidationResult<CoreTopology> topology = CoreTopology.TryCreate(
                1,
                1,
                new[] { channel });
            if (!topology.IsValid)
            {
                return Invalid(topology.FirstDiagnostic);
            }

            ContractValidationResult<SpatialStencil> stencil = SpatialStencil.TryCreate(topology.Value);
            if (!stencil.IsValid)
            {
                return Invalid(stencil.FirstDiagnostic);
            }

            ContractValidationResult<SpatialFluxPowerResponseV1> response = state.TryCreatePowerResponse();
            if (!response.IsValid)
            {
                return Invalid(response.FirstDiagnostic);
            }

            var nodeCoefficients = new SpatialNodeCoefficients(
                node,
                nodeVolumeM3,
                state.AbsorptionGroup1PerM,
                state.AbsorptionGroup2PerM,
                state.DownscatterGroup1To2PerM,
                0.0,
                0.0,
                state.NuFissionGroup1PerM,
                state.NuFissionGroup2PerM,
                1.0,
                0.0,
                0.0,
                response.Value,
                false);

            SpatialBoundaryConductance[] boundaryConductances = boundaries
                .Select(boundary => new SpatialBoundaryConductance(
                    node,
                    boundary.Face,
                    0.0,
                    0.0))
                .ToArray();
            ContractValidationResult<SpatialCoefficientSet> coefficients = SpatialCoefficientSet.TryCreate(
                stencil.Value,
                new[] { nodeCoefficients },
                Array.Empty<SpatialEdgeConductance>(),
                boundaryConductances);
            if (!coefficients.IsValid)
            {
                return Invalid(coefficients.FirstDiagnostic);
            }

            return ContractValidationResult<CanduTwoGroupReflectiveLatticeV1>.Valid(
                new CanduTwoGroupReflectiveLatticeV1(
                    state,
                    topology.Value,
                    stencil.Value,
                    coefficients.Value));
        }

        public CanduTwoGroupLeakageResultV1 CalculateFourFactorLeakage(
            double geometricBucklingPerM2)
        {
            double fastRemovalPerM = State.AbsorptionGroup1PerM + State.DownscatterGroup1To2PerM;
            double fastNonLeakage = fastRemovalPerM /
                (fastRemovalPerM + (State.DiffusionGroup1M * geometricBucklingPerM2));
            double thermalNonLeakage = State.AbsorptionGroup2PerM /
                (State.AbsorptionGroup2PerM + (State.DiffusionGroup2M * geometricBucklingPerM2));
            return new CanduTwoGroupLeakageResultV1(
                geometricBucklingPerM2,
                fastNonLeakage,
                thermalNonLeakage,
                State.KInfinite,
                State.KInfinite * fastNonLeakage * thermalNonLeakage);
        }

        private static ContractValidationResult<CanduTwoGroupReflectiveLatticeV1> Invalid(
            ContractDiagnostic diagnostic)
        {
            return ContractValidationResult<CanduTwoGroupReflectiveLatticeV1>.Invalid(
                diagnostic.Code,
                diagnostic.Path,
                diagnostic.Message);
        }

        private static ContractValidationResult<CanduTwoGroupReflectiveLatticeV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<CanduTwoGroupReflectiveLatticeV1>.Invalid(
                code,
                path,
                message);
        }
    }
}
