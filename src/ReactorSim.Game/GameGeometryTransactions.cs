using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using ReactorSim.Core;

namespace ReactorSim.Game
{
    internal static class GameGeometryTransactions
    {
        internal static ContractValidationResult<ConfiguredCoreDesign>
            BuildDesign(
                uint channelIndex,
                uint position,
                bool hasFuel,
                IReadOnlyCollection<TopologyFace> reflectiveFaces, IReadOnlyList<NodeKey> previousNonfuel,
                IReadOnlyDictionary<NodeKey, IReadOnlyList<TopologyFace>> previousFaces)
        {
            if (channelIndex >= GameCorePresentationConstants.ChannelCount)
            {
                return InvalidConfiguredDesign(
                    "GameSession.ConfigureCell.Channel.OutOfRange",
                    "channelIndex",
                    "The configured channel index must identify one of the 380 full-core channels.");
            }

            if (position >= GameCorePresentationConstants.BundlePositionCount)
            {
                return InvalidConfiguredDesign(
                    "GameSession.ConfigureCell.Position.OutOfRange",
                    "position",
                    "The configured position must identify one of the 12 bundle positions.");
            }

            if (reflectiveFaces == null)
            {
                return InvalidConfiguredDesign(
                    "GameSession.ConfigureCell.ReflectiveFaces.Missing",
                    "reflectiveFaces",
                    "A cell configuration requires an explicit reflective face collection.");
            }

            var requestedFaces = new HashSet<TopologyFace>();
            foreach (TopologyFace face in reflectiveFaces)
            {
                if (!Enum.IsDefined(typeof(TopologyFace), face))
                {
                    return InvalidConfiguredDesign(
                        "GameSession.ConfigureCell.ReflectiveFaces.Invalid",
                        "reflectiveFaces",
                        "Every reflective face must be a known topology face.");
                }

                if (!requestedFaces.Add(face))
                {
                    return InvalidConfiguredDesign(
                        "GameSession.ConfigureCell.ReflectiveFaces.Duplicate",
                        "reflectiveFaces",
                        "A reflective face may be listed only once.");
                }
            }

            NodeKey node = new NodeKey(
                new ChannelId(channelIndex),
                new BundlePosition(position));
            var nonfuelNodes = new HashSet<NodeKey>(previousNonfuel);
            if (hasFuel)
            {
                nonfuelNodes.Remove(node);
            }
            else
            {
                nonfuelNodes.Add(node);
            }

            var faceMap = new Dictionary<NodeKey, List<TopologyFace>>();
            foreach (KeyValuePair<NodeKey, IReadOnlyList<TopologyFace>> entry in
                previousFaces)
            {
                faceMap[entry.Key] = entry.Value.ToList();
            }

            if (faceMap.TryGetValue(node, out List<TopologyFace>? currentFaces))
            {
                foreach (TopologyFace currentFace in currentFaces.ToArray())
                {
                    RemoveFace(faceMap, node, currentFace);
                    if (TryGetInteriorNeighbor(
                            node,
                            currentFace,
                            out NodeKey neighbor))
                    {
                        RemoveFace(faceMap, neighbor, InverseFace(currentFace));
                    }
                }
            }

            foreach (TopologyFace face in requestedFaces)
            {
                AddFace(faceMap, node, face);
                if (TryGetInteriorNeighbor(node, face, out NodeKey neighbor))
                {
                    AddFace(faceMap, neighbor, InverseFace(face));
                }
            }

            return ContractValidationResult<ConfiguredCoreDesign>.Valid(
                new ConfiguredCoreDesign(
                    nonfuelNodes,
                    faceMap.Select(entry =>
                        new KeyValuePair<NodeKey, IEnumerable<TopologyFace>>(
                            entry.Key,
                            entry.Value))));
        }
        internal static ContractValidationResult<ConfiguredCoreTransaction>
            Build(ConfiguredCoreDesign design,
                PracticeLiquidZoneRrsMappingV1? zoneMapping, EquilibriumCoreSolverV1 currentSolver,
                SyntheticGameCoreStateV1 core, PracticeLiquidZoneRrsV1 rrs, PracticeXenonStateV1 xenon,
                double simulationTime, ulong version)
        {
            if (design == null)
            {
                return InvalidConfiguredTransaction(
                    "GameSession.ConfigureCell.Design.Missing",
                    "design",
                    "A configured full-core solve requires an explicit immutable design.");
            }

            var overrides = design.ReflectiveFaceOverrides
                .OrderBy(entry => entry.Key)
                .SelectMany(entry => entry.Value.Select(face =>
                    new ReflectiveFaceOverrideV1(entry.Key, face)))
                .ToArray();
            ContractValidationResult<CoreTopology> topology =
                Candu6CoreTopologyFactoryV1.TryCreate(overrides);
            if (!topology.IsValid)
            {
                return InvalidConfiguredTransaction(
                    topology.FirstDiagnostic.Code,
                    topology.FirstDiagnostic.Path,
                    topology.FirstDiagnostic.Message);
            }

            ContractValidationResult<SpatialStencil> stencil =
                SpatialStencil.TryCreate(topology.Value);
            if (!stencil.IsValid)
            {
                return InvalidConfiguredTransaction(
                    stencil.FirstDiagnostic.Code,
                    stencil.FirstDiagnostic.Path,
                    stencil.FirstDiagnostic.Message);
            }

            ContractValidationResult<FullCoreDiffusionModelV1> model =
                FullCoreDiffusionModelV1.TryCreate(
                    currentSolver.DataPack,
                    topology.Value,
                    stencil.Value,
                    design.NonfuelNodes);
            if (!model.IsValid)
            {
                return InvalidConfiguredTransaction(
                    model.FirstDiagnostic.Code,
                    model.FirstDiagnostic.Path,
                    model.FirstDiagnostic.Message);
            }

            ContractValidationResult<EquilibriumCoreSolverV1> solver =
                EquilibriumCoreSolverV1.TryCreate(
                    model.Value,
                    core.EnumerateBundles(),
                    currentSolver.TargetPowerWatts);
            if (!solver.IsValid)
            {
                return InvalidConfiguredTransaction(
                    solver.FirstDiagnostic.Code,
                    solver.FirstDiagnostic.Path,
                    solver.FirstDiagnostic.Message);
            }

            PracticeLiquidZoneRrsV1 previousRrs = rrs;
            if (zoneMapping != null)
            {
                var remapped = PracticeLiquidZoneRrsV1.TryCreate(zoneMapping,
                    solver.Value.CurrentProjection, simulationTime, rrs.ZoneFills);
                if (!remapped.IsValid)
                    return InvalidConfiguredTransaction(remapped.FirstDiagnostic.Code,
                        remapped.FirstDiagnostic.Path, remapped.FirstDiagnostic.Message);
                previousRrs = remapped.Value;
            }
            ContractValidationResult<PracticeLiquidZoneRrsEquilibriumResultV1> regulated =
                PracticeLiquidZoneRrsV1.TryRunEquilibrium(
                    solver.Value,
                    core.EnumerateBundles(),
                    previousRrs,
                    simulationTime,
                    backgroundOverlay: xenon.BuildOverlay(design.NonfuelNodes));
            if (!regulated.IsValid)
            {
                return InvalidConfiguredTransaction(
                    regulated.FirstDiagnostic.Code,
                    regulated.FirstDiagnostic.Path,
                    regulated.FirstDiagnostic.Message);
            }

            ContractValidationResult<bool> committed =
                solver.Value.TryCommitCandidate(regulated.Value.Projection);
            if (!committed.IsValid)
            {
                return InvalidConfiguredTransaction(
                    committed.FirstDiagnostic.Code,
                    committed.FirstDiagnostic.Path,
                    committed.FirstDiagnostic.Message);
            }

            ContractValidationResult<ulong> nextProjectionVersion =
                GameSession.TryNextPowerProjectionVersion(version);
            if (!nextProjectionVersion.IsValid)
            {
                return InvalidConfiguredTransaction(
                    nextProjectionVersion.FirstDiagnostic.Code,
                    nextProjectionVersion.FirstDiagnostic.Path,
                    nextProjectionVersion.FirstDiagnostic.Message);
            }

            return ContractValidationResult<ConfiguredCoreTransaction>.Valid(
                new ConfiguredCoreTransaction(
                    solver.Value,
                    regulated.Value.State,
                    design,
                    nextProjectionVersion.Value));
        }
        private static ContractValidationResult<ConfiguredCoreDesign>
            InvalidConfiguredDesign(string code, string path, string message)
        {
            return ContractValidationResult<ConfiguredCoreDesign>.Invalid(
                code,
                path,
                message);
        }
        private static ContractValidationResult<ConfiguredCoreTransaction>
            InvalidConfiguredTransaction(string code, string path, string message)
        {
            return ContractValidationResult<ConfiguredCoreTransaction>.Invalid(
                code,
                path,
                message);
        }
        private static void AddFace(
            Dictionary<NodeKey, List<TopologyFace>> faceMap,
            NodeKey node,
            TopologyFace face)
        {
            if (!faceMap.TryGetValue(node, out List<TopologyFace>? faces))
            {
                faces = new List<TopologyFace>();
                faceMap[node] = faces;
            }

            if (!faces.Contains(face))
            {
                faces.Add(face);
                faces.Sort((left, right) => FaceRank(left).CompareTo(FaceRank(right)));
            }
        }
        private static void RemoveFace(
            Dictionary<NodeKey, List<TopologyFace>> faceMap,
            NodeKey node,
            TopologyFace face)
        {
            if (!faceMap.TryGetValue(node, out List<TopologyFace>? faces))
            {
                return;
            }

            faces.Remove(face);
            if (faces.Count == 0)
            {
                faceMap.Remove(node);
            }
        }
        private static bool TryGetInteriorNeighbor(
            NodeKey node,
            TopologyFace face,
            out NodeKey neighbor)
        {
            neighbor = default(NodeKey);
            int column;
            int displayRow;
            switch (face)
            {
                case TopologyFace.North:
                    column = Candu6CoreTopologyFactoryV1.GetPosition(node.ChannelId.Value).Column;
                    displayRow = Candu6CoreTopologyFactoryV1.GetPosition(node.ChannelId.Value).DisplayRow - 1;
                    break;
                case TopologyFace.East:
                    column = Candu6CoreTopologyFactoryV1.GetPosition(node.ChannelId.Value).Column + 1;
                    displayRow = Candu6CoreTopologyFactoryV1.GetPosition(node.ChannelId.Value).DisplayRow;
                    break;
                case TopologyFace.South:
                    column = Candu6CoreTopologyFactoryV1.GetPosition(node.ChannelId.Value).Column;
                    displayRow = Candu6CoreTopologyFactoryV1.GetPosition(node.ChannelId.Value).DisplayRow + 1;
                    break;
                case TopologyFace.West:
                    column = Candu6CoreTopologyFactoryV1.GetPosition(node.ChannelId.Value).Column - 1;
                    displayRow = Candu6CoreTopologyFactoryV1.GetPosition(node.ChannelId.Value).DisplayRow;
                    break;
                case TopologyFace.EndA:
                    if (node.Position.Value == 0)
                    {
                        return false;
                    }

                    neighbor = new NodeKey(
                        node.ChannelId,
                        new BundlePosition(node.Position.Value - 1));
                    return true;
                case TopologyFace.EndB:
                    if (node.Position.Value + 1 >=
                        GameCorePresentationConstants.BundlePositionCount)
                    {
                        return false;
                    }

                    neighbor = new NodeKey(
                        node.ChannelId,
                        new BundlePosition(node.Position.Value + 1));
                    return true;
                default:
                    return false;
            }

            if (!Candu6CoreTopologyFactoryV1.TryGetChannelIndex(
                    column,
                    displayRow,
                    out uint neighborChannel))
            {
                return false;
            }

            neighbor = new NodeKey(
                new ChannelId(neighborChannel),
                node.Position);
            return true;
        }
        private static TopologyFace InverseFace(TopologyFace face)
        {
            switch (face)
            {
                case TopologyFace.North:
                    return TopologyFace.South;
                case TopologyFace.East:
                    return TopologyFace.West;
                case TopologyFace.South:
                    return TopologyFace.North;
                case TopologyFace.West:
                    return TopologyFace.East;
                case TopologyFace.EndA:
                    return TopologyFace.EndB;
                case TopologyFace.EndB:
                    return TopologyFace.EndA;
                default:
                    throw new ArgumentOutOfRangeException(nameof(face));
            }
        }
        private static int FaceRank(TopologyFace face)
        {
            return (byte)face;
        }
    }
}
