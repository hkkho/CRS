using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ReactorSim.Core
{
    /// <summary>Opt-in synchronous diagnostics. Never used to make simulation decisions
    /// or included in state/replay digests. Disabled scopes read no clock and allocate nothing.</summary>
    public sealed class RuntimeProfile : IDisposable
    {
#if RUNTIME_PROFILE
        public static bool ScopesEnabled => true;
#else
        public static bool ScopesEnabled => false;
#endif
        [ThreadStatic] private static RuntimeProfile? _current;
        private readonly Dictionary<string, ProfileRow> _rows = new Dictionary<string, ProfileRow>();
        private Scope? _top;
        private RuntimeProfile() { }
        public static RuntimeProfile Begin()
        {
            if (_current != null) throw new InvalidOperationException("A profile is already active on this thread.");
            return _current = new RuntimeProfile();
        }
        public static IDisposable? Measure(string name) => _current == null ? null : new Scope(_current, name);
        public IReadOnlyList<ProfileRow> Rows => _rows.Values.OrderBy(row => row.Name, StringComparer.Ordinal).ToArray();
        public void Dispose() { if (ReferenceEquals(_current, this)) _current = null; }
        public sealed class ProfileRow
        {
            internal ProfileRow(string name) { Name = name; }
            public string Name { get; }
            public int Calls { get; internal set; }
            public double InclusiveMs { get; internal set; }
            public double ExclusiveMs { get; internal set; }
        }
        private sealed class Scope : IDisposable
        {
            private readonly RuntimeProfile _owner;
            private readonly ProfileRow _row;
            private readonly Scope? _parent;
            private readonly long _start;
            private long _children;
            private bool _disposed;
            internal Scope(RuntimeProfile owner, string name)
            {
                _owner = owner; _parent = owner._top;
                if (!owner._rows.TryGetValue(name, out ProfileRow row))
                    owner._rows.Add(name, row = new ProfileRow(name));
                _row = row; owner._top = this; _start = Stopwatch.GetTimestamp();
            }
            public void Dispose()
            {
                if (_disposed) return;
                long elapsed = Stopwatch.GetTimestamp() - _start;
                _disposed = true; _owner._top = _parent;
                if (_parent != null) _parent._children += elapsed;
                _row.Calls++;
                _row.InclusiveMs += elapsed * 1000.0 / Stopwatch.Frequency;
                _row.ExclusiveMs += (elapsed - _children) * 1000.0 / Stopwatch.Frequency;
            }
        }
    }
}
