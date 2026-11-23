"""Create a readable report and static plots from authoritative benchmark output."""
import csv
import json
import sys
from pathlib import Path

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

directory = Path(sys.argv[1])
report = json.loads((directory / "report.json").read_text(encoding="utf-8"))
summaries = report["summaries"]
thermal_mw = report["referencePowerWatts"] / 1e6
zone_audit = None
if len(sys.argv) > 2:
    audit_path = Path(sys.argv[2])
    if audit_path.is_dir():
        audit_path /= "zone-worth.json"
    zone_audit = json.loads(audit_path.read_text(encoding="utf-8"))
    if zone_audit["dataPack"] != report["dataPack"]:
        raise ValueError("Zone audit and benchmark must use the same physics pack.")
    if not {s["Seed"] for s in summaries}.issubset({r["Seed"] for r in zone_audit["results"]}):
        raise ValueError("Zone audit must contain every benchmark seed.")
has_probes = all("OfflineProbeReactivityLossMk" in s and "OfflineProbeMeanLossMkPerDay" in s for s in summaries)
def loss(s):
    return s.get("OfflineProbeReactivityLossMk", s["ReactivityLossMk"])
def rate(s):
    return s.get("OfflineProbeMeanLossMkPerDay", s["MeanReactivityLossMkPerDay"])
with (directory / "samples.csv").open(encoding="utf-8", newline="") as stream:
    rows = list(csv.DictReader(stream))

lines = ["# Three-day aged-core benchmark", "",
         f"{len(summaries)} seeded starts, up to 72 simulated hours, no refuelling; native authoritative GameSession with normal RRS. "
         "Samples are captured at half-hour equilibrium boundaries, including startup.", "",
         f"Fission power uses a {thermal_mw:,.0f} MW thermal reference scale. The power schedule and "
         "poison treatment depend on the recorded model. "
         "These are predictions of the synthetic model, not validated plant results.", "",
         "## Whole-run power peaks and final zone fills", "",
         "Power peaks include every half-hour sample. Zone statistics below are across all fourteen zones at each run's final sample.", "",
         "| Seed | Peak channel MW | Peak bundle kW | Zone avg % | Zone min % | Zone max % | Reactivity loss, milli-k | Loss, milli-k/day |",
         "|---:|---:|---:|---:|---:|---:|---:|---:|"]
if "zoneModel" in report:
    zone = report["zoneModel"]
    lines[2:2] = [f"Zone model: `{zone['mappingIdentity']}`. {zone['configuration']} "
                  f"Absorber reference fill: {zone['referenceFill'] * 100:g}%.", ""]
verification_path = directory / "repeat-verification.json"
if verification_path.exists():
    verification = json.loads(verification_path.read_text(encoding="utf-8"))
    if verification.get("trajectoryCsvByteIdentical"):
        lines[2:2] = [f"Repeat verification: all {verification['totalSamples']} trajectory samples are byte-identical "
                      "to the comparison benchmark. "
                      "See [repeat-verification.json](repeat-verification.json) for the baseline and checksum.", ""]
for s in summaries:
    lines.append(f"| {s['Seed']} | {s['PeakChannelMw']:.3f} | {s['PeakBundleKw']:.1f} | "
                 f"{s['FinalZoneMeanPercent']:.2f} | {s['FinalZoneMinPercent']:.2f} | {s['FinalZoneMaxPercent']:.2f} | "
                 f"{loss(s):.3f} | {rate(s):.3f} |")

lines += ["", "Decay is `rho(start) - rho(end)`, with `rho=(k-1)/k`. "
          "A positive loss indicates decreasing reactivity; 1 milli-k = 0.001 rho. Interpret the loss in light of "
          "the reported pack and poison treatment. Rates cover each run's elapsed time, not a constant future slope.", "",
          ("The headline decay values use tighter independent start/end fuel-inventory solves with no "
           "zone or live-xenon overlay. The live gameplay trajectory uses the normal solver; "
           "its poison treatment depends on the recorded model and can differ from the probes." if has_probes else
           "Each headline value uses the tighter endpoint probe when present and otherwise uses the gameplay measurement."), "",
          "## Daily checkpoints", "",
          "Power columns are spatial maxima at each checkpoint, not maxima over the preceding day. "
          "Reactivity columns are live trajectory readings; their start-to-end changes can differ from independent probe losses when those are shown above.", "",
          "| Seed | Day | Max channel MW | Max bundle kW | Zone avg % | Min % | Max % | Unregulated rho, milli-k | Regulated rho, milli-k |",
          "|---:|---:|---:|---:|---:|---:|---:|---:|---:|"]
for s in summaries:
    for r in s["DailyCheckpoints"]:
        lines.append(f"| {s['Seed']} | {r['Seconds']/86400:g} | {r['MaxChannelMw']:.3f} | {r['MaxBundleKw']:.1f} | "
                     f"{r['ZoneMeanPercent']:.2f} | {r['ZoneMinPercent']:.2f} | {r['ZoneMaxPercent']:.2f} | "
                     f"{r['UnregulatedRho']*1000:.3f} | {r['RegulatedRho']*1000:+.3f} |")
lines += ["", "## Zone envelope over the entire run", "",
          "Mean is the equal-weight sample mean of the fourteen-zone average; min/max cover all sampled zones and times.", "",
          "| Seed | Time mean % | Min % | Max % | Peak channel index / hour | Peak bundle channel / position / hour |",
          "|---:|---:|---:|---:|---|---|"]
for s in summaries:
    lines.append(f"| {s['Seed']} | {s['TrajectoryZoneTimeMeanPercent']:.2f} | {s['TrajectoryZoneMinPercent']:.2f} | "
                 f"{s['TrajectoryZoneMaxPercent']:.2f} | {s['PeakChannelIndex']} / {s['PeakChannelHour']:g} | "
                 f"{s['PeakBundleChannelIndex']} / {s['PeakBundlePositionIndex']} / {s['PeakBundleHour']:g} |")
lines += ["", "Channel and bundle indices above are zero-based.", "",
          "## First-day linear prediction and recorded endpoint", "",
          ("| Seed | Day-one gameplay slope extrapolated to 3 days, milli-k loss | Gameplay endpoint loss, milli-k | "
           + ("Tighter endpoint loss" if has_probes else "Selected endpoint loss") + ", milli-k |"),
          "|---:|---:|---:|---:|"]
for s in summaries:
    prediction = s["DayOneLinearPredictionOfThreeDayLossMk"]
    predicted = f"{prediction:.3f}" if prediction is not None else "unavailable"
    lines.append(f"| {s['Seed']} | {predicted} | {s['ReactivityLossMk']:.3f} | {loss(s):.3f} |")
lines += ["", "Compare the extrapolation with each run's recorded duration and endpoint. Exposure and power shape can change over time.", "",
          f"Pack: `{report['dataPack']}`. Completed days by seed: " +
          ", ".join(f"{s['Seed']}: {s['CompletedDays']:g}" for s in summaries) + ". "
          "The benchmark requested no refuelling and checked each sample for channel/bundle/core power conservation.", "",
          "![Benchmark trajectories](trajectories.png)", "",
          "## Reactivity and zone compensation", "",
          "The live net reactivity includes "
          "the changing zone absorption: draining removes absorption and can keep the core close to critical "
          "without adding fuel, until zone control authority is exhausted. The regulated plot uses its own "
          "vertical scale and shows compensated reactivity rather than fuel-only change.", "",
          "![Reactivity and zone fills](reactivity.png)", "",
          ("The fixed-initial-fill panel contains the audit's tight startup and 72-hour endpoint probes. "
           "It shows reactivity with the initial zones retained. The report checks the pack and seed, "
           "but does not verify that the audit and displayed trajectory used identical settings or inventories. "
           "No intermediate frozen-fill trajectory is plotted." if zone_audit else
           "The change panel shows empty-zone reactivity relative to the initial sample."), "",
          "Full precision: [report.json](report.json), [samples.csv](samples.csv)."]
(directory / "summary.md").write_text("\n".join(lines) + "\n", encoding="utf-8")

fig, axes = plt.subplots(2, 2, figsize=(12, 8), sharex=True, layout="constrained")
for s in summaries:
    selected = [r for r in rows if int(r["seed"]) == s["Seed"]]
    hours = [float(r["hours"]) for r in selected]
    initial_rho = float(selected[0]["unregulated_rho"])
    for ax, field in zip(axes.flat, ["max_channel_mw", "max_bundle_kw", "zone_mean_percent", None]):
        values = ([float(r[field]) for r in selected] if field else
                  [(initial_rho - float(r["unregulated_rho"])) * 1000 for r in selected])
        line, = ax.plot(hours, values, label=str(s["Seed"]), linewidth=1.3)
        if field is None and has_probes:
            ax.plot(s["CompletedDays"] * 24, loss(s), "o", color=line.get_color(), markersize=6)
for ax, title, units in zip(axes.flat,
                          ["Maximum channel power", "Maximum bundle power", "Average of 14 zone fills", "Reactivity loss: gameplay trace" + (" + independent probe dots" if has_probes else "")],
                          ["MW", "kW", "Fill (%)", "milli-k"]):
    ax.set_title(title)
    ax.set_ylabel(units)
    ax.set_xlim(0, 72)
    ax.set_xticks([0, 12, 24, 36, 48, 60, 72])
    ax.grid(alpha=0.25)
for ax in axes[-1]:
    ax.set_xlabel("Simulated hours")
handles, labels = axes[0, 0].get_legend_handles_labels()
fig.legend(handles, labels, title="Starting seed", loc="outside lower center", ncol=6, frameon=False)
fig.suptitle(f"Seeded aged cores: up to 3 days without refuelling\nSynthetic model, {thermal_mw:,.0f} MW thermal reference power, normal RRS, half-hour samples", fontsize=13)
fig.savefig(directory / "trajectories.png", dpi=180)
fig.savefig(directory / "trajectories.svg")
plt.close(fig)

# Plot absolute reactivity and, without a zone audit, change from startup.
fig, axes = plt.subplots(2, 2, figsize=(12, 8), layout="constrained")
audit_by_seed = {r["Seed"]: r for r in zone_audit["results"]} if zone_audit else {}
for s in summaries:
    selected = [r for r in rows if int(r["seed"]) == s["Seed"]]
    hours = [float(r["hours"]) for r in selected]
    line, = axes[0, 0].plot(hours, [1000 * float(r["unregulated_rho"]) for r in selected],
                           label=str(s["Seed"]), linewidth=1.5)
    color = line.get_color()
    if has_probes:
        axes[0, 0].scatter([0, s["CompletedDays"] * 24],
                           [1000 * s["InitialOfflineProbe"]["Rho"], 1000 * s["FinalOfflineProbe"]["Rho"]],
                           color=color, s=25, zorder=3)
    axes[1, 0].plot(hours, [1000 * float(r["regulated_rho"]) for r in selected], color=color, linewidth=1)
    axes[1, 1].plot(hours, [float(r["zone_mean_percent"]) for r in selected], color=color, linewidth=1.5)
    if s["Seed"] in audit_by_seed:
        a = audit_by_seed[s["Seed"]]
        initial = a["InitialTightControlledRhoMk"]
        final_fixed = initial - a["FixedInitialZonesBurnupLossMk"]
        axes[0, 1].scatter(0, initial, color=color, marker="o", s=40)
        axes[0, 1].scatter(72, final_fixed, color=color, marker="s", s=40)
    else:
        initial = float(selected[0]["unregulated_rho"])
        axes[0, 1].plot(hours, [1000 * (float(r["unregulated_rho"]) - initial) for r in selected],
                       color=color, linewidth=1.5)

titles = ["Fuel reactivity: empty zones" + (" (independent probe dots)" if has_probes else ""),
          "Initial zones held fixed: endpoint probes only" if zone_audit else "Empty-zone reactivity change from startup",
          "Live net reactivity: zones compensate", "Average of 14 zone fills"]
for ax, title, units in zip(axes.flat, titles, ["Reactivity (mk)", "Reactivity (mk)", "Reactivity (mk)", "Fill (%)"]):
    ax.set_title(title, fontsize=11)
    ax.set_ylabel(units)
    ax.set_xlim(-1, 73)
    ax.set_xticks([0, 12, 24, 36, 48, 60, 72])
    ax.set_xlabel("Simulated hours")
    ax.grid(alpha=0.25)
axes[0, 1].axhline(0, color="black", linewidth=0.8, linestyle="--",
                    label="Critical: rho = 0" if zone_audit else "No change from startup")
axes[1, 0].axhline(0, color="black", linewidth=0.8, linestyle="--", label="Critical: rho = 0")
if zone_audit:
    axes[0, 1].text(0.03, 0.08, "Circles: startup | Squares: audit at 72 h\nNo intermediate fixed-fill trajectory",
                    transform=axes[0, 1].transAxes, fontsize=9)
handles, labels = axes[0, 0].get_legend_handles_labels()
fig.legend(handles, labels, title="Starting seed", loc="outside lower center", ncol=6, frameon=False)
fig.suptitle(f"Reactivity and zone response across recorded runs\nNo refuelling, synthetic {thermal_mw:,.0f} MW thermal model", fontsize=13)
fig.savefig(directory / "reactivity.png", dpi=180)
fig.savefig(directory / "reactivity.svg")
plt.close(fig)
print(f"Created {directory / 'summary.md'}, trajectory and absolute-reactivity plots")
