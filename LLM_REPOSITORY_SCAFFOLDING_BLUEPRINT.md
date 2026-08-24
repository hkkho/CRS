# Retired generic LLM repository scaffolding blueprint

**Status:** superseded for this repository. This file is retained only as a
pointer for people looking for the former generic guidance; it does not direct
CANDU work and must not be used to create a parallel task, requirements, or
status system.

## CANDU documentation map

| Need | Current source |
|---|---|
| How to select, execute, review, validate, and report work | [AGENTS.md](AGENTS.md) |
| Product scope, architecture, phases, gates, and capability targets | [docs/Implementation_plan.md](docs/Implementation_plan.md) |
| Current status, evidence gaps, and next handoff | [docs/PROJECT_SCOPE.md](docs/PROJECT_SCOPE.md) |
| Approved technical behavior | [docs/spec/](docs/spec/) and accepted [docs/adr/](docs/adr/) records |
| Task and gate evidence | [docs/tasks/](docs/tasks/) and [docs/gates/](docs/gates/) |
| Offline DRAGON5/DONJON5 provenance and operating boundary | [reference/README.md](reference/README.md) |

## Do not revive the generic system

Do not create `MASTER_REQUIREMENTS.md`, `PROJECT_STATUS.md`,
`TECHNICAL_DEBT.md`, generic `tasks/{backlog,active,done}` folders,
milestone ledgers, XML requirement tags, or copied generic agent prompts.
Those mechanisms duplicate the current project controls and can contradict
approved phase, gate, literature, and evidence rules.

For any new task, use `CODEX_TASK_TEMPLATE.md`, execute one task ID, and
write the required report. If a proposal is out of scope, record it in
`docs/backlog.md` when available rather than adding a second planning system.
