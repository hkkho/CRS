using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ReactorSim.Core;

internal static class Program
{
    private const string SourceFormat = "reactorsim.reduced-interpolation-source/v1";
    private const string PackFormat = "reactorsim.reduced-interpolation-pack/v1";
    private const string ManifestFormat = "reactorsim.reduced-interpolation-manifest/v1";
    private const string CandidatePackArtifactId = "p4-t06-r4-reduced-candidate-v1";
    private const string TransformId = "identity_projection_v1";
    private const string CandidateStatus = "candidate";
    private const string RuntimeUse = "candidate_interpolation_input";

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0)
            {
                PrintUsage();
                return 1;
            }

            switch (args[0].ToLowerInvariant())
            {
                case "generate":
                    RequireArgumentCount(args, 4, "generate <source> <pack> <manifest>");
                    Generate(args[1], args[2], args[3]);
                    return 0;
                case "validate":
                    if (args.Length != 3 && args.Length != 4)
                    {
                        throw new PackFailure(
                            "Arguments.Invalid",
                            "arguments",
                            "validate requires <pack> <manifest> and an optional <source>.");
                    }

                    Validate(args[1], args[2], args.Length == 4 ? args[3] : null);
                    return 0;
                default:
                    PrintUsage();
                    return 1;
            }
        }
        catch (PackFailure failure)
        {
            Console.Error.WriteLine(
                "P4_T06_R4_FAILURE code=" + failure.Code +
                " path=" + failure.Path +
                " message=" + failure.Message);
            return 2;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                "P4_T06_R4_FAILURE code=Unhandled.Exception path=tool message=" + exception.Message);
            return 3;
        }
    }

    private static void Generate(string sourcePath, string packPath, string manifestPath)
    {
        string sourceFullPath = Path.GetFullPath(sourcePath);
        string packFullPath = Path.GetFullPath(packPath);
        string manifestFullPath = Path.GetFullPath(manifestPath);
        if (string.Equals(sourceFullPath, packFullPath, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(sourceFullPath, manifestFullPath, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(packFullPath, manifestFullPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new PackFailure(
                "Arguments.TargetCollision",
                "arguments",
                "Source, pack, and manifest must be distinct files.");
        }

        byte[] sourceBytes = ReadFile(sourceFullPath, "source");
        SourceModel source = ParseSource(sourceBytes);
        Artifact artifact = BuildArtifact(source, sourceBytes);
        WriteFile(packFullPath, artifact.PackBytes, "pack");
        WriteFile(manifestFullPath, artifact.ManifestBytes, "manifest");

        Console.WriteLine(
            "P4_T06_R4_GENERATE_PASS pack_sha256=" +
            ToHex(artifact.PackSha256) +
            " source_sha256=" + ToHex(artifact.SourceSha256) +
            " table_count=" + artifact.Tables.Count.ToString(CultureInfo.InvariantCulture));
    }

    private static void Validate(string packPath, string manifestPath, string? sourcePath)
    {
        string packFullPath = Path.GetFullPath(packPath);
        string manifestFullPath = Path.GetFullPath(manifestPath);
        byte[] packBytes = ReadFile(packFullPath, "pack");
        PackModel pack = ParsePack(packBytes);
        ManifestModel manifest = ParseManifest(ReadFile(manifestFullPath, "manifest"));
        ValidateManifest(pack, packBytes, manifest);

        if (sourcePath != null)
        {
            string sourceFullPath = Path.GetFullPath(sourcePath);
            byte[] sourceBytes = ReadFile(sourceFullPath, "source");
            SourceModel source = ParseSource(sourceBytes);
            Artifact expected = BuildArtifact(source, sourceBytes);
            if (!CryptographicOperations.FixedTimeEquals(packBytes, expected.PackBytes))
            {
                throw new PackFailure(
                    "Pack.NonDeterministic",
                    "pack",
                    "The pack bytes do not match deterministic regeneration from the supplied source.");
            }

            if (!string.Equals(ToHex(expected.SourceSha256), manifest.SourceSha256, StringComparison.Ordinal))
            {
                throw new PackFailure(
                    "Manifest.SourceDigest.Mismatch",
                    "manifest.source_sha256",
                    "The manifest source digest does not match the supplied source bytes.");
            }
        }

        Console.WriteLine(
            "P4_T06_R4_VALIDATE_PASS pack_sha256=" + ToHex(Sha256(packBytes)) +
            " table_count=" + pack.Tables.Count.ToString(CultureInfo.InvariantCulture) +
            " source_checked=" + (sourcePath == null ? "false" : "true"));
    }

    private static Artifact BuildArtifact(SourceModel source, byte[] sourceBytes)
    {
        List<PackTable> tables = new List<PackTable>();
        HashSet<string> materialIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (SourceTable sourceTable in source.Tables)
        {
            if (!materialIds.Add(sourceTable.MaterialVariantId.Value))
            {
                throw new PackFailure(
                    "Source.MaterialVariant.Duplicate",
                    "tables.material_variant_id",
                    "Exactly one source table is allowed per material variant.");
            }

            PackTable table = new PackTable(
                sourceTable.TableId,
                source.DataVersion,
                sourceTable.MaterialVariantId,
                source.UnitsProfileId,
                source.SourceProvenance,
                sourceTable.Rows);
            byte[] identityBytes = SerializeTableIdentity(table);
            byte[] checksum = Sha256(identityBytes);
            ContractValidationResult<BurnupCoefficientTableV1> coreTable =
                BurnupCoefficientTableV1.TryCreate(
                    table.TableId,
                    BurnupCoefficientTableV1.CurrentSchemaVersion,
                    table.DataVersion,
                    table.MaterialVariantId,
                    table.UnitsProfileId,
                    table.SourceProvenance,
                    new Digest32(checksum),
                    table.Rows.Select(row => row.CoreRow));
            table.SetChecksumAndCoreTable(
                checksum,
                Valid(coreTable, "tables[" + table.MaterialVariantId.Value + "]"));
            tables.Add(table);
        }

        tables.Sort(CompareTables);
        byte[] packBytes = SerializePack(source, tables);
        byte[] packSha256 = Sha256(packBytes);
        byte[] sourceSha256 = Sha256(sourceBytes);
        byte[] manifestBytes = SerializeManifest(
            source,
            tables,
            sourceSha256,
            packSha256);
        return new Artifact(packBytes, manifestBytes, sourceSha256, packSha256, tables);
    }

    private static SourceModel ParseSource(byte[] bytes)
    {
        JsonDocumentOptions options = new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 32
        };
        using JsonDocument document = ParseJson(bytes, "source", options);
        Dictionary<string, JsonElement> properties = ReadObject(
            document.RootElement,
            "source",
            "format",
            "evidence_class",
            "source_artifact_id",
            "source_provenance",
            "transform_id",
            "data_version",
            "units_profile_id",
            "tables");

        RequireString(properties, "format", "source.format", SourceFormat);
        RequireString(properties, "evidence_class", "source.evidence_class", "synthetic");
        string sourceArtifactId = ReadPathFreeIdentifier(
            properties["source_artifact_id"],
            "source.source_artifact_id");
        string sourceProvenance = ReadRequiredString(
            properties["source_provenance"],
            "source.source_provenance");
        string transformId = RequireString(
            properties,
            "transform_id",
            "source.transform_id",
            TransformId);
        string dataVersion = ReadRequiredString(properties["data_version"], "source.data_version");
        string unitsProfileId = ReadRequiredString(properties["units_profile_id"], "source.units_profile_id");
        JsonElement tablesElement = properties["tables"];
        if (tablesElement.ValueKind != JsonValueKind.Array || tablesElement.GetArrayLength() == 0)
        {
            throw new PackFailure(
                "Source.Tables.Empty",
                "source.tables",
                "The source must contain at least one material table.");
        }

        List<SourceTable> tables = new List<SourceTable>();
        int index = 0;
        foreach (JsonElement tableElement in tablesElement.EnumerateArray())
        {
            string path = "source.tables[" + index.ToString(CultureInfo.InvariantCulture) + "]";
            Dictionary<string, JsonElement> tableProperties = ReadObject(
                tableElement,
                path,
                "table_id",
                "material_variant_id",
                "rows");
            StableId tableId = ReadStableId(tableProperties["table_id"], path + ".table_id");
            MaterialVariantId materialVariantId = new MaterialVariantId(
                ReadRequiredString(tableProperties["material_variant_id"], path + ".material_variant_id"));
            List<SourceRow> rows = ReadRows(tableProperties["rows"], path + ".rows");
            tables.Add(new SourceTable(tableId, materialVariantId, rows));
            index++;
        }

        return new SourceModel(
            sourceArtifactId,
            sourceProvenance,
            transformId,
            dataVersion,
            unitsProfileId,
            tables);
    }

    private static PackModel ParsePack(byte[] bytes)
    {
        using JsonDocument document = ParseJson(bytes, "pack", new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 32
        });
        Dictionary<string, JsonElement> properties = ReadObject(
            document.RootElement,
            "pack",
            "format",
            "approval_status",
            "evidence_class",
            "runtime_use",
            "pack_artifact_id",
            "schema_version",
            "data_version",
            "units_profile_id",
            "source_artifact_id",
            "source_provenance",
            "transform_id",
            "tables");

        RequireString(properties, "format", "pack.format", PackFormat);
        RequireString(properties, "approval_status", "pack.approval_status", CandidateStatus);
        string evidenceClass = RequireString(properties, "evidence_class", "pack.evidence_class", "synthetic");
        RequireString(properties, "runtime_use", "pack.runtime_use", RuntimeUse);
        string packArtifactId = ReadPathFreeIdentifier(properties["pack_artifact_id"], "pack.pack_artifact_id");
        ReadSchemaVersion(properties["schema_version"], "pack.schema_version");
        string dataVersion = ReadRequiredString(properties["data_version"], "pack.data_version");
        string unitsProfileId = ReadRequiredString(properties["units_profile_id"], "pack.units_profile_id");
        string sourceArtifactId = ReadPathFreeIdentifier(properties["source_artifact_id"], "pack.source_artifact_id");
        string sourceProvenance = ReadRequiredString(properties["source_provenance"], "pack.source_provenance");
        string transformId = RequireString(properties, "transform_id", "pack.transform_id", TransformId);
        JsonElement tablesElement = properties["tables"];
        if (tablesElement.ValueKind != JsonValueKind.Array || tablesElement.GetArrayLength() == 0)
        {
            throw new PackFailure("Pack.Tables.Empty", "pack.tables", "The pack must contain at least one material table.");
        }

        List<PackTable> tables = new List<PackTable>();
        HashSet<string> materialIds = new HashSet<string>(StringComparer.Ordinal);
        int index = 0;
        foreach (JsonElement tableElement in tablesElement.EnumerateArray())
        {
            string path = "pack.tables[" + index.ToString(CultureInfo.InvariantCulture) + "]";
            Dictionary<string, JsonElement> tableProperties = ReadObject(
                tableElement,
                path,
                "table_id",
                "schema_version",
                "data_version",
                "material_variant_id",
                "units_profile_id",
                "source_provenance",
                "checksum",
                "rows");
            StableId tableId = ReadStableId(tableProperties["table_id"], path + ".table_id");
            ReadSchemaVersion(tableProperties["schema_version"], path + ".schema_version");
            string tableDataVersion = ReadRequiredString(tableProperties["data_version"], path + ".data_version");
            MaterialVariantId materialVariantId = new MaterialVariantId(
                ReadRequiredString(tableProperties["material_variant_id"], path + ".material_variant_id"));
            string tableUnitsProfileId = ReadRequiredString(
                tableProperties["units_profile_id"],
                path + ".units_profile_id");
            string tableSourceProvenance = ReadRequiredString(
                tableProperties["source_provenance"],
                path + ".source_provenance");
            byte[] checksum = ReadDigest(tableProperties["checksum"], path + ".checksum");
            List<SourceRow> rows = ReadRows(tableProperties["rows"], path + ".rows");
            PackTable table = new PackTable(
                tableId,
                tableDataVersion,
                materialVariantId,
                tableUnitsProfileId,
                tableSourceProvenance,
                rows);
            table.SetChecksumAndCoreTable(
                checksum,
                CreateCoreTable(table, checksum, path));
            byte[] expectedChecksum = Sha256(SerializeTableIdentity(table));
            EnsureDigestEqual(expectedChecksum, checksum, path + ".checksum", "The table checksum does not match its canonical identity bytes.");

            if (!string.Equals(tableDataVersion, dataVersion, StringComparison.Ordinal) ||
                !string.Equals(tableUnitsProfileId, unitsProfileId, StringComparison.Ordinal) ||
                !string.Equals(tableSourceProvenance, sourceProvenance, StringComparison.Ordinal))
            {
                throw new PackFailure(
                    "Pack.TableMetadata.Mismatch",
                    path,
                    "Every table must bind the pack data version, units profile, and source provenance.");
            }

            if (!materialIds.Add(materialVariantId.Value))
            {
                throw new PackFailure(
                    "Pack.MaterialVariant.Duplicate",
                    path + ".material_variant_id",
                    "Exactly one pack table is allowed per material variant.");
            }

            tables.Add(table);
            index++;
        }

        if (tables.SequenceEqual(tables.OrderBy(table => table, Comparer<PackTable>.Create(CompareTables))) == false)
        {
            throw new PackFailure(
                "Pack.Tables.Order.Invalid",
                "pack.tables",
                "Pack tables must be ordered by explicit material identity.");
        }

        return new PackModel(
            packArtifactId,
            sourceArtifactId,
            sourceProvenance,
            transformId,
            dataVersion,
            unitsProfileId,
            evidenceClass,
            tables);
    }

    private static ManifestModel ParseManifest(byte[] bytes)
    {
        using JsonDocument document = ParseJson(bytes, "manifest", new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 32
        });
        Dictionary<string, JsonElement> properties = ReadObject(
            document.RootElement,
            "manifest",
            "format",
            "manifest_schema_version",
            "approval_status",
            "evidence_class",
            "pack_artifact_id",
            "pack_format",
            "pack_sha256",
            "source_artifact_id",
            "source_sha256",
            "source_provenance",
            "transform_id",
            "data_version",
            "units_profile_id",
            "tables");

        RequireString(properties, "format", "manifest.format", ManifestFormat);
        ReadSchemaVersion(properties["manifest_schema_version"], "manifest.manifest_schema_version");
        RequireString(properties, "approval_status", "manifest.approval_status", CandidateStatus);
        string evidenceClass = RequireString(properties, "evidence_class", "manifest.evidence_class", "synthetic");
        string packArtifactId = ReadPathFreeIdentifier(properties["pack_artifact_id"], "manifest.pack_artifact_id");
        RequireString(properties, "pack_format", "manifest.pack_format", PackFormat);
        string packSha256 = ReadDigestHex(properties["pack_sha256"], "manifest.pack_sha256");
        string sourceArtifactId = ReadPathFreeIdentifier(properties["source_artifact_id"], "manifest.source_artifact_id");
        string sourceSha256 = ReadDigestHex(properties["source_sha256"], "manifest.source_sha256");
        string sourceProvenance = ReadRequiredString(properties["source_provenance"], "manifest.source_provenance");
        RequireString(properties, "transform_id", "manifest.transform_id", TransformId);
        string dataVersion = ReadRequiredString(properties["data_version"], "manifest.data_version");
        string unitsProfileId = ReadRequiredString(properties["units_profile_id"], "manifest.units_profile_id");
        JsonElement tablesElement = properties["tables"];
        if (tablesElement.ValueKind != JsonValueKind.Array || tablesElement.GetArrayLength() == 0)
        {
            throw new PackFailure("Manifest.Tables.Empty", "manifest.tables", "The manifest must contain table identities.");
        }

        List<ManifestTable> tables = new List<ManifestTable>();
        int index = 0;
        foreach (JsonElement tableElement in tablesElement.EnumerateArray())
        {
            string path = "manifest.tables[" + index.ToString(CultureInfo.InvariantCulture) + "]";
            Dictionary<string, JsonElement> tableProperties = ReadObject(
                tableElement,
                path,
                "table_id",
                "material_variant_id",
                "checksum",
                "row_count",
                "burnup_min_j_per_kg_hm",
                "burnup_max_j_per_kg_hm");
            StableId tableId = ReadStableId(tableProperties["table_id"], path + ".table_id");
            string materialVariantId = ReadRequiredString(
                tableProperties["material_variant_id"],
                path + ".material_variant_id");
            string checksum = ReadDigestHex(tableProperties["checksum"], path + ".checksum");
            uint rowCount = ReadUInt32(tableProperties["row_count"], path + ".row_count");
            double burnupMin = ReadFiniteDouble(
                tableProperties["burnup_min_j_per_kg_hm"],
                path + ".burnup_min_j_per_kg_hm");
            double burnupMax = ReadFiniteDouble(
                tableProperties["burnup_max_j_per_kg_hm"],
                path + ".burnup_max_j_per_kg_hm");
            tables.Add(new ManifestTable(tableId, materialVariantId, checksum, rowCount, burnupMin, burnupMax));
            index++;
        }

        return new ManifestModel(
            evidenceClass,
            packArtifactId,
            packSha256,
            sourceArtifactId,
            sourceSha256,
            sourceProvenance,
            dataVersion,
            unitsProfileId,
            tables);
    }

    private static void ValidateManifest(PackModel pack, byte[] packBytes, ManifestModel manifest)
    {
        string packSha256 = ToHex(Sha256(packBytes));
        if (!string.Equals(packSha256, manifest.PackSha256, StringComparison.Ordinal))
        {
            throw new PackFailure(
                "Manifest.PackDigest.Mismatch",
                "manifest.pack_sha256",
                "The manifest pack digest does not match the pack bytes.");
        }

        if (!string.Equals(pack.SourceArtifactId, manifest.SourceArtifactId, StringComparison.Ordinal) ||
            !string.Equals(pack.PackArtifactId, manifest.PackArtifactId, StringComparison.Ordinal) ||
            !string.Equals(manifest.PackArtifactId, CandidatePackArtifactId, StringComparison.Ordinal) ||
            !string.Equals(pack.EvidenceClass, manifest.EvidenceClass, StringComparison.Ordinal) ||
            !string.Equals(pack.SourceProvenance, manifest.SourceProvenance, StringComparison.Ordinal) ||
            !string.Equals(pack.DataVersion, manifest.DataVersion, StringComparison.Ordinal) ||
            !string.Equals(pack.UnitsProfileId, manifest.UnitsProfileId, StringComparison.Ordinal))
        {
            throw new PackFailure(
                "Manifest.Binding.Mismatch",
                "manifest",
                "Manifest artifact, source, evidence, and data metadata do not match the pack.");
        }

        List<PackTable> packTables = pack.Tables;
        if (packTables.Count != manifest.Tables.Count)
        {
            throw new PackFailure(
                "Manifest.Tables.CountMismatch",
                "manifest.tables",
                "Manifest table count does not match the pack.");
        }

        for (int index = 0; index < packTables.Count; index++)
        {
            PackTable packTable = packTables[index];
            ManifestTable manifestTable = manifest.Tables[index];
            if (packTable.TableId != manifestTable.TableId ||
                !string.Equals(packTable.MaterialVariantId.Value, manifestTable.MaterialVariantId, StringComparison.Ordinal) ||
                !string.Equals(ToHex(packTable.Checksum), manifestTable.Checksum, StringComparison.Ordinal) ||
                packTable.Rows.Count != manifestTable.RowCount ||
                packTable.Rows[0].BurnupJPerKgHm != manifestTable.BurnupMinJPerKgHm ||
                packTable.Rows[^1].BurnupJPerKgHm != manifestTable.BurnupMaxJPerKgHm)
            {
                throw new PackFailure(
                    "Manifest.TableBinding.Mismatch",
                    "manifest.tables[" + index.ToString(CultureInfo.InvariantCulture) + "]",
                    "Manifest table identity or domain does not match the pack.");
            }
        }
    }

    private static List<SourceRow> ReadRows(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() == 0)
        {
            throw new PackFailure("Rows.Empty", path, "A table requires at least one ordered row.");
        }

        List<SourceRow> rows = new List<SourceRow>();
        double previousBurnup = 0.0;
        int index = 0;
        foreach (JsonElement rowElement in element.EnumerateArray())
        {
            string rowPath = path + "[" + index.ToString(CultureInfo.InvariantCulture) + "]";
            Dictionary<string, JsonElement> rowProperties = ReadObject(
                rowElement,
                rowPath,
                "burnup_j_per_kg_hm",
                "coefficients");
            double burnup = ReadFiniteDouble(rowProperties["burnup_j_per_kg_hm"], rowPath + ".burnup_j_per_kg_hm");
            if (burnup < 0.0 || (index > 0 && burnup <= previousBurnup))
            {
                throw new PackFailure(
                    "Rows.Burnup.OrderInvalid",
                    rowPath + ".burnup_j_per_kg_hm",
                    "Burnup knots must be nonnegative and strictly increasing.");
            }

            BurnupCoefficientValuesV1 values = ReadCoefficients(rowProperties["coefficients"], rowPath + ".coefficients");
            ContractValidationResult<BurnupCoefficientRowV1> rowResult =
                BurnupCoefficientRowV1.TryCreate(burnup, values);
            BurnupCoefficientRowV1 row = Valid(rowResult, rowPath);
            rows.Add(new SourceRow(burnup, values, row));
            previousBurnup = burnup;
            index++;
        }

        return rows;
    }

    private static BurnupCoefficientValuesV1 ReadCoefficients(JsonElement element, string path)
    {
        Dictionary<string, JsonElement> properties = ReadObject(
            element,
            path,
            "absorption_group1_per_m",
            "absorption_group2_per_m",
            "fission_group1_per_m",
            "fission_group2_per_m",
            "nu_fission_group1_per_m",
            "nu_fission_group2_per_m",
            "downscatter_group1_to2_per_m",
            "chi_group1",
            "energy_per_fission_j");
        ContractValidationResult<BurnupCoefficientValuesV1> result =
            BurnupCoefficientValuesV1.TryCreate(
                ReadFiniteDouble(properties["absorption_group1_per_m"], path + ".absorption_group1_per_m"),
                ReadFiniteDouble(properties["absorption_group2_per_m"], path + ".absorption_group2_per_m"),
                ReadFiniteDouble(properties["fission_group1_per_m"], path + ".fission_group1_per_m"),
                ReadFiniteDouble(properties["fission_group2_per_m"], path + ".fission_group2_per_m"),
                ReadFiniteDouble(properties["nu_fission_group1_per_m"], path + ".nu_fission_group1_per_m"),
                ReadFiniteDouble(properties["nu_fission_group2_per_m"], path + ".nu_fission_group2_per_m"),
                ReadFiniteDouble(properties["downscatter_group1_to2_per_m"], path + ".downscatter_group1_to2_per_m"),
                ReadFiniteDouble(properties["chi_group1"], path + ".chi_group1"),
                ReadFiniteDouble(properties["energy_per_fission_j"], path + ".energy_per_fission_j"));
        return Valid(result, path);
    }

    private static BurnupCoefficientTableV1 CreateCoreTable(PackTable table, byte[] checksum, string path)
    {
        ContractValidationResult<BurnupCoefficientTableV1> result =
            BurnupCoefficientTableV1.TryCreate(
                table.TableId,
                BurnupCoefficientTableV1.CurrentSchemaVersion,
                table.DataVersion,
                table.MaterialVariantId,
                table.UnitsProfileId,
                table.SourceProvenance,
                new Digest32(checksum),
                table.Rows.Select(row => row.CoreRow));
        return Valid(result, path);
    }

    private static byte[] SerializeTableIdentity(PackTable table)
    {
        return WriteJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("table_id", table.TableId.ToString());
            writer.WriteNumber("schema_version", BurnupCoefficientTableV1.CurrentSchemaVersion);
            writer.WriteString("data_version", table.DataVersion);
            writer.WriteString("material_variant_id", table.MaterialVariantId.Value);
            writer.WriteString("units_profile_id", table.UnitsProfileId);
            writer.WriteString("source_provenance", table.SourceProvenance);
            WriteRows(writer, table.Rows);
            writer.WriteEndObject();
        });
    }

    private static byte[] SerializePack(SourceModel source, List<PackTable> tables)
    {
        return WriteJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("format", PackFormat);
            writer.WriteString("approval_status", CandidateStatus);
            writer.WriteString("evidence_class", "synthetic");
            writer.WriteString("runtime_use", RuntimeUse);
            writer.WriteString("pack_artifact_id", CandidatePackArtifactId);
            writer.WriteNumber("schema_version", 1U);
            writer.WriteString("data_version", source.DataVersion);
            writer.WriteString("units_profile_id", source.UnitsProfileId);
            writer.WriteString("source_artifact_id", source.SourceArtifactId);
            writer.WriteString("source_provenance", source.SourceProvenance);
            writer.WriteString("transform_id", source.TransformId);
            writer.WritePropertyName("tables");
            writer.WriteStartArray();
            foreach (PackTable table in tables)
            {
                WritePackTable(writer, table);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        });
    }

    private static byte[] SerializeManifest(
        SourceModel source,
        List<PackTable> tables,
        byte[] sourceSha256,
        byte[] packSha256)
    {
        return WriteJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("format", ManifestFormat);
            writer.WriteNumber("manifest_schema_version", 1U);
            writer.WriteString("approval_status", CandidateStatus);
            writer.WriteString("evidence_class", "synthetic");
            writer.WriteString("pack_artifact_id", CandidatePackArtifactId);
            writer.WriteString("pack_format", PackFormat);
            writer.WriteString("pack_sha256", ToHex(packSha256));
            writer.WriteString("source_artifact_id", source.SourceArtifactId);
            writer.WriteString("source_sha256", ToHex(sourceSha256));
            writer.WriteString("source_provenance", source.SourceProvenance);
            writer.WriteString("transform_id", source.TransformId);
            writer.WriteString("data_version", source.DataVersion);
            writer.WriteString("units_profile_id", source.UnitsProfileId);
            writer.WritePropertyName("tables");
            writer.WriteStartArray();
            foreach (PackTable table in tables)
            {
                writer.WriteStartObject();
                writer.WriteString("table_id", table.TableId.ToString());
                writer.WriteString("material_variant_id", table.MaterialVariantId.Value);
                writer.WriteString("checksum", ToHex(table.Checksum));
                writer.WriteNumber("row_count", table.Rows.Count);
                writer.WriteNumber("burnup_min_j_per_kg_hm", table.Rows[0].BurnupJPerKgHm);
                writer.WriteNumber("burnup_max_j_per_kg_hm", table.Rows[^1].BurnupJPerKgHm);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        });
    }

    private static void WritePackTable(Utf8JsonWriter writer, PackTable table)
    {
        writer.WriteStartObject();
        writer.WriteString("table_id", table.TableId.ToString());
        writer.WriteNumber("schema_version", BurnupCoefficientTableV1.CurrentSchemaVersion);
        writer.WriteString("data_version", table.DataVersion);
        writer.WriteString("material_variant_id", table.MaterialVariantId.Value);
        writer.WriteString("units_profile_id", table.UnitsProfileId);
        writer.WriteString("source_provenance", table.SourceProvenance);
        writer.WriteString("checksum", ToHex(table.Checksum));
        WriteRows(writer, table.Rows);
        writer.WriteEndObject();
    }

    private static void WriteRows(Utf8JsonWriter writer, List<SourceRow> rows)
    {
        writer.WritePropertyName("rows");
        writer.WriteStartArray();
        foreach (SourceRow row in rows)
        {
            writer.WriteStartObject();
            writer.WriteNumber("burnup_j_per_kg_hm", row.BurnupJPerKgHm);
            writer.WritePropertyName("coefficients");
            writer.WriteStartObject();
            writer.WriteNumber("absorption_group1_per_m", row.Values.AbsorptionGroup1PerM);
            writer.WriteNumber("absorption_group2_per_m", row.Values.AbsorptionGroup2PerM);
            writer.WriteNumber("fission_group1_per_m", row.Values.FissionGroup1PerM);
            writer.WriteNumber("fission_group2_per_m", row.Values.FissionGroup2PerM);
            writer.WriteNumber("nu_fission_group1_per_m", row.Values.NuFissionGroup1PerM);
            writer.WriteNumber("nu_fission_group2_per_m", row.Values.NuFissionGroup2PerM);
            writer.WriteNumber("downscatter_group1_to2_per_m", row.Values.DownscatterGroup1To2PerM);
            writer.WriteNumber("chi_group1", row.Values.ChiGroup1);
            writer.WriteNumber("energy_per_fission_j", row.Values.EnergyPerFissionJ);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static Dictionary<string, JsonElement> ReadObject(
        JsonElement element,
        string path,
        params string[] allowedNames)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new PackFailure("Json.Object.Required", path, "An object was required.");
        }

        HashSet<string> allowed = new HashSet<string>(allowedNames, StringComparer.Ordinal);
        Dictionary<string, JsonElement> properties = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!allowed.Contains(property.Name))
            {
                throw new PackFailure(
                    "Json.Field.Unknown",
                    path + "." + property.Name,
                    "Unknown fields are rejected by the offline pack contract.");
            }

            if (!properties.TryAdd(property.Name, property.Value))
            {
                throw new PackFailure(
                    "Json.Field.Duplicate",
                    path + "." + property.Name,
                    "Duplicate object fields are rejected.");
            }
        }

        foreach (string requiredName in allowedNames)
        {
            if (!properties.ContainsKey(requiredName))
            {
                throw new PackFailure(
                    "Json.Field.Missing",
                    path + "." + requiredName,
                    "Required field is missing.");
            }
        }

        return properties;
    }

    private static string RequireString(
        Dictionary<string, JsonElement> properties,
        string name,
        string path,
        string expected)
    {
        string actual = ReadRequiredString(properties[name], path);
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new PackFailure("Value.Unexpected", path, "Expected '" + expected + "'.");
        }

        return actual;
    }

    private static string ReadRequiredString(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            throw new PackFailure("Value.String.Required", path, "A string was required.");
        }

        string value = element.GetString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new PackFailure("Value.String.Empty", path, "A non-empty string was required.");
        }

        return value;
    }

    private static string ReadPathFreeIdentifier(JsonElement element, string path)
    {
        string value = ReadRequiredString(element, path);
        if (value.Contains('/') || value.Contains('\\') || value.Contains("..", StringComparison.Ordinal))
        {
            throw new PackFailure("Provenance.Path", path, "Artifact identifiers may not contain paths.");
        }

        return value;
    }

    private static StableId ReadStableId(JsonElement element, string path)
    {
        string value = ReadRequiredString(element, path);
        if (!StableId.TryParse(value, out StableId result) || result.IsEmpty)
        {
            throw new PackFailure("StableId.Invalid", path, "A non-empty canonical UUID was required.");
        }

        return result;
    }

    private static uint ReadSchemaVersion(JsonElement element, string path)
    {
        uint value = ReadUInt32(element, path);
        if (value != BurnupCoefficientTableV1.CurrentSchemaVersion)
        {
            throw new PackFailure("SchemaVersion.Unsupported", path, "Only schema version 1 is supported.");
        }

        return value;
    }

    private static uint ReadUInt32(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Number || !element.TryGetUInt32(out uint value))
        {
            throw new PackFailure("Value.UInt32.Required", path, "A nonnegative UInt32 number was required.");
        }

        return value;
    }

    private static double ReadFiniteDouble(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Number || !element.TryGetDouble(out double value) || !double.IsFinite(value))
        {
            throw new PackFailure("Value.FiniteDouble.Required", path, "A finite JSON number was required.");
        }

        return value;
    }

    private static byte[] ReadDigest(JsonElement element, string path)
    {
        string value = ReadDigestHex(element, path);
        return Convert.FromHexString(value);
    }

    private static string ReadDigestHex(JsonElement element, string path)
    {
        string value = ReadRequiredString(element, path);
        if (value.Length != 64 || !string.Equals(value, value.ToLowerInvariant(), StringComparison.Ordinal))
        {
            throw new PackFailure("Digest.Invalid", path, "A lowercase 32-byte SHA-256 hex digest was required.");
        }

        try
        {
            _ = Convert.FromHexString(value);
        }
        catch (FormatException)
        {
            throw new PackFailure("Digest.Invalid", path, "A lowercase 32-byte SHA-256 hex digest was required.");
        }

        return value;
    }

    private static JsonDocument ParseJson(byte[] bytes, string path, JsonDocumentOptions options)
    {
        try
        {
            return JsonDocument.Parse(bytes, options);
        }
        catch (JsonException exception)
        {
            throw new PackFailure("Json.Invalid", path, exception.Message);
        }
    }

    private static byte[] WriteJson(Action<Utf8JsonWriter> action)
    {
        using MemoryStream stream = new MemoryStream();
        using (Utf8JsonWriter writer = new Utf8JsonWriter(
                   stream,
                   new JsonWriterOptions
                   {
                       Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Default,
                       Indented = true
                   }))
        {
            action(writer);
        }

        string text = Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n') + "\n";
        return Encoding.UTF8.GetBytes(text);
    }

    private static byte[] ReadFile(string path, string role)
    {
        if (!File.Exists(path))
        {
            throw new PackFailure("File.Missing", role, "The " + role + " file does not exist: " + path);
        }

        return File.ReadAllBytes(path);
    }

    private static void WriteFile(string path, byte[] bytes, string role)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(path, bytes);
    }

    private static byte[] Sha256(byte[] bytes)
    {
        return SHA256.HashData(bytes);
    }

    private static string ToHex(byte[] bytes)
    {
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static void EnsureDigestEqual(byte[] expected, byte[] actual, string path, string message)
    {
        if (!CryptographicOperations.FixedTimeEquals(expected, actual))
        {
            throw new PackFailure("Digest.Mismatch", path, message);
        }
    }

    private static T Valid<T>(ContractValidationResult<T> result, string path)
    {
        if (!result.IsValid || result.Value == null)
        {
            string diagnostic = result.IsValid ? "The validated value was unexpectedly null." : result.FirstDiagnostic.ToString();
            throw new PackFailure("CoreContract.Invalid", path, diagnostic);
        }

        return result.Value;
    }

    private static int CompareTables(PackTable left, PackTable right)
    {
        int materialComparison = string.CompareOrdinal(left.MaterialVariantId.Value, right.MaterialVariantId.Value);
        return materialComparison != 0
            ? materialComparison
            : left.TableId.CompareTo(right.TableId);
    }

    private static void RequireArgumentCount(string[] args, int expected, string usage)
    {
        if (args.Length != expected)
        {
            throw new PackFailure("Arguments.Invalid", "arguments", "Usage: " + usage);
        }
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("Usage: generate <source> <pack> <manifest>");
        Console.Error.WriteLine("       validate <pack> <manifest> [source]");
    }

    private sealed class SourceModel
    {
        public SourceModel(
            string sourceArtifactId,
            string sourceProvenance,
            string transformId,
            string dataVersion,
            string unitsProfileId,
            List<SourceTable> tables)
        {
            SourceArtifactId = sourceArtifactId;
            SourceProvenance = sourceProvenance;
            TransformId = transformId;
            DataVersion = dataVersion;
            UnitsProfileId = unitsProfileId;
            Tables = tables;
        }

        public string SourceArtifactId { get; }

        public string SourceProvenance { get; }

        public string TransformId { get; }

        public string DataVersion { get; }

        public string UnitsProfileId { get; }

        public List<SourceTable> Tables { get; }
    }

    private sealed class SourceTable
    {
        public SourceTable(StableId tableId, MaterialVariantId materialVariantId, List<SourceRow> rows)
        {
            TableId = tableId;
            MaterialVariantId = materialVariantId;
            Rows = rows;
        }

        public StableId TableId { get; }

        public MaterialVariantId MaterialVariantId { get; }

        public List<SourceRow> Rows { get; }
    }

    private sealed class SourceRow
    {
        public SourceRow(double burnupJPerKgHm, BurnupCoefficientValuesV1 values, BurnupCoefficientRowV1 coreRow)
        {
            BurnupJPerKgHm = burnupJPerKgHm;
            Values = values;
            CoreRow = coreRow;
        }

        public double BurnupJPerKgHm { get; }

        public BurnupCoefficientValuesV1 Values { get; }

        public BurnupCoefficientRowV1 CoreRow { get; }
    }

    private sealed class PackTable
    {
        public PackTable(
            StableId tableId,
            string dataVersion,
            MaterialVariantId materialVariantId,
            string unitsProfileId,
            string sourceProvenance,
            List<SourceRow> rows)
        {
            TableId = tableId;
            DataVersion = dataVersion;
            MaterialVariantId = materialVariantId;
            UnitsProfileId = unitsProfileId;
            SourceProvenance = sourceProvenance;
            Rows = rows;
        }

        public StableId TableId { get; }

        public string DataVersion { get; }

        public MaterialVariantId MaterialVariantId { get; }

        public string UnitsProfileId { get; }

        public string SourceProvenance { get; }

        public List<SourceRow> Rows { get; }

        public byte[] Checksum { get; private set; } = Array.Empty<byte>();

        public BurnupCoefficientTableV1? CoreTable { get; private set; }

        public void SetChecksumAndCoreTable(byte[] checksum, BurnupCoefficientTableV1 coreTable)
        {
            Checksum = (byte[])checksum.Clone();
            CoreTable = coreTable;
        }
    }

    private sealed class PackModel
    {
        public PackModel(
            string packArtifactId,
            string sourceArtifactId,
            string sourceProvenance,
            string transformId,
            string dataVersion,
            string unitsProfileId,
            string evidenceClass,
            List<PackTable> tables)
        {
            PackArtifactId = packArtifactId;
            SourceArtifactId = sourceArtifactId;
            SourceProvenance = sourceProvenance;
            TransformId = transformId;
            DataVersion = dataVersion;
            UnitsProfileId = unitsProfileId;
            EvidenceClass = evidenceClass;
            Tables = tables;
        }

        public string SourceArtifactId { get; }

        public string PackArtifactId { get; }

        public string SourceProvenance { get; }

        public string TransformId { get; }

        public string DataVersion { get; }

        public string UnitsProfileId { get; }

        public string EvidenceClass { get; }

        public List<PackTable> Tables { get; }
    }

    private sealed class ManifestModel
    {
        public ManifestModel(
            string evidenceClass,
            string packArtifactId,
            string packSha256,
            string sourceArtifactId,
            string sourceSha256,
            string sourceProvenance,
            string dataVersion,
            string unitsProfileId,
            List<ManifestTable> tables)
        {
            EvidenceClass = evidenceClass;
            PackArtifactId = packArtifactId;
            PackSha256 = packSha256;
            SourceArtifactId = sourceArtifactId;
            SourceSha256 = sourceSha256;
            SourceProvenance = sourceProvenance;
            DataVersion = dataVersion;
            UnitsProfileId = unitsProfileId;
            Tables = tables;
        }

        public string EvidenceClass { get; }

        public string PackArtifactId { get; }

        public string PackSha256 { get; }

        public string SourceArtifactId { get; }

        public string SourceSha256 { get; }

        public string SourceProvenance { get; }

        public string DataVersion { get; }

        public string UnitsProfileId { get; }

        public List<ManifestTable> Tables { get; }
    }

    private sealed class ManifestTable
    {
        public ManifestTable(
            StableId tableId,
            string materialVariantId,
            string checksum,
            uint rowCount,
            double burnupMinJPerKgHm,
            double burnupMaxJPerKgHm)
        {
            TableId = tableId;
            MaterialVariantId = materialVariantId;
            Checksum = checksum;
            RowCount = rowCount;
            BurnupMinJPerKgHm = burnupMinJPerKgHm;
            BurnupMaxJPerKgHm = burnupMaxJPerKgHm;
        }

        public StableId TableId { get; }

        public string MaterialVariantId { get; }

        public string Checksum { get; }

        public uint RowCount { get; }

        public double BurnupMinJPerKgHm { get; }

        public double BurnupMaxJPerKgHm { get; }
    }

    private sealed class Artifact
    {
        public Artifact(
            byte[] packBytes,
            byte[] manifestBytes,
            byte[] sourceSha256,
            byte[] packSha256,
            List<PackTable> tables)
        {
            PackBytes = packBytes;
            ManifestBytes = manifestBytes;
            SourceSha256 = sourceSha256;
            PackSha256 = packSha256;
            Tables = tables;
        }

        public byte[] PackBytes { get; }

        public byte[] ManifestBytes { get; }

        public byte[] SourceSha256 { get; }

        public byte[] PackSha256 { get; }

        public List<PackTable> Tables { get; }
    }

    private sealed class PackFailure : Exception
    {
        public PackFailure(string code, string path, string message)
            : base(message)
        {
            Code = code;
            Path = path;
            Message = message;
        }

        public string Code { get; }

        public string Path { get; }

        public new string Message { get; }
    }
}
