using System;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace ReactorSim.Core
{
    /// <summary>
    /// Exercises the reflection-free JSON token path selected by ADR-011.
    /// This infrastructure probe is not a save, replay, or data-pack schema.
    /// </summary>
    public static class JsonSerializationCompatibilityProbe
    {
        private const string ExpectedPayload = "{\"probe\":\"reactor-sim\",\"value\":42}";

        public static string Run()
        {
            var buffer = new StringBuilder();
            using (var textWriter = new StringWriter(buffer, CultureInfo.InvariantCulture))
            using (var jsonWriter = new JsonTextWriter(textWriter))
            {
                jsonWriter.Culture = CultureInfo.InvariantCulture;
                jsonWriter.Formatting = Formatting.None;
                jsonWriter.WriteStartObject();
                jsonWriter.WritePropertyName("probe");
                jsonWriter.WriteValue("reactor-sim");
                jsonWriter.WritePropertyName("value");
                jsonWriter.WriteValue(42);
                jsonWriter.WriteEndObject();
                jsonWriter.Flush();
            }

            string payload = buffer.ToString();
            if (!string.Equals(payload, ExpectedPayload, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The JSON token writer produced an unexpected payload.");
            }

            using (var textReader = new StringReader(payload))
            using (var jsonReader = new JsonTextReader(textReader))
            {
                jsonReader.Culture = CultureInfo.InvariantCulture;
                jsonReader.DateParseHandling = DateParseHandling.None;
                RequireToken(jsonReader, JsonToken.StartObject, null);
                RequireToken(jsonReader, JsonToken.PropertyName, "probe");
                RequireToken(jsonReader, JsonToken.String, "reactor-sim");
                RequireToken(jsonReader, JsonToken.PropertyName, "value");
                RequireToken(jsonReader, JsonToken.Integer, 42L);
                RequireToken(jsonReader, JsonToken.EndObject, null);
                if (jsonReader.Read())
                {
                    throw new InvalidOperationException("The JSON token reader found trailing content.");
                }
            }

            return payload;
        }

        private static void RequireToken(JsonTextReader reader, JsonToken expectedToken, object? expectedValue)
        {
            if (!reader.Read() || reader.TokenType != expectedToken ||
                !Equals(reader.Value, expectedValue))
            {
                throw new InvalidOperationException($"The JSON token reader did not produce {expectedToken}.");
            }
        }
    }
}
