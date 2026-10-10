using System;
using System.Collections.Generic;
using System.Text.Json;
using ReactorSim.Game;

namespace ReactorSim.Browser
{
    internal sealed class DailyDispatchProgressDto
    {
        public bool Completed { get; set; }
        public double SimulationSecondsAdvanced { get; set; }
        public double RequestedSimulationSeconds { get; set; }
        public string? ResponseJson { get; set; }
    }

    public sealed partial class PlaytestRuntime
    {
        private GameSession.DailyTurnOperation? _pendingDay;
        private string? _pendingDayCommand;
        private GameSessionCommandResult? _preparedDayResult;

        public string BeginDailyDispatchJson(string commandJson)
        {
            lock (Sync)
            {
                if (_pendingDay != null) return CompletedDailyResponse(Dispatch(commandJson));
                if (!TryParseObject(commandJson, out var document, out _)) return CompletedDailyResponse(Dispatch(commandJson));
                using (document)
                {
                    var root = document!.RootElement;
                    var payload = PlaytestInput.TryGetProperty(root, out var nested, "payload") ? nested : root;
                    if (!PlaytestInput.TryGetProperty(root, out var protocol, "protocol") || protocol.ValueKind != JsonValueKind.String || protocol.GetString() != PlaytestProtocolV2.ProtocolId ||
                        !TryGetString(payload, out string type, "type") || type != "commit-day" ||
                        !TryGetUInt32(payload, out uint day, "expectedCompletedDays") ||
                        !PlaytestInput.TryGetProperty(payload, out var channels, "channelIndices") || channels.ValueKind != JsonValueKind.Array || channels.GetArrayLength() > 380)
                        return CompletedDailyResponse(Dispatch(commandJson));
                    if (PlaytestInput.TryGetProperty(root, out var sequence, "baseSequence", "base_sequence") &&
                        (sequence.ValueKind != JsonValueKind.Number || !sequence.TryGetUInt64(out ulong supplied) || supplied != _runtime.Sequence))
                        return CompletedDailyResponse(Dispatch(commandJson));
                    var indices = new List<uint>();
                    foreach (var channel in channels.EnumerateArray())
                    {
                        if (channel.ValueKind != JsonValueKind.Number || !channel.TryGetUInt32(out uint index)) return CompletedDailyResponse(Dispatch(commandJson));
                        indices.Add(index);
                    }
                    _pendingDay = _runtime.PlaySession.BeginDay(day, indices);
                    _pendingDayCommand = commandJson;
                    return PlaytestProtocolV2.Serialize(new DailyDispatchProgressDto
                    {
                        RequestedSimulationSeconds = _pendingDay.RequestedSimulationSeconds
                    });
                }
            }
        }

        public string ContinueDailyDispatchJson()
        {
            lock (Sync)
            {
                if (_pendingDay == null) throw new InvalidOperationException("No day is being calculated.");
                _pendingDay.CalculateDay();
                if (_pendingDay.Result == null)
                    return PlaytestProtocolV2.Serialize(new DailyDispatchProgressDto
                    {
                        SimulationSecondsAdvanced = _pendingDay.SimulationSecondsAdvanced,
                        RequestedSimulationSeconds = _pendingDay.RequestedSimulationSeconds
                    });
                _preparedDayResult = _pendingDay.Result;
                string command = _pendingDayCommand!;
                _pendingDay = null; _pendingDayCommand = null;
                try { return CompletedDailyResponse(Dispatch(command)); }
                finally { _preparedDayResult = null; }
            }
        }

        private static string CompletedDailyResponse(string response) => PlaytestProtocolV2.Serialize(
            new DailyDispatchProgressDto { Completed = true, ResponseJson = response });
    }
}
