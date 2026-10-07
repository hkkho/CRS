"""Plot the saved 100-day attempt, without running or smoothing the simulation."""
import json
import sys
from pathlib import Path

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

source = Path(sys.argv[1])
report = json.loads(source.read_text(encoding="utf-8-sig"))
if "final" in report and "extrema" in report:
    # Existing LongRunPlaytest saves half-hour accepted snapshots in a JSONL
    # sidecar. Internal Game integration and limit checks still occur at 3 min.
    rows = [json.loads(line) for line in source.with_suffix(".jsonl").read_text(encoding="utf-8-sig").splitlines() if line.strip()]
    report = {
        "seed": report["seed"], "policy": report["policy"],
        "completed": report["final"]["day"] == report["days"] and report["final"]["RunStatus"] == "running",
        "terminal": report["final"]["RunStatus"] == "ended", "reason": report["final"]["GameOverReason"],
        "completedDays": report["final"]["day"], "operations": report["final"]["RefuellingOperationCount"],
        "samples": [{"days": r["day"], "meanFillPercent": r["lzc"] * 100,
                     "zoneFillsPercent": [f * 100 for f in r["zoneFills"]],
                     "channelKw": r["maxChannelKw"], "bundleKw": r["maxBundleKw"],
                     "tiltPercent": r["tilt"] * 100, "FuelConsumed": r["RefuellingOperationCount"] * 8} for r in rows]
    }
samples = report["samples"]
days = [s["days"] for s in samples]
fig, axes = plt.subplots(4, 1, figsize=(11, 11), sharex=True, layout="constrained")
for zone in range(14):
    axes[0].plot(days, [s["zoneFillsPercent"][zone] for s in samples], lw=.65, alpha=.6)
axes[0].plot(days, [s["meanFillPercent"] for s in samples], color="black", lw=1.3, label="Core mean")
axes[0].axhline(45, color="black", linestyle=":", lw=.8, label="Fuelling trigger")
axes[0].set_ylabel("Zone fill (%)")
axes[0].legend(loc="upper right")
axes[1].plot(days, [s["channelKw"] / 7300 * 100 for s in samples], label="Peak channel / 7,300 kW")
axes[1].plot(days, [s["bundleKw"] / 935 * 100 for s in samples], label="Peak bundle / 935 kW")
axes[1].axhline(100, color="firebrick", linestyle="--", lw=1, label="Run limit")
axes[1].set_ylabel("Power limit (%)")
axes[1].legend(loc="lower right")
axes[2].plot(days, [s["tiltPercent"] for s in samples], color="purple")
axes[2].axhline(20, color="firebrick", linestyle="--", lw=.8)
axes[2].axhline(-20, color="firebrick", linestyle="--", lw=.8)
axes[2].set_ylabel("Axial tilt (%)")
axes[3].step(days, [s["FuelConsumed"] for s in samples], where="post", color="darkgreen")
axes[3].set_ylabel("Fresh bundles used")
axes[3].set_xlabel("Full-power days elapsed")
for axis in axes:
    axis.grid(alpha=.2)
    axis.set_xlim(0, max(days))
outcome = "Completed" if report["completed"] else f"Stopped: {report['reason']}" if report["terminal"] else "In progress"
policy_name = "Power-headroom fuelling" if report["policy"] == "reserve" else "Regional fuelling" if "paired regional" in report["policy"] else "Oldest-channel fuelling"
fig.suptitle(f"100-day fuelling capability attempt — seed {report['seed']}\n"
             f"{outcome} at day {report['completedDays']:.3f} · {report['operations']} eight-bundle moves\n"
             f"Shared browser rules · 3-minute LZC steps · {policy_name} below 45% mean fill")
fig.savefig(source.with_name("fuelling-capability.png"), dpi=160)
plt.close(fig)
