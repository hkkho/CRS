"""Report matched native solver campaigns; only Python's standard library is required."""
import csv
import json
import pathlib
import sys

directory = pathlib.Path(sys.argv[1])
report = json.loads((directory / "comparison.json").read_text())
runs = report["runs"]
seeds = [0, 1001, 1002, 1003, 1004, 4294967295]
policies = ["reference-game-1800", "reference-core-1800", "two-check-core-1800", "two-check-core-180"]
if any(r["Policy"] == "two-check-core-900" for r in runs):
    policies.insert(3, "two-check-core-900")


def find(policy, seed, refuel=False):
    return next((r for r in runs if (r["Policy"], r["Seed"], r["Refuel"]) == (policy, seed, refuel)), None)


def complete(run):
    return run is not None and run["CompletedHours"] == 72 and not run["Terminal"] and not run["Error"]


def fmt(value, digits=3):
    return "—" if value is None else f"{value:.{digits}f}"


def endpoint(run):
    return run["Samples"][-1] if run and run["Samples"] else None


def max_delta(a, b):
    return max(abs(x - y) for x, y in zip(a, b, strict=True))


def comparisons(a, b):
    if not (complete(a) and complete(b)):
        return None
    ea, eb = endpoint(a), endpoint(b)
    return {
        "nodePowerMaxRelativeDifference": max_delta(a["EndNodePowerWatts"], b["EndNodePowerWatts"]) / max(a["EndNodePowerWatts"]),
        "burnupMaxDifferenceMWdPerKg": max_delta(a["EndBurnup"], b["EndBurnup"]),
        "fillMaxDifferencePercentagePoints": max_delta(ea["ZoneFillsPercent"], eb["ZoneFillsPercent"]),
        "reactivityDifferenceMk": 1000 * (eb["Reactivity"] - ea["Reactivity"]),
        "iodineRelativeDifference": eb["Iodine"] / ea["Iodine"] - 1,
        "xenonRelativeDifference": eb["Xenon"] / ea["Xenon"] - 1,
        "energyRelativeDifference": eb["GeneratedEnergyJ"] / ea["GeneratedEnergyJ"] - 1,
    }


lines = ["# Two spatial checks: six 72-hour cases and fuelling comparison", "",
         "Native Release, serial shared CPU solver, profiling scopes compiled out. One measured trajectory per case; these are descriptive timings, not a statistical latency claim or browser-WASM timings.", "",
         "All paths use the current physics pack and analytic iodine/xenon. The Game reference follows the existing aged-core benchmark. Core variants retain the same startup power schedule, integration boundaries, fixed hour-14 actions and half-hour sample times. Shorter-cadence variants additionally check spatial shape between those samples.", "",
         "## Completion and native wall time", "",
         "Core-to-Core comparisons isolate controller/cadence changes. The Game reference additionally includes session and presentation work.", "",
         "| Seed | Current Game, s | Current Core, s | Two checks / 30 min, s | " +
         ("Two checks / 15 min, s | " if "two-check-core-900" in policies else "") + "Two checks / 3 min, s |",
         "|" + "---:|" * (1 + len(policies))]
if "referenceDirectory" in report:
    lines.insert(4, f"The 15-minute cases are freshly measured; earlier policies and recovery traces retain their original measurements from `{report['referenceDirectory']}`. Browser gameplay cadence is unchanged.")
for seed in seeds:
    values = []
    for policy in policies:
        run = find(policy, seed)
        values.append(fmt(run["WallMs"] / 1000, 2) if complete(run) else "FAILED")
    lines.append(f"| {seed} | " + " | ".join(values) + " |")
matched_seeds = [seed for seed in seeds if all(complete(find(policy, seed)) for policy in policies[1:])]
lines += ["", f"Fair aggregate timing uses only matched completed seeds: {', '.join(map(str, matched_seeds))}.", "",
          "| Matched Core policy | Total wall seconds | Relative to current Core |",
          "|---|---:|---:|"]
old_seconds = sum(find(policies[1], seed)["WallMs"] for seed in matched_seeds) / 1000
for policy in policies[1:]:
    seconds = sum(find(policy, seed)["WallMs"] for seed in matched_seeds) / 1000
    lines.append(f"| {policy} | {seconds:.2f} | {seconds/old_seconds:.3f}× time; {100*(1-seconds/old_seconds):.1f}% time reduction |")
lines += ["", "| Policy | Completed seeds | Total wall seconds | Routine candidates | Checks outside controller tolerances | Peak absolute rho, mk | Peak shape error |",
          "|---|---:|---:|---:|---:|---:|---:|"]
for policy in policies:
    cases = [find(policy, seed) for seed in seeds]
    present = [r for r in cases if r is not None]
    lines.append(f"| {policy} | {sum(complete(r) for r in cases)}/6 | {sum(r['WallMs'] for r in present)/1000:.2f} | "
                 f"{sum(r['CandidateSolves'] for r in present)} | {sum(r['NonconvergedChecks'] for r in present)}/{sum(r['ResidualChecks'] for r in present)} | "
                 f"{1000*max(r['MaxAbsoluteReactivity'] for r in present):.6f} | {max(r['MaxShapeError'] for r in present):.6g} |")
lines += ["", f"Controller tolerances: absolute rho ≤ {1000*report['criticalityTolerance']:g} mk and absolute regional fraction error ≤ {report['shapeTolerance']:g}. A spatial solve can converge while the controller misses its regional target; the residual counts above do not label that as a diffusion-solver failure. Candidate counts exclude bootstrap and include explicit fuel events.", "",
          "## Core reference versus authoritative Game reference", ""]
parity = []
for refuel in [False, True]:
    for seed in ([1001] if refuel else seeds):
        delta = comparisons(find(policies[0], seed, refuel), find(policies[1], seed, refuel))
        parity.append({"seed": seed, "refuel": refuel, "difference": delta})
valid = [p["difference"] for p in parity if p["difference"] is not None]
lines.append(f"Across {len(valid)} completed paired cases, maximum endpoint node-power relative difference is "
             f"{max(d['nodePowerMaxRelativeDifference'] for d in valid):.3g}, maximum fill difference is "
             f"{max(d['fillMaxDifferencePercentagePoints'] for d in valid):.3g} percentage points, and maximum bundle-burnup difference is "
             f"{max(d['burnupMaxDifferenceMWdPerKg'] for d in valid):.3g} MWd/kg. This checks the Core comparison harness against Game orchestration.")
if any(d["nodePowerMaxRelativeDifference"] > 1e-7 or d["fillMaxDifferencePercentagePoints"] > 1e-5 for d in valid):
    lines += ["", "**Reference parity exceeded the comparison budget. Treat Core timing/accuracy comparisons as provisional.**"]
lines += ["", "## New solver endpoints relative to the current Core solver", "",
          "| Seed | Cadence | Largest fill difference, pp | Largest node-power difference / reference peak, % | Largest bundle burnup difference, MWd/kg | Xenon total difference, % |",
          "|---:|---|---:|---:|---:|---:|"]
deltas = []
for seed in seeds:
    for policy in policies[2:]:
        delta = comparisons(find(policies[1], seed), find(policy, seed))
        deltas.append({"seed": seed, "policy": policy, "difference": delta})
        if delta:
            lines.append(f"| {seed} | {policy.rsplit('-', 1)[1]} s | {delta['fillMaxDifferencePercentagePoints']:.4f} | "
                         f"{100*delta['nodePowerMaxRelativeDifference']:.4f} | {delta['burnupMaxDifferenceMWdPerKg']:.6f} | {100*delta['xenonRelativeDifference']:.4f} |")
        else:
            lines.append(f"| {seed} | {policy.rsplit('-', 1)[1]} s | FAILED | — | — | — |")

lines += ["", "## Hour-14 fuelling", "",
          "Two eight-bundle insertions at the same clock time. Channels/directions are chosen by the current Game reference and fixed across variants; the no-refuel controls are the corresponding seed-1001 runs above.", "",
          "| Policy | Phase at 14h | Net rho, mk | Mean fill, % | Max channel, MW | Max bundle, kW |",
          "|---|---|---:|---:|---:|---:|"]
for policy in policies:
    run = find(policy, 1001, True)
    if run is None:
        continue
    for sample in run["Samples"]:
        if sample["Hours"] == 14:
            lines.append(f"| {policy} | {sample['Phase']} | {1000*sample['Reactivity']:.6f} | "
                         f"{sum(sample['ZoneFillsPercent'])/14:.4f} | {sample['MaxChannelMw']:.4f} | {sample['MaxBundleKw']:.3f} |")
lines += ["", "| Policy | Completed hours | Full run, s | Refuel 1, ms | Refuel 2, ms | End rho, mk | End mean fill, % |",
          "|---|---:|---:|---:|---:|---:|---:|"]
for policy in policies:
    run = find(policy, 1001, True)
    if run is None:
        continue
    end = endpoint(run)
    actions = run["RefuelActions"]
    lines.append(f"| {policy} | {run['CompletedHours']:g} | {run['WallMs']/1000:.2f} | " +
                 " | ".join(fmt(actions[n]["WallMs"], 2) if n < len(actions) else "—" for n in range(2)) +
                 f" | {fmt(1000*end['Reactivity'], 6) if end else '—'} | {fmt(sum(end['ZoneFillsPercent'])/14, 4) if end else '—'} |")

errors = [r for r in runs if r["Error"] or r["Terminal"] or r["CompletedHours"] != 72]
if errors:
    lines += ["", "## Failures", ""]
    lines.extend(f"- {r['Policy']}, seed {r['Seed']}, refuel={r['Refuel']}: {r['Error'] or r['TerminalReason']}" for r in errors)
expected_energy = report["referenceThermalPowerWatts"] * (72*3600 - 12)
energy_errors = [abs(endpoint(r)["GeneratedEnergyJ"] / expected_energy - 1) for r in runs if complete(r)]
lines += ["", f"Generated-energy ledger maximum relative error: {max(energy_errors):.3g} across completed runs. Discharged energy is not subtracted from the generated-energy ledger.", "",
          "## Historical published results", "",
          "The September 30 archived reports exclude xenon and used earlier controller/tolerance behavior. They are context, not an apples-to-apples speed or solver comparison. The freshly rerun current reference above is the comparison baseline.", ""]
root = pathlib.Path(__file__).resolve().parents[2]
historical_path = root / "artifacts/core-cycle190-benchmark-2026-09-30/report.json"
if historical_path.exists():
    historical = json.loads(historical_path.read_text())
    lines += ["| Seed | Historical peak channel, MW | Current Game peak channel, MW | Two checks / 3 min peak channel, MW |",
              "|---:|---:|---:|---:|"]
    for summary in historical["summaries"]:
        seed = summary["Seed"]
        game = find(policies[0], seed)
        new = find("two-check-core-180", seed)
        peak = lambda r: max(s["MaxChannelMw"] for s in r["Samples"]) if complete(r) else None
        lines.append(f"| {seed} | {summary['PeakChannelMw']:.4f} | {fmt(peak(game), 4)} | {fmt(peak(new), 4)} |")
historical_fuel_path = root / "artifacts/core-cycle190-refuel-response-2026-09-30/report.json"
if historical_fuel_path.exists():
    historical_fuel = json.loads(historical_fuel_path.read_text())
    lines += ["", "Historical no-xenon fuelling checkpoints:", "",
              "| Phase | Net rho, mk | Mean fill, % | Max channel, MW |",
              "|---|---:|---:|---:|"]
    for row in historical_fuel["samples"]:
        if row["Scenario"] == "two-channels-at-14h" and (row["Phase"].startswith("after-refuel") or row["Summary"]["Seconds"] == 72*3600):
            sample = row["Summary"]
            phase = row["Phase"] if sample["Seconds"] == 14*3600 else "72h endpoint"
            lines.append(f"| {phase} | {1000*sample['RegulatedRho']:.6f} | {sample['ZoneMeanPercent']:.4f} | {sample['MaxChannelMw']:.4f} |")
recovery_path = directory / "initialization-recovery.json"
recovery_runs = json.loads(recovery_path.read_text())["runs"] if recovery_path.exists() else []
if recovery_runs:
    lines += ["", "## Bounded startup recovery", "",
              "Seed 4294967295 was rerun with the bootstrap budget increased from 8 to 32, without changing either controller tolerance. The default-budget failures above remain part of the result.", "",
              "| Policy | Completed hours | Bootstrap budget | Wall seconds | Peak rho, mk | Peak shape error | Checks outside tolerances |",
              "|---|---:|---:|---:|---:|---:|---:|"]
    for run in recovery_runs:
        lines.append(f"| {run['Policy']} | {run['CompletedHours']:g} | {run['BootstrapPassBudget']} | {run['WallMs']/1000:.2f} | "
                     f"{1000*run['MaxAbsoluteReactivity']:.6f} | {run['MaxShapeError']:.6g} | {run['NonconvergedChecks']}/{run['ResidualChecks']} |")
    lines += ["", "Including the successful bounded recovery, full six-seed native wall times:", "",
              "| Core policy | Completed seeds | Wall seconds | Relative to current Core |",
              "|---|---:|---:|---:|"]
    full_old_seconds = sum(find(policies[1], seed)["WallMs"] for seed in seeds) / 1000
    for policy in policies[1:]:
        cases = []
        for seed in seeds:
            run = find(policy, seed)
            if not complete(run):
                run = next((r for r in recovery_runs if r["Policy"] == policy and r["Seed"] == seed), run)
            cases.append(run)
        seconds = sum(r["WallMs"] for r in cases) / 1000
        lines.append(f"| {policy} | {sum(complete(r) for r in cases)}/6 | {seconds:.2f} | {seconds/full_old_seconds:.3f}× time; {100*(1-seconds/full_old_seconds):.1f}% time reduction |")
lines += ["", "Full precision: [comparison.json](comparison.json), [samples.csv](samples.csv), [differences.json](differences.json). Recovery: [initialization-recovery.json](initialization-recovery.json).", ""]
(directory / "summary.md").write_text("\n".join(lines), encoding="utf-8")
(directory / "differences.json").write_text(json.dumps({"parity": parity, "endpoints": deltas}, indent=2), encoding="utf-8")
with (directory / "samples.csv").open("w", newline="", encoding="utf-8") as file:
    writer = csv.writer(file)
    writer.writerow(["policy", "seed", "refuel", "hours", "phase", "rho", "max_shape_error", "max_channel_mw", "max_bundle_kw", "average_burnup", "iodine", "xenon", "generated_energy_j"] + [f"zone_{n}_percent" for n in range(1, 15)])
    for run in runs:
        for s in run["Samples"]:
            writer.writerow([run["Policy"], run["Seed"], run["Refuel"], s["Hours"], s["Phase"], s["Reactivity"], s["MaxShapeError"],
                             s["MaxChannelMw"], s["MaxBundleKw"], s["AverageBurnup"], s["Iodine"], s["Xenon"], s["GeneratedEnergyJ"]] + s["ZoneFillsPercent"])
print(directory / "summary.md")
