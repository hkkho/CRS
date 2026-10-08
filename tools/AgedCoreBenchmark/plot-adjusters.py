"""Plot saved adjuster geometry and authoritative reference; no simulation."""
import json
import sys
from pathlib import Path
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.patches import Rectangle
import numpy as np

fit = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8-sig"))
source = Path(sys.argv[2])
reference = json.loads(source.read_text(encoding="utf-8-sig"))
assert len(fit["rods"]) == 21 and len(reference["channelPowerWatts"]) == 380
fig, axes = plt.subplots(1, 2, figsize=(13, 7), layout="constrained")
for rod in fit["rods"]:
    x, z = rod["HorizontalCentreM"] * 100, rod["AxialCentreM"] * 100
    axes[0].add_patch(Rectangle((x - 28.575/2, z - 49.53/2), 28.575, 49.53,
                              facecolor="#cc704d", edgecolor="black", alpha=.75))
    axes[0].text(x, z, str(rod["Id"]), ha="center", va="center", fontsize=8)
axes[0].set(xlim=(-240, 240), ylim=(0, 594.36), xlabel="Horizontal offset (cm)",
            ylabel="Axial distance from End A (cm)", title="21 rods: plan view (internal IDs)")
for z in np.arange(13)*49.53:
    axes[0].axhline(z, color="gray", linewidth=.5, alpha=.5)
axes[0].set_aspect("equal")
counts=[6,12,14,16,18,18,20,20,22,22,22,22,22,22,20,20,18,18,16,14,12,6]
values=np.full((22,22),np.nan)
index=0
for row,count in enumerate(counts):
    start=(22-count)//2
    values[row,start:start+count]=np.array(reference["channelPowerWatts"][index:index+count])/1e6
    index+=count
im=axes[1].imshow(values, extent=(-11*28.575,11*28.575,-11*28.575,11*28.575),
                  cmap="viridis", interpolation="nearest")
for x in np.arange(-6,7,2)*28.575:
    axes[1].add_patch(Rectangle((x-28.575/2,-171.45),28.575,342.9,
                              fill=False,edgecolor="black",linewidth=1.2))
axes[1].set(xlabel="Horizontal offset (cm)",ylabel="Vertical offset (cm)",
            title="Time-average channel targets; rod regions in black")
fig.colorbar(im,ax=axes[1],label="Reference channel power (MW thermal)",shrink=.8)
fig.suptitle(f"Fixed CANDU-6 adjusters - {fit['measuredWorthMk']:.4f} mk\n"
             "One lattice pitch x one axial bundle length; all fuel retained")
fig.savefig(source.with_name("layout.png"),dpi=160)
plt.close(fig)
