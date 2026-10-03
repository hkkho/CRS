using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using ReactorSim.Core;

namespace ReactorSim.Game
{
    internal sealed class ConfiguredCoreDesign
    {
        internal ConfiguredCoreDesign(
            IEnumerable<NodeKey> nonfuelNodes,
            IEnumerable<KeyValuePair<NodeKey, IEnumerable<TopologyFace>>> reflectiveFaceOverrides)
        {
            NodeKey[] orderedNonfuelNodes = (nonfuelNodes ?? throw new ArgumentNullException(nameof(nonfuelNodes)))
                .Distinct()
                .OrderBy(node => node)
                .ToArray();
            _nonfuelNodes = new ReadOnlyCollection<NodeKey>(orderedNonfuelNodes);

            var map = new Dictionary<NodeKey, IReadOnlyList<TopologyFace>>();
            if (reflectiveFaceOverrides == null)
            {
                throw new ArgumentNullException(nameof(reflectiveFaceOverrides));
            }

            foreach (KeyValuePair<NodeKey, IEnumerable<TopologyFace>> entry in
                reflectiveFaceOverrides.OrderBy(item => item.Key))
            {
                TopologyFace[] faces = (entry.Value ?? throw new ArgumentNullException(nameof(reflectiveFaceOverrides)))
                    .Distinct()
                    .OrderBy(face => (byte)face)
                    .ToArray();
                if (faces.Length > 0)
                {
                    map[entry.Key] = new ReadOnlyCollection<TopologyFace>(faces);
                }
            }

            _reflectiveFaceOverrides =
                new ReadOnlyDictionary<NodeKey, IReadOnlyList<TopologyFace>>(map);
        }

        private readonly IReadOnlyList<NodeKey> _nonfuelNodes;
        private readonly IReadOnlyDictionary<NodeKey, IReadOnlyList<TopologyFace>>
            _reflectiveFaceOverrides;

        internal IReadOnlyList<NodeKey> NonfuelNodes
        {
            get { return _nonfuelNodes; }
        }

        internal IReadOnlyDictionary<NodeKey, IReadOnlyList<TopologyFace>>
            ReflectiveFaceOverrides
        {
            get { return _reflectiveFaceOverrides; }
        }
    }
    internal sealed class ConfiguredCoreTransaction
    {
        internal ConfiguredCoreTransaction(
            EquilibriumCoreSolverV1 solver,
            PracticeLiquidZoneRrsV1 rrs,
            ConfiguredCoreDesign design,
            ulong powerProjectionVersion)
        {
            Solver = solver;
            Rrs = rrs;
            Design = design;
            PowerProjectionVersion = powerProjectionVersion;
        }

        internal EquilibriumCoreSolverV1 Solver { get; }

        internal PracticeLiquidZoneRrsV1 Rrs { get; }

        internal ConfiguredCoreDesign Design { get; }

        internal ulong PowerProjectionVersion { get; }
    }
}
