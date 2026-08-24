using System;
using System.Globalization;

namespace ReactorSim.Core
{
    /// <summary>
    /// Zero-based channel identity. It is not a physical ordering or flow
    /// direction; topology validation establishes its legal range.
    /// </summary>
    public readonly struct ChannelId : IEquatable<ChannelId>, IComparable<ChannelId>
    {
        public ChannelId(uint value)
        {
            Value = value;
        }

        public uint Value { get; }

        public int CompareTo(ChannelId other)
        {
            return Value.CompareTo(other.Value);
        }

        public bool Equals(ChannelId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object? obj)
        {
            return obj is ChannelId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value.ToString(CultureInfo.InvariantCulture);
        }

        public static bool operator ==(ChannelId left, ChannelId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ChannelId left, ChannelId right)
        {
            return !left.Equals(right);
        }

        public static bool operator <(ChannelId left, ChannelId right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator <=(ChannelId left, ChannelId right)
        {
            return left.CompareTo(right) <= 0;
        }

        public static bool operator >(ChannelId left, ChannelId right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator >=(ChannelId left, ChannelId right)
        {
            return left.CompareTo(right) >= 0;
        }
    }

    /// <summary>
    /// Zero-based physical bundle position within one channel.
    /// </summary>
    public readonly struct BundlePosition : IEquatable<BundlePosition>, IComparable<BundlePosition>
    {
        public BundlePosition(uint value)
        {
            Value = value;
        }

        public uint Value { get; }

        public int CompareTo(BundlePosition other)
        {
            return Value.CompareTo(other.Value);
        }

        public bool Equals(BundlePosition other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object? obj)
        {
            return obj is BundlePosition other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value.ToString(CultureInfo.InvariantCulture);
        }

        public static bool operator ==(BundlePosition left, BundlePosition right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(BundlePosition left, BundlePosition right)
        {
            return !left.Equals(right);
        }

        public static bool operator <(BundlePosition left, BundlePosition right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator <=(BundlePosition left, BundlePosition right)
        {
            return left.CompareTo(right) <= 0;
        }

        public static bool operator >(BundlePosition left, BundlePosition right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator >=(BundlePosition left, BundlePosition right)
        {
            return left.CompareTo(right) >= 0;
        }
    }

    /// <summary>
    /// Explicit spatial node identity. It is the only legal node key for the
    /// Phase 3 inventory and later approved spatial work.
    /// </summary>
    public readonly struct NodeKey : IEquatable<NodeKey>, IComparable<NodeKey>
    {
        public NodeKey(ChannelId channelId, BundlePosition position)
        {
            ChannelId = channelId;
            Position = position;
        }

        public ChannelId ChannelId { get; }

        public BundlePosition Position { get; }

        public int CompareTo(NodeKey other)
        {
            int channelComparison = ChannelId.CompareTo(other.ChannelId);
            return channelComparison != 0 ? channelComparison : Position.CompareTo(other.Position);
        }

        public bool Equals(NodeKey other)
        {
            return ChannelId == other.ChannelId && Position == other.Position;
        }

        public override bool Equals(object? obj)
        {
            return obj is NodeKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (ChannelId.GetHashCode() * 397) ^ Position.GetHashCode();
            }
        }

        public override string ToString()
        {
            return "(" + ChannelId + "," + Position + ")";
        }

        public static bool operator ==(NodeKey left, NodeKey right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(NodeKey left, NodeKey right)
        {
            return !left.Equals(right);
        }

        public static bool operator <(NodeKey left, NodeKey right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator <=(NodeKey left, NodeKey right)
        {
            return left.CompareTo(right) <= 0;
        }

        public static bool operator >(NodeKey left, NodeKey right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator >=(NodeKey left, NodeKey right)
        {
            return left.CompareTo(right) >= 0;
        }
    }

    /// <summary>
    /// Opaque 128-bit identity stored and compared in canonical UUID byte
    /// order. Runtime code never generates one implicitly.
    /// </summary>
    public readonly struct StableId : IEquatable<StableId>, IComparable<StableId>
    {
        private readonly ulong _mostSignificantBits;
        private readonly ulong _leastSignificantBits;

        private StableId(ulong mostSignificantBits, ulong leastSignificantBits)
        {
            _mostSignificantBits = mostSignificantBits;
            _leastSignificantBits = leastSignificantBits;
        }

        public static StableId Empty
        {
            get { return new StableId(0, 0); }
        }

        public bool IsEmpty
        {
            get { return _mostSignificantBits == 0 && _leastSignificantBits == 0; }
        }

        public static StableId FromGuid(Guid value)
        {
            string compact = value.ToString("N");
            return new StableId(ParseHexUInt64(compact, 0), ParseHexUInt64(compact, 16));
        }

        public static StableId Parse(string value)
        {
            StableId parsed;
            if (!TryParse(value, out parsed))
            {
                throw new FormatException("StableId must be a canonical UUID in D format.");
            }

            return parsed;
        }

        public static bool TryParse(string? value, out StableId result)
        {
            result = Empty;
            if (value == null)
            {
                return false;
            }

            Guid guid;
            if (!Guid.TryParseExact(value, "D", out guid))
            {
                return false;
            }

            StableId parsed = FromGuid(guid);
            if (!string.Equals(parsed.ToString(), value, StringComparison.Ordinal))
            {
                return false;
            }

            result = parsed;
            return true;
        }

        public byte[] ToCanonicalBytes()
        {
            var result = new byte[16];
            WriteUInt64BigEndian(_mostSignificantBits, result, 0);
            WriteUInt64BigEndian(_leastSignificantBits, result, 8);
            return result;
        }

        public int CompareTo(StableId other)
        {
            int highComparison = _mostSignificantBits.CompareTo(other._mostSignificantBits);
            return highComparison != 0 ? highComparison : _leastSignificantBits.CompareTo(other._leastSignificantBits);
        }

        public bool Equals(StableId other)
        {
            return _mostSignificantBits == other._mostSignificantBits &&
                   _leastSignificantBits == other._leastSignificantBits;
        }

        public override bool Equals(object? obj)
        {
            return obj is StableId other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (int)(_mostSignificantBits ^ (_mostSignificantBits >> 32) ^
                              _leastSignificantBits ^ (_leastSignificantBits >> 32));
            }
        }

        public override string ToString()
        {
            string compact = _mostSignificantBits.ToString("x16", CultureInfo.InvariantCulture) +
                             _leastSignificantBits.ToString("x16", CultureInfo.InvariantCulture);
            return compact.Substring(0, 8) + "-" + compact.Substring(8, 4) + "-" +
                   compact.Substring(12, 4) + "-" + compact.Substring(16, 4) + "-" + compact.Substring(20, 12);
        }

        public static bool operator ==(StableId left, StableId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(StableId left, StableId right)
        {
            return !left.Equals(right);
        }

        public static bool operator <(StableId left, StableId right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator <=(StableId left, StableId right)
        {
            return left.CompareTo(right) <= 0;
        }

        public static bool operator >(StableId left, StableId right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator >=(StableId left, StableId right)
        {
            return left.CompareTo(right) >= 0;
        }

        private static ulong ParseHexUInt64(string value, int offset)
        {
            ulong result = 0;
            for (int i = offset; i < offset + 16; i++)
            {
                char character = value[i];
                int digit;
                if (character >= '0' && character <= '9')
                {
                    digit = character - '0';
                }
                else if (character >= 'a' && character <= 'f')
                {
                    digit = character - 'a' + 10;
                }
                else
                {
                    digit = character - 'A' + 10;
                }

                result = (result << 4) | (uint)digit;
            }

            return result;
        }

        private static void WriteUInt64BigEndian(ulong value, byte[] destination, int offset)
        {
            for (int i = 7; i >= 0; i--)
            {
                destination[offset + (7 - i)] = (byte)(value >> (i * 8));
            }
        }
    }

    /// <summary>
    /// Opaque material-library key. It is deliberately not an array index.
    /// </summary>
    public readonly struct MaterialVariantId : IEquatable<MaterialVariantId>, IComparable<MaterialVariantId>
    {
        public MaterialVariantId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("MaterialVariantId must not be empty.", nameof(value));
            }

            Value = value;
        }

        public string Value { get; }

        public int CompareTo(MaterialVariantId other)
        {
            return string.CompareOrdinal(Value, other.Value);
        }

        public bool Equals(MaterialVariantId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj)
        {
            return obj is MaterialVariantId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        }

        public override string ToString()
        {
            return Value ?? string.Empty;
        }

        public static bool operator ==(MaterialVariantId left, MaterialVariantId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(MaterialVariantId left, MaterialVariantId right)
        {
            return !left.Equals(right);
        }

        public static bool operator <(MaterialVariantId left, MaterialVariantId right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator <=(MaterialVariantId left, MaterialVariantId right)
        {
            return left.CompareTo(right) <= 0;
        }

        public static bool operator >(MaterialVariantId left, MaterialVariantId right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator >=(MaterialVariantId left, MaterialVariantId right)
        {
            return left.CompareTo(right) >= 0;
        }
    }

    public enum FlowDirection : byte
    {
        EndAtoEndB = 0,
        EndBtoEndA = 1
    }

    public enum NeighborDirection : byte
    {
        North = 0,
        East = 1,
        South = 2,
        West = 3,
        TowardEndA = 4,
        TowardEndB = 5
    }

    public enum TopologyFace : byte
    {
        North = 0,
        East = 1,
        South = 2,
        West = 3,
        EndA = 4,
        EndB = 5
    }

    public enum BoundaryClassification : byte
    {
        Reflective = 0,
        Vacuum = 1,
        SpecifiedLeakage = 2
    }
}
