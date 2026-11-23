# Documentation guide

Write for the current repository and the reader's next task. Use present tense, direct sentences, consistent terms and descriptive links. Explain an abbreviation when it first appears. Keep a single authoritative explanation of each behavior and link to it from summaries.

## Page types

- **Player guide:** state the goal, prerequisites, actions, visible results and recovery options. Use the labels shown in the interface.
- **Developer guide:** state the component's responsibility, entry points, data flow and relevant source links. Give commands with their working directory, prerequisites and expected result.
- **Theory and calibration:** define the model, assumptions, quantities, units and group ordering; explain equations and numerical methods; distinguish published evidence from project-authored approximations; cite sources and link to the implementing code and reproducible data.
- **Protocol and formats:** identify the applicable version, message structure, field types, units, ordering, validation, failure behavior and compatibility requirements. Treat these details as a contract.
- **Benchmark record:** identify the measured configuration, inputs, method, outputs and limitations. Keep dates, version identifiers and comparisons when necessary to interpret or reproduce evidence; do not present a historical measurement as current behavior.

Open each page with its purpose and scope. Use headings that help readers find a task or concept. Include examples when they clarify a contract, and link to the files that establish values or behavior. Mark proposed work as planned, not implemented. Avoid release diaries, unexplained comparisons with earlier designs and claims that exceed the available evidence.

Place player pages in `docs/code-library/manual` and code walkthroughs in `docs/code-library`. Put introductory theory in `docs/code-library/theory`, detailed physics and calibration in `docs/physics`, and protocol specifications in `docs/spec`. Put deployment procedures in `docs/maintenance` and measurement records beside their benchmark data. Add reader-facing pages to the appropriate index and, when they belong to the published code library, to `mkdocs.yml`.

Begin each page with purpose and scope, then present the topics listed for its page type in that order. Omit sections that do not apply. Label proposed work with a **Planned:** prefix so it cannot be mistaken for a current feature.

## Verification

Check claims against the current source, tests, configuration and embedded data. Preserve equations, units, numerical provenance, reproducibility and intentional compatibility behavior. Check links and run the documentation build and relevant repository checks for the changed material. If a command or claim cannot be verified, state the limitation.

Run the documentation tests, strict build and site verification from the repository root using the commands in [Build the documentation](hosting.md#build-the-documentation). Successful commands exit without errors; the strict build also reports broken documentation links.

## Writing references

- [DRAGON5 and DONJON5 Version5 guides](https://merlin.polymtl.ca/version5.htm) separate user guides, object structures and theory.
- [OpenMC documentation](https://docs.openmc.org/en/stable/) separates user, developer, theory and file-format material.
- [OpenMC beginner's guide](https://docs.openmc.org/en/stable/usersguide/beginners.html) introduces the model and reader prerequisites before detailed procedures.
- [OpenMC developer's guide](https://docs.openmc.org/en/stable/devguide/index.html) groups contribution, testing and documentation workflows.
