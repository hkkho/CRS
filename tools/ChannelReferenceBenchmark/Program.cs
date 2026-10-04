using System.Globalization;
using System.Text.Json;
using ReactorSim.Game;

if (args.FirstOrDefault() == "--fit-power")
{
    PowerBalanceCalibration.Run(args.Length > 1 ? args[1] : "tmp/power-balance",
        args.Length > 2 ? args[2] : "data/calibration/power-limit-balance-2026-10-04/source-pack.json");
    return;
}

var reference = PracticeGameSessionFactory.ReferenceChannelPower;
string directory = args.Length == 0 ? "tmp/channel-reference" : args[0];
Directory.CreateDirectory(directory);
File.WriteAllText(Path.Combine(directory, "reference.json"), JsonSerializer.Serialize(new
{
    referenceId = ReactorSim.Core.PracticeChannelPowerReference.ModelId,
    reference.DataPackVersion,
    reference.ThermalPowerWatts,
    meanChannelPowerWatts = reference.ChannelPowerWatts.Average(),
    minimumChannelPowerWatts = reference.ChannelPowerWatts.Min(),
    maximumChannelPowerWatts = reference.ChannelPowerWatts.Max(),
    channelPowerWatts = reference.ChannelPowerWatts,
    starts = new ulong[] { 1001, 1002, 1013 }.Select(seed => new
    {
        seed,
        ripple = PracticeGameSessionFactory.Create(seed).Snapshot.Ripple
    })
}, new JsonSerializerOptions { WriteIndented = true }));
string[] header = { "channel_index,reference_thermal_power_w" };
File.WriteAllLines(Path.Combine(directory, "reference.csv"), header
    .Concat(reference.ChannelPowerWatts.Select((p, i) => i.ToString(CultureInfo.InvariantCulture) + "," + p.ToString("R", CultureInfo.InvariantCulture))));
Console.WriteLine("Reference written to " + directory);
