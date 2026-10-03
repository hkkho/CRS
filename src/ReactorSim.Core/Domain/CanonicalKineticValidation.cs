using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ReactorSim.Core
{
    internal static class KineticContractValidation
    {
        internal static bool IsPositiveFinite(double value)
        {
            return IsCanonicalFinite(value) && value > 0.0;
        }

        internal static bool IsCanonicalNonnegativeFinite(double value)
        {
            return IsCanonicalFinite(value) && value >= 0.0;
        }

        internal static bool IsCanonicalFinite(double value)
        {
            return ContractValidation.IsFinite(value) &&
                   BitConverter.DoubleToInt64Bits(value) != long.MinValue;
        }
    }
}
