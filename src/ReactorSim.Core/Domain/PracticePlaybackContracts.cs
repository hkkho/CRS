namespace ReactorSim.Core
{
    /// <summary>
    /// The approved Phase 8 presentation pacing authority. It supplies wall
    /// time only as an explicit client request; it never becomes a physics
    /// timestep or a replacement for the existing stable integration policy.
    /// </summary>
    public sealed class Phase8TimeModelV1
    {
        private Phase8TimeModelV1(
            uint wallControlTickMilliseconds,
            double maximumPresentationAdvancePerWallTickSeconds,
            double defaultAccelerationFactor,
            double maximumAccelerationFactor)
        {
            WallControlTickMilliseconds = wallControlTickMilliseconds;
            MaximumPresentationAdvancePerWallTickSeconds = maximumPresentationAdvancePerWallTickSeconds;
            DefaultAccelerationFactor = defaultAccelerationFactor;
            MaximumAccelerationFactor = maximumAccelerationFactor;
        }

        public uint WallControlTickMilliseconds { get; }

        public double WallControlTickSeconds
        {
            get { return WallControlTickMilliseconds / 1000.0; }
        }

        public double MaximumPresentationAdvancePerWallTickSeconds { get; }

        public double DefaultAccelerationFactor { get; }

        public double MaximumAccelerationFactor { get; }

        public static ContractValidationResult<Phase8TimeModelV1> TryCreate(
            uint wallControlTickMilliseconds,
            double maximumPresentationAdvancePerWallTickSeconds,
            double defaultAccelerationFactor,
            double maximumAccelerationFactor,
            bool wallClockMustNotDrivePhysics)
        {
            if (wallControlTickMilliseconds == 0)
            {
                return ContractValidationResult<Phase8TimeModelV1>.Invalid(
                    "Phase8TimeModel.WallTick.Invalid",
                    "wall_control_tick_ms",
                    "The wall control tick must be a positive number of milliseconds.");
            }

            if (!ContractValidation.IsFinite(maximumPresentationAdvancePerWallTickSeconds) ||
                maximumPresentationAdvancePerWallTickSeconds <= 0)
            {
                return ContractValidationResult<Phase8TimeModelV1>.Invalid(
                    "Phase8TimeModel.PresentationCap.Invalid",
                    "maximum_presentation_advance_per_wall_tick_s",
                    "The presentation advance cap must be finite and strictly positive SI seconds.");
            }

            if (!ContractValidation.IsFinite(defaultAccelerationFactor) || defaultAccelerationFactor <= 0)
            {
                return ContractValidationResult<Phase8TimeModelV1>.Invalid(
                    "Phase8TimeModel.DefaultFactor.Invalid",
                    "default_acceleration_factor_simulation_seconds_per_wall_second",
                    "The default acceleration factor must be finite and strictly positive.");
            }

            if (!ContractValidation.IsFinite(maximumAccelerationFactor) ||
                maximumAccelerationFactor < defaultAccelerationFactor)
            {
                return ContractValidationResult<Phase8TimeModelV1>.Invalid(
                    "Phase8TimeModel.MaximumFactor.Invalid",
                    "maximum_acceleration_factor_simulation_seconds_per_wall_second",
                    "The maximum acceleration factor must be finite and at least the default factor.");
            }

            if (!wallClockMustNotDrivePhysics)
            {
                return ContractValidationResult<Phase8TimeModelV1>.Invalid(
                    "Phase8TimeModel.WallClockBoundary.Invalid",
                    "wall_clock_must_not_drive_physics",
                    "Wall-clock pacing must not become an implicit physics input.");
            }

            double tickSeconds = wallControlTickMilliseconds / 1000.0;
            if (defaultAccelerationFactor * tickSeconds >
                maximumPresentationAdvancePerWallTickSeconds + 1e-12 ||
                maximumAccelerationFactor * tickSeconds >
                maximumPresentationAdvancePerWallTickSeconds + 1e-12)
            {
                return ContractValidationResult<Phase8TimeModelV1>.Invalid(
                    "Phase8TimeModel.PresentationCap.Exceeded",
                    "maximum_presentation_advance_per_wall_tick_s",
                    "Every approved acceleration factor must fit within the per-tick presentation cap.");
            }

            return ContractValidationResult<Phase8TimeModelV1>.Valid(
                new Phase8TimeModelV1(
                    wallControlTickMilliseconds,
                    maximumPresentationAdvancePerWallTickSeconds,
                    defaultAccelerationFactor,
                    maximumAccelerationFactor));
        }
    }

    public sealed class Phase8PlaybackModeV1
    {
        private Phase8PlaybackModeV1(string modeId, double accelerationFactor)
        {
            ModeId = modeId;
            AccelerationFactor = accelerationFactor;
        }

        public string ModeId { get; }

        public double AccelerationFactor { get; }

        public static ContractValidationResult<Phase8PlaybackModeV1> TryCreate(
            string modeId,
            double accelerationFactor,
            Phase8TimeModelV1 timeModel)
        {
            if (string.IsNullOrWhiteSpace(modeId))
            {
                return ContractValidationResult<Phase8PlaybackModeV1>.Invalid(
                    "Phase8PlaybackMode.Id.Missing",
                    "mode_id",
                    "A playback mode requires a stable nonempty identifier.");
            }

            if (timeModel == null)
            {
                return ContractValidationResult<Phase8PlaybackModeV1>.Invalid(
                    "Phase8PlaybackMode.TimeModel.Missing",
                    "time_model",
                    "A playback mode requires the approved time model.");
            }

            if (!ContractValidation.IsFinite(accelerationFactor) ||
                accelerationFactor <= 0 ||
                accelerationFactor > timeModel.MaximumAccelerationFactor)
            {
                return ContractValidationResult<Phase8PlaybackModeV1>.Invalid(
                    "Phase8PlaybackMode.Factor.Invalid",
                    "acceleration_factor_simulation_seconds_per_wall_second",
                    "The playback factor must be finite, positive, and no greater than the approved maximum.");
            }

            if (accelerationFactor * timeModel.WallControlTickSeconds >
                timeModel.MaximumPresentationAdvancePerWallTickSeconds + 1e-12)
            {
                return ContractValidationResult<Phase8PlaybackModeV1>.Invalid(
                    "Phase8PlaybackMode.PresentationCap.Exceeded",
                    "acceleration_factor_simulation_seconds_per_wall_second",
                    "The playback factor exceeds the approved per-tick presentation cap.");
            }

            return ContractValidationResult<Phase8PlaybackModeV1>.Valid(
                new Phase8PlaybackModeV1(modeId, accelerationFactor));
        }
    }
}
