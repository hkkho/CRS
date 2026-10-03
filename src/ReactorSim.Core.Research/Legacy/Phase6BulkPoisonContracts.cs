using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ReactorSim.Core
{
    /// <summary>
    /// Explicit bulk-moderator-poison branch modes. Disabled is serialized
    /// state, while Prescribed is setup-only and never advances over a
    /// positive-duration interval.
    /// </summary>
    public enum BulkPoisonModeV1 : byte
    {
        Disabled = 0,
        Add = 1,
        Withdraw = 2,
        Prescribed = 3
    }

    /// <summary>
    /// Immutable owner-bound synthetic bulk-poison state. Mass is
    /// authoritative; concentration is derived from the explicit moderator
    /// volume and is never independently evolved.
    /// </summary>
    public sealed class BulkPoisonStateV1
    {
        public const uint CurrentSchemaVersion = 1;
        public const string SchemaId = "CANDU-BULK-POISON-STATE-V1";

        private BulkPoisonStateV1(
            uint schemaVersion,
            StableId poisonSourceId,
            bool enabled,
            BulkPoisonModeV1 mode,
            double poisonMassKg,
            double moderatorVolumeM3,
            double poisonMassConcentrationKgPerM3,
            double referenceConcentrationKgPerM3,
            double addRateKgPerSecond,
            double withdrawRateKgPerSecond,
            StableId influenceMapId,
            string dataVersion,
            Digest32 influenceMapDigest,
            double updateTimeSeconds,
            Digest32 stateDigest)
        {
            SchemaVersion = schemaVersion;
            PoisonSourceId = poisonSourceId;
            Enabled = enabled;
            Mode = mode;
            PoisonMassKg = poisonMassKg;
            ModeratorVolumeM3 = moderatorVolumeM3;
            PoisonMassConcentrationKgPerM3 = poisonMassConcentrationKgPerM3;
            ReferenceConcentrationKgPerM3 = referenceConcentrationKgPerM3;
            AddRateKgPerSecond = addRateKgPerSecond;
            WithdrawRateKgPerSecond = withdrawRateKgPerSecond;
            InfluenceMapId = influenceMapId;
            DataVersion = dataVersion;
            InfluenceMapDigest = influenceMapDigest;
            UpdateTimeSeconds = updateTimeSeconds;
            StateDigest = stateDigest;
        }

        public uint SchemaVersion { get; }

        public StableId PoisonSourceId { get; }

        public bool Enabled { get; }

        public BulkPoisonModeV1 Mode { get; }

        public double PoisonMassKg { get; }

        public double ModeratorVolumeM3 { get; }

        public double PoisonMassConcentrationKgPerM3 { get; }

        public double ReferenceConcentrationKgPerM3 { get; }

        public double AddRateKgPerSecond { get; }

        public double WithdrawRateKgPerSecond { get; }

        public StableId InfluenceMapId { get; }

        public string DataVersion { get; }

        public Digest32 InfluenceMapDigest { get; }

        public double UpdateTimeSeconds { get; }

        public Digest32 StateDigest { get; }

        public static ContractValidationResult<BulkPoisonStateV1> TryCreate(
            uint schemaVersion,
            StableId poisonSourceId,
            bool enabled,
            BulkPoisonModeV1 mode,
            double poisonMassKg,
            double moderatorVolumeM3,
            double referenceConcentrationKgPerM3,
            double addRateKgPerSecond,
            double withdrawRateKgPerSecond,
            StableId influenceMapId,
            string? dataVersion,
            Digest32? influenceMapDigest,
            double updateTimeSeconds,
            Digest32? expectedStateDigest)
        {
            if (schemaVersion != CurrentSchemaVersion)
            {
                return Invalid(
                    "BulkPoisonState.SchemaVersion.Unsupported",
                    "schema_version",
                    "Only synthetic P6-T04 bulk-poison state schema version 1 is accepted.");
            }

            if (poisonSourceId != BulkPoisonInfluenceMapV1.ApprovedPoisonSourceId)
            {
                return Invalid(
                    "BulkPoisonState.SourceId.Unapproved",
                    "poison_source_id",
                    "The P6-T04 state requires the owner-approved synthetic poison source identity.");
            }

            if ((byte)mode > (byte)BulkPoisonModeV1.Prescribed)
            {
                return Invalid(
                    "BulkPoisonState.Mode.Unsupported",
                    "mode",
                    "The bulk-poison mode is not part of the approved v1 enum.");
            }

            if ((!enabled && mode != BulkPoisonModeV1.Disabled) ||
                (enabled && mode == BulkPoisonModeV1.Disabled))
            {
                return Invalid(
                    "BulkPoisonState.EnabledMode.Mismatch",
                    "enabled",
                    "Disabled state must use Disabled mode and active state must use an action or setup mode.");
            }

            if (!BulkPoisonValidationV1.IsCanonicalNonnegative(poisonMassKg))
            {
                return Invalid(
                    "BulkPoisonState.Mass.Invalid",
                    "poison_mass_kg",
                    "Poison mass must be finite, nonnegative kg without signed zero.");
            }

            if (!BulkPoisonValidationV1.IsCanonicalPositive(moderatorVolumeM3) ||
                moderatorVolumeM3 != BulkPoisonInfluenceMapV1.ApprovedModeratorVolumeM3)
            {
                return Invalid(
                    "BulkPoisonState.Volume.Unapproved",
                    "moderator_volume_m3",
                    "The approved synthetic fixture requires finite positive moderator volume exactly 5.0 m^3.");
            }

            if (!BulkPoisonValidationV1.IsCanonicalNonnegative(referenceConcentrationKgPerM3) ||
                referenceConcentrationKgPerM3 != BulkPoisonInfluenceMapV1.ApprovedReferenceConcentrationKgPerM3)
            {
                return Invalid(
                    "BulkPoisonState.ReferenceConcentration.Unapproved",
                    "reference_concentration_kg_per_m3",
                    "The approved synthetic reference concentration is exactly 0.0 kg/m^3.");
            }

            if (!BulkPoisonValidationV1.IsCanonicalNonnegative(addRateKgPerSecond) ||
                addRateKgPerSecond != BulkPoisonInfluenceMapV1.ApprovedAddRateKgPerSecond)
            {
                return Invalid(
                    "BulkPoisonState.AddRate.Unapproved",
                    "add_rate_kg_per_second",
                    "The approved synthetic add rate is exactly 0.1 kg/s.");
            }

            if (!BulkPoisonValidationV1.IsCanonicalNonnegative(withdrawRateKgPerSecond) ||
                withdrawRateKgPerSecond != BulkPoisonInfluenceMapV1.ApprovedWithdrawRateKgPerSecond)
            {
                return Invalid(
                    "BulkPoisonState.WithdrawRate.Unapproved",
                    "withdraw_rate_kg_per_second",
                    "The approved synthetic slow withdraw rate is exactly 0.01 kg/s.");
            }

            if (mode == BulkPoisonModeV1.Prescribed &&
                poisonMassKg > BulkPoisonInfluenceMapV1.ApprovedSetupMaximumMassKg)
            {
                return Invalid(
                    "BulkPoisonState.SetupMass.OutOfBounds",
                    "poison_mass_kg",
                    "Prescribed setup mass may not exceed the approved synthetic 1.0 kg limit.");
            }

            if (influenceMapId != BulkPoisonInfluenceMapV1.ApprovedMapId)
            {
                return Invalid(
                    "BulkPoisonState.MapId.Unapproved",
                    "influence_map_id",
                    "The state must bind the owner-approved synthetic P6-T04 map identity.");
            }

            if (!string.Equals(
                    dataVersion,
                    BulkPoisonInfluenceMapV1.ApprovedDataVersion,
                    StringComparison.Ordinal))
            {
                return Invalid(
                    "BulkPoisonState.DataVersion.Unapproved",
                    "data_version",
                    "The state must bind the owner-approved synthetic P6-T04 data version.");
            }

            if (influenceMapDigest == null)
            {
                return Invalid(
                    "BulkPoisonState.MapDigest.Missing",
                    "influence_map_digest",
                    "The state must bind a complete map digest.");
            }

            if (!BulkPoisonValidationV1.IsCanonicalNonnegative(updateTimeSeconds))
            {
                return Invalid(
                    "BulkPoisonState.UpdateTime.Invalid",
                    "update_time_s",
                    "Update time must be finite, nonnegative SI seconds without signed zero.");
            }

            double concentration = poisonMassKg / moderatorVolumeM3;
            if (!BulkPoisonValidationV1.IsCanonicalNonnegative(concentration))
            {
                return Invalid(
                    "BulkPoisonState.Concentration.NonFinite",
                    "poison_mass_concentration_kg_per_m3",
                    "Derived poison concentration must be finite and nonnegative.");
            }

            if (expectedStateDigest == null)
            {
                return Invalid(
                    "BulkPoisonState.StateDigest.Missing",
                    "state_digest",
                    "The complete state digest is required and may not be inferred by a consumer.");
            }

            Digest32 calculatedDigest = ComputeDigest(
                schemaVersion,
                poisonSourceId,
                enabled,
                mode,
                poisonMassKg,
                moderatorVolumeM3,
                concentration,
                referenceConcentrationKgPerM3,
                addRateKgPerSecond,
                withdrawRateKgPerSecond,
                influenceMapId,
                dataVersion!,
                influenceMapDigest,
                updateTimeSeconds);
            if (!expectedStateDigest.Equals(calculatedDigest))
            {
                return Invalid(
                    "BulkPoisonState.StateDigest.Mismatch",
                    "state_digest",
                    "The supplied state digest does not equal the canonical state fields.");
            }

            return ContractValidationResult<BulkPoisonStateV1>.Valid(
                new BulkPoisonStateV1(
                    schemaVersion,
                    poisonSourceId,
                    enabled,
                    mode,
                    poisonMassKg,
                    moderatorVolumeM3,
                    concentration,
                    referenceConcentrationKgPerM3,
                    addRateKgPerSecond,
                    withdrawRateKgPerSecond,
                    influenceMapId,
                    dataVersion!,
                    influenceMapDigest,
                    updateTimeSeconds,
                    expectedStateDigest));
        }

        public static Digest32 ComputeDigest(
            uint schemaVersion,
            StableId poisonSourceId,
            bool enabled,
            BulkPoisonModeV1 mode,
            double poisonMassKg,
            double moderatorVolumeM3,
            double poisonMassConcentrationKgPerM3,
            double referenceConcentrationKgPerM3,
            double addRateKgPerSecond,
            double withdrawRateKgPerSecond,
            StableId influenceMapId,
            string dataVersion,
            Digest32 influenceMapDigest,
            double updateTimeSeconds)
        {
            if (dataVersion == null)
            {
                throw new ArgumentNullException(nameof(dataVersion));
            }

            if (influenceMapDigest == null)
            {
                throw new ArgumentNullException(nameof(influenceMapDigest));
            }

            return new Digest32(
                Phase5CanonicalBytesV1.Sha256(
                    BuildBytes(
                        schemaVersion,
                        poisonSourceId,
                        enabled,
                        mode,
                        poisonMassKg,
                        moderatorVolumeM3,
                        poisonMassConcentrationKgPerM3,
                        referenceConcentrationKgPerM3,
                        addRateKgPerSecond,
                        withdrawRateKgPerSecond,
                        influenceMapId,
                        dataVersion,
                        influenceMapDigest,
                        updateTimeSeconds,
                        null)));
        }

        public byte[] ToCanonicalBytes()
        {
            return BuildBytes(
                SchemaVersion,
                PoisonSourceId,
                Enabled,
                Mode,
                PoisonMassKg,
                ModeratorVolumeM3,
                PoisonMassConcentrationKgPerM3,
                ReferenceConcentrationKgPerM3,
                AddRateKgPerSecond,
                WithdrawRateKgPerSecond,
                InfluenceMapId,
                DataVersion,
                InfluenceMapDigest,
                UpdateTimeSeconds,
                StateDigest);
        }

        private static byte[] BuildBytes(
            uint schemaVersion,
            StableId poisonSourceId,
            bool enabled,
            BulkPoisonModeV1 mode,
            double poisonMassKg,
            double moderatorVolumeM3,
            double poisonMassConcentrationKgPerM3,
            double referenceConcentrationKgPerM3,
            double addRateKgPerSecond,
            double withdrawRateKgPerSecond,
            StableId influenceMapId,
            string dataVersion,
            Digest32 influenceMapDigest,
            double updateTimeSeconds,
            Digest32? stateDigest)
        {
            return Phase5CanonicalBytesV1.Build(writer =>
            {
                Phase5CanonicalBytesV1.WriteAscii(writer, SchemaId);
                writer.Write((byte)0);
                Phase5CanonicalBytesV1.WriteUInt32(writer, schemaVersion);
                Phase5CanonicalBytesV1.WriteStableId(writer, poisonSourceId);
                writer.Write(enabled ? (byte)1 : (byte)0);
                writer.Write((byte)mode);
                Phase5CanonicalBytesV1.WriteDouble(writer, poisonMassKg);
                Phase5CanonicalBytesV1.WriteDouble(writer, moderatorVolumeM3);
                Phase5CanonicalBytesV1.WriteDouble(writer, poisonMassConcentrationKgPerM3);
                Phase5CanonicalBytesV1.WriteDouble(writer, referenceConcentrationKgPerM3);
                Phase5CanonicalBytesV1.WriteDouble(writer, addRateKgPerSecond);
                Phase5CanonicalBytesV1.WriteDouble(writer, withdrawRateKgPerSecond);
                Phase5CanonicalBytesV1.WriteStableId(writer, influenceMapId);
                Phase5CanonicalBytesV1.WriteString(writer, dataVersion);
                Phase5CanonicalBytesV1.WriteDigest(writer, influenceMapDigest);
                Phase5CanonicalBytesV1.WriteDouble(writer, updateTimeSeconds);
                if (stateDigest != null)
                {
                    Phase5CanonicalBytesV1.WriteDigest(writer, stateDigest);
                }
            });
        }

        private static ContractValidationResult<BulkPoisonStateV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<BulkPoisonStateV1>.Invalid(code, path, message);
        }
    }

    /// <summary>
    /// Pure mass-accounting projection. It never mutates the supplied state;
    /// queue allocation/transition/rollback and scenario application remain
    /// outside P6-T04.
    /// </summary>
    public static class BulkPoisonAccountingV1
    {
        public static ContractValidationResult<BulkPoisonActionResultV1> TryAdvance(
            BulkPoisonStateV1? state,
            double currentTimeSeconds)
        {
            if (state == null)
            {
                return Invalid(
                    "BulkPoisonAccounting.State.Missing",
                    "state",
                    "A validated bulk-poison state is required.");
            }

            if (!BulkPoisonValidationV1.IsCanonicalNonnegative(currentTimeSeconds))
            {
                return Invalid(
                    "BulkPoisonAccounting.Time.Invalid",
                    "current_time_s",
                    "Current time must be finite, nonnegative SI seconds without signed zero.");
            }

            if (currentTimeSeconds < state.UpdateTimeSeconds)
            {
                return Invalid(
                    "BulkPoisonAccounting.Time.Order",
                    "current_time_s",
                    "Current time may not precede the state's update time.");
            }

            double elapsedSeconds = currentTimeSeconds - state.UpdateTimeSeconds;
            if (!BulkPoisonValidationV1.IsCanonicalNonnegative(elapsedSeconds))
            {
                return Invalid(
                    "BulkPoisonAccounting.Elapsed.NonFinite",
                    "elapsed_s",
                    "Elapsed time must remain finite and nonnegative.");
            }

            if (state.Mode == BulkPoisonModeV1.Prescribed && elapsedSeconds > 0.0)
            {
                return Invalid(
                    "BulkPoisonAccounting.PrescribedPositiveDuration",
                    "mode",
                    "Prescribed poison state is setup-only and cannot advance over positive duration.");
            }

            double requestedDeltaKg = 0.0;
            double appliedDeltaKg = 0.0;
            if (state.Enabled && state.Mode == BulkPoisonModeV1.Add)
            {
                requestedDeltaKg = state.AddRateKgPerSecond * elapsedSeconds;
                if (!BulkPoisonValidationV1.IsCanonicalNonnegative(requestedDeltaKg))
                {
                    return Invalid(
                        "BulkPoisonAccounting.Add.NonFinite",
                        "requested_mass_delta_kg",
                        "Add mass must be finite and nonnegative.");
                }

                appliedDeltaKg = requestedDeltaKg;
            }
            else if (state.Enabled && state.Mode == BulkPoisonModeV1.Withdraw)
            {
                double requestedWithdrawalKg = state.WithdrawRateKgPerSecond * elapsedSeconds;
                if (!BulkPoisonValidationV1.IsCanonicalNonnegative(requestedWithdrawalKg))
                {
                    return Invalid(
                        "BulkPoisonAccounting.Withdraw.NonFinite",
                        "requested_mass_delta_kg",
                        "Withdraw mass must be finite and nonnegative before applying its negative sign.");
                }

                requestedDeltaKg = BulkPoisonValidationV1.NormalizeZero(-requestedWithdrawalKg);
                appliedDeltaKg = BulkPoisonValidationV1.NormalizeZero(
                    -Math.Min(requestedWithdrawalKg, state.PoisonMassKg));
            }

            double massAfterKg = state.PoisonMassKg + appliedDeltaKg;
            if (!BulkPoisonValidationV1.IsCanonicalNonnegative(massAfterKg))
            {
                return Invalid(
                    "BulkPoisonAccounting.MassAfter.Invalid",
                    "poison_mass_after_kg",
                    "Mass accounting must remain finite and nonnegative without clamping an invalid result.");
            }

            double concentrationAfter = massAfterKg / state.ModeratorVolumeM3;
            if (!BulkPoisonValidationV1.IsCanonicalNonnegative(concentrationAfter))
            {
                return Invalid(
                    "BulkPoisonAccounting.ConcentrationAfter.NonFinite",
                    "poison_mass_concentration_after_kg_per_m3",
                    "The derived post-action concentration must remain finite and nonnegative.");
            }

            return ContractValidationResult<BulkPoisonActionResultV1>.Valid(
                BulkPoisonActionResultV1.Create(
                    state,
                    currentTimeSeconds,
                    elapsedSeconds,
                    requestedDeltaKg,
                    appliedDeltaKg,
                    massAfterKg,
                    concentrationAfter));
        }

        private static ContractValidationResult<BulkPoisonActionResultV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<BulkPoisonActionResultV1>.Invalid(code, path, message);
        }
    }

    /// <summary>
    /// Immutable audit record for one pure add/withdraw/setup projection.
    /// </summary>
    public sealed class BulkPoisonActionResultV1
    {
        public const uint CurrentSchemaVersion = 1;
        public const string SchemaId = "CANDU-BULK-POISON-ACTION-V1";

        private BulkPoisonActionResultV1(
            StableId poisonSourceId,
            BulkPoisonModeV1 mode,
            bool enabled,
            double poisonMassBeforeKg,
            double poisonMassAfterKg,
            double concentrationBeforeKgPerM3,
            double concentrationAfterKgPerM3,
            double requestedMassDeltaKg,
            double appliedMassDeltaKg,
            double elapsedSeconds,
            double currentTimeSeconds,
            Digest32 mapDigest,
            Digest32 sourceStateDigest,
            Digest32 actionDigest)
        {
            PoisonSourceId = poisonSourceId;
            Mode = mode;
            Enabled = enabled;
            PoisonMassBeforeKg = poisonMassBeforeKg;
            PoisonMassAfterKg = poisonMassAfterKg;
            ConcentrationBeforeKgPerM3 = concentrationBeforeKgPerM3;
            ConcentrationAfterKgPerM3 = concentrationAfterKgPerM3;
            RequestedMassDeltaKg = requestedMassDeltaKg;
            AppliedMassDeltaKg = appliedMassDeltaKg;
            ElapsedSeconds = elapsedSeconds;
            CurrentTimeSeconds = currentTimeSeconds;
            MapDigest = mapDigest;
            SourceStateDigest = sourceStateDigest;
            ActionDigest = actionDigest;
        }

        public StableId PoisonSourceId { get; }

        public BulkPoisonModeV1 Mode { get; }

        public bool Enabled { get; }

        public double PoisonMassBeforeKg { get; }

        public double PoisonMassAfterKg { get; }

        public double ConcentrationBeforeKgPerM3 { get; }

        public double ConcentrationAfterKgPerM3 { get; }

        public double RequestedMassDeltaKg { get; }

        public double AppliedMassDeltaKg { get; }

        public double ElapsedSeconds { get; }

        public double CurrentTimeSeconds { get; }

        public Digest32 MapDigest { get; }

        public Digest32 SourceStateDigest { get; }

        public Digest32 ActionDigest { get; }

        internal static BulkPoisonActionResultV1 Create(
            BulkPoisonStateV1 state,
            double currentTimeSeconds,
            double elapsedSeconds,
            double requestedMassDeltaKg,
            double appliedMassDeltaKg,
            double poisonMassAfterKg,
            double concentrationAfterKgPerM3)
        {
            byte[] body = BuildBytes(
                state.PoisonSourceId,
                state.Mode,
                state.Enabled,
                state.PoisonMassKg,
                poisonMassAfterKg,
                state.PoisonMassConcentrationKgPerM3,
                concentrationAfterKgPerM3,
                requestedMassDeltaKg,
                appliedMassDeltaKg,
                elapsedSeconds,
                currentTimeSeconds,
                state.InfluenceMapDigest,
                state.StateDigest,
                null);
            Digest32 actionDigest = new Digest32(Phase5CanonicalBytesV1.Sha256(body));
            return new BulkPoisonActionResultV1(
                state.PoisonSourceId,
                state.Mode,
                state.Enabled,
                state.PoisonMassKg,
                poisonMassAfterKg,
                state.PoisonMassConcentrationKgPerM3,
                concentrationAfterKgPerM3,
                requestedMassDeltaKg,
                appliedMassDeltaKg,
                elapsedSeconds,
                currentTimeSeconds,
                state.InfluenceMapDigest,
                state.StateDigest,
                actionDigest);
        }

        public byte[] ToCanonicalBytes()
        {
            return BuildBytes(
                PoisonSourceId,
                Mode,
                Enabled,
                PoisonMassBeforeKg,
                PoisonMassAfterKg,
                ConcentrationBeforeKgPerM3,
                ConcentrationAfterKgPerM3,
                RequestedMassDeltaKg,
                AppliedMassDeltaKg,
                ElapsedSeconds,
                CurrentTimeSeconds,
                MapDigest,
                SourceStateDigest,
                ActionDigest);
        }

        private static byte[] BuildBytes(
            StableId poisonSourceId,
            BulkPoisonModeV1 mode,
            bool enabled,
            double poisonMassBeforeKg,
            double poisonMassAfterKg,
            double concentrationBeforeKgPerM3,
            double concentrationAfterKgPerM3,
            double requestedMassDeltaKg,
            double appliedMassDeltaKg,
            double elapsedSeconds,
            double currentTimeSeconds,
            Digest32 mapDigest,
            Digest32 sourceStateDigest,
            Digest32? actionDigest)
        {
            return Phase5CanonicalBytesV1.Build(writer =>
            {
                Phase5CanonicalBytesV1.WriteAscii(writer, SchemaId);
                writer.Write((byte)0);
                Phase5CanonicalBytesV1.WriteUInt32(writer, CurrentSchemaVersion);
                Phase5CanonicalBytesV1.WriteStableId(writer, poisonSourceId);
                writer.Write((byte)mode);
                writer.Write(enabled ? (byte)1 : (byte)0);
                Phase5CanonicalBytesV1.WriteDouble(writer, poisonMassBeforeKg);
                Phase5CanonicalBytesV1.WriteDouble(writer, poisonMassAfterKg);
                Phase5CanonicalBytesV1.WriteDouble(writer, concentrationBeforeKgPerM3);
                Phase5CanonicalBytesV1.WriteDouble(writer, concentrationAfterKgPerM3);
                Phase5CanonicalBytesV1.WriteDouble(writer, requestedMassDeltaKg);
                Phase5CanonicalBytesV1.WriteDouble(writer, appliedMassDeltaKg);
                Phase5CanonicalBytesV1.WriteDouble(writer, elapsedSeconds);
                Phase5CanonicalBytesV1.WriteDouble(writer, currentTimeSeconds);
                Phase5CanonicalBytesV1.WriteDigest(writer, mapDigest);
                Phase5CanonicalBytesV1.WriteDigest(writer, sourceStateDigest);
                if (actionDigest != null)
                {
                    Phase5CanonicalBytesV1.WriteDigest(writer, actionDigest);
                }
            });
        }
    }

    internal static class BulkPoisonValidationV1
    {
        public static bool IsCanonicalNonnegative(double value)
        {
            return ContractValidation.IsFinite(value) && value >= 0.0 &&
                   BitConverter.DoubleToInt64Bits(value) >= 0;
        }

        public static bool IsCanonicalPositive(double value)
        {
            return ContractValidation.IsFinite(value) && value > 0.0 &&
                   BitConverter.DoubleToInt64Bits(value) >= 0;
        }

        public static bool IsCanonicalText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            try
            {
                _ = new UTF8Encoding(false, true).GetBytes(value);
                return true;
            }
            catch (EncoderFallbackException)
            {
                return false;
            }
        }

        public static double NormalizeZero(double value)
        {
            return value == 0.0 ? 0.0 : value;
        }
    }
}
