using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ReactorSim.Core
{
    public sealed partial class FullCoreDiffusionDataPackV1
    {
        public const string PracticeProfileFormat = "crs-physics-profile";
        public const int MaximumPracticeProfileBytes = 64 * 1024;

        public static ContractValidationResult<FullCoreDiffusionDataPackV1> TryLoadPracticeProfile(string json)
        {
            if (Encoding.UTF8.GetByteCount(json) > MaximumPracticeProfileBytes)
                return Invalid("CustomPhysics.Profile.Size", "profile", "Use a physics profile under 64 KiB.");

            JObject profile;
            try
            {
                profile = JObject.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            }
            catch (JsonException error)
            {
                return Invalid("CustomPhysics.Profile.Json", "profile", error.Message);
            }

            if (profile["format"]?.Type != JTokenType.String || (string?)profile["format"] != PracticeProfileFormat ||
                profile["version"]?.Type != JTokenType.Integer || profile["version"]!.ToString() != "1")
                return Invalid("CustomPhysics.Profile.Version", "profile", "Use a CRS physics profile version 1.");
            if (profile["model"]?.Type != JTokenType.String || (string?)profile["model"] != SupportedModelId ||
                profile["units"]?.Type != JTokenType.String || (string?)profile["units"] != SupportedUnitsProfileId)
                return Invalid("CustomPhysics.Profile.Model", "model", "The profile model and units must match the active CANDU-6 pack.");
            if (profile["basePack"]?.Type != JTokenType.String || profile.Count != 6 || !(profile["parameters"] is JObject parameters))
                return Invalid("CustomPhysics.Profile.Header", "profile", "The profile header and parameters object are required.");

            var edits = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (JProperty section in parameters.Properties())
            {
                string? path = section.Name switch
                {
                    "practice" => section.Name,
                    "geometry" => section.Name,
                    "adjusters" => section.Name,
                    "solver" => section.Name,
                    "fuel_rows" => "coefficient_tables[0].rows",
                    "xenon_rows" => "xenon_reference.rows",
                    _ => null
                };
                if (path == null || !(section.Value is JObject fields))
                    return Invalid("CustomPhysics.Profile.Section", section.Name, "A supported parameter section is required.");
                bool rows = section.Name == "fuel_rows" || section.Name == "xenon_rows";
                if (!TryCollectProfileEdits(fields, path, rows || section.Name == "geometry", rows, edits, out string failure))
                    return Invalid("CustomPhysics.Profile.Parameter", failure, "Use numeric parameters and nonempty groups.");
            }

            return TryLoadPracticeOverrides(edits, (string)profile["basePack"]!);
        }

        private static bool TryCollectProfileEdits(JObject fields, string parent, bool allowGroup, bool rowGroup,
            Dictionary<string, double> edits, out string failure)
        {
            failure = parent;
            if (fields.Count == 0) return false;
            foreach (JProperty field in fields.Properties())
            {
                failure = parent + "." + field.Name;
                string pattern = rowGroup ? @"\A(0|[1-9][0-9]*)\z" : @"\A[a-z][a-z0-9_]*\z";
                if (failure.Length > 100 || !Regex.IsMatch(field.Name, pattern)) return false;
                string path = rowGroup ? parent + "[" + field.Name + "]" : failure;
                if (field.Value is JObject nested && allowGroup)
                {
                    if (!TryCollectProfileEdits(nested, path, false, false, edits, out failure)) return false;
                }
                else if (rowGroup || edits.Count >= 512 ||
                    (field.Value.Type != JTokenType.Integer && field.Value.Type != JTokenType.Float) ||
                    !double.TryParse(field.Value.ToString(Formatting.None), NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ||
                    !edits.TryAdd(path, value))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
