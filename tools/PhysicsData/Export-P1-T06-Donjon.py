#!/usr/bin/env python3
"""Fail-closed exporter for the single P1-T03 DONJON5 smoke listing."""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
import re
import sys
from decimal import Decimal, InvalidOperation
from pathlib import Path
from typing import Any


class ParseFailure(ValueError):
    pass


ROOT = Path(__file__).resolve().parents[2]
DESCRIPTOR = ROOT / "reference" / "donjon5" / "P1-T03-AFA_180_310_type1_dual.case.json"
DESCRIPTOR_SHA256 = "09851b1ee3155592eaa379454f2baa615af3749f82507bbb4ad526f0282a881b"
CASE_ID = "donjon5-afa-180-310-type1-dual-v5.1.0"
WARNING_TEXT = "SPHAPX: WARNING -- Record MEDIA_VOLUME is missing in the Apex file. Volume set to 1.0"
NUMBER = r"-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[Ee][+-]?[0-9]+)?"
NUMBER_FULL = re.compile(rf"^{NUMBER}$")
K_LINE = re.compile(rf"^\s*FLDDIR:\s+EFFECTIVE MULTIPLICATION FACTOR\s*=\s*(?P<value>{NUMBER})\s*$")
MAXOUT_LINE = re.compile(r"^\s*MAXOUT\s+(?P<value>[1-9][0-9]*)\s+\(MAXIMUM NUMBER OF OUTER ITERATIONS\)\s*$")
EPSOUT_LINE = re.compile(rf"^\s*EPSOUT\s+(?P<value>{NUMBER})\s+\(OUTER ITERATION (?P<kind>KEFF|FLUX) EPSILON\)\s*$")
ASSERTION_LINE = re.compile(rf"^\s*>\|TEST SUCCESSFUL; DELTA=\s*(?P<delta>{NUMBER})\s*\|>[0-9]+\s*$")
COMPLETION_LINE = re.compile(r"^\s*>\|AFA_180_310_type1_dual completed\s*\|>[0-9]+\s*$")
RUNTIME_COMPLETION_TOKEN = re.compile(r"^\s*>\|.*AFA_180_310_type1_dual completed", re.IGNORECASE)
NORMAL_END_LINE = re.compile(r"^\s*normal end of execution for donjon 5\s+Version 5\.1\.0\s*$", re.IGNORECASE)
NORMAL_END_TOKEN = re.compile(r"\bnormal end of execution for donjon\b", re.IGNORECASE)
NONFINITE = re.compile(r"(?<![A-Za-z0-9_-])[+-]?(?:nan|inf|infinity)(?![A-Za-z0-9_-])", re.IGNORECASE)
FAILURES = (
    re.compile(r"\b(?:x?abort|test\s+fail(?:ure|ed)|kernel\s+error|fatal\s+error|input\s+error)\b", re.IGNORECASE),
    re.compile(r"\b(?:non[- ]?converg(?:ence|ed|ing)?|not\s+converg(?:ed|ing)?)\b", re.IGNORECASE),
)
FATAL_MARKERS = (
    "FLDDIR: ***WARNING*** THE MAXIMUM NUMBER OF OUTER ITERATIONS IS REACHED.",
    "FLDDIR: CONVERGENCE FAILURE.",
)


def fail(message: str) -> None:
    raise ParseFailure(message)


def reject_constant(value: str) -> None:
    raise ValueError(f"non-standard JSON number {value!r}")


def unique_object(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"duplicate JSON name {key!r}")
        result[key] = value
    return result


def read_utf8(path: Path, label: str) -> bytes:
    if not path.is_file():
        fail(f"{label} is not a regular file: {path}")
    try:
        data = path.read_bytes()
    except OSError as error:
        fail(f"could not read {label}: {error}")
    if data.startswith(b"\xef\xbb\xbf"):
        fail(f"{label} has a forbidden UTF-8 BOM")
    try:
        data.decode("utf-8", errors="strict")
    except UnicodeDecodeError as error:
        fail(f"{label} is not strict UTF-8: {error}")
    return data


def require(document: dict[str, Any], path: tuple[str, ...], expected: Any) -> None:
    current: Any = document
    for key in path:
        if not isinstance(current, dict) or key not in current:
            fail(f"descriptor is missing {'/'.join(path)}")
        current = current[key]
    if current != expected:
        fail(f"descriptor has unsupported {'/'.join(path)}: {current!r}")


def load_descriptor(path: Path) -> tuple[dict[str, Any], str]:
    try:
        resolved = path.resolve(strict=True)
        expected = DESCRIPTOR.resolve(strict=True)
    except OSError as error:
        fail(f"could not resolve descriptor: {error}")
    if resolved != expected:
        fail("only the repository P1-T03 DONJON descriptor is supported")
    data = read_utf8(resolved, "case descriptor")
    if b"\r" in data or not data.endswith(b"\n") or data.endswith(b"\n\n"):
        fail("case descriptor violates the canonical LF contract")
    digest = hashlib.sha256(data).hexdigest()
    if digest != DESCRIPTOR_SHA256:
        fail("P1-T03 descriptor SHA-256 does not match the fixed parser input")
    try:
        document = json.loads(data.decode("utf-8"), parse_constant=reject_constant,
                              parse_float=Decimal, object_pairs_hook=unique_object)
    except (json.JSONDecodeError, ValueError) as error:
        fail(f"invalid case descriptor JSON: {error}")
    if not isinstance(document, dict):
        fail("case descriptor root must be an object")
    fixed = {
        ("format",): "candu.reference-donjon5-smoke-case/v1",
        ("task_id",): "P1-T03",
        ("case_id",): CASE_ID,
        ("version5_source", "ref"): "v5.1.0",
        ("version5_source", "commit"): "eee582f8594d3b7d01b65660ca1e8bef00eb6b2a",
        ("version5_source", "tree"): "0d3e8f721c84b3c3b2d03e9898a8a0a9c29287d8",
        ("execution", "program_sha256"): "e125c8bc095d9a1a7e088847b1c21b1bc37043681b6104713fa29ee1507f1a76",
        ("execution", "expected_effective_multiplication_factor"): "9.9859273434E-01",
        ("execution", "convergence", "maxout"): 200,
        ("execution", "convergence", "epsout"): "1.00E-04",
        ("execution", "convergence", "expected_final_outer_iteration"): 15,
        ("execution", "convergence", "expected_final_dels"): "0.55E-07",
        ("execution", "convergence", "expected_final_delt"): "0.82E-04",
        ("execution", "runtime_warning_policy", "allowed_exact_text"): WARNING_TEXT,
        ("execution", "runtime_warning_policy", "required_count"): 2,
    }
    for key_path, value in fixed.items():
        require(document, key_path, value)
    return document, digest


def number(lexeme: str, label: str, exportable: bool = False) -> float:
    if NUMBER_FULL.fullmatch(lexeme) is None:
        fail(f"{label} has malformed numeric lexeme {lexeme!r}")
    try:
        decimal = Decimal(lexeme)
    except InvalidOperation as error:
        fail(f"{label} is invalid: {error}")
    if not decimal.is_finite():
        fail(f"{label} is non-finite")
    value = float(decimal)
    if not math.isfinite(value):
        fail(f"{label} cannot be represented as finite JSON")
    if exportable and Decimal(str(value)) != decimal:
        fail(f"{label} cannot preserve decimal agreement")
    return value


def parse_final_row(line: str, line_number: int) -> tuple[str, str, str] | None:
    tokens = line.split()
    if len(tokens) != 15 or tokens[0] != "15":
        return None
    if any(NUMBER_FULL.fullmatch(token) is None for token in tokens):
        fail(f"listing line {line_number} has malformed final FLDDIR row")
    for index, token in enumerate(tokens[1:], start=1):
        number(token, f"listing line {line_number} column {index}")
    if tokens[-1] != "0":
        fail(f"listing line {line_number} final FLDDIR status is not zero")
    return tokens[0], tokens[-3], tokens[-2]


def one(items: list[Any], label: str) -> Any:
    if len(items) != 1:
        fail(f"listing must contain exactly one {label}; found {len(items)}")
    return items[0]


def parse_listing(path: Path) -> dict[str, Any]:
    try:
        path = path.resolve(strict=True)
    except OSError as error:
        fail(f"could not resolve listing: {error}")
    data = read_utf8(path, "listing")
    if b"\x00" in data or b"\r" in data:
        fail("listing contains NUL or non-LF line endings")
    lines = data.decode("utf-8").splitlines()
    warnings: list[int] = []
    rows: list[tuple[int, str, str, str]] = []
    factors: list[tuple[int, str]] = []
    maxouts: list[tuple[int, int]] = []
    epsouts: list[tuple[int, str, str]] = []
    assertions: list[tuple[int, str]] = []
    completions: list[int] = []
    normal_ends: list[int] = []
    for line_number, line in enumerate(lines, 1):
        if NONFINITE.search(line) or any(pattern.search(line) for pattern in FAILURES):
            fail(f"listing line {line_number} contains a fatal diagnostic")
        if any(marker in line for marker in FATAL_MARKERS):
            fail(f"listing line {line_number} contains a fatal convergence marker")
        if "WARNING" in line.upper():
            if line.strip().lower() == "check for warning in listing":
                pass
            elif line.strip() != WARNING_TEXT:
                fail(f"listing line {line_number} contains an unapproved warning")
            else:
                warnings.append(line_number)
        row = parse_final_row(line, line_number)
        if row is not None:
            rows.append((line_number, *row))
        if "EFFECTIVE MULTIPLICATION FACTOR" in line.upper():
            match = K_LINE.fullmatch(line)
            if match is None:
                fail(f"listing line {line_number} has ambiguous multiplication-factor syntax")
            lexeme = match.group("value")
            number(lexeme, "K effective", exportable=True)
            factors.append((line_number, lexeme))
        if re.search(r"\bMAXOUT\b", line, re.IGNORECASE):
            match = MAXOUT_LINE.fullmatch(line)
            if match is None:
                fail(f"listing line {line_number} has ambiguous MAXOUT syntax")
            maxouts.append((line_number, int(match.group("value"))))
        if re.search(r"\bEPSOUT\b", line, re.IGNORECASE):
            match = EPSOUT_LINE.fullmatch(line)
            if match is None:
                fail(f"listing line {line_number} has ambiguous EPSOUT syntax")
            lexeme = match.group("value")
            number(lexeme, "EPSOUT", exportable=True)
            epsouts.append((line_number, match.group("kind"), lexeme))
        if "TEST SUCCESSFUL" in line.upper():
            match = ASSERTION_LINE.fullmatch(line)
            if match is None:
                fail(f"listing line {line_number} has ambiguous assertion syntax")
            delta = match.group("delta")
            number(delta, "assertion DELTA")
            assertions.append((line_number, delta))
        if RUNTIME_COMPLETION_TOKEN.search(line):
            if COMPLETION_LINE.fullmatch(line) is None:
                fail(f"listing line {line_number} has ambiguous runtime completion syntax")
            completions.append(line_number)
        if NORMAL_END_TOKEN.search(line):
            if NORMAL_END_LINE.fullmatch(line) is None:
                fail(f"listing line {line_number} has ambiguous normal-end syntax")
            normal_ends.append(line_number)
    if len(warnings) != 2:
        fail(f"listing must contain exactly two allowlisted warnings; found {len(warnings)}")
    row_line, iteration, dels, delt = one(rows, "final FLDDIR iteration row")
    factor_line, factor = one(factors, "FLDDIR multiplication factor")
    maxout_line, maxout = one(maxouts, "MAXOUT record")
    if len(epsouts) != 2 or [item[1] for item in epsouts] != ["KEFF", "FLUX"]:
        fail("listing must contain exactly the ordered KEFF and FLUX EPSOUT records")
    assertion_line, delta = one(assertions, "runtime successful assertion")
    completion_line = one(completions, "runtime completion record")
    normal_line = one(normal_ends, "normal-end record")
    expected = ("15", "0.55E-07", "0.82E-04", "9.9859273434E-01", 200, "1.00E-04", "1.00E-04")
    observed = (iteration, dels, delt, factor, maxout, epsouts[0][2], epsouts[1][2])
    if observed != expected:
        fail(f"listing observables do not exactly match P1-T03: {observed!r}")
    if number(delt, "DELT") > number(epsouts[1][2], "EPSOUT"):
        fail("listing DELT exceeds emitted EPSOUT")
    if not (warnings[-1] < row_line < factor_line < maxout_line < epsouts[0][0] < epsouts[1][0]
            < assertion_line < completion_line < normal_line):
        fail("listing required records are not in the fixed P1-T03 runtime order")
    return {"iteration": iteration, "dels": dels, "delt": delt, "epsout": epsouts[1][2],
            "factor": factor, "delta": delta}


def compact(descriptor: dict[str, Any], digest: str, parsed: dict[str, Any]) -> dict[str, Any]:
    image_digest = descriptor["execution"]["container_image"].split("@", 1)[1]
    return {
        "format": "reactorsim.reference-compact-export/v1",
        "approval_status": "candidate_reference",
        "retention": "external-private",
        "publication": "not-approved",
        "case": {"case_id": descriptor["case_id"], "descriptor_sha256": digest},
        "tool_provenance": {
            "program": "donjon5", "version": "5.1.0",
            "source_commit": descriptor["version5_source"]["commit"],
            "source_tree": descriptor["version5_source"]["tree"],
            "executable_sha256": descriptor["execution"]["program_sha256"],
            "container_image_digest": image_digest,
        },
        "observations": [{"source_ordinal": 1,
            "quantity_id": "donjon5.AFA_180_310_type1_dual.flddir.k_effective",
            "value": number(parsed["factor"], "K effective", True),
            "source_lexeme": parsed["factor"], "unit_id": "1"}],
        "diagnostics": {
            "normal_end": True,
            "warning_codes": [{"code": "donjon5.sphapx.media_volume_missing", "count": 2}],
            "assertions": [{"assertion_id": "donjon5.AFA_180_310_type1_dual.k_effective",
                            "passed": True, "count": 1}],
            "convergence_status": "converged",
            "convergence": [
                {"quantity_id": "donjon5.flddir.outer_iteration", "value": 15,
                 "source_lexeme": parsed["iteration"]},
                {"quantity_id": "donjon5.flddir.dels", "value": number(parsed["dels"], "DELS", True),
                 "source_lexeme": parsed["dels"]},
                {"quantity_id": "donjon5.flddir.delt", "value": number(parsed["delt"], "DELT", True),
                 "source_lexeme": parsed["delt"]},
                {"quantity_id": "donjon5.flddir.epsout", "value": number(parsed["epsout"], "EPSOUT", True),
                 "source_lexeme": parsed["epsout"]},
            ],
        },
    }


def output_path(raw: str, input_path: Path, descriptor_path: Path) -> Path:
    path = Path(raw)
    if re.fullmatch(r"[A-Za-z0-9._-]+\.json", path.name) is None:
        fail("output filename must be a conservative .json basename")
    stem = path.name.split(".", 1)[0].upper()
    if stem in {"CON", "PRN", "AUX", "NUL"} or re.fullmatch(r"(?:COM|LPT)[1-9]", stem):
        fail("output filename uses a reserved Windows device basename")
    root = ROOT.resolve(strict=True)
    def within(candidate: Path) -> bool:
        try:
            candidate.relative_to(root)
            return True
        except ValueError:
            return False
    if within(path.absolute()):
        fail("output path must be lexically outside the repository")
    try:
        parent = path.parent.resolve(strict=True)
    except OSError as error:
        fail(f"output parent must exist: {error}")
    result = parent / path.name
    if not parent.is_dir() or within(result):
        fail("output path must resolve to an existing directory outside the repository")
    if result.exists() or result.is_symlink() or result in {input_path, descriptor_path}:
        fail("output path exists or collides with an input")
    return result


def write_new(path: Path, data: bytes) -> None:
    try:
        handle = os.open(str(path), os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    except OSError as error:
        fail(f"could not create output exclusively: {error}")
    try:
        with os.fdopen(handle, "wb") as stream:
            stream.write(data)
    except OSError as error:
        try:
            path.unlink()
        except OSError:
            pass
        fail(f"could not write output: {error}")


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--descriptor", default=str(DESCRIPTOR))
    try:
        args = parser.parse_args(argv)
        descriptor_path = Path(args.descriptor).resolve(strict=True)
        input_path = Path(args.input).resolve(strict=True)
        descriptor, digest = load_descriptor(descriptor_path)
        parsed = parse_listing(input_path)
        rendered = (json.dumps(compact(descriptor, digest, parsed), ensure_ascii=False,
                               allow_nan=False, indent=2, separators=(",", ": ")) + "\n").encode("utf-8")
        write_new(output_path(args.output, input_path, descriptor_path), rendered)
    except (ParseFailure, OSError, ValueError) as error:
        print(f"ERROR: {error}", file=sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
