using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace ReactorSim.Core
{
    /// <summary>Read-only numerical experiment, never an accepted game candidate.
    /// Packs the accepted Core operator for a generic GPU executor and supplies an
    /// independent f64 reference using the existing validated CPU operator.</summary>
    public sealed class GpuSpatialPrototypeFixtureV1
    {
        public const string Identity = "candu-spatial-gpu-prototype-v1";
        public const uint BoundaryTarget = uint.MaxValue;
        private static readonly Lazy<string> Shader = new Lazy<string>(() =>
        {
            using var stream = typeof(GpuSpatialPrototypeFixtureV1).Assembly.GetManifestResourceStream(
                "ReactorSim.Core.Gpu.spatial-prototype-v1.wgsl") ?? throw new InvalidOperationException("GPU kernel resource missing.");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        });
        private GpuSpatialPrototypeFixtureV1() { }
        public int NodeCount { get; private set; }
        public int Group { get; private set; }
        public int Iterations { get; private set; }
        public string CoefficientDigestHex { get; private set; } = "";
        public string KernelDigestHex { get; private set; } = "";
        public string ShaderSource { get; private set; } = "";
        public IReadOnlyList<uint> RowOffsets { get; private set; } = Array.Empty<uint>();
        public IReadOnlyList<uint> Targets { get; private set; } = Array.Empty<uint>();
        // Per node: removal [m^-1], volume [m^3], Jacobi diagonal [m^-1], source [m^-3 s^-1].
        public IReadOnlyList<float> NodeData { get; private set; } = Array.Empty<float>();
        public IReadOnlyList<float> Conductances { get; private set; } = Array.Empty<float>();
        public IReadOnlyList<float> InputFlux { get; private set; } = Array.Empty<float>();
        public IReadOnlyList<double> ReferenceApplied { get; private set; } = Array.Empty<double>();
        public IReadOnlyList<double> ReferenceFlux { get; private set; } = Array.Empty<double>();
        public double ReferenceRelativeResidual { get; private set; }
        public double CpuOperatorMilliseconds { get; private set; }
        public double CpuIterationMilliseconds { get; private set; }
        public double RequiredRelativeTolerance { get; private set; }

        public static GpuSpatialPrototypeFixtureV1 Create(FullCoreDiffusionSolveResultV1 solve,
            SpatialEnergyGroup group, int iterations)
        {
            if (solve == null) throw new ArgumentNullException(nameof(solve));
            if (group != SpatialEnergyGroup.Group1 && group != SpatialEnergyGroup.Group2)
                throw new ArgumentOutOfRangeException(nameof(group));
            if (iterations < 0 || iterations > 128) throw new ArgumentOutOfRangeException(nameof(iterations));
            var coefficients = solve.Coefficients;
            var stencil = coefficients.Stencil;
            var opResult = SpatialOperator.TryCreate(stencil, coefficients);
            if (!opResult.IsValid) throw new InvalidOperationException(opResult.FirstDiagnostic.ToString());
            var op = opResult.Value;
            int count = stencil.NodeCount;
            var offsets = new uint[count + 1]; var targets = new List<uint>();
            var conductances = new List<float>(); var data = new float[count * 4];
            var input = new float[count]; var source = new double[count]; var diagonal = new double[count];
            for (int n = 0; n < count; n++)
            {
                var node = stencil.Nodes[n]; var row = coefficients.Nodes[n];
                offsets[n] = checked((uint)targets.Count);
                double sum = 0;
                foreach (var neighbor in node.NeighborTerms)
                {
                    if (!coefficients.TryGetEdge(new SpatialEdgeKey(node.Node, neighbor.TargetNode), out var pair))
                        throw new InvalidOperationException("GPU prototype edge is not bound.");
                    double c = group == SpatialEnergyGroup.Group1 ? pair.Group1 : pair.Group2;
                    targets.Add(checked((uint)neighbor.TargetFlatIndex)); conductances.Add(ToFloat(c)); sum += c;
                }
                foreach (var boundary in node.BoundaryTerms)
                {
                    if (!coefficients.TryGetBoundary(new SpatialBoundaryKey(node.Node, boundary.Face), out var pair))
                        throw new InvalidOperationException("GPU prototype boundary is not bound.");
                    double c = group == SpatialEnergyGroup.Group1 ? pair.Group1 : pair.Group2;
                    targets.Add(BoundaryTarget); conductances.Add(ToFloat(c)); sum += c;
                }
                double removal = group == SpatialEnergyGroup.Group1
                    ? row.AbsorptionGroup1PerM + row.DownscatterGroup1To2PerM : row.AbsorptionGroup2PerM;
                diagonal[n] = removal + sum / row.VolumeM3;
                double fission = row.NuFissionGroup1PerM * solve.Group1Flux[n] + row.NuFissionGroup2PerM * solve.Group2Flux[n];
                source[n] = group == SpatialEnergyGroup.Group1 ? row.ChiGroup1 * fission / solve.EffectiveK
                    : row.DownscatterGroup1To2PerM * solve.Group1Flux[n] + row.ChiGroup2 * fission / solve.EffectiveK;
                double flux = group == SpatialEnergyGroup.Group1 ? solve.Group1Flux[n] : solve.Group2Flux[n];
                // Perturb the warm start so fixed-step comparisons exercise iteration, not only equilibrium.
                input[n] = ToFloat(flux * (n % 2 == 0 ? 1.02 : 0.98));
                data[n * 4] = ToFloat(removal); data[n * 4 + 1] = ToFloat(row.VolumeM3);
                data[n * 4 + 2] = ToFloat(diagonal[n]); data[n * 4 + 3] = ToFloat(source[n]);
                if (data[n * 4 + 1] <= 0 || data[n * 4 + 2] <= 0)
                    throw new InvalidOperationException("GPU prototype volume and diagonal must remain positive in f32.");
            }
            offsets[count] = checked((uint)targets.Count);
            var current = input.Select(value => (double)value).ToArray();
            var applied = new double[count]; var initialApplied = new double[count];
            var watch = Stopwatch.StartNew();
            if (!op.TryApply(group, current, applied, out var failure)) throw new InvalidOperationException(failure.ToString());
            double operatorMs = watch.Elapsed.TotalMilliseconds;
            Array.Copy(applied, initialApplied, count);
            watch.Restart();
            for (int step = 0; step < iterations; step++)
            {
                for (int n = 0; n < count; n++)
                {
                    double next = current[n] + (source[n] - applied[n]) / diagonal[n];
                    if (!ContractValidation.IsFinite(next) || next < 0) throw new InvalidOperationException("CPU prototype iterate invalid.");
                    current[n] = next;
                }
                if (!op.TryApply(group, current, applied, out failure)) throw new InvalidOperationException(failure.ToString());
            }
            double iterationMs = watch.Elapsed.TotalMilliseconds;
            double residual = 0, scale = 0;
            for (int n = 0; n < count; n++)
            {
                residual = Math.Max(residual, Math.Abs(applied[n] - source[n]));
                scale = Math.Max(scale, Math.Abs(applied[n]) + Math.Abs(source[n]));
            }
            return new GpuSpatialPrototypeFixtureV1
            {
                NodeCount = count,
                Group = (int)group,
                Iterations = iterations,
                ShaderSource = Shader.Value,
                CoefficientDigestHex = ConvertDigest(solve.CoefficientBindingDigest),
                KernelDigestHex = ConvertDigest(new Digest32(Phase5CanonicalBytesV1.HashBody(Identity, writer => writer.Write(Shader.Value)))),
                RowOffsets = ReadOnly(offsets),
                Targets = ReadOnly(targets.ToArray()),
                NodeData = ReadOnly(data),
                Conductances = ReadOnly(conductances.ToArray()),
                InputFlux = ReadOnly(input),
                ReferenceApplied = ReadOnly(initialApplied),
                ReferenceFlux = ReadOnly(current),
                ReferenceRelativeResidual = scale == 0 ? 0 : residual / scale,
                CpuOperatorMilliseconds = operatorMs,
                CpuIterationMilliseconds = iterationMs,
                RequiredRelativeTolerance = solve.DataPack.LinearSolvePolicy.RelativeResidualTolerance
            };
        }
        private static ReadOnlyCollection<T> ReadOnly<T>(T[] array) => new ReadOnlyCollection<T>(array);
        private static float ToFloat(double value)
        {
            float result = (float)value;
            if (!ContractValidation.IsFinite(value) || value < 0 || float.IsNaN(result) || float.IsInfinity(result))
                throw new InvalidOperationException("GPU prototype input is outside finite nonnegative f32 range.");
            return result;
        }
        private static string ConvertDigest(Digest32 value) => BitConverter.ToString(value.Bytes.ToArray()).Replace("-", "").ToLowerInvariant();
    }
}
