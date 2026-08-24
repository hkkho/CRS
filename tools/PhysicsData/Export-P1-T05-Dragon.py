#!/usr/bin/env python3
"""Fail-closed P1-T05 parser/exporter for the one DRAGON5 lumpSS smoke case.

This tool consumes an external/private DRAGON listing and emits only the compact
P1-T04 evidence.  It never accepts arbitrary DRAGON cases and it deliberately
does not retain or embed the listing in its output.
"""

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
    """Raised for any invalid or ambiguous parser input."""


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
EXPECTED_DESCRIPTOR_PATH = REPOSITORY_ROOT / "reference" / "dragon5" / "P1-T02-lumpSS.case.json"
EXPECTED_DESCRIPTOR_SHA256 = "ce053458cbd18987a9ca81dd15a088b0d64b36eebded23395bf928786ee399ae"
EXPECTED_CASE_ID = "dragon5-lumpss-v5.1.0-source-era"
EXPECTED_KINF = ("1.329496E+00", "1.314796E+00", "1.292124E+00")

NUMBER = r"-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[Ee][+-]?[0-9]+)?"
NUMBER_FULL = re.compile(rf"^{NUMBER}$")
CONVERGENCE_TOKEN = re.compile(r"\bEXTERNAL\s+CONVERGENCE\b", re.IGNORECASE)
KINF_TOKEN = re.compile(r"\bFINAL\s+KINF\b", re.IGNORECASE)
ASSERTION_TOKEN = re.compile(r"\bTEST\s+SUCCESSFUL\b", re.IGNORECASE)
NORMAL_END_TOKEN = re.compile(r"\bNORMAL\s+END\b", re.IGNORECASE)
CONVERGENCE_LINE = re.compile(
    r"^\s*FLU2DR:\s*.*?\bEXTERNAL\s+CONVERGENCE\s+REACHED\s+AFTER\s+"
    r"(?P<iterations>[1-9][0-9]*)\s+ITERATIONS\.\s*$",
    re.IGNORECASE,
)
KINF_LINE = re.compile(
    rf"^\s*\+\+\s+TRACKING\b.*?\bFINAL\s+KINF\s*=\s*(?P<kinf>{NUMBER})\s+"
    rf"FINAL\s+KEFF\s*=\s*(?P<keff>{NUMBER})\s+B2\s*=\s*(?P<b2>{NUMBER})\s+"
    rf"PRECISION\s*=\s*(?P<precision>{NUMBER})\s*$",
    re.IGNORECASE,
)
ASSERTION_LINE = re.compile(
    rf"^\s*>\|\s*TEST\s+SUCCESSFUL\s*;\s*DELTA\s*=\s*(?P<delta>{NUMBER})\s*\|>[0-9]+\s*$",
    re.IGNORECASE,
)
COMPLETION_LINE = re.compile(r"^\s*>\|\s*test\s+lumpSS\s+completed\s*\|>[0-9]+\s*$", re.IGNORECASE)
RUNTIME_COMPLETION_TOKEN = re.compile(
    r"^\s*>\|.*\btest\s+lumpSS\s+completed\b",
    re.IGNORECASE,
)
NORMAL_END_LINE = re.compile(
    r"^\s*normal\s+end\s+of\s+execution\s+for\s+dragon\s+5\s+Version\s+5\.1\.0\s*$",
    re.IGNORECASE,
)
FATAL_PATTERNS = (
    re.compile(r"\b(?:fatal\s+error|x?abort|test\s+fail(?:ure|ed)|error\s+code|kernel\s+error)\b", re.IGNORECASE),
    re.compile(r"\b(?:non[- ]?converg(?:ence|ed|ing)?|not\s+converg(?:ed|ing)?)\b", re.IGNORECASE),
)
NONFINITE_TOKEN = re.compile(
    r"(?<![A-Za-z0-9_-])[+-]?(?:nan|inf|infinity)(?![A-Za-z0-9_-])",
    re.IGNORECASE,
)


def fail(message: str) -> None:
    raise ParseFailure(message)


def reject_json_constant(value: str) -> None:
    raise ValueError(f"non-standard JSON numeric token {value!r}")


def unique_object(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"duplicate JSON object name {key!r}")
        result[key] = value
    return result


def read_strict_utf8(path: Path, label: str) -> bytes:
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


def read_strict_json(path: Path) -> tuple[dict[str, Any], bytes]:
    data = read_strict_utf8(path, "case descriptor")
    if b"\r" in data:
        fail("case descriptor must use LF line endings")
    if not data.endswith(b"\n") or data.endswith(b"\n\n"):
        fail("case descriptor must have exactly one terminal LF")
    try:
        value = json.loads(
            data.decode("utf-8"),
            parse_constant=reject_json_constant,
            parse_float=Decimal,
            object_pairs_hook=unique_object,
        )
    except (json.JSONDecodeError, ValueError) as error:
        fail(f"invalid case descriptor JSON: {error}")
    if not isinstance(value, dict):
        fail("case descriptor root must be an object")
    return value, data


def require_value(document: dict[str, Any], path: tuple[str, ...], expected: Any) -> None:
    current: Any = document
    for key in path:
        if not isinstance(current, dict) or key not in current:
            fail(f"case descriptor is missing {'/'.join(path)}")
        current = current[key]
    if current != expected:
        fail(f"case descriptor has unsupported {'/'.join(path)}: {current!r}")


def load_case_descriptor(path: Path) -> tuple[dict[str, Any], str]:
    try:
        resolved = path.resolve(strict=True)
        expected_path = EXPECTED_DESCRIPTOR_PATH.resolve(strict=True)
    except OSError as error:
        fail(f"could not resolve case descriptor: {error}")
    if resolved != expected_path:
        fail("only reference/dragon5/P1-T02-lumpSS.case.json is supported")

    descriptor, descriptor_bytes = read_strict_json(resolved)
    descriptor_sha256 = hashlib.sha256(descriptor_bytes).hexdigest()
    if descriptor_sha256 != EXPECTED_DESCRIPTOR_SHA256:
        fail("P1-T02 case descriptor SHA-256 does not match the approved parser input")

    require_value(descriptor, ("format",), "candu.reference-dragon5-smoke-case/v1")
    require_value(descriptor, ("task_id",), "P1-T02")
    require_value(descriptor, ("case_id",), EXPECTED_CASE_ID)
    require_value(descriptor, ("version5_source", "ref"), "v5.1.0")
    require_value(descriptor, ("version5_source", "commit"), "eee582f8594d3b7d01b65660ca1e8bef00eb6b2a")
    require_value(descriptor, ("version5_source", "tree"), "0d3e8f721c84b3c3b2d03e9898a8a0a9c29287d8")
    require_value(descriptor, ("execution", "program_sha256"), "3121023a186cab6d0a56fd363bb979794fcd904f860f49439567bff4b1510cd0")
    require_value(
        descriptor,
        ("execution", "container_image"),
        "docker.oecd-nea.org/dragon/5.1@sha256:eb8ddff7d788f563f958ada277a2c8304d2dcf829ef59a35db62438397188d79",
    )
    require_value(descriptor, ("execution", "required_final_kinf"), list(EXPECTED_KINF))
    return descriptor, descriptor_sha256


def parse_finite_number(lexeme: str, label: str, require_exportable: bool) -> float:
    if not NUMBER_FULL.fullmatch(lexeme):
        fail(f"{label} has malformed numeric lexeme {lexeme!r}")
    try:
        decimal_value = Decimal(lexeme)
    except InvalidOperation as error:
        fail(f"{label} could not parse numeric lexeme {lexeme!r}: {error}")
    if not decimal_value.is_finite():
        fail(f"{label} is non-finite")
    try:
        value = float(decimal_value)
    except (OverflowError, ValueError) as error:
        fail(f"{label} cannot be represented as a finite JSON number: {error}")
    if not math.isfinite(value):
        fail(f"{label} cannot be represented as a finite JSON number")
    if require_exportable and Decimal(str(value)) != decimal_value:
        fail(f"{label} cannot preserve exact decimal agreement in the compact export")
    return value


def parse_listing(path: Path) -> list[tuple[str, int, str]]:
    try:
        listing_path = path.resolve(strict=True)
    except OSError as error:
        fail(f"could not resolve listing: {error}")
    data = read_strict_utf8(listing_path, "listing")
    if b"\x00" in data:
        fail("listing contains a NUL byte")
    if b"\r" in data:
        fail("listing must use LF line endings without bare carriage returns")
    text = data.decode("utf-8", errors="strict")
    events: list[tuple[str, int, str]] = []
    completion_lines: list[int] = []
    normal_end_lines: list[int] = []

    for line_number, line in enumerate(text.splitlines(), start=1):
        for pattern in FATAL_PATTERNS:
            if pattern.search(line):
                fail(f"listing line {line_number} contains a fatal diagnostic")
        if NONFINITE_TOKEN.search(line):
            fail(f"listing line {line_number} contains NaN or infinity")

        recognized: list[str] = []
        if CONVERGENCE_TOKEN.search(line):
            if len(CONVERGENCE_TOKEN.findall(line)) != 1:
                fail(f"listing line {line_number} contains duplicate external-convergence markers")
            match = CONVERGENCE_LINE.fullmatch(line)
            if match is None:
                fail(f"listing line {line_number} has ambiguous external-convergence syntax")
            int(match.group("iterations"))
            events.append(("convergence", line_number, ""))
            recognized.append("convergence")

        if KINF_TOKEN.search(line):
            if len(KINF_TOKEN.findall(line)) != 1:
                fail(f"listing line {line_number} contains duplicate FINAL KINF markers")
            match = KINF_LINE.fullmatch(line)
            if match is None:
                fail(f"listing line {line_number} has ambiguous FINAL KINF syntax")
            kinf = match.group("kinf")
            parse_finite_number(kinf, f"listing line {line_number} FINAL KINF", require_exportable=True)
            parse_finite_number(match.group("keff"), f"listing line {line_number} FINAL KEFF", require_exportable=False)
            parse_finite_number(match.group("b2"), f"listing line {line_number} B2", require_exportable=False)
            parse_finite_number(match.group("precision"), f"listing line {line_number} PRECISION", require_exportable=False)
            events.append(("kinf", line_number, kinf))
            recognized.append("kinf")

        if ASSERTION_TOKEN.search(line):
            match = ASSERTION_LINE.fullmatch(line)
            if match is None:
                fail(f"listing line {line_number} has ambiguous TEST SUCCESSFUL syntax")
            parse_finite_number(match.group("delta"), f"listing line {line_number} DELTA", require_exportable=False)
            events.append(("assertion", line_number, ""))
            recognized.append("assertion")

        if len(recognized) > 1:
            fail(f"listing line {line_number} combines multiple required markers")

        if RUNTIME_COMPLETION_TOKEN.search(line):
            if COMPLETION_LINE.fullmatch(line) is None:
                fail(f"listing line {line_number} has ambiguous runtime-completion syntax")
            completion_lines.append(line_number)
        if NORMAL_END_TOKEN.search(line):
            if NORMAL_END_LINE.fullmatch(line) is None:
                fail(f"listing line {line_number} has ambiguous normal-end syntax")
            normal_end_lines.append(line_number)

    expected_events = ["convergence", "kinf", "assertion"] * len(EXPECTED_KINF)
    observed_events = [kind for kind, _, _ in events]
    if observed_events != expected_events:
        fail(
            "listing required marker sequence must be exactly "
            "EXTERNAL CONVERGENCE -> FINAL KINF -> TEST SUCCESSFUL for each of three blocks"
        )

    kinf = [lexeme for kind, _, lexeme in events if kind == "kinf"]
    if tuple(kinf) != EXPECTED_KINF:
        fail(f"listing FINAL KINF lexemes do not exactly match P1-T02: {kinf!r}")

    if len(completion_lines) != 1:
        fail(f"listing must contain exactly one runtime completion marker, found {len(completion_lines)}")
    if len(normal_end_lines) != 1:
        fail(f"listing must contain exactly one normal-end marker, found {len(normal_end_lines)}")
    final_block_line = events[-1][1]
    if not (final_block_line < completion_lines[0] < normal_end_lines[0]):
        fail("completion and normal-end markers must follow all three required blocks in order")
    return events


def build_compact_export(descriptor: dict[str, Any], descriptor_sha256: str, events: list[tuple[str, int, str]]) -> dict[str, Any]:
    kinf = [lexeme for kind, _, lexeme in events if kind == "kinf"]
    observations: list[dict[str, Any]] = []
    for ordinal, lexeme in enumerate(kinf, start=1):
        observations.append(
            {
                "source_ordinal": ordinal,
                "quantity_id": f"dragon5.lumpSS.final_kinf.source_{ordinal}",
                "value": parse_finite_number(lexeme, f"FINAL KINF source_{ordinal}", require_exportable=True),
                "source_lexeme": lexeme,
                "unit_id": "1",
            }
        )

    return {
        "format": "reactorsim.reference-compact-export/v1",
        "approval_status": "candidate_reference",
        "retention": "external-private",
        "publication": "not-approved",
        "case": {
            "case_id": descriptor["case_id"],
            "descriptor_sha256": descriptor_sha256,
        },
        "tool_provenance": {
            "program": "dragon5",
            "version": descriptor["version5_source"]["ref"].removeprefix("v"),
            "source_commit": descriptor["version5_source"]["commit"],
            "source_tree": descriptor["version5_source"]["tree"],
            "executable_sha256": descriptor["execution"]["program_sha256"],
            "container_image_digest": descriptor["execution"]["container_image"].split("@", 1)[1],
        },
        "observations": observations,
        "diagnostics": {
            "normal_end": True,
            "warning_codes": [],
            "assertions": [
                {
                    "assertion_id": "dragon5.lumpSS.upstream_assertions",
                    "passed": True,
                    "count": 3,
                }
            ],
            "convergence_status": "converged",
            "convergence": [
                {
                    "quantity_id": "dragon5.lumpSS.final_external_convergence_count",
                    "value": 3,
                    "source_lexeme": "3",
                }
            ],
        },
    }


def render_compact_export(document: dict[str, Any]) -> bytes:
    try:
        rendered = json.dumps(
            document,
            ensure_ascii=False,
            allow_nan=False,
            indent=2,
            separators=(",", ": "),
        ) + "\n"
    except (TypeError, ValueError) as error:
        fail(f"could not render compact export: {error}")
    return rendered.encode("utf-8")


def is_within(candidate: Path, parent: Path) -> bool:
    try:
        candidate.relative_to(parent)
        return True
    except ValueError:
        return False


def resolve_output_path(raw_output: str, input_path: Path, descriptor_path: Path) -> Path:
    output = Path(raw_output)
    if not re.fullmatch(r"[A-Za-z0-9._-]+\.json", output.name):
        fail("output filename must be a conservative .json basename")
    reserved_stem = output.name.split(".", 1)[0].upper()
    if reserved_stem in {"CON", "PRN", "AUX", "NUL"} or re.fullmatch(r"(?:COM|LPT)[1-9]", reserved_stem):
        fail("output filename uses a reserved Windows device basename")
    repository_root = REPOSITORY_ROOT.resolve(strict=True)
    lexical_output = output.absolute()
    if is_within(lexical_output, repository_root):
        fail("output path must be lexically outside the repository")
    try:
        parent = output.parent.resolve(strict=True)
    except OSError as error:
        fail(f"output parent must already exist and resolve without escape: {error}")
    if not parent.is_dir():
        fail("output parent must be an existing directory")
    output = parent / output.name
    if is_within(output, repository_root):
        fail("output path must resolve outside the repository")
    if output.exists() or output.is_symlink():
        fail("output path already exists; refusing to overwrite")
    if output == input_path or output == descriptor_path:
        fail("output path must not collide with input or descriptor")
    return output


def write_new_file(path: Path, data: bytes) -> None:
    try:
        descriptor = os.open(str(path), os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    except OSError as error:
        fail(f"could not create output without overwrite: {error}")
    try:
        with os.fdopen(descriptor, "wb") as stream:
            stream.write(data)
    except OSError as error:
        try:
            path.unlink()
        except OSError:
            pass
        fail(f"could not write output: {error}")


def parse_arguments(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Parse the fixed P1-T02 DRAGON lumpSS listing into P1-T04 compact JSON.")
    parser.add_argument("--input", required=True, help="External/private DRAGON listing to parse.")
    parser.add_argument("--output", required=True, help="New external/private compact-export path; it must not exist.")
    parser.add_argument(
        "--descriptor",
        default=str(EXPECTED_DESCRIPTOR_PATH),
        help="P1-T02 case descriptor; only the repository's exact descriptor is accepted.",
    )
    return parser.parse_args(argv)


def main(argv: list[str]) -> int:
    try:
        args = parse_arguments(argv)
        descriptor_path = Path(args.descriptor).resolve(strict=True)
        input_path = Path(args.input).resolve(strict=True)
        descriptor, descriptor_sha256 = load_case_descriptor(descriptor_path)
        events = parse_listing(input_path)
        output_path = resolve_output_path(args.output, input_path, descriptor_path)
        write_new_file(output_path, render_compact_export(build_compact_export(descriptor, descriptor_sha256, events)))
    except (ParseFailure, OSError, ValueError) as error:
        print(f"ERROR: {error}", file=sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
