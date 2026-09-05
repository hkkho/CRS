using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ReactorSim.Core
{
    /// <summary>
    /// Stable identity for the first-order perturbation reactivity used by
    /// the steady-state gameplay path.  The static k/rho value remains a
    /// separate diagnostic owned by <see cref="FullCoreDiffusionSolveResultV1"/>.
    /// </summary>
    public static class AdjointWeightedReactivityIdentityV1
    {
        public const uint CurrentSchemaVersion = 1;
        public const string MethodId =
            "adjoint-weighted-first-order-perturbation-v1";
        public const string FormulaId =
            "bilinear-delta-f-minus-delta-a-over-reference-f-v1";
        public const string EnergyGroupOrderIdentity = "fast|thermal";
        public const double MinimumRelativeDenominator = 1.0e-15;
    }

    /// <summary>
    /// Compact result of one deterministic adjoint-weighted perturbation
    /// calculation.  It deliberately contains no nodewise arrays.
    /// </summary>
    public sealed class AdjointWeightedReactivityResultV1
    {
        private readonly string _identity;
        private readonly string _formulaIdentity;

        internal AdjointWeightedReactivityResultV1(
            double numerator,
            double denominator,
            double reactivity,
            Digest32 bindingDigest,
            Digest32 referenceCoefficientDigest,
            Digest32 currentCoefficientDigest)
        {
            Numerator = numerator;
            Denominator = denominator;
            Reactivity = reactivity;
            BindingDigest = bindingDigest;
            ReferenceCoefficientDigest = referenceCoefficientDigest;
            CurrentCoefficientDigest = currentCoefficientDigest;
            _identity = AdjointWeightedReactivityIdentityV1.MethodId;
            _formulaIdentity = AdjointWeightedReactivityIdentityV1.FormulaId;
        }

        public double Numerator { get; }

        public double Denominator { get; }

        public double Reactivity { get; }

        public double WeightedPerturbationReactivity
        {
            get { return Reactivity; }
        }

        public string Identity
        {
            get { return _identity; }
        }

        public string FormulaIdentity
        {
            get { return _formulaIdentity; }
        }

        public Digest32 BindingDigest { get; }

        public Digest32 ReferenceCoefficientDigest { get; }

        public Digest32 CurrentCoefficientDigest { get; }

        public string BindingDigestHex
        {
            get { return ToHex(BindingDigest); }
        }

        private static string ToHex(Digest32 digest)
        {
            var value = new System.Text.StringBuilder(digest.Bytes.Count * 2);
            foreach (byte item in digest.Bytes)
            {
                value.Append(item.ToString("x2", CultureInfo.InvariantCulture));
            }

            return value.ToString();
        }
    }

    /// <summary>
    /// Deterministic first-order perturbation calculator for the active
    /// adiabatic static-eigenmode path.
    ///
    /// For the canonical two-group matrix A and fission operator F, the
    /// operational result is
    ///
    ///   rho_1 = &lt;W_ref, (-Delta A + Delta F) phi_current&gt;
    ///           / &lt;W_ref, F_ref phi_current&gt;.
    ///
    /// The reference adjoint is B1's immutable field.  The current shape is
    /// the current solve flux; its arbitrary positive normalization cancels
    /// between the explicit numerator and denominator.
    /// </summary>
    public static class AdjointWeightedReactivityV1
    {
        public static ContractValidationResult<AdjointWeightedReactivityResultV1> TryCompute(
            FullCoreAdjointImportanceV1 referenceAdjoint,
            FullCoreDiffusionSolveResultV1 referenceSolve,
            FullCoreDiffusionSolveResultV1 currentSolve,
            IqsKineticsDataPackV1 kineticsDataPack)
        {
            if (referenceAdjoint == null)
            {
                return Invalid(
                    "AdjointReactivity.ReferenceAdjoint.Missing",
                    "reference_adjoint",
                    "A B2 reactivity calculation requires the validated B1 reference adjoint.");
            }

            if (referenceSolve == null)
            {
                return Invalid(
                    "AdjointReactivity.ReferenceSolve.Missing",
                    "reference_solve",
                    "A B2 reactivity calculation requires the reference spatial solve.");
            }

            if (currentSolve == null)
            {
                return Invalid(
                    "AdjointReactivity.CurrentSolve.Missing",
                    "current_solve",
                    "A B2 reactivity calculation requires the current spatial solve.");
            }

            if (kineticsDataPack == null)
            {
                return Invalid(
                    "AdjointReactivity.KineticsDataPack.Missing",
                    "kinetics_data_pack",
                    "A B2 reactivity calculation requires the ordered kinetics data pack.");
            }

            if (referenceSolve.Coefficients == null ||
                currentSolve.Coefficients == null)
            {
                return Invalid(
                    "AdjointReactivity.Coefficients.Missing",
                    "coefficients",
                    "Reference and current solves must retain validated coefficient bindings.");
            }

            if (!referenceSolve.InventoryBindingDigest.Equals(
                    referenceAdjoint.ReferenceStateDigest))
            {
                return Invalid(
                    "AdjointReactivity.ReferenceBinding.Stale",
                    "reference_solve.inventory_binding_digest",
                    "The reference solve inventory does not match the B1 adjoint reference-state binding.");
            }

            ContractValidationResult<AdjointWeightedReactivityResultV1> result =
                TryCompute(
                    referenceAdjoint,
                    referenceSolve.DataPack,
                    kineticsDataPack,
                    referenceSolve.Coefficients,
                    currentSolve.Coefficients,
                    currentSolve.Group1Flux,
                    currentSolve.Group2Flux);
            if (!result.IsValid)
            {
                return result;
            }

            if (!referenceSolve.CoefficientBindingDigest.Equals(
                    result.Value.ReferenceCoefficientDigest) ||
                !currentSolve.CoefficientBindingDigest.Equals(
                    result.Value.CurrentCoefficientDigest))
            {
                return Invalid(
                    "AdjointReactivity.CoefficientBinding.Stale",
                    "coefficients.binding_digest",
                    "The solve coefficient binding digest does not match the canonical material rows.");
            }

            if (!SameDigest(
                    referenceSolve.DataPack.Descriptor.ContentDigest,
                    currentSolve.DataPack.Descriptor.ContentDigest) ||
                !SameDigest(
                    referenceSolve.DataPack.Descriptor.TopologyDigest,
                    currentSolve.DataPack.Descriptor.TopologyDigest))
            {
                return Invalid(
                    "AdjointReactivity.DataPack.Mismatch",
                    "current_solve.data_pack",
                    "Reference and current spatial solves must use one exact diffusion data-pack binding.");
            }

            return result;
        }

        /// <summary>
        /// Lower-level Core contract used by manufactured and invalid-binding
        /// tests.  The supplied coefficient sets must be bound to the exact
        /// B1 stencil and diffusion pack; no presentation layer calls this
        /// overload.
        /// </summary>
        public static ContractValidationResult<AdjointWeightedReactivityResultV1> TryCompute(
            FullCoreAdjointImportanceV1 referenceAdjoint,
            FullCoreDiffusionDataPackV1 diffusionDataPack,
            IqsKineticsDataPackV1 kineticsDataPack,
            SpatialCoefficientSet referenceCoefficients,
            SpatialCoefficientSet currentCoefficients,
            IReadOnlyList<double> currentGroup1Flux,
            IReadOnlyList<double> currentGroup2Flux)
        {
            if (referenceAdjoint == null)
            {
                return Invalid(
                    "AdjointReactivity.ReferenceAdjoint.Missing",
                    "reference_adjoint",
                    "A B2 reactivity calculation requires the validated B1 reference adjoint.");
            }

            if (diffusionDataPack == null)
            {
                return Invalid(
                    "AdjointReactivity.DiffusionDataPack.Missing",
                    "diffusion_data_pack",
                    "A B2 reactivity calculation requires a validated diffusion data pack.");
            }

            if (kineticsDataPack == null)
            {
                return Invalid(
                    "AdjointReactivity.KineticsDataPack.Missing",
                    "kinetics_data_pack",
                    "A B2 reactivity calculation requires the ordered kinetics data pack.");
            }

            if (referenceCoefficients == null || currentCoefficients == null)
            {
                return Invalid(
                    "AdjointReactivity.Coefficients.Missing",
                    "coefficients",
                    "Reference and current coefficient bindings are required.");
            }

            if (currentGroup1Flux == null || currentGroup2Flux == null)
            {
                return Invalid(
                    "AdjointReactivity.Flux.Missing",
                    "current_flux",
                    "Both current energy-group flux vectors are required.");
            }

            if (!SameDigest(
                    diffusionDataPack.Descriptor.TopologyDigest,
                    referenceAdjoint.TopologyDigest.Bytes) ||
                !SameDigest(
                    diffusionDataPack.Descriptor.ContentDigest,
                    referenceAdjoint.DiffusionDataPackDigest.Bytes))
            {
                return Invalid(
                    "AdjointReactivity.DataPack.BindingMismatch",
                    "diffusion_data_pack",
                    "The supplied diffusion pack is not the pack bound into the B1 reference adjoint.");
            }

            if (!SameDigest(
                    kineticsDataPack.TopologyDigest.Bytes,
                    referenceAdjoint.TopologyDigest.Bytes) ||
                !SameDigest(
                    kineticsDataPack.ContentDigest.Bytes,
                    referenceAdjoint.KineticsDataPackDigest.Bytes))
            {
                return Invalid(
                    "AdjointReactivity.KineticsDataPack.BindingMismatch",
                    "kinetics_data_pack",
                    "The supplied kinetics pack is not the pack bound into the B1 reference adjoint.");
            }

            ContractValidationResult<bool> groupIdentity =
                ValidateEnergyGroupBinding(
                    diffusionDataPack,
                    kineticsDataPack,
                    referenceAdjoint);
            if (!groupIdentity.IsValid)
            {
                return Invalid(groupIdentity.FirstDiagnostic);
            }

            if (!ReferenceEquals(referenceCoefficients.Stencil, referenceAdjoint.Stencil) ||
                !ReferenceEquals(currentCoefficients.Stencil, referenceAdjoint.Stencil))
            {
                return Invalid(
                    "AdjointReactivity.Topology.BindingMismatch",
                    "coefficients.stencil",
                    "Reference and current coefficient sets must use the exact B1 topology and stencil instance.");
            }

            int nodeCount = referenceAdjoint.NodeCount;
            if (referenceCoefficients.NodeCount != nodeCount ||
                currentCoefficients.NodeCount != nodeCount ||
                currentGroup1Flux.Count != nodeCount ||
                currentGroup2Flux.Count != nodeCount)
            {
                return Invalid(
                    "AdjointReactivity.Dimensions.Mismatch",
                    "dimensions",
                    "Coefficient, adjoint, and current-flux dimensions must match the canonical stencil.");
            }

            double velocity1 = kineticsDataPack.GroupVelocitiesMPerSecond[0];
            double velocity2 = kineticsDataPack.GroupVelocitiesMPerSecond[1];
            if (!ContractValidation.IsFinite(velocity1) || velocity1 <= 0.0 ||
                !ContractValidation.IsFinite(velocity2) || velocity2 <= 0.0)
            {
                return Invalid(
                    "AdjointReactivity.GroupVelocities.Invalid",
                    "kinetics_data_pack.group_velocities_m_per_s",
                    "Both ordered energy-group velocities must be finite and strictly positive.");
            }

            double numerator = 0.0;
            double denominator = 0.0;
            for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
            {
                SpatialNodeCoefficients reference = referenceCoefficients.Nodes[nodeIndex];
                SpatialNodeCoefficients current = currentCoefficients.Nodes[nodeIndex];
                if (reference.Node != current.Node ||
                    reference.Node != referenceAdjoint.Stencil.Nodes[nodeIndex].Node)
                {
                    return Invalid(
                        "AdjointReactivity.Topology.NodeOrderMismatch",
                        ContractValidation.NodePath(reference.Node, ".coefficients"),
                        "Reference/current coefficient rows must remain in the canonical B1 node order.");
                }

                if (!ValidateNodeCoefficients(
                        reference,
                        current,
                        diffusionDataPack.NodeVolumeM3,
                        reference.Node,
                        out ContractDiagnostic coefficientDiagnostic))
                {
                    return Invalid(coefficientDiagnostic);
                }

                double group1Flux = currentGroup1Flux[nodeIndex];
                double group2Flux = currentGroup2Flux[nodeIndex];
                double importance1 = referenceAdjoint.Group1Importance[nodeIndex];
                double importance2 = referenceAdjoint.Group2Importance[nodeIndex];
                if (!IsFiniteNonnegative(group1Flux) ||
                    !IsFiniteNonnegative(group2Flux) ||
                    !IsFiniteNonnegative(importance1) ||
                    !IsFiniteNonnegative(importance2))
                {
                    return Invalid(
                        "AdjointReactivity.FluxOrImportance.Invalid",
                        ContractValidation.NodePath(reference.Node, ".bilinear_terms"),
                        "Current flux and reference importance values must be finite and nonnegative.");
                }

                double deltaDownscatter =
                    current.DownscatterGroup1To2PerM -
                    reference.DownscatterGroup1To2PerM;
                double deltaAbsorption1 =
                    current.AbsorptionGroup1PerM -
                    reference.AbsorptionGroup1PerM;
                double deltaAbsorption2 =
                    current.AbsorptionGroup2PerM -
                    reference.AbsorptionGroup2PerM;

                // A has group-1 removal (absorption + downscatter) on its
                // first row and -downscatter on the group-2/source coupling.
                // Writing -Delta A explicitly makes the sign convention
                // auditable rather than hiding it in an eigenvalue delta.
                double deltaAContribution =
                    -importance1 *
                        (deltaAbsorption1 + deltaDownscatter) * group1Flux -
                    importance2 *
                        (deltaAbsorption2 * group2Flux -
                         deltaDownscatter * group1Flux);

                double referenceFissionSource =
                    reference.NuFissionGroup1PerM * group1Flux +
                    reference.NuFissionGroup2PerM * group2Flux;
                double currentFissionSource =
                    current.NuFissionGroup1PerM * group1Flux +
                    current.NuFissionGroup2PerM * group2Flux;
                double referenceFissionGroup1 =
                    reference.ChiGroup1 * referenceFissionSource;
                double referenceFissionGroup2 =
                    reference.ChiGroup2 * referenceFissionSource;
                double deltaFissionGroup1 =
                    current.ChiGroup1 * currentFissionSource -
                    referenceFissionGroup1;
                double deltaFissionGroup2 =
                    current.ChiGroup2 * currentFissionSource -
                    referenceFissionGroup2;

                double numeratorDensity = deltaAContribution +
                    importance1 * deltaFissionGroup1 +
                    importance2 * deltaFissionGroup2;
                double denominatorDensity =
                    importance1 * referenceFissionGroup1 +
                    importance2 * referenceFissionGroup2;
                double numeratorContribution =
                    reference.VolumeM3 * numeratorDensity;
                double denominatorContribution =
                    reference.VolumeM3 * denominatorDensity;
                if (!ContractValidation.IsFinite(numeratorContribution) ||
                    !ContractValidation.IsFinite(denominatorContribution) ||
                    denominatorContribution < 0.0)
                {
                    return Invalid(
                        "AdjointReactivity.BilinearTerm.NonFinite",
                        ContractValidation.NodePath(reference.Node, ".bilinear_terms"),
                        "Every weighted perturbation term must be finite and the denominator contribution nonnegative.");
                }

                numerator += numeratorContribution;
                denominator += denominatorContribution;
                if (!ContractValidation.IsFinite(numerator) ||
                    !ContractValidation.IsFinite(denominator))
                {
                    return Invalid(
                        "AdjointReactivity.BilinearReduction.NonFinite",
                        "bilinear_reduction",
                        "The deterministic bilinear numerator or denominator became non-finite.");
                }
            }

            if (!ContractValidation.IsFinite(numerator))
            {
                return Invalid(
                    "AdjointReactivity.Numerator.NonFinite",
                    "numerator",
                    "The weighted perturbation numerator must be finite.");
            }

            if (!ContractValidation.IsFinite(denominator) || denominator <= 0.0)
            {
                return Invalid(
                    "AdjointReactivity.Denominator.Invalid",
                    "denominator",
                    "The weighted fission denominator must be finite and strictly positive.");
            }

            double denominatorFloor =
                AdjointWeightedReactivityIdentityV1.MinimumRelativeDenominator *
                Math.Max(1.0, Math.Abs(denominator));
            if (denominator <= denominatorFloor)
            {
                return Invalid(
                    "AdjointReactivity.Denominator.NearZero",
                    "denominator",
                    "The weighted fission denominator is too close to zero for a stable first-order result.");
            }

            double reactivity = numerator / denominator;
            if (!ContractValidation.IsFinite(reactivity))
            {
                return Invalid(
                    "AdjointReactivity.Result.NonFinite",
                    "reactivity",
                    "The weighted first-order reactivity must be finite.");
            }

            if (reactivity == 0.0)
            {
                reactivity = 0.0;
            }

            Digest32 referenceCoefficientDigest =
                ComputeCoefficientBindingDigest(diffusionDataPack, referenceCoefficients);
            Digest32 currentCoefficientDigest =
                ComputeCoefficientBindingDigest(diffusionDataPack, currentCoefficients);
            Digest32 bindingDigest = ComputeBindingDigest(
                diffusionDataPack,
                kineticsDataPack,
                referenceAdjoint,
                referenceCoefficientDigest,
                currentCoefficientDigest);

            return ContractValidationResult<AdjointWeightedReactivityResultV1>.Valid(
                new AdjointWeightedReactivityResultV1(
                    numerator,
                    denominator,
                    reactivity,
                    bindingDigest,
                    referenceCoefficientDigest,
                    currentCoefficientDigest));
        }

        internal static Digest32 ComputeCoefficientBindingDigest(
            FullCoreDiffusionDataPackV1 dataPack,
            SpatialCoefficientSet coefficients)
        {
            return new Digest32(Phase5CanonicalBytesV1.HashBody(
                "full-core-coefficient-binding-v1",
                writer =>
                {
                    Phase5CanonicalBytesV1.WriteDigest(
                        writer,
                        new Digest32(dataPack.Descriptor.TopologyDigest.ToArray()));
                    Phase5CanonicalBytesV1.WriteDigest(
                        writer,
                        new Digest32(dataPack.Descriptor.ContentDigest.ToArray()));
                    Phase5CanonicalBytesV1.WriteString(
                        writer,
                        dataPack.Descriptor.TopologySchemaId);
                    Phase5CanonicalBytesV1.WriteString(
                        writer,
                        AdjointWeightedReactivityIdentityV1.EnergyGroupOrderIdentity);
                    Phase5CanonicalBytesV1.WriteUInt32(
                        writer,
                        checked((uint)coefficients.NodeCount));
                    foreach (SpatialNodeCoefficients node in coefficients.Nodes)
                    {
                        Phase5CanonicalBytesV1.WriteUInt32(
                            writer,
                            node.Node.ChannelId.Value);
                        Phase5CanonicalBytesV1.WriteUInt32(
                            writer,
                            node.Node.Position.Value);
                        Phase5CanonicalBytesV1.WriteDouble(writer, node.VolumeM3);
                        Phase5CanonicalBytesV1.WriteDouble(writer, node.AbsorptionGroup1PerM);
                        Phase5CanonicalBytesV1.WriteDouble(writer, node.AbsorptionGroup2PerM);
                        Phase5CanonicalBytesV1.WriteDouble(writer, node.DownscatterGroup1To2PerM);
                        Phase5CanonicalBytesV1.WriteDouble(writer, node.FissionGroup1PerM);
                        Phase5CanonicalBytesV1.WriteDouble(writer, node.FissionGroup2PerM);
                        Phase5CanonicalBytesV1.WriteDouble(writer, node.NuFissionGroup1PerM);
                        Phase5CanonicalBytesV1.WriteDouble(writer, node.NuFissionGroup2PerM);
                        Phase5CanonicalBytesV1.WriteDouble(writer, node.ChiGroup1);
                        Phase5CanonicalBytesV1.WriteDouble(writer, node.ChiGroup2);
                        Phase5CanonicalBytesV1.WriteDouble(writer, node.EnergyPerFissionJ);
                    }
                }));
        }

        private static Digest32 ComputeBindingDigest(
            FullCoreDiffusionDataPackV1 diffusionDataPack,
            IqsKineticsDataPackV1 kineticsDataPack,
            FullCoreAdjointImportanceV1 referenceAdjoint,
            Digest32 referenceCoefficientDigest,
            Digest32 currentCoefficientDigest)
        {
            return new Digest32(Phase5CanonicalBytesV1.HashBody(
                "adjoint-weighted-reactivity-binding-v1",
                writer =>
                {
                    Phase5CanonicalBytesV1.WriteUInt32(
                        writer,
                        AdjointWeightedReactivityIdentityV1.CurrentSchemaVersion);
                    Phase5CanonicalBytesV1.WriteString(
                        writer,
                        AdjointWeightedReactivityIdentityV1.MethodId);
                    Phase5CanonicalBytesV1.WriteString(
                        writer,
                        AdjointWeightedReactivityIdentityV1.FormulaId);
                    Phase5CanonicalBytesV1.WriteDigest(
                        writer,
                        referenceAdjoint.Digest);
                    Phase5CanonicalBytesV1.WriteDigest(
                        writer,
                        new Digest32(diffusionDataPack.Descriptor.TopologyDigest.ToArray()));
                    Phase5CanonicalBytesV1.WriteDigest(
                        writer,
                        new Digest32(diffusionDataPack.Descriptor.ContentDigest.ToArray()));
                    Phase5CanonicalBytesV1.WriteDigest(
                        writer,
                        kineticsDataPack.ContentDigest);
                    Phase5CanonicalBytesV1.WriteString(
                        writer,
                        AdjointWeightedReactivityIdentityV1.EnergyGroupOrderIdentity);
                    Phase5CanonicalBytesV1.WriteDigest(writer, referenceCoefficientDigest);
                    Phase5CanonicalBytesV1.WriteDigest(writer, currentCoefficientDigest);
                }));
        }

        private static ContractValidationResult<bool> ValidateEnergyGroupBinding(
            FullCoreDiffusionDataPackV1 diffusionDataPack,
            IqsKineticsDataPackV1 kineticsDataPack,
            FullCoreAdjointImportanceV1 referenceAdjoint)
        {
            if (!IsCanonicalEnergyGroupOrder(diffusionDataPack.EnergyGroupOrder) ||
                !IsCanonicalEnergyGroupOrder(kineticsDataPack.EnergyGroupOrder) ||
                !IsCanonicalEnergyGroupOrder(referenceAdjoint.EnergyGroupOrder) ||
                !diffusionDataPack.EnergyGroupOrder.SequenceEqual(
                    kineticsDataPack.EnergyGroupOrder,
                    StringComparer.Ordinal) ||
                !diffusionDataPack.EnergyGroupOrder.SequenceEqual(
                    referenceAdjoint.EnergyGroupOrder,
                    StringComparer.Ordinal))
            {
                return ContractValidationResult<bool>.Invalid(
                    "AdjointReactivity.EnergyGroupOrder.Mismatch",
                    "energy_group_order",
                    "The diffusion pack, kinetics pack, and B1 adjoint must use exact [fast, thermal] ordering.");
            }

            return ContractValidationResult<bool>.Valid(true);
        }

        private static bool ValidateNodeCoefficients(
            SpatialNodeCoefficients reference,
            SpatialNodeCoefficients current,
            double expectedVolume,
            NodeKey node,
            out ContractDiagnostic diagnostic)
        {
            double[] values =
            {
                reference.VolumeM3,
                current.VolumeM3,
                reference.AbsorptionGroup1PerM,
                current.AbsorptionGroup1PerM,
                reference.AbsorptionGroup2PerM,
                current.AbsorptionGroup2PerM,
                reference.DownscatterGroup1To2PerM,
                current.DownscatterGroup1To2PerM,
                reference.NuFissionGroup1PerM,
                current.NuFissionGroup1PerM,
                reference.NuFissionGroup2PerM,
                current.NuFissionGroup2PerM,
                reference.ChiGroup1,
                current.ChiGroup1,
                reference.ChiGroup2,
                current.ChiGroup2
            };
            if (values.Any(value => !ContractValidation.IsFinite(value)) ||
                !ContractValidation.IsFinite(expectedVolume) ||
                expectedVolume <= 0.0)
            {
                diagnostic = new ContractDiagnostic(
                    "AdjointReactivity.Coefficients.NonFinite",
                    ContractValidation.NodePath(node, ".coefficients"),
                    "Reference/current material coefficients must be finite.");
                return false;
            }

            if (reference.VolumeM3 <= 0.0 || current.VolumeM3 <= 0.0 ||
                reference.VolumeM3 != expectedVolume ||
                current.VolumeM3 != expectedVolume)
            {
                diagnostic = new ContractDiagnostic(
                    "AdjointReactivity.Coefficients.VolumeMismatch",
                    ContractValidation.NodePath(node, ".volume_m3"),
                    "Reference/current coefficient rows must use the diffusion pack node volume.");
                return false;
            }

            diagnostic = null!;
            return true;
        }

        private static bool IsCanonicalEnergyGroupOrder(IReadOnlyList<string> order)
        {
            return order != null && order.Count == 2 &&
                   string.Equals(order[0], "fast", StringComparison.Ordinal) &&
                   string.Equals(order[1], "thermal", StringComparison.Ordinal);
        }

        private static bool IsFiniteNonnegative(double value)
        {
            return ContractValidation.IsFinite(value) && value >= 0.0;
        }

        private static bool SameDigest(
            IReadOnlyList<byte> left,
            IReadOnlyList<byte> right)
        {
            return left != null && right != null && left.SequenceEqual(right);
        }

        private static ContractValidationResult<AdjointWeightedReactivityResultV1> Invalid(
            ContractDiagnostic diagnostic)
        {
            return Invalid(diagnostic.Code, diagnostic.Path, diagnostic.Message);
        }

        private static ContractValidationResult<AdjointWeightedReactivityResultV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<AdjointWeightedReactivityResultV1>.Invalid(
                code,
                path,
                message);
        }
    }
}
