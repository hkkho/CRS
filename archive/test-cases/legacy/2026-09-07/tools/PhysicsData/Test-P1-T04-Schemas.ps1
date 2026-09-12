[CmdletBinding()]
param(
    [string]$PythonCommand = 'python'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path

$pythonSource = @'
import copy
import hashlib
import importlib.metadata
import json
import math
import sys
from datetime import datetime
from decimal import Decimal, InvalidOperation
from pathlib import Path

from jsonschema import Draft202012Validator, FormatChecker

root = Path(sys.argv[1])
schema_root = root / "data" / "schema"
examples_root = schema_root / "examples"

raw_schema_path = schema_root / "reference-raw-run-v1.schema.json"
compact_schema_path = schema_root / "reference-compact-export-v1.schema.json"


def reject_constant(value):
    raise ValueError(f"non-standard numeric token {value!r}")


def finite_float(value):
    parsed = float(value)
    if not math.isfinite(parsed):
        raise ValueError(f"non-finite JSON number {value!r}")
    return parsed


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"duplicate object name {key!r}")
        result[key] = value
    return result


def read_json(path):
    data = path.read_bytes()
    if data.startswith(b"\xef\xbb\xbf"):
        raise AssertionError(f"{path.name}: UTF-8 BOM is forbidden")
    if b"\r" in data:
        raise AssertionError(f"{path.name}: CR/CRLF is forbidden; use LF")
    if not data.endswith(b"\n") or data.endswith(b"\n\n"):
        raise AssertionError(f"{path.name}: expected exactly one terminal LF")
    try:
        text = data.decode("utf-8", errors="strict")
    except UnicodeDecodeError as error:
        raise AssertionError(f"{path.name}: not strict UTF-8: {error}") from error
    return json.loads(
        text,
        parse_constant=reject_constant,
        parse_float=finite_float,
        object_pairs_hook=unique_object,
    ), data


raw_schema, _ = read_json(raw_schema_path)
compact_schema, _ = read_json(compact_schema_path)
Draft202012Validator.check_schema(raw_schema)
Draft202012Validator.check_schema(compact_schema)
if raw_schema["$id"] == compact_schema["$id"]:
    raise AssertionError("Schema $id values must be distinct")


def require_closed_objects(value, path=()):
    if isinstance(value, dict):
        if value.get("type") == "object" and value.get("additionalProperties") is not False:
            raise AssertionError(f"object schema is not closed at {'/'.join(path) or '<root>'}")
        for key, child in value.items():
            require_closed_objects(child, path + (str(key),))
    elif isinstance(value, list):
        for index, child in enumerate(value):
            require_closed_objects(child, path + (str(index),))


require_closed_objects(raw_schema)
require_closed_objects(compact_schema)

format_checker = FormatChecker()
validators = {
    "raw": Draft202012Validator(raw_schema, format_checker=format_checker),
    "compact": Draft202012Validator(compact_schema, format_checker=format_checker),
}

valid_cases = [
    ("raw", "P1-T04-raw-dragon5-valid.json"),
    ("raw", "P1-T04-raw-donjon5-valid.json"),
    ("compact", "P1-T04-compact-dragon5-valid.json"),
    ("compact", "P1-T04-compact-donjon5-valid.json"),
]

loaded_valid = {}
for kind, name in valid_cases:
    document, data = read_json(examples_root / name)
    errors = list(validators[kind].iter_errors(document))
    if errors:
        first = errors[0]
        path = "/".join(str(part) for part in first.absolute_path) or "<root>"
        raise AssertionError(f"{name}: unexpected validation error at {path}: {first.message}")
    loaded_valid[name] = (document, data)


def all_errors(error):
    yield error
    for child in error.context:
        yield from all_errors(child)


invalid_cases = [
    ("raw", "P1-T04-raw-invalid-embedded-payload.json", ("artifacts", 0), "additionalProperties"),
    ("raw", "P1-T04-raw-invalid-uppercase-sha256.json", ("artifacts", 0, "sha256"), "pattern"),
    ("raw", "P1-T04-raw-invalid-missing-byte-length.json", ("artifacts", 0), "required"),
    ("raw", "P1-T04-raw-invalid-local-path.json", ("execution", "runner_path"), "pattern"),
    ("raw", "P1-T04-raw-invalid-unc-path.json", ("execution", "runner_path"), "pattern"),
    ("raw", "P1-T04-raw-invalid-unknown-format.json", ("format",), "const"),
    ("compact", "P1-T04-compact-invalid-missing-unit.json", ("observations", 0), "required"),
    ("compact", "P1-T04-compact-invalid-spatial-field.json", (), "additionalProperties"),
    ("compact", "P1-T04-compact-invalid-wrong-branch.json", (), "oneOf"),
    ("compact", "P1-T04-compact-invalid-extra-observation.json", ("observations",), "maxItems"),
    ("compact", "P1-T04-compact-invalid-tolerance.json", ("observations", 0), "additionalProperties"),
]

for kind, name, expected_path, expected_validator in invalid_cases:
    document, _ = read_json(examples_root / name)
    roots = list(validators[kind].iter_errors(document))
    flattened = [item for root_error in roots for item in all_errors(root_error)]
    if not flattened:
        raise AssertionError(f"{name}: invalid fixture unexpectedly validated")
    matched = any(
        tuple(error.absolute_path) == expected_path and error.validator == expected_validator
        for error in flattened
    )
    if not matched:
        observed = sorted({
            ("/".join(str(part) for part in error.absolute_path) or "<root>", str(error.validator))
            for error in flattened
        })
        raise AssertionError(
            f"{name}: expected {expected_validator} at {expected_path}, observed {observed}"
        )

for lexical_name in (
    "P1-T04-lexical-invalid-duplicate-key.json",
    "P1-T04-lexical-invalid-nan.json",
):
    try:
        read_json(examples_root / lexical_name)
    except ValueError:
        pass
    else:
        raise AssertionError(f"{lexical_name}: strict JSON loader unexpectedly accepted it")


def require_scalar_agreement(value, label="root"):
    if isinstance(value, dict):
        if "value" in value and "source_lexeme" in value:
            try:
                numeric = Decimal(str(value["value"]))
                lexeme = Decimal(value["source_lexeme"])
            except (InvalidOperation, ValueError) as error:
                raise AssertionError(f"{label}: invalid source numeric evidence: {error}") from error
            if not numeric.is_finite() or not lexeme.is_finite() or numeric != lexeme:
                raise AssertionError(
                    f"{label}: value {value['value']!r} disagrees with source_lexeme {value['source_lexeme']!r}"
                )
        for key, child in value.items():
            require_scalar_agreement(child, f"{label}/{key}")
    elif isinstance(value, list):
        for index, child in enumerate(value):
            require_scalar_agreement(child, f"{label}/{index}")


for name, (document, _) in loaded_valid.items():
    require_scalar_agreement(document, name)

mismatch_document, _ = read_json(examples_root / "P1-T04-compact-invalid-lexeme-mismatch.json")
if list(validators["compact"].iter_errors(mismatch_document)):
    raise AssertionError("lexeme mismatch fixture must isolate the semantic equality check")
try:
    require_scalar_agreement(mismatch_document, "P1-T04-compact-invalid-lexeme-mismatch.json")
except AssertionError:
    pass
else:
    raise AssertionError("P1-T04-compact-invalid-lexeme-mismatch.json unexpectedly passed")


def require_keys(value, expected, label):
    actual = list(value.keys())
    if actual != expected:
        raise AssertionError(f"{label}: key order {actual} != {expected}")


top_keys = ["format", "approval_status", "retention", "publication", "case", "tool_provenance", "observations", "diagnostics"]
case_keys = ["case_id", "descriptor_sha256"]
tool_keys = ["program", "version", "source_commit", "source_tree", "executable_sha256", "container_image_digest"]
observation_keys = ["source_ordinal", "quantity_id", "value", "source_lexeme", "unit_id"]
diagnostics_keys = ["normal_end", "warning_codes", "assertions", "convergence_status", "convergence"]
warning_keys = ["code", "count"]
assertion_keys = ["assertion_id", "passed", "count"]
measure_keys = ["quantity_id", "value", "source_lexeme"]

for name in ("P1-T04-compact-dragon5-valid.json", "P1-T04-compact-donjon5-valid.json"):
    document, data = loaded_valid[name]
    require_keys(document, top_keys, f"{name}: root")
    require_keys(document["case"], case_keys, f"{name}: case")
    require_keys(document["tool_provenance"], tool_keys, f"{name}: tool_provenance")
    for index, observation in enumerate(document["observations"]):
        require_keys(observation, observation_keys, f"{name}: observations/{index}")
    require_keys(document["diagnostics"], diagnostics_keys, f"{name}: diagnostics")
    for index, warning in enumerate(document["diagnostics"]["warning_codes"]):
        require_keys(warning, warning_keys, f"{name}: diagnostics/warning_codes/{index}")
    for index, assertion in enumerate(document["diagnostics"]["assertions"]):
        require_keys(assertion, assertion_keys, f"{name}: diagnostics/assertions/{index}")
    for index, measure in enumerate(document["diagnostics"]["convergence"]):
        require_keys(measure, measure_keys, f"{name}: diagnostics/convergence/{index}")
    ordinals = [item["source_ordinal"] for item in document["observations"]]
    if ordinals != list(range(1, len(ordinals) + 1)):
        raise AssertionError(f"{name}: non-canonical source ordinals {ordinals}")
    quantity_ids = [item["quantity_id"] for item in document["observations"]]
    if len(quantity_ids) != len(set(quantity_ids)):
        raise AssertionError(f"{name}: duplicate observation quantity_id")
    forbidden = {
        "raw_manifest_sha256", "raw_listing_sha256", "raw_listing_path", "run_uuid",
        "started_utc", "finished_utc", "host", "timing", "memory", "geometry", "tolerance"
    }

    def reject_forbidden(value, path=()):
        if isinstance(value, dict):
            for key, child in value.items():
                if key in forbidden:
                    raise AssertionError(f"{name}: forbidden compact field {'/'.join(path + (key,))}")
                reject_forbidden(child, path + (key,))
        elif isinstance(value, list):
            for index, child in enumerate(value):
                reject_forbidden(child, path + (str(index),))

    reject_forbidden(document)
    first_render = (
        json.dumps(
            document,
            ensure_ascii=False,
            allow_nan=False,
            indent=2,
            separators=(",", ": "),
        ) + "\n"
    ).encode("utf-8")
    second_document = json.loads(
        first_render.decode("utf-8"),
        parse_constant=reject_constant,
        parse_float=finite_float,
        object_pairs_hook=unique_object,
    )
    second_render = (
        json.dumps(
            second_document,
            ensure_ascii=False,
            allow_nan=False,
            indent=2,
            separators=(",", ": "),
        ) + "\n"
    ).encode("utf-8")
    if first_render != second_render or first_render != data:
        raise AssertionError(f"{name}: committed bytes do not match the deterministic writer profile")

def require_time_order(document, label):
    started = datetime.fromisoformat(document["execution"]["started_utc"].replace("Z", "+00:00"))
    finished = datetime.fromisoformat(document["execution"]["finished_utc"].replace("Z", "+00:00"))
    if finished < started:
        raise AssertionError(f"{label}: finished_utc precedes started_utc")


for name in ("P1-T04-raw-dragon5-valid.json", "P1-T04-raw-donjon5-valid.json"):
    document, _ = loaded_valid[name]
    artifact_ids = [item["artifact_id"] for item in document["artifacts"]]
    if len(artifact_ids) != len(set(artifact_ids)):
        raise AssertionError(f"{name}: duplicate artifact_id")
    for artifact in document["artifacts"]:
        if artifact["retention"] != "external-private" or artifact["publication"] != "not-approved":
            raise AssertionError(f"{name}: artifact policy is not external-private/not-approved")
    require_time_order(document, name)

raw_dragon = loaded_valid["P1-T04-raw-dragon5-valid.json"][0]
failed_without_error = copy.deepcopy(raw_dragon)
failed_without_error["outcome"]["status"] = "failed"
failed_without_error["outcome"]["normal_end"] = False
if not list(validators["raw"].iter_errors(failed_without_error)):
    raise AssertionError("raw schema accepted a failed outcome without an error code")
succeeded_with_error = copy.deepcopy(raw_dragon)
succeeded_with_error["outcome"]["error_codes"] = [{"code": "example.error", "count": 1}]
if not list(validators["raw"].iter_errors(succeeded_with_error)):
    raise AssertionError("raw schema accepted a successful outcome with an error code")
wrong_program = copy.deepcopy(raw_dragon)
wrong_program["tool"]["program"] = "donjon5"
if not list(validators["raw"].iter_errors(wrong_program)):
    raise AssertionError("raw schema accepted a Dragon case with the DONJON program")
missing_listing = copy.deepcopy(raw_dragon)
missing_listing["artifacts"][0]["role"] = "compact-export"
if not list(validators["raw"].iter_errors(missing_listing)):
    raise AssertionError("raw schema accepted a manifest without a listing artifact")
missing_stderr = copy.deepcopy(raw_dragon)
missing_stderr["artifacts"] = [item for item in missing_stderr["artifacts"] if item["role"] != "stderr"]
if not list(validators["raw"].iter_errors(missing_stderr)):
    raise AssertionError("raw schema accepted a manifest without a stderr artifact")
succeeded_with_failed_assertion = copy.deepcopy(raw_dragon)
succeeded_with_failed_assertion["outcome"]["assertions"][0]["passed"] = False
if not list(validators["raw"].iter_errors(succeeded_with_failed_assertion)):
    raise AssertionError("raw schema accepted success with a failed assertion")
succeeded_with_failed_convergence = copy.deepcopy(raw_dragon)
succeeded_with_failed_convergence["outcome"]["convergence"]["status"] = "failed"
if not list(validators["raw"].iter_errors(succeeded_with_failed_convergence)):
    raise AssertionError("raw schema accepted success with failed convergence")
failed_after_normal_end = copy.deepcopy(raw_dragon)
failed_after_normal_end["outcome"]["status"] = "failed"
failed_after_normal_end["outcome"]["error_codes"] = [
    {"code": "manifest.observable_mismatch", "count": 1}
]
if list(validators["raw"].iter_errors(failed_after_normal_end)):
    raise AssertionError("raw schema rejected a normal-end run that failed manifest validation")
reversed_time = copy.deepcopy(raw_dragon)
reversed_time["execution"]["finished_utc"] = "2026-08-07T11:59:00Z"
try:
    require_time_order(reversed_time, "reversed-time mutation")
except AssertionError:
    pass
else:
    raise AssertionError("raw semantic check accepted finish before start")

descriptor_checks = {
    "P1-T04-raw-dragon5-valid.json": root / "reference" / "dragon5" / "P1-T02-lumpSS.case.json",
    "P1-T04-raw-donjon5-valid.json": root / "reference" / "donjon5" / "P1-T03-AFA_180_310_type1_dual.case.json",
    "P1-T04-compact-dragon5-valid.json": root / "reference" / "dragon5" / "P1-T02-lumpSS.case.json",
    "P1-T04-compact-donjon5-valid.json": root / "reference" / "donjon5" / "P1-T03-AFA_180_310_type1_dual.case.json",
}
runner_checks = {
    "P1-T04-raw-dragon5-valid.json": root / "tools" / "PhysicsData" / "Run-P1-T02-DragonLumpSS.ps1",
    "P1-T04-raw-donjon5-valid.json": root / "tools" / "PhysicsData" / "Run-P1-T03-DonjonAfa180310.ps1",
}
for name, path in descriptor_checks.items():
    expected = loaded_valid[name][0]["case"]["descriptor_sha256"]
    actual = hashlib.sha256(path.read_bytes()).hexdigest()
    if actual != expected:
        raise AssertionError(f"{name}: descriptor SHA-256 {expected} != {actual}")
    descriptor, _ = read_json(path)
    document = loaded_valid[name][0]
    tool = document.get("tool", document.get("tool_provenance"))
    expected_program = "dragon5" if "dragon5" in name else "donjon5"
    expected_tool = {
        "program": expected_program,
        "version": "5.1.0",
        "source_commit": descriptor["version5_source"]["commit"],
        "source_tree": descriptor["version5_source"]["tree"],
        "executable_sha256": descriptor["execution"]["program_sha256"],
        "container_image_digest": descriptor["execution"]["container_image"].split("@", 1)[1],
    }
    if tool != expected_tool:
        raise AssertionError(f"{name}: tool provenance disagrees with its case descriptor")
for name, path in runner_checks.items():
    expected = loaded_valid[name][0]["execution"]["runner_sha256"]
    actual = hashlib.sha256(path.read_bytes()).hexdigest()
    if actual != expected:
        raise AssertionError(f"{name}: runner SHA-256 {expected} != {actual}")

donjon = loaded_valid["P1-T04-compact-donjon5-valid.json"][0]
measures = {item["quantity_id"]: Decimal(str(item["value"])) for item in donjon["diagnostics"]["convergence"]}
if measures["donjon5.flddir.delt"] > measures["donjon5.flddir.epsout"]:
    raise AssertionError("DONJON compact evidence contradicts DELT <= EPSOUT")

print(f"Python {sys.version.split()[0]}; jsonschema {importlib.metadata.version('jsonschema')}")
print("P1-T04 schema validation passed: 2 schemas, 4 valid fixtures, 11 schema-invalid, 2 lexical-invalid, and 1 semantic-invalid fixture.")
print("Compact byte policy passed: strict JSON, exact double render, fixed nested key/array order, and unique identifiers.")
'@

$pythonSource | & $PythonCommand - $repositoryRoot
if ($LASTEXITCODE -ne 0) {
    throw "P1-T04 schema validation failed with exit code $LASTEXITCODE."
}
