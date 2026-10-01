"""Render authoritative regional membership and the current absorber footprint."""
import json
import sys
from pathlib import Path
import matplotlib.pyplot as plt
from matplotlib.patches import Rectangle, Patch

root = Path(sys.argv[1])
audit = json.loads((root / "zone-geometry.json").read_text())
colors = ["#c87952", "#daae52", "#6d9c6e", "#43888b", "#758abd", "#aa76a2", "#ba626b",
          "#994d28", "#9c7927", "#426d44", "#246365", "#4c6096", "#795679", "#8d4149"]
fig, axes = plt.subplots(1, 3, figsize=(15, 5.5))
for index, (ax, position, title) in enumerate(zip(axes, [0, 6, 0], ["End A: control regions (bundles 1–6)",
    "End B: control regions (bundles 7–12)", "Default absorber coverage: every node"])):
    for node in audit["nodes"]:
        if node["position"] != position:
            continue
        x, y = node["gridColumn"], node["gridRow"]
        color = colors[node["logicalZoneId"]] if index < 2 else "#43888b"
        ax.add_patch(Rectangle((x - .45, y - .45), .9, .9, facecolor=color, edgecolor="white", linewidth=.3))
    ax.set(xlim=(-1, 22), ylim=(22, -1), aspect="equal", title=title, xlabel="Lattice column (zero-based)", ylabel="Display row (zero-based)")
    ax.set_xticks([0, 7, 14, 21]); ax.set_yticks([0, 7, 14, 21])
fig.legend(handles=[Patch(color=c, label=f"Z{i+1}") for i, c in enumerate(colors)], loc="lower center", ncol=14, frameon=False)
fig.suptitle("Current synthetic zone layout — region membership is not physical absorber tube volume", fontsize=14)
fig.tight_layout(rect=(0, .16, 1, .94))
fig.savefig(root / "zone-regions.png", dpi=170)
fig.savefig(root / "zone-regions.svg")
