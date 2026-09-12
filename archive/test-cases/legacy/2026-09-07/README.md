# Archived legacy test cases

The legacy .NET, Unity, and browser test cases, their checked-in test fixtures,
and the legacy `tools/**/Test-*.ps1` runners were moved here on 2026-09-07
before the replacement suite was implemented.

The directory structure below preserves each file's original repository-relative
path. These files are intentionally outside the active .NET solution, Unity
asset tree, browser test discovery paths, or active test-runner paths.

The replacement case draft is [TEST_CASE_REBUILD_DRAFT.md](../../../../docs/TEST_CASE_REBUILD_DRAFT.md).

To restore a specific archived file, move it from this archive back to the
original path shown below it. Do not copy archived assertions into the new
suite mechanically; use the draft's setup/action/oracle rules.
