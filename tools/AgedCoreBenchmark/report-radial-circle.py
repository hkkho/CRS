"""Compare saved authoritative 100-day campaigns; never rerun or smooth physics.

Usage: python tools/AgedCoreBenchmark/report-radial-circle.py ARCHIVE_DIRECTORY
"""
import json
import math
import sys
from pathlib import Path

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.colors import Normalize

root = Path(sys.argv[1])
before = json.loads((root / "baseline.json").read_text(encoding="utf-8-sig"))
after = json.loads((root / "campaign.json").read_text(encoding="utf-8-sig"))
old_reference = json.loads((root / "baseline-reference.json").read_text(encoding="utf-8-sig"))["referenceChannelPowerWatts"]
new_reference = json.loads((root / "reference/reference.json").read_text(encoding="utf-8-sig"))["channelPowerWatts"]

rows = [6, 12, 14, 16, 18, 18, 20, 20, 22, 22, 22, 22, 22, 22, 20, 20, 18, 18, 16, 14, 12, 6]
xy = [(x, 21 - row) for row, length in enumerate(rows) for x in range((22 - length) // 2, (22 + length) // 2)]
radii = [math.hypot(x - 10.5, y - 10.5) for x, y in xy]
rim = [i for i, r in enumerate(radii) if r > 10.7]
middle = [i for i, r in enumerate(radii) if r < 4]

def summary(campaign):
    observations = campaign["dailyObservations"]
    discharges = [b["burnupMwdPerKg"] for o in observations if o["dailyResult"]
                  for m in o["dailyResult"]["movements"] for b in m["bundles"]
                  if "beforePosition" in b and "afterPosition" not in b]
    return {
        "status": campaign["status"], "days": campaign["completedDays"],
        "thermalEnergyMwh": campaign["final"]["thermalEnergyMwh"],
        "electricalEnergyMwh": campaign["final"]["electricalEnergyMwh"],
        "fuelBundles": campaign["final"]["fuelConsumed"],
        "score": campaign["final"]["score"],
        "lzcMinPercent": min(o["averageLzcFraction"] for o in observations) * 100,
        "lzcMaxPercent": max(o["averageLzcFraction"] for o in observations) * 100,
        "lzcFinalPercent": campaign["final"]["averageLzcFraction"] * 100,
        "maxAbsoluteTiltPercent": max(abs(o["axialTiltFraction"]) for o in observations) * 100,
        "maximumChannelKw": max(o["maximumChannelKw"] for o in observations),
        "maximumBundleKw": max(o["maximumBundleKw"] for o in observations),
        "meanDischargeBurnupMwdPerKg": sum(discharges) / len(discharges),
    }

def reference_summary(values):
    return {
        "minimumKw": min(values) / 1000, "maximumKw": max(values) / 1000,
        "outermost20MeanKw": sum(values[i] for i in rim) / len(rim) / 1000,
        "middleMeanKw": sum(values[i] for i in middle) / len(middle) / 1000,
        "thermalPowerMw": sum(values) / 1e6,
    }

comparison = {"baseline": summary(before), "circular": summary(after),
              "baselineReference": reference_summary(old_reference), "circularReference": reference_summary(new_reference),
              "rimDefinition": "20 channel centres farther than 10.7 lattice pitches from centre",
              "middleDefinition": "channel centres within 4 lattice pitches of centre"}
comparison["circularKeffRange"] = [min(o["effectiveK"] for o in after["dailyObservations"]),
                                    max(o["effectiveK"] for o in after["dailyObservations"])]
assert before["completedDays"] == after["completedDays"] == 100
assert before["status"] == after["status"] == "completed"
assert abs(comparison["baseline"]["thermalEnergyMwh"] - comparison["circular"]["thermalEnergyMwh"]) < 1e-4
assert comparison["circular"]["maximumChannelKw"] <= 7100
assert comparison["circular"]["maximumBundleKw"] <= 900
(root / "comparison.json").write_text(json.dumps(comparison, indent=2) + "\n")

fig, axes = plt.subplots(1, 2, figsize=(12, 5), layout="constrained")
for ax, values, title in zip(axes, [old_reference, new_reference], ["Before: fitted face leakage", "After: circular extrapolation"]):
    artist = ax.scatter([x for x, y in xy], [y for x, y in xy], c=[v / 1000 for v in values],
                        s=82, marker="s", cmap="inferno", norm=Normalize(3900, 6800))
    ax.set(aspect="equal", title=title, xlabel="X (lattice pitch)", ylabel="Y (lattice pitch)")
    ax.set_xlim(-1, 22)
    ax.set_ylim(-1, 22)
fig.colorbar(artist, ax=axes, label="Reference channel power (kW)")
fig.suptitle("Fixed time-average references at 2,064 MW thermal")
fig.savefig(root / "reference-comparison.png", dpi=170)
plt.close(fig)

fig, axes = plt.subplots(2, 2, figsize=(11, 7), layout="constrained")
for campaign, label in [(before, "Baseline v8"), (after, "Circular v9")]:
    observations = campaign["dailyObservations"]
    days = [o["simulationDays"] for o in observations]
    for ax, key, factor, ylabel in [
        (axes[0, 0], "averageLzcFraction", 100, "Average LZC (%)"),
        (axes[0, 1], "maximumChannelKw", 1, "Peak channel (kW)"),
        (axes[1, 0], "maximumBundleKw", 1, "Peak bundle (kW)"),
        (axes[1, 1], "fuelConsumed", 1, "Fresh bundles consumed"),
    ]:
        ax.plot(days, [o[key] * factor for o in observations], label=label, linewidth=1.2)
        ax.set(xlabel="Completed days", ylabel=ylabel)
        ax.grid(alpha=.2)
axes[0, 1].axhline(7100, linestyle="--", color="red", linewidth=.8, label="Loss limit")
axes[1, 0].axhline(900, linestyle="--", color="red", linewidth=.8)
axes[0, 0].legend()
axes[0, 1].legend()
fig.suptitle("100 days through the shared Browser bridge · seed 1001 · same oldest-LZC policy")
fig.savefig(root / "campaign-comparison.png", dpi=170)
plt.close(fig)
print(json.dumps(comparison, indent=2))
