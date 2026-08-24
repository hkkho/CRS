"""Create the G4-R6 approved ReducedModel payload from the frozen candidate."""

from __future__ import annotations

import copy
import hashlib
import importlib.util
import json
import sys
from pathlib import Path
from typing import Any

sys.dont_write_bytecode = True

ROOT = Path(__file__).resolve().parents[2]
PROGRAM_PATH = Path(__file__).resolve().with_name("Program.py")
SPEC = importlib.util.spec_from_file_location("g4k_program", PROGRAM_PATH)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError("cannot load the G4K producer helpers")
PROGRAM = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(PROGRAM)


def sha256_file(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def write_json(path: Path, value: Any) -> bytes:
    encoded = json.dumps(
        value,
        ensure_ascii=False,
        indent=2,
        allow_nan=False,
        separators=(",", ": "),
    ).encode("utf-8") + b"\n"
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(encoded)
    return encoded


PROFILE_SPECIFICATIONS: dict[str, dict[str, Any]] = {
    "spatial.k": {
        "rule": "absolute_or_relative",
        "absolute_tolerance": 1e-9,
        "relative_tolerance": 1e-8,
        "reference_scale": 1.0,
        "approval_basis": "Frozen P2-T02 eigenvalue/residual policy; all five independent cases converge with a dominant-eigenvalue gap and Core-to-authority k differences below the approved profile.",
    },
    "spatial.flux": {
        "rule": "absolute_or_relative_l_inf",
        "absolute_tolerance": 1e-7,
        "relative_tolerance": 1e-6,
        "reference_scale": 1.0,
        "approval_basis": "Canonical 48-node/two-group L_inf comparison; independent matrix conditioning is below 4 and the smallest dominant-eigenvalue gap is above 0.0025, with Core convergence under the frozen 1e-10 residual policy.",
    },
    "spatial.power": {
        "rule": "absolute_or_relative",
        "absolute_tolerance": 1e-5,
        "relative_tolerance": 1e-7,
        "reference_scale": 1.0,
        "approval_basis": "P2-T02 fission-power equation applied to the independently normalized flux; all five node-power comparisons pass the scoped profile.",
    },
    "spatial.total_power": {
        "rule": "absolute_or_relative",
        "absolute_tolerance": 1e-8,
        "relative_tolerance": 1e-12,
        "reference_scale": 1000.0,
        "approval_basis": "Frozen caller target-power normalization; all five Core totals match the 1000 W target at machine-scale difference.",
    },
    "spatial.residual_relative_inf": {
        "rule": "absolute_or_relative",
        "absolute_tolerance": 1e-9,
        "relative_tolerance": 1e-6,
        "reference_scale": 1.0,
        "approval_basis": "Independent dense matrix residual is approximately 2.5e-15 and Core reports the frozen outer residual at or below 1e-10 for every case; the profile compares the explicitly recorded residual metric.",
    },
    "spatial.convergence": {
        "rule": "exact_status_reason_policy",
        "absolute_tolerance": None,
        "relative_tolerance": None,
        "reference_scale": None,
        "approval_basis": "Exact discrete status, reason, and frozen policy identity; algorithm-specific iteration counts remain diagnostic and are not cross-solver equality fields.",
    },
}


def main() -> int:
    candidate_path = ROOT / "data/comparisons/p4-t06-g4k-independent-authority-v1.json"
    candidate_manifest_path = ROOT / "data/comparisons/p4-t06-g4k-independent-authority-v1.manifest.json"
    definition_path = ROOT / "data/comparisons/p4-t06-g4j-representative-definition-v1.json"
    pack_path = ROOT / "data/packs/p4-t06-r4-reduced-candidate-v1.json"
    authority_definition_path = ROOT / "data/comparisons/p4-t06-g4k-independent-authority-definition-v1.json"
    approved_path = ROOT / "data/golden/p4-t06-g4k-independent-authority-v1.json"
    approved_manifest_path = ROOT / "data/golden/p4-t06-g4k-independent-authority-v1.manifest.json"

    candidate = read_json(candidate_path)
    candidate_manifest = read_json(candidate_manifest_path)
    candidate_hash = sha256_file(candidate_path)
    candidate_manifest_hash = sha256_file(candidate_manifest_path)
    definition_hash = sha256_file(definition_path)
    pack_hash = sha256_file(pack_path)
    authority_definition_hash = sha256_file(authority_definition_path)

    required = {
        "status": "candidate",
        "evidence_approval": "Candidate",
        "comparison_status": "Deferred",
        "tolerance_status": "Deferred",
        "golden_status": "NoGolden",
        "coverage_class": "RepresentativeReducedModel",
        "validation_domain": "ReducedModel",
    }
    for field, expected in required.items():
        if candidate.get(field) != expected:
            raise RuntimeError(f"candidate boundary invalid for {field}")
    if candidate_manifest.get("disposition") != "candidate":
        raise RuntimeError("candidate manifest disposition is not candidate")
    if candidate_manifest.get("artifact_sha256") != candidate_hash:
        raise RuntimeError("candidate artifact hash binding is invalid")
    if candidate_manifest.get("definition_sha256") != definition_hash:
        raise RuntimeError("candidate definition hash binding is invalid")
    if candidate_manifest.get("source_pack_sha256") != pack_hash:
        raise RuntimeError("candidate source-pack hash binding is invalid")
    if candidate_manifest.get("independent_definition_sha256") != authority_definition_hash:
        raise RuntimeError("candidate authority-definition hash binding is invalid")

    approved = copy.deepcopy(candidate)
    approved.update(
        {
            "status": "approved_golden",
            "evidence_class": "approved_reduced_model_reference",
            "artifact_availability": "CommittedSynthetic",
            "evidence_approval": "Approved",
            "comparison_status": "Approved",
            "tolerance_status": "Approved",
            "golden_status": "ApprovedGolden",
            "approval_scope": "Approved golden and tolerance authority for the selected ReducedModel runtime spatial-validation scope only; not direct CANDU, DRAGON5, DONJON5, production, release, safety, or full-core authority.",
        }
    )
    tolerance = approved["tolerance"]
    tolerance.update(
        {
            "status": "Approved",
            "owner_gate": "G4-R6",
            "authority": "G4-R6 reduced-model-only disposition",
            "basis": "Independent dense generalized-eigen reference, complete five-case authority record, focused Core consumer, deterministic/hash-fail-closed regeneration, full T3 regression, and the quantity-specific evidence bases recorded in each approved profile.",
            "diagnostic_bound": None,
        }
    )
    for profile_id, specification in PROFILE_SPECIFICATIONS.items():
        profile = tolerance["profiles"].get(profile_id)
        if profile is None:
            raise RuntimeError(f"missing approved profile {profile_id}")
        profile.update(specification)
        profile["threshold_state"] = "NotApplicable" if profile_id == "spatial.convergence" else "Specified"
        profile["approval_status"] = "Approved"
        profile["owner_gate"] = "G4"
        profile["data_digest"] = PROGRAM.tolerance_profile_digest(profile)

    approved_bytes = write_json(approved_path, approved)
    approved_hash = hashlib.sha256(approved_bytes).hexdigest()
    approved_manifest = {
        "format": "reactorsim.g4k-independent-reduced-authority-manifest/v1",
        "task_id": "G4-R6",
        "source_task_id": "P4-T06-G4K",
        "artifact_id": "p4-t06-g4k-independent-authority-v1",
        "disposition": "approved",
        "coverage_class": "RepresentativeReducedModel",
        "validation_domain": "ReducedModel",
        "golden_status": "ApprovedGolden",
        "evidence_approval": "Approved",
        "approved_artifact_sha256": approved_hash,
        "candidate_artifact_sha256": candidate_hash,
        "candidate_manifest_sha256": candidate_manifest_hash,
        "definition_sha256": definition_hash,
        "source_pack_sha256": pack_hash,
        "independent_definition_sha256": authority_definition_hash,
        "source_snapshot_sha256": approved["source_authority"]["source_snapshot_sha256"],
        "reference_solver": approved["source_authority"]["generator_identity"],
        "scenario_ids": [scenario["scenario_id"] for scenario in approved["scenarios"]],
        "approved_profile_ids": [
            profile["profile_id"] for profile in tolerance["profiles"].values()
        ],
    }
    approved_manifest_bytes = write_json(approved_manifest_path, approved_manifest)
    approved_manifest_hash = hashlib.sha256(approved_manifest_bytes).hexdigest()
    PROGRAM.validate_path_free(approved)
    PROGRAM.validate_path_free(approved_manifest)
    print(
        "P4_T06_G4K_APPROVE_PASS"
        " disposition=approved coverage=RepresentativeReducedModel"
        " validation_domain=ReducedModel scenarios=5 profiles=6"
        f" candidate_artifact_sha256={candidate_hash}"
        f" approved_artifact_sha256={approved_hash}"
        f" approved_manifest_sha256={approved_manifest_hash}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
