using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ReactorSim.Core
{
    /// <summary>
    /// One input to the deterministic reduced full-core power projection.
    /// This is an explicit migration seam: it carries the state and topology
    /// context the later two-group solve will consume without pretending that
    /// the current synthetic coefficients are plant data.
    /// </summary>
    public sealed class ReducedPowerNodeInputV1
    {
        private ReducedPowerNodeInputV1(
            NodeKey node,
            double radialDistance,
            double axialPosition,
            double burnupMwdPerKgHm,
            double channelAverageBurnupMwdPerKgHm,
            double neighbouringAverageBurnupMwdPerKgHm,
            int flowDirectionSign,
            double materialPowerFactor)
        {
            Node = node;
            RadialDistance = radialDistance;
            AxialPosition = axialPosition;
            BurnupMwdPerKgHm = burnupMwdPerKgHm;
            ChannelAverageBurnupMwdPerKgHm = channelAverageBurnupMwdPerKgHm;
            NeighbouringAverageBurnupMwdPerKgHm = neighbouringAverageBurnupMwdPerKgHm;
            FlowDirectionSign = flowDirectionSign;
            MaterialPowerFactor = materialPowerFactor;
        }

        public NodeKey Node { get; }

        public double RadialDistance { get; }

        public double AxialPosition { get; }

        public double BurnupMwdPerKgHm { get; }

        public double ChannelAverageBurnupMwdPerKgHm { get; }

        public double NeighbouringAverageBurnupMwdPerKgHm { get; }

        public int FlowDirectionSign { get; }

        public double MaterialPowerFactor { get; }

        public static ContractValidationResult<ReducedPowerNodeInputV1> TryCreate(
            NodeKey node,
            double radialDistance,
            double axialPosition,
            double burnupMwdPerKgHm,
            double channelAverageBurnupMwdPerKgHm,
            double neighbouringAverageBurnupMwdPerKgHm,
            int flowDirectionSign,
            double materialPowerFactor)
        {
            if (!IsCanonicalFinite(radialDistance) || radialDistance < 0.0 || radialDistance > 1.0)
            {
                return Invalid(
                    "ReducedPowerNode.RadialDistance.Invalid",
                    "radial_distance",
                    "Reduced power radial distance must be finite, canonical, and between zero and one.");
            }

            if (!IsCanonicalFinite(axialPosition) || axialPosition < 0.0 || axialPosition > 1.0)
            {
                return Invalid(
                    "ReducedPowerNode.AxialPosition.Invalid",
                    "axial_position",
                    "Reduced power axial position must be finite, canonical, and between zero and one.");
            }

            if (!IsCanonicalNonnegative(burnupMwdPerKgHm) ||
                !IsCanonicalNonnegative(channelAverageBurnupMwdPerKgHm) ||
                !IsCanonicalNonnegative(neighbouringAverageBurnupMwdPerKgHm))
            {
                return Invalid(
                    "ReducedPowerNode.Burnup.Invalid",
                    "burnup_mwd_per_kg_hm",
                    "Reduced power burnup inputs must be finite, canonical, and nonnegative.");
            }

            if (flowDirectionSign != -1 && flowDirectionSign != 1)
            {
                return Invalid(
                    "ReducedPowerNode.FlowDirection.Invalid",
                    "flow_direction_sign",
                    "Reduced power flow direction must be either -1 or +1.");
            }

            if (!IsCanonicalFinite(materialPowerFactor) || materialPowerFactor <= 0.0)
            {
                return Invalid(
                    "ReducedPowerNode.MaterialFactor.Invalid",
                    "material_power_factor",
                    "Reduced power material factors must be finite, canonical, and strictly positive.");
            }

            return ContractValidationResult<ReducedPowerNodeInputV1>.Valid(
                new ReducedPowerNodeInputV1(
                    node,
                    radialDistance,
                    axialPosition,
                    burnupMwdPerKgHm,
                    channelAverageBurnupMwdPerKgHm,
                    neighbouringAverageBurnupMwdPerKgHm,
                    flowDirectionSign,
                    materialPowerFactor));
        }

        private static ContractValidationResult<ReducedPowerNodeInputV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<ReducedPowerNodeInputV1>.Invalid(code, path, message);
        }

        private static bool IsCanonicalFinite(double value)
        {
            return ContractValidation.IsFinite(value) &&
                   BitConverter.DoubleToInt64Bits(value) != long.MinValue;
        }

        private static bool IsCanonicalNonnegative(double value)
        {
            return IsCanonicalFinite(value) && value >= 0.0;
        }
    }

    /// <summary>
    /// Power and shape for one ordered reduced-model node. ShapeFraction is
    /// relative to the mean node power and is independent of amplitude.
    /// </summary>
    public sealed class ReducedPowerNodeResultV1
    {
        internal ReducedPowerNodeResultV1(
            NodeKey node,
            double rawShapeWeight,
            double shapeFraction,
            double powerWatts)
        {
            Node = node;
            RawShapeWeight = rawShapeWeight;
            ShapeFraction = shapeFraction;
            PowerWatts = powerWatts;
        }

        public NodeKey Node { get; }

        public double RawShapeWeight { get; }

        public double ShapeFraction { get; }

        public double PowerWatts { get; }
    }

    /// <summary>
    /// Immutable result of one reduced synthetic projection. The power
    /// identity is explicit: target/total power are watts, while amplitude is
    /// dimensionless and rho is derived only from the returned k.
    /// </summary>
    public sealed class ReducedCorePowerProjectionV1
    {
        internal ReducedCorePowerProjectionV1(
            string modelId,
            double referencePowerWatts,
            double amplitude,
            double targetPowerWatts,
            double totalPowerWatts,
            double meanNodePowerWatts,
            double effectiveK,
            double reactivity,
            double powerBalanceRelativeError,
            IEnumerable<ReducedPowerNodeResultV1> nodes)
        {
            ModelId = modelId;
            ReferencePowerWatts = referencePowerWatts;
            Amplitude = amplitude;
            TargetPowerWatts = targetPowerWatts;
            TotalPowerWatts = totalPowerWatts;
            MeanNodePowerWatts = meanNodePowerWatts;
            EffectiveK = effectiveK;
            Reactivity = reactivity;
            PowerBalanceRelativeError = powerBalanceRelativeError;
            Nodes = new ReadOnlyCollection<ReducedPowerNodeResultV1>(nodes.ToArray());
        }

        public string ModelId { get; }

        public double ReferencePowerWatts { get; }

        public double Amplitude { get; }

        public double TargetPowerWatts { get; }

        public double TotalPowerWatts { get; }

        public double MeanNodePowerWatts { get; }

        public double EffectiveK { get; }

        public double Reactivity { get; }

        public double PowerBalanceRelativeError { get; }

        public IReadOnlyList<ReducedPowerNodeResultV1> Nodes { get; }
    }

    /// <summary>
    /// Project-authored fallback power model used until the existing
    /// SpatialEigenSolve is bound to the playable full-core state. Its
    /// coefficients are deliberately dimensionless and synthetic; this type
    /// is not a source of CANDU plant constants.
    /// </summary>
    public static class ReducedCorePowerModelV1
    {
        public const string ModelId = "reduced-synthetic-candu6-power-v2";

        public static ContractValidationResult<ReducedCorePowerProjectionV1> TryProject(
            IEnumerable<ReducedPowerNodeInputV1> nodeInputs,
            double referencePowerWatts,
            double amplitude)
        {
            if (nodeInputs == null)
            {
                return Invalid(
                    "ReducedPowerProjection.Nodes.Missing",
                    "nodes",
                    "A reduced power projection requires an ordered node input set.");
            }

            if (!IsCanonicalFinite(referencePowerWatts) || referencePowerWatts <= 0.0)
            {
                return Invalid(
                    "ReducedPowerProjection.ReferencePower.Invalid",
                    "reference_power_w",
                    "Reference power must be finite, canonical, and strictly positive SI watts.");
            }

            if (!IsCanonicalNonnegative(amplitude))
            {
                return Invalid(
                    "ReducedPowerProjection.Amplitude.Invalid",
                    "amplitude",
                    "Power amplitude must be finite, canonical, and nonnegative.");
            }

            ReducedPowerNodeInputV1[] inputs = nodeInputs.ToArray();
            if (inputs.Length == 0)
            {
                return Invalid(
                    "ReducedPowerProjection.Nodes.Empty",
                    "nodes",
                    "A reduced power projection requires at least one node.");
            }

            var nodeSet = new HashSet<NodeKey>();
            foreach (ReducedPowerNodeInputV1 input in inputs)
            {
                if (input == null)
                {
                    return Invalid(
                        "ReducedPowerProjection.Node.Null",
                        "nodes",
                        "A reduced power projection may not contain null node inputs.");
                }

                if (!nodeSet.Add(input.Node))
                {
                    return Invalid(
                        "ReducedPowerProjection.Node.Duplicate",
                        ContractValidation.NodePath(input.Node, string.Empty),
                        "A reduced power projection requires one input per unique node.");
                }
            }

            var rawWeights = new double[inputs.Length];
            double rawWeightTotal = 0.0;
            double worthTotal = 0.0;
            double radialBalanceTotal = 0.0;
            double flowTotal = 0.0;
            for (int index = 0; index < inputs.Length; index++)
            {
                ReducedPowerNodeInputV1 input = inputs[index];
                double radialShape = 0.84 +
                                     0.30 * (1.0 - Math.Pow(input.RadialDistance, 1.35));
                double axialShape = 0.86 +
                                    0.28 * Math.Sin(Math.PI * input.AxialPosition);
                double burnupShape = Clamp(
                    1.04 - 0.006 * input.BurnupMwdPerKgHm,
                    0.90,
                    1.04);
                double neighbourShape = Clamp(
                    1.0 +
                    (input.ChannelAverageBurnupMwdPerKgHm -
                     input.NeighbouringAverageBurnupMwdPerKgHm) * 0.0025,
                    0.96,
                    1.04);
                double flowShape = 1.0 + input.FlowDirectionSign * 0.004;
                double materialShape = Clamp(input.MaterialPowerFactor, 0.85, 1.15);
                double rawWeight = radialShape * axialShape * burnupShape *
                                   neighbourShape * flowShape * materialShape;
                if (!IsCanonicalPositive(rawWeight))
                {
                    return Invalid(
                        "ReducedPowerProjection.Shape.Invalid",
                        ContractValidation.NodePath(input.Node, ".shape"),
                        "The reduced power model produced a nonpositive or non-finite shape weight.");
                }

                rawWeights[index] = rawWeight;
                rawWeightTotal += rawWeight;

                double burnupWorth = Clamp(
                    1.03 - 0.004 * input.BurnupMwdPerKgHm,
                    0.90,
                    1.03);
                double neighbourWorth = Clamp(
                    1.0 +
                    (input.NeighbouringAverageBurnupMwdPerKgHm -
                     input.ChannelAverageBurnupMwdPerKgHm) * 0.001,
                    0.98,
                    1.02);
                worthTotal += materialShape * burnupWorth *
                              neighbourWorth * (1.0 + input.FlowDirectionSign * 0.001);
                radialBalanceTotal += 1.0 - input.RadialDistance;
                flowTotal += input.FlowDirectionSign;
            }

            if (!IsCanonicalPositive(rawWeightTotal) ||
                !IsCanonicalFinite(worthTotal) ||
                !IsCanonicalFinite(radialBalanceTotal) ||
                !IsCanonicalFinite(flowTotal))
            {
                return Invalid(
                    "ReducedPowerProjection.Aggregate.Invalid",
                    "nodes",
                    "The reduced power model aggregate became non-finite or nonpositive.");
            }

            double targetPowerWatts = referencePowerWatts * amplitude;
            if (!IsCanonicalNonnegative(targetPowerWatts))
            {
                return Invalid(
                    "ReducedPowerProjection.TargetPower.Invalid",
                    "target_power_w",
                    "Reference power multiplied by amplitude must remain finite and nonnegative SI watts.");
            }

            double meanRawWeight = rawWeightTotal / inputs.Length;
            double meanWorth = worthTotal / inputs.Length;
            double radialBalance = radialBalanceTotal / inputs.Length;
            double meanFlow = flowTotal / inputs.Length;
            double effectiveK = Clamp(
                1.0 +
                0.012 * (meanWorth - 1.0) +
                0.0008 * (radialBalance - 0.55) +
                0.0002 * meanFlow,
                0.90,
                1.10);
            double reactivity = (effectiveK - 1.0) / effectiveK;
            var nodeResults = new List<ReducedPowerNodeResultV1>(inputs.Length);
            double totalPowerWatts = 0.0;
            for (int index = 0; index < inputs.Length; index++)
            {
                double powerWatts = targetPowerWatts == 0.0
                    ? 0.0
                    : targetPowerWatts * rawWeights[index] / rawWeightTotal;
                if (!IsCanonicalNonnegative(powerWatts))
                {
                    return Invalid(
                        "ReducedPowerProjection.NodePower.Invalid",
                        ContractValidation.NodePath(inputs[index].Node, ".power_w"),
                        "The reduced power model produced a non-finite or negative SI node power.");
                }

                totalPowerWatts += powerWatts;
                nodeResults.Add(
                    new ReducedPowerNodeResultV1(
                        inputs[index].Node,
                        rawWeights[index],
                        rawWeights[index] / meanRawWeight,
                        powerWatts));
            }

            if (nodeResults.Count > 0 && targetPowerWatts > 0.0)
            {
                // Keep the aggregate identity stable after the ordered
                // floating-point sum. The correction is bounded to the final
                // node and is not a second physical calculation.
                double correction = targetPowerWatts - totalPowerWatts;
                ReducedPowerNodeResultV1 last = nodeResults[nodeResults.Count - 1];
                double correctedPower = last.PowerWatts + correction;
                if (!IsCanonicalNonnegative(correctedPower))
                {
                    return Invalid(
                        "ReducedPowerProjection.PowerBalance.Invalid",
                        "total_power_w",
                        "The deterministic power-balance correction would make a node power negative.");
                }

                nodeResults[nodeResults.Count - 1] =
                    new ReducedPowerNodeResultV1(
                        last.Node,
                        last.RawShapeWeight,
                        last.ShapeFraction,
                        correctedPower);
                totalPowerWatts = nodeResults.Sum(node => node.PowerWatts);
            }

            double meanNodePowerWatts = totalPowerWatts / inputs.Length;
            double powerBalanceRelativeError = targetPowerWatts == 0.0
                ? 0.0
                : Math.Abs(totalPowerWatts - targetPowerWatts) / targetPowerWatts;
            if (!IsCanonicalFinite(effectiveK) ||
                !IsCanonicalFinite(reactivity) ||
                !IsCanonicalNonnegative(meanNodePowerWatts) ||
                !IsCanonicalFinite(powerBalanceRelativeError))
            {
                return Invalid(
                    "ReducedPowerProjection.Result.Invalid",
                    "projection",
                    "The reduced power projection result contains a non-finite value.");
            }

            return ContractValidationResult<ReducedCorePowerProjectionV1>.Valid(
                new ReducedCorePowerProjectionV1(
                    ModelId,
                    referencePowerWatts,
                    amplitude,
                    targetPowerWatts,
                    totalPowerWatts,
                    meanNodePowerWatts,
                    effectiveK,
                    reactivity,
                    powerBalanceRelativeError,
                    nodeResults));
        }

        private static ContractValidationResult<ReducedCorePowerProjectionV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<ReducedCorePowerProjectionV1>.Invalid(code, path, message);
        }

        private static bool IsCanonicalFinite(double value)
        {
            return ContractValidation.IsFinite(value) &&
                   BitConverter.DoubleToInt64Bits(value) != long.MinValue;
        }

        private static bool IsCanonicalNonnegative(double value)
        {
            return IsCanonicalFinite(value) && value >= 0.0;
        }

        private static bool IsCanonicalPositive(double value)
        {
            return IsCanonicalFinite(value) && value > 0.0;
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }
}
