using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Text.Json;
using System.Text.Json.Serialization;
using ReactorSim.Core;
using ReactorSim.Game;

namespace ReactorSim.Browser
{
    public sealed partial class PlaytestRuntime
    {
        private static bool TryParseObject(
            string? json,
            out JsonDocument? document,
            out BridgeDiagnosticDto? failure)
        {
            document = null;
            failure = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                failure = PlaytestProtocolV2.Diagnostic(
                    "Browser.Json.Empty",
                    "json",
                    "A non-empty JSON object is required.");
                return false;
            }

            try
            {
                document = JsonDocument.Parse(json);
            }
            catch (JsonException exception)
            {
                failure = PlaytestProtocolV2.Diagnostic(
                    "Browser.Json.Invalid",
                    "json",
                    "The browser command was not valid JSON: " + exception.Message);
                return false;
            }

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                document.Dispose();
                document = null;
                failure = PlaytestProtocolV2.Diagnostic(
                    "Browser.Json.ObjectRequired",
                    "json",
                    "The browser protocol requires a JSON object.");
                return false;
            }

            return true;
        }

        private static bool TryGetString(
            JsonElement value,
            out string result,
            params string[] names)
        {
            result = string.Empty;
            if (!PlaytestInput.TryGetProperty(value, out JsonElement property, names) ||
                property.ValueKind != JsonValueKind.String ||
                property.GetString() == null)
            {
                return false;
            }

            result = property.GetString()!;
            return !string.IsNullOrWhiteSpace(result);
        }

        private static bool TryGetFiniteDouble(
            JsonElement value,
            out double result,
            params string[] names)
        {
            result = 0.0;
            if (!PlaytestInput.TryGetProperty(value, out JsonElement property, names) ||
                property.ValueKind != JsonValueKind.Number ||
                !property.TryGetDouble(out result))
            {
                return false;
            }

            return double.IsFinite(result);
        }

        private static bool TryGetUInt16(
            JsonElement value,
            out ushort result,
            params string[] names)
        {
            result = 0;
            return PlaytestInput.TryGetProperty(value, out JsonElement property, names) &&
                property.ValueKind == JsonValueKind.Number &&
                property.TryGetUInt16(out result);
        }

        private static bool TryGetUInt32(
            JsonElement value,
            out uint result,
            params string[] names)
        {
            result = 0;
            return PlaytestInput.TryGetProperty(value, out JsonElement property, names) &&
                property.ValueKind == JsonValueKind.Number &&
                property.TryGetUInt32(out result);
        }

        private static bool TryGetUInt64(
            JsonElement value,
            out ulong result,
            params string[] names)
        {
            result = 0;
            return PlaytestInput.TryGetProperty(value, out JsonElement property, names) &&
                property.ValueKind == JsonValueKind.Number &&
                property.TryGetUInt64(out result);
        }

        private static string FormatPercent(double value)
        {
            return (value * 100.0).ToString("0.0", CultureInfo.InvariantCulture) + "%";
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }

    }
}
