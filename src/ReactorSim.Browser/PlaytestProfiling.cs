using System;
using System.Text;
using System.Text.Json;
using ReactorSim.Core;

namespace ReactorSim.Browser
{
    public sealed partial class PlaytestRuntime
    {
        public string DispatchProfileJson(string commandJson)
        {
            if (!RuntimeProfile.ScopesEnabled)
                throw new NotSupportedException("Build with EnableRuntimeProfiling=true to collect runtime timings.");
            lock (Sync)
            {
                using var capture = RuntimeProfile.Begin();
                string result = Dispatch(commandJson);
                using var stream = new System.IO.MemoryStream();
                using (var writer = new Utf8JsonWriter(stream))
                {
                    writer.WriteStartObject();
                    writer.WriteString("resultJson", result);
                    writer.WriteStartArray("profile");
                    foreach (var row in capture.Rows)
                    {
                        writer.WriteStartObject();
                        writer.WriteString("name", row.Name);
                        writer.WriteNumber("calls", row.Calls);
                        writer.WriteNumber("inclusiveMs", row.InclusiveMs);
                        writer.WriteNumber("exclusiveMs", row.ExclusiveMs);
                        writer.WriteEndObject();
                    }
                    writer.WriteEndArray(); writer.WriteEndObject();
                }
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }
    }
}
