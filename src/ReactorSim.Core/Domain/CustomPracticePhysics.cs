using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ReactorSim.Core
{
    /// <summary>Per-pack tuning for the practice physics outside the diffusion tables.</summary>
    internal sealed class CustomPracticePhysics
    {
        public static readonly CustomPracticePhysics Default = new CustomPracticePhysics(new JObject());

        public CustomPracticePhysics(JObject values, double iodineYield = PracticeXenonDataV1.GammaI,
            double xenonYield = PracticeXenonDataV1.GammaXe, double thermalCaptureM2 = PracticeXenonDataV1.SigmaGroup2M2,
            double thermalWorthScale = 1)
        {
            double Scale(string name) => values.Value<double?>(name) ?? 1;
            ThermalPowerMultiplier = Scale("thermal_power_multiplier");
            ZoneFastAbsorption = PracticeLiquidZoneRrsIdentityV1.Group1AbsorptionPerMPerFillFraction * Scale("lzc_fast_absorption_multiplier");
            ZoneThermalAbsorption = PracticeLiquidZoneRrsIdentityV1.Group2AbsorptionPerMPerFillFraction * Scale("lzc_thermal_absorption_multiplier");
            GammaI = iodineYield * Scale("iodine_yield_multiplier");
            GammaXe = xenonYield * Scale("xenon_yield_multiplier");
            LambdaI = PracticeXenonDataV1.LambdaI / Scale("iodine_half_life_multiplier");
            LambdaXe = PracticeXenonDataV1.LambdaXe / Scale("xenon_half_life_multiplier");
            SigmaFast = values.Value<double?>("xenon_fast_absorption_m2") ?? PracticeXenonDataV1.SigmaGroup1M2;
            SigmaThermalCapture = thermalCaptureM2 * Scale("xenon_thermal_absorption_multiplier");
            // The compact diffusion group's reaction worth is an independently fitted surrogate.
            SigmaThermal = SigmaThermalCapture * thermalWorthScale;
        }

        public double ThermalPowerMultiplier { get; }
        public double ZoneFastAbsorption { get; }
        public double ZoneThermalAbsorption { get; }
        public double GammaI { get; }
        public double GammaXe { get; }
        public double LambdaI { get; }
        public double LambdaXe { get; }
        public double SigmaFast { get; }
        public double SigmaThermal { get; }
        public double SigmaThermalCapture { get; }
    }

    public sealed partial class FullCoreDiffusionDataPackV1
    {
        public const string CustomPracticeVersionPrefix = "custom-practice-v1-";
        internal CustomPracticePhysics PracticePhysics { get; }
        public double PracticeThermalPowerMultiplier => PracticePhysics.ThermalPowerMultiplier;

        public PracticeLiquidZoneRrsMappingV1 CreatePracticeZoneMapping()
        {
            var result = PracticeLiquidZoneRrsMappingV1.TryCreateCandu6(
                PracticePhysics.ZoneFastAbsorption, PracticePhysics.ZoneThermalAbsorption);
            if (!result.IsValid) throw new InvalidOperationException(result.FirstDiagnostic.ToString());
            return result.Value;
        }

        // Numeric overrides can only address existing active fields. Matching Xe
        // burnup knots are derived from the fuel knots, as required by the loader.
        public static ContractValidationResult<FullCoreDiffusionDataPackV1> TryLoadPracticeOverrides(
            IReadOnlyDictionary<string, double> overrides)
        {
            using var stream = typeof(FullCoreDiffusionDataPackV1).Assembly.GetManifestResourceStream(EmbeddedResourceName);
            if (stream == null) return TryLoadEmbeddedCandu6();
            using var reader = new StreamReader(stream, Encoding.UTF8, true);
            var root = JObject.Parse(reader.ReadToEnd());
            var practice = new JObject
            {
                ["thermal_power_multiplier"] = 1.0,
                ["lzc_fast_absorption_multiplier"] = 1.0,
                ["lzc_thermal_absorption_multiplier"] = 1.0,
                ["iodine_yield_multiplier"] = 1.0,
                ["xenon_yield_multiplier"] = 1.0,
                ["iodine_half_life_multiplier"] = 1.0,
                ["xenon_half_life_multiplier"] = 1.0,
                ["xenon_fast_absorption_m2"] = 0.0,
                ["xenon_thermal_absorption_multiplier"] = 1.0
            };
            root["practice"] = practice;
            var fuelRows = (JArray)root["coefficient_tables"]![0]!["rows"]!;
            var xenonRows = (JArray)root["xenon_reference"]!["rows"]!;
            var fields = new[] { root["geometry"]!, root["solver"]!, root["adjusters"]!, fuelRows, xenonRows, practice }
                .SelectMany(section => ((JContainer)section).Descendants().OfType<JValue>())
                .Where(value => (value.Type == JTokenType.Float || value.Type == JTokenType.Integer) &&
                    !value.Path.StartsWith("geometry.vacuum_boundary_conductance_m2.", StringComparison.Ordinal) &&
                    !value.Path.Equals("coefficient_tables[0].rows[0].burnup_j_per_kg_hm", StringComparison.Ordinal) &&
                    (!value.Path.StartsWith("xenon_reference.rows", StringComparison.Ordinal) ||
                        (value.Path.EndsWith(".xe135_number_density_m3", StringComparison.Ordinal) &&
                         !value.Path.StartsWith("xenon_reference.rows[0]", StringComparison.Ordinal))))
                .ToDictionary(value => value.Path, StringComparer.Ordinal);

            foreach (var edit in overrides)
            {
                string path = edit.Key;
                double value = edit.Value;
                if (!fields.TryGetValue(path, out JValue field) || !ContractValidation.IsFinite(value) ||
                    value < 0 || (value == 0 && BitConverter.DoubleToInt64Bits(value) < 0))
                    return Invalid("CustomPhysics.Parameter.Invalid", path, "Use a supported physics parameter and a finite nonnegative value.");
                if (value == field.Value<double>()) continue;

                if (path == "solver.maximum_iterations" || path == "solver.maximum_inner_iterations")
                {
                    // Keep browser work bounded even with an uncooperative model.
                    if (value < 1 || value > field.Value<double>() || value != Math.Truncate(value))
                        return Invalid("CustomPhysics.Iterations.Invalid", path, "Use a whole number between 1 and the default iteration limit.");
                    field.Replace(new JValue((int)value));
                    continue;
                }

                if (path.StartsWith("practice.", StringComparison.Ordinal))
                {
                    double minimum = path == "practice.thermal_power_multiplier" || path.Contains("_half_life_") ? 0.01 : 0;
                    double maximum = path == "practice.xenon_fast_absorption_m2" ? 1e-23 : 10;
                    if (value < minimum || value > maximum)
                        return Invalid("CustomPhysics.PracticeRange.Invalid", path, "The value is outside the supported practice range.");
                }
                field.Replace(new JValue(value));
            }

            for (int n = 0; n < xenonRows.Count; n++)
                xenonRows[n]["burnup_j_per_kg_hm"] = fuelRows[n]["burnup_j_per_kg_hm"]!.DeepClone();

            string version = CustomPracticeVersionPrefix + BitConverter.ToString(Sha256(root.ToString(Formatting.None)))
                .Replace("-", string.Empty).ToLowerInvariant();
            var table = root["coefficient_tables"]![0]!;
            table["data_version"] = version;
            table["source_provenance"] = "Player-configured practice fuel table based on the embedded CANDU-6 pack.";
            table["checksum"] = BitConverter.ToString(Sha256(fuelRows.ToString(Formatting.None))).Replace("-", string.Empty);
            root["data_pack_version"] = version;
            root["evidence_class"] = "custom-practice";
            root["source_provenance"] = "Player-configured practice physics based on the embedded CANDU-6 pack.";
            return TryLoadJson(root.ToString(Formatting.None), practice);
        }
    }
}
