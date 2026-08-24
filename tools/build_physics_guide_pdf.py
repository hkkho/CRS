from __future__ import annotations

import hashlib
import re
from pathlib import Path
from xml.sax.saxutils import escape

from reportlab.lib import colors
from reportlab.lib.enums import TA_CENTER, TA_LEFT
from reportlab.lib.pagesizes import letter
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import inch
from reportlab.platypus import (
    BaseDocTemplate,
    Flowable,
    Frame,
    KeepTogether,
    ListFlowable,
    ListItem,
    PageBreak,
    PageTemplate,
    Paragraph,
    Preformatted,
    Spacer,
    Table,
    TableStyle,
)


REPO_ROOT = Path(__file__).resolve().parents[1]
SOURCE = REPO_ROOT / "docs" / "physics" / "candu-nuclear-diffusion-student-guide.md"
OUTPUT = REPO_ROOT / "output" / "pdf" / "candu-nuclear-diffusion-student-guide.pdf"

PAGE_WIDTH, PAGE_HEIGHT = letter
LEFT = 0.68 * inch
RIGHT = 0.68 * inch
TOP = 0.72 * inch
BOTTOM = 0.62 * inch
CONTENT_WIDTH = PAGE_WIDTH - LEFT - RIGHT


def metadata_value(source_text: str, label: str) -> str:
    pattern = rf"^\*\*{re.escape(label)}:\*\*\s*(.+?)\s*$"
    match = re.search(pattern, source_text, re.MULTILINE)
    if not match:
        raise SystemExit(f"Guide metadata is missing: {label}")
    return match.group(1)


def inline_markup(text: str) -> str:
    """Convert the small Markdown inline subset used by the guide to XML."""

    text = re.sub(r"\[([^\]]+)\]\([^)]*\)", r"\1", text)
    text = escape(text)
    text = re.sub(r"`([^`]+)`", r'<font name="Courier">\1</font>', text)
    text = re.sub(r"\*\*([^*]+)\*\*", r"<b>\1</b>", text)
    text = re.sub(r"\*([^*]+)\*", r"<i>\1</i>", text)
    return text


class GuideDocTemplate(BaseDocTemplate):
    def __init__(self, filename: str, **kwargs):
        super().__init__(filename, **kwargs)
        frame = Frame(
            LEFT,
            BOTTOM,
            CONTENT_WIDTH,
            PAGE_HEIGHT - TOP - BOTTOM,
            id="normal",
            leftPadding=0,
            rightPadding=0,
            topPadding=0,
            bottomPadding=0,
        )
        self.addPageTemplates([PageTemplate(id="guide", frames=[frame], onPage=draw_page)])


def draw_page(canvas, document):
    page = canvas.getPageNumber()
    canvas.saveState()
    if page > 1:
        canvas.setStrokeColor(colors.HexColor("#D7DEE8"))
        canvas.setLineWidth(0.5)
        canvas.line(LEFT, PAGE_HEIGHT - 0.48 * inch, PAGE_WIDTH - RIGHT, PAGE_HEIGHT - 0.48 * inch)
        canvas.setFont("Helvetica", 7.5)
        canvas.setFillColor(colors.HexColor("#64748B"))
        canvas.drawString(LEFT, PAGE_HEIGHT - 0.36 * inch, "Nuclear Diffusion Theory in a CANDU Reactor")
    canvas.setStrokeColor(colors.HexColor("#D7DEE8"))
    canvas.setLineWidth(0.5)
    canvas.line(LEFT, 0.42 * inch, PAGE_WIDTH - RIGHT, 0.42 * inch)
    canvas.setFont("Helvetica", 7.5)
    canvas.setFillColor(colors.HexColor("#64748B"))
    canvas.drawString(LEFT, 0.25 * inch, "Living guide - update with the project specifications and implementation status")
    canvas.drawRightString(PAGE_WIDTH - RIGHT, 0.25 * inch, f"Page {page}")
    canvas.restoreState()


def table_widths(column_count: int):
    presets = {
        2: [0.27, 0.73],
        3: [0.22, 0.30, 0.48],
        4: [0.18, 0.24, 0.28, 0.30],
        5: [0.13, 0.16, 0.23, 0.19, 0.29],
        6: [0.12, 0.14, 0.18, 0.18, 0.18, 0.20],
    }
    fractions = presets.get(column_count, [1 / column_count] * column_count)
    return [CONTENT_WIDTH * value for value in fractions]


def make_table(lines, styles):
    rows = []
    for line in lines:
        cells = [cell.strip() for cell in line.strip().strip("|").split("|")]
        if all(re.fullmatch(r":?-{3,}:?", cell.replace(" ", "")) for cell in cells):
            continue
        rows.append(cells)
    if not rows:
        return Spacer(1, 0)
    width = max(len(row) for row in rows)
    rows = [row + [""] * (width - len(row)) for row in rows]
    header_style = styles["TableHeader"]
    cell_style = styles["TableCell"]
    flow_rows = []
    for row_index, row in enumerate(rows):
        cell_style_to_use = header_style if row_index == 0 else cell_style
        flow_rows.append([Paragraph(inline_markup(cell), cell_style_to_use) for cell in row])
    table = Table(flow_rows, colWidths=table_widths(width), repeatRows=1, hAlign="LEFT")
    table.setStyle(
        TableStyle(
            [
                ("BACKGROUND", (0, 0), (-1, 0), colors.HexColor("#E8F0FA")),
                ("TEXTCOLOR", (0, 0), (-1, 0), colors.HexColor("#16324F")),
                ("GRID", (0, 0), (-1, -1), 0.35, colors.HexColor("#B7C3D0")),
                ("VALIGN", (0, 0), (-1, -1), "TOP"),
                ("LEFTPADDING", (0, 0), (-1, -1), 5),
                ("RIGHTPADDING", (0, 0), (-1, -1), 5),
                ("TOPPADDING", (0, 0), (-1, -1), 4),
                ("BOTTOMPADDING", (0, 0), (-1, -1), 4),
                ("ROWBACKGROUNDS", (0, 1), (-1, -1), [colors.white, colors.HexColor("#F7FAFC")]),
            ]
        )
    )
    table.spaceAfter = 0.12 * inch
    return table


def make_styles():
    base = getSampleStyleSheet()
    styles = {
        "Title": ParagraphStyle(
            "GuideTitle",
            parent=base["Title"],
            fontName="Helvetica-Bold",
            fontSize=25,
            leading=30,
            alignment=TA_CENTER,
            textColor=colors.HexColor("#12304A"),
            spaceAfter=16,
        ),
        "Subtitle": ParagraphStyle(
            "GuideSubtitle",
            parent=base["Normal"],
            fontName="Helvetica",
            fontSize=13,
            leading=18,
            alignment=TA_CENTER,
            textColor=colors.HexColor("#476277"),
            spaceAfter=18,
        ),
        "H1": ParagraphStyle(
            "GuideH1",
            parent=base["Heading1"],
            fontName="Helvetica-Bold",
            fontSize=18,
            leading=22,
            textColor=colors.HexColor("#12304A"),
            spaceBefore=16,
            spaceAfter=8,
            keepWithNext=True,
        ),
        "H2": ParagraphStyle(
            "GuideH2",
            parent=base["Heading2"],
            fontName="Helvetica-Bold",
            fontSize=13.5,
            leading=17,
            textColor=colors.HexColor("#1D5B7A"),
            spaceBefore=12,
            spaceAfter=5,
            keepWithNext=True,
        ),
        "H3": ParagraphStyle(
            "GuideH3",
            parent=base["Heading3"],
            fontName="Helvetica-Bold",
            fontSize=10.8,
            leading=14,
            textColor=colors.HexColor("#2D6A83"),
            spaceBefore=9,
            spaceAfter=4,
            keepWithNext=True,
        ),
        "Body": ParagraphStyle(
            "GuideBody",
            parent=base["BodyText"],
            fontName="Helvetica",
            fontSize=9.2,
            leading=13.3,
            textColor=colors.HexColor("#1F2933"),
            spaceAfter=6,
            alignment=TA_LEFT,
            splitLongWords=True,
        ),
        "Bullet": ParagraphStyle(
            "GuideBullet",
            parent=base["BodyText"],
            fontName="Helvetica",
            fontSize=9.1,
            leading=12.6,
            textColor=colors.HexColor("#1F2933"),
            spaceAfter=2,
            leftIndent=5,
            splitLongWords=True,
        ),
        "Code": ParagraphStyle(
            "GuideCode",
            parent=base["Code"],
            fontName="Courier",
            fontSize=7.6,
            leading=9.1,
            leftIndent=7,
            rightIndent=7,
            borderColor=colors.HexColor("#CBD5E1"),
            borderWidth=0.5,
            borderPadding=6,
            backColor=colors.HexColor("#F5F7FA"),
            spaceBefore=4,
            spaceAfter=8,
        ),
        "TableHeader": ParagraphStyle(
            "GuideTableHeader",
            parent=base["BodyText"],
            fontName="Helvetica-Bold",
            fontSize=7.3,
            leading=8.7,
            textColor=colors.HexColor("#16324F"),
            splitLongWords=True,
        ),
        "TableCell": ParagraphStyle(
            "GuideTableCell",
            parent=base["BodyText"],
            fontName="Helvetica",
            fontSize=7.1,
            leading=8.45,
            textColor=colors.HexColor("#1F2933"),
            splitLongWords=True,
        ),
        "Meta": ParagraphStyle(
            "GuideMeta",
            parent=base["BodyText"],
            fontName="Helvetica",
            fontSize=9.2,
            leading=13,
            alignment=TA_CENTER,
            textColor=colors.HexColor("#526779"),
            spaceAfter=4,
        ),
    }
    return styles


def parse_markdown(text: str, styles):
    lines = text.splitlines()
    story = []
    i = 0
    first_title = False
    while i < len(lines):
        line = lines[i].rstrip()
        if not line:
            i += 1
            continue
        if line.startswith("```"):
            code = []
            i += 1
            while i < len(lines) and not lines[i].startswith("```"):
                code.append(lines[i].rstrip("\n"))
                i += 1
            if i < len(lines):
                i += 1
            story.append(Preformatted("\n".join(code), styles["Code"]))
            continue
        if line.startswith("|"):
            table_lines = []
            while i < len(lines) and lines[i].strip().startswith("|"):
                table_lines.append(lines[i].strip())
                i += 1
            story.append(make_table(table_lines, styles))
            continue
        heading = re.match(r"^(#{1,3})\s+(.+)$", line)
        if heading:
            level = len(heading.group(1))
            heading_text = inline_markup(heading.group(2))
            if level == 1 and first_title:
                story.append(Spacer(1, 0.75 * inch))
                story.append(Paragraph(heading_text, styles["Title"]))
                first_title = False
                story.append(Spacer(1, 0.05 * inch))
            else:
                story.append(Paragraph(heading_text, styles[f"H{level}"]))
            i += 1
            continue
        bullet_match = re.match(r"^[-*]\s+(.+)$", line)
        if bullet_match:
            while i < len(lines):
                match = re.match(r"^[-*]\s+(.+)$", lines[i].strip())
                if not match:
                    break
                item_text = [match.group(1)]
                i += 1
                while i < len(lines) and lines[i].strip() and (lines[i].startswith("  ") or lines[i].startswith("\t")):
                    item_text.append(lines[i].strip())
                    i += 1
                story.append(Paragraph("- " + inline_markup(" ".join(item_text)), styles["Bullet"]))
            story.append(Spacer(1, 0.04 * inch))
            continue
        numbered_match = re.match(r"^\d+\.\s+(.+)$", line)
        if numbered_match:
            while i < len(lines):
                match = re.match(r"^\d+\.\s+(.+)$", lines[i].strip())
                if not match:
                    break
                item_number = lines[i].strip().split(".", 1)[0]
                item_text = [match.group(1)]
                i += 1
                while i < len(lines) and lines[i].strip() and (lines[i].startswith("  ") or lines[i].startswith("\t")):
                    item_text.append(lines[i].strip())
                    i += 1
                story.append(Paragraph(f"{item_number}. " + inline_markup(" ".join(item_text)), styles["Bullet"]))
            story.append(Spacer(1, 0.04 * inch))
            continue
        paragraph_lines = [line]
        i += 1
        while i < len(lines):
            next_line = lines[i].rstrip()
            if (
                not next_line
                or next_line.startswith("```")
                or next_line.startswith("|")
                or re.match(r"^#{1,3}\s+", next_line)
                or re.match(r"^[-*]\s+", next_line)
                or re.match(r"^\d+\.\s+", next_line)
            ):
                break
            paragraph_lines.append(next_line)
            i += 1
        paragraph = " ".join(part.strip() for part in paragraph_lines)
        if paragraph.startswith("**Guide version:") or paragraph.startswith("**Last checked:") or paragraph.startswith("**Status:"):
            story.append(Paragraph(inline_markup(paragraph), styles["Meta"]))
        elif first_title:
            story.append(Paragraph(inline_markup(paragraph), styles["Subtitle"]))
        else:
            story.append(Paragraph(inline_markup(paragraph), styles["Body"]))
    return story


def main():
    if not SOURCE.exists():
        raise SystemExit(f"Missing Markdown source: {SOURCE}")
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    source_bytes = SOURCE.read_bytes()
    source_hash = hashlib.sha256(source_bytes).hexdigest()
    source_text = SOURCE.read_text(encoding="utf-8")
    guide_version = metadata_value(source_text, "Guide version")
    last_checked = metadata_value(source_text, "Last checked")
    styles = make_styles()
    doc = GuideDocTemplate(
        str(OUTPUT),
        pagesize=letter,
        leftMargin=LEFT,
        rightMargin=RIGHT,
        topMargin=TOP,
        bottomMargin=BOTTOM,
        title="Nuclear Diffusion Theory in a CANDU Reactor",
        author="ReactorSim project",
        subject="Student guide and living physics status",
        keywords="CANDU, neutron diffusion, two-group, burnup, xenon",
    )
    body_marker = "## The short project answer"
    if body_marker not in source_text:
        raise SystemExit(f"Guide body marker is missing: {body_marker}")
    body_text = body_marker + source_text.split(body_marker, 1)[1]
    cover_status = Table(
        [[
            Paragraph("SOURCE STATUS", styles["TableHeader"]),
            Paragraph(
                "This guide records bounded G4-R6/G5 PASS and G6 CONDITIONAL PASS evidence. The latest direct pinned headless run passed Core 158/158 and Golden 19/19; the Phase 6 focused run passed 46/46. It is not a direct external/full-core reactor simulator, and no applicable RRS comparison package or production authority has been admitted.",
                styles["TableCell"],
            ),
        ]],
        colWidths=[1.2 * inch, CONTENT_WIDTH - 1.2 * inch],
        style=TableStyle(
            [
                ("BACKGROUND", (0, 0), (-1, -1), colors.HexColor("#FFF7E6")),
                ("BOX", (0, 0), (-1, -1), 0.6, colors.HexColor("#D6A84F")),
                ("VALIGN", (0, 0), (-1, -1), "TOP"),
                ("LEFTPADDING", (0, 0), (-1, -1), 7),
                ("RIGHTPADDING", (0, 0), (-1, -1), 7),
                ("TOPPADDING", (0, 0), (-1, -1), 6),
                ("BOTTOMPADDING", (0, 0), (-1, -1), 6),
            ]
        ),
    )
    cover = [
        Spacer(1, 0.82 * inch),
        Paragraph("Nuclear Diffusion Theory in a CANDU Reactor", styles["Title"]),
        Paragraph("A first-year university guide to the physics blueprint of the CANDU refuelling game", styles["Subtitle"]),
        cover_status,
        Spacer(1, 0.24 * inch),
        Paragraph(f"<b>Guide version:</b> {escape(guide_version)}", styles["Meta"]),
        Paragraph(f"<b>Last checked:</b> {escape(last_checked)}", styles["Meta"]),
        Paragraph("<b>Status:</b> Living documentation; update when specifications, implementation, validation, or game rules change.", styles["Meta"]),
        PageBreak(),
    ]
    story = cover + parse_markdown(body_text, styles)
    doc.build(story)
    print(f"PHYSICS_GUIDE_PDF_PASS output={OUTPUT} source_sha256={source_hash}")


if __name__ == "__main__":
    main()
