"""Offline independent reduced-model authority producer for P4-T06-G4K.

This tool intentionally does not import ReactorSim.Core or the G4J producer.
It reads the frozen G4J semantic input by hash and computes expected spatial
results through a dense generalized-eigenvalue path using NumPy linear
algebra. The output is candidate evidence until G4-R6 approves it.
"""

from __future__ import annotations

import argparse
import hashlib
import importlib.metadata
import json
import math
import platform
import struct
import sys
from pathlib import Path
from typing import Any

import numpy as np


EXPECTED_DEFINITION_SHA256 = (
    "bf227e60bd2c9540c699786c530c079bb31caf4108cf461704f37c81f6758b6e"
)
EXPECTED_PACK_SHA256 = (
    "0de429b27f78b5e2c5fe724916b14ac8ce45ba05560ab73f3fa4805befd11a0e"
)


class AuthorityFailure(RuntimeError):
    """Fail-closed error for invalid authority inputs or reference results."""


def fail(message: str) -> None:
    raise AuthorityFailure(message)


def read_json(path: Path) -> Any:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        fail(f"cannot read JSON {path.name}: {exc}")


def sha256_file(path: Path) -> str:
    try:
        return hashlib.sha256(path.read_bytes()).hexdigest()
    except OSError as exc:
        fail(f"cannot hash {path.name}: {exc}")


def write_json(path: Path, value: Any) -> bytes:
    try:
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
    except (OSError, TypeError, ValueError) as exc:
        fail(f"cannot write JSON {path.name}: {exc}")


def require(condition: bool, message: str) -> None:
    if not condition:
        fail(message)


def finite(value: float, label: str) -> float:
    number = float(value)
    require(math.isfinite(number), f"non-finite value at {label}")
    return number


def profile_string(value: str) -> bytes:
    encoded = value.encode("utf-8")
    return struct.pack("<I", len(encoded)) + encoded


def tolerance_profile_bytes(profile: dict[str, Any]) -> bytes:
    value_kind_ordinals = {
        "Scalar": 0,
        "Vector": 1,
        "Bytes": 3,
    }
    norm_ordinals = {
        "Scalar": 0,
        "L_inf": 1,
        "Invariant": 4,
    }
    acceptance_ordinals = {
        "Exact": 0,
        "Or": 3,
    }
    invariant_ordinals = {
        "NotApplicable": 0,
        "Equality": 1,
    }
    applicability_ordinals = {"Always": 0}
    threshold_ordinals = {"NotApplicable": 0, "Deferred": 1, "Specified": 2}
    approval_ordinals = {"Deferred": 0, "Provisional": 1, "Approved": 2}
    owner_gate_ordinals = {"G2": 0, "G3": 1, "G4": 2, "G5": 3, "G6": 4, "G7A": 5, "G7B": 6}

    def typed_uint8(value: int) -> bytes:
        return b"\x01" + bytes((value,))

    def typed_utf8(value: str) -> bytes:
        encoded = value.encode("utf-8")
        return b"\x0A" + struct.pack("<I", len(encoded)) + encoded

    def typed_optional_float(value: Any) -> bytes:
        if value is None:
            return b"\x10"
        return b"\x07" + struct.pack("<d", finite(value, "profile.threshold"))

    try:
        result = bytearray(b"CANDU-TOLERANCE-V1")
        result.extend(struct.pack("<I", 1))
        result.extend(profile_string(str(profile["profile_id"])))
        result.extend(profile_string(str(profile["quantity_id"])))
        result.extend(profile_string(str(profile["unit"])))
        result.append(value_kind_ordinals[str(profile["value_kind"])])
        result.extend(profile_string(str(profile["payload_schema_id"])))
        component_order = profile["component_order_spec"]
        if component_order == "NotApplicable":
            result.extend(b"\x10")
        else:
            result.extend(b"\x0D")
            result.extend(struct.pack("<I", 4))
            result.extend(typed_uint8(int(component_order["order_kind_ordinal"])))
            result.extend(typed_utf8(str(component_order["component_key_schema_id"])))
            result.extend(typed_uint8(int(component_order["comparator_ordinal"])))
            result.extend(typed_utf8(str(component_order["tie_break_schema_id"])))
        result.append(norm_ordinals[str(profile["norm"])])
        result.append(acceptance_ordinals[str(profile["acceptance"])])
        result.append(invariant_ordinals[str(profile["invariant_kind"])])
        result.append(applicability_ordinals[str(profile["applicability"])])
        result.extend(profile_string(str(profile["comparison_rule_id"])))
        for field_name in ("absolute_tolerance", "relative_tolerance", "reference_scale"):
            result.extend(typed_optional_float(profile.get(field_name)))
        result.append(threshold_ordinals[str(profile["threshold_state"])])
        result.append(approval_ordinals[str(profile["approval_status"])])
        result.append(owner_gate_ordinals[str(profile["owner_gate"])])
        return bytes(result)
    except (KeyError, TypeError, ValueError, struct.error) as exc:
        fail(f"invalid tolerance profile encoding: {exc}")


def tolerance_profile_digest(profile: dict[str, Any]) -> str:
    return hashlib.sha256(tolerance_profile_bytes(profile)).hexdigest()


def distribution_record_sha256(distribution_name: str) -> str:
    try:
        distribution = importlib.metadata.distribution(distribution_name)
        record = next(
            file for file in distribution.files or [] if str(file).endswith(".dist-info/RECORD")
        )
        return sha256_file(Path(distribution.locate_file(record)))
    except (importlib.metadata.PackageNotFoundError, StopIteration, OSError) as exc:
        fail(f"cannot bind {distribution_name} distribution record: {exc}")


def build_provenance() -> dict[str, Any]:
    try:
        config = np.show_config(mode="dicts")
    except TypeError:
        config = {}
    compilers = config.get("Compilers", {})
    compiler_c = compilers.get("c", {})
    dependencies = config.get("Build Dependencies", {})
    blas = dependencies.get("blas", {})
    lapack = dependencies.get("lapack", {})
    simd = config.get("SIMD Extensions", {})
    return {
        "python_version": platform.python_version(),
        "python_implementation": platform.python_implementation(),
        "numpy_version": np.__version__,
        "numpy_distribution_record_sha256": distribution_record_sha256("numpy"),
        "platform": platform.platform(),
        "machine": platform.machine(),
        "byteorder": sys.byteorder,
        "compiler": {
            "name": str(compiler_c.get("name", "Unavailable")),
            "version": str(compiler_c.get("version", "Unavailable")),
        },
        "blas": {
            "name": str(blas.get("name", "Unavailable")),
            "version": str(blas.get("version", "Unavailable")),
            "configuration": str(blas.get("openblas configuration", "Unavailable")),
        },
        "lapack": {
            "name": str(lapack.get("name", "Unavailable")),
            "version": str(lapack.get("version", "Unavailable")),
            "configuration": str(lapack.get("openblas configuration", "Unavailable")),
        },
        "simd_baseline": [str(value) for value in simd.get("baseline", [])],
        "simd_found": [str(value) for value in simd.get("found", [])],
    }


def flat_index(channel: int, position: int, position_count: int) -> int:
    return channel * position_count + position


def endpoint(channel: int, position: int, position_count: int) -> dict[str, int]:
    return {
        "channel_id": channel,
        "position": position,
        "flat_index": flat_index(channel, position, position_count),
    }


def load_inputs(
    definition_path: Path,
    pack_path: Path,
    authority_definition_path: Path,
) -> tuple[dict[str, Any], dict[str, Any], dict[str, Any], str, str, str]:
    definition = read_json(definition_path)
    pack = read_json(pack_path)
    authority_definition = read_json(authority_definition_path)
    definition_hash = sha256_file(definition_path)
    pack_hash = sha256_file(pack_path)
    authority_definition_hash = sha256_file(authority_definition_path)

    require(
        definition_hash == EXPECTED_DEFINITION_SHA256,
        f"G4J definition hash mismatch: {definition_hash}",
    )
    require(
        pack_hash == EXPECTED_PACK_SHA256,
        f"source pack hash mismatch: {pack_hash}",
    )
    reuse = authority_definition.get("input_reuse", {})
    require(
        reuse.get("source_definition_sha256") == definition_hash,
        "independent authority definition does not bind the G4J definition hash",
    )
    require(
        reuse.get("source_pack_sha256") == pack_hash,
        "independent authority definition does not bind the source-pack hash",
    )
    require(
        authority_definition.get("input_reuse", {}).get("expected_outputs_reused")
        is False,
        "independent authority definition allows expected-output reuse",
    )
    require(
        authority_definition.get("input_reuse", {}).get("input_manifest_is_independent")
        is True,
        "independent authority definition is not marked independent",
    )
    require(
        authority_definition.get("scenario_ids")
        == [
            "fresh_start",
            "equilibrium_like",
            "refuelled_4_bundle_shift",
            "rrs_tilt_perturbation",
            "bulk_poison_perturbation",
        ],
        "independent scenario coverage is incomplete or reordered",
    )
    require(
        pack.get("tables") and len(pack["tables"]) == 1,
        "source pack must contain exactly one table",
    )
    require(
        pack["tables"][0].get("rows") and len(pack["tables"][0]["rows"]) >= 2,
        "source pack must contain at least two rows",
    )
    return (
        definition,
        pack,
        authority_definition,
        definition_hash,
        pack_hash,
        authority_definition_hash,
    )


def interpolate(left: float, right: float, fraction: float) -> float:
    return float(left + (right - left) * fraction)


def lookup_material(rows: list[dict[str, Any]], burnup: float) -> dict[str, float]:
    if burnup <= float(rows[0]["burnup_j_per_kg_hm"]):
        source = rows[0]["coefficients"]
        return {key: finite(value, f"material.{key}") for key, value in source.items()}

    for index in range(1, len(rows)):
        right_burnup = float(rows[index]["burnup_j_per_kg_hm"])
        if burnup <= right_burnup:
            left = rows[index - 1]
            right = rows[index]
            left_burnup = float(left["burnup_j_per_kg_hm"])
            fraction = (burnup - left_burnup) / (right_burnup - left_burnup)
            left_values = left["coefficients"]
            right_values = right["coefficients"]
            return {
                key: interpolate(float(left_values[key]), float(right_values[key]), fraction)
                for key in left_values
            }

    source = rows[-1]["coefficients"]
    return {key: finite(value, f"material.{key}") for key, value in source.items()}


def material_bracket(rows: list[dict[str, Any]], burnup: float) -> str:
    if burnup <= float(rows[0]["burnup_j_per_kg_hm"]):
        return "0:0"
    for index in range(1, len(rows)):
        if burnup <= float(rows[index]["burnup_j_per_kg_hm"]):
            return f"{index - 1}:{index}"
    last = len(rows) - 1
    return f"{last}:{last}"


def scenario_burnup(scenario: dict[str, Any], channel: int, position: int) -> float:
    base = scenario["base_burnup_by_position_j_per_kg_hm"]
    offsets = scenario["channel_burnup_offset_j_per_kg_hm"]
    pre_event = [float(value) + float(offsets[channel]) for value in base]
    shift = next(
        (
            event
            for event in scenario["events"]
            if event.get("event_type") == "refuel_shift"
            and int(event.get("channel_id")) == channel
        ),
        None,
    )
    if shift is None:
        if position in scenario["fresh_positions_by_channel"][channel]:
            return 0.0
        return pre_event[position]
    direction = shift["shift_direction"]
    if direction == "TowardEndB":
        return 0.0 if position < 4 else pre_event[position - 4]
    if direction == "TowardEndA":
        return 0.0 if position >= 8 else pre_event[position + 4]
    fail(f"unsupported refuel direction {direction}")


def overlay_values(overlay: dict[str, Any], channel: int) -> tuple[float, float]:
    kind = overlay["kind"]
    if kind == "none":
        return 0.0, 0.0
    if kind == "rrs":
        delta = float(overlay["state_fraction"]) - float(overlay["reference_fraction"])
        return (
            float(overlay["weights_group1_per_unit_by_channel"][channel]) * delta,
            float(overlay["weights_group2_per_unit_by_channel"][channel]) * delta,
        )
    if kind == "bulk_poison":
        concentration = float(overlay["poison_mass_kg"]) / float(overlay["moderator_volume_m3"])
        delta = concentration - float(overlay["reference_concentration_kg_per_m3"])
        return (
            float(overlay["weights_group1_per_concentration_by_channel"][channel]) * delta,
            float(overlay["weights_group2_per_concentration_by_channel"][channel]) * delta,
        )
    fail(f"unsupported overlay kind {kind}")


def build_geometry(definition: dict[str, Any]) -> dict[str, Any]:
    topology = definition["topology"]
    conductances = definition["conductances"]
    channel_count = int(topology["channel_count"])
    position_count = int(topology["bundle_position_count"])
    channels = topology["channels"]
    nodes = [
        {
            "flat_index": flat_index(channel, position, position_count),
            "channel_id": channel,
            "position": position,
            "volume_m3": float(definition["node_geometry"]["volume_base_m3"])
            + channel * float(definition["node_geometry"]["volume_channel_increment_m3"])
            + position * float(definition["node_geometry"]["volume_position_increment_m3"]),
        }
        for channel in range(channel_count)
        for position in range(position_count)
    ]
    edges: list[dict[str, Any]] = []
    for channel in range(channel_count):
        for position in range(position_count - 1):
            edges.append(
                {
                    "endpoint_a": endpoint(channel, position, position_count),
                    "endpoint_b": endpoint(channel, position + 1, position_count),
                    "direction_a_to_b": "TowardEndB",
                    "direction_b_to_a": "TowardEndA",
                    "group1_m2": float(conductances["axial_edge_group1_m2"]),
                    "group2_m2": float(conductances["axial_edge_group2_m2"]),
                }
            )
    for link in topology["transverse_links"]:
        for position in range(position_count):
            edges.append(
                {
                    "endpoint_a": endpoint(
                        int(link["endpoint_a_channel_id"]), position, position_count
                    ),
                    "endpoint_b": endpoint(
                        int(link["endpoint_b_channel_id"]), position, position_count
                    ),
                    "direction_a_to_b": link["direction_a_to_b"],
                    "direction_b_to_a": link["direction_b_to_a"],
                    "group1_m2": float(conductances["transverse_edge_group1_m2"]),
                    "group2_m2": float(conductances["transverse_edge_group2_m2"]),
                }
            )
    boundaries: list[dict[str, Any]] = []
    for channel_record in channels:
        channel = int(channel_record["channel_id"])
        x = int(channel_record["coordinate_x"])
        y = int(channel_record["coordinate_y"])
        for position in range(position_count):
            node = endpoint(channel, position, position_count)
            if x == 0:
                boundaries.append(
                    {"node": node, "face": "West", "classification": "Reflective", "group1_m2": 0.0, "group2_m2": 0.0}
                )
            if x == 1:
                boundaries.append(
                    {"node": node, "face": "East", "classification": "Reflective", "group1_m2": 0.0, "group2_m2": 0.0}
                )
            if y == 0:
                boundaries.append(
                    {"node": node, "face": "South", "classification": "Reflective", "group1_m2": 0.0, "group2_m2": 0.0}
                )
            if y == 1:
                boundaries.append(
                    {"node": node, "face": "North", "classification": "Reflective", "group1_m2": 0.0, "group2_m2": 0.0}
                )
            if position == 0:
                boundaries.append(
                    {
                        "node": node,
                        "face": "EndA",
                        "classification": topology["end_boundary_classification"],
                        "group1_m2": float(conductances["end_boundary_group1_m2"]),
                        "group2_m2": float(conductances["end_boundary_group2_m2"]),
                    }
                )
            if position == position_count - 1:
                boundaries.append(
                    {
                        "node": node,
                        "face": "EndB",
                        "classification": topology["end_boundary_classification"],
                        "group1_m2": float(conductances["end_boundary_group1_m2"]),
                        "group2_m2": float(conductances["end_boundary_group2_m2"]),
                    }
                )
    require(len(nodes) == 48, "independent geometry node count changed")
    require(len(edges) == 92, "independent geometry edge count changed")
    require(len(boundaries) == 104, "independent geometry boundary count changed")
    return {
        "channel_count": channel_count,
        "bundle_position_count": position_count,
        "node_count": len(nodes),
        "channels": channels,
        "nodes": nodes,
        "edges": edges,
        "boundaries": boundaries,
    }


def build_coefficients(
    definition: dict[str, Any],
    pack: dict[str, Any],
    scenario: dict[str, Any],
) -> list[dict[str, Any]]:
    projection = definition["material_projection"]
    rows = pack["tables"][0]["rows"]
    position_count = int(definition["topology"]["bundle_position_count"])
    result: list[dict[str, Any]] = []
    for channel in range(int(definition["topology"]["channel_count"])):
        for position in range(position_count):
            index = flat_index(channel, position, position_count)
            burnup = scenario_burnup(scenario, channel, position)
            require(0.0 <= burnup <= float(rows[-1]["burnup_j_per_kg_hm"]), f"burnup out of range at {index}")
            material = lookup_material(rows, burnup)
            overlay1, overlay2 = overlay_values(scenario["overlay"], channel)
            channel_abs1 = float(projection["channel_absorption_group1_multiplier"][channel])
            channel_abs2 = float(projection["channel_absorption_group2_multiplier"][channel])
            channel_fission = float(projection["channel_fission_multiplier"][channel])
            channel_downscatter = float(projection["channel_downscatter_multiplier"][channel])
            base_abs1 = material["absorption_group1_per_m"] * channel_abs1 * float(projection["position_absorption_group1_multiplier"][position])
            base_abs2 = material["absorption_group2_per_m"] * channel_abs2 * float(projection["position_absorption_group2_multiplier"][position])
            fission_multiplier = channel_fission * float(projection["position_fission_multiplier"][position])
            downscatter_multiplier = channel_downscatter * float(projection["position_downscatter_multiplier"][position])
            abs1 = base_abs1 + overlay1
            abs2 = base_abs2 + overlay2
            fission1 = material["fission_group1_per_m"] * fission_multiplier
            fission2 = material["fission_group2_per_m"] * fission_multiplier
            nu_fission1 = material["nu_fission_group1_per_m"] * fission_multiplier
            nu_fission2 = material["nu_fission_group2_per_m"] * fission_multiplier
            downscatter = material["downscatter_group1_to2_per_m"] * downscatter_multiplier
            volume = float(definition["node_geometry"]["volume_base_m3"]) + channel * float(definition["node_geometry"]["volume_channel_increment_m3"]) + position * float(definition["node_geometry"]["volume_position_increment_m3"])
            require(abs1 >= fission1 and abs2 >= fission2, f"absorption/fission invariant failed at {index}")
            result.append(
                {
                    "flat_index": index,
                    "channel_id": channel,
                    "position": position,
                    "volume_m3": volume,
                    "burnup_j_per_kg_hm": burnup,
                    "base_absorption_group1_per_m": base_abs1,
                    "base_absorption_group2_per_m": base_abs2,
                    "overlay_absorption_group1_per_m": overlay1,
                    "overlay_absorption_group2_per_m": overlay2,
                    "absorption_group1_per_m": abs1,
                    "absorption_group2_per_m": abs2,
                    "downscatter_group1_to2_per_m": downscatter,
                    "fission_group1_per_m": fission1,
                    "fission_group2_per_m": fission2,
                    "nu_fission_group1_per_m": nu_fission1,
                    "nu_fission_group2_per_m": nu_fission2,
                    "chi_group1": material["chi_group1"],
                    "chi_group2": 1.0 - material["chi_group1"],
                    "energy_per_fission_j": material["energy_per_fission_j"],
                    "material_bracket": material_bracket(rows, burnup),
                }
            )
    return result


def build_matrices(
    geometry: dict[str, Any], coefficients: list[dict[str, Any]]
) -> tuple[np.ndarray, np.ndarray]:
    node_count = len(coefficients)
    group1 = np.zeros((node_count, node_count), dtype=np.float64)
    group2 = np.zeros((node_count, node_count), dtype=np.float64)
    downscatter = np.zeros(node_count, dtype=np.float64)
    for node in coefficients:
        index = int(node["flat_index"])
        volume = float(node["volume_m3"])
        group1[index, index] += (float(node["absorption_group1_per_m"]) + float(node["downscatter_group1_to2_per_m"]))
        group2[index, index] += float(node["absorption_group2_per_m"])
        downscatter[index] = float(node["downscatter_group1_to2_per_m"])
        require(volume > 0.0, f"node volume invalid at {index}")
    for edge in geometry["edges"]:
        a = int(edge["endpoint_a"]["flat_index"])
        b = int(edge["endpoint_b"]["flat_index"])
        volume_a = float(coefficients[a]["volume_m3"])
        volume_b = float(coefficients[b]["volume_m3"])
        conductance1 = float(edge["group1_m2"])
        conductance2 = float(edge["group2_m2"])
        group1[a, a] += conductance1 / volume_a
        group1[b, b] += conductance1 / volume_b
        group1[a, b] -= conductance1 / volume_a
        group1[b, a] -= conductance1 / volume_b
        group2[a, a] += conductance2 / volume_a
        group2[b, b] += conductance2 / volume_b
        group2[a, b] -= conductance2 / volume_a
        group2[b, a] -= conductance2 / volume_b
    for boundary in geometry["boundaries"]:
        index = int(boundary["node"]["flat_index"])
        volume = float(coefficients[index]["volume_m3"])
        group1[index, index] += float(boundary["group1_m2"]) / volume
        group2[index, index] += float(boundary["group2_m2"]) / volume

    matrix_a = np.zeros((2 * node_count, 2 * node_count), dtype=np.float64)
    matrix_f = np.zeros((2 * node_count, 2 * node_count), dtype=np.float64)
    matrix_a[:node_count, :node_count] = group1
    matrix_a[node_count:, :node_count] = -np.diag(downscatter)
    matrix_a[node_count:, node_count:] = group2
    chi1 = np.diag([float(node["chi_group1"]) for node in coefficients])
    chi2 = np.diag([float(node["chi_group2"]) for node in coefficients])
    nu_fission1 = np.diag([float(node["nu_fission_group1_per_m"]) for node in coefficients])
    nu_fission2 = np.diag([float(node["nu_fission_group2_per_m"]) for node in coefficients])
    matrix_f[:node_count, :node_count] = chi1 @ nu_fission1
    matrix_f[:node_count, node_count:] = chi1 @ nu_fission2
    matrix_f[node_count:, :node_count] = chi2 @ nu_fission1
    matrix_f[node_count:, node_count:] = chi2 @ nu_fission2
    require(np.all(np.isfinite(matrix_a)) and np.all(np.isfinite(matrix_f)), "reference matrices are non-finite")
    return matrix_a, matrix_f


def direct_reference(
    definition: dict[str, Any], geometry: dict[str, Any], coefficients: list[dict[str, Any]]
) -> dict[str, Any]:
    matrix_a, matrix_f = build_matrices(geometry, coefficients)
    try:
        transformed = np.linalg.solve(matrix_a, matrix_f)
        eigenvalues, eigenvectors = np.linalg.eig(transformed)
    except np.linalg.LinAlgError as exc:
        fail(f"dense generalized eigen solve failed: {exc}")
    viable: list[tuple[float, int]] = []
    for index, value in enumerate(eigenvalues):
        real = float(np.real(value))
        imaginary = float(abs(np.imag(value)))
        if math.isfinite(real) and real > 0.0 and imaginary <= 1e-10 * max(1.0, abs(real)):
            viable.append((real, index))
    require(viable, "reference generalized eigenproblem has no finite positive eigenvalue")
    ordered_viable = sorted(viable, key=lambda item: item[0], reverse=True)
    eigenvalue, selected = ordered_viable[0]
    eigenvalue_gap = (
        float(eigenvalue - ordered_viable[1][0])
        if len(ordered_viable) > 1
        else float("inf")
    )
    condition_number_a = float(np.linalg.cond(matrix_a, np.inf))
    require(math.isfinite(condition_number_a), "reference matrix condition number is non-finite")
    require(math.isfinite(eigenvalue_gap) and eigenvalue_gap > 0.0, "dominant eigenvalue is not separated")
    vector = np.real(eigenvectors[:, selected]).astype(np.float64)
    require(np.all(np.isfinite(vector)), "reference eigenvector is non-finite")
    if float(np.sum(vector)) < 0.0:
        vector = -vector
    require(float(np.min(vector)) >= 0.0, "reference eigenvector contains a negative component")
    node_count = len(coefficients)
    group1 = vector[:node_count]
    group2 = vector[node_count:]
    power = sum(
        float(node["volume_m3"])
        * float(node["energy_per_fission_j"])
        * (float(node["fission_group1_per_m"]) * group1[index] + float(node["fission_group2_per_m"]) * group2[index])
        for index, node in enumerate(coefficients)
    )
    require(math.isfinite(power) and power > 0.0, "reference eigenvector has invalid power")
    target_power = float(definition["initial_state"]["target_power_w"])
    normalization_scale = target_power / power
    group1 = group1 * normalization_scale
    group2 = group2 * normalization_scale
    require(
        bool(np.all(group1 >= 0.0) and np.all(group2 >= 0.0)),
        "normalized reference flux contains a negative component",
    )
    fission_source = np.array(
        [
            float(node["nu_fission_group1_per_m"]) * group1[index]
            + float(node["nu_fission_group2_per_m"]) * group2[index]
            for index, node in enumerate(coefficients)
        ],
        dtype=np.float64,
    )
    node_power = np.array(
        [
            float(node["volume_m3"])
            * float(node["energy_per_fission_j"])
            * (float(node["fission_group1_per_m"]) * group1[index] + float(node["fission_group2_per_m"]) * group2[index])
            for index, node in enumerate(coefficients)
        ],
        dtype=np.float64,
    )
    left1 = matrix_a[:node_count, :node_count] @ group1
    right1 = np.array(
        [float(node["chi_group1"]) * fission_source[index] / eigenvalue for index, node in enumerate(coefficients)],
        dtype=np.float64,
    )
    left2 = matrix_a[node_count:, node_count:] @ group2
    right2 = np.array(
        [
            float(node["downscatter_group1_to2_per_m"]) * group1[index]
            + float(node["chi_group2"]) * fission_source[index] / eigenvalue
            for index, node in enumerate(coefficients)
        ],
        dtype=np.float64,
    )
    residual_absolute = max(
        float(np.max(np.abs(left1 - right1))), float(np.max(np.abs(left2 - right2)))
    )
    residual_scale = max(
        float(np.max(np.abs(left1) + np.abs(right1))),
        float(np.max(np.abs(left2) + np.abs(right2))),
    )
    residual_relative = 0.0 if residual_scale == 0.0 else residual_absolute / residual_scale
    normalized_vector = np.concatenate((group1, group2))
    matrix_left = matrix_f @ normalized_vector
    matrix_right = eigenvalue * (matrix_a @ normalized_vector)
    matrix_scale = float(np.max(np.abs(matrix_left) + np.abs(matrix_right)))
    matrix_residual = 0.0 if matrix_scale == 0.0 else float(np.max(np.abs(matrix_left - matrix_right))) / matrix_scale
    total_power = float(np.sum(node_power))
    power_balance = abs(total_power - target_power) / target_power
    require(np.all(np.isfinite(group1)) and np.all(np.isfinite(group2)), "reference flux is non-finite")
    flux_canonical = [
        {
            "channel_id": index // int(definition["topology"]["bundle_position_count"]),
            "position": index % int(definition["topology"]["bundle_position_count"]),
            "group": group,
            "value": float(group1[index] if group == 1 else group2[index]),
        }
        for index in range(node_count)
        for group in (1, 2)
    ]
    return {
        "converged": True,
        "convergence_reason": "converged",
        "solver_status": "Converged",
        "solver_algorithm": "direct_dense_generalized_eigen",
        "outer_iterations": 1,
        "normalization_scale": float(normalization_scale),
        "eigenvalue": float(eigenvalue),
        "total_power_w": total_power,
        "group1_flux": [float(value) for value in group1],
        "group2_flux": [float(value) for value in group2],
        "flux_canonical": flux_canonical,
        "node_power_w": [float(value) for value in node_power],
        "fission_source_by_node": [float(value) for value in fission_source],
        "residual_absolute_infinity": float(residual_absolute),
        "residual_relative_infinity": float(residual_relative),
        "reference_matrix_residual_relative_infinity": float(matrix_residual),
        "reference_matrix_condition_number_inf": condition_number_a,
        "dominant_eigenvalue_gap": eigenvalue_gap,
        "source_shape_change_infinity": 0.0,
        "power_balance_relative": float(power_balance),
        "eigenvalue_change_absolute": 0.0,
        "eigenvalue_change_relative": 0.0,
        "reference_eigenvalue_imaginary_magnitude": float(abs(np.imag(eigenvalues[selected]))),
    }


def build_scenario(
    definition: dict[str, Any],
    pack: dict[str, Any],
    geometry: dict[str, Any],
    scenario: dict[str, Any],
) -> dict[str, Any]:
    coefficients = build_coefficients(definition, pack, scenario)
    expected = direct_reference(definition, geometry, coefficients)
    events = scenario["events"]
    refuel_events = [event for event in events if event.get("event_type") == "refuel_shift"]
    refuel_audits = [
        {
            "event_id": event["event_id"],
            "channel_id": event["channel_id"],
            "scheme_id": event["scheme_id"],
            "shift_direction": event["shift_direction"],
            "inserted_positions": event["inserted_positions"],
            "discharged_positions": event["discharged_positions"],
        }
        for event in refuel_events
    ]
    return {
        "scenario_id": scenario["scenario_id"],
        "description": scenario["description"],
        "simulation_time_s": scenario["simulation_time_s"],
        "event_history": events,
        "overlay": scenario["overlay"],
        "state": {
            "refuelling_audits": refuel_audits,
            "kinetics_xenon": {"status": "NotCovered"},
            "lifecycle_binding": "Explicit scenario event history; no runtime controller is inferred.",
        },
        "coefficients": coefficients,
        "independent_reproduction": {
            "status": "Converged",
            "converged": True,
            "convergence_reason": "converged",
            "solver": "p4-t06-g4k-python-numpy-dense-generalized-eigen-v1",
            "method": "F*x=k*A*x dense generalized eigen solve with target-power normalization",
            "expected_output_frozen_before_core_consumer": True,
        },
        "expected_independent": expected,
        "observable_records": [
            {"observable_id": "spatial.k", "unit": "1", "value": expected["eigenvalue"], "order_key": "k"},
            {"observable_id": "spatial.total_power", "unit": "W", "value": expected["total_power_w"], "order_key": "total_power"},
            {"observable_id": "spatial.residual_relative_inf", "unit": "1", "value": expected["residual_relative_infinity"], "order_key": "residual"},
            {
                "observable_id": "spatial.flux",
                "unit": "m^-2 s^-1",
                "component_order_spec": {
                    "order_kind": "SpatialNodeGroup",
                    "order_kind_ordinal": 0,
                    "component_key_schema_id": "NodeGroupKeyV1",
                    "comparator": "AscendingCanonical",
                    "comparator_ordinal": 0,
                    "tie_break_schema_id": "ChannelPositionGroupV1",
                },
                "values": expected["flux_canonical"],
                "order_key": "flux",
            },
            {"observable_id": "spatial.power", "unit": "W", "values": expected["node_power_w"], "order_key": "power"},
        ],
    }


def build_artifact(
    definition: dict[str, Any],
    pack: dict[str, Any],
    authority_definition: dict[str, Any],
    definition_hash: str,
    pack_hash: str,
    authority_definition_hash: str,
    source_snapshot_hash: str,
) -> dict[str, Any]:
    geometry = build_geometry(definition)
    scenarios = [
        build_scenario(definition, pack, geometry, scenario)
        for scenario in definition["scenarios"]
    ]
    require(len(scenarios) == 5, "independent artifact scenario count changed")
    tolerance = json.loads(json.dumps(authority_definition["tolerance"]))
    for profile in tolerance["profiles"].values():
        profile["data_digest"] = tolerance_profile_digest(profile)
    build = build_provenance()
    return {
        "format": "reactorsim.g4k-independent-reduced-authority/v1",
        "task_id": "P4-T06-G4K",
        "artifact_id": "p4-t06-g4k-independent-authority-v1",
        "case_id": "g4k_4x12_independent_reduced_sequence",
        "status": "candidate",
        "evidence_class": "candidate_numeric_evidence",
        "coverage_class": "RepresentativeReducedModel",
        "artifact_availability": "RepositoryCandidate",
        "evidence_approval": "Candidate",
        "validation_domain": "ReducedModel",
        "comparison_status": "Deferred",
        "tolerance_status": "Deferred",
        "golden_status": "NoGolden",
        "approval_scope": "Independent project-authored reduced-model reference candidate only; not direct CANDU, release, or production authority until G4 acts.",
        "input_authority": {
            "definition_relative_path": "data/comparisons/p4-t06-g4j-representative-definition-v1.json",
            "definition_sha256": definition_hash,
            "source_pack_relative_path": "data/packs/p4-t06-r4-reduced-candidate-v1.json",
            "source_pack_sha256": pack_hash,
            "independent_manifest_relative_path": "data/comparisons/p4-t06-g4k-independent-authority-definition-v1.json",
            "independent_manifest_sha256": authority_definition_hash,
            "expected_outputs_reused": False,
        },
        "source_authority": {
            "source_program": "P4-T06-G4K Independent Python Reference Producer",
            "source_version": "P4-T06-G4K-v1",
            "build_identity": f"python-{build['python_version']}|numpy-{build['numpy_version']}|{build['blas']['name']}-{build['blas']['version']}|offline-release",
            "source_commit": "UNAVAILABLE_NO_GIT_HEAD",
            "source_snapshot_sha256": source_snapshot_hash,
            "source_snapshot_scope": ["tools/IndependentReducedAuthority/Program.py"],
            "build_provenance": build,
            "coupling_tool_identity": "Python NumPy direct dense generalized-eigen solver; no ReactorSim.Core, G4J Program.cs, or G4J artifact coupling",
            "generator_identity": "p4-t06-g4k-python-numpy-dense-generalized-eigen-v1",
        },
        "nuclear_data": {
            "library": "NotApplicable",
            "external_data_used": False,
            "identity": "MAT-SYN",
            "source_pack": "p4-t06-r4-reduced-candidate-v1",
            "reason": "The project-authored synthetic reduced pack is reused by explicit hash; no external nuclear-data library is claimed.",
        },
        "geometry": geometry,
        "units": {
            "macroscopic_coefficients": "m^-1",
            "conductance": "m^2",
            "volume": "m^3",
            "flux": "m^-2 s^-1",
            "power": "W",
            "burnup": "J/kg_HM",
            "time": "s",
            "energy_per_fission": "J",
        },
        "normalization": {
            "target_power_w": definition["initial_state"]["target_power_w"],
            "initial_eigenvalue": definition["initial_state"]["initial_eigenvalue"],
            "flux_normalization": "Normalize the independent dominant eigenvector after solve to the frozen caller target power.",
            "sign_convention": "Positive fission source and power; absorption overlays add to absorption, with prescribed negative RRS weights reducing absorption where declared.",
        },
        "state": {
            "initial_state": definition["initial_state"],
            "kinetics_xenon": {"status": "NotCovered"},
            "history_binding": "Five explicit scenario histories and event order from the hash-bound input definition.",
        },
        "policies": definition["policies"],
        "reference_method": authority_definition["reference_method"],
        "observable_contract": authority_definition["observable_contract"],
        "authority_requirements": authority_definition["authority_requirements"],
        "coverage": {
            "scenario_count": 5,
            "required_scenarios": authority_definition["scenario_ids"],
            "full_core_380_channel": "NotCovered",
            "i_xe_kinetics": "NotCovered",
            "liquid_zone_adjuster_runtime": "NotCovered",
            "external_candu_numeric_authority": "Deferred",
            "reduced_model_reference": "Candidate independent authority; G4 approval deferred.",
        },
        "tolerance": tolerance,
        "rights": authority_definition["rights"],
        "scenarios": scenarios,
        "generator_checks": {
            "generator": "p4-t06-g4k-python-numpy-dense-generalized-eigen-v1",
            "source_snapshot_sha256": source_snapshot_hash,
            "independent_manifest_sha256": authority_definition_hash,
            "independent_repeat_equal": True,
            "expected_outputs_frozen_before_core_consumer": True,
        },
    }


def json_bytes(value: Any) -> bytes:
    return json.dumps(
        value,
        ensure_ascii=False,
        indent=2,
        allow_nan=False,
        separators=(",", ": "),
    ).encode("utf-8") + b"\n"


def validate_path_free(value: Any) -> None:
    encoded = json.dumps(value, ensure_ascii=False)
    require("file://" not in encoded.lower(), "file URI leaked into authority artifact")
    require(not any(f"{drive}:\\" in encoded for drive in "ABCDEFGHIJKLMNOPQRSTUVWXYZ"), "Windows path leaked into authority artifact")
    require("C:/" not in encoded and "c:/" not in encoded, "host path leaked into authority artifact")


def generate(args: argparse.Namespace) -> None:
    source_hash = args.source_snapshot_sha256.lower()
    require(len(source_hash) == 64 and all(character in "0123456789abcdef" for character in source_hash), "source snapshot hash must be SHA-256")
    definition, pack, authority_definition, definition_hash, pack_hash, authority_definition_hash = load_inputs(
        args.definition, args.pack, args.authority_definition
    )
    artifact = build_artifact(
        definition,
        pack,
        authority_definition,
        definition_hash,
        pack_hash,
        authority_definition_hash,
        source_hash,
    )
    first_bytes = json_bytes(artifact)
    second_artifact = build_artifact(
        definition,
        pack,
        authority_definition,
        definition_hash,
        pack_hash,
        authority_definition_hash,
        source_hash,
    )
    require(first_bytes == json_bytes(second_artifact), "independent repeated output is not byte-identical")
    validate_path_free(artifact)
    artifact_bytes = write_json(args.artifact, artifact)
    manifest = {
        "format": "reactorsim.g4k-independent-reduced-authority-manifest/v1",
        "task_id": "P4-T06-G4K",
        "artifact_id": "p4-t06-g4k-independent-authority-v1",
        "disposition": "candidate",
        "artifact_sha256": hashlib.sha256(artifact_bytes).hexdigest(),
        "definition_sha256": definition_hash,
        "source_pack_sha256": pack_hash,
        "independent_definition_sha256": authority_definition_hash,
        "source_snapshot_sha256": source_hash,
        "reference_solver": "p4-t06-g4k-python-numpy-dense-generalized-eigen-v1",
        "scenario_ids": [scenario["scenario_id"] for scenario in artifact["scenarios"]],
        "scenario_results": [
            {
                "scenario_id": scenario["scenario_id"],
                "eigenvalue": scenario["expected_independent"]["eigenvalue"],
                "total_power_w": scenario["expected_independent"]["total_power_w"],
                "reference_matrix_residual_relative_infinity": scenario["expected_independent"]["reference_matrix_residual_relative_infinity"],
                "reference_matrix_condition_number_inf": scenario["expected_independent"]["reference_matrix_condition_number_inf"],
                "dominant_eigenvalue_gap": scenario["expected_independent"]["dominant_eigenvalue_gap"],
            }
            for scenario in artifact["scenarios"]
        ],
    }
    validate_path_free(manifest)
    manifest_bytes = write_json(args.manifest, manifest)
    print(
        "P4_T06_G4K_GENERATE_PASS"
        f" disposition=candidate scenarios={len(artifact['scenarios'])}"
        f" nodes={artifact['geometry']['node_count']} edges={len(artifact['geometry']['edges'])}"
        f" boundaries={len(artifact['geometry']['boundaries'])}"
        f" artifact_sha256={hashlib.sha256(artifact_bytes).hexdigest()}"
        f" manifest_sha256={hashlib.sha256(manifest_bytes).hexdigest()}"
        " repeat_equal=True"
    )


def validate(args: argparse.Namespace) -> None:
    source_hash = args.source_snapshot_sha256.lower()
    definition, pack, authority_definition, definition_hash, pack_hash, authority_definition_hash = load_inputs(
        args.definition, args.pack, args.authority_definition
    )
    expected = build_artifact(
        definition,
        pack,
        authority_definition,
        definition_hash,
        pack_hash,
        authority_definition_hash,
        source_hash,
    )
    stored_bytes = args.artifact.read_bytes()
    require(stored_bytes == json_bytes(expected), "stored independent artifact differs from deterministic regeneration")
    manifest = read_json(args.manifest)
    artifact_hash = hashlib.sha256(stored_bytes).hexdigest()
    require(manifest.get("artifact_sha256") == artifact_hash, "independent manifest artifact hash mismatch")
    require(manifest.get("definition_sha256") == definition_hash, "independent manifest definition hash mismatch")
    require(manifest.get("source_pack_sha256") == pack_hash, "independent manifest source-pack hash mismatch")
    require(manifest.get("independent_definition_sha256") == authority_definition_hash, "independent manifest definition-manifest hash mismatch")
    require(manifest.get("disposition") == "candidate", "independent manifest disposition changed")
    stored = json.loads(stored_bytes.decode("utf-8"))
    require(stored.get("status") == "candidate", "independent artifact status changed")
    require(stored.get("evidence_approval") == "Candidate", "independent artifact approval changed")
    require(stored.get("comparison_status") == "Deferred", "independent comparison was promoted")
    require(stored.get("tolerance_status") == "Deferred", "independent tolerance was promoted")
    require(stored.get("golden_status") == "NoGolden", "independent golden status was promoted")
    validate_path_free(stored)
    print(
        "P4_T06_G4K_VALIDATE_PASS"
        f" disposition=candidate scenarios={len(stored['scenarios'])}"
        f" artifact_sha256={artifact_hash} independent_repeat_equal=True"
    )


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("command", choices=("generate", "validate"))
    parser.add_argument("definition", type=Path)
    parser.add_argument("pack", type=Path)
    parser.add_argument("authority_definition", type=Path)
    parser.add_argument("artifact", type=Path)
    parser.add_argument("manifest", type=Path)
    parser.add_argument("source_snapshot_sha256")
    return parser.parse_args()


def main() -> int:
    try:
        args = parse_args()
        if args.command == "generate":
            generate(args)
        else:
            validate(args)
        return 0
    except AuthorityFailure as exc:
        print(f"P4_T06_G4K_FAILURE {exc}", file=sys.stderr)
        return 1
    except Exception as exc:  # pragma: no cover - fail closed at tool boundary
        print(f"P4_T06_G4K_FAILURE unexpected={type(exc).__name__}: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
