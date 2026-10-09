using System;
using System.Collections.Generic;
using System.Linq;

namespace ReactorSim.Core
{
    /// <summary>Time-average approximation for the authored eight-bundle fuel cycle.
    /// Prescribed exposure ranges, exact piecewise-linear coefficient averages,
    /// half-filled liquid zones, and pack-bound nominal adjusters. Not a converged plant model.</summary>
    public sealed class PracticeChannelPowerReference
    {
        public const string ModelId = "cycle190-time-average-21-adjusters-lzc-tubes-half-zones-v4";
        private PracticeChannelPowerReference(FullCoreDiffusionSolveResultV1 solve)
        {
            ChannelPowerWatts = Array.AsReadOnly(Enumerable.Range(0, 380)
                .Select(c => solve.NodePowerWatts.Skip(c * 12).Take(12).Sum()).ToArray());
            ThermalPowerWatts = ChannelPowerWatts.Sum();
            DataPackVersion = solve.DataPack.Descriptor.DataPackVersion;
            CoefficientBindingDigest = solve.CoefficientBindingDigest;
        }
        public IReadOnlyList<double> ChannelPowerWatts { get; }
        public double ThermalPowerWatts { get; }
        public string DataPackVersion { get; }
        public Digest32 CoefficientBindingDigest { get; }

        public static PracticeChannelPowerReference Create(FullCoreDiffusionDataPackV1 pack, double powerWatts)
        {
            var model = FullCoreDiffusionModelV1.TryCreateCandu6(pack);
            if (!model.IsValid) throw new InvalidOperationException(model.FirstDiagnostic.ToString());
            var solve = model.Value.TrySolvePracticeTimeAverage(powerWatts);
            if (!solve.IsValid) throw new InvalidOperationException(solve.FirstDiagnostic.ToString());
            return new PracticeChannelPowerReference(solve.Value);
        }

        public static BurnupCoefficientValuesV1 AverageCoefficients(BurnupCoefficientTableV1 table,
            double beginningJPerKg, double endJPerKg)
        {
            if (!ContractValidation.IsFinite(beginningJPerKg) || !ContractValidation.IsFinite(endJPerKg) ||
                beginningJPerKg < 0 || endJPerKg <= beginningJPerKg)
                throw new ArgumentOutOfRangeException(nameof(endJPerKg));
            var knots = new[] { beginningJPerKg }.Concat(table.Rows.Select(row => row.BurnupJPerKgHm)
                .Where(b => b > beginningJPerKg && b < endJPerKg)).Concat(new[] { endJPerKg }).ToArray();
            var sums = new double[9];
            double[] Values(double b)
            {
                var lookup = table.TryLookup(b);
                if (!lookup.IsValid) throw new ArgumentOutOfRangeException(nameof(endJPerKg), lookup.FirstDiagnostic.ToString());
                var v = lookup.Value.Coefficients;
                return new[] { v.AbsorptionGroup1PerM, v.AbsorptionGroup2PerM, v.FissionGroup1PerM,
                    v.FissionGroup2PerM, v.NuFissionGroup1PerM, v.NuFissionGroup2PerM,
                    v.DownscatterGroup1To2PerM, v.ChiGroup1, v.EnergyPerFissionJ };
            }
            for (int k = 1; k < knots.Length; k++)
            {
                double[] a = Values(knots[k - 1]), b = Values(knots[k]);
                double weight = (knots[k] - knots[k - 1]) / (endJPerKg - beginningJPerKg);
                for (int i = 0; i < sums.Length; i++) sums[i] += 0.5 * (a[i] + b[i]) * weight;
            }
            var result = BurnupCoefficientValuesV1.TryCreate(sums[0], sums[1], sums[2], sums[3],
                sums[4], sums[5], sums[6], Math.Max(0, Math.Min(1, sums[7])), sums[8]);
            if (!result.IsValid) throw new InvalidOperationException(result.FirstDiagnostic.ToString());
            return result.Value;
        }
    }

    public sealed partial class FullCoreDiffusionModelV1
    {
        internal ContractValidationResult<FullCoreDiffusionSolveResultV1> TrySolvePracticeTimeAverage(double powerWatts)
        {
            var state = SyntheticGameCoreStateV1.CreateAgedPractice();
            var table = _dataPack.CoefficientTables.Single(t => t.MaterialVariantId.Value == "NAT-U-SYNTHETIC");
            var axial = new BurnupCoefficientValuesV1[12];
            for (int k = 0; k < axial.Length; k++)
                axial[k] = PracticeChannelPowerReference.AverageCoefficients(table,
                    AgedCoreSnapshotGeneratorV1.BundleBurnupMwDayPerKg(k, 0) * 8.64e10,
                    AgedCoreSnapshotGeneratorV1.BundleBurnupMwDayPerKg(k, 1) * 8.64e10);
            var values = new BurnupCoefficientValuesV1[_stencil.NodeCount];
            foreach (var node in _stencil.Nodes)
            {
                bool reverse = Candu6CoreTopologyFactoryV1.GetFlowDirection(
                    Candu6CoreTopologyFactoryV1.GetPosition(node.Node.ChannelId.Value)) == FlowDirection.EndBtoEndA;
                int k = (int)node.Node.Position.Value;
                values[node.FlatIndex] = axial[reverse ? 11 - k : k];
            }
            var inventory = BundleInventory.TryCreate(_topology, state.EnumerateBundles());
            if (!inventory.IsValid) return InvalidSolve(inventory.FirstDiagnostic.Code, inventory.FirstDiagnostic.Path, inventory.FirstDiagnostic.Message);
            var coefficients = BuildCoefficientSet(inventory.Value, values);
            if (!coefficients.IsValid) return InvalidSolve(coefficients.FirstDiagnostic.Code, coefficients.FirstDiagnostic.Path, coefficients.FirstDiagnostic.Message);
            var mapping = PracticeLiquidZoneRrsMappingV1.TryCreateCandu6();
            if (!mapping.IsValid) return InvalidSolve(mapping.FirstDiagnostic.Code, mapping.FirstDiagnostic.Path, mapping.FirstDiagnostic.Message);
            var overlay = mapping.Value.TryBuildOverlay(Enumerable.Repeat(0.5, 14).ToArray());
            if (!overlay.IsValid) return InvalidSolve(overlay.FirstDiagnostic.Code, overlay.FirstDiagnostic.Path, overlay.FirstDiagnostic.Message);
            var effective = ApplyStaticAbsorptionOverlay(coefficients.Value, overlay.Value);
            if (!effective.IsValid) return InvalidSolve(effective.FirstDiagnostic.Code, effective.FirstDiagnostic.Path, effective.FirstDiagnostic.Message);
            var solved = TrySolveWithCoefficients(inventory.Value, effective.Value, powerWatts,
                1.0, null, null, null, overlay.Value, null);
            if (!solved.IsValid || _dataPack.XenonReference == null) return solved;
            var referenceAxial = new double[12];
            for (int k = 0; k < 12; k++)
                referenceAxial[k] = _dataPack.XenonReference.Average(
                    AgedCoreSnapshotGeneratorV1.BundleBurnupMwDayPerKg(k, 0) * 8.64e10,
                    AgedCoreSnapshotGeneratorV1.BundleBurnupMwDayPerKg(k, 1) * 8.64e10);
            var previousDelta = new double[_stencil.NodeCount];
            for (int pass = 0; pass < 16; pass++)
            {
                var entries = new List<StaticAbsorptionOverlayEntryV1>(_stencil.NodeCount);
                double error = 0, maximumXeAbsorption = 0;
                foreach (var node in _stencil.Nodes)
                {
                    int n = node.FlatIndex;
                    var c = coefficients.Value.Nodes[n];
                    double f = c.FissionGroup1PerM * solved.Value.Group1Flux[n] +
                        c.FissionGroup2PerM * solved.Value.Group2Flux[n];
                    double xe = (PracticeXenonDataV1.GammaI + PracticeXenonDataV1.GammaXe) * f /
                        (PracticeXenonDataV1.LambdaXe + PracticeXenonDataV1.SigmaGroup2M2 * solved.Value.Group2Flux[n]);
                    bool reverse = Candu6CoreTopologyFactoryV1.GetFlowDirection(
                        Candu6CoreTopologyFactoryV1.GetPosition(node.Node.ChannelId.Value)) == FlowDirection.EndBtoEndA;
                    int k = (int)node.Node.Position.Value;
                    maximumXeAbsorption = Math.Max(maximumXeAbsorption, PracticeXenonDataV1.SigmaGroup2M2 * xe);
                    double delta = PracticeXenonDataV1.SigmaGroup2M2 * (xe - referenceAxial[reverse ? 11 - k : k]);
                    error = Math.Max(error, Math.Abs(delta - previousDelta[n]));
                    previousDelta[n] = delta;
                    entries.Add(new StaticAbsorptionOverlayEntryV1(node.Node,
                        overlay.Value.GetDeltaAbsorptionGroup1PerM(node.Node),
                        overlay.Value.GetDeltaAbsorptionGroup2PerM(node.Node) + delta));
                }
                double tolerance = Math.Max(1e-10, 4 * _dataPack.ConvergencePolicy.SourceShapeTolerance * maximumXeAbsorption);
                if (pass > 0 && error <= tolerance) return solved;
                var combined = StaticAbsorptionOverlayV1.TryCreate("time-average-equilibrium-xenon-replacement", entries);
                if (!combined.IsValid) return InvalidSolve(combined.FirstDiagnostic.Code, combined.FirstDiagnostic.Path, combined.FirstDiagnostic.Message);
                effective = ApplyStaticAbsorptionOverlay(coefficients.Value, combined.Value);
                if (!effective.IsValid) return InvalidSolve(effective.FirstDiagnostic.Code, effective.FirstDiagnostic.Path, effective.FirstDiagnostic.Message);
                solved = TrySolveWithCoefficients(inventory.Value, effective.Value, powerWatts,
                    solved.Value.EffectiveK, solved.Value.Group1Flux, solved.Value.Group2Flux, null, combined.Value, null);
                if (!solved.IsValid) return solved;
            }
            return InvalidSolve("PracticeReference.Xenon.NotConverged", "xenon", "Time-average poison replacement did not settle.");
        }
    }
}
