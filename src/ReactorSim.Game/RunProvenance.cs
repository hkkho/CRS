using System;
using System.Collections.Generic;
using System.Linq;

namespace ReactorSim.Game
{
    /// <summary>Immutable eligibility for comparing a run with the authored standard challenge.</summary>
    public sealed class RunProvenance
    {
        internal RunProvenance(bool challenge, IEnumerable<string> reasons)
        {
            Reasons = Array.AsReadOnly(reasons.Distinct(StringComparer.Ordinal).ToArray());
            IsModified = Reasons.Count > 0;
            Kind = IsModified ? "modified-sandbox" : challenge ? "standard-challenge" : "free-practice";
            Label = IsModified ? "Modified sandbox" : challenge ? "Standard challenge" : "Free practice";
            EligibleForStandardChallenge = challenge && !IsModified;
        }
        public string Kind { get; }
        public string Label { get; }
        public bool IsModified { get; }
        public bool EligibleForStandardChallenge { get; }
        public IReadOnlyList<string> Reasons { get; }
    }
}
