"""Plot all fourteen authoritative zone fills as seven axial pairs."""
import csv
import json
import sys
from pathlib import Path

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.lines import Line2D

directory = Path(sys.argv[1])
report = json.loads((directory / "report.json").read_text(encoding="utf-8"))
thermal_mw = report["referencePowerWatts"] / 1e6
with (directory / "zones.csv").open(encoding="utf-8", newline="") as stream:
    rows = list(csv.DictReader(stream))
fuelled = [r for r in rows if r["scenario"] == "two-channels-at-14h"]
control = [r for r in rows if r["scenario"] == "no-refuelling"]
assert len(control) == 145 and len(fuelled) == 147
assert float(control[-1]["hours"]) == float(fuelled[-1]["hours"]) == 72
assert [r["phase"] for r in fuelled if float(r["hours"]) == 14] == [
    "before-refuel", "after-refuel-1", "after-refuel-2"]

regions = ["Lower left", "Upper left", "Lower centre", "Centre", "Upper centre", "Lower right", "Upper right"]
events = report["refuellingEvents"]
channels = ", ".join(str(e["channelIndex"]) for e in events)
colors = ["#1768ac", "#d65c19"]

def plot(filename, xmin, xmax):
    fig, axes = plt.subplots(7, 1, figsize=(12, 16), sharex=True, layout="constrained")
    for pair, ax in enumerate(axes):
        for end, zone in enumerate([pair + 1, pair + 8]):
            field = f"z{zone}_percent"
            ax.plot([float(r["hours"]) for r in control], [float(r[field]) for r in control],
                    color=colors[end], linestyle="--", alpha=0.5, linewidth=1.2)
            ax.plot([float(r["hours"]) for r in fuelled], [float(r[field]) for r in fuelled],
                    color=colors[end], linewidth=1.7)
            # Three samples at exactly 14 h retain the immediate sequential
            # equilibrium corrections, without inventing elapsed time.
            event_rows = [r for r in fuelled if float(r["hours"]) == 14]
            ax.scatter([14] * len(event_rows), [float(r[field]) for r in event_rows],
                       color=colors[end], s=16, zorder=4)
        ax.axvline(14, color="#555555", linestyle=":", linewidth=1.1)
        ax.set_title(f"{regions[pair]} — Z{pair + 1} (End A) / Z{pair + 8} (End B)", fontsize=11, loc="left")
        ax.set_ylabel("Fill (%)")
        ax.set_ylim(-2, 102)
        ax.set_yticks([0, 25, 50, 75, 100])
        ax.set_xlim(xmin, xmax)
        ax.grid(alpha=0.22)
    axes[-1].set_xlabel("Simulated hours")
    handles = [Line2D([0], [0], color=colors[0], label="End A: refuelled run"),
               Line2D([0], [0], color=colors[1], label="End B: refuelled run"),
               Line2D([0], [0], color=colors[0], linestyle="--", alpha=0.5, label="End A: no-refuel control"),
               Line2D([0], [0], color=colors[1], linestyle="--", alpha=0.5, label="End B: no-refuel control")]
    fig.legend(handles=handles, loc="outside lower center", ncol=2, frameon=False)
    fig.suptitle(f"Seed {report['seed']}: two channels refuelled at hour 14\n"
                 f"Channels {channels} (zero-based), 8 fresh bundles each; synthetic {thermal_mw:,.0f} MW thermal model\n"
                 "Solid: refuelled run | Dashed: matched no-refuel control | Dotted vertical: refuelling", fontsize=13)
    fig.savefig(directory / f"{filename}.png", dpi=180)
    fig.savefig(directory / f"{filename}.svg")
    plt.close(fig)

plot("zone-pairs", 0, 72)
plot("zone-pairs-event", 12, 20)

fig, axes = plt.subplots(4, 1, figsize=(12, 11), sharex=True, layout="constrained")
for selected, label, color in [(control, "No refuelling", "#64748b"), (fuelled, "16 fresh bundles at 14 h", "#1768ac")]:
    hours = [float(r["hours"]) for r in selected]
    axes[0].plot(hours, [1000 * float(r["unregulated_rho"]) for r in selected], label=label, color=color)
    axes[1].plot(hours, [1000 * (float(r["regulated_rho"]) - float(r["unregulated_rho"])) for r in selected], color=color)
    axes[2].plot(hours, [1000 * float(r["regulated_rho"]) for r in selected], color=color)
    axes[3].plot(hours, [sum(float(r[f"z{z}_percent"]) for z in range(1, 15)) / 14 for r in selected], color=color)
for ax in axes:
    ax.axvline(14, color="#555555", linestyle=":")
    ax.grid(alpha=0.22)
axes[0].set_ylabel("Empty-zone reactivity (mk)")
axes[1].set_ylabel("Zone contribution (mk)")
axes[2].set_ylabel("Net reactivity (mk)")
axes[2].axhline(0, color="#555555", linestyle="--", linewidth=0.8)
axes[3].set_ylabel("Mean zone fill (%)")
axes[3].set_xlabel("Simulated hours")
axes[0].legend()
fig.suptitle(f"Seed {report['seed']}: fuel burnup and zone regulation at {thermal_mw:,.0f} MW thermal\n"
             "Fuel reactivity + zone absorption contribution = net reactivity")
fig.savefig(directory / "reactivity.png", dpi=180)
fig.savefig(directory / "reactivity.svg")
plt.close(fig)

pre, first, second = [r for r in fuelled if float(r["hours"]) == 14]
lines = [f"# Two-channel refuelling response — seed {report['seed']}", "",
         "Two identical authoritative GameSession runs were advanced to 72 hours: a no-refuel control, "
         "and a run with two channel refuellings at exactly hour 14. Each operation inserts eight fresh bundles; "
         "fuel stock falls from 128 to 112. Both operations run sequentially at the same simulation time, "
         "with RRS solving after each. Half-hour samples plus before/after-event samples are retained.", "",
         "One oldest channel is selected for each discharge direction, using live channel-average burnup "
         "and the older eight-bundle outlet. Channels and zone IDs below are zero-based and one-based respectively.", "",
         "| Channel index | Direction | Fresh bundles | Transverse zone pair | Pre-refuel average burnup MWd/kg |",
         "|---:|---|---:|---|---:|"]
for e in events:
    z = e["transverseZonePair"]
    lines.append(f"| {e['channelIndex']} | {e['direction']} | 8 | Z{z}/Z{z + 7} | {e['preChannelAverageBurnupMwDayPerKg']:.3f} |")
lines += ["", "## Immediate zone response at hour 14", "",
          "| Zone | Before % | After first channel % | After both channels % | Combined change pp |",
          "|---:|---:|---:|---:|---:|"]
for z in range(1, 15):
    field = f"z{z}_percent"
    a, b, c = (float(r[field]) for r in [pre, first, second])
    lines.append(f"| Z{z} | {a:.3f} | {b:.3f} | {c:.3f} | {c-a:+.3f} |")
policy = ("Common-mode criticality regulation acts independently of fuel burnup. Shape corrections preserve the criticality band; fill and event bounds remain active."
          if "criticality-first" in report.get("controllerModel", "") else
          "The combined controller can retain fills despite positive net reactivity; these are historical results before criticality and shape were separated.")
lines += ["", "## Response limits and whole-run results", "",
          policy + " These are synthetic equilibrium results, not plant transients.", "",
          "| Zone | Limit % | First reaches limit, hour |", "|---:|---:|---:|"]
limited = False
for z in range(1, 15):
    for limit in [0, 100]:
        saturated = [r for r in fuelled if abs(float(r[f"z{z}_percent"]) - limit) < 1e-8]
        if saturated:
            limited = True
            lines.append(f"| Z{z} | {limit} | {float(saturated[0]['hours']):g} |")
if not limited:
    lines.append("| None | — | — |")
lines += ["", "| Run | Max channel MW | Max bundle kW | Final average zone fill % | Final net reactivity mk |",
          "|---|---:|---:|---:|---:|"]
for title, selected in [("No refuelling", control), ("Two channels at 14 h", fuelled)]:
    final_mean = sum(float(selected[-1][f"z{z}_percent"]) for z in range(1, 15)) / 14
    lines.append(f"| {title} | {max(float(r['max_channel_mw']) for r in selected):.3f} | "
                 f"{max(float(r['max_bundle_kw']) for r in selected):.1f} | {final_mean:.3f} | "
                 f"{1000 * float(selected[-1]['regulated_rho']):+.4f} |")
lines += ["", f"Empty-zone fuel reactivity immediately before/after both operations: "
          f"{1000 * float(pre['unregulated_rho']):.4f} → {1000 * float(second['unregulated_rho']):.4f} mk. "
          f"Net compensated reactivity: {1000 * float(pre['regulated_rho']):+.4f} → "
          f"{1000 * float(second['regulated_rho']):+.4f} mk.", "",
          "The lines at hour 14 show sequential equilibrium corrections at the same timestamp, not a valve transient. "
          "Individual zones respond to both spatial shape and core reactivity. No xenon dynamics are included.", "",
          "![Seven zone pairs over 72 hours](zone-pairs.png)", "",
          "![Seven zone pairs around refuelling](zone-pairs-event.png)", "",
          "![Reactivity and mean zone response](reactivity.png)", "",
          "Full precision: [zones.csv](zones.csv), [report.json](report.json)."]
(directory / "summary.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
print(f"Created seven-panel zone plots and {directory / 'summary.md'}")
