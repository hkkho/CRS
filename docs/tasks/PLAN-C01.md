# PLAN-C01 - Add curated literature review task and physics-use policy

## Outcome

Status: COMPLETE

Added `P1-T08` as a bounded Sol-led task to verify, read, and digest the six
user-supplied CANDU/DRAGON5/DONJON5 references. The implementation plan now
defines the source set, allowed outputs, claim-level digest requirements,
physics-domain crosswalk, golden-case admission gaps, non-goals, validation,
and independent review requirements.

Repository instructions and the reusable task template now require every new
physics-related task to wait for P1-T08, read its approved digest, and cite
applicable digest row IDs or a concrete `NotApplicable` coverage reason.
Literature remains an evidence layer: it cannot silently override approved
specifications or promote a published number into an equation, tolerance, or
golden value.

This task added the review task and policy only. It did not browse, download,
verify, summarize, or interpret the six sources; those actions belong solely to
P1-T08.

## Files created or changed

- `AGENTS.md` - added the repository-wide P1-T08 prerequisite and literature
  evidence rules for physics tasks.
- `CODEX_TASK_TEMPLATE.md` - added the required literature-digest applicability
  field and execution check.
- `docs/Implementation_plan.md` - added the detailed P1-T08 contract, six-source
  candidate bibliography, golden-data discipline, task-queue row, and execution
  ordering.
- `docs/tasks/PLAN-C01.md` - this task report.

No physics specification, equation, unit, normalization, tolerance, reference
input/output, manifest, golden data, runtime code, test code, Unity file, or
historical task/gate report changed.

## Assumptions and design choices

- The current administrative task ID is `PLAN-C01`; the newly queued execution
  task is `P1-T08`.
- “Use it for any physics-related tasks” is implemented as a real prerequisite:
  P1-T08 is next eligible and precedes G2-C04/G2-C06 because those tasks touch
  physics or physics-validation methodology.
- P1-T08 is placed in Phase 1 as a post-G1 amendment because it governs
  reference methodology. It does not reopen the already approved executable G1
  baseline unless a later task changes reference inputs, generated outputs, or
  approved data.
- The user-supplied citations are explicitly labeled candidate metadata.
  P1-T08 must resolve exact metadata and access/licensing status from primary
  institutional or publisher sources.
- P1-T08 may classify evidence as `ContextOnly`, `MethodologySupport`,
  `CandidateCaseDesign`, or `CandidateNumericEvidence`; it cannot assign
  `ApprovedGolden`.
- Source PDFs remain external unless explicit redistribution permission is
  established. The planned manifest is path-free and records lawful artifact
  identity/hashes when available.
- Safety/full-thermal-hydraulic, thorium, accident-tolerant-cladding, and
  advanced-cycle material may provide context but cannot expand runtime scope.

## Validation commands and results

### Required reading and scope - T0

```powershell
Get-Content -Raw AGENTS.md
Get-Content -Raw docs/Implementation_plan.md
Get-Content -Raw CODEX_TASK_TEMPLATE.md
Get-Content -Raw docs/tasks/TASK_REPORT_TEMPLATE.md
Get-Content -Raw docs/tasks/G2-C03.md
Get-Content -Raw docs/gates/G2-R1.md
git status --short
```

Result: PASS. The complete 754-line pre-edit implementation plan, repository
instructions, templates, completed G2-C03 report, current gate handoff, and
unborn/untracked worktree were inspected before edits.

### Focused task/policy contract check - T1

```powershell
@'
from pathlib import Path
import hashlib, re

paths = [Path('AGENTS.md'), Path('CODEX_TASK_TEMPLATE.md'), Path('docs/Implementation_plan.md')]
texts = {p: p.read_text(encoding='utf-8') for p in paths}
agents = texts[paths[0]]
template = texts[paths[1]]
plan = texts[paths[2]]

required_agents = [
    '## Literature evidence for physics tasks',
    'Until its report is\n`COMPLETE`, do not begin a new task',
    'List the applicable digest row IDs',
    'The literature digest does not override approved specifications',
    'A published number may become golden evidence only through a',
    'Stop on a source conflict',
]
assert all(term in agents for term in required_agents)
assert agents.index('## Literature evidence for physics tasks') < agents.index('## Numerical and runtime rules')
assert 'Literature digest applicability (physics tasks):' in template
assert 'first confirm that P1-T08 is COMPLETE' in template
assert 'P1-T08 itself is the only execution exception' in template

required_plan = [
    '### Post-G1 task P1-T08',
    'user-supplied candidate metadata, not yet verified',
    '**Allowed execution files:**',
    '`docs/reference/candu-literature-digest-v1.md`',
    '`reference/manifests/candu-literature-sources-v1.json`',
    '`docs/spec/observables-validation-methodology-v1.md`',
    '`docs/tasks/P1-T08.md`',
    '`ContextOnly`, `MethodologySupport`',
    '`CandidateCaseDesign`, or `CandidateNumericEvidence`',
    'No row is\n   `ApprovedGolden` in P1-T08',
    'Record `NotReported` rather than',
    'complete\n   case-admission proof',
    'trigger requires T3 and independent Sol review',
    'P1-T08 is a late post-G1 amendment and is the next eligible execution task after',
    'It must complete before G2-C04, G2-C06',
    'does not reopen G1 or trigger T6 unless',
    'Literature digest applicability (physics tasks): <P1-T08 row IDs',
]
assert all(term in plan for term in required_plan)
queue_rows = [line for line in plan.splitlines() if line.startswith('| P1-T08 |')]
assert len(queue_rows) == 1
assert 'Sol max' in queue_rows[0] and 'G1, G2-C03' in queue_rows[0]
assert 'ApprovedGolden |' not in plan and '| ApprovedGolden' not in plan

urls = [
    'https://publications.polymtl.ca/5048/11/2019_Naceur_Candu-6_operation_simulations_using_accident.pdf',
    'https://publications.polymtl.ca/714/',
    'https://publications.polymtl.ca/6643/',
    'https://publications.polymtl.ca/1307/',
    'https://doi.org/10.1016/j.anucene.2017.11.016',
    'https://doi.org/10.1016/j.nucengdes.2018.06.026',
]
assert all(plan.count(url) == 1 for url in urls)
section = plan.split('The bibliography below is user-supplied candidate metadata, not yet verified:', 1)[1].split('**Allowed execution files:**', 1)[0]
assert len(re.findall(r'^\d+\. ', section, flags=re.MULTILINE)) == 6

for path, text in texts.items():
    lines = text.splitlines()
    trailing = [(n, line) for n, line in enumerate(lines, 1) if line.rstrip() != line]
    if path == Path('docs/Implementation_plan.md'):
        assert [n for n, _ in trailing] == [3, 4, 5, 6]
        assert all(line.endswith('  ') and line.startswith('**') for _, line in trailing)
    else:
        assert not trailing, f'trailing whitespace: {path}'
    assert sum(line.startswith('```') for line in lines) % 2 == 0, f'unbalanced fences: {path}'
    width = None
    for number, line in enumerate(lines, 1):
        if line.startswith('|') and line.endswith('|'):
            cells = len(line.split('|')) - 2
            if width is None:
                width = cells
            else:
                assert cells == width, f'table width: {path}:{number}'
        else:
            width = None
    for target in re.findall(r'\[[^\]]+\]\(([^)]+)\)', text):
        target = target.strip('<>')
        if re.match(r'^[a-z]+://', target) or target.startswith('#'):
            continue
        assert (path.parent / target.split('#', 1)[0]).resolve().exists(), f'broken local link: {path} -> {target}'
    print(f'{path} lines={len(lines)} sha256={hashlib.sha256(path.read_bytes()).hexdigest()}')

print('PLAN_C01_T1_PASS sources=6 queue_rows=1 physics_block=P1-T08 golden_promotion=fail_closed')
'@ | python -
```

Result: PASS, exit `0`.

```text
AGENTS.md lines=118 sha256=96579efdac7178a1e8cd3c739af8b519322602a6e5f4314cc0e6e7e8c431fe9b
CODEX_TASK_TEMPLATE.md lines=49 sha256=af81a1c1996ce03778e567bb197c6a3717abba1b35574006073b99cde7fec2ad
docs\Implementation_plan.md lines=875 sha256=b117ef1dcc8672a255aafd3bdbb4aa609aa0fd71ca6648b62543f159b4d95798
PLAN_C01_T1_PASS sources=6 queue_rows=1 physics_block=P1-T08 golden_promotion=fail_closed
```

The check proves that all six URLs occur exactly once in the candidate
bibliography, P1-T08 has one queue row and correct prerequisites, required
outputs/use classes/golden guards are present, physics work is blocked until the
digest exists, Markdown fences/tables/local links are valid, and only the four
intentional pre-existing Markdown hard breaks remain.

### Repository format - T1

```powershell
& .\tools\Check-Format.ps1
```

Result: PASS, exit `0`.

## Numerical differences

Not applicable; no numerical behavior, physics equation, coefficient, unit,
normalization, tolerance, reference result, golden value, or runtime data
changed.

## Deferred validation

- Retrieval, metadata verification, full-text review, claim extraction,
  literature digest, source manifest, and validation-methodology crosswalk -
  deferred to P1-T08 by design.
- T3 and independent Sol review - required within P1-T08 because that task will
  change reference methodology/manifest evidence; not triggered by this
  task-definition-only amendment.
- T6 - deferred unless a later separate task changes or regenerates an approved
  DRAGON5/DONJON5 reference baseline.
- G2-C04, G2-C06, and the G2 rerun - remain ordered after P1-T08.

## Blockers, risks, and follow-up

- Blockers: none for PLAN-C01.
- Risk triggers: none; this task changed planning/instruction documents only and
  did not alter physics, runtime behavior, schemas, manifests, tolerances, or
  reference/golden data.
- Risks accepted or deferred: candidate bibliography metadata remains
  deliberately unverified until P1-T08. No source claim or number was treated as
  authoritative.
- Follow-up work: execute P1-T08 alone, then resume G2-C04 and G2-C06 using the
  approved digest.
- No commit, push, publication, external message, download, secret, destructive
  operation, or repository-local generated artifact was produced.

## Final scope audit

Baseline:

`C:\Users\infin\AppData\Local\Temp\candu-g2-c03-sol-complete-audit-20260809-1240\git-visible-manifest.txt`

Final:

`C:\Users\infin\AppData\Local\Temp\candu-plan-c01-final-audit-20260809-1300\git-visible-manifest.txt`

The final comparison contains 724 Git-visible paths: one addition
(`docs/tasks/PLAN-C01.md`), exactly three changed baseline paths (`AGENTS.md`,
`CODEX_TASK_TEMPLATE.md`, and `docs/Implementation_plan.md`), zero removals, and
zero newly introduced private/raw or generated-artifact paths.

## Next eligible task

`P1-T08 - curated CANDU literature digest and physics/golden-method crosswalk`.
No new physics-related task, including G2-C04 or G2-C06, starts before P1-T08 is
complete and independently approved.

Source: [`docs/Implementation_plan.md`](../Implementation_plan.md), sections
5.4, 6, 7 Phase 1, and 8.
