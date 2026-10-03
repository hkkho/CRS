using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ReactorSim.Core
{
    public sealed class SyntheticCoreFixture
    {
        internal SyntheticCoreFixture(
            CoreTopology topology,
            DataPackDescriptor dataPack,
            SimulationConfiguration configuration,
            BundleInventory inventory)
        {
            Topology = topology;
            DataPack = dataPack;
            Configuration = configuration;
            Inventory = inventory;
        }

        public CoreTopology Topology { get; }

        public DataPackDescriptor DataPack { get; }

        public SimulationConfiguration Configuration { get; }

        public BundleInventory Inventory { get; }
    }

    /// <summary>
    /// A small fully explicit two-channel by three-position fixture. It is
    /// topology/state data only and deliberately contains no physics behavior.
    /// </summary>
    public static class SyntheticFixtures
    {
        public static SyntheticCoreFixture CreateTwoChannelThreePosition()
        {
            const uint channelCount = 2;
            const uint positionCount = 3;
            var channels = new List<ChannelTopology>
            {
                CreateChannelZero(positionCount),
                CreateChannelOne(positionCount)
            };

            ContractValidationResult<CoreTopology> topologyResult =
                CoreTopology.TryCreate(channelCount, positionCount, channels);
            if (!topologyResult.IsValid)
            {
                throw new InvalidOperationException(topologyResult.FirstDiagnostic.ToString());
            }

            CoreTopology topology = topologyResult.Value;
            byte[] topologyDigest = Digest("synthetic-topology-p3-t01");
            byte[] contentDigest = Digest("synthetic-content-p3-t01");
            ContractValidationResult<DataPackDescriptor> dataPackResult =
                DataPackDescriptor.TryCreate(
                    DataPackDescriptor.CurrentSchemaVersion,
                    StableId.Parse("00000000-0000-0000-0000-000000000101"),
                    "synthetic-p3-t01",
                    "topology-v1",
                    channelCount,
                    positionCount,
                    "SI-v1",
                    topologyDigest,
                    contentDigest);
            if (!dataPackResult.IsValid)
            {
                throw new InvalidOperationException(dataPackResult.FirstDiagnostic.ToString());
            }

            DataPackDescriptor dataPack = dataPackResult.Value;
            ContractValidationResult<SimulationConfiguration> configurationResult =
                SimulationConfiguration.TryCreate(
                    SimulationConfiguration.CurrentSchemaVersion,
                    topology,
                    dataPack,
                    0.0,
                    100,
                    200,
                    300);
            if (!configurationResult.IsValid)
            {
                throw new InvalidOperationException(configurationResult.FirstDiagnostic.ToString());
            }

            var bundles = Enumerable.Range(0, topology.SlotCount)
                .Select(index => new BundleState(
                    StableId.Parse("00000000-0000-0000-0000-" +
                                   (0x200 + index + 1).ToString("x12", CultureInfo.InvariantCulture)),
                    new ChannelId((uint)(index / positionCount)),
                    new BundlePosition((uint)(index % positionCount)),
                    new MaterialVariantId("synthetic-fuel"),
                    0.0,
                    0.0,
                    1000.0,
                    0.0))
                .ToArray();
            ContractValidationResult<BundleInventory> inventoryResult =
                BundleInventory.TryCreate(topology, bundles);
            if (!inventoryResult.IsValid)
            {
                throw new InvalidOperationException(inventoryResult.FirstDiagnostic.ToString());
            }

            return new SyntheticCoreFixture(
                topology,
                dataPack,
                configurationResult.Value,
                inventoryResult.Value);
        }

        private static ChannelTopology CreateChannelZero(uint positionCount)
        {
            return new ChannelTopology(
                new ChannelId(0),
                0,
                0,
                FlowDirection.EndAtoEndB,
                new BundlePosition(0),
                new BundlePosition(positionCount - 1),
                CreateAxialNeighbors(0, positionCount).Concat(CreateTransverseNeighbors(0, positionCount)).ToArray(),
                CreateBoundaryFaces(0, positionCount, TopologyFace.West).ToArray());
        }

        private static ChannelTopology CreateChannelOne(uint positionCount)
        {
            return new ChannelTopology(
                new ChannelId(1),
                1,
                0,
                FlowDirection.EndBtoEndA,
                new BundlePosition(positionCount - 1),
                new BundlePosition(0),
                CreateAxialNeighbors(1, positionCount).Concat(CreateTransverseNeighbors(1, positionCount)).ToArray(),
                CreateBoundaryFaces(1, positionCount, TopologyFace.East).ToArray());
        }

        private static IEnumerable<NeighborRecord> CreateAxialNeighbors(uint channel, uint positionCount)
        {
            for (uint position = 0; position + 1 < positionCount; position++)
            {
                yield return new NeighborRecord(
                    new ChannelId(channel),
                    new BundlePosition(position),
                    new ChannelId(channel),
                    new BundlePosition(position + 1),
                    NeighborDirection.TowardEndB);
                yield return new NeighborRecord(
                    new ChannelId(channel),
                    new BundlePosition(position + 1),
                    new ChannelId(channel),
                    new BundlePosition(position),
                    NeighborDirection.TowardEndA);
            }
        }

        private static IEnumerable<NeighborRecord> CreateTransverseNeighbors(uint channel, uint positionCount)
        {
            for (uint position = 0; position < positionCount; position++)
            {
                bool isChannelZero = channel == 0;
                yield return new NeighborRecord(
                    new ChannelId(channel),
                    new BundlePosition(position),
                    new ChannelId(isChannelZero ? 1u : 0u),
                    new BundlePosition(position),
                    isChannelZero ? NeighborDirection.East : NeighborDirection.West);
            }
        }

        private static IEnumerable<BoundaryFaceRecord> CreateBoundaryFaces(
            uint channel,
            uint positionCount,
            TopologyFace transverseExteriorFace)
        {
            TopologyFace[] cardinalFaces =
            {
                TopologyFace.North,
                TopologyFace.South,
                transverseExteriorFace
            };
            for (uint position = 0; position < positionCount; position++)
            {
                foreach (TopologyFace face in cardinalFaces)
                {
                    yield return new BoundaryFaceRecord(
                        new ChannelId(channel),
                        new BundlePosition(position),
                        face,
                        BoundaryClassification.Reflective);
                }
            }

            yield return new BoundaryFaceRecord(
                new ChannelId(channel),
                new BundlePosition(0),
                TopologyFace.EndA,
                BoundaryClassification.Reflective);
            yield return new BoundaryFaceRecord(
                new ChannelId(channel),
                new BundlePosition(positionCount - 1),
                TopologyFace.EndB,
                BoundaryClassification.Reflective);
        }

        private static byte[] Digest(string value)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                return sha256.ComputeHash(Encoding.UTF8.GetBytes(value));
            }
        }
    }
}
