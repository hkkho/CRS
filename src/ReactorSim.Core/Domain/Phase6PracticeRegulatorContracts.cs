using System;

namespace ReactorSim.Core
{
    /// <summary>
    /// Identity and defaults for the small synthetic controller used by the
    /// playable practice loop. This is deliberately narrower than the
    /// industrial-shaped P6 RRS fixture: it compensates one scalar static
    /// reactivity value and does not pretend to model plant devices.
    /// </summary>
    public static class SyntheticPracticeRegulatorIdentityV1
    {
        public const uint CurrentSchemaVersion = 1;
        public const string ControllerIdentity = "synthetic-practice-regulator-v1";
        public const string CadenceIdentity = "deterministic-regulated-steady-state-long-step-v1";
        public const double DefaultResponseTimeSeconds = 4.0;
        public const double MinimumResponseTimeSeconds = 2.0;
        public const double MaximumResponseTimeSeconds = 5.0;
        public const double DefaultLowerBound = -0.25;
        public const double DefaultUpperBound = 0.25;
    }

    /// <summary>
    /// Immutable deterministic scalar compensation state for the synthetic
    /// practice loop. Core reactivity is the static-eigenmode solve result;
    /// compensated net reactivity is the sum of that value and the actuator
    /// state. A transition never mutates its input, which makes preview and
    /// failed candidate paths safe to discard.
    /// </summary>
    public sealed class SyntheticPracticeRegulatorV1
    {
        private readonly uint _schemaVersion =
            SyntheticPracticeRegulatorIdentityV1.CurrentSchemaVersion;
        private readonly string _controllerIdentity =
            SyntheticPracticeRegulatorIdentityV1.ControllerIdentity;
        private readonly string _cadenceIdentity =
            SyntheticPracticeRegulatorIdentityV1.CadenceIdentity;

        private SyntheticPracticeRegulatorV1(
            double coreReactivity,
            double compensatedNetReactivity,
            double compensationState,
            double compensationCommand,
            double lowerBound,
            double upperBound,
            bool compensationSaturated,
            double responseTimeSeconds,
            double simulationTimeSeconds)
        {
            CoreReactivity = coreReactivity;
            CompensatedNetReactivity = compensatedNetReactivity;
            CompensationState = compensationState;
            CompensationCommand = compensationCommand;
            LowerBound = lowerBound;
            UpperBound = upperBound;
            CompensationSaturated = compensationSaturated;
            ResponseTimeSeconds = responseTimeSeconds;
            SimulationTimeSeconds = simulationTimeSeconds;
        }

        public uint SchemaVersion
        {
            get { return _schemaVersion; }
        }

        public string ControllerIdentity
        {
            get { return _controllerIdentity; }
        }

        public string CadenceIdentity
        {
            get { return _cadenceIdentity; }
        }

        public double CoreReactivity { get; }

        public double CompensatedNetReactivity { get; }

        /// <summary>
        /// The bounded actuator state, expressed as a reactivity
        /// compensation. It is separate from the command so actuator motion
        /// remains visible during the short response interval.
        /// </summary>
        public double CompensationState { get; }

        /// <summary>
        /// The bounded controller command. A saturated command is held at the
        /// nearest explicit actuator bound.
        /// </summary>
        public double CompensationCommand { get; }

        public double LowerBound { get; }

        public double UpperBound { get; }

        public bool CompensationSaturated { get; }

        public double ResponseTimeSeconds { get; }

        public double SimulationTimeSeconds { get; }

        public static ContractValidationResult<SyntheticPracticeRegulatorV1> TryCreate(
            double coreReactivity,
            double simulationTimeSeconds = 0.0)
        {
            return TryCreate(
                coreReactivity,
                simulationTimeSeconds,
                SyntheticPracticeRegulatorIdentityV1.DefaultResponseTimeSeconds,
                SyntheticPracticeRegulatorIdentityV1.DefaultLowerBound,
                SyntheticPracticeRegulatorIdentityV1.DefaultUpperBound);
        }

        public static ContractValidationResult<SyntheticPracticeRegulatorV1> TryCreate(
            double coreReactivity,
            double simulationTimeSeconds,
            double responseTimeSeconds,
            double lowerBound,
            double upperBound)
        {
            ContractValidationResult<bool> inputs = ValidateInputs(
                coreReactivity,
                simulationTimeSeconds,
                responseTimeSeconds,
                lowerBound,
                upperBound);
            if (!inputs.IsValid)
            {
                return Invalid(
                    inputs.FirstDiagnostic.Code,
                    inputs.FirstDiagnostic.Path,
                    inputs.FirstDiagnostic.Message);
            }

            double requestedCommand = -coreReactivity;
            double boundedCommand = Clamp(requestedCommand, lowerBound, upperBound);
            bool saturated = requestedCommand != boundedCommand;
            double net = coreReactivity + boundedCommand;
            if (!ContractValidation.IsFinite(net))
            {
                return Invalid(
                    "SyntheticPracticeRegulator.InitialState.NonFinite",
                    "compensated_net_reactivity",
                    "The initial compensated net reactivity must be finite.");
            }

            return ContractValidationResult<SyntheticPracticeRegulatorV1>.Valid(
                new SyntheticPracticeRegulatorV1(
                    coreReactivity,
                    net,
                    boundedCommand,
                    boundedCommand,
                    lowerBound,
                    upperBound,
                    saturated,
                    responseTimeSeconds,
                    simulationTimeSeconds));
        }

        /// <summary>
        /// Rebinds the solved core reactivity without moving the actuator.
        /// This is used immediately after a committed refuelling or scheduled
        /// shape solve; the next timed transition supplies the declared
        /// actuator response.
        /// </summary>
        public ContractValidationResult<SyntheticPracticeRegulatorV1> TryBindCoreReactivity(
            double coreReactivity,
            double simulationTimeSeconds)
        {
            if (!ContractValidation.IsFinite(coreReactivity) ||
                !ContractValidation.IsFinite(simulationTimeSeconds) ||
                simulationTimeSeconds < 0.0 ||
                !AreSameSimulationTime(simulationTimeSeconds, SimulationTimeSeconds))
            {
                return Invalid(
                    "SyntheticPracticeRegulator.Binding.Invalid",
                    "core_reactivity",
                    "A core reactivity binding must be finite and use the regulator's current simulation time.");
            }

            double requestedCommand = -coreReactivity;
            double boundedCommand = Clamp(requestedCommand, LowerBound, UpperBound);
            double net = coreReactivity + CompensationState;
            if (!ContractValidation.IsFinite(net))
            {
                return Invalid(
                    "SyntheticPracticeRegulator.Binding.NonFinite",
                    "compensated_net_reactivity",
                    "A core reactivity binding must leave a finite compensated net reactivity.");
            }

            return ContractValidationResult<SyntheticPracticeRegulatorV1>.Valid(
                new SyntheticPracticeRegulatorV1(
                    coreReactivity,
                    net,
                    CompensationState,
                    boundedCommand,
                    LowerBound,
                    UpperBound,
                    requestedCommand != boundedCommand,
                    ResponseTimeSeconds,
                    SimulationTimeSeconds));
        }

        /// <summary>
        /// Advances the bounded actuator to a later shared simulation time.
        /// The exact first-order update is partition-stable for a constant
        /// core reactivity and makes the 2–5 second response explicit without
        /// claiming sub-second transient fidelity for the practice loop.
        /// </summary>
        public ContractValidationResult<SyntheticPracticeRegulatorV1> TryAdvance(
            double coreReactivity,
            double simulationTimeSeconds)
        {
            if (!ContractValidation.IsFinite(coreReactivity) ||
                !ContractValidation.IsFinite(simulationTimeSeconds) ||
                simulationTimeSeconds <= SimulationTimeSeconds)
            {
                return Invalid(
                    "SyntheticPracticeRegulator.Advance.TimeInvalid",
                    "simulation_time_s",
                    "A regulator advance requires a finite simulation time later than the current state.");
            }

            double deltaTimeSeconds = simulationTimeSeconds - SimulationTimeSeconds;
            if (!ContractValidation.IsFinite(deltaTimeSeconds) || deltaTimeSeconds <= 0.0)
            {
                return Invalid(
                    "SyntheticPracticeRegulator.Advance.DeltaInvalid",
                    "delta_time_s",
                    "The regulator time interval must be finite and strictly positive.");
            }

            double requestedCommand = -coreReactivity;
            double boundedCommand = Clamp(requestedCommand, LowerBound, UpperBound);
            double decay = Math.Exp(-deltaTimeSeconds / ResponseTimeSeconds);
            double response = 1.0 - decay;
            double nextState = CompensationState +
                (boundedCommand - CompensationState) * response;
            double net = coreReactivity + nextState;
            if (!ContractValidation.IsFinite(nextState) ||
                nextState < LowerBound ||
                nextState > UpperBound ||
                !ContractValidation.IsFinite(net))
            {
                return Invalid(
                    "SyntheticPracticeRegulator.Advance.NonFinite",
                    "state",
                    "The bounded actuator update must remain finite and within its explicit bounds.");
            }

            return ContractValidationResult<SyntheticPracticeRegulatorV1>.Valid(
                new SyntheticPracticeRegulatorV1(
                    coreReactivity,
                    net,
                    nextState,
                    boundedCommand,
                    LowerBound,
                    UpperBound,
                    requestedCommand != boundedCommand,
                    ResponseTimeSeconds,
                    simulationTimeSeconds));
        }

        /// <summary>
        /// Compatibility alias for callers that describe the state in its
        /// physical units rather than as an actuator state.
        /// </summary>
        public double CompensationReactivity
        {
            get { return CompensationState; }
        }

        private static ContractValidationResult<bool> ValidateInputs(
            double coreReactivity,
            double simulationTimeSeconds,
            double responseTimeSeconds,
            double lowerBound,
            double upperBound)
        {
            if (!ContractValidation.IsFinite(coreReactivity))
            {
                return ContractValidationResult<bool>.Invalid(
                    "SyntheticPracticeRegulator.CoreReactivity.NonFinite",
                    "core_reactivity",
                    "Core reactivity must be finite.");
            }

            if (!ContractValidation.IsFinite(simulationTimeSeconds) ||
                simulationTimeSeconds < 0.0)
            {
                return ContractValidationResult<bool>.Invalid(
                    "SyntheticPracticeRegulator.Time.Invalid",
                    "simulation_time_s",
                    "The regulator simulation time must be finite and nonnegative.");
            }

            if (!ContractValidation.IsFinite(responseTimeSeconds) ||
                responseTimeSeconds < SyntheticPracticeRegulatorIdentityV1.MinimumResponseTimeSeconds ||
                responseTimeSeconds > SyntheticPracticeRegulatorIdentityV1.MaximumResponseTimeSeconds)
            {
                return ContractValidationResult<bool>.Invalid(
                    "SyntheticPracticeRegulator.ResponseTime.Invalid",
                    "response_time_s",
                    "The practice actuator response time must be within the approved 2–5 second range.");
            }

            if (!ContractValidation.IsFinite(lowerBound) ||
                !ContractValidation.IsFinite(upperBound) ||
                lowerBound >= upperBound)
            {
                return ContractValidationResult<bool>.Invalid(
                    "SyntheticPracticeRegulator.Bounds.Invalid",
                    "bounds",
                    "The practice actuator requires finite ordered lower and upper bounds.");
            }

            return ContractValidationResult<bool>.Valid(true);
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private static bool AreSameSimulationTime(double first, double second)
        {
            return Math.Abs(first - second) <= 1e-8;
        }

        private static ContractValidationResult<SyntheticPracticeRegulatorV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<SyntheticPracticeRegulatorV1>.Invalid(code, path, message);
        }
    }
}
