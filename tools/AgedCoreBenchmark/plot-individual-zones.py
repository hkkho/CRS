"""Fourteen separate zone panels from the saved authoritative refuel benchmark."""
import csv
import json
import math
import sys
from pathlib import Path

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.lines import Line2D

directory = Path(sys.argv[1])
report = json.loads((directory / "report.json").read_text(encoding="utf-8"))
with (directory / "zones.csv").open(encoding="utf-8", newline="") as stream:
    rows = list(csv.DictReader(stream))
fuelled = [r for r in rows if r["scenario"] == "two-channels-at-14h"]
control = [r for r in rows if r["scenario"] == "no-refuelling"]
assert len(fuelled) == 147 and len(control) == 145
assert "criticality-first" in report["controllerModel"]
regions = ["Lower left", "Upper left", "Lower centre", "Centre", "Upper centre", "Lower right", "Upper right"]
event = [r for r in fuelled if float(r["hours"]) == 14]
assert [r["phase"] for r in event] == ["before-refuel", "after-refuel-1", "after-refuel-2"]


def draw(ax, zone, xmin, xmax, limits):
    field = f"z{zone}_percent"
    color = "#1768ac" if zone <= 7 else "#d65c19"
    ax.plot([float(r["hours"]) for r in control], [float(r[field]) for r in control],
            color="#64748b", linestyle="--", linewidth=1.4)
    ax.plot([float(r["hours"]) for r in fuelled], [float(r[field]) for r in fuelled],
            color=color, linewidth=1.8)
    ax.scatter([14] * 3, [float(r[field]) for r in event], color=color, s=20, zorder=4)
    ax.axvline(14, color="#555555", linestyle=":", linewidth=1)
    ax.set_title(f"Z{zone} — {regions[(zone - 1) % 7]}, End {'A' if zone <= 7 else 'B'}", loc="left", fontsize=11)
    ax.set_xlim(xmin, xmax)
    ax.set_ylim(*limits)
    ax.set_ylabel("Fill (%)")
    ax.set_xlabel("Simulated hours")
    ax.grid(alpha=0.22)


def grid(filename, xmin, xmax):
    values = [float(r[f"z{z}_percent"]) for r in rows if xmin <= float(r["hours"]) <= xmax for z in range(1, 15)]
    limits = (5 * math.floor((min(values) - 1) / 5), 5 * math.ceil((max(values) + 1) / 5))
    fig, axes = plt.subplots(7, 2, figsize=(15, 20), layout="constrained")
    for pair in range(7):
        for end in range(2):
            draw(axes[pair, end], pair + 1 + 7 * end, xmin, xmax, limits)
    handles = [Line2D([0], [0], color="#1768ac", label="Refuelled: Z1–Z7 (End A)"),
               Line2D([0], [0], color="#d65c19", label="Refuelled: Z8–Z14 (End B)"),
               Line2D([0], [0], color="#64748b", linestyle="--", label="No-refuelling control")]
    fig.legend(handles=handles, loc="outside lower center", ncol=3, frameon=False)
    fig.suptitle(f"Each of the 14 zones individually — seed {report['seed']}\n"
                 "16 fresh bundles at hour 14 | Dotted line: refuelling | Dots: before / after each channel\n"
                 "Every panel uses the same fill scale; left: End A, right: End B", fontsize=14)
    fig.savefig(directory / f"{filename}.png", dpi=150)
    fig.savefig(directory / f"{filename}.svg")
    plt.close(fig)
    return limits


limits = grid("zones-individual", 0, 72)
grid("zones-individual-event", 12, 20)
folder = directory / "individual-zones"
folder.mkdir(exist_ok=True)
for zone in range(1, 15):
    fig, ax = plt.subplots(figsize=(9, 4), layout="constrained")
    draw(ax, zone, 0, 72, limits)
    ax.legend(["No-refuelling control", "16 bundles at hour 14"], loc="lower left")
    fig.savefig(folder / f"zone-{zone:02}.png", dpi=180)
    fig.savefig(folder / f"zone-{zone:02}.svg")
    plt.close(fig)

half = next(r for r in fuelled if float(r["hours"]) == 14.5)
lines = ["# Individual zone response", "", "Saved authoritative seed-1001 benchmark; no new simulation or smoothing.", "",
         "![All fourteen zones](zones-individual.png)", "", "![Refuelling interval](zones-individual-event.png)", "",
         "| Zone | Before % | After channel 75 % | After channel 324 % | At 14.5 h % | Individual plot |",
         "|---:|---:|---:|---:|---:|---|"]
for zone in range(1, 15):
    field = f"z{zone}_percent"
    values = " | ".join(f"{float(r[field]):.3f}" for r in [*event, half])
    lines.append(f"| Z{zone} | {values} | [Z{zone}](individual-zones/zone-{zone:02}.png) |")
(directory / "individual-zones.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
print(f"Created fourteen-panel full/zoom charts, fourteen individual charts, and {directory / 'individual-zones.md'}")
