using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace ReactorSim.Core
{
    /// <summary>
    /// One explicit nodewise static absorption delta. The delta is expressed
    /// in inverse metres and is deliberately independent of xenon, kinetics,
    /// and any particular controller implementation.
    /// </summary>
    public sealed class StaticAbsorptionOverlayEntryV1
    {
        public StaticAbsorptionOverlayEntryV1(
            NodeKey node,
            double deltaAbsorptionGroup1PerM,
            double deltaAbsorptionGroup2PerM)
        {
            if (!IsCanonicalFinite(deltaAbsorptionGroup1PerM) ||
                !IsCanonicalFinite(deltaAbsorptionGroup2PerM))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(deltaAbsorptionGroup1PerM),
                    "Static absorption deltas must be finite and free of signed negative zero.");
            }

            Node = node;
            DeltaAbsorptionGroup1PerM = deltaAbsorptionGroup1PerM;
            DeltaAbsorptionGroup2PerM = deltaAbsorptionGroup2PerM;
        }

        public NodeKey Node { get; }

        public double DeltaAbsorptionGroup1PerM { get; }

        public double DeltaAbsorptionGroup2PerM { get; }

        public static ContractValidationResult<StaticAbsorptionOverlayEntryV1> TryCreate(
            NodeKey node,
            double deltaAbsorptionGroup1PerM,
            double deltaAbsorptionGroup2PerM)
        {
            if (!IsCanonicalFinite(deltaAbsorptionGroup1PerM) ||
                !IsCanonicalFinite(deltaAbsorptionGroup2PerM))
            {
                return ContractValidationResult<StaticAbsorptionOverlayEntryV1>.Invalid(
                    "StaticAbsorptionOverlay.Entry.NonFinite",
                    ContractValidation.NodePath(node, ".delta_absorption_m_inverse"),
                    "Static absorption deltas must be finite and free of signed negative zero.");
            }

            return ContractValidationResult<StaticAbsorptionOverlayEntryV1>.Valid(
                new StaticAbsorptionOverlayEntryV1(
                    node,
                    deltaAbsorptionGroup1PerM,
                    deltaAbsorptionGroup2PerM));
        }

        private static bool IsCanonicalFinite(double value)
        {
            return ContractValidation.IsFinite(value) &&
                   (value != 0.0 || BitConverter.DoubleToInt64Bits(value) >= 0);
        }
    }

    /// <summary>
    /// Immutable, explicitly identified nodewise static absorption overlay.
    /// Sparse overlays are accepted by the seam; missing nodes mean a zero
    /// delta. Callers that need a complete map, such as the practice RRS,
    /// provide one entry for every diffusion node.
    /// </summary>
    public sealed class StaticAbsorptionOverlayV1
    {
        public const uint CurrentSchemaVersion = 1;
        public const string TargetUnit = "m^-1";

        private readonly ReadOnlyCollection<StaticAbsorptionOverlayEntryV1> _entries;
        private readonly Dictionary<NodeKey, StaticAbsorptionOverlayEntryV1> _byNode;
        private readonly string _unitIdentity;

        private StaticAbsorptionOverlayV1(
            string sourceIdentity,
            IEnumerable<StaticAbsorptionOverlayEntryV1> entries,
            Digest32 overlayDigest)
        {
            SourceIdentity = sourceIdentity;
            _entries = new ReadOnlyCollection<StaticAbsorptionOverlayEntryV1>(entries.ToArray());
            _byNode = _entries.ToDictionary(entry => entry.Node, entry => entry);
            _unitIdentity = TargetUnit;
            OverlayDigest = overlayDigest;
        }

        public string SourceIdentity { get; }

        public string UnitIdentity
        {
            get { return _unitIdentity; }
        }

        public IReadOnlyList<StaticAbsorptionOverlayEntryV1> Entries
        {
            get { return _entries; }
        }

        public Digest32 OverlayDigest { get; }

        public string OverlayDigestHex
        {
            get { return DigestHex(OverlayDigest); }
        }

        public bool IsZero
        {
            get
            {
                return _entries.All(entry =>
                    entry.DeltaAbsorptionGroup1PerM == 0.0 &&
                    entry.DeltaAbsorptionGroup2PerM == 0.0);
            }
        }

        public static ContractValidationResult<StaticAbsorptionOverlayV1> TryCreate(
            string sourceIdentity,
            IEnumerable<StaticAbsorptionOverlayEntryV1> entries)
        {
            if (string.IsNullOrWhiteSpace(sourceIdentity))
            {
                return Invalid(
                    "StaticAbsorptionOverlay.SourceIdentity.Empty",
                    "source_identity",
                    "A static absorption overlay requires an explicit source identity.");
            }

            if (entries == null)
            {
                return Invalid(
                    "StaticAbsorptionOverlay.Entries.Missing",
                    "entries",
                    "A static absorption overlay requires explicit node entries.");
            }

            StaticAbsorptionOverlayEntryV1[] records = entries.ToArray();
            if (records.Any(entry => entry == null))
            {
                return Invalid(
                    "StaticAbsorptionOverlay.Entry.Null",
                    "entries",
                    "A static absorption overlay entry may not be null.");
            }

            StaticAbsorptionOverlayEntryV1[] canonical = records
                .OrderBy(entry => entry.Node)
                .ToArray();
            for (int index = 0; index < canonical.Length; index++)
            {
                StaticAbsorptionOverlayEntryV1 entry = canonical[index];
                if (!IsCanonicalFinite(entry.DeltaAbsorptionGroup1PerM) ||
                    !IsCanonicalFinite(entry.DeltaAbsorptionGroup2PerM))
                {
                    return Invalid(
                        "StaticAbsorptionOverlay.Entry.NonFinite",
                        ContractValidation.NodePath(entry.Node, ".delta_absorption_m_inverse"),
                        "Static absorption deltas must be finite and free of signed negative zero.");
                }

                if (index > 0 && canonical[index - 1].Node == entry.Node)
                {
                    return Invalid(
                        "StaticAbsorptionOverlay.Entry.Duplicate",
                        ContractValidation.NodePath(entry.Node, ".delta_absorption_m_inverse"),
                        "A static absorption overlay may contain only one entry per node.");
                }
            }

            Digest32 digest = new Digest32(Phase5CanonicalBytesV1.HashBody(
                "CANDU-STATIC-ABSORPTION-OVERLAY-V1",
                writer =>
                {
                    Phase5CanonicalBytesV1.WriteUInt32(writer, CurrentSchemaVersion);
                    Phase5CanonicalBytesV1.WriteString(writer, sourceIdentity);
                    Phase5CanonicalBytesV1.WriteString(writer, TargetUnit);
                    Phase5CanonicalBytesV1.WriteUInt32(
                        writer,
                        checked((uint)canonical.Length));
                    foreach (StaticAbsorptionOverlayEntryV1 entry in canonical)
                    {
                        WriteNodeKey(writer, entry.Node);
                        Phase5CanonicalBytesV1.WriteDouble(
                            writer,
                            entry.DeltaAbsorptionGroup1PerM);
                        Phase5CanonicalBytesV1.WriteDouble(
                            writer,
                            entry.DeltaAbsorptionGroup2PerM);
                    }
                }));

            return ContractValidationResult<StaticAbsorptionOverlayV1>.Valid(
                new StaticAbsorptionOverlayV1(sourceIdentity, canonical, digest));
        }

        public static ContractValidationResult<StaticAbsorptionOverlayV1> TryCreateZero(
            string sourceIdentity,
            IEnumerable<NodeKey> nodes)
        {
            if (nodes == null)
            {
                return Invalid(
                    "StaticAbsorptionOverlay.Nodes.Missing",
                    "nodes",
                    "A zero static absorption overlay requires an explicit node list.");
            }

            return TryCreate(
                sourceIdentity,
                nodes.Select(node => new StaticAbsorptionOverlayEntryV1(node, 0.0, 0.0)));
        }

        public bool TryGetEntry(
            NodeKey node,
            out StaticAbsorptionOverlayEntryV1 entry)
        {
            return _byNode.TryGetValue(node, out entry!);
        }

        public double GetDeltaAbsorptionGroup1PerM(NodeKey node)
        {
            return _byNode.TryGetValue(node, out StaticAbsorptionOverlayEntryV1 entry)
                ? entry.DeltaAbsorptionGroup1PerM
                : 0.0;
        }

        public double GetDeltaAbsorptionGroup2PerM(NodeKey node)
        {
            return _byNode.TryGetValue(node, out StaticAbsorptionOverlayEntryV1 entry)
                ? entry.DeltaAbsorptionGroup2PerM
                : 0.0;
        }

        private static bool IsCanonicalFinite(double value)
        {
            return ContractValidation.IsFinite(value) &&
                   (value != 0.0 || BitConverter.DoubleToInt64Bits(value) >= 0);
        }

        private static void WriteNodeKey(System.IO.BinaryWriter writer, NodeKey node)
        {
            Phase5CanonicalBytesV1.WriteUInt32(writer, node.ChannelId.Value);
            Phase5CanonicalBytesV1.WriteUInt32(writer, node.Position.Value);
        }

        private static string DigestHex(Digest32 digest)
        {
            var builder = new StringBuilder(digest.Bytes.Count * 2 + 7);
            builder.Append("sha256:");
            foreach (byte value in digest.Bytes)
            {
                builder.Append(value.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private static ContractValidationResult<StaticAbsorptionOverlayV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<StaticAbsorptionOverlayV1>.Invalid(code, path, message);
        }
    }
}

