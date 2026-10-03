"""Refresh the conservative task 21 consumer manifest; no physics data is modified."""
import hashlib
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
MANIFEST = ROOT / "docs/maintenance/core-family-consumers.json"
DECLARATION = re.compile(
    r"^    (?:public|internal)\s+(?:(?:sealed|static|readonly|abstract|partial)\s+)*"
    r"(?:class|struct|enum|interface)\s+(\w+)", re.M)


def authored(path):
    return not set(path.parts) & {"bin", "obj", "node_modules"}


def category(path):
    if path.startswith("tests/"):
        return "executed invariant test"
    if path.startswith(("tools/", "benchmarks/")):
        return "retained research/developer tool"
    if path.startswith("src/ReactorSim.Core.Research/"):
        return "retained research implementation"
    return "shared runtime contract"


def main():
    manifest = json.loads(MANIFEST.read_text(encoding="utf-8-sig"))
    files = {
        p.relative_to(ROOT).as_posix(): p.read_text(encoding="utf-8-sig")
        for area in ("src", "tests", "tools", "benchmarks")
        for p in (ROOT / area).rglob("*.cs") if authored(p)
    }
    tokens = {p: set(re.findall(r"\b\w+\b", text)) for p, text in files.items()}
    for record in manifest["types"]:
        path, name = record["source"], record["type"]
        assert path in files and name in DECLARATION.findall(files[path]), (path, name)
        record["currentConsumers"] = [
            {"source": p, "category": category(p)}
            for p in sorted(files) if p != path and name in tokens[p]
        ]
        # Include peer declarations in the same source file, which file-only
        # searches would miss. Four-space closing braces delimit top-level types.
        peers = []
        for declaration in DECLARATION.finditer(files[path]):
            peer = declaration.group(1)
            end = files[path].find("\n    }", declaration.end())
            assert end >= 0, (path, peer)
            body = files[path][declaration.start():end + len("\n    }")]
            if peer != name and name in set(re.findall(r"\b\w+\b", body)):
                peers.append(peer)
        record["sameSourceConsumerTypes"] = peers
        record["consumerCategories"] = sorted({
            c["category"] for c in record["currentConsumers"]
        } | ({category(path)} if peers else set())) or ["no external/peer consumer"]
        record["sourceSha256"] = hashlib.sha256(files[path].encode()).hexdigest()
    manifest["method"] = (
        "Conservative identifier scan including comments and peer type bodies; "
        "generated sources excluded. consumers records pre-relocation paths; "
        "currentConsumers and sameSourceConsumerTypes are refreshed by "
        "python tools/Audit-CoreFamilies.py. Shared contract consumers indicate "
        "compile-time reachability, not that gameplay invokes every method. "
        "No public types deleted; research assembly references are required for relocated APIs."
    )
    MANIFEST.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(f"Checked {len(manifest['types'])} type declarations and their consumers.")


if __name__ == "__main__":
    main()
