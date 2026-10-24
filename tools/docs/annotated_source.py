"""Render verified source excerpts beside their explanations in MkDocs.

An annotated-source fence declares a repository file, unique start/end lines,
and consecutive explanation groups ending at exact source lines. Missing anchors
or uncovered lines stop the build instead of publishing a misleading excerpt.
"""

from html import escape
import os
from pathlib import Path
import re
import textwrap
from urllib.parse import quote

import markdown as md
from mkdocs.exceptions import PluginError
from pygments import highlight
from pygments.formatters import HtmlFormatter
from pygments.lexers import get_lexer_by_name
import yaml


FENCE = re.compile(r"^```annotated-source\s*\n(.*?)^```\s*$", re.M | re.S)


def find_anchor(lines, anchor, first=0, last=None):
    wanted = [line.strip() for line in str(anchor).strip().splitlines()]
    last = len(lines) if last is None else last
    return [(i, i + len(wanted) - 1) for i in range(first, last - len(wanted) + 1)
            if [line.strip() for line in lines[i:i + len(wanted)]] == wanted]


def render_excerpt(spec, config):
    repo_root = Path(config["config_file_path"]).resolve().parent
    path = (repo_root / spec["path"]).resolve()
    if not path.is_relative_to(repo_root) or not path.is_file():
        raise ValueError("source path must name a file within the repository")
    lines = path.read_text(encoding="utf-8-sig").splitlines()
    start = find_anchor(lines, spec["start"])
    if len(start) != 1:
        raise ValueError(f"start anchor must occur once, found {len(start)}: {spec['start']}")
    first = start[0][0]
    ends = find_anchor(lines, spec["end"], first)
    if not ends:
        raise ValueError(f"end anchor missing: {spec['end']}")
    last = ends[0][1]
    excerpt = textwrap.dedent("\n".join(lines[first:last + 1]))
    formatted = highlight(excerpt, get_lexer_by_name(spec["language"]), HtmlFormatter(nowrap=True)).splitlines()
    if len(formatted) != last - first + 1:
        raise ValueError("highlighted source line count changed")
    revision = quote(os.environ.get("CRS_DOCS_REF", "main"), safe="")
    source_path = path.relative_to(repo_root).as_posix()
    source_url = f"{config['repo_url'].rstrip('/')}/blob/{revision}/{quote(source_path, safe='/')}"
    title = escape(spec["title"])
    parts = [f'<section class="annotated-code" aria-label="{title}">',
             f'<div class="annotated-heading"><strong>{title}</strong>',
             f'<a href="{source_url}#L{first + 1}-L{last + 1}">Open full source: {escape(source_path)} · L{first + 1}–{last + 1}</a></div>',
             '<div class="annotated-columns" aria-hidden="true"><span>Source code</span><span>Explanation</span></div>']
    cursor = first
    for step in spec["steps"]:
        matches = find_anchor(lines, step["through"], cursor, last + 1)
        if not matches:
            raise ValueError(f"explanation end anchor missing/in wrong order: {step['through']}")
        end = matches[0][1]
        explanation = str(step["explanation"]).strip()
        if not explanation:
            raise ValueError("every source group needs an explanation")
        code = []
        for i in range(cursor, end + 1):
            code.append(f'<span class="source-line"><a class="source-number" href="{source_url}#L{i + 1}" aria-label="Source line {i + 1}">{i + 1}</a><span>{formatted[i - first] or " "}</span></span>')
        parts.extend([
            '<div class="annotated-step">',
            f'<div class="annotated-source highlight"><pre><code class="language-{escape(spec["language"])}">{"".join(code)}</code></pre></div>',
            f'<div class="annotated-explanation"><p class="source-range">Lines {cursor + 1}–{end + 1}</p>{md.markdown(explanation)}</div>',
            '</div>',
        ])
        cursor = end + 1
    if cursor != last + 1:
        raise ValueError(f"source lines {cursor + 1}–{last + 1} have no explanation")
    parts.append('</section>')
    return "\n".join(parts)


def on_page_markdown(markdown, *, page, config, files):
    def replace(match):
        try:
            return render_excerpt(yaml.safe_load(match[1]), config)
        except Exception as error:
            raise PluginError(f"{page.file.src_uri}: invalid annotated source: {error}") from error

    return FENCE.sub(replace, markdown)
