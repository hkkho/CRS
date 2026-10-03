"""Refresh the task 26 tool/data index without moving or rewriting physics artifacts."""
import hashlib
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "docs/maintenance/research-inventory.json"
ACTIVE = {
    "AgedCoreBenchmark": ("seeded aged-core observations", "tmp/task26-aged 1001", "seed + embedded packs", "tmp/task26-aged/report.json"),
    "SingleSolveBenchmark": ("experimental solve cadence", "900 2 2", "step/count/checks + embedded packs", "stdout JSON"),
    "CpuSpatialBenchmark": ("CPU row partition comparison", "", "embedded pack; 1/2/4 workers", "stdout JSON"),
    "GameplayBalanceBenchmark": ("score/physical report comparison", "--compare benchmarks/gameplay-balance-v2.json benchmarks/gameplay-balance-v2.json", "tracked baseline/tuned gameplay report", "stdout comparison; checks archived baseline versus tuned results, not a new balance study"),
    "ReplayBookkeepingBenchmark": ("solver-free replay storage scaling", "", "100/1000/10000 commands", "stdout CSV"),
    "Phase4ContractCorpus": ("full browser wire characterization", "tmp/task26-contract.json", "two seeds + commands", "24 response hashes"),
    "ReactorSim.Benchmarks": ("retained three-node static solve", "--warmup 1 --measure 1", "benchmarks/P4-T08-static-solver-benchmark.json", "stdout measurements; P9 profile mode retired"),
}


def main():
    authored = [p for area in ("src", "tests", "tools", "benchmarks", "docs", "data", "reference", "web/candu-playtest/scripts")
                for p in (ROOT / area).rglob("*") if p.is_file()
                and not set(p.parts) & {"bin", "obj", "node_modules"}
                and p.suffix in {".cs", ".csproj", ".py", ".ps1", ".md", ".json", ".mjs"}
                and not p.is_relative_to(ROOT / "docs/maintenance")]
    texts = {p.relative_to(ROOT).as_posix(): p.read_text(encoding="utf-8-sig", errors="replace") for p in authored}

    def consumers(p):
        return sorted(q for q, text in texts.items() if q != p.relative_to(ROOT).as_posix() and p.name in text)

    tools = []
    for p in sorted([*ROOT.glob("tools/**/*.csproj"), *ROOT.glob("benchmarks/**/*.csproj")]):
        name = p.parent.name
        active = name in ACTIVE
        detail = ACTIVE.get(name, ("historical authority/calibration/reproduction", "", "named artifact/schema arguments printed by CLI", "usage only in this audit; no historical numerical rerun claimed"))
        command = f"dotnet run --project {p.relative_to(ROOT).as_posix()} --artifacts-path tmp/task26-{'tools' if active else 'archive'} -- {detail[1]}".rstrip()
        tools.append(dict(source=p.relative_to(ROOT).as_posix(), owner="repository developer-tool maintainers", purpose=detail[0], command=command,
                          inputs=detail[2], outputs=detail[3], consumers=consumers(p),
                          decision="keep maintained" if active else "archive in place; CLI/build retained, not a gameplay dependency",
                          verification=f"tmp/task26-{name}-{'run' if active else 'cli'}.log"))
    for p in sorted([*ROOT.glob("tools/**/*.py"), *ROOT.glob("tools/**/*.ps1"), *ROOT.glob("web/candu-playtest/scripts/*.mjs")]):
        path = p.relative_to(ROOT).as_posix()
        developer = p.parent == ROOT / "tools" and p.name != "build_physics_guide_pdf.py"
        browser = path.startswith("web/")
        decision = "keep developer command" if developer or browser else "archive in place; optional historical/report helper"
        language = "python" if p.suffix == ".py" else "powershell -File" if p.suffix == ".ps1" else "node"
        tools.append(dict(source=path, owner="browser maintainers" if browser else "repository developer-tool maintainers", purpose=("browser verification/measurement helper" if browser else "repository maintenance" if developer else "historical exporter/authority/report helper"),
                          command=f"{language} {path}", inputs="arguments/dependencies in source; browser scripts use a preview URL",
                          outputs="stdout or explicitly selected reports; never runtime data unless Sync-PhysicsPacks -Stage is requested",
                          consumers=consumers(p), decision=decision,
                          verification="tmp/task26-script-checks.json or task26-powershell-checks.json (syntax); browser integration acceptance in task26 browser/smoke logs; external DRAGON/DONJON execution not rerun"))
    artifacts = []
    for area in ("data", "reference"):
        for p in sorted((ROOT / area).rglob("*")):
            if not p.is_file(): continue
            path = p.relative_to(ROOT).as_posix()
            refs = consumers(p)
            owner = "historical reference/fixture collection steward (repository maintainers)"
            status = "archive in place; retain metadata/schema lineage and reproduction references"
            if path == "data/packs/candu6-two-group-diffusion-pack-v1.json":
                owner = "Core authored diffusion pack; canonical source"
                status = "keep canonical; checked embedded mirror; source verification is not a gameplay gate"
            elif any(q.startswith("tests/") for q in refs):
                owner = "executed test fixture collection"
                status = "keep invariant fixture; preserve referenced metadata"
            elif area == "data":
                owner = "archived authority/calibration/schema collection"
            artifacts.append(dict(source=path, owner=owner, consumers=refs, decision=status,
                                  sha256=hashlib.sha256(p.read_bytes()).hexdigest(), bytes=p.stat().st_size))
    for p in sorted((ROOT / "src/ReactorSim.Core/EmbeddedData").glob("*")):
        if p.is_file():
            artifacts.append(dict(source=p.relative_to(ROOT).as_posix(), owner="Core runtime pack/resource collection", consumers=consumers(p),
                                  decision="keep embedded; diffusion mirror checked against data/packs; IQS shared metadata and opt-in WGSL preserved",
                                  sha256=hashlib.sha256(p.read_bytes()).hexdigest(), bytes=p.stat().st_size))
    report = dict(method="Conservative literal filename consumers including metadata/docs; no path moves or artifact rewrites. Empty consumer lists retain explicit archive-collection ownership. Archive status is lifecycle classification in place.", tools=tools, artifacts=artifacts)
    OUT.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(f"Indexed {len(tools)} tools/scripts and {len(artifacts)} artifacts.")


if __name__ == "__main__":
    main()
