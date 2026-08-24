# Reference output formats v1

Status: schema contract for P1-T04. The examples are validation fixtures only and are not approved reference data, golden data, or redistributable upstream artifacts.

## Boundary

`reference-raw-run-v1.schema.json` describes a retention manifest for one external, private DRAGON5 or DONJON5 run. It records identities, timestamps, hashes, byte lengths, outcomes, and diagnostics. It never contains an input, listing, standard output, standard error, HDF5 payload, or other retained artifact. Each artifact remains outside Git with `retention: external-private` and `publication: not-approved`. A raw artifact hash proves which bytes were retained; it is not a semantic reproducibility criterion.

`reference-compact-export-v1.schema.json` describes deterministic semantic output for only the two Phase 1 smoke cases. It is not the runtime data-pack schema and it is not a golden-data approval format. The compact document deliberately excludes run identifiers, timestamps, host facts, paths, raw-manifest or raw-listing hashes, timing, memory diagnostics, and variable warning text. Repeated equivalent runs can therefore produce identical compact bytes.

P1-T07 may associate an external raw manifest with a compact export. The association does not belong in the compact document.

## Strictness and evolution

Both schemas use JSON Schema Draft 2020-12, have distinct absolute `$id` URNs, require an exact `format` discriminator ending in `/v1`, and close every defined object with `additionalProperties: false`. Unknown formats and fields are rejected. A new case family, physics quantity, changed meaning, or breaking structure requires a new format version; v1 is not widened in place.

The schemas contain no acceptance tolerances and define no new physics, normalization, geometry, burnup, time, indexing, or convergence rule. The DONJON convergence measures preserve source-emitted evidence. `DELS`, `DELT`, and `EPSOUT` have no invented unit. `unit_id: "1"` is used only for dimensionless physics observations.

## Raw-run manifest

The raw manifest records only the two Phase 1 smoke cases. The schema binds each exact case ID to its matching program and requires at least one external listing artifact. It also records:

- case descriptor identity and repository-relative path;
- Version5 commit/tree, exact executable hash, and container image digest;
- repository-relative runner identity, UTC start/finish timestamps, an invocation identifier, and offline network mode;
- external artifact role, media type, byte length, SHA-256, retention/publication policy, and an optional opaque locator identifier; and
- success/failure, normal-end evidence, stable warning and error codes, assertion results, and convergence measures.

Repository paths use forward slashes. Absolute drive, rooted, UNC/backslash, and parent-traversal paths are invalid. Start/finish timestamps must use UTC `Z`, and finish must not precede start. `locator_id`, when present, is opaque and cannot encode a local path. Payload-bearing fields are not part of the closed schema. Both the retained listing and separately captured program stderr are mandatory artifacts, including a zero-byte stderr stream. Successful manifest validation requires normal end, no error codes, only passed assertions, and converged source evidence. Failed manifest validation requires at least one structured error code while `normal_end` remains independent, truthful program evidence.

## Compact export

Top-level member order is canonical and fixed:

1. `format`
2. `approval_status`
3. `retention`
4. `publication`
5. `case`
6. `tool_provenance`
7. `observations`
8. `diagnostics`

Nested member and array order is the order shown in the valid examples and checked by `Test-P1-T04-Schemas.ps1`. Documents are UTF-8 without a byte-order mark, use LF line endings, end with one LF, and use two-space JSON indentation. The reference rendering is JSON serialization with insertion order preserved, no ASCII escaping, non-finite values rejected, two-space indentation, `,` item separators, `: ` name separators, and one terminal LF; the validator parses and renders twice and requires exact byte equality. This is a project serialization contract, not a claim of RFC 8785 canonicalization.

DRAGON exports contain exactly three ordered source observations for the three `FINAL KINF` records. Their explicit one-based `source_ordinal` and source-qualified `quantity_id` make no burnup-step, time-step, or geometry assertion. Diagnostics retain the three upstream assertion occurrences and the three final external-convergence occurrences as counts.

DONJON exports contain exactly one physics observation: the FLDDIR `K-EFFECTIVE` value. The one upstream assertion occurrence, stable warning code/count, explicit `converged` status, outer iteration, `DELS`, `DELT`, and `EPSOUT` remain under `diagnostics`; they are not additional physics observations. The validator checks the retained source relationship `DELT <= EPSOUT`; it does not introduce a new criterion.

All quantitative values are finite JSON numbers. `source_lexeme` preserves the source spelling for audit without making a numeric string authoritative, and its exact decimal value must agree with `value`. Duplicate object names and non-standard `NaN`/infinity tokens are rejected. The validator additionally checks unique identifiers and prescribed array order where JSON Schema alone is insufficient.

## Legal and approval status

The DRAGON deck, procedures, nuclear-data asset, and raw outputs remain external/private. The DONJON deck, assertion procedure, HDF5 group constants, and raw outputs also remain external/private; the HDF5 inputs carry CEA/AREVA AL `0D001` and ECCN `N` export metadata. Public availability is not redistribution permission.

Committed examples contain only minimal scalar facts already recorded in P1-T02/P1-T03 reports and are marked `unapproved_schema_example`. Parser-produced exports may use `candidate_reference`, but both statuses require `retention: external-private` and `publication: not-approved`; approval remains a gate decision outside this byte format. Examples validate structure only. They do not approve a reference baseline, license redistribution, or change the publication stop condition.
