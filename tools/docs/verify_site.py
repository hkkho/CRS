"""Check the generated documentation's required routes and local links/anchors."""

import argparse
from html.parser import HTMLParser
from pathlib import Path
from urllib.parse import unquote, urlsplit


class Page(HTMLParser):
    def __init__(self, source):
        super().__init__()
        self.links = []
        self.ids = set()
        self.feed(source)

    def handle_starttag(self, tag, attrs):
        values = dict(attrs)
        if "id" in values:
            self.ids.add(values["id"])
        if tag == "a" and values.get("href"):
            self.links.append(values["href"])


def verify(site):
    site = site.resolve()
    required = ("index.html", "manual/index.html", "manual/start/index.html",
                "theory/index.html", "theory/numerical-model/index.html",
                "01-startup/index.html", "search/search_index.json", "build-info.json")
    for route in required:
        if not (site / route).is_file():
            raise ValueError(f"Missing documentation route: {route}")
    pages = {path: Page(path.read_text(encoding="utf-8")) for path in site.rglob("*.html")}
    checked = 0
    for path, page in pages.items():
        for href in page.links:
            url = urlsplit(href)
            if url.scheme or url.netloc:
                continue
            decoded = unquote(url.path)
            # MkDocs' 404 page uses the configured project's absolute URL prefix.
            if decoded.startswith("/CRS/docs/"):
                target = (site / decoded.removeprefix("/CRS/docs/")).resolve()
            elif decoded.startswith("/"):
                continue  # A link outside this documentation subsite.
            else:
                target = (path.parent / decoded).resolve() if decoded else path
            if target.is_dir():
                target /= "index.html"
            if not target.is_relative_to(site) or not target.exists():
                raise ValueError(f"{path.relative_to(site)}: broken local link {href}")
            if url.fragment and target in pages and unquote(url.fragment) not in pages[target].ids:
                raise ValueError(f"{path.relative_to(site)}: missing anchor {href}")
            checked += 1
    print(f"Documentation verified: {len(pages)} HTML pages; {checked} local links/anchors.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--site-dir", type=Path, default=Path("artifacts/code-library"))
    verify(parser.parse_args().site_dir)
