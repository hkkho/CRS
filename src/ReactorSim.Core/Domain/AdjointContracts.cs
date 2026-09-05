using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ReactorSim.Core
{
    /// <summary>
    /// The deterministic transpose-eigenmode method used to construct the
    /// reference importance field.  The method is deliberately named
    /// independently from the legacy IqsFullCoreSolver type.
    /// </summary>
    public static class SpatialAdjointEigenSolve
    {
        private static readonly string[] CanonicalEnergyGroupOrder =
            { "fast", "thermal" };

        public const uint CurrentSchemaVersion = 1;
        public const string SolverIdentity =
            "deterministic-transpose-adjoint-k-eigenmode-jacobi-v1";
        public const string NormalizationIdentity =
            "unit-volume-speed-integral-v1";
        public const string EnergyGroupOrderIdentity = "fast|thermal";

        public static ContractValidationResult<SpatialAdjointSolveResultV1> TrySolve(
            SpatialStencil stencil,
            SpatialCoefficientSet coefficients,
            SpatialLinearSolvePolicy linearSolvePolicy,
            SpatialConvergencePolicy convergencePolicy,
            double referenceEigenvalue,
            IReadOnlyList<double> groupVelocitiesMPerSecond)
        {
            return TrySolve(
                stencil,
                coefficients,
                linearSolvePolicy,
                convergencePolicy,
                referenceEigenvalue,
                groupVelocitiesMPerSecond,
                CanonicalEnergyGroupOrder);
        }

        public static ContractValidationResult<SpatialAdjointSolveResultV1> TrySolve(
            SpatialStencil stencil,
            SpatialCoefficientSet coefficients,
            SpatialLinearSolvePolicy linearSolvePolicy,
            SpatialConvergencePolicy convergencePolicy,
            double referenceEigenvalue,
            IReadOnlyList<double> groupVelocitiesMPerSecond,
            IReadOnlyList<string> energyGroupOrder)
        {
            if (stencil == null)
            {
                return Invalid(
                    "SpatialAdjointSolve.Stencil.Missing",
                    "stencil",
                    "An adjoint solve requires an assembled spatial stencil.");
            }

            if (coefficients == null)
            {
                return Invalid(
                    "SpatialAdjointSolve.Coefficients.Missing",
                    "coefficients",
                    "An adjoint solve requires validated spatial coefficients.");
            }

            if (!ReferenceEquals(stencil, coefficients.Stencil))
            {
                return Invalid(
                    "SpatialAdjointSolve.Binding.Mismatch",
                    "coefficients",
                    "Adjoint coefficients must be bound to the exact supplied stencil.");
            }

            if (linearSolvePolicy == null)
            {
                return Invalid(
                    "SpatialAdjointSolve.LinearSolvePolicy.Missing",
                    "linear_solve_policy",
                    "An adjoint solve requires an explicit deterministic inner-solve policy.");
            }

            if (convergencePolicy == null)
            {
                return Invalid(
                    "SpatialAdjointSolve.ConvergencePolicy.Missing",
                    "convergence_policy",
                    "An adjoint solve requires an explicit convergence policy.");
            }

            if (!ContractValidation.IsFinite(referenceEigenvalue) || referenceEigenvalue <= 0.0)
            {
                return Invalid(
                    "SpatialAdjointSolve.ReferenceEigenvalue.Invalid",
                    "reference_eigenvalue",
                    "The reference eigenvalue must be finite and strictly positive.");
            }

            ContractValidationResult<bool> groupIdentity = ValidateGroupIdentity(
                energyGroupOrder,
                "energy_group_order");
            if (!groupIdentity.IsValid)
            {
                return Invalid(groupIdentity.FirstDiagnostic);
            }

            if (groupVelocitiesMPerSecond == null || groupVelocitiesMPerSecond.Count != 2)
            {
                return Invalid(
                    "SpatialAdjointSolve.GroupVelocities.DimensionMismatch",
                    "group_velocities_m_per_s",
                    "An adjoint solve requires exactly one velocity for each ordered energy group.");
            }

            for (int group = 0; group < groupVelocitiesMPerSecond.Count; group++)
            {
                double velocity = groupVelocitiesMPerSecond[group];
                if (!ContractValidation.IsFinite(velocity) || velocity <= 0.0)
                {
                    return Invalid(
                        "SpatialAdjointSolve.GroupVelocities.Invalid",
                        "group_velocities_m_per_s[" + group.ToString(CultureInfo.InvariantCulture) + "]",
                        "Every energy-group velocity must be finite and strictly positive.");
                }
            }

            if (stencil.NodeCount <= 0 || coefficients.NodeCount != stencil.NodeCount)
            {
                return Invalid(
                    "SpatialAdjointSolve.Dimensions.Invalid",
                    "dimensions",
                    "The adjoint stencil and coefficient set must have the same positive node count.");
            }

            ContractValidationResult<SpatialOperator> operatorResult =
                SpatialOperator.TryCreate(stencil, coefficients);
            if (!operatorResult.IsValid)
            {
                return Invalid(operatorResult.FirstDiagnostic);
            }

            ContractValidationResult<double[]> group1DiagonalResult = BuildDiagonal(
                stencil,
                coefficients,
                SpatialEnergyGroup.Group1);
            if (!group1DiagonalResult.IsValid)
            {
                return Invalid(group1DiagonalResult.FirstDiagnostic);
            }

            ContractValidationResult<double[]> group2DiagonalResult = BuildDiagonal(
                stencil,
                coefficients,
                SpatialEnergyGroup.Group2);
            if (!group2DiagonalResult.IsValid)
            {
                return Invalid(group2DiagonalResult.FirstDiagnostic);
            }

            int nodeCount = stencil.NodeCount;
            double[] currentGroup1 = CreateUnitVector(nodeCount);
            double[] currentGroup2 = CreateUnitVector(nodeCount);
            ContractValidationResult<double> initialNormalization = Normalize(
                coefficients,
                groupVelocitiesMPerSecond,
                currentGroup1,
                currentGroup2);
            if (!initialNormalization.IsValid)
            {
                return Invalid(initialNormalization.FirstDiagnostic);
            }

            double[] currentSourceShape = new double[nodeCount];
            if (!TryComputeFissionSourceShape(
                    coefficients,
                    currentGroup1,
                    currentGroup2,
                    currentSourceShape,
                    out ContractDiagnostic sourceShapeDiagnostic))
            {
                return Invalid(sourceShapeDiagnostic);
            }

            var sourceGroup1 = new double[nodeCount];
            var sourceGroup2 = new double[nodeCount];
            var rawGroup1 = new double[nodeCount];
            var rawGroup2 = new double[nodeCount];
            var normalizedGroup1 = new double[nodeCount];
            var normalizedGroup2 = new double[nodeCount];
            var innerCandidate = new double[nodeCount];
            var innerApplied = new double[nodeCount];
            var nextSourceShape = new double[nodeCount];
            var leftGroup1 = new double[nodeCount];
            var leftGroup2 = new double[nodeCount];

            double currentEigenvalue = referenceEigenvalue;
            double lastDeltaEigenvalueAbsolute = double.NaN;
            double lastDeltaEigenvalueRelative = double.NaN;
            double lastResidualAbsolute = double.NaN;
            double lastResidualRelative = double.NaN;
            double lastSourceShapeChange = double.NaN;

            for (int iteration = 1; iteration <= convergencePolicy.MaximumIterations; iteration++)
            {
                if (!TryBuildAdjointSources(
                        coefficients,
                        currentGroup1,
                        currentGroup2,
                        sourceGroup1,
                        sourceGroup2,
                        out ContractDiagnostic sourceDiagnostic))
                {
                    return Invalid(sourceDiagnostic);
                }

                if (!TrySolveLinear(
                        stencil,
                        operatorResult.Value,
                        SpatialEnergyGroup.Group2,
                        sourceGroup2,
                        group2DiagonalResult.Value,
                        rawGroup2,
                        innerCandidate,
                        innerApplied,
                        linearSolvePolicy,
                        out ContractDiagnostic group2Diagnostic))
                {
                    return Invalid(group2Diagnostic);
                }

                for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
                {
                    double downscatter = coefficients.Nodes[nodeIndex].DownscatterGroup1To2PerM *
                                         rawGroup2[nodeIndex];
                    double source = sourceGroup1[nodeIndex] + downscatter;
                    if (!IsValidImportance(downscatter) || !IsValidImportance(source))
                    {
                        return Invalid(
                            "SpatialAdjointSolve.Source.Invalid",
                            ContractValidation.NodePath(coefficients.Nodes[nodeIndex].Node, ".source.group1"),
                            "The transpose group 1 scatter source must be finite and nonnegative.");
                    }

                    sourceGroup1[nodeIndex] = source;
                }

                if (!TrySolveLinear(
                        stencil,
                        operatorResult.Value,
                        SpatialEnergyGroup.Group1,
                        sourceGroup1,
                        group1DiagonalResult.Value,
                        rawGroup1,
                        innerCandidate,
                        innerApplied,
                        linearSolvePolicy,
                        out ContractDiagnostic group1Diagnostic))
                {
                    return Invalid(group1Diagnostic);
                }

                ContractValidationResult<double> rawNormalization = Normalize(
                    coefficients,
                    groupVelocitiesMPerSecond,
                    rawGroup1,
                    rawGroup2);
                if (!rawNormalization.IsValid)
                {
                    return Invalid(rawNormalization.FirstDiagnostic);
                }

                double nextEigenvalue = rawNormalization.Value;
                if (!ContractValidation.IsFinite(nextEigenvalue) || nextEigenvalue <= 0.0)
                {
                    return Invalid(
                        "SpatialAdjointSolve.Eigenvalue.Invalid",
                        "convergence.eigenvalue",
                        "The transpose power iteration produced an invalid eigenvalue.");
                }

                // Normalize mutates the raw vectors in place and returns the
                // scale that was removed.  Do not divide by that scale again:
                // the resulting vector is already the unit-volume-speed
                // iterate used by the residual and the next power step.
                Array.Copy(rawGroup1, normalizedGroup1, nodeCount);
                Array.Copy(rawGroup2, normalizedGroup2, nodeCount);
                for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
                {
                    if (!IsValidImportance(normalizedGroup1[nodeIndex]) ||
                        !IsValidImportance(normalizedGroup2[nodeIndex]))
                    {
                        return Invalid(
                            "SpatialAdjointSolve.NormalizedImportance.Invalid",
                            ContractValidation.NodePath(stencil.Nodes[nodeIndex].Node, ".importance"),
                            "The normalized transpose importance must remain finite and nonnegative.");
                    }
                }

                if (!TryComputeTransposeResidual(
                        stencil,
                        coefficients,
                        operatorResult.Value,
                        normalizedGroup1,
                        normalizedGroup2,
                        nextEigenvalue,
                        leftGroup1,
                        leftGroup2,
                        out double residualAbsolute,
                        out double residualRelative,
                        out ContractDiagnostic residualDiagnostic))
                {
                    return Invalid(residualDiagnostic);
                }

                if (!TryComputeFissionSourceShape(
                        coefficients,
                        normalizedGroup1,
                        normalizedGroup2,
                        nextSourceShape,
                        out sourceShapeDiagnostic))
                {
                    return Invalid(sourceShapeDiagnostic);
                }

                double deltaEigenvalueAbsolute = Math.Abs(nextEigenvalue - currentEigenvalue);
                double eigenvalueScale = Math.Max(
                    Math.Abs(nextEigenvalue),
                    Math.Abs(currentEigenvalue));
                double deltaEigenvalueRelative = eigenvalueScale == 0.0
                    ? 0.0
                    : deltaEigenvalueAbsolute / eigenvalueScale;
                double sourceShapeChange = ComputeInfinityDifference(
                    currentSourceShape,
                    nextSourceShape);
                if (!ContractValidation.IsFinite(deltaEigenvalueAbsolute) ||
                    !ContractValidation.IsFinite(deltaEigenvalueRelative) ||
                    !ContractValidation.IsFinite(sourceShapeChange))
                {
                    return Invalid(
                        "SpatialAdjointSolve.Convergence.NonFinite",
                        "convergence",
                        "The transpose convergence diagnostics became non-finite.");
                }

                lastDeltaEigenvalueAbsolute = deltaEigenvalueAbsolute;
                lastDeltaEigenvalueRelative = deltaEigenvalueRelative;
                lastResidualAbsolute = residualAbsolute;
                lastResidualRelative = residualRelative;
                lastSourceShapeChange = sourceShapeChange;

                bool eigenvalueConverged =
                    deltaEigenvalueAbsolute <= convergencePolicy.KAbsoluteTolerance ||
                    deltaEigenvalueRelative <= convergencePolicy.KRelativeTolerance;
                if (eigenvalueConverged &&
                    residualRelative <= convergencePolicy.ResidualTolerance &&
                    sourceShapeChange <= convergencePolicy.SourceShapeTolerance)
                {
                    return ContractValidationResult<SpatialAdjointSolveResultV1>.Valid(
                        new SpatialAdjointSolveResultV1(
                            stencil,
                            CanonicalEnergyGroupOrder,
                            groupVelocitiesMPerSecond,
                            normalizedGroup1,
                            normalizedGroup2,
                            nextEigenvalue,
                            1.0,
                            iteration,
                            deltaEigenvalueAbsolute,
                            deltaEigenvalueRelative,
                            residualAbsolute,
                            residualRelative,
                            sourceShapeChange));
                }

                Array.Copy(normalizedGroup1, currentGroup1, nodeCount);
                Array.Copy(normalizedGroup2, currentGroup2, nodeCount);
                Array.Copy(nextSourceShape, currentSourceShape, nodeCount);
                currentEigenvalue = nextEigenvalue;
            }

            return Invalid(
                "SpatialAdjointSolve.Nonconverged",
                "convergence",
                "The transpose adjoint solve exhausted its caller-supplied iteration limit " +
                "(iterations=" + convergencePolicy.MaximumIterations.ToString(CultureInfo.InvariantCulture) +
                ", delta_k=" + Format(lastDeltaEigenvalueRelative) +
                ", residual=" + Format(lastResidualRelative) +
                ", source_shape=" + Format(lastSourceShapeChange) + ").");
        }

        private static bool TryBuildAdjointSources(
            SpatialCoefficientSet coefficients,
            double[] group1Importance,
            double[] group2Importance,
            double[] destinationGroup1,
            double[] destinationGroup2,
            out ContractDiagnostic diagnostic)
        {
            for (int nodeIndex = 0; nodeIndex < coefficients.NodeCount; nodeIndex++)
            {
                SpatialNodeCoefficients node = coefficients.Nodes[nodeIndex];
                double fissionImportance =
                    node.ChiGroup1 * group1Importance[nodeIndex] +
                    node.ChiGroup2 * group2Importance[nodeIndex];
                double group1 = node.NuFissionGroup1PerM * fissionImportance;
                double group2 = node.NuFissionGroup2PerM * fissionImportance;
                if (!IsValidImportance(fissionImportance) ||
                    !IsValidImportance(group1) ||
                    !IsValidImportance(group2))
                {
                    diagnostic = new ContractDiagnostic(
                        "SpatialAdjointSolve.Source.Invalid",
                        ContractValidation.NodePath(node.Node, ".source"),
                        "Transpose fission/scatter sources must be finite and nonnegative.");
                    return false;
                }

                destinationGroup1[nodeIndex] = group1;
                destinationGroup2[nodeIndex] = group2;
            }

            diagnostic = null!;
            return true;
        }

        private static bool TrySolveLinear(
            SpatialStencil stencil,
            SpatialOperator spatialOperator,
            SpatialEnergyGroup group,
            double[] source,
            double[] diagonal,
            double[] solution,
            double[] candidate,
            double[] applied,
            SpatialLinearSolvePolicy policy,
            out ContractDiagnostic diagnostic)
        {
            Array.Clear(solution, 0, solution.Length);
            for (int nodeIndex = 0; nodeIndex < source.Length; nodeIndex++)
            {
                if (!IsValidImportance(source[nodeIndex]) ||
                    !ContractValidation.IsFinite(diagonal[nodeIndex]) || diagonal[nodeIndex] <= 0.0)
                {
                    diagnostic = new ContractDiagnostic(
                        "SpatialAdjointSolve.InnerSource.Invalid",
                        ContractValidation.NodePath(stencil.Nodes[nodeIndex].Node, ".inner_source"),
                        "The transpose inner source and diagonal must be finite and valid.");
                    return false;
                }
            }

            for (int innerIteration = 0;
                 innerIteration < policy.MaximumInnerIterations;
                 innerIteration++)
            {
                if (!spatialOperator.TryApplyTranspose(
                        group,
                        solution,
                        applied,
                        out diagnostic))
                {
                    return false;
                }

                for (int nodeIndex = 0; nodeIndex < source.Length; nodeIndex++)
                {
                    double correction = (source[nodeIndex] - applied[nodeIndex]) / diagonal[nodeIndex];
                    double next = solution[nodeIndex] + correction;
                    if (!ContractValidation.IsFinite(correction) ||
                        !IsValidImportance(next))
                    {
                        diagnostic = new ContractDiagnostic(
                            "SpatialAdjointSolve.InnerImportance.Invalid",
                            ContractValidation.NodePath(stencil.Nodes[nodeIndex].Node, ".inner_importance"),
                            "The deterministic transpose Jacobi update produced a non-finite or negative importance.");
                        return false;
                    }

                    candidate[nodeIndex] = next;
                }

                if (!spatialOperator.TryApplyTranspose(
                        group,
                        candidate,
                        applied,
                        out diagnostic))
                {
                    return false;
                }

                double absoluteResidual = 0.0;
                double scale = 0.0;
                for (int nodeIndex = 0; nodeIndex < source.Length; nodeIndex++)
                {
                    double difference = applied[nodeIndex] - source[nodeIndex];
                    double absoluteDifference = Math.Abs(difference);
                    double rowScale = Math.Abs(applied[nodeIndex]) + Math.Abs(source[nodeIndex]);
                    if (!ContractValidation.IsFinite(absoluteDifference) ||
                        !ContractValidation.IsFinite(rowScale))
                    {
                        diagnostic = new ContractDiagnostic(
                            "SpatialAdjointSolve.InnerResidual.NonFinite",
                            ContractValidation.NodePath(stencil.Nodes[nodeIndex].Node, ".inner_residual"),
                            "The transpose inner residual became non-finite.");
                        return false;
                    }

                    absoluteResidual = Math.Max(absoluteResidual, absoluteDifference);
                    scale = Math.Max(scale, rowScale);
                }

                double relativeResidual = scale == 0.0
                    ? 0.0
                    : absoluteResidual / scale;
                if (!ContractValidation.IsFinite(relativeResidual))
                {
                    diagnostic = new ContractDiagnostic(
                        "SpatialAdjointSolve.InnerResidual.NonFinite",
                        "linear_solve",
                        "The transpose inner relative residual became non-finite.");
                    return false;
                }

                if (absoluteResidual <= policy.AbsoluteResidualTolerance ||
                    relativeResidual <= policy.RelativeResidualTolerance)
                {
                    Array.Copy(candidate, solution, solution.Length);
                    diagnostic = null!;
                    return true;
                }

                Array.Copy(candidate, solution, solution.Length);
            }

            diagnostic = new ContractDiagnostic(
                "SpatialAdjointSolve.InnerSolve.Nonconverged",
                group == SpatialEnergyGroup.Group1
                    ? "linear_solve.group1"
                    : "linear_solve.group2",
                "The deterministic transpose inner solve exhausted its caller-supplied iteration limit.");
            return false;
        }

        private static bool TryComputeTransposeResidual(
            SpatialStencil stencil,
            SpatialCoefficientSet coefficients,
            SpatialOperator spatialOperator,
            double[] group1Importance,
            double[] group2Importance,
            double eigenvalue,
            double[] leftGroup1,
            double[] leftGroup2,
            out double absoluteResidual,
            out double relativeResidual,
            out ContractDiagnostic diagnostic)
        {
            if (!spatialOperator.TryApplyTranspose(
                    SpatialEnergyGroup.Group1,
                    group1Importance,
                    leftGroup1,
                    out diagnostic) ||
                !spatialOperator.TryApplyTranspose(
                    SpatialEnergyGroup.Group2,
                    group2Importance,
                    leftGroup2,
                    out diagnostic))
            {
                absoluteResidual = double.NaN;
                relativeResidual = double.NaN;
                return false;
            }

            absoluteResidual = 0.0;
            double scale = 0.0;
            double inverseEigenvalue = 1.0 / eigenvalue;
            if (!ContractValidation.IsFinite(inverseEigenvalue))
            {
                diagnostic = new ContractDiagnostic(
                    "SpatialAdjointSolve.Residual.Eigenvalue.Invalid",
                    "convergence.eigenvalue",
                    "The inverse eigenvalue used by the transpose residual is invalid.");
                relativeResidual = double.NaN;
                return false;
            }

            for (int nodeIndex = 0; nodeIndex < stencil.NodeCount; nodeIndex++)
            {
                SpatialNodeCoefficients node = coefficients.Nodes[nodeIndex];
                double fissionImportance =
                    node.ChiGroup1 * group1Importance[nodeIndex] +
                    node.ChiGroup2 * group2Importance[nodeIndex];
                double group1Right =
                    node.NuFissionGroup1PerM * fissionImportance * inverseEigenvalue +
                    node.DownscatterGroup1To2PerM * group2Importance[nodeIndex];
                double group2Right =
                    node.NuFissionGroup2PerM * fissionImportance * inverseEigenvalue;
                double group1Difference = leftGroup1[nodeIndex] - group1Right;
                double group2Difference = leftGroup2[nodeIndex] - group2Right;
                double group1Scale = Math.Abs(leftGroup1[nodeIndex]) + Math.Abs(group1Right);
                double group2Scale = Math.Abs(leftGroup2[nodeIndex]) + Math.Abs(group2Right);
                if (!ContractValidation.IsFinite(group1Right) || group1Right < 0.0 ||
                    !ContractValidation.IsFinite(group2Right) || group2Right < 0.0 ||
                    !ContractValidation.IsFinite(group1Difference) ||
                    !ContractValidation.IsFinite(group2Difference) ||
                    !ContractValidation.IsFinite(group1Scale) ||
                    !ContractValidation.IsFinite(group2Scale))
                {
                    diagnostic = new ContractDiagnostic(
                        "SpatialAdjointSolve.Residual.NonFinite",
                        ContractValidation.NodePath(node.Node, ".residual"),
                        "The transpose residual terms must remain finite and nonnegative on the right-hand side.");
                    relativeResidual = double.NaN;
                    return false;
                }

                absoluteResidual = Math.Max(
                    absoluteResidual,
                    Math.Max(Math.Abs(group1Difference), Math.Abs(group2Difference)));
                scale = Math.Max(scale, Math.Max(group1Scale, group2Scale));
            }

            relativeResidual = scale == 0.0
                ? 0.0
                : absoluteResidual / scale;
            if (!ContractValidation.IsFinite(absoluteResidual) ||
                !ContractValidation.IsFinite(relativeResidual))
            {
                diagnostic = new ContractDiagnostic(
                    "SpatialAdjointSolve.Residual.NonFinite",
                    "convergence.residual",
                    "The transpose residual reduction became non-finite.");
                return false;
            }

            diagnostic = null!;
            return true;
        }

        private static bool TryComputeFissionSourceShape(
            SpatialCoefficientSet coefficients,
            double[] group1Importance,
            double[] group2Importance,
            double[] destination,
            out ContractDiagnostic diagnostic)
        {
            double total = 0.0;
            for (int nodeIndex = 0; nodeIndex < coefficients.NodeCount; nodeIndex++)
            {
                SpatialNodeCoefficients node = coefficients.Nodes[nodeIndex];
                double fissionImportance =
                    node.ChiGroup1 * group1Importance[nodeIndex] +
                    node.ChiGroup2 * group2Importance[nodeIndex];
                double contribution = node.VolumeM3 * fissionImportance;
                if (!IsValidImportance(fissionImportance) ||
                    !ContractValidation.IsFinite(contribution) || contribution < 0.0)
                {
                    diagnostic = new ContractDiagnostic(
                        "SpatialAdjointSolve.SourceShape.Invalid",
                        ContractValidation.NodePath(node.Node, ".source_shape"),
                        "The normalized transpose source shape must be finite and nonnegative.");
                    return false;
                }

                destination[nodeIndex] = contribution;
                total += contribution;
                if (!ContractValidation.IsFinite(total))
                {
                    diagnostic = new ContractDiagnostic(
                        "SpatialAdjointSolve.SourceShape.NonFinite",
                        "source_shape",
                        "The transpose source-shape reduction became non-finite.");
                    return false;
                }
            }

            if (!ContractValidation.IsFinite(total) || total <= 0.0)
            {
                diagnostic = new ContractDiagnostic(
                    "SpatialAdjointSolve.SourceShape.Zero",
                    "source_shape",
                    "The transpose adjoint source shape must have strictly positive finite total importance.");
                return false;
            }

            for (int nodeIndex = 0; nodeIndex < destination.Length; nodeIndex++)
            {
                destination[nodeIndex] /= total;
                if (!IsValidImportance(destination[nodeIndex]))
                {
                    diagnostic = new ContractDiagnostic(
                        "SpatialAdjointSolve.SourceShape.Invalid",
                        "source_shape",
                        "The normalized transpose source shape must be finite and nonnegative.");
                    return false;
                }
            }

            diagnostic = null!;
            return true;
        }

        private static ContractValidationResult<double> Normalize(
            SpatialCoefficientSet coefficients,
            IReadOnlyList<double> groupVelocitiesMPerSecond,
            double[] group1Importance,
            double[] group2Importance)
        {
            double normalization = 0.0;
            for (int nodeIndex = 0; nodeIndex < coefficients.NodeCount; nodeIndex++)
            {
                SpatialNodeCoefficients node = coefficients.Nodes[nodeIndex];
                if (!IsValidImportance(group1Importance[nodeIndex]) ||
                    !IsValidImportance(group2Importance[nodeIndex]))
                {
                    return ContractValidationResult<double>.Invalid(
                        "SpatialAdjointSolve.Importance.Invalid",
                        ContractValidation.NodePath(node.Node, ".importance"),
                        "Adjoint importance values must be finite and nonnegative before normalization.");
                }

                double contribution = node.VolumeM3 * (
                    group1Importance[nodeIndex] / groupVelocitiesMPerSecond[0] +
                    group2Importance[nodeIndex] / groupVelocitiesMPerSecond[1]);
                if (!ContractValidation.IsFinite(contribution) || contribution < 0.0)
                {
                    return ContractValidationResult<double>.Invalid(
                        "SpatialAdjointSolve.Normalization.NonFinite",
                        ContractValidation.NodePath(node.Node, ".normalization"),
                        "The adjoint volume-speed normalization contribution must be finite and nonnegative.");
                }

                normalization += contribution;
                if (!ContractValidation.IsFinite(normalization))
                {
                    return ContractValidationResult<double>.Invalid(
                        "SpatialAdjointSolve.Normalization.NonFinite",
                        "normalization",
                        "The adjoint volume-speed normalization became non-finite.");
                }
            }

            if (!ContractValidation.IsFinite(normalization) || normalization <= 0.0)
            {
                return ContractValidationResult<double>.Invalid(
                    "SpatialAdjointSolve.Normalization.Invalid",
                    "normalization",
                    "The adjoint volume-speed normalization must be finite and strictly positive.");
            }

            for (int nodeIndex = 0; nodeIndex < coefficients.NodeCount; nodeIndex++)
            {
                group1Importance[nodeIndex] /= normalization;
                group2Importance[nodeIndex] /= normalization;
                if (!IsValidImportance(group1Importance[nodeIndex]) ||
                    !IsValidImportance(group2Importance[nodeIndex]))
                {
                    return ContractValidationResult<double>.Invalid(
                        "SpatialAdjointSolve.NormalizedImportance.Invalid",
                        ContractValidation.NodePath(coefficients.Nodes[nodeIndex].Node, ".importance"),
                        "The normalized adjoint importance must be finite and nonnegative.");
                }
            }

            return ContractValidationResult<double>.Valid(normalization);
        }

        private static ContractValidationResult<double[]> BuildDiagonal(
            SpatialStencil stencil,
            SpatialCoefficientSet coefficients,
            SpatialEnergyGroup group)
        {
            var diagonal = new double[stencil.NodeCount];
            for (int nodeIndex = 0; nodeIndex < stencil.NodeCount; nodeIndex++)
            {
                SpatialNodeStencil stencilNode = stencil.Nodes[nodeIndex];
                SpatialNodeCoefficients node = coefficients.Nodes[nodeIndex];
                double removal = group == SpatialEnergyGroup.Group1
                    ? node.AbsorptionGroup1PerM + node.DownscatterGroup1To2PerM
                    : node.AbsorptionGroup2PerM;
                double conductanceSum = 0.0;
                foreach (SpatialNeighborTerm neighbor in stencilNode.NeighborTerms)
                {
                    if (!coefficients.TryGetEdge(
                            new SpatialEdgeKey(stencilNode.Node, neighbor.TargetNode),
                            out SpatialConductancePair conductance))
                    {
                        return ContractValidationResult<double[]>.Invalid(
                            "SpatialAdjointSolve.EdgeBinding.Missing",
                            ContractValidation.NodePath(stencilNode.Node, ".neighbors"),
                            "Every transpose stencil neighbor must have a bound conductance.");
                    }

                    conductanceSum += group == SpatialEnergyGroup.Group1
                        ? conductance.Group1
                        : conductance.Group2;
                }

                foreach (SpatialBoundaryTerm boundary in stencilNode.BoundaryTerms)
                {
                    if (!coefficients.TryGetBoundary(
                            new SpatialBoundaryKey(stencilNode.Node, boundary.Face),
                            out SpatialConductancePair conductance))
                    {
                        return ContractValidationResult<double[]>.Invalid(
                            "SpatialAdjointSolve.BoundaryBinding.Missing",
                            ContractValidation.NodePath(stencilNode.Node, ".boundary_faces"),
                            "Every transpose stencil boundary must have a bound conductance.");
                    }

                    conductanceSum += group == SpatialEnergyGroup.Group1
                        ? conductance.Group1
                        : conductance.Group2;
                }

                double value = removal + conductanceSum / node.VolumeM3;
                if (!ContractValidation.IsFinite(value) || value <= 0.0)
                {
                    return ContractValidationResult<double[]>.Invalid(
                        "SpatialAdjointSolve.Diagonal.Invalid",
                        ContractValidation.NodePath(stencilNode.Node, ".diagonal"),
                        "Every transpose Jacobi diagonal must be finite and strictly positive.");
                }

                diagonal[nodeIndex] = value;
            }

            return ContractValidationResult<double[]>.Valid(diagonal);
        }

        private static ContractValidationResult<bool> ValidateGroupIdentity(
            IReadOnlyList<string> groupOrder,
            string path)
        {
            if (groupOrder == null || groupOrder.Count != 2 ||
                !string.Equals(groupOrder[0], "fast", StringComparison.Ordinal) ||
                !string.Equals(groupOrder[1], "thermal", StringComparison.Ordinal))
            {
                return ContractValidationResult<bool>.Invalid(
                    "SpatialAdjointSolve.EnergyGroupOrder.Unsupported",
                    path,
                    "The reference adjoint requires the canonical [fast, thermal] energy-group order.");
            }

            return ContractValidationResult<bool>.Valid(true);
        }

        private static double[] CreateUnitVector(int nodeCount)
        {
            var values = new double[nodeCount];
            for (int nodeIndex = 0; nodeIndex < values.Length; nodeIndex++)
            {
                values[nodeIndex] = 1.0;
            }

            return values;
        }

        private static double ComputeInfinityDifference(
            double[] left,
            double[] right)
        {
            double maximum = 0.0;
            for (int index = 0; index < left.Length; index++)
            {
                maximum = Math.Max(maximum, Math.Abs(left[index] - right[index]));
            }

            return maximum;
        }

        private static bool IsValidImportance(double value)
        {
            return ContractValidation.IsFinite(value) && value >= 0.0;
        }

        private static string Format(double value)
        {
            return ContractValidation.IsFinite(value)
                ? value.ToString("R", CultureInfo.InvariantCulture)
                : "unavailable";
        }

        private static ContractValidationResult<SpatialAdjointSolveResultV1> Invalid(
            ContractDiagnostic diagnostic)
        {
            return ContractValidationResult<SpatialAdjointSolveResultV1>.Invalid(
                diagnostic.Code,
                diagnostic.Path,
                diagnostic.Message);
        }

        private static ContractValidationResult<SpatialAdjointSolveResultV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<SpatialAdjointSolveResultV1>.Invalid(
                code,
                path,
                message);
        }
    }

    /// <summary>
    /// Immutable converged result of the deterministic two-group transpose
    /// eigen solve.  The arrays are kept separate from the full-core binding
    /// so the numerical contract can be tested on a controlled fixture.
    /// </summary>
    public sealed class SpatialAdjointSolveResultV1
    {
        private readonly ReadOnlyCollection<string> _energyGroupOrder;
        private readonly ReadOnlyCollection<double> _groupVelocitiesMPerSecond;
        private readonly ReadOnlyCollection<double> _group1Importance;
        private readonly ReadOnlyCollection<double> _group2Importance;
        private readonly bool _isConverged;
        private readonly string _solverIdentity;
        private readonly string _normalizationIdentity;

        internal SpatialAdjointSolveResultV1(
            SpatialStencil stencil,
            IEnumerable<string> energyGroupOrder,
            IEnumerable<double> groupVelocitiesMPerSecond,
            IEnumerable<double> group1Importance,
            IEnumerable<double> group2Importance,
            double eigenvalue,
            double normalizationValue,
            int iterationCount,
            double eigenvalueChangeAbsolute,
            double eigenvalueChangeRelative,
            double residualAbsoluteInfinity,
            double residualRelativeInfinity,
            double sourceShapeChangeInfinity)
        {
            Stencil = stencil;
            _energyGroupOrder = new ReadOnlyCollection<string>(energyGroupOrder.ToArray());
            _groupVelocitiesMPerSecond = new ReadOnlyCollection<double>(
                groupVelocitiesMPerSecond.ToArray());
            _group1Importance = new ReadOnlyCollection<double>(group1Importance.ToArray());
            _group2Importance = new ReadOnlyCollection<double>(group2Importance.ToArray());
            Eigenvalue = eigenvalue;
            NormalizationValue = normalizationValue;
            IterationCount = iterationCount;
            EigenvalueChangeAbsolute = eigenvalueChangeAbsolute;
            EigenvalueChangeRelative = eigenvalueChangeRelative;
            ResidualAbsoluteInfinity = residualAbsoluteInfinity;
            ResidualRelativeInfinity = residualRelativeInfinity;
            SourceShapeChangeInfinity = sourceShapeChangeInfinity;
            _isConverged = true;
            _solverIdentity = SpatialAdjointEigenSolve.SolverIdentity;
            _normalizationIdentity = SpatialAdjointEigenSolve.NormalizationIdentity;
        }

        public SpatialStencil Stencil { get; }

        public int NodeCount
        {
            get { return _group1Importance.Count; }
        }

        public IReadOnlyList<string> EnergyGroupOrder
        {
            get { return _energyGroupOrder; }
        }

        public IReadOnlyList<double> GroupVelocitiesMPerSecond
        {
            get { return _groupVelocitiesMPerSecond; }
        }

        public IReadOnlyList<double> Group1Importance
        {
            get { return _group1Importance; }
        }

        public IReadOnlyList<double> Group2Importance
        {
            get { return _group2Importance; }
        }

        public double Eigenvalue { get; }

        public double NormalizationValue { get; }

        public int IterationCount { get; }

        public double EigenvalueChangeAbsolute { get; }

        public double EigenvalueChangeRelative { get; }

        public double ResidualAbsoluteInfinity { get; }

        public double ResidualRelativeInfinity { get; }

        public double SourceShapeChangeInfinity { get; }

        public bool IsConverged
        {
            get { return _isConverged; }
        }

        public string SolverIdentity
        {
            get { return _solverIdentity; }
        }

        public string NormalizationIdentity
        {
            get { return _normalizationIdentity; }
        }
    }

    /// <summary>
    /// Reference importance field bound to one full-core topology, the
    /// project-authored diffusion and kinetics identities, and one reference
    /// bundle state.  It is immutable and carries a digest over all metadata,
    /// diagnostics, and canonical node-order values.
    /// </summary>
    public sealed class FullCoreAdjointImportanceV1
    {
        public const uint CurrentSchemaVersion = 1;
        public const string SchemaId = "candu6-two-group-reference-adjoint-v1";

        private readonly ReadOnlyCollection<double> _group1Importance;
        private readonly ReadOnlyCollection<double> _group2Importance;
        private readonly ReadOnlyCollection<string> _energyGroupOrder;
        private readonly string _energyGroupOrderIdentity;
        private readonly string _normalizationIdentity;

        private FullCoreAdjointImportanceV1(
            FullCoreDiffusionDataPackV1 diffusionDataPack,
            IqsKineticsDataPackV1 kineticsDataPack,
            CoreTopology topology,
            SpatialStencil stencil,
            Digest32 referenceStateDigest,
            SpatialAdjointSolveResultV1 solve,
            Digest32 digest)
        {
            DiffusionDataPack = diffusionDataPack;
            KineticsDataPack = kineticsDataPack;
            Topology = topology;
            Stencil = stencil;
            ReferenceStateDigest = referenceStateDigest;
            _energyGroupOrder = new ReadOnlyCollection<string>(
                solve.EnergyGroupOrder.ToArray());
            _group1Importance = new ReadOnlyCollection<double>(
                solve.Group1Importance.ToArray());
            _group2Importance = new ReadOnlyCollection<double>(
                solve.Group2Importance.ToArray());
            _energyGroupOrderIdentity = SpatialAdjointEigenSolve.EnergyGroupOrderIdentity;
            _normalizationIdentity = solve.NormalizationIdentity;
            ReferenceEigenvalue = solve.Eigenvalue;
            NormalizationValue = solve.NormalizationValue;
            IterationCount = solve.IterationCount;
            EigenvalueChangeAbsolute = solve.EigenvalueChangeAbsolute;
            EigenvalueChangeRelative = solve.EigenvalueChangeRelative;
            TransposeResidualAbsoluteInfinity = solve.ResidualAbsoluteInfinity;
            TransposeResidualRelativeInfinity = solve.ResidualRelativeInfinity;
            SourceShapeChangeInfinity = solve.SourceShapeChangeInfinity;
            Digest = digest;
        }

        public FullCoreDiffusionDataPackV1 DiffusionDataPack { get; }

        public IqsKineticsDataPackV1 KineticsDataPack { get; }

        public CoreTopology Topology { get; }

        public SpatialStencil Stencil { get; }

        public Digest32 ReferenceStateDigest { get; }

        public string TopologySchemaId
        {
            get { return DiffusionDataPack.Descriptor.TopologySchemaId; }
        }

        public uint ChannelCount
        {
            get { return DiffusionDataPack.Descriptor.ChannelCount; }
        }

        public uint BundlePositionCount
        {
            get { return DiffusionDataPack.Descriptor.BundlePositionCount; }
        }

        public int NodeCount
        {
            get { return _group1Importance.Count; }
        }

        public string UnitsProfileId
        {
            get { return DiffusionDataPack.Descriptor.UnitsProfileId; }
        }

        public IReadOnlyList<string> EnergyGroupOrder
        {
            get { return _energyGroupOrder; }
        }

        public string EnergyGroupOrderIdentity
        {
            get { return _energyGroupOrderIdentity; }
        }

        public string DiffusionDataPackVersion
        {
            get { return DiffusionDataPack.Descriptor.DataPackVersion; }
        }

        public string KineticsDataPackVersion
        {
            get { return KineticsDataPack.DataPackVersion; }
        }

        public Digest32 TopologyDigest
        {
            get { return new Digest32(DiffusionDataPack.Descriptor.TopologyDigest.ToArray()); }
        }

        public Digest32 DiffusionDataPackDigest
        {
            get { return new Digest32(DiffusionDataPack.Descriptor.ContentDigest.ToArray()); }
        }

        public Digest32 KineticsDataPackDigest
        {
            get { return KineticsDataPack.ContentDigest; }
        }

        /// <summary>
        /// Compatibility alias for callers that have one active spatial pack.
        /// The more specific diffusion and kinetics digests remain available.
        /// </summary>
        public Digest32 DataPackDigest
        {
            get { return DiffusionDataPackDigest; }
        }

        public string SolverIdentity
        {
            get
            {
                return SpatialAdjointEigenSolve.SolverIdentity + "/" +
                       DiffusionDataPack.SolverId + "/" +
                       DiffusionDataPack.Descriptor.DataPackVersion;
            }
        }

        public string NormalizationIdentity
        {
            get { return _normalizationIdentity; }
        }

        public double NormalizationValue { get; }

        public IReadOnlyList<double> Group1Importance
        {
            get { return _group1Importance; }
        }

        public IReadOnlyList<double> Group2Importance
        {
            get { return _group2Importance; }
        }

        public double ReferenceEigenvalue { get; }

        public int IterationCount { get; }

        public double EigenvalueChangeAbsolute { get; }

        public double EigenvalueChangeRelative { get; }

        public double TransposeResidualAbsoluteInfinity { get; }

        public double TransposeResidualRelativeInfinity { get; }

        public double SourceShapeChangeInfinity { get; }

        public Digest32 Digest { get; }

        public string DigestHex
        {
            get { return ToHex(Digest); }
        }

        public static ContractValidationResult<FullCoreAdjointImportanceV1> TryCreate(
            FullCoreDiffusionDataPackV1 diffusionDataPack,
            IqsKineticsDataPackV1 kineticsDataPack,
            CoreTopology topology,
            SpatialStencil stencil,
            Digest32 referenceStateDigest,
            SpatialAdjointSolveResultV1 solve,
            Digest32? expectedDigest = null)
        {
            if (diffusionDataPack == null)
            {
                return Invalid(
                    "FullCoreAdjoint.DataPack.Missing",
                    "diffusion_data_pack",
                    "A reference adjoint requires a validated diffusion data pack.");
            }

            if (kineticsDataPack == null)
            {
                return Invalid(
                    "FullCoreAdjoint.KineticsDataPack.Missing",
                    "kinetics_data_pack",
                    "A reference adjoint requires the validated ordered kinetics pack.");
            }

            if (topology == null)
            {
                return Invalid(
                    "FullCoreAdjoint.Topology.Missing",
                    "topology",
                    "A reference adjoint requires a validated full-core topology.");
            }

            if (stencil == null)
            {
                return Invalid(
                    "FullCoreAdjoint.Stencil.Missing",
                    "stencil",
                    "A reference adjoint requires the topology's assembled stencil.");
            }

            if (!ReferenceEquals(stencil.Topology, topology))
            {
                return Invalid(
                    "FullCoreAdjoint.Topology.BindingMismatch",
                    "stencil",
                    "The reference adjoint stencil must be assembled from the supplied topology instance.");
            }

            if (referenceStateDigest == null)
            {
                return Invalid(
                    "FullCoreAdjoint.ReferenceStateDigest.Missing",
                    "reference_state_digest",
                    "A reference adjoint requires a deterministic reference-state digest.");
            }

            if (solve == null)
            {
                return Invalid(
                    "FullCoreAdjoint.Solve.Missing",
                    "solve",
                    "A reference adjoint requires a converged transpose solve result.");
            }

            if (!solve.IsConverged ||
                !string.Equals(
                    solve.SolverIdentity,
                    SpatialAdjointEigenSolve.SolverIdentity,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    solve.NormalizationIdentity,
                    SpatialAdjointEigenSolve.NormalizationIdentity,
                    StringComparison.Ordinal))
            {
                return Invalid(
                    "FullCoreAdjoint.Solve.IdentityMismatch",
                    "solve",
                    "The reference adjoint must come from the converged deterministic transpose solver and normalization identity.");
            }

            ContractValidationResult<bool> compatibility =
                diffusionDataPack.Descriptor.ValidateCompatibility(topology);
            if (!compatibility.IsValid)
            {
                return Invalid(compatibility.FirstDiagnostic);
            }

            Digest32 canonicalTopologyDigest = new Digest32(
                Phase5CanonicalBytesV1.Sha256(
                    Encoding.UTF8.GetBytes(Candu6CoreTopologyFactoryV1.GetTopologyIdentity())));
            if (!canonicalTopologyDigest.Equals(
                    new Digest32(diffusionDataPack.Descriptor.TopologyDigest.ToArray())))
            {
                return Invalid(
                    "FullCoreAdjoint.TopologyDigest.Mismatch",
                    "diffusion_data_pack.descriptor.topology_digest",
                    "The diffusion data-pack topology digest does not match the canonical CANDU-6 topology identity.");
            }

            if (!canonicalTopologyDigest.Equals(kineticsDataPack.TopologyDigest))
            {
                return Invalid(
                    "FullCoreAdjoint.KineticsTopologyDigest.Mismatch",
                    "kinetics_data_pack.topology_digest",
                    "The kinetics data-pack topology digest does not match the canonical CANDU-6 topology identity.");
            }

            if (!string.Equals(
                    diffusionDataPack.Descriptor.TopologySchemaId,
                    Candu6CoreTopologyFactoryV1.TopologySchemaId,
                    StringComparison.Ordinal) ||
                topology.ChannelCount != Candu6CoreTopologyFactoryV1.ChannelCount ||
                topology.BundlePositionCount != Candu6CoreTopologyFactoryV1.BundlePositionCount ||
                stencil.NodeCount != topology.SlotCount ||
                solve.Stencil == null ||
                !ReferenceEquals(solve.Stencil, stencil))
            {
                return Invalid(
                    "FullCoreAdjoint.Topology.Incompatible",
                    "topology",
                    "The reference adjoint requires the canonical CANDU-6 topology and exact stencil binding.");
            }

            ContractValidationResult<bool> groupIdentity = ValidateGroupIdentity(
                diffusionDataPack.EnergyGroupOrder,
                "diffusion_data_pack.energy_group_order");
            if (!groupIdentity.IsValid)
            {
                return Invalid(groupIdentity.FirstDiagnostic);
            }

            groupIdentity = ValidateGroupIdentity(
                kineticsDataPack.EnergyGroupOrder,
                "kinetics_data_pack.energy_group_order");
            if (!groupIdentity.IsValid)
            {
                return Invalid(groupIdentity.FirstDiagnostic);
            }

            if (!diffusionDataPack.EnergyGroupOrder.SequenceEqual(
                    kineticsDataPack.EnergyGroupOrder,
                    StringComparer.Ordinal) ||
                !solve.EnergyGroupOrder.SequenceEqual(
                    kineticsDataPack.EnergyGroupOrder,
                    StringComparer.Ordinal))
            {
                return Invalid(
                    "FullCoreAdjoint.EnergyGroupOrder.Mismatch",
                    "energy_group_order",
                    "The reference adjoint, diffusion pack, and kinetics pack must share the exact ordered groups.");
            }

            if (!solve.GroupVelocitiesMPerSecond.SequenceEqual(
                    kineticsDataPack.GroupVelocitiesMPerSecond) ||
                solve.NodeCount != stencil.NodeCount ||
                solve.Group1Importance.Count != stencil.NodeCount ||
                solve.Group2Importance.Count != stencil.NodeCount)
            {
                return Invalid(
                    "FullCoreAdjoint.Dimensions.Mismatch",
                    "importance",
                    "Reference adjoint dimensions and group velocities must match the canonical stencil and kinetics pack.");
            }

            if (!ContractValidation.IsFinite(solve.Eigenvalue) || solve.Eigenvalue <= 0.0 ||
                !ContractValidation.IsFinite(solve.NormalizationValue) ||
                solve.NormalizationValue <= 0.0 ||
                Math.Abs(solve.NormalizationValue - 1.0) > 1.0e-12 ||
                solve.IterationCount <= 0 ||
                !ContractValidation.IsFinite(solve.EigenvalueChangeAbsolute) ||
                !ContractValidation.IsFinite(solve.EigenvalueChangeRelative) ||
                !ContractValidation.IsFinite(solve.ResidualAbsoluteInfinity) ||
                !ContractValidation.IsFinite(solve.ResidualRelativeInfinity) ||
                !ContractValidation.IsFinite(solve.SourceShapeChangeInfinity) ||
                solve.ResidualRelativeInfinity < 0.0 ||
                solve.SourceShapeChangeInfinity < 0.0)
            {
                return Invalid(
                    "FullCoreAdjoint.Diagnostics.Invalid",
                    "solve.diagnostics",
                    "Reference adjoint iteration, normalization, and residual diagnostics must be finite and valid.");
            }

            if (solve.ResidualRelativeInfinity > 1.0 ||
                solve.SourceShapeChangeInfinity > 1.0)
            {
                return Invalid(
                    "FullCoreAdjoint.Diagnostics.Unbounded",
                    "solve.diagnostics",
                    "Reference adjoint residual and source-shape diagnostics must be bounded by one.");
            }

            double normalizedConstraint = ComputeNormalization(
                diffusionDataPack.NodeVolumeM3,
                solve.GroupVelocitiesMPerSecond,
                solve.Group1Importance,
                solve.Group2Importance);
            if (!ContractValidation.IsFinite(normalizedConstraint) || normalizedConstraint <= 0.0 ||
                Math.Abs(normalizedConstraint - solve.NormalizationValue) >
                    1e-9 * Math.Max(1.0, Math.Abs(solve.NormalizationValue)))
            {
                return Invalid(
                    "FullCoreAdjoint.Normalization.Mismatch",
                    "normalization",
                    "The stored reference adjoint normalization does not match the canonical field values.");
            }

            for (int nodeIndex = 0; nodeIndex < solve.NodeCount; nodeIndex++)
            {
                if (!ContractValidation.IsFinite(solve.Group1Importance[nodeIndex]) ||
                    !ContractValidation.IsFinite(solve.Group2Importance[nodeIndex]) ||
                    solve.Group1Importance[nodeIndex] < 0.0 ||
                    solve.Group2Importance[nodeIndex] < 0.0)
                {
                    return Invalid(
                        "FullCoreAdjoint.Importance.Invalid",
                        ContractValidation.NodePath(stencil.Nodes[nodeIndex].Node, ".importance"),
                        "Reference adjoint importance values must be finite and nonnegative.");
                }
            }

            Digest32 digest = ComputeDigest(
                diffusionDataPack,
                kineticsDataPack,
                topology,
                referenceStateDigest,
                solve);
            if (expectedDigest != null && !digest.Equals(expectedDigest))
            {
                return Invalid(
                    "FullCoreAdjoint.Digest.Mismatch",
                    "digest",
                    "The supplied reference adjoint digest does not match its canonical metadata and field values.");
            }

            return ContractValidationResult<FullCoreAdjointImportanceV1>.Valid(
                new FullCoreAdjointImportanceV1(
                    diffusionDataPack,
                    kineticsDataPack,
                    topology,
                    stencil,
                    referenceStateDigest,
                    solve,
                    digest));
        }

        public ContractValidationResult<bool> ValidateDigest(Digest32 expectedDigest)
        {
            if (expectedDigest == null)
            {
                return ContractValidationResult<bool>.Invalid(
                    "FullCoreAdjoint.Digest.Missing",
                    "digest",
                    "An expected reference adjoint digest is required.");
            }

            if (!Digest.Equals(expectedDigest))
            {
                return ContractValidationResult<bool>.Invalid(
                    "FullCoreAdjoint.Digest.Mismatch",
                    "digest",
                    "The expected reference adjoint digest does not match the immutable field.");
            }

            return ContractValidationResult<bool>.Valid(true);
        }

        internal static Digest32 ComputeReferenceStateDigest(
            FullCoreDiffusionDataPackV1 diffusionDataPack,
            BundleInventory inventory)
        {
            byte[] bytes = Phase5CanonicalBytesV1.HashBody(
                "full-core-reference-state-v1",
                writer =>
                {
                    Phase5CanonicalBytesV1.WriteString(
                        writer,
                        diffusionDataPack.Descriptor.DataPackVersion);
                    Phase5CanonicalBytesV1.WriteDigest(
                        writer,
                        new Digest32(diffusionDataPack.Descriptor.ContentDigest.ToArray()));
                    Phase5CanonicalBytesV1.WriteString(
                        writer,
                        diffusionDataPack.Descriptor.TopologySchemaId);
                    Phase5CanonicalBytesV1.WriteUInt32(
                        writer,
                        diffusionDataPack.Descriptor.ChannelCount);
                    Phase5CanonicalBytesV1.WriteUInt32(
                        writer,
                        diffusionDataPack.Descriptor.BundlePositionCount);
                    Phase5CanonicalBytesV1.WriteUInt32(
                        writer,
                        checked((uint)inventory.SlotCount));
                    for (int slotIndex = 0; slotIndex < inventory.Slots.Count; slotIndex++)
                    {
                        BundleState? bundle = inventory.Slots[slotIndex];
                        writer.Write(bundle == null ? (byte)0 : (byte)1);
                        if (bundle == null)
                        {
                            continue;
                        }

                        Phase5CanonicalBytesV1.WriteStableId(writer, bundle.BundleId);
                        Phase5CanonicalBytesV1.WriteUInt32(writer, bundle.ChannelId.Value);
                        Phase5CanonicalBytesV1.WriteUInt32(writer, bundle.Position.Value);
                        Phase5CanonicalBytesV1.WriteString(writer, bundle.MaterialVariantId.Value);
                        Phase5CanonicalBytesV1.WriteDouble(writer, bundle.InitialBurnupJPerKgHm);
                        Phase5CanonicalBytesV1.WriteDouble(writer, bundle.CumulativeFissionEnergyJ);
                        Phase5CanonicalBytesV1.WriteDouble(writer, bundle.HeavyMetalMassKg);
                        Phase5CanonicalBytesV1.WriteDouble(writer, bundle.InsertedAtSeconds);
                    }
                });
            return new Digest32(bytes);
        }

        private static Digest32 ComputeDigest(
            FullCoreDiffusionDataPackV1 diffusionDataPack,
            IqsKineticsDataPackV1 kineticsDataPack,
            CoreTopology topology,
            Digest32 referenceStateDigest,
            SpatialAdjointSolveResultV1 solve)
        {
            byte[] digestBytes = Phase5CanonicalBytesV1.HashBody(
                SchemaId,
                writer =>
                {
                    Phase5CanonicalBytesV1.WriteUInt32(writer, CurrentSchemaVersion);
                    Phase5CanonicalBytesV1.WriteString(
                        writer,
                        diffusionDataPack.Descriptor.TopologySchemaId);
                    Phase5CanonicalBytesV1.WriteUInt32(writer, topology.ChannelCount);
                    Phase5CanonicalBytesV1.WriteUInt32(writer, topology.BundlePositionCount);
                    Phase5CanonicalBytesV1.WriteDigest(
                        writer,
                        new Digest32(diffusionDataPack.Descriptor.TopologyDigest.ToArray()));
                    Phase5CanonicalBytesV1.WriteDigest(
                        writer,
                        new Digest32(diffusionDataPack.Descriptor.ContentDigest.ToArray()));
                    Phase5CanonicalBytesV1.WriteDigest(
                        writer,
                        kineticsDataPack.TopologyDigest);
                    Phase5CanonicalBytesV1.WriteDigest(writer, kineticsDataPack.ContentDigest);
                    Phase5CanonicalBytesV1.WriteString(
                        writer,
                        diffusionDataPack.Descriptor.DataPackVersion);
                    Phase5CanonicalBytesV1.WriteString(writer, kineticsDataPack.DataPackVersion);
                    Phase5CanonicalBytesV1.WriteString(writer, SpatialAdjointEigenSolve.SolverIdentity);
                    Phase5CanonicalBytesV1.WriteString(writer, SpatialAdjointEigenSolve.NormalizationIdentity);
                    Phase5CanonicalBytesV1.WriteString(writer, SpatialAdjointEigenSolve.EnergyGroupOrderIdentity);
                    Phase5CanonicalBytesV1.WriteDigest(writer, referenceStateDigest);
                    Phase5CanonicalBytesV1.WriteDouble(writer, solve.Eigenvalue);
                    Phase5CanonicalBytesV1.WriteDouble(writer, solve.NormalizationValue);
                    Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)solve.IterationCount));
                    Phase5CanonicalBytesV1.WriteDouble(writer, solve.EigenvalueChangeAbsolute);
                    Phase5CanonicalBytesV1.WriteDouble(writer, solve.EigenvalueChangeRelative);
                    Phase5CanonicalBytesV1.WriteDouble(writer, solve.ResidualAbsoluteInfinity);
                    Phase5CanonicalBytesV1.WriteDouble(writer, solve.ResidualRelativeInfinity);
                    Phase5CanonicalBytesV1.WriteDouble(writer, solve.SourceShapeChangeInfinity);
                    for (int group = 0; group < solve.EnergyGroupOrder.Count; group++)
                    {
                        Phase5CanonicalBytesV1.WriteString(writer, solve.EnergyGroupOrder[group]);
                        Phase5CanonicalBytesV1.WriteDouble(
                            writer,
                            solve.GroupVelocitiesMPerSecond[group]);
                    }

                    Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)solve.NodeCount));
                    for (int nodeIndex = 0; nodeIndex < solve.NodeCount; nodeIndex++)
                    {
                        Phase5CanonicalBytesV1.WriteDouble(
                            writer,
                            solve.Group1Importance[nodeIndex]);
                        Phase5CanonicalBytesV1.WriteDouble(
                            writer,
                            solve.Group2Importance[nodeIndex]);
                    }
                });
            return new Digest32(digestBytes);
        }

        private static double ComputeNormalization(
            double nodeVolume,
            IReadOnlyList<double> velocities,
            IReadOnlyList<double> group1Importance,
            IReadOnlyList<double> group2Importance)
        {
            double total = 0.0;
            for (int nodeIndex = 0; nodeIndex < group1Importance.Count; nodeIndex++)
            {
                total += nodeVolume * (
                    group1Importance[nodeIndex] / velocities[0] +
                    group2Importance[nodeIndex] / velocities[1]);
            }

            return total;
        }

        private static ContractValidationResult<bool> ValidateGroupIdentity(
            IReadOnlyList<string> groupOrder,
            string path)
        {
            if (groupOrder == null || groupOrder.Count != 2 ||
                !string.Equals(groupOrder[0], "fast", StringComparison.Ordinal) ||
                !string.Equals(groupOrder[1], "thermal", StringComparison.Ordinal))
            {
                return ContractValidationResult<bool>.Invalid(
                    "FullCoreAdjoint.EnergyGroupOrder.Unsupported",
                    path,
                    "The reference adjoint requires the canonical [fast, thermal] energy-group order.");
            }

            return ContractValidationResult<bool>.Valid(true);
        }

        private static string ToHex(Digest32 digest)
        {
            var builder = new StringBuilder(digest.Bytes.Count * 2);
            foreach (byte value in digest.Bytes)
            {
                builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private static ContractValidationResult<FullCoreAdjointImportanceV1> Invalid(
            ContractDiagnostic diagnostic)
        {
            return Invalid(diagnostic.Code, diagnostic.Path, diagnostic.Message);
        }

        private static ContractValidationResult<FullCoreAdjointImportanceV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<FullCoreAdjointImportanceV1>.Invalid(
                code,
                path,
                message);
        }
    }
}
