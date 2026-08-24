"""Independent P2-T05 tolerance-profile digest audit for the G4K artifact."""

from __future__ import annotations

import hashlib
import json
import struct
import sys
from pathlib import Path
from typing import Any


def string_bytes(value: str) -> bytes:
    encoded = value.encode("utf-8")
    return struct.pack("<I", len(encoded)) + encoded


def typed_uint8(value: int) -> bytes:
    return b"\x01" + bytes((value,))


def typed_utf8(value: str) -> bytes:
    encoded = value.encode("utf-8")
    return b"\x0A" + struct.pack("<I", len(encoded)) + encoded


def optional_float(value: Any) -> bytes:
    if value is None:
        return b"\x10"
    return b"\x07" + struct.pack("<d", float(value))


def profile_bytes(profile: dict[str, Any]) -> bytes:
    value_kind = {"Scalar": 0, "Vector": 1, "Bytes": 3}
    norm = {"Scalar": 0, "L_inf": 1, "Invariant": 4}
    acceptance = {"Exact": 0, "Or": 3}
    invariant = {"NotApplicable": 0, "Equality": 1}
    applicability = {"Always": 0}
    threshold = {"NotApplicable": 0, "Deferred": 1, "Specified": 2}
    approval = {"Deferred": 0, "Provisional": 1, "Approved": 2}
    owner_gate = {"G2": 0, "G3": 1, "G4": 2, "G5": 3, "G6": 4, "G7A": 5, "G7B": 6}

    result = bytearray(b"CANDU-TOLERANCE-V1")
    result.extend(struct.pack("<I", 1))
    result.extend(string_bytes(profile["profile_id"]))
    result.extend(string_bytes(profile["quantity_id"]))
    result.extend(string_bytes(profile["unit"]))
    result.append(value_kind[profile["value_kind"]])
    result.extend(string_bytes(profile["payload_schema_id"]))
    component_order = profile["component_order_spec"]
    if component_order == "NotApplicable":
        result.extend(b"\x10")
    else:
        result.extend(b"\x0D")
        result.extend(struct.pack("<I", 4))
        result.extend(typed_uint8(component_order["order_kind_ordinal"]))
        result.extend(typed_utf8(component_order["component_key_schema_id"]))
        result.extend(typed_uint8(component_order["comparator_ordinal"]))
        result.extend(typed_utf8(component_order["tie_break_schema_id"]))
    result.append(norm[profile["norm"]])
    result.append(acceptance[profile["acceptance"]])
    result.append(invariant[profile["invariant_kind"]])
    result.append(applicability[profile["applicability"]])
    result.extend(string_bytes(profile["comparison_rule_id"]))
    result.extend(optional_float(profile.get("absolute_tolerance")))
    result.extend(optional_float(profile.get("relative_tolerance")))
    result.extend(optional_float(profile.get("reference_scale")))
    result.append(threshold[profile["threshold_state"]])
    result.append(approval[profile["approval_status"]])
    result.append(owner_gate[profile["owner_gate"]])
    return bytes(result)


def main() -> int:
    if len(sys.argv) != 2:
        raise SystemExit("usage: ProfileDigestAudit.py ARTIFACT")
    artifact = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
    profiles = artifact["tolerance"]["profiles"]
    for profile_id, profile in profiles.items():
        actual = hashlib.sha256(profile_bytes(profile)).hexdigest()
        if actual != profile.get("data_digest"):
            raise SystemExit(f"P4_T06_G4K_PROFILE_DIGEST_FAILURE profile={profile_id}")
    print(f"P4_T06_G4K_PROFILE_DIGEST_PASS profiles={len(profiles)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
