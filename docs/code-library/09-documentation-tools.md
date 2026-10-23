# 9. Maintaining and viewing the library

[Previous: source atlas](08-source-atlas.md) · [Library](index.md)

## Delivered format

**Markdown** is a plain-text document format whose punctuation marks headings,
links, lists and examples. A **static-site generator** converts such source
documents into HTML pages before they are served. In this library, MkDocs is the
generator and Material is the presentation theme. Mermaid is the diagram
language used inside selected fenced blocks.

The Markdown files are the editable library. They can be read in the repository
without installing a documentation generator. Mermaid fences describe the startup,
request, transaction, numerical and presentation connections alongside the prose.
MkDocs adds navigation, search and browser-rendered diagrams. The Pages workflow
publishes the player manual, current reactor theory and code walkthrough together
at [CRS Documentation](https://hkkho.github.io/CRS/docs/); local preview uses the
same configuration.

## Preview with MkDocs

**Example 7. Creating a documentation environment and live preview.** These
PowerShell commands require Python 3.10 or newer and begin at the repository root.
The virtual environment resides under ignored `tmp` and keeps the documentation
packages separate from the frontend's npm dependencies:

```powershell
python -m venv tmp/code-docs-venv
tmp/code-docs-venv/Scripts/python.exe -m pip install -r tools/docs/requirements.txt
tmp/code-docs-venv/Scripts/python.exe -m mkdocs serve --dev-addr 127.0.0.1:8000
```

The first line uses Python's `venv` module to create an environment with its own
interpreter and installed packages. The second invokes that interpreter's `pip`
module; `-r` loads the package names and pinned versions from
`tools/docs/requirements.txt`. The third invokes MkDocs in the same environment
and starts its preview server. `--dev-addr 127.0.0.1:8000` selects a local-only
address and port. The server remains running and rebuilds documentation when
watched source files change. Its pages are available at
`http://127.0.0.1:8000`; stopping the process stops that preview.

**Example 8. Building and validating the static library.** This command runs from
the repository root using the previously created documentation environment:

```powershell
tmp/code-docs-venv/Scripts/python.exe -m mkdocs build --strict
```

`build` reads `mkdocs.yml`, converts the Markdown pages and writes the site to
`artifacts/code-library`. `--strict` turns MkDocs warnings into build failures,
making broken internal documentation links visible during verification. The
repository-link hook also fails the build for an invalid local source target.
A successful build produces HTML and a search index; Mermaid figures are still
rendered later by the browser. The command finishes after generation rather than
leaving a server running.

**Example 9. Serving the already-built output.** This alternative to live preview
serves the generated files without rebuilding them:

```powershell
python -m http.server 8000 --bind 127.0.0.1 --directory artifacts/code-library
```

`python -m http.server` runs Python's static HTTP server. `8000` selects the port,
`--bind 127.0.0.1` restricts the address to the local machine, and `--directory`
selects the generated site folder. This process stays running until stopped.
Unlike `mkdocs serve`, it does not watch Markdown sources, so source changes
require another build before appearing here. It uses the same port as Example 7;
the two examples are alternative servers and cannot occupy that address at once.

The pinned direct dependencies are in
[tools/docs/requirements.txt](../../tools/docs/requirements.txt).
[mkdocs.yml](../../mkdocs.yml) selects this library as its documentation root;
the Pages workflow includes the generated output in the combined game release.
The documentation generator does not compile the simulation or change its rules.

### Source links in the two formats

Raw Markdown uses repository-relative source links so a checked-out file can be
opened in context. A standalone HTML site cannot follow a relative path outside
its documentation root. The small
[repository_links.py hook](../../tools/docs/repository_links.py) validates these
local targets at build time and rewrites out-of-library links to the configured
GitHub repository. Library-to-library links remain internal navigation.

By default, generated source links use `main`, including links to other existing
repository documentation. A **Git revision** identifies a particular repository
state, allowing a source link to refer to that state even after the branch changes.

**Example 10. Pinning generated source links to the current commit.** The following
PowerShell sequence sets a temporary environment override for a build, then
removes that override:

```powershell
$env:CRS_DOCS_REF = git rev-parse HEAD
tmp/code-docs-venv/Scripts/python.exe -m mkdocs build --strict
Remove-Item Env:CRS_DOCS_REF
```

`git rev-parse HEAD` returns the current commit identifier. Assigning its output
to `$env:CRS_DOCS_REF` makes that identifier available to the link hook in the
MkDocs process. The middle line builds the library with those source-link targets.
`Remove-Item Env:CRS_DOCS_REF` removes the current shell's override, so subsequent
builds use the default unless another override is supplied. Here `Env:` is
PowerShell's environment-variable provider; this command does not remove a file.
If the shell already has a deliberate `CRS_DOCS_REF` value, that value must be
preserved and restored instead of treating this example as an automatic reset.

The preview prose always comes from the local working tree. A generated GitHub
link cannot display uncommitted source changes, so use raw Markdown/local source
when reviewing those changes. Missing local link targets fail the documentation
build even when the final HTML link would be external.

The hook handles this library's simple inline Markdown links. If adding reference
links, HTML links or more complex link syntax, extend its validation/conversion
accordingly. MkDocs' own strict checks cover links inside the library.

### How the documentation configuration connects

[mkdocs.yml](../../mkdocs.yml) divides documentation generation into several
responsibilities. `docs_dir` selects the Markdown inputs and `site_dir` selects
the generated output. `nav` defines reading order. The theme controls visual
presentation, while the search plugin builds a searchable index. The Markdown
extensions enable tables, linked headings, code highlighting and Mermaid fences.
`extra_css` adds local scrolling for wide diagrams.

The hook named in `hooks` runs `on_page_markdown()` from
[repository_links.py](../../tools/docs/repository_links.py) for each page. Its
operation is a sequence of transformations:

1. Resolve the documentation directory, repository root and current page directory.
   These paths determine how a relative link is interpreted.
2. Match simple inline Markdown links and split each URL into path, query and
   fragment components. External links and same-page anchors pass through.
3. Resolve a local target against the current page. A missing target or a path
   outside the repository raises `PluginError` and stops the build.
4. Leave in-library targets unchanged so MkDocs can build local navigation.
5. Convert other repository targets to GitHub URLs using `blob` for files or
   `tree` for directories, together with the selected revision and relative path.
6. Return the converted Markdown to MkDocs for normal page rendering.

This build-time adaptation keeps the editable Markdown useful in a checkout and
the generated HTML useful outside one. It changes rendered links, not the source
Markdown files or linked code. The regular-expression matcher supports the simple
inline-link convention used by this library; it is not a complete Markdown parser.

### Mermaid rendering

The Material theme initializes Mermaid when it finds a configured Mermaid fence;
the configuration follows its [official diagram guide](https://squidfunk.github.io/mkdocs-material/reference/diagrams/).
Diagrams render in the browser rather than becoming static images during
`mkdocs build`. The theme fetches its Mermaid runtime from a CDN, so diagram
rendering requires network access; the Markdown diagram sources remain local.

Use diagrams for call order, ownership and state transitions. Label conceptual
dependency diagrams as such: a dependency arrow is not proof of one direct
function call. Keep diagrams small enough to read on a narrow screen.

## Publication and shared manual sources

The [Pages workflow](../../.github/workflows/deploy-candu-playtest.yml) builds
documentation in its `documentation` job and stores a validated artifact. After
building the game, `pages-build` places that artifact in `dist-pages/docs`. The
normal Pages upload therefore publishes both the game at `/CRS/` and the library
at `/CRS/docs/`. `CRS_DOCS_REF` identifies the same commit used by the game and is
recorded in documentation build metadata. Documentation smoke checks run before
and after deployment.

The seven files under [manual](manual/index.md) are the canonical player-manual
chapters. [manualContent.ts](../../web/candu-playtest/src/manualContent.ts) imports
them as raw strings through Vite and uses Marked to render trusted repository
content for the F1 dialog. The dialog's existing heading is retained, so imported
document headings shift down a level. No network fetch is required to read the
in-game manual. [Reactor theory](theory/index.md) provides the longer explanation
in the website navigation without duplicating simulation logic.

For local combined-site verification, the existing build command can receive
`--site-dir web/candu-playtest/dist-pages/docs` after the game build. That option
changes only the documentation output location. A subsequent frontend build clears
`dist-pages`, so the documentation build must follow it. The repository's
`python tools/docs/verify_site.py` checks generated routes, links and anchors;
`npm run smoke:docs` checks the served site and shared manual in the browser.

## Tool assessment

| Option | Useful for this repository | Limitations / choice |
| --- | --- | --- |
| Markdown + Mermaid | Reviewable execution narrative beside code, stable file links and diagrams | Selected source format; needs a Mermaid-capable viewer for rendered diagrams |
| MkDocs | Navigation and searchable HTML from the same Markdown pages | Published with the game and available locally; authored prose still needs maintenance |
| Doxygen | A supplementary C# symbol/member reference extracted from managed source comments | Does not explain the complete TS → worker → JSON → C# transaction by itself |
| PDF export | A fixed document for printing or offline distribution | Pagination and diagram rendering need separate visual verification; not generated by this Markdown deliverable |

MkDocs builds Markdown and allows explicit navigation, as described in its
[writing guide](https://www.mkdocs.org/user-guide/writing-your-docs/).
Configuration details are in the [official configuration reference](https://www.mkdocs.org/user-guide/configuration/).
The narrative and source atlas are authored documentation rather than generated
API output. The selected Material theme is optional; its published
[maintenance schedule](https://squidfunk.github.io/mkdocs-material/changelog/)
announces end of life on 5 May 2027, so revisit the theme before depending on it
for long-term hosted documentation. Plain Markdown remains independent of it.

Doxygen lists C# among its built-in languages, while TypeScript is not listed
as a built-in parser in the [getting-started guide](https://www.doxygen.nl/manual/starting.html).
It also accepts [Markdown pages](https://www.doxygen.nl/manual/markdown.html).
An API extraction pass could supplement this walkthrough, but would not replace
the manually traced ownership, command flow and failure semantics.

If adding Doxygen later, start with managed `src` directories, exclude generated
`bin`/`obj`, and audit output for current C# syntax and existing XML comments.
`doxygen -g` creates a starter configuration. `EXTRACT_ALL = YES` is useful for
an initial inventory but suppresses missing-documentation warnings, so it is not
evidence that member documentation is complete. No Doxygen configuration or
generated API output is claimed as part of this library.

## Keeping the trace accurate

When changing a boundary, update the matching chapter and diagram in the same
change. Preserve these documentation checks:

1. Introduce every fenced block with its purpose, its notation or language, and
   any required working directory or preconditions. Explain its fields, symbols,
   commands or diagram paths immediately afterward, including the expected result.
2. Open the named entry function and follow its actual caller/callee chain.
3. Keep public daily behavior separate from retained real-time comparison code.
4. State units and array/group ordering at numerical boundaries.
5. Describe validation rejection, numerical rollback, physical loss and unknown
   transport outcomes separately.
6. Link to the existing detailed physics/specification page instead of copying
   a second full set of equations or calibration tables.
7. Run `mkdocs build --strict`, then inspect rendered diagrams and navigation
   when changing their syntax or presentation configuration.
8. Inspect the final diff and keep generated site/environment files out of source.

Use the [source atlas](08-source-atlas.md) to find the responsible module and the
[development guide](07-development.md) to choose behavior checks. A documentation
build validates structure and links, not numerical correctness of the application.
