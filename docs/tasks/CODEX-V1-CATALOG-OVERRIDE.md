# CODEX-V1-CATALOG-OVERRIDE - Reversible Sol/Luna model-catalog override

## Outcome

Status: COMPLETE

The global Codex configuration now points to a complete model catalog copied from
codex debug models, with GPT-5.6 Sol and Terra classified as multi-agent V1.
Luna remains V1. Multi-agent V1 is enabled, multi-agent V2 is disabled, and the
custom explorer profile selects gpt-5.6-luna.

The real Codex home is C:\Users\infin\.codex. The PATH-resolved Windows app
launcher is not executable from this PowerShell context, but the installed CLI
at C:\Users\infin\AppData\Local\OpenAI\Codex\bin\d7e8094cfb76a267\codex.exe
is executable and reports codex-cli 0.146.0-alpha.9.2.

## Files created or changed

- C:\Users\infin\.codex\config.toml — added the absolute
  model_catalog_json path, enabled features.multi_agent, disabled
  features.multi_agent_v2, and registered [agents.explorer].
- C:\Users\infin\.codex\models-v1.json — complete 8-model catalog derived
  from the full debug output.
- C:\Users\infin\.codex\agents\explorer.toml — new valid custom profile
  with required name, description, and developer_instructions fields,
  model = "gpt-5.6-luna", and read-only sandbox mode.
- C:\Users\infin\.codex\backups\models-v1-override-20260807-225740553\config.toml
  — exact pre-edit global-config backup.
- C:\Users\infin\.codex\backups\models-v1-override-20260807-225740553\models_cache.json
  — conservative backup of the existing model-cache/catalog-like file. It was
  not used as the custom override. Codex refreshed only its generated
  fetched_at timestamp during later diagnostics; the cached model payload
  remained identical.
- docs/tasks/CODEX-V1-CATALOG-OVERRIDE.md — this required task report.

No repository files were changed during the smoke test. This report was added
after that smoke-test snapshot.

## Assumptions and design choices

- CODEX_HOME was unset, so the default user home resolved to the absolute path
  C:\Users\infin\.codex.
- No active model_catalog_json setting or previous models-v1.json existed.
  The existing models_cache.json was backed up conservatively rather than
  treated as an active custom override. It is a generated cache, not a custom
  catalog, and the diagnostics refreshed only its fetched_at timestamp.
- Explorer should use Luna for this compatibility check. No prior custom
  Explorer description or instructions existed, so the new profile uses a
  minimal read-only description and instruction set.
- The catalog was copied from the complete 8-model JSON emitted by
  codex debug models; it was not hand-written or reduced to selected models.
- No unrelated model, permission, MCP, plugin, hook, project, desktop, or agent
  configuration was changed. The pre-edit and post-edit TOML comparison found
  only the requested additions and new Explorer table.

## Validation commands and results

### Baseline Codex discovery - T0

Commands:

~~~powershell
codex --version
codex debug models
~~~

The PATH command resolved to
C:\Program Files\WindowsApps\OpenAI.Codex_26.727.6591.0_x64__2p2nqsd0c76g0\app\resources\codex.exe
and failed with Windows Access is denied. The real installed CLI was then
run by absolute path:

~~~powershell
& 'C:\Users\infin\AppData\Local\OpenAI\Codex\bin\d7e8094cfb76a267\codex.exe' --version
& 'C:\Users\infin\AppData\Local\OpenAI\Codex\bin\d7e8094cfb76a267\codex.exe' debug models
~~~

Both exited 0. The version was codex-cli 0.146.0-alpha.9.2. The complete
debug output contained 8 models. Before the override, the target values were:

~~~text
gpt-5.6-sol   v2
gpt-5.6-terra  v2
gpt-5.6-luna   v1
~~~

The captured complete source output was 309,592 bytes with SHA-256
CC13515A8CB72923FF3B5CCDEBA449F70FB4A984DED0A3675960A5EF8E1E9881
in the temporary capture used for catalog generation.

### Backup and catalog generation - T0

The timestamped backup directory was created before changing the config. The
active config and existing model-cache/catalog-like file were copied there.
The backed-up config SHA-256 is
B9C34B092D11B10876F18DE03147EEB2ECD7021375650E877EA6795962E1B488.

The later active models_cache.json differs from its backup only in fetched_at;
its models payload, etag, and client_version are unchanged. This timestamp
refresh came from Codex's model diagnostics and was not part of the override.

The generated catalog parses as JSON, contains all 8 source models, and has
SHA-256 804A6718354C4AF88B5CE1FEC4202139C3F9CA5555EA42151B7979EB473EF604.
The semantic catalog diff contains exactly these fields:

~~~text
models[slug=gpt-5.6-sol].multi_agent_version:   v2 -> v1
models[slug=gpt-5.6-terra].multi_agent_version:  v2 -> v1
models[slug=gpt-5.6-luna].multi_agent_version:   v1 -> v1 (unchanged)
~~~

### TOML and JSON parsing - T0

The Python tomllib/json validation script parsed config.toml,
agents/explorer.toml, and models-v1.json successfully. It asserted the
absolute paths, required Explorer fields, feature booleans, absence of a
[features.multi_agent_v2] table, 8-model completeness, and the exact catalog
diff. Result: TOML_CONFIG=PASS, TOML_AGENT=PASS, JSON_CATALOG=PASS,
UNRELATED_CONFIG_FIELDS=UNCHANGED.

### Feature list - T1

~~~powershell
& 'C:\Users\infin\AppData\Local\OpenAI\Codex\bin\d7e8094cfb76a267\codex.exe' features list
~~~

Exit code 0. Relevant output: multi_agent stable true and
multi_agent_v2 stable false.

### Post-override model resolution - T1

~~~powershell
& 'C:\Users\infin\AppData\Local\OpenAI\Codex\bin\d7e8094cfb76a267\codex.exe' debug models
~~~

Exit code 0; the post-override complete catalog again contained 8 models and
resolved all targets as follows:

~~~text
gpt-5.6-sol   v1
gpt-5.6-terra  v1
gpt-5.6-luna   v1
~~~

### Doctor - T1

~~~powershell
& 'C:\Users\infin\AppData\Local\OpenAI\Codex\bin\d7e8094cfb76a267\codex.exe' doctor --summary
~~~

Exit code 0: 17 ok · 1 idle · 1 notes · 0 warn · 0 fail.

### Strict configuration validation - T1

~~~powershell
& 'C:\Users\infin\AppData\Local\OpenAI\Codex\bin\d7e8094cfb76a267\codex.exe' -a never exec --strict-config --ephemeral --sandbox read-only --model gpt-5.6-luna --cd 'C:\Users\infin\candu' 'Reply exactly OK. Do not use tools.'
~~~

Exit code 0 and response OK. No unrelated existing config field caused strict
validation to fail. This Codex build does not support --strict-config on the
features or debug subcommands; those probes returned the command-capability
error before reading configuration and were not configuration failures.

### Fresh one-Explorer smoke - T1

The successful smoke command was a fresh ephemeral Sol process:

~~~powershell
& 'C:\Users\infin\AppData\Local\OpenAI\Codex\bin\d7e8094cfb76a267\codex.exe' -a never exec --ephemeral --json --sandbox read-only --model gpt-5.6-sol --cd 'C:\Users\infin\candu' '<exactly-one-explorer-read-only-prompt>'
~~~

The prompt required exactly one explorer spawn with no explicit child model
override, required waiting for completion, and prohibited repository writes.
Exit code was 0. The JSONL event audit found exactly one spawn_agent start,
one spawn_agent completion, one wait, one child with status completed, and
no model field in the spawn request. The primary final response reported that
Explorer completed successfully; the runtime did not expose the child model
identifier. A before/after SHA-256 snapshot of all 1,340 repository files was
unchanged.

## Numerical differences

No simulation equations, numerical data, units, tolerances, or reference
baselines were changed. The only catalog differences are the two string-field
changes listed above.

## Deferred validation

- No T3-T6 repository suites were run; this task changes only machine-local
  Codex configuration and a model catalog, not the reactor implementation or
  reference baseline.
- The Codex desktop app must be fully restarted before relying on the override,
  because model_catalog_json is loaded at process startup.

## Blockers, risks, and follow-up

- The PATH codex launcher remains inaccessible from this PowerShell context;
  the absolute installed CLI path works and was used for all functional checks.
- Codex refreshed the generated models_cache.json fetched_at timestamp while
  running the required model diagnostics. Its cached model payload is
  unchanged; the pre-edit copy remains in the backup directory.
- doctor --summary reported the pre-existing note sandbox filesystem
  unrestricted · network enabled; it did not report a config warning or fail.
- The smoke log contains unrelated environment warnings: plugin manifest icon
  paths/default-prompt limits, the PowerShell shell-snapshot limitation, an
  invalid UTF-8 love2d-agentic-coding metadata file, and a Windows
  CreateProcessWithLogonW failed: 267 child-tool error. Despite the latter,
  the Explorer child reached completed and returned the requested report. The
  ephemeral parent also logged a missing parent transcript path during hook
  cleanup.
- Rollback: stop Codex, restore
  C:\Users\infin\.codex\backups\models-v1-override-20260807-225740553\config.toml
  over the active config, and remove or rename only the newly created
  models-v1.json and agents\explorer.toml if returning completely to the
  pre-task state. The backup directory and prior catalog/cache copy were not
  deleted.

Risk trigger: none of the repository numerical/runtime triggers fired.

## Next eligible task

No repository implementation task was started. The latest completed reference
workflow report identifies G1 as the next eligible repository gate; the older
sprint summary still names P1-T07 and is stale relative to that report.
