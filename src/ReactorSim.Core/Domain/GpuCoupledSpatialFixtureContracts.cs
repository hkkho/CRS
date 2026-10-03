using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace ReactorSim.Core
{
    /// <summary>Complete coupled solve experiment and independent f64 validation. Never commits game state.</summary>
    public sealed class GpuCoupledSpatialFixtureV1
    {
        public const string Identity = "candu-coupled-gpu-experiment-v1";
        private static readonly Lazy<string> Shader = new Lazy<string>(() =>
        {
            using var stream = typeof(GpuCoupledSpatialFixtureV1).Assembly.GetManifestResourceStream(
                "ReactorSim.Core.Gpu.coupled-spatial-v1.wgsl") ?? throw new InvalidOperationException("Coupled GPU kernel missing.");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        });
        private readonly FullCoreDiffusionSolveResultV1 _accepted;
        private PracticeLiquidZoneRrsMappingV1? _mapping;
        private GpuCoupledSpatialFixtureV1(FullCoreDiffusionSolveResultV1 accepted) { _accepted = accepted; ShaderSource = Shader.Value; }
        public int NodeCount => _accepted.Coefficients.Stencil.NodeCount;
        public string ShaderSource { get; }
        public string KernelDigest => BitConverter.ToString(new Digest32(Phase5CanonicalBytesV1.HashBody(Identity,
            writer => writer.Write(ShaderSource))).Bytes.ToArray()).Replace("-", "").ToLowerInvariant();
        public uint[] Indices { get; private set; } = Array.Empty<uint>();
        public float[] Conductances { get; private set; } = Array.Empty<float>();
        public float[] Nodes { get; private set; } = Array.Empty<float>();
        public double[] InitialFlux { get; private set; } = Array.Empty<double>();
        public double[] ReferenceFlux { get; private set; } = Array.Empty<double>();
        public double ReferenceK { get; private set; }
        public double InitialK { get; private set; }
        public double TargetPower => _accepted.TotalPowerWatts;
        public double CpuSolveMilliseconds { get; private set; }
        public int CpuIterations { get; private set; }
        public SpatialLinearSolvePolicy InnerPolicy => _accepted.DataPack.LinearSolvePolicy;
        public SpatialConvergencePolicy OuterPolicy => _accepted.DataPack.ConvergencePolicy;
        public string CoefficientDigest => BitConverter.ToString(_accepted.CoefficientBindingDigest.Bytes.ToArray()).Replace("-", "").ToLowerInvariant();

        public static GpuCoupledSpatialFixtureV1 Create(FullCoreDiffusionSolveResultV1 accepted, bool cold,
            PracticeLiquidZoneRrsMappingV1? mapping = null)
        {
            if (accepted == null) throw new ArgumentNullException(nameof(accepted));
            var fixture = new GpuCoupledSpatialFixtureV1(accepted);
            fixture._mapping = mapping;
            var coefficients = accepted.Coefficients;
            var offsets = new uint[fixture.NodeCount + 1]; var targets = new List<uint>(); var edges = new List<float>();
            var data = new float[fixture.NodeCount * 12];
            for (int n = 0; n < fixture.NodeCount; n++)
            {
                var stencil = coefficients.Stencil.Nodes[n]; var row = coefficients.Nodes[n];
                offsets[n] = (uint)targets.Count; double sum1 = 0, sum2 = 0;
                foreach (var edge in stencil.NeighborTerms)
                {
                    if (!coefficients.TryGetEdge(new SpatialEdgeKey(stencil.Node, edge.TargetNode), out var pair)) throw new InvalidOperationException("Missing GPU edge.");
                    targets.Add((uint)edge.TargetFlatIndex); edges.Add(F32(pair.Group1)); edges.Add(F32(pair.Group2)); sum1 += pair.Group1; sum2 += pair.Group2;
                }
                foreach (var boundary in stencil.BoundaryTerms)
                {
                    if (!coefficients.TryGetBoundary(new SpatialBoundaryKey(stencil.Node, boundary.Face), out var pair)) throw new InvalidOperationException("Missing GPU boundary.");
                    targets.Add(uint.MaxValue); edges.Add(F32(pair.Group1)); edges.Add(F32(pair.Group2)); sum1 += pair.Group1; sum2 += pair.Group2;
                }
                double r1 = row.AbsorptionGroup1PerM + row.DownscatterGroup1To2PerM;
                double power1 = row.PowerResponse?.Group1WattsPerFluxDensity ?? row.VolumeM3 * row.EnergyPerFissionJ * row.FissionGroup1PerM;
                double power2 = row.PowerResponse?.Group2WattsPerFluxDensity ?? row.VolumeM3 * row.EnergyPerFissionJ * row.FissionGroup2PerM;
                double[] values = { r1, row.AbsorptionGroup2PerM, row.VolumeM3, row.DownscatterGroup1To2PerM,
                    r1 + sum1 / row.VolumeM3, row.AbsorptionGroup2PerM + sum2 / row.VolumeM3, row.NuFissionGroup1PerM, row.NuFissionGroup2PerM,
                    row.ChiGroup1, row.ChiGroup2, power1, power2 };
                for (int j = 0; j < 12; j++) data[n * 12 + j] = F32(values[j]);
            }
            offsets[fixture.NodeCount] = (uint)targets.Count;
            fixture.Indices = offsets.Concat(targets).ToArray(); fixture.Conductances = edges.ToArray(); fixture.Nodes = data;
            var g1 = new double[fixture.NodeCount]; var g2 = new double[fixture.NodeCount];
            for (int n = 0; n < fixture.NodeCount; n++)
            {
                g1[n] = cold ? 1 : accepted.Group1Flux[n] * (n % 2 == 0 ? 1.02 : .98);
                g2[n] = cold ? 1 : accepted.Group2Flux[n] * (n % 2 == 0 ? .98 : 1.02);
            }
            fixture.InitialK = cold ? 1 : accepted.EffectiveK * 1.01;
            var iteration = Require(SpatialEigenIteration.TryCreate(coefficients.Stencil, coefficients, fixture.InnerPolicy,
                fixture.TargetPower, fixture.InitialK, g1, g2));
            fixture.InitialFlux = Interleave(iteration.InitialState.Group1Flux, iteration.InitialState.Group2Flux);
            var watch = Stopwatch.StartNew();
            var reference = Require(Require(SpatialEigenSolve.TryCreate(iteration, fixture.OuterPolicy)).TrySolve());
            fixture.CpuSolveMilliseconds = watch.Elapsed.TotalMilliseconds;
            if (!reference.IsConverged || reference.FinalState == null) throw new InvalidOperationException("CPU coupled reference did not converge.");
            fixture.ReferenceK = reference.FinalState.Eigenvalue;
            fixture.ReferenceFlux = Interleave(reference.FinalState.Group1Flux, reference.FinalState.Group2Flux);
            fixture.CpuIterations = reference.Diagnostics.IterationCount;
            return fixture;
        }

        /// <summary>Checks both successive GPU states with the existing f64 equations. Normalizes power in f64;
        /// this repairs representation roundoff only and does not iterate or alter shape.</summary>
        public GpuCoupledValidationV1 Validate(double k, double previousK, double[] flux, double[] previousFlux, int iterations)
        {
            if (iterations < 1 || iterations > OuterPolicy.MaximumIterations) throw new ArgumentOutOfRangeException(nameof(iterations));
            var current = Normalize(flux); var previous = Normalize(previousFlux);
            var iteration = Require(SpatialEigenIteration.TryCreate(_accepted.Coefficients.Stencil, _accepted.Coefficients,
                InnerPolicy, TargetPower, InitialK));
            var wrapper = Require(SpatialEigenSolve.TryCreate(iteration, OuterPolicy));
            var now = MakeState(iteration, k, current, iterations); var before = MakeState(iteration, previousK, previous, iterations - 1);
            var shape = new double[NodeCount]; var oldShape = new double[NodeCount];
            if (!wrapper.TryEvaluateState(now, shape, out var metrics, out var failure) ||
                !wrapper.TryEvaluateState(before, oldShape, out _, out failure)) throw new InvalidOperationException(failure.ToString());
            double delta = Math.Abs(k - previousK), shapeDelta = shape.Zip(oldShape, (a, b) => Math.Abs(a - b)).Max();
            double scale = ReferenceFlux.Max(Math.Abs);
            double error = current.Zip(ReferenceFlux, (a, b) => Math.Abs(a - b)).Max() / scale;
            double reactivityErrorMk = Math.Abs((k - 1) / k - (ReferenceK - 1) / ReferenceK) * 1000;
            double g1Error = 0, g2Error = 0, g1Scale = 0, g2Scale = 0, powerError = 0, powerScale = 0;
            double total = 0, referenceTotal = 0;
            var regions = new double[14]; var referenceRegions = new double[14];
            for (int n = 0; n < NodeCount; n++)
            {
                g1Scale = Math.Max(g1Scale, ReferenceFlux[n * 2]); g2Scale = Math.Max(g2Scale, ReferenceFlux[n * 2 + 1]);
                g1Error = Math.Max(g1Error, Math.Abs(current[n * 2] - ReferenceFlux[n * 2]));
                g2Error = Math.Max(g2Error, Math.Abs(current[n * 2 + 1] - ReferenceFlux[n * 2 + 1]));
                double nodePower = Power(n, current[n * 2], current[n * 2 + 1]);
                double referencePower = Power(n, ReferenceFlux[n * 2], ReferenceFlux[n * 2 + 1]);
                powerError = Math.Max(powerError, Math.Abs(nodePower - referencePower)); powerScale = Math.Max(powerScale, referencePower);
                total += nodePower; referenceTotal += referencePower;
                if (_mapping != null)
                {
                    int zone = (int)_mapping.Nodes[n].LogicalZoneId;
                    regions[zone] += nodePower; referenceRegions[zone] += referencePower;
                }
            }
            g1Error /= g1Scale; g2Error /= g2Scale; powerError /= powerScale;
            double regionalError = _mapping == null ? 0 : regions.Zip(referenceRegions,
                (a, b) => Math.Abs(a / total - b / referenceTotal)).Max();
            return new GpuCoupledValidationV1
            {
                CpuVerifiedConverged = (delta <= OuterPolicy.KAbsoluteTolerance || delta / Math.Max(k, previousK) <= OuterPolicy.KRelativeTolerance) &&
                    metrics.ResidualRelativeInfinity <= OuterPolicy.ResidualTolerance && shapeDelta <= OuterPolicy.SourceShapeTolerance &&
                    metrics.PowerBalanceRelative <= OuterPolicy.PowerBalanceTolerance,
                Residual = metrics.ResidualRelativeInfinity,
                ShapeChange = shapeDelta,
                PowerBalance = metrics.PowerBalanceRelative,
                FluxError = error,
                ReactivityErrorMk = reactivityErrorMk,
                Group1FluxError = g1Error,
                Group2FluxError = g2Error,
                NodePowerError = powerError,
                RegionalFractionError = regionalError,
                RegionsChecked = _mapping != null,
                AgreementPassed = g1Error <= 1e-4 && g2Error <= 1e-4 && powerError <= 1e-4 &&
                    regionalError <= PracticeLiquidZoneRrsIdentityV1.ControllerTolerance &&
                    reactivityErrorMk <= PracticeLiquidZoneRrsIdentityV1.CriticalityToleranceMk
            };
        }
        private double[] Normalize(double[] values)
        {
            if (values == null || values.Length != NodeCount * 2 || values.Any(x => !ContractValidation.IsFinite(x) || x < 0)) throw new ArgumentException("Invalid GPU flux.");
            var result = (double[])values.Clone();
            double power = 0;
            for (int n = 0; n < NodeCount; n++) power += Power(n, result[n * 2], result[n * 2 + 1]);
            if (!ContractValidation.IsFinite(power) || power <= 0) throw new ArgumentException("Invalid GPU power.");
            double factor = TargetPower / power;
            for (int n = 0; n < result.Length; n++) result[n] *= factor;
            return result;
        }
        private double Power(int n, double a, double b)
        {
            var row = _accepted.Coefficients.Nodes[n];
            return row.PowerResponse == null ? row.VolumeM3 * row.EnergyPerFissionJ * (row.FissionGroup1PerM * a + row.FissionGroup2PerM * b)
                : row.PowerResponse.Group1WattsPerFluxDensity * a + row.PowerResponse.Group2WattsPerFluxDensity * b;
        }
        private SpatialEigenIterationState MakeState(SpatialEigenIteration owner, double k, double[] interleaved, int count)
        {
            double power = 0, production = 0; var a = new double[NodeCount]; var b = new double[NodeCount];
            for (int n = 0; n < NodeCount; n++)
            {
                a[n] = interleaved[n * 2]; b[n] = interleaved[n * 2 + 1]; var row = _accepted.Coefficients.Nodes[n];
                power += Power(n, a[n], b[n]); production += row.VolumeM3 * (row.NuFissionGroup1PerM * a[n] + row.NuFissionGroup2PerM * b[n]);
            }
            return new SpatialEigenIterationState(owner, count, k, 1, power, production, a, b);
        }
        private static double[] Interleave(IReadOnlyList<double> a, IReadOnlyList<double> b)
        {
            var result = new double[a.Count * 2];
            for (int n = 0; n < a.Count; n++)
            {
                result[n * 2] = a[n];
                result[n * 2 + 1] = b[n];
            }
            return result;
        }
        private static float F32(double value)
        {
            float result = (float)value;
            if (!ContractValidation.IsFinite(value) || value < 0 || float.IsInfinity(result)) throw new InvalidOperationException("GPU coefficient outside f32 range.");
            return result;
        }
        private static T Require<T>(ContractValidationResult<T> result) => result.IsValid ? result.Value : throw new InvalidOperationException(result.FirstDiagnostic.ToString());
    }
    public sealed class GpuCoupledValidationV1
    {
        public bool CpuVerifiedConverged { get; internal set; }
        public bool AgreementPassed { get; internal set; }
        public double Residual { get; internal set; }
        public double ShapeChange { get; internal set; }
        public double PowerBalance { get; internal set; }
        public double FluxError { get; internal set; }
        public double ReactivityErrorMk { get; internal set; }
        public double Group1FluxError { get; internal set; }
        public double Group2FluxError { get; internal set; }
        public double NodePowerError { get; internal set; }
        public double RegionalFractionError { get; internal set; }
        public bool RegionsChecked { get; internal set; }
    }
}
