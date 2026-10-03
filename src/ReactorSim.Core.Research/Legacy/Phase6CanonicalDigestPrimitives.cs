using System;
using System.Collections.Generic;

namespace ReactorSim.Core
{
    /// <summary>
    /// Internal canonical-byte primitive for Phase 6 versioned mapping
    /// collections whose body layout is deliberately identical. Callers own
    /// ordering and entry validation; this helper owns only the shared field
    /// sequence and optional trailing collection digest.
    /// </summary>
    internal static class Phase6CanonicalDigestPrimitives
    {
        internal static Digest32 ComputeVersionedCollectionDigest(
            string schemaId,
            uint schemaVersion,
            StableId collectionId,
            string collectionVersion,
            IReadOnlyList<byte[]> canonicalEntryBytes)
        {
            return new Digest32(
                Phase5CanonicalBytesV1.Sha256(
                    BuildVersionedCollectionBytes(
                        schemaId,
                        schemaVersion,
                        collectionId,
                        collectionVersion,
                        canonicalEntryBytes,
                        null)));
        }

        internal static byte[] BuildVersionedCollectionBytes(
            string schemaId,
            uint schemaVersion,
            StableId collectionId,
            string collectionVersion,
            IReadOnlyList<byte[]> canonicalEntryBytes,
            Digest32? collectionDigest)
        {
            if (schemaId == null)
            {
                throw new ArgumentNullException(nameof(schemaId));
            }

            if (collectionVersion == null)
            {
                throw new ArgumentNullException(nameof(collectionVersion));
            }

            if (canonicalEntryBytes == null)
            {
                throw new ArgumentNullException(nameof(canonicalEntryBytes));
            }

            return Phase5CanonicalBytesV1.Build(writer =>
            {
                Phase5CanonicalBytesV1.WriteAscii(writer, schemaId);
                writer.Write((byte)0);
                Phase5CanonicalBytesV1.WriteUInt32(writer, schemaVersion);
                Phase5CanonicalBytesV1.WriteStableId(writer, collectionId);
                Phase5CanonicalBytesV1.WriteString(writer, collectionVersion);
                Phase5CanonicalBytesV1.WriteUInt32(
                    writer,
                    checked((uint)canonicalEntryBytes.Count));
                foreach (byte[] entryBytes in canonicalEntryBytes)
                {
                    Phase5CanonicalBytesV1.WriteBytes(writer, entryBytes);
                }

                if (collectionDigest != null)
                {
                    Phase5CanonicalBytesV1.WriteDigest(writer, collectionDigest);
                }
            });
        }
    }
}
