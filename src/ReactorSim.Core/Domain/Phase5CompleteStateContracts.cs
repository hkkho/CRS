using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ReactorSim.Core
{
    /// <summary>
    /// Material/table-keyed I-135/Xe-135 data. The values are supplied by a
    /// validated data pack; this type does not select nuclear constants.
    /// </summary>
    public sealed class NuclideDataV1
    {
        public const uint CurrentSchemaVersion = 1;

        private NuclideDataV1(
            MaterialVariantId materialVariantId,
            string dataId,
            Digest32 dataDigest,
            double gammaI,
            double gammaXe,
            double lambdaI,
            double lambdaXe,
            double sigmaXeGroup1M2,
            double sigmaXeGroup2M2)
        {
            MaterialVariantId = materialVariantId;
            DataId = dataId;
            DataDigest = dataDigest;
            GammaI = gammaI;
            GammaXe = gammaXe;
            LambdaI = lambdaI;
            LambdaXe = lambdaXe;
            SigmaXeGroup1M2 = sigmaXeGroup1M2;
            SigmaXeGroup2M2 = sigmaXeGroup2M2;
        }

        public MaterialVariantId MaterialVariantId { get; }

        public string DataId { get; }

        public Digest32 DataDigest { get; }

        public double GammaI { get; }

        public double GammaXe { get; }

        public double LambdaI { get; }

        public double LambdaXe { get; }

        public double SigmaXeGroup1M2 { get; }

        public double SigmaXeGroup2M2 { get; }

        public static ContractValidationResult<NuclideDataV1> TryCreate(
            MaterialVariantId materialVariantId,
            string dataId,
            Digest32 dataDigest,
            double gammaI,
            double gammaXe,
            double lambdaI,
            double lambdaXe,
            double sigmaXeGroup1M2,
            double sigmaXeGroup2M2)
        {
            if (string.IsNullOrWhiteSpace(materialVariantId.Value))
            {
                return Invalid(
                    "NuclideData.MaterialVariant.Empty",
                    "material_variant_id",
                    "Nuclide data requires an explicit material variant identity.");
            }

            if (string.IsNullOrWhiteSpace(dataId))
            {
                return Invalid(
                    "NuclideData.Id.Empty",
                    "data_id",
                    "Nuclide data requires an explicit table identity.");
            }

            if (dataDigest == null)
            {
                return Invalid(
                    "NuclideData.Digest.Missing",
                    "data_digest",
                    "Nuclide data requires an explicit 32-byte digest.");
            }

            double[] values =
            {
                gammaI,
                gammaXe,
                lambdaI,
                lambdaXe,
                sigmaXeGroup1M2,
                sigmaXeGroup2M2
            };
            if (values.Any(value => !PowerHistoryRecordV1.IsCanonicalFinite(value)))
            {
                return Invalid(
                    "NuclideData.NonFinite",
                    "data",
                    "Every nuclide-data value must be finite and canonical.");
            }

            if (!PowerHistoryRecordV1.IsCanonicalNonnegative(gammaI) ||
                !PowerHistoryRecordV1.IsCanonicalNonnegative(gammaXe) ||
                !PowerHistoryRecordV1.IsCanonicalNonnegative(sigmaXeGroup1M2) ||
                !PowerHistoryRecordV1.IsCanonicalNonnegative(sigmaXeGroup2M2))
            {
                return Invalid(
                    "NuclideData.Negative",
                    "data",
                    "Yields and microscopic absorption cross sections must be finite, canonical, and nonnegative.");
            }

            if (!PowerHistoryRecordV1.IsCanonicalPositiveFinite(lambdaI) ||
                !PowerHistoryRecordV1.IsCanonicalPositiveFinite(lambdaXe))
            {
                return Invalid(
                    "NuclideData.Decay.Invalid",
                    "data.decay_constant_s_inv",
                    "I-135 and Xe-135 decay constants must be strictly positive SI s^-1 values.");
            }

            return ContractValidationResult<NuclideDataV1>.Valid(
                new NuclideDataV1(
                    materialVariantId,
                    dataId,
                    dataDigest,
                    gammaI,
                    gammaXe,
                    lambdaI,
                    lambdaXe,
                    sigmaXeGroup1M2,
                    sigmaXeGroup2M2));
        }

        private static ContractValidationResult<NuclideDataV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<NuclideDataV1>.Invalid(code, path, message);
        }
    }

    /// <summary>
    /// One accepted bundle-power history value. It is append-only and carries
    /// the exact lifecycle versions that accepted the spatial snapshot.
    /// </summary>
    public sealed class PowerHistoryRecordV1
    {
        private PowerHistoryRecordV1(
            double snapshotTimeSeconds,
            double powerWatts,
            ulong coreStateVersion,
            ulong spatialStateVersion,
            ulong powerSnapshotVersion)
        {
            SnapshotTimeSeconds = snapshotTimeSeconds;
            PowerWatts = powerWatts;
            CoreStateVersion = coreStateVersion;
            SpatialStateVersion = spatialStateVersion;
            PowerSnapshotVersion = powerSnapshotVersion;
        }

        public double SnapshotTimeSeconds { get; }

        public double PowerWatts { get; }

        public ulong CoreStateVersion { get; }

        public ulong SpatialStateVersion { get; }

        public ulong PowerSnapshotVersion { get; }

        public static ContractValidationResult<PowerHistoryRecordV1> TryCreate(
            double snapshotTimeSeconds,
            double powerWatts,
            ulong coreStateVersion,
            ulong spatialStateVersion,
            ulong powerSnapshotVersion)
        {
            if (!IsCanonicalTime(snapshotTimeSeconds))
            {
                return Invalid(
                    "PowerHistory.Time.Invalid",
                    "snapshot_time_s",
                    "Power-history time must be finite, nonnegative, and not signed negative zero.");
            }

            if (!IsCanonicalNonnegative(powerWatts))
            {
                return Invalid(
                    "PowerHistory.Power.Invalid",
                    "power_w",
                    "Power history must contain finite nonnegative SI watts.");
            }

            return ContractValidationResult<PowerHistoryRecordV1>.Valid(
                new PowerHistoryRecordV1(
                    snapshotTimeSeconds,
                    powerWatts,
                    coreStateVersion,
                    spatialStateVersion,
                    powerSnapshotVersion));
        }

        public byte[] ToCanonicalBytes()
        {
            return Phase5CanonicalBytesV1.Build(writer =>
            {
                Phase5CanonicalBytesV1.WriteAscii(writer, "CANDU-POWER-HISTORY-RECORD-V1");
                writer.Write((byte)0);
                Phase5CanonicalBytesV1.WriteUInt32(writer, 1);
                Phase5CanonicalBytesV1.WriteDouble(writer, SnapshotTimeSeconds);
                Phase5CanonicalBytesV1.WriteDouble(writer, PowerWatts);
                Phase5CanonicalBytesV1.WriteUInt64(writer, CoreStateVersion);
                Phase5CanonicalBytesV1.WriteUInt64(writer, SpatialStateVersion);
                Phase5CanonicalBytesV1.WriteUInt64(writer, PowerSnapshotVersion);
            });
        }

        internal static bool IsCanonicalTime(double value)
        {
            return IsCanonicalNonnegative(value);
        }

        internal static bool IsCanonicalNonnegative(double value)
        {
            return IsCanonicalFinite(value) && value >= 0;
        }

        internal static bool IsCanonicalFinite(double value)
        {
            return ContractValidation.IsFinite(value) &&
                   BitConverter.DoubleToInt64Bits(value) != long.MinValue;
        }

        internal static bool IsCanonicalPositiveFinite(double value)
        {
            return IsCanonicalFinite(value) && value > 0;
        }

        private static ContractValidationResult<PowerHistoryRecordV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<PowerHistoryRecordV1>.Invalid(code, path, message);
        }
    }

    /// <summary>
    /// Explicit applicability for the current accepted bundle power. A
    /// missing power is not represented by a numeric zero.
    /// </summary>
    public sealed class OptionalPowerWattsV1 : IEquatable<OptionalPowerWattsV1>
    {
        private OptionalPowerWattsV1(bool isApplicable, double value)
        {
            IsApplicable = isApplicable;
            Value = value;
        }

        public static OptionalPowerWattsV1 NotApplicable
        {
            get { return new OptionalPowerWattsV1(false, 0.0); }
        }

        public bool IsApplicable { get; }

        public double Value { get; }

        public static ContractValidationResult<OptionalPowerWattsV1> TryApplicable(double value)
        {
            if (!PowerHistoryRecordV1.IsCanonicalNonnegative(value))
            {
                return ContractValidationResult<OptionalPowerWattsV1>.Invalid(
                    "OptionalPowerWatts.Power.Invalid",
                    "power_w",
                    "An applicable current power must be finite and nonnegative SI watts.");
            }

            return ContractValidationResult<OptionalPowerWattsV1>.Valid(
                new OptionalPowerWattsV1(true, value));
        }

        public bool Equals(OptionalPowerWattsV1? other)
        {
            return other != null && IsApplicable == other.IsApplicable &&
                   (!IsApplicable || Value == other.Value);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as OptionalPowerWattsV1);
        }

        public override int GetHashCode()
        {
            return IsApplicable ? Value.GetHashCode() : 0;
        }
    }

    /// <summary>
    /// The complete coefficient identity/bracket retained on a bundle state.
    /// Coefficient values themselves remain owned by the validated table.
    /// </summary>
    public sealed class BundleCoefficientBindingV1
    {
        private BundleCoefficientBindingV1(
            StableId tableId,
            int lowerIndex,
            int upperIndex,
            double interpolationFraction,
            Digest32 tableDigest)
        {
            TableId = tableId;
            LowerIndex = lowerIndex;
            UpperIndex = upperIndex;
            InterpolationFraction = interpolationFraction;
            TableDigest = tableDigest;
        }

        public StableId TableId { get; }

        public int LowerIndex { get; }

        public int UpperIndex { get; }

        public double InterpolationFraction { get; }

        public Digest32 TableDigest { get; }

        public static ContractValidationResult<BundleCoefficientBindingV1> TryCreate(
            StableId tableId,
            int lowerIndex,
            int upperIndex,
            double interpolationFraction,
            Digest32 tableDigest)
        {
            if (tableId.IsEmpty || tableDigest == null)
            {
                return Invalid(
                    "BundleCoefficientBinding.Identity.Missing",
                    "coefficient_binding",
                    "A coefficient binding requires a table identity and digest.");
            }

            if (lowerIndex < 0 || upperIndex < lowerIndex ||
                !ContractValidation.IsFinite(interpolationFraction) ||
                interpolationFraction < 0 || interpolationFraction > 1)
            {
                return Invalid(
                    "BundleCoefficientBinding.Bracket.Invalid",
                    "coefficient_binding.bracket",
                    "A coefficient bracket must be ordered and have a fraction in [0,1].");
            }

            return ContractValidationResult<BundleCoefficientBindingV1>.Valid(
                new BundleCoefficientBindingV1(
                    tableId,
                    lowerIndex,
                    upperIndex,
                    interpolationFraction,
                    tableDigest));
        }

        public byte[] ToCanonicalBytes()
        {
            return Phase5CanonicalBytesV1.Build(writer =>
            {
                Phase5CanonicalBytesV1.WriteStableId(writer, TableId);
                Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)LowerIndex));
                Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)UpperIndex));
                Phase5CanonicalBytesV1.WriteDouble(writer, InterpolationFraction);
                Phase5CanonicalBytesV1.WriteDigest(writer, TableDigest);
            });
        }

        private static ContractValidationResult<BundleCoefficientBindingV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<BundleCoefficientBindingV1>.Invalid(code, path, message);
        }
    }

    /// <summary>
    /// One canonical accepted I-135/Xe-135 transition record.
    /// </summary>
    public sealed class NuclideTransitionRecordV1
    {
        public const uint CurrentSchemaVersion = 1;

        private NuclideTransitionRecordV1(
            StableId ownerEventId,
            StableId recordId,
            ulong recordSequence,
            EventRankV1 eventRank,
            double eventTimeSeconds,
            double deltaTimeSeconds,
            StableId bundleId,
            ulong coreStateVersionBefore,
            OptionalUInt64 coreStateVersionAfterOrNA,
            ulong nuclideStateVersionBefore,
            ulong nuclideStateVersionAfter,
            double nodeVolumeM3,
            double iBefore,
            double iAfter,
            double xeBefore,
            double xeAfter,
            double iDensityBefore,
            double iDensityAfter,
            double xeDensityBefore,
            double xeDensityAfter,
            double iDirectProductionAtomsPerSecond,
            double iDecayLossAtomsPerSecond,
            double xeDirectProductionAtomsPerSecond,
            double xeFromI135DecayAtomsPerSecond,
            double xeDecayLossAtomsPerSecond,
            double xeAbsorptionLossAtomsPerSecond,
            string nuclideDataId,
            Digest32 nuclideDataDigest,
            Digest32 stateBindingDigest,
            Digest32 recordDigest)
        {
            OwnerEventId = ownerEventId;
            RecordId = recordId;
            RecordSequence = recordSequence;
            EventRank = eventRank;
            EventTimeSeconds = eventTimeSeconds;
            DeltaTimeSeconds = deltaTimeSeconds;
            BundleId = bundleId;
            CoreStateVersionBefore = coreStateVersionBefore;
            CoreStateVersionAfterOrNA = coreStateVersionAfterOrNA;
            NuclideStateVersionBefore = nuclideStateVersionBefore;
            NuclideStateVersionAfter = nuclideStateVersionAfter;
            NodeVolumeM3 = nodeVolumeM3;
            I135AtomInventoryBefore = iBefore;
            I135AtomInventoryAfter = iAfter;
            Xe135AtomInventoryBefore = xeBefore;
            Xe135AtomInventoryAfter = xeAfter;
            I135NumberDensityBefore = iDensityBefore;
            I135NumberDensityAfter = iDensityAfter;
            Xe135NumberDensityBefore = xeDensityBefore;
            Xe135NumberDensityAfter = xeDensityAfter;
            I135DirectProductionAtomsPerSecond = iDirectProductionAtomsPerSecond;
            I135DecayLossAtomsPerSecond = iDecayLossAtomsPerSecond;
            Xe135DirectProductionAtomsPerSecond = xeDirectProductionAtomsPerSecond;
            Xe135FromI135DecayAtomsPerSecond = xeFromI135DecayAtomsPerSecond;
            Xe135DecayLossAtomsPerSecond = xeDecayLossAtomsPerSecond;
            Xe135AbsorptionLossAtomsPerSecond = xeAbsorptionLossAtomsPerSecond;
            NuclideDataId = nuclideDataId;
            NuclideDataDigest = nuclideDataDigest;
            StateBindingDigest = stateBindingDigest;
            RecordDigest = recordDigest;
        }

        public StableId OwnerEventId { get; }

        public StableId RecordId { get; }

        public ulong RecordSequence { get; }

        public EventRankV1 EventRank { get; }

        public double EventTimeSeconds { get; }

        public double DeltaTimeSeconds { get; }

        public StableId BundleId { get; }

        public ulong CoreStateVersionBefore { get; }

        public OptionalUInt64 CoreStateVersionAfterOrNA { get; }

        public ulong NuclideStateVersionBefore { get; }

        public ulong NuclideStateVersionAfter { get; }

        public double NodeVolumeM3 { get; }

        public double I135AtomInventoryBefore { get; }

        public double I135AtomInventoryAfter { get; }

        public double Xe135AtomInventoryBefore { get; }

        public double Xe135AtomInventoryAfter { get; }

        public double I135NumberDensityBefore { get; }

        public double I135NumberDensityAfter { get; }

        public double Xe135NumberDensityBefore { get; }

        public double Xe135NumberDensityAfter { get; }

        public double I135DirectProductionAtomsPerSecond { get; }

        public double I135DecayLossAtomsPerSecond { get; }

        public double Xe135DirectProductionAtomsPerSecond { get; }

        public double Xe135FromI135DecayAtomsPerSecond { get; }

        public double Xe135DecayLossAtomsPerSecond { get; }

        public double Xe135AbsorptionLossAtomsPerSecond { get; }

        public string NuclideDataId { get; }

        public Digest32 NuclideDataDigest { get; }

        public Digest32 StateBindingDigest { get; }

        public Digest32 RecordDigest { get; }

        public static ContractValidationResult<NuclideTransitionRecordV1> TryCreate(
            StableId ownerEventId,
            ulong recordSequence,
            EventRankV1 eventRank,
            double eventTimeSeconds,
            double deltaTimeSeconds,
            StableId bundleId,
            ulong coreStateVersionBefore,
            OptionalUInt64 coreStateVersionAfterOrNA,
            ulong nuclideStateVersionBefore,
            ulong nuclideStateVersionAfter,
            double nodeVolumeM3,
            double iBefore,
            double iAfter,
            double xeBefore,
            double xeAfter,
            double iDensityBefore,
            double iDensityAfter,
            double xeDensityBefore,
            double xeDensityAfter,
            double iDirectProductionAtomsPerSecond,
            double iDecayLossAtomsPerSecond,
            double xeDirectProductionAtomsPerSecond,
            double xeFromI135DecayAtomsPerSecond,
            double xeDecayLossAtomsPerSecond,
            double xeAbsorptionLossAtomsPerSecond,
            string nuclideDataId,
            Digest32 nuclideDataDigest,
            Digest32 stateBindingDigest)
        {
            if (ownerEventId.IsEmpty || bundleId.IsEmpty)
            {
                return Invalid(
                    "NuclideTransition.Identity.Empty",
                    "identity",
                    "An accepted nuclide transition requires owner and bundle identities.");
            }

            if (!Enum.IsDefined(typeof(EventRankV1), eventRank))
            {
                return Invalid(
                    "NuclideTransition.EventRank.Invalid",
                    "event_rank",
                    "The transition event rank must be a defined P2-T05 rank.");
            }

            if (!PowerHistoryRecordV1.IsCanonicalTime(eventTimeSeconds) ||
                !PowerHistoryRecordV1.IsCanonicalPositiveFinite(deltaTimeSeconds))
            {
                return Invalid(
                    "NuclideTransition.Time.Invalid",
                    "time",
                    "An integration transition requires canonical time and a positive duration.");
            }

            if (nuclideStateVersionBefore == ulong.MaxValue ||
                nuclideStateVersionAfter != nuclideStateVersionBefore + 1)
            {
                return Invalid(
                    "NuclideTransition.Version.Invalid",
                    "nuclide_state_version",
                    "An accepted transition must increment the bundle nuclide version exactly once.");
            }

            if (coreStateVersionAfterOrNA == null)
            {
                return Invalid(
                    "NuclideTransition.CoreVersion.Missing",
                    "core_state_version_after",
                    "Core-state applicability must be explicit.");
            }

            if (!coreStateVersionAfterOrNA.IsApplicable ||
                coreStateVersionAfterOrNA.Value != coreStateVersionBefore)
            {
                return Invalid(
                    "NuclideTransition.CoreVersion.Invalid",
                    "core_state_version_after",
                    "An accepted I/Xe transition must bind an applicable unchanged core-state version.");
            }

            if (nuclideDataDigest == null || stateBindingDigest == null || string.IsNullOrWhiteSpace(nuclideDataId))
            {
                return Invalid(
                    "NuclideTransition.Provenance.Missing",
                    "provenance",
                    "An accepted transition requires data identity, data digest, and state-binding digest.");
            }

            double[] values =
            {
                nodeVolumeM3,
                iBefore,
                iAfter,
                xeBefore,
                xeAfter,
                iDensityBefore,
                iDensityAfter,
                xeDensityBefore,
                xeDensityAfter,
                iDirectProductionAtomsPerSecond,
                iDecayLossAtomsPerSecond,
                xeDirectProductionAtomsPerSecond,
                xeFromI135DecayAtomsPerSecond,
                xeDecayLossAtomsPerSecond,
                xeAbsorptionLossAtomsPerSecond
            };
            if (values.Any(value => !PowerHistoryRecordV1.IsCanonicalFinite(value)))
            {
                return Invalid(
                    "NuclideTransition.NonFinite",
                    "transition",
                    "Every nuclide transition value must be finite and canonical.");
            }

            if (!PowerHistoryRecordV1.IsCanonicalPositiveFinite(nodeVolumeM3) ||
                !PowerHistoryRecordV1.IsCanonicalNonnegative(iBefore) ||
                !PowerHistoryRecordV1.IsCanonicalNonnegative(iAfter) ||
                !PowerHistoryRecordV1.IsCanonicalNonnegative(xeBefore) ||
                !PowerHistoryRecordV1.IsCanonicalNonnegative(xeAfter) ||
                !PowerHistoryRecordV1.IsCanonicalNonnegative(iDensityBefore) ||
                !PowerHistoryRecordV1.IsCanonicalNonnegative(iDensityAfter) ||
                !PowerHistoryRecordV1.IsCanonicalNonnegative(xeDensityBefore) ||
                !PowerHistoryRecordV1.IsCanonicalNonnegative(xeDensityAfter))
            {
                return Invalid(
                    "NuclideTransition.Inventory.Invalid",
                    "transition.inventory",
                    "Atom inventories and derived densities must be finite, canonical, and nonnegative; node volume must be finite, canonical, and positive.");
            }

            if (iDensityBefore != iBefore / nodeVolumeM3 || iDensityAfter != iAfter / nodeVolumeM3 ||
                xeDensityBefore != xeBefore / nodeVolumeM3 || xeDensityAfter != xeAfter / nodeVolumeM3)
            {
                return Invalid(
                    "NuclideTransition.Density.Mismatch",
                    "transition.density",
                    "Number densities must equal the exact atom-inventory divisions.");
            }

            if (iDirectProductionAtomsPerSecond < 0 || iDecayLossAtomsPerSecond > 0 ||
                xeDirectProductionAtomsPerSecond < 0 || xeFromI135DecayAtomsPerSecond < 0 ||
                xeDecayLossAtomsPerSecond > 0 || xeAbsorptionLossAtomsPerSecond > 0)
            {
                return Invalid(
                    "NuclideTransition.Term.Sign.Invalid",
                    "transition.terms",
                    "Production/source terms must be nonnegative and loss terms must be nonpositive.");
            }

            double expectedIAfter = iBefore +
                (deltaTimeSeconds * (iDirectProductionAtomsPerSecond + iDecayLossAtomsPerSecond));
            double expectedXeAfter = xeBefore +
                (deltaTimeSeconds * (
                    xeDirectProductionAtomsPerSecond +
                    xeFromI135DecayAtomsPerSecond +
                    xeDecayLossAtomsPerSecond +
                    xeAbsorptionLossAtomsPerSecond));
            if (xeFromI135DecayAtomsPerSecond != -iDecayLossAtomsPerSecond ||
                iAfter != expectedIAfter || xeAfter != expectedXeAfter)
            {
                return Invalid(
                    "NuclideTransition.Equation.Mismatch",
                    "transition.inventory",
                    "Transition inventories and source terms must satisfy the approved explicit left-endpoint Euler equations exactly.");
            }

            StableId recordId = Phase5CanonicalBytesV1.DeriveUuidV8(
                "CANDU-NUCLIDE-TRANSITION-ID-V1",
                writer =>
                {
                    Phase5CanonicalBytesV1.WriteStableId(writer, ownerEventId);
                    Phase5CanonicalBytesV1.WriteStableId(writer, bundleId);
                    Phase5CanonicalBytesV1.WriteUInt64(writer, recordSequence);
                });

            byte[] recordBody = BuildBody(
                ownerEventId,
                recordId,
                recordSequence,
                eventRank,
                eventTimeSeconds,
                deltaTimeSeconds,
                bundleId,
                coreStateVersionBefore,
                coreStateVersionAfterOrNA,
                nuclideStateVersionBefore,
                nuclideStateVersionAfter,
                nodeVolumeM3,
                iBefore,
                iAfter,
                xeBefore,
                xeAfter,
                iDensityBefore,
                iDensityAfter,
                xeDensityBefore,
                xeDensityAfter,
                iDirectProductionAtomsPerSecond,
                iDecayLossAtomsPerSecond,
                xeDirectProductionAtomsPerSecond,
                xeFromI135DecayAtomsPerSecond,
                xeDecayLossAtomsPerSecond,
                xeAbsorptionLossAtomsPerSecond,
                nuclideDataId,
                nuclideDataDigest,
                stateBindingDigest,
                false);
            Digest32 recordDigest = new Digest32(
                Phase5CanonicalBytesV1.HashBodyBytes("CANDU-NUCLIDE-TRANSITION-V1", recordBody));

            return ContractValidationResult<NuclideTransitionRecordV1>.Valid(
                new NuclideTransitionRecordV1(
                    ownerEventId,
                    recordId,
                    recordSequence,
                    eventRank,
                    eventTimeSeconds,
                    deltaTimeSeconds,
                    bundleId,
                    coreStateVersionBefore,
                    coreStateVersionAfterOrNA,
                    nuclideStateVersionBefore,
                    nuclideStateVersionAfter,
                    nodeVolumeM3,
                    iBefore,
                    iAfter,
                    xeBefore,
                    xeAfter,
                    iDensityBefore,
                    iDensityAfter,
                    xeDensityBefore,
                    xeDensityAfter,
                    iDirectProductionAtomsPerSecond,
                    iDecayLossAtomsPerSecond,
                    xeDirectProductionAtomsPerSecond,
                    xeFromI135DecayAtomsPerSecond,
                    xeDecayLossAtomsPerSecond,
                    xeAbsorptionLossAtomsPerSecond,
                    nuclideDataId,
                    nuclideDataDigest,
                    stateBindingDigest,
                    recordDigest));
        }

        public byte[] ToCanonicalBytes()
        {
            return BuildBody(
                OwnerEventId,
                RecordId,
                RecordSequence,
                EventRank,
                EventTimeSeconds,
                DeltaTimeSeconds,
                BundleId,
                CoreStateVersionBefore,
                CoreStateVersionAfterOrNA,
                NuclideStateVersionBefore,
                NuclideStateVersionAfter,
                NodeVolumeM3,
                I135AtomInventoryBefore,
                I135AtomInventoryAfter,
                Xe135AtomInventoryBefore,
                Xe135AtomInventoryAfter,
                I135NumberDensityBefore,
                I135NumberDensityAfter,
                Xe135NumberDensityBefore,
                Xe135NumberDensityAfter,
                I135DirectProductionAtomsPerSecond,
                I135DecayLossAtomsPerSecond,
                Xe135DirectProductionAtomsPerSecond,
                Xe135FromI135DecayAtomsPerSecond,
                Xe135DecayLossAtomsPerSecond,
                Xe135AbsorptionLossAtomsPerSecond,
                NuclideDataId,
                NuclideDataDigest,
                StateBindingDigest,
                true);
        }

        private static byte[] BuildBody(
            StableId ownerEventId,
            StableId recordId,
            ulong recordSequence,
            EventRankV1 eventRank,
            double eventTimeSeconds,
            double deltaTimeSeconds,
            StableId bundleId,
            ulong coreStateVersionBefore,
            OptionalUInt64 coreStateVersionAfterOrNA,
            ulong nuclideStateVersionBefore,
            ulong nuclideStateVersionAfter,
            double nodeVolumeM3,
            double iBefore,
            double iAfter,
            double xeBefore,
            double xeAfter,
            double iDensityBefore,
            double iDensityAfter,
            double xeDensityBefore,
            double xeDensityAfter,
            double iDirectProductionAtomsPerSecond,
            double iDecayLossAtomsPerSecond,
            double xeDirectProductionAtomsPerSecond,
            double xeFromI135DecayAtomsPerSecond,
            double xeDecayLossAtomsPerSecond,
            double xeAbsorptionLossAtomsPerSecond,
            string nuclideDataId,
            Digest32 nuclideDataDigest,
            Digest32 stateBindingDigest,
            bool includeRecordDigest)
        {
            return Phase5CanonicalBytesV1.Build(writer =>
            {
                Phase5CanonicalBytesV1.WriteUInt32(writer, CurrentSchemaVersion);
                Phase5CanonicalBytesV1.WriteStableId(writer, ownerEventId);
                Phase5CanonicalBytesV1.WriteStableId(writer, recordId);
                Phase5CanonicalBytesV1.WriteUInt64(writer, recordSequence);
                Phase5CanonicalBytesV1.WriteUInt16(writer, (ushort)eventRank);
                Phase5CanonicalBytesV1.WriteDouble(writer, eventTimeSeconds);
                Phase5CanonicalBytesV1.WriteDouble(writer, deltaTimeSeconds);
                Phase5CanonicalBytesV1.WriteStableId(writer, bundleId);
                Phase5CanonicalBytesV1.WriteUInt64(writer, coreStateVersionBefore);
                Phase5CanonicalBytesV1.WriteOptionalUInt64(writer, coreStateVersionAfterOrNA);
                Phase5CanonicalBytesV1.WriteUInt64(writer, nuclideStateVersionBefore);
                Phase5CanonicalBytesV1.WriteUInt64(writer, nuclideStateVersionAfter);
                Phase5CanonicalBytesV1.WriteDouble(writer, nodeVolumeM3);
                Phase5CanonicalBytesV1.WriteDouble(writer, iBefore);
                Phase5CanonicalBytesV1.WriteDouble(writer, iAfter);
                Phase5CanonicalBytesV1.WriteDouble(writer, xeBefore);
                Phase5CanonicalBytesV1.WriteDouble(writer, xeAfter);
                Phase5CanonicalBytesV1.WriteDouble(writer, iDensityBefore);
                Phase5CanonicalBytesV1.WriteDouble(writer, iDensityAfter);
                Phase5CanonicalBytesV1.WriteDouble(writer, xeDensityBefore);
                Phase5CanonicalBytesV1.WriteDouble(writer, xeDensityAfter);
                Phase5CanonicalBytesV1.WriteDouble(writer, iDirectProductionAtomsPerSecond);
                Phase5CanonicalBytesV1.WriteDouble(writer, iDecayLossAtomsPerSecond);
                Phase5CanonicalBytesV1.WriteDouble(writer, xeDirectProductionAtomsPerSecond);
                Phase5CanonicalBytesV1.WriteDouble(writer, xeFromI135DecayAtomsPerSecond);
                Phase5CanonicalBytesV1.WriteDouble(writer, xeDecayLossAtomsPerSecond);
                Phase5CanonicalBytesV1.WriteDouble(writer, xeAbsorptionLossAtomsPerSecond);
                Phase5CanonicalBytesV1.WriteString(writer, nuclideDataId);
                Phase5CanonicalBytesV1.WriteDigest(writer, nuclideDataDigest);
                Phase5CanonicalBytesV1.WriteDigest(writer, stateBindingDigest);
                if (includeRecordDigest)
                {
                    Phase5CanonicalBytesV1.WriteDigest(writer, new Digest32(
                        Phase5CanonicalBytesV1.HashBodyBytes(
                            "CANDU-NUCLIDE-TRANSITION-V1",
                            BuildBody(
                                ownerEventId,
                                recordId,
                                recordSequence,
                                eventRank,
                                eventTimeSeconds,
                                deltaTimeSeconds,
                                bundleId,
                                coreStateVersionBefore,
                                coreStateVersionAfterOrNA,
                                nuclideStateVersionBefore,
                                nuclideStateVersionAfter,
                                nodeVolumeM3,
                                iBefore,
                                iAfter,
                                xeBefore,
                                xeAfter,
                                iDensityBefore,
                                iDensityAfter,
                                xeDensityBefore,
                                xeDensityAfter,
                                iDirectProductionAtomsPerSecond,
                                iDecayLossAtomsPerSecond,
                                xeDirectProductionAtomsPerSecond,
                                xeFromI135DecayAtomsPerSecond,
                                xeDecayLossAtomsPerSecond,
                                xeAbsorptionLossAtomsPerSecond,
                                nuclideDataId,
                                nuclideDataDigest,
                                stateBindingDigest,
                                false))));
                }
            });
        }

        private static ContractValidationResult<NuclideTransitionRecordV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<NuclideTransitionRecordV1>.Invalid(code, path, message);
        }
    }

    /// <summary>
    /// Complete authoritative I/Xe envelope owned by one persistent bundle.
    /// Number densities and both digests are derived/validated values.
    /// </summary>
    public sealed class NuclideStateEnvelopeV1
    {
        public const uint CurrentSchemaVersion = 1;

        private NuclideStateEnvelopeV1(
            StableId bundleId,
            double iInventory,
            double xeInventory,
            double initialI,
            double initialXe,
            double nodeVolumeM3,
            double iDensity,
            double xeDensity,
            ulong nuclideStateVersion,
            NuclideDataV1 data,
            IEnumerable<NuclideTransitionRecordV1> history,
            Digest32 historyDigest,
            Digest32 stateDigest)
        {
            BundleId = bundleId;
            I135AtomInventory = iInventory;
            Xe135AtomInventory = xeInventory;
            InitialI135 = initialI;
            InitialXe135 = initialXe;
            NodeVolumeM3 = nodeVolumeM3;
            I135NumberDensity = iDensity;
            Xe135NumberDensity = xeDensity;
            NuclideStateVersion = nuclideStateVersion;
            NuclideDataId = data.DataId;
            NuclideDataDigest = data.DataDigest;
            Data = data;
            _history = new ReadOnlyCollection<NuclideTransitionRecordV1>(history.ToArray());
            NuclideHistoryDigest = historyDigest;
            NuclideStateDigest = stateDigest;
        }

        private readonly ReadOnlyCollection<NuclideTransitionRecordV1> _history;

        public StableId BundleId { get; }

        public double I135AtomInventory { get; }

        public double Xe135AtomInventory { get; }

        public double InitialI135 { get; }

        public double InitialXe135 { get; }

        public double NodeVolumeM3 { get; }

        public double I135NumberDensity { get; }

        public double Xe135NumberDensity { get; }

        public ulong NuclideStateVersion { get; }

        public string NuclideDataId { get; }

        public Digest32 NuclideDataDigest { get; }

        public NuclideDataV1 Data { get; }

        public IReadOnlyList<NuclideTransitionRecordV1> I135XeHistory
        {
            get { return _history; }
        }

        public Digest32 NuclideHistoryDigest { get; }

        public Digest32 NuclideStateDigest { get; }

        public static ContractValidationResult<NuclideStateEnvelopeV1> TryCreate(
            StableId bundleId,
            double iInventory,
            double xeInventory,
            double initialI,
            double initialXe,
            double nodeVolumeM3,
            ulong nuclideStateVersion,
            NuclideDataV1 data,
            IEnumerable<NuclideTransitionRecordV1> history,
            Digest32? expectedHistoryDigest = null,
            Digest32? expectedStateDigest = null)
        {
            if (bundleId.IsEmpty)
            {
                return Invalid(
                    "NuclideState.BundleId.Empty",
                    "bundle_id",
                    "A nuclide envelope requires its persistent bundle identity.");
            }

            if (data == null)
            {
                return Invalid(
                    "NuclideState.Data.Missing",
                    "data",
                    "A nuclide envelope requires validated material/table data.");
            }

            if (history == null)
            {
                return Invalid(
                    "NuclideState.History.Missing",
                    "history",
                    "A nuclide envelope requires an explicit counted history, including empty history.");
            }

            double[] values = { iInventory, xeInventory, initialI, initialXe, nodeVolumeM3 };
            if (values.Any(value => !PowerHistoryRecordV1.IsCanonicalFinite(value)))
            {
                return Invalid(
                    "NuclideState.NonFinite",
                    "state",
                    "Nuclide inventories, initial values, and volume must be finite and canonical.");
            }

            if (!PowerHistoryRecordV1.IsCanonicalNonnegative(iInventory) ||
                !PowerHistoryRecordV1.IsCanonicalNonnegative(xeInventory) ||
                !PowerHistoryRecordV1.IsCanonicalNonnegative(initialI) ||
                !PowerHistoryRecordV1.IsCanonicalNonnegative(initialXe) ||
                !PowerHistoryRecordV1.IsCanonicalPositiveFinite(nodeVolumeM3))
            {
                return Invalid(
                    "NuclideState.Range.Invalid",
                    "state",
                    "Atom inventories must be finite, canonical, and nonnegative; node volume must be finite, canonical, and strictly positive.");
            }

            if (data.MaterialVariantId.Value.Length == 0)
            {
                return Invalid(
                    "NuclideState.Data.MaterialVariant.Empty",
                    "data.material_variant_id",
                    "Nuclide data must retain its material identity.");
            }

            double iDensity = iInventory / nodeVolumeM3;
            double xeDensity = xeInventory / nodeVolumeM3;
            if (!PowerHistoryRecordV1.IsCanonicalNonnegative(iDensity) ||
                !PowerHistoryRecordV1.IsCanonicalNonnegative(xeDensity))
            {
                return Invalid(
                    "NuclideState.Density.NonFinite",
                    "state.density",
                    "Derived number densities must remain finite, canonical, and nonnegative.");
            }

            NuclideTransitionRecordV1[] records = history.ToArray();
            for (int index = 0; index < records.Length; index++)
            {
                if (records[index] == null)
                {
                    return Invalid(
                        "NuclideState.History.Null",
                        "history[" + index.ToString(CultureInfo.InvariantCulture) + "]",
                        "Nuclide history records may not be null.");
                }
            }

            var ordered = records
                .OrderBy(record => record.EventTimeSeconds)
                .ThenBy(record => (ushort)record.EventRank)
                .ThenBy(record => record.RecordSequence)
                .ThenBy(record => record.RecordId)
                .ToArray();
            for (int index = 0; index < ordered.Length; index++)
            {
                NuclideTransitionRecordV1 record = ordered[index];
                if (records[index] != record)
                {
                    return Invalid(
                        "NuclideState.History.Order.Invalid",
                        "history",
                        "I/Xe history must be supplied in its strict canonical order; reordered history is rejected.");
                }

                if (record.BundleId != bundleId || record.NuclideDataId != data.DataId ||
                    !record.NuclideDataDigest.Equals(data.DataDigest))
                {
                    return Invalid(
                        "NuclideState.History.BindingMismatch",
                        "history[" + index.ToString(CultureInfo.InvariantCulture) + "]",
                        "Every history record must bind the owning bundle and exact nuclide data identity.");
                }

                if (index > 0 && CompareHistory(ordered[index - 1], record) >= 0)
                {
                    return Invalid(
                        "NuclideState.History.Order.Invalid",
                        "history",
                        "I/Xe history must be strictly ordered by the approved canonical key.");
                }

                if (index > 0 && ordered[index - 1].NuclideStateVersionAfter != record.NuclideStateVersionBefore)
                {
                    return Invalid(
                        "NuclideState.History.VersionChain.Invalid",
                        "history",
                        "Nuclide history version bindings must form one contiguous chain.");
                }
            }

            if (ordered.Length > 0)
            {
                if (ordered[0].NuclideStateVersionBefore > nuclideStateVersion ||
                    ordered[ordered.Length - 1].NuclideStateVersionAfter != nuclideStateVersion)
                {
                    return Invalid(
                        "NuclideState.Version.Mismatch",
                        "nuclide_state_version",
                        "The current nuclide version must equal the final accepted history version.");
                }

                if (ordered[0].I135AtomInventoryBefore != initialI ||
                    ordered[0].Xe135AtomInventoryBefore != initialXe)
                {
                    return Invalid(
                        "NuclideState.History.InitialMismatch",
                        "history[0]",
                        "The first accepted I/Xe record must begin at the explicit initial inventories.");
                }

                for (int index = 1; index < ordered.Length; index++)
                {
                    NuclideTransitionRecordV1 previous = ordered[index - 1];
                    NuclideTransitionRecordV1 current = ordered[index];
                    if (previous.I135AtomInventoryAfter != current.I135AtomInventoryBefore ||
                        previous.Xe135AtomInventoryAfter != current.Xe135AtomInventoryBefore)
                    {
                        return Invalid(
                            "NuclideState.History.InventoryChain.Invalid",
                            "history",
                            "Adjacent I/Xe history records must chain exact before/after atom inventories.");
                    }
                }

                NuclideTransitionRecordV1 finalRecord = ordered[ordered.Length - 1];
                if (finalRecord.I135AtomInventoryAfter != iInventory ||
                    finalRecord.Xe135AtomInventoryAfter != xeInventory)
                {
                    return Invalid(
                        "NuclideState.History.CurrentMismatch",
                        "history",
                        "The current I/Xe inventories must equal the final accepted history record.");
                }
            }
            else if (iInventory != initialI || xeInventory != initialXe)
            {
                return Invalid(
                    "NuclideState.History.EmptyCurrentMismatch",
                    "state",
                    "A state with empty I/Xe history must retain its explicit initial inventories exactly.");
            }

            Digest32 historyDigest = ComputeHistoryDigest(bundleId, ordered);
            if (expectedHistoryDigest != null && !expectedHistoryDigest.Equals(historyDigest))
            {
                return Invalid(
                    "NuclideState.HistoryDigest.Mismatch",
                    "nuclide_history_digest",
                    "The supplied history digest does not equal the canonical ordered history.");
            }

            Digest32 stateDigest = ComputeStateDigest(
                bundleId,
                iInventory,
                xeInventory,
                initialI,
                initialXe,
                nodeVolumeM3,
                iDensity,
                xeDensity,
                nuclideStateVersion,
                data,
                ordered,
                historyDigest);
            if (expectedStateDigest != null && !expectedStateDigest.Equals(stateDigest))
            {
                return Invalid(
                    "NuclideState.Digest.Mismatch",
                    "nuclide_state_digest",
                    "The supplied nuclide state digest does not equal the canonical envelope.");
            }

            return ContractValidationResult<NuclideStateEnvelopeV1>.Valid(
                new NuclideStateEnvelopeV1(
                    bundleId,
                    iInventory,
                    xeInventory,
                    initialI,
                    initialXe,
                    nodeVolumeM3,
                    iDensity,
                    xeDensity,
                    nuclideStateVersion,
                    data,
                    ordered,
                    historyDigest,
                    stateDigest));
        }

        public static ContractValidationResult<NuclideStateEnvelopeV1> TryCreateFresh(
            StableId bundleId,
            double initialI,
            double initialXe,
            double nodeVolumeM3,
            ulong nuclideStateVersion,
            NuclideDataV1 data)
        {
            return TryCreate(
                bundleId,
                initialI,
                initialXe,
                initialI,
                initialXe,
                nodeVolumeM3,
                nuclideStateVersion,
                data,
                Array.Empty<NuclideTransitionRecordV1>());
        }

        public ContractValidationResult<NuclideStateEnvelopeV1> TryMoveToVolume(double nodeVolumeM3)
        {
            return TryCreate(
                BundleId,
                I135AtomInventory,
                Xe135AtomInventory,
                InitialI135,
                InitialXe135,
                nodeVolumeM3,
                NuclideStateVersion,
                Data,
                I135XeHistory,
                NuclideHistoryDigest);
        }

        public byte[] ToCanonicalBytes()
        {
            return BuildStateBytes(
                BundleId,
                I135AtomInventory,
                Xe135AtomInventory,
                InitialI135,
                InitialXe135,
                NodeVolumeM3,
                I135NumberDensity,
                Xe135NumberDensity,
                NuclideStateVersion,
                Data,
                I135XeHistory,
                NuclideHistoryDigest);
        }

        internal static Digest32 ComputeHistoryDigest(
            StableId bundleId,
            IReadOnlyList<NuclideTransitionRecordV1> history)
        {
            byte[] bytes = Phase5CanonicalBytesV1.HashBody(
                "CANDU-NUCLIDE-HISTORY-V1",
                writer =>
                {
                    Phase5CanonicalBytesV1.WriteUInt32(writer, CurrentSchemaVersion);
                    Phase5CanonicalBytesV1.WriteStableId(writer, bundleId);
                    Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)history.Count));
                    foreach (NuclideTransitionRecordV1 record in history)
                    {
                        Phase5CanonicalBytesV1.WriteBytes(writer, record.ToCanonicalBytes());
                    }
                });
            return new Digest32(bytes);
        }

        internal static Digest32 ComputeStateDigest(
            StableId bundleId,
            double iInventory,
            double xeInventory,
            double initialI,
            double initialXe,
            double nodeVolumeM3,
            double iDensity,
            double xeDensity,
            ulong nuclideStateVersion,
            NuclideDataV1 data,
            IReadOnlyList<NuclideTransitionRecordV1> history,
            Digest32 historyDigest)
        {
            return new Digest32(Phase5CanonicalBytesV1.HashBody(
                "CANDU-NUCLIDE-STATE-V1",
                writer =>
                {
                    Phase5CanonicalBytesV1.WriteUInt32(writer, CurrentSchemaVersion);
                    Phase5CanonicalBytesV1.WriteStableId(writer, bundleId);
                    Phase5CanonicalBytesV1.WriteDouble(writer, iInventory);
                    Phase5CanonicalBytesV1.WriteDouble(writer, xeInventory);
                    Phase5CanonicalBytesV1.WriteDouble(writer, initialI);
                    Phase5CanonicalBytesV1.WriteDouble(writer, initialXe);
                    Phase5CanonicalBytesV1.WriteDouble(writer, nodeVolumeM3);
                    Phase5CanonicalBytesV1.WriteDouble(writer, iDensity);
                    Phase5CanonicalBytesV1.WriteDouble(writer, xeDensity);
                    Phase5CanonicalBytesV1.WriteUInt64(writer, nuclideStateVersion);
                    Phase5CanonicalBytesV1.WriteString(writer, data.DataId);
                    Phase5CanonicalBytesV1.WriteDigest(writer, data.DataDigest);
                    Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)history.Count));
                    foreach (NuclideTransitionRecordV1 record in history)
                    {
                        Phase5CanonicalBytesV1.WriteBytes(writer, record.ToCanonicalBytes());
                    }

                    Phase5CanonicalBytesV1.WriteDigest(writer, historyDigest);
                }));
        }

        private static int CompareHistory(
            NuclideTransitionRecordV1 left,
            NuclideTransitionRecordV1 right)
        {
            int result = left.EventTimeSeconds.CompareTo(right.EventTimeSeconds);
            if (result != 0) return result;
            result = ((ushort)left.EventRank).CompareTo((ushort)right.EventRank);
            if (result != 0) return result;
            result = left.RecordSequence.CompareTo(right.RecordSequence);
            return result != 0 ? result : left.RecordId.CompareTo(right.RecordId);
        }

        private static byte[] BuildStateBytes(
            StableId bundleId,
            double iInventory,
            double xeInventory,
            double initialI,
            double initialXe,
            double nodeVolumeM3,
            double iDensity,
            double xeDensity,
            ulong nuclideStateVersion,
            NuclideDataV1 data,
            IReadOnlyList<NuclideTransitionRecordV1> history,
            Digest32 historyDigest)
        {
            return Phase5CanonicalBytesV1.Build(writer =>
            {
                Phase5CanonicalBytesV1.WriteAscii(writer, "CANDU-NUCLIDE-STATE-V1");
                writer.Write((byte)0);
                Phase5CanonicalBytesV1.WriteUInt32(writer, CurrentSchemaVersion);
                Phase5CanonicalBytesV1.WriteStableId(writer, bundleId);
                Phase5CanonicalBytesV1.WriteDouble(writer, iInventory);
                Phase5CanonicalBytesV1.WriteDouble(writer, xeInventory);
                Phase5CanonicalBytesV1.WriteDouble(writer, initialI);
                Phase5CanonicalBytesV1.WriteDouble(writer, initialXe);
                Phase5CanonicalBytesV1.WriteDouble(writer, nodeVolumeM3);
                Phase5CanonicalBytesV1.WriteDouble(writer, iDensity);
                Phase5CanonicalBytesV1.WriteDouble(writer, xeDensity);
                Phase5CanonicalBytesV1.WriteUInt64(writer, nuclideStateVersion);
                Phase5CanonicalBytesV1.WriteString(writer, data.DataId);
                Phase5CanonicalBytesV1.WriteDigest(writer, data.DataDigest);
                Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)history.Count));
                foreach (NuclideTransitionRecordV1 record in history)
                {
                    Phase5CanonicalBytesV1.WriteBytes(writer, record.ToCanonicalBytes());
                }

                Phase5CanonicalBytesV1.WriteDigest(writer, historyDigest);
            });
        }

        private static ContractValidationResult<NuclideStateEnvelopeV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<NuclideStateEnvelopeV1>.Invalid(code, path, message);
        }
    }

    /// <summary>
    /// Frozen left-endpoint I/Xe integration inputs. Fluxes are actual-group
    /// fluxes in m^-2 s^-1 and fission rate density is m^-3 s^-1.
    /// </summary>
    public sealed class NuclideIntegrationInputV1
    {
        private NuclideIntegrationInputV1(
            StableId ownerEventId,
            ulong recordSequence,
            EventRankV1 eventRank,
            double eventTimeSeconds,
            double deltaTimeSeconds,
            ulong coreStateVersion,
            double fissionRateDensity,
            double fluxGroup1,
            double fluxGroup2,
            NuclideDataV1 data,
            Digest32 stateBindingDigest)
        {
            OwnerEventId = ownerEventId;
            RecordSequence = recordSequence;
            EventRank = eventRank;
            EventTimeSeconds = eventTimeSeconds;
            DeltaTimeSeconds = deltaTimeSeconds;
            CoreStateVersion = coreStateVersion;
            FissionRateDensity = fissionRateDensity;
            FluxGroup1 = fluxGroup1;
            FluxGroup2 = fluxGroup2;
            Data = data;
            StateBindingDigest = stateBindingDigest;
        }

        public StableId OwnerEventId { get; }

        public ulong RecordSequence { get; }

        public EventRankV1 EventRank { get; }

        public double EventTimeSeconds { get; }

        public double DeltaTimeSeconds { get; }

        public ulong CoreStateVersion { get; }

        public double FissionRateDensity { get; }

        public double FluxGroup1 { get; }

        public double FluxGroup2 { get; }

        public NuclideDataV1 Data { get; }

        public Digest32 StateBindingDigest { get; }

        public static ContractValidationResult<NuclideIntegrationInputV1> TryCreate(
            StableId ownerEventId,
            ulong recordSequence,
            EventRankV1 eventRank,
            double eventTimeSeconds,
            double deltaTimeSeconds,
            ulong coreStateVersion,
            double fissionRateDensity,
            double fluxGroup1,
            double fluxGroup2,
            NuclideDataV1 data,
            Digest32 stateBindingDigest)
        {
            if (ownerEventId.IsEmpty || data == null || stateBindingDigest == null)
            {
                return Invalid(
                    "NuclideIntegrationInput.IdentityOrData.Missing",
                    "input",
                    "Integration requires an owner event, validated data, and a binding digest.");
            }

            if (!Enum.IsDefined(typeof(EventRankV1), eventRank) ||
                !PowerHistoryRecordV1.IsCanonicalTime(eventTimeSeconds) ||
                !PowerHistoryRecordV1.IsCanonicalPositiveFinite(deltaTimeSeconds))
            {
                return Invalid(
                    "NuclideIntegrationInput.Time.Invalid",
                    "input.time",
                    "Integration requires a defined event rank, canonical time, and positive duration.");
            }

            double[] values = { fissionRateDensity, fluxGroup1, fluxGroup2 };
            if (values.Any(value => !PowerHistoryRecordV1.IsCanonicalNonnegative(value)))
            {
                return Invalid(
                    "NuclideIntegrationInput.FluxOrRate.Invalid",
                    "input.flux_or_fission_rate",
                    "Fission rate density and actual group fluxes must be finite, canonical, and nonnegative.");
            }

            return ContractValidationResult<NuclideIntegrationInputV1>.Valid(
                new NuclideIntegrationInputV1(
                    ownerEventId,
                    recordSequence,
                    eventRank,
                    eventTimeSeconds,
                    deltaTimeSeconds,
                    coreStateVersion,
                    fissionRateDensity,
                    fluxGroup1,
                    fluxGroup2,
                    data,
                    stateBindingDigest));
        }

        private static ContractValidationResult<NuclideIntegrationInputV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<NuclideIntegrationInputV1>.Invalid(code, path, message);
        }
    }

    public sealed class NuclideIntegrationResultV1
    {
        internal NuclideIntegrationResultV1(
            NuclideStateEnvelopeV1 resultingState,
            NuclideTransitionRecordV1 transition)
        {
            ResultingState = resultingState;
            Transition = transition;
        }

        public NuclideStateEnvelopeV1 ResultingState { get; }

        public NuclideTransitionRecordV1 Transition { get; }
    }

    /// <summary>
    /// Applies the approved explicit Euler I/Xe equations without clamping or
    /// hidden equilibrium/reset behavior.
    /// </summary>
    public static class NuclideIntegrationTransitionV1
    {
        public static ContractValidationResult<NuclideIntegrationResultV1> TryApply(
            NuclideStateEnvelopeV1 current,
            NuclideIntegrationInputV1 input)
        {
            if (current == null || input == null)
            {
                return Invalid(
                    "NuclideIntegration.StateOrInput.Missing",
                    "integration",
                    "A validated current nuclide state and integration input are required.");
            }

            if (!string.Equals(current.NuclideDataId, input.Data.DataId, StringComparison.Ordinal) ||
                !current.NuclideDataDigest.Equals(input.Data.DataDigest) ||
                current.Data.MaterialVariantId != input.Data.MaterialVariantId)
            {
                return Invalid(
                    "NuclideIntegration.Data.Stale",
                    "data",
                    "Integration data must match the exact current material/table identity and digest.");
            }

            if (current.NuclideStateVersion == ulong.MaxValue)
            {
                return Invalid(
                    "NuclideIntegration.Version.Overflow",
                    "nuclide_state_version",
                    "The per-bundle nuclide version cannot increment beyond UInt64.MaxValue.");
            }

            NuclideDataV1 data = input.Data;
            double iDirect = current.NodeVolumeM3 * data.GammaI * input.FissionRateDensity;
            double iDecay = CanonicalNegativeLoss(data.LambdaI * current.I135AtomInventory);
            double xeDirect = current.NodeVolumeM3 * data.GammaXe * input.FissionRateDensity;
            double xeFromI = iDecay == 0.0 ? 0.0 : -iDecay;
            double absorptionRate =
                (data.SigmaXeGroup1M2 * input.FluxGroup1) +
                (data.SigmaXeGroup2M2 * input.FluxGroup2);
            double xeDecay = CanonicalNegativeLoss(data.LambdaXe * current.Xe135AtomInventory);
            double xeAbsorption = CanonicalNegativeLoss(absorptionRate * current.Xe135AtomInventory);

            double nextI = current.I135AtomInventory +
                (input.DeltaTimeSeconds * (iDirect + iDecay));
            double nextXe = current.Xe135AtomInventory +
                (input.DeltaTimeSeconds * (xeDirect + xeFromI + xeDecay + xeAbsorption));
            double[] computedTerms =
            {
                absorptionRate,
                iDirect,
                iDecay,
                xeDirect,
                xeFromI,
                xeDecay,
                xeAbsorption
            };
            if (!PowerHistoryRecordV1.IsCanonicalNonnegative(nextI) ||
                !PowerHistoryRecordV1.IsCanonicalNonnegative(nextXe) ||
                computedTerms.Any(value => !PowerHistoryRecordV1.IsCanonicalFinite(value)))
            {
                return Invalid(
                    "NuclideIntegration.Result.Invalid",
                    "integration.result",
                    "The explicit Euler result must remain finite and nonnegative without clamping.");
            }

            ContractValidationResult<NuclideTransitionRecordV1> record =
                NuclideTransitionRecordV1.TryCreate(
                    input.OwnerEventId,
                    input.RecordSequence,
                    input.EventRank,
                    input.EventTimeSeconds,
                    input.DeltaTimeSeconds,
                    current.BundleId,
                    input.CoreStateVersion,
                    OptionalUInt64.Applicable(input.CoreStateVersion),
                    current.NuclideStateVersion,
                    current.NuclideStateVersion + 1,
                    current.NodeVolumeM3,
                    current.I135AtomInventory,
                    nextI,
                    current.Xe135AtomInventory,
                    nextXe,
                    current.I135NumberDensity,
                    nextI / current.NodeVolumeM3,
                    current.Xe135NumberDensity,
                    nextXe / current.NodeVolumeM3,
                    iDirect,
                    iDecay,
                    xeDirect,
                    xeFromI,
                    xeDecay,
                    xeAbsorption,
                    data.DataId,
                    data.DataDigest,
                    input.StateBindingDigest);
            if (!record.IsValid)
            {
                return ContractValidationResult<NuclideIntegrationResultV1>.Invalid(
                    record.FirstDiagnostic.Code,
                    record.FirstDiagnostic.Path,
                    record.FirstDiagnostic.Message);
            }

            var history = current.I135XeHistory.ToList();
            history.Add(record.Value);
            ContractValidationResult<NuclideStateEnvelopeV1> next = NuclideStateEnvelopeV1.TryCreate(
                current.BundleId,
                nextI,
                nextXe,
                current.InitialI135,
                current.InitialXe135,
                current.NodeVolumeM3,
                current.NuclideStateVersion + 1,
                data,
                history);
            if (!next.IsValid)
            {
                return ContractValidationResult<NuclideIntegrationResultV1>.Invalid(
                    next.FirstDiagnostic.Code,
                    next.FirstDiagnostic.Path,
                    next.FirstDiagnostic.Message);
            }

            return ContractValidationResult<NuclideIntegrationResultV1>.Valid(
                new NuclideIntegrationResultV1(next.Value, record.Value));
        }

        private static double CanonicalNegativeLoss(double nonnegativeMagnitude)
        {
            return nonnegativeMagnitude == 0.0 ? 0.0 : -nonnegativeMagnitude;
        }

        private static ContractValidationResult<NuclideIntegrationResultV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<NuclideIntegrationResultV1>.Invalid(code, path, message);
        }
    }

    /// <summary>
    /// One complete bundle entry in an accepted P2-T02 power snapshot.
    /// </summary>
    public sealed class CompletePowerSnapshotBundleV1
    {
        private CompletePowerSnapshotBundleV1(
            StableId bundleId,
            ChannelId channelId,
            BundlePosition position,
            double powerWatts,
            ulong nuclideStateVersion,
            BundleCoefficientBindingV1 coefficientBinding)
        {
            BundleId = bundleId;
            ChannelId = channelId;
            Position = position;
            PowerWatts = powerWatts;
            NuclideStateVersion = nuclideStateVersion;
            CoefficientBinding = coefficientBinding;
        }

        public StableId BundleId { get; }

        public ChannelId ChannelId { get; }

        public BundlePosition Position { get; }

        public NodeKey Node
        {
            get { return new NodeKey(ChannelId, Position); }
        }

        public double PowerWatts { get; }

        public ulong NuclideStateVersion { get; }

        public BundleCoefficientBindingV1 CoefficientBinding { get; }

        public static ContractValidationResult<CompletePowerSnapshotBundleV1> TryCreate(
            StableId bundleId,
            ChannelId channelId,
            BundlePosition position,
            double powerWatts,
            ulong nuclideStateVersion,
            BundleCoefficientBindingV1 coefficientBinding)
        {
            if (bundleId.IsEmpty || coefficientBinding == null)
            {
                return Invalid(
                    "PowerSnapshot.Bundle.IdentityOrCoefficient.Missing",
                    "bundle",
                    "A complete snapshot entry requires a bundle identity and coefficient binding.");
            }

            if (!PowerHistoryRecordV1.IsCanonicalNonnegative(powerWatts))
            {
                return Invalid(
                    "PowerSnapshot.Bundle.Power.Invalid",
                    "bundle.power_w",
                    "Snapshot bundle power must be finite and nonnegative SI watts.");
            }

            return ContractValidationResult<CompletePowerSnapshotBundleV1>.Valid(
                new CompletePowerSnapshotBundleV1(
                    bundleId,
                    channelId,
                    position,
                    powerWatts,
                    nuclideStateVersion,
                    coefficientBinding));
        }

        private static ContractValidationResult<CompletePowerSnapshotBundleV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<CompletePowerSnapshotBundleV1>.Invalid(code, path, message);
        }
    }

    /// <summary>
    /// Full accepted-power identity required before a positive-duration
    /// interval may begin.
    /// </summary>
    public sealed class CompletePowerSnapshotV1
    {
        public const uint CurrentSchemaVersion = 1;

        private CompletePowerSnapshotV1(
            StableId spatialSolveId,
            StableId powerSnapshotId,
            double snapshotTimeSeconds,
            ulong coreStateVersion,
            ulong spatialStateVersion,
            ulong powerSnapshotVersion,
            Digest32 stateDigest,
            Digest32 inventoryDigest,
            Digest32 acceptedInventoryDigest,
            Digest32 burnupEnergyDigest,
            Digest32 coefficientDigest,
            Digest32 topologyDigest,
            Digest32 dataPackDigest,
            Digest32 snapshotDigest,
            IEnumerable<CompletePowerSnapshotBundleV1> bundles,
            IEnumerable<BundleNuclideVersionV1> bundleNuclideVersions)
        {
            SpatialSolveId = spatialSolveId;
            PowerSnapshotId = powerSnapshotId;
            SnapshotTimeSeconds = snapshotTimeSeconds;
            CoreStateVersion = coreStateVersion;
            SpatialStateVersion = spatialStateVersion;
            PowerSnapshotVersion = powerSnapshotVersion;
            StateDigest = stateDigest;
            InventoryDigest = inventoryDigest;
            AcceptedInventoryDigest = acceptedInventoryDigest;
            BurnupEnergyDigest = burnupEnergyDigest;
            CoefficientDigest = coefficientDigest;
            TopologyDigest = topologyDigest;
            DataPackDigest = dataPackDigest;
            SnapshotDigest = snapshotDigest;
            Bundles = new ReadOnlyCollection<CompletePowerSnapshotBundleV1>(
                bundles.OrderBy(bundle => bundle.ChannelId.Value)
                    .ThenBy(bundle => bundle.Position.Value)
                    .ThenBy(bundle => bundle.BundleId)
                    .ToArray());
            BundleNuclideVersions = new ReadOnlyCollection<BundleNuclideVersionV1>(
                bundleNuclideVersions.OrderBy(record => record.BundleId).ToArray());
        }

        public StableId SpatialSolveId { get; }

        public StableId PowerSnapshotId { get; }

        public double SnapshotTimeSeconds { get; }

        public ulong CoreStateVersion { get; }

        public ulong SpatialStateVersion { get; }

        public ulong PowerSnapshotVersion { get; }

        public Digest32 StateDigest { get; }

        /// <summary>
        /// Canonical digest of the complete live inventory bound before this
        /// snapshot is accepted, including I/Xe, power history, and
        /// coefficient identity fields.
        /// </summary>
        public Digest32 InventoryDigest { get; }

        /// <summary>
        /// Canonical digest of the complete live inventory immediately after
        /// this snapshot is accepted. It includes every retained power
        /// history record and the current power/coefficient bindings consumed
        /// by the positive-duration burnup transition.
        /// </summary>
        public Digest32 AcceptedInventoryDigest { get; }

        /// <summary>
        /// Canonical digest of the live bundle identity and burnup/energy
        /// fields bound by the accepted snapshot.
        /// </summary>
        public Digest32 BurnupEnergyDigest { get; }

        public Digest32 CoefficientDigest { get; }

        public Digest32 TopologyDigest { get; }

        public Digest32 DataPackDigest { get; }

        public Digest32 SnapshotDigest { get; }

        public IReadOnlyList<CompletePowerSnapshotBundleV1> Bundles { get; }

        public IReadOnlyList<BundleNuclideVersionV1> BundleNuclideVersions { get; }

        /// <summary>
        /// Reconstructs the accepted complete snapshot from a restored
        /// inventory and its lifecycle-authenticated binding. This is the
        /// restart path for callers that persist the state archive rather than
        /// retaining the pre-save solver result in memory.
        /// </summary>
        public static ContractValidationResult<CompletePowerSnapshotV1> TryReconstructFromAcceptedState(
            BundleInventory inventory,
            VersionLifecycleV1 lifecycle)
        {
            if (inventory == null || lifecycle == null)
            {
                return Invalid(
                    "PowerSnapshot.Restore.Input.Missing",
                    "snapshot",
                    "Accepted snapshot reconstruction requires restored inventory and lifecycle state.");
            }

            if (!lifecycle.HasSameBundleIdentitySet(inventory) ||
                lifecycle.SpatialBindingStatus != BindingStatusV1.Valid ||
                lifecycle.PowerBindingStatus != BindingStatusV1.Valid ||
                !lifecycle.SpatialSolveId.IsApplicable ||
                !lifecycle.PowerSnapshotId.IsApplicable ||
                !lifecycle.StateDigest.IsApplicable ||
                !lifecycle.CoefficientDigest.IsApplicable ||
                !lifecycle.TopologyDigest.IsApplicable ||
                !lifecycle.DataPackDigest.IsApplicable ||
                !lifecycle.SnapshotDigest.IsApplicable ||
                !lifecycle.PowerSnapshotInventoryDigest.IsApplicable ||
                !lifecycle.PowerSnapshotAcceptedInventoryDigest.IsApplicable ||
                !lifecycle.PowerSnapshotBurnupEnergyDigest.IsApplicable ||
                lifecycle.StateDigest.Value == null ||
                lifecycle.CoefficientDigest.Value == null ||
                lifecycle.TopologyDigest.Value == null ||
                lifecycle.DataPackDigest.Value == null ||
                lifecycle.SnapshotDigest.Value == null ||
                lifecycle.PowerSnapshotInventoryDigest.Value == null ||
                lifecycle.PowerSnapshotAcceptedInventoryDigest.Value == null ||
                lifecycle.PowerSnapshotBurnupEnergyDigest.Value == null)
            {
                return Invalid(
                    "PowerSnapshot.Restore.Binding.Invalid",
                    "snapshot.binding",
                    "Accepted snapshot reconstruction requires one complete lifecycle binding and all authenticated digests.");
            }

            if (!CompleteStateDigestV1.ComputeInventory(inventory)
                    .Equals(lifecycle.PowerSnapshotAcceptedInventoryDigest.Value) ||
                !CompleteStateDigestV1.ComputeBurnupEnergy(inventory)
                    .Equals(lifecycle.PowerSnapshotBurnupEnergyDigest.Value))
            {
                return Invalid(
                    "PowerSnapshot.Restore.StateDigest.Stale",
                    "snapshot.state_digests",
                    "The restored complete inventory must equal the lifecycle-authenticated accepted inventory and burnup/energy digests.");
            }

            var entries = new List<CompletePowerSnapshotBundleV1>(inventory.OccupiedCount);
            foreach (BundleState bundle in inventory.EnumerateOccupied()
                         .OrderBy(candidate => candidate.ChannelId.Value)
                         .ThenBy(candidate => candidate.Position.Value)
                         .ThenBy(candidate => candidate.BundleId))
            {
                if (bundle.NuclideState == null ||
                    !bundle.PowerWatts.IsApplicable ||
                    !bundle.PowerSnapshotId.IsApplicable ||
                    bundle.PowerSnapshotId.Value != lifecycle.PowerSnapshotId.Value ||
                    bundle.CoefficientBinding == null ||
                    bundle.PowerHistory.Count == 0)
                {
                    return Invalid(
                        "PowerSnapshot.Restore.BundleBinding.Invalid",
                        "snapshot.bundles",
                        "Every restored complete bundle must retain the accepted power, latest history record, coefficient binding, and nuclide state.");
                }

                PowerHistoryRecordV1 latest = bundle.PowerHistory[bundle.PowerHistory.Count - 1];
                if (latest.SnapshotTimeSeconds != lifecycle.CurrentSimulationTimeSeconds ||
                    latest.PowerWatts != bundle.PowerWatts.Value ||
                    latest.CoreStateVersion != lifecycle.CoreStateVersion ||
                    latest.SpatialStateVersion != lifecycle.SpatialStateVersion ||
                    latest.PowerSnapshotVersion != lifecycle.PowerSnapshotVersion)
                {
                    return Invalid(
                        "PowerSnapshot.Restore.PowerHistory.Stale",
                        "snapshot.bundles.power_history",
                        "Every restored complete bundle must retain a latest history record bound to the lifecycle snapshot.");
                }

                ContractValidationResult<CompletePowerSnapshotBundleV1> entry =
                    CompletePowerSnapshotBundleV1.TryCreate(
                        bundle.BundleId,
                        bundle.ChannelId,
                        bundle.Position,
                        bundle.PowerWatts.Value,
                        bundle.NuclideState.NuclideStateVersion,
                        bundle.CoefficientBinding);
                if (!entry.IsValid)
                {
                    return Invalid(entry.FirstDiagnostic.Code, entry.FirstDiagnostic.Path, entry.FirstDiagnostic.Message);
                }

                entries.Add(entry.Value);
            }

            return TryCreate(
                lifecycle.SpatialSolveId.Value,
                lifecycle.PowerSnapshotId.Value,
                lifecycle.CurrentSimulationTimeSeconds,
                lifecycle.CoreStateVersion,
                lifecycle.SpatialStateVersion,
                lifecycle.PowerSnapshotVersion,
                lifecycle.StateDigest.Value,
                lifecycle.PowerSnapshotInventoryDigest.Value,
                lifecycle.PowerSnapshotAcceptedInventoryDigest.Value,
                lifecycle.PowerSnapshotBurnupEnergyDigest.Value,
                lifecycle.CoefficientDigest.Value,
                lifecycle.TopologyDigest.Value,
                lifecycle.DataPackDigest.Value,
                lifecycle.SnapshotDigest.Value,
                entries,
                lifecycle.BundleNuclideVersions);
        }

        public static ContractValidationResult<CompletePowerSnapshotV1> TryCreate(
            StableId spatialSolveId,
            StableId powerSnapshotId,
            double snapshotTimeSeconds,
            ulong coreStateVersion,
            ulong spatialStateVersion,
            ulong powerSnapshotVersion,
            Digest32 stateDigest,
            Digest32 inventoryDigest,
            Digest32 acceptedInventoryDigest,
            Digest32 burnupEnergyDigest,
            Digest32 coefficientDigest,
            Digest32 topologyDigest,
            Digest32 dataPackDigest,
            Digest32 snapshotDigest,
            IEnumerable<CompletePowerSnapshotBundleV1> bundles,
            IEnumerable<BundleNuclideVersionV1> bundleNuclideVersions)
        {
            if (spatialSolveId.IsEmpty || powerSnapshotId.IsEmpty ||
                stateDigest == null || inventoryDigest == null || acceptedInventoryDigest == null ||
                burnupEnergyDigest == null ||
                coefficientDigest == null || topologyDigest == null ||
                dataPackDigest == null || snapshotDigest == null)
            {
                return Invalid(
                    "PowerSnapshot.Provenance.Missing",
                    "snapshot",
                    "A complete accepted snapshot requires all solver, state, coefficient, topology, data-pack, and snapshot identities.");
            }

            if (!PowerHistoryRecordV1.IsCanonicalTime(snapshotTimeSeconds))
            {
                return Invalid(
                    "PowerSnapshot.Time.Invalid",
                    "snapshot_time_s",
                    "Accepted snapshot time must be finite, nonnegative, and canonical.");
            }

            if (bundles == null || bundleNuclideVersions == null)
            {
                return Invalid(
                    "PowerSnapshot.Bundles.Missing",
                    "bundles",
                    "A complete accepted snapshot requires explicit bundle entries and version bindings.");
            }

            CompletePowerSnapshotBundleV1[] entries = bundles.ToArray();
            BundleNuclideVersionV1[] versions = bundleNuclideVersions.ToArray();
            if (entries.Any(entry => entry == null) || versions.Any(version => version == null))
            {
                return Invalid(
                    "PowerSnapshot.Bundles.Null",
                    "bundles",
                    "Snapshot entries and version records may not be null.");
            }

            CompletePowerSnapshotBundleV1[] orderedEntries = entries
                .OrderBy(entry => entry.ChannelId.Value)
                .ThenBy(entry => entry.Position.Value)
                .ThenBy(entry => entry.BundleId)
                .ToArray();
            BundleNuclideVersionV1[] orderedVersions = versions
                .OrderBy(version => version.BundleId)
                .ToArray();
            if (entries.Where((entry, index) => entry != orderedEntries[index]).Any() ||
                versions.Where((version, index) => version != orderedVersions[index]).Any())
            {
                return Invalid(
                    "PowerSnapshot.Order.Invalid",
                    "snapshot",
                    "Snapshot bundle entries and version records must already be in canonical order.");
            }

            var ids = new HashSet<StableId>();
            var nodes = new HashSet<NodeKey>();
            foreach (CompletePowerSnapshotBundleV1 entry in entries)
            {
                if (!ids.Add(entry.BundleId) || !nodes.Add(entry.Node))
                {
                    return Invalid(
                        "PowerSnapshot.Bundles.Duplicate",
                        "bundles",
                        "A complete snapshot requires one unique identity and node per bundle.");
                }
            }

            var versionIds = new HashSet<StableId>();
            foreach (BundleNuclideVersionV1 version in versions)
            {
                if (!versionIds.Add(version.BundleId) || version.BundleId.IsEmpty)
                {
                    return Invalid(
                        "PowerSnapshot.BundleVersions.Duplicate",
                        "bundle_nuclide_versions",
                        "Snapshot bundle versions must have unique nonempty identities.");
                }
            }

            if (ids.Count != versionIds.Count || ids.Any(id => !versionIds.Contains(id)))
            {
                return Invalid(
                    "PowerSnapshot.BundleVersions.IdentityMismatch",
                    "bundle_nuclide_versions",
                    "Snapshot bundle entries and nuclide-version identities must match exactly.");
            }

            Dictionary<StableId, ulong> entryVersions = entries.ToDictionary(
                entry => entry.BundleId,
                entry => entry.NuclideStateVersion);
            foreach (BundleNuclideVersionV1 version in versions)
            {
                if (entryVersions[version.BundleId] != version.NuclideStateVersion)
                {
                    return Invalid(
                        "PowerSnapshot.BundleVersions.ValueMismatch",
                        "bundle_nuclide_versions",
                        "Snapshot bundle nuclide versions must equal the corresponding complete bundle entry versions.");
                }
            }

            return ContractValidationResult<CompletePowerSnapshotV1>.Valid(
                new CompletePowerSnapshotV1(
                    spatialSolveId,
                    powerSnapshotId,
                    snapshotTimeSeconds,
                    coreStateVersion,
                    spatialStateVersion,
                    powerSnapshotVersion,
                    stateDigest,
                    inventoryDigest,
                    acceptedInventoryDigest,
                    burnupEnergyDigest,
                    coefficientDigest,
                    topologyDigest,
                    dataPackDigest,
                    snapshotDigest,
                    entries,
                    versions));
        }

        public byte[] ToCanonicalBytes()
        {
            return Phase5CanonicalBytesV1.Build(writer =>
            {
                Phase5CanonicalBytesV1.WriteAscii(writer, "CANDU-POWER-SNAPSHOT-V1");
                writer.Write((byte)0);
                Phase5CanonicalBytesV1.WriteUInt32(writer, CurrentSchemaVersion);
                Phase5CanonicalBytesV1.WriteStableId(writer, SpatialSolveId);
                Phase5CanonicalBytesV1.WriteStableId(writer, PowerSnapshotId);
                Phase5CanonicalBytesV1.WriteDouble(writer, SnapshotTimeSeconds);
                Phase5CanonicalBytesV1.WriteUInt64(writer, CoreStateVersion);
                Phase5CanonicalBytesV1.WriteUInt64(writer, SpatialStateVersion);
                Phase5CanonicalBytesV1.WriteUInt64(writer, PowerSnapshotVersion);
                Phase5CanonicalBytesV1.WriteDigest(writer, StateDigest);
                Phase5CanonicalBytesV1.WriteDigest(writer, InventoryDigest);
                Phase5CanonicalBytesV1.WriteDigest(writer, AcceptedInventoryDigest);
                Phase5CanonicalBytesV1.WriteDigest(writer, BurnupEnergyDigest);
                Phase5CanonicalBytesV1.WriteDigest(writer, CoefficientDigest);
                Phase5CanonicalBytesV1.WriteDigest(writer, TopologyDigest);
                Phase5CanonicalBytesV1.WriteDigest(writer, DataPackDigest);
                Phase5CanonicalBytesV1.WriteDigest(writer, SnapshotDigest);
                Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)Bundles.Count));
                foreach (CompletePowerSnapshotBundleV1 bundle in Bundles)
                {
                    Phase5CanonicalBytesV1.WriteStableId(writer, bundle.BundleId);
                    Phase5CanonicalBytesV1.WriteUInt32(writer, bundle.ChannelId.Value);
                    Phase5CanonicalBytesV1.WriteUInt32(writer, bundle.Position.Value);
                    Phase5CanonicalBytesV1.WriteDouble(writer, bundle.PowerWatts);
                    Phase5CanonicalBytesV1.WriteUInt64(writer, bundle.NuclideStateVersion);
                    Phase5CanonicalBytesV1.WriteBytes(writer, bundle.CoefficientBinding.ToCanonicalBytes());
                }

                Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)BundleNuclideVersions.Count));
                foreach (BundleNuclideVersionV1 version in BundleNuclideVersions)
                {
                    Phase5CanonicalBytesV1.WriteStableId(writer, version.BundleId);
                    Phase5CanonicalBytesV1.WriteUInt64(writer, version.InitialNuclideStateVersion);
                    Phase5CanonicalBytesV1.WriteUInt64(writer, version.NuclideStateVersion);
                }
            });
        }

        private static ContractValidationResult<CompletePowerSnapshotV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<CompletePowerSnapshotV1>.Invalid(code, path, message);
        }
    }

    public sealed class CompletePowerSnapshotResultV1
    {
        internal CompletePowerSnapshotResultV1(
            BundleInventory resultingInventory,
            VersionLifecycleV1 resultingLifecycle)
        {
            ResultingInventory = resultingInventory;
            ResultingLifecycle = resultingLifecycle;
        }

        public BundleInventory ResultingInventory { get; }

        public VersionLifecycleV1 ResultingLifecycle { get; }
    }

    /// <summary>
    /// Atomically accepts a full P2-T02 snapshot and appends one power-history
    /// record to every live complete bundle.
    /// </summary>
    public static class CompletePowerSnapshotTransitionV1
    {
        public static ContractValidationResult<CompletePowerSnapshotResultV1> TryApply(
            BundleInventory inventory,
            VersionLifecycleV1 lifecycle,
            CompletePowerSnapshotV1 snapshot)
        {
            if (inventory == null || lifecycle == null || snapshot == null)
            {
                return Invalid(
                    "PowerSnapshotTransition.Input.Missing",
                    "transition",
                    "A complete snapshot transition requires inventory, lifecycle, and snapshot inputs.");
            }

            if (snapshot.CoreStateVersion != lifecycle.CoreStateVersion ||
                snapshot.SnapshotTimeSeconds != lifecycle.CurrentSimulationTimeSeconds)
            {
                return Invalid(
                    "PowerSnapshotTransition.StateBinding.Stale",
                    "snapshot",
                    "The accepted snapshot must bind the current core version and exact simulation time.");
            }

            if (!lifecycle.HasSameBundleIdentitySet(inventory))
            {
                return Invalid(
                    "PowerSnapshotTransition.InventoryBinding.Stale",
                    "inventory",
                    "The accepted snapshot must bind the exact lifecycle topology, locations, and bundle identity set.");
            }

            if (!CompleteStateDigestV1.ComputeInventory(inventory).Equals(snapshot.InventoryDigest))
            {
                return Invalid(
                    "PowerSnapshotTransition.InventoryDigest.Stale",
                    "snapshot.inventory_digest",
                    "The accepted snapshot must bind the canonical complete inventory digest.");
            }

            if (!CompleteStateDigestV1.ComputeBurnupEnergy(inventory).Equals(snapshot.BurnupEnergyDigest))
            {
                return Invalid(
                    "PowerSnapshotTransition.BurnupEnergyDigest.Stale",
                    "snapshot.burnup_energy_digest",
                    "The accepted snapshot must bind the canonical burnup and energy digest.");
            }

            ContractValidationResult<VersionLifecycleV1> accepted = lifecycle.TryAcceptSpatialSolve(
                snapshot.CoreStateVersion,
                snapshot.BundleNuclideVersions,
                lifecycle.TopologyVersion,
                lifecycle.DataPackVersion,
                snapshot.TopologyDigest,
                snapshot.DataPackDigest,
                snapshot.StateDigest,
                snapshot.SpatialSolveId,
                snapshot.PowerSnapshotId,
                snapshot.CoefficientDigest,
                snapshot.SnapshotDigest,
                snapshot.SnapshotTimeSeconds,
                snapshot.InventoryDigest,
                snapshot.AcceptedInventoryDigest,
                snapshot.BurnupEnergyDigest);
            if (!accepted.IsValid)
            {
                return Invalid(
                    accepted.FirstDiagnostic.Code,
                    accepted.FirstDiagnostic.Path,
                    accepted.FirstDiagnostic.Message);
            }

            if (accepted.Value.SpatialStateVersion != snapshot.SpatialStateVersion ||
                accepted.Value.PowerSnapshotVersion != snapshot.PowerSnapshotVersion)
            {
                return Invalid(
                    "PowerSnapshotTransition.Version.Mismatch",
                    "snapshot.version",
                    "Snapshot spatial and power versions must equal the lifecycle post-commit versions.");
            }

            var entries = snapshot.Bundles.ToDictionary(entry => entry.BundleId, entry => entry);
            var proposed = new List<BundleState>(inventory.OccupiedCount);
            foreach (BundleState bundle in inventory.EnumerateOccupied()
                         .OrderBy(candidate => candidate.ChannelId.Value)
                         .ThenBy(candidate => candidate.Position.Value)
                         .ThenBy(candidate => candidate.BundleId))
            {
                if (bundle.NuclideState == null)
                {
                    return Invalid(
                        "PowerSnapshotTransition.NuclideState.Missing",
                        "bundle[" + bundle.BundleId + "]",
                        "An accepted full snapshot requires a complete I/Xe envelope on every live bundle.");
                }

                CompletePowerSnapshotBundleV1 entry;
                if (!entries.TryGetValue(bundle.BundleId, out entry) ||
                    entry.ChannelId != bundle.ChannelId || entry.Position != bundle.Position ||
                    entry.NuclideStateVersion != bundle.NuclideState.NuclideStateVersion)
                {
                    return Invalid(
                        "PowerSnapshotTransition.BundleBinding.Stale",
                        "bundle[" + bundle.BundleId + "]",
                        "Every snapshot entry must bind the current bundle location and nuclide version.");
                }

                ContractValidationResult<PowerHistoryRecordV1> history = PowerHistoryRecordV1.TryCreate(
                    snapshot.SnapshotTimeSeconds,
                    entry.PowerWatts,
                    snapshot.CoreStateVersion,
                    snapshot.SpatialStateVersion,
                    snapshot.PowerSnapshotVersion);
                if (!history.IsValid)
                {
                    return Invalid(history.FirstDiagnostic.Code, history.FirstDiagnostic.Path, history.FirstDiagnostic.Message);
                }

                ContractValidationResult<BundleState> updated = bundle.TryWithAcceptedPowerSnapshot(
                    snapshot.PowerSnapshotId,
                    history.Value,
                    entry.CoefficientBinding);
                if (!updated.IsValid)
                {
                    return Invalid(updated.FirstDiagnostic.Code, updated.FirstDiagnostic.Path, updated.FirstDiagnostic.Message);
                }

                proposed.Add(updated.Value);
            }

            if (entries.Count != proposed.Count)
            {
                return Invalid(
                    "PowerSnapshotTransition.BundleCount.Mismatch",
                    "snapshot.bundles",
                    "The accepted snapshot must contain exactly one entry per live bundle.");
            }

            ContractValidationResult<BundleInventory> resulting = BundleInventory.TryCreate(
                inventory.Topology,
                proposed);
            if (!resulting.IsValid)
            {
                return Invalid(
                    resulting.FirstDiagnostic.Code,
                    resulting.FirstDiagnostic.Path,
                    resulting.FirstDiagnostic.Message);
            }

            if (!CompleteStateDigestV1.ComputeInventory(resulting.Value)
                .Equals(snapshot.AcceptedInventoryDigest))
            {
                return Invalid(
                    "PowerSnapshotTransition.AcceptedInventoryDigest.Stale",
                    "snapshot.accepted_inventory_digest",
                    "The accepted snapshot must bind the full post-accept inventory, including every power-history record and current power/coefficient binding.");
            }

            return ContractValidationResult<CompletePowerSnapshotResultV1>.Valid(
                new CompletePowerSnapshotResultV1(resulting.Value, accepted.Value));
        }

        private static ContractValidationResult<CompletePowerSnapshotResultV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<CompletePowerSnapshotResultV1>.Invalid(code, path, message);
        }
    }

    /// <summary>
    /// Shared fixed-width canonical bytes used by the Phase 5 state/digest
    /// projections. Host paths, wall time, and unordered collection order are
    /// intentionally absent.
    /// </summary>
    internal static class Phase5CanonicalBytesV1
    {
        private static readonly Encoding Ascii = Encoding.ASCII;
        private static readonly Encoding Utf8 = new UTF8Encoding(false, true);

        public static byte[] Build(Action<BinaryWriter> write)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Utf8, true))
            {
                write(writer);
                writer.Flush();
                return stream.ToArray();
            }
        }

        public static byte[] HashBody(string magic, Action<BinaryWriter> write)
        {
            return HashBodyBytes(magic, Build(write));
        }

        public static byte[] HashBodyBytes(string magic, byte[] body)
        {
            return Sha256(Build(writer =>
            {
                WriteAscii(writer, magic);
                writer.Write((byte)0);
                WriteBytesRaw(writer, body);
            }));
        }

        public static byte[] Sha256(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return sha.ComputeHash(bytes);
            }
        }

        public static StableId DeriveUuidV8(string magic, Action<BinaryWriter> write)
        {
            byte[] digest = Sha256(Build(writer =>
            {
                WriteAscii(writer, magic);
                writer.Write((byte)0);
                write(writer);
            }));
            digest[6] = (byte)((digest[6] & 0x0f) | 0x80);
            digest[8] = (byte)((digest[8] & 0x3f) | 0x80);
            return StableIdFromCanonicalBytes(digest.Take(16).ToArray());
        }

        public static void WriteAscii(BinaryWriter writer, string value)
        {
            byte[] bytes = Ascii.GetBytes(value);
            writer.Write(bytes);
        }

        public static void WriteString(BinaryWriter writer, string value)
        {
            byte[] bytes = Utf8.GetBytes(value);
            WriteUInt32(writer, checked((uint)bytes.Length));
            WriteBytesRaw(writer, bytes);
        }

        public static void WriteBytes(BinaryWriter writer, byte[] bytes)
        {
            WriteUInt32(writer, checked((uint)bytes.Length));
            WriteBytesRaw(writer, bytes);
        }

        public static void WriteBytesRaw(BinaryWriter writer, byte[] bytes)
        {
            writer.Write(bytes);
        }

        public static void WriteStableId(BinaryWriter writer, StableId value)
        {
            writer.Write(value.ToCanonicalBytes());
        }

        public static void WriteDigest(BinaryWriter writer, Digest32 value)
        {
            writer.Write(value.ToArray());
        }

        public static void WriteUInt16(BinaryWriter writer, ushort value)
        {
            writer.Write(value);
        }

        public static void WriteUInt32(BinaryWriter writer, uint value)
        {
            writer.Write(value);
        }

        public static void WriteUInt64(BinaryWriter writer, ulong value)
        {
            writer.Write(value);
        }

        public static void WriteDouble(BinaryWriter writer, double value)
        {
            writer.Write(value);
        }

        public static void WriteOptionalUInt64(BinaryWriter writer, OptionalUInt64 value)
        {
            writer.Write(value.IsApplicable ? (byte)1 : (byte)0);
            if (value.IsApplicable)
            {
                writer.Write(value.Value);
            }
        }

        private static StableId StableIdFromCanonicalBytes(byte[] bytes)
        {
            if (bytes.Length != 16)
            {
                throw new ArgumentException("A canonical UUID requires 16 bytes.", nameof(bytes));
            }

            string hex = string.Concat(bytes.Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
            string text = hex.Substring(0, 8) + "-" + hex.Substring(8, 4) + "-" +
                          hex.Substring(12, 4) + "-" + hex.Substring(16, 4) + "-" + hex.Substring(20, 12);
            return StableId.Parse(text);
        }
    }
}
