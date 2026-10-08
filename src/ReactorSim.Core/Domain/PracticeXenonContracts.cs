using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ReactorSim.Core
{
    /// <summary>Authored gameplay poison data, SI units. Cross sections are deliberately
    /// softened for the compact synthetic diffusion pack; not plant nuclear data.</summary>
    public static class PracticeXenonDataV1
    {
        public const string Identity = "practice-iodine-xenon-analytic-v1";
        public const double GammaI = 0.063;
        public const double GammaXe = 0.003;
        public static readonly double LambdaI = Math.Log(2) / (6.57 * 3600);
        public static readonly double LambdaXe = Math.Log(2) / (9.14 * 3600);
        public const double SigmaGroup1M2 = 0;
        public const double SigmaGroup2M2 = 1.3e-24;
        public static readonly Digest32 DataDigest = new Digest32(Phase5CanonicalBytesV1.HashBody(Identity, writer =>
        {
            writer.Write(GammaI); writer.Write(GammaXe); writer.Write(LambdaI); writer.Write(LambdaXe);
            writer.Write(SigmaGroup1M2); writer.Write(SigmaGroup2M2);
        }));

        // Exact frozen-source Bateman step. The divided exponential has a regular
        // equal-rate limit and does not overflow when the rates straddle each other.
        public static (double Iodine, double Xenon) Advance(double iodine, double xenon,
            double fissionRateDensity, double flux1, double flux2, double seconds)
        {
            if (!Valid(iodine) || !Valid(xenon) || !Valid(fissionRateDensity) ||
                !Valid(flux1) || !Valid(flux2) || !Valid(seconds))
                throw new ArgumentOutOfRangeException(nameof(seconds), "Poison inputs must be finite and nonnegative.");
            double a = LambdaI;
            double b = LambdaXe + SigmaGroup1M2 * flux1 + SigmaGroup2M2 * flux2;
            double ea = Math.Exp(-a * seconds), eb = Math.Exp(-b * seconds);
            double ieq = GammaI * fissionRateDensity / a;
            double nextI = iodine * ea + GammaI * fissionRateDensity * seconds * Phi(a * seconds);
            double divided = Math.Exp(-Math.Min(a, b) * seconds) * seconds * Phi(Math.Abs(b - a) * seconds);
            double nextXe = xenon * eb + (GammaXe * fissionRateDensity + a * ieq) * seconds * Phi(b * seconds)
                + a * (iodine - ieq) * divided;
            if (!ContractValidation.IsFinite(nextI) || !ContractValidation.IsFinite(nextXe) || nextI < 0 || nextXe < 0)
                throw new InvalidOperationException("Analytic poison result must be finite and nonnegative.");
            return (nextI == 0 ? 0 : nextI, nextXe == 0 ? 0 : nextXe);
        }

        private static double Phi(double x) => x < 1e-5
            ? 1 - x / 2 + x * x / 6 - x * x * x / 24
            : (1 - Math.Exp(-x)) / x;
        private static bool Valid(double value) => ContractValidation.IsFinite(value) && value >= 0;
    }

    /// <summary>Immutable bundle-bound poison. Active packs replace an explicit
    /// included reference at each bundle's current burnup with live Xe exactly once.
    /// Archived undeclared packs retain their historical fixed-rebase behavior.</summary>
    public sealed class PracticeXenonStateV1
    {
        private readonly StableId[] _bundles;
        private readonly double[] _iodine, _xenon, _reference;
        private readonly PracticeXenonReferenceV1? _background;
        private readonly Lazy<Digest32> _stateDigest;
        private readonly Lazy<StaticAbsorptionOverlayV1> _overlay;
        public IReadOnlyList<double> Iodine { get; }
        public IReadOnlyList<double> Xenon { get; }
        public IReadOnlyList<double> IncludedReferenceXenon { get; }
        public double SimulationTimeSeconds { get; }
        public ulong StateVersion { get; }
        public Digest32 StateDigest => _stateDigest.Value;
        public StaticAbsorptionOverlayV1 Overlay => _overlay.Value;

        private PracticeXenonStateV1(StableId[] bundles, double[] iodine, double[] xenon,
            double[] reference, double time, ulong version, PracticeXenonReferenceV1? background = null)
        {
            _background = background;
            _bundles = bundles; _iodine = iodine; _xenon = xenon; _reference = reference;
            IncludedReferenceXenon = new ReadOnlyCollection<double>(reference);
            Iodine = new ReadOnlyCollection<double>(iodine);
            Xenon = new ReadOnlyCollection<double>(xenon);
            SimulationTimeSeconds = time; StateVersion = version;
            _stateDigest = new Lazy<Digest32>(() => new Digest32(Phase5CanonicalBytesV1.HashBody(PracticeXenonDataV1.Identity, writer =>
            {
                Phase5CanonicalBytesV1.WriteDigest(writer, PracticeXenonDataV1.DataDigest);
                if (background != null) Phase5CanonicalBytesV1.WriteDigest(writer, background.ReferenceDigest);
                writer.Write(time); writer.Write(version);
                for (int n = 0; n < bundles.Length; n++)
                {
                    Phase5CanonicalBytesV1.WriteStableId(writer, bundles[n]);
                    writer.Write(iodine[n]); writer.Write(xenon[n]); writer.Write(reference[n]);
                }
            })));
            _overlay = new Lazy<StaticAbsorptionOverlayV1>(() => BuildOverlayCore(null));
        }

        public static PracticeXenonStateV1 CreateEquilibrium(SyntheticGameCoreStateV1 core,
            EquilibriumCoreProjectionV1 projection, double time)
        {
            if (core == null || projection == null) throw new ArgumentNullException(nameof(core));
            if (!ContractValidation.IsFinite(time) || time < 0) throw new ArgumentOutOfRangeException(nameof(time));
            BundleState[] bundles = core.EnumerateBundles().ToArray();
            if (projection.ShapeNodePowerWatts.Count != bundles.Length)
                throw new ArgumentException("Poison state requires the complete channel-major practice topology.");
            var iodine = new double[bundles.Length]; var xenon = new double[bundles.Length];
            for (int n = 0; n < bundles.Length; n++)
            {
                double f = FissionRate(projection, n);
                iodine[n] = PracticeXenonDataV1.GammaI * f / PracticeXenonDataV1.LambdaI;
                xenon[n] = (PracticeXenonDataV1.GammaXe + PracticeXenonDataV1.GammaI) * f /
                    (PracticeXenonDataV1.LambdaXe + PracticeXenonDataV1.SigmaGroup2M2 * projection.ShapeGroup2[n]);
            }
            var background = projection.DataPack.XenonReference;
            double[] reference = background == null ? (double[])xenon.Clone() :
                bundles.Select(b => background.At(b.CurrentBurnupJPerKgHm)).ToArray();
            return new PracticeXenonStateV1(bundles.Select(b => b.BundleId).ToArray(), iodine, xenon,
                reference, time, 0, background);
        }

        public PracticeXenonStateV1 Advance(EquilibriumCoreProjectionV1 projection, double amplitude, double seconds)
        {
#if RUNTIME_PROFILE
            using var profileScope = ReactorSim.Core.RuntimeProfile.Measure("isotopes");
#endif
            if (projection == null || projection.ShapeNodePowerWatts.Count != _bundles.Length)
                throw new ArgumentException("Poison update requires the complete practice projection.");
            if (!ContractValidation.IsFinite(amplitude) || amplitude < 0 ||
                !ContractValidation.IsFinite(seconds) || seconds <= 0 ||
                !ContractValidation.IsFinite(SimulationTimeSeconds + seconds))
                throw new ArgumentOutOfRangeException(nameof(seconds));
            var iodine = new double[_iodine.Length]; var xenon = new double[_xenon.Length];
            for (int n = 0; n < iodine.Length; n++)
            {
                double f = FissionRate(projection, n) * amplitude;
                (iodine[n], xenon[n]) = PracticeXenonDataV1.Advance(_iodine[n], _xenon[n], f,
                    projection.ShapeGroup1[n] * amplitude, projection.ShapeGroup2[n] * amplitude, seconds);
            }
            return new PracticeXenonStateV1(_bundles, iodine, xenon, _reference,
                SimulationTimeSeconds + seconds, checked(StateVersion + 1), _background);
        }

        public PracticeXenonStateV1 Rebind(SyntheticGameCoreStateV1 core)
        {
            BundleState[] bundles = core.EnumerateBundles().ToArray();
            var indices = new Dictionary<StableId, int>(_bundles.Length);
            for (int n = 0; n < _bundles.Length; n++) indices.Add(_bundles[n], n);
            var iodine = new double[bundles.Length]; var xenon = new double[bundles.Length];
            for (int n = 0; n < bundles.Length; n++)
                if (indices.TryGetValue(bundles[n].BundleId, out int old))
                { iodine[n] = _iodine[old]; xenon[n] = _xenon[old]; }
            // Included reference poison belongs to the current fuel burnup, not its old spatial slot.
            double[] reference = _background == null ? _reference :
                bundles.Select(b => _background.At(b.CurrentBurnupJPerKgHm)).ToArray();
            return new PracticeXenonStateV1(bundles.Select(b => b.BundleId).ToArray(), iodine, xenon,
                reference, SimulationTimeSeconds, checked(StateVersion + 1), _background);
        }

        public PracticeXenonStateV1 BindBurnupReference(SyntheticGameCoreStateV1 core)
        {
            if (_background == null) return this;
            BundleState[] bundles = core.EnumerateBundles().ToArray();
            if (bundles.Length != _bundles.Length || bundles.Where((b, n) => b.BundleId != _bundles[n]).Any())
                throw new ArgumentException("Burnup binding must retain the exact poison bundle identities.", nameof(core));
            return new PracticeXenonStateV1(_bundles, _iodine, _xenon,
                bundles.Select(b => _background.At(b.CurrentBurnupJPerKgHm)).ToArray(),
                SimulationTimeSeconds, StateVersion, _background);
        }

        private static double FissionRate(EquilibriumCoreProjectionV1 projection, int n)
        {
            var row = projection.SpatialSolve.Coefficients.Nodes[n];
            return row.FissionGroup1PerM * projection.ShapeGroup1[n] +
                row.FissionGroup2PerM * projection.ShapeGroup2[n];
        }

        public StaticAbsorptionOverlayV1 BuildOverlay(IEnumerable<NodeKey>? nonfuel = null)
        {
            return nonfuel == null || !nonfuel.Any() ? Overlay : BuildOverlayCore(nonfuel);
        }

        private StaticAbsorptionOverlayV1 BuildOverlayCore(IEnumerable<NodeKey>? nonfuel)
        {
#if RUNTIME_PROFILE
            using var profileScope = ReactorSim.Core.RuntimeProfile.Measure("poison-overlay");
#endif
            var excluded = nonfuel == null ? null : new HashSet<NodeKey>(nonfuel);
            var entries = new StaticAbsorptionOverlayEntryV1[_xenon.Length];
            for (int n = 0; n < entries.Length; n++)
            {
                var node = new NodeKey(new ChannelId((uint)(n / 12)), new BundlePosition((uint)(n % 12)));
                double delta = excluded != null && excluded.Contains(node) ? 0 :
                    PracticeXenonDataV1.SigmaGroup2M2 * (_xenon[n] - _reference[n]);
                entries[n] = new StaticAbsorptionOverlayEntryV1(node, 0, delta == 0 ? 0 : delta);
            }
            var result = StaticAbsorptionOverlayV1.TryCreate(PracticeXenonDataV1.Identity + (_background == null ? "/fixed-reference-calibration" : "/burnup-reference-replacement"), entries);
            if (!result.IsValid) throw new InvalidOperationException(result.FirstDiagnostic.ToString());
            return result.Value;
        }
    }
}
