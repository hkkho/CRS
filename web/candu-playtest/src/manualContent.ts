import { marked } from "marked";
import start from "../../../docs/code-library/manual/start.md?raw";
import fuel from "../../../docs/code-library/manual/fuel.md?raw";
import physics from "../../../docs/code-library/manual/physics.md?raw";
import score from "../../../docs/code-library/manual/score.md?raw";

// These are trusted, repository-authored Markdown inputs bundled by Vite.
// No player input or fetched document is passed to the HTML parser.
const sources = [
  ["start", "Quick start", start],
  ["fuel", "Fuelling", fuel],
  ["physics", "Maps & readings", physics],
  ["score", "Limits & score", score],
] as const;

/** The F1 dialog and the published MkDocs manual share these Markdown files. */
export const manualChapters = sources.map(([id, title, source]) => [
  id,
  title,
  // The dialog already has an h1. Shift document headings down one level.
  marked.parse(source, { async: false }).replace(
    /<(\/?)(h[1-5])(?=[ >])/g,
    (_match, closing: string, heading: string) => `<${closing}h${Number(heading[1]) + 1}`,
  ),
] as const);
