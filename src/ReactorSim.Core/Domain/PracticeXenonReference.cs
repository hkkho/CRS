using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ReactorSim.Core
{
    /// <summary>Explicit Xe-135 component assigned to the poison-inclusive fuel curve.
    /// The authored reference follows a declared depletion history; it is not a
    /// measured isotope inventory. Dynamic coupling replaces this component.</summary>
    public sealed class PracticeXenonReferenceV1
    {
        public const string Identity = "burnup-included-xe135-reference-v1";
        public const string BasisId = "included-burnup-reference";
        private readonly double[] _burnup, _xenon;
        internal PracticeXenonReferenceV1(double power, double[] burnup, double[] xenon)
        {
            ReferenceSpecificPowerWPerKgHm = power;
            _burnup = (double[])burnup.Clone(); _xenon = (double[])xenon.Clone();
            BurnupJPerKgHm = Array.AsReadOnly(_burnup);
            Xe135NumberDensityM3 = Array.AsReadOnly(_xenon);
            ReferenceDigest = new Digest32(Phase5CanonicalBytesV1.HashBody(Identity, writer =>
            {
                Phase5CanonicalBytesV1.WriteDigest(writer, PracticeXenonDataV1.DataDigest);
                writer.Write(power);
                for (int n = 0; n < _burnup.Length; n++) { writer.Write(_burnup[n]); writer.Write(_xenon[n]); }
            }));
        }
        public double ReferenceSpecificPowerWPerKgHm { get; }
        public IReadOnlyList<double> BurnupJPerKgHm { get; }
        public IReadOnlyList<double> Xe135NumberDensityM3 { get; }
        public Digest32 ReferenceDigest { get; }
        public double Average(double beginning, double end)
        {
            if (!ContractValidation.IsFinite(beginning) || !ContractValidation.IsFinite(end) || end <= beginning)
                throw new ArgumentOutOfRangeException(nameof(end));
            double[] knots = new[] { beginning }.Concat(_burnup.Where(b => b > beginning && b < end))
                .Concat(new[] { end }).ToArray();
            double sum = 0;
            for (int n = 1; n < knots.Length; n++)
                sum += .5 * (At(knots[n - 1]) + At(knots[n])) * (knots[n] - knots[n - 1]) / (end - beginning);
            return sum;
        }
        public double At(double burnup)
        {
            if (!ContractValidation.IsFinite(burnup) || burnup < _burnup[0] || burnup > _burnup[_burnup.Length - 1])
                throw new ArgumentOutOfRangeException(nameof(burnup));
            int index = Array.BinarySearch(_burnup, burnup);
            if (index >= 0) return _xenon[index];
            int hi = ~index, lo = hi - 1;
            double fraction = (burnup - _burnup[lo]) / (_burnup[hi] - _burnup[lo]);
            return _xenon[lo] + fraction * (_xenon[hi] - _xenon[lo]);
        }
    }
}
