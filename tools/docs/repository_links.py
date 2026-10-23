"""Validate simple inline local links and adapt source links for standalone HTML.

Markdown keeps checkout-relative links. Only links outside the library are
rewritten, using the repository URL and CRS_DOCS_REF (main by default).
"""

import os
import json
from pathlib import Path
import re
from urllib.parse import quote, unquote, urlsplit, urlunsplit

from mkdocs.exceptions import PluginError


INLINE_LINK = re.compile(r"(?P<start>!?\[[^\]\n]*\]\()(?P<url>[^\s)]+)(?P<end>\))")


def on_page_markdown(markdown, *, page, config, files):
    docs_root = Path(config["docs_dir"]).resolve()
    repo_root = Path(config["config_file_path"]).resolve().parent
    page_dir = Path(page.file.abs_src_path).parent
    repository = config["repo_url"].rstrip("/")
    revision = quote(os.environ.get("CRS_DOCS_REF", "main"), safe="")

    def adapt(match):
        url = urlsplit(match["url"])
        if url.scheme or url.netloc or not url.path:
            return match[0]
        target = (page_dir / unquote(url.path)).resolve()
        if not target.is_relative_to(repo_root) or not target.exists():
            raise PluginError(f"{page.file.src_uri}: missing/invalid local link {match['url']}")
        if target.is_relative_to(docs_root):
            return match[0]
        kind = "tree" if target.is_dir() else "blob"
        path = quote(target.relative_to(repo_root).as_posix(), safe="/")
        converted = urlunsplit(("", "", f"{repository}/{kind}/{revision}/{path}", url.query, url.fragment))
        return f"{match['start']}{converted}{match['end']}"

    return INLINE_LINK.sub(adapt, markdown)


def on_post_build(*, config):
    """Publish the source revision so acceptance can verify the combined release."""
    target = Path(config["site_dir"]) / "build-info.json"
    target.write_text(json.dumps({
        "sourceRevision": os.environ.get("CRS_DOCS_REF", "main"),
        "documentation": "crs-manual-theory-code",
    }) + "\n", encoding="utf-8")
