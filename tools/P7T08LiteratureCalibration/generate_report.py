from __future__ import annotations

import hashlib
import json
import math
from pathlib import Path
from typing import Any, Iterable

from PIL import Image, ImageDraw, ImageFont
from reportlab.lib import colors
from reportlab.lib.enums import TA_CENTER, TA_LEFT
from reportlab.lib.pagesizes import letter
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import inch
from reportlab.pdfgen.canvas import Canvas
from reportlab.platypus import (
    Image as ReportImage,
    KeepTogether,
    PageBreak,
    Paragraph,
    SimpleDocTemplate,
    Spacer,
    Table,
    TableStyle,
)


ROOT = Path(__file__).resolve().parents[2]
APPROVED = ROOT / "data" / "golden" / "p7-t08-literature-calibrated-synthetic-v1.json"
APPROVED_MANIFEST = ROOT / "data" / "golden" / "p7-t08-literature-calibrated-synthetic-v1.manifest.json"
DEFINITION = ROOT / "data" / "comparisons" / "p7-t08-literature-calibrated-definition-v1.json"
DEFINITION_MANIFEST = ROOT / "reference" / "manifests" / "candu-literature-sources-v1.json"
TMP = ROOT / "tmp" / "pdfs" / "p7-t08"
OUT = ROOT / "output" / "pdf" / "p7-t08-literature-calibrated-golden-cases-v1.pdf"


NAVY = colors.HexColor("#16324F")
BLUE = colors.HexColor("#2563EB")
TEAL = colors.HexColor("#0F766E")
GOLD = colors.HexColor("#B7791F")
RED = colors.HexColor("#B42318")
INK = colors.HexColor("#1F2937")
MUTED = colors.HexColor("#52606D")
PALE = colors.HexColor("#F3F6F9")
GRID = colors.HexColor("#D9E2EC")


class DeterministicCanvas(Canvas):
    """Use ReportLab's invariant mode so rerendered evidence has stable bytes."""

    def __init__(self, *args: Any, **kwargs: Any) -> None:
        kwargs["invariant"] = 1
        super().__init__(*args, **kwargs)


def load_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8"))


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def font(size: int, bold: bool = False) -> ImageFont.FreeTypeFont | ImageFont.ImageFont:
    candidates = [
        Path("C:/Windows/Fonts/arialbd.ttf" if bold else "C:/Windows/Fonts/arial.ttf"),
        Path("C:/Windows/Fonts/segoeuib.ttf" if bold else "C:/Windows/Fonts/segoeui.ttf"),
    ]
    for candidate in candidates:
        if candidate.exists():
            return ImageFont.truetype(str(candidate), size)
    return ImageFont.load_default()


def text(draw: ImageDraw.ImageDraw, xy: tuple[int, int], value: str, size: int = 22, fill: tuple[int, int, int] = (31, 41, 55), bold: bool = False) -> None:
    draw.text(xy, value, font=font(size, bold), fill=fill)


def chart_base(title: str, y_label: str, width: int = 1200, height: int = 620) -> tuple[Image.Image, ImageDraw.ImageDraw, tuple[int, int, int, int]]:
    image = Image.new("RGB", (width, height), "white")
    draw = ImageDraw.Draw(image)
    text(draw, (55, 28), title, 28, (22, 50, 79), True)
    text(draw, (18, height // 2 - 40), y_label, 18, (82, 96, 109))
    return image, draw, (105, 90, width - 55, height - 72)


def nice_ticks(low: float, high: float, count: int = 5) -> list[float]:
    if math.isclose(low, high):
        return [low]
    raw = (high - low) / max(count, 1)
    magnitude = 10 ** math.floor(math.log10(abs(raw)))
    normalized = raw / magnitude
    step = 1 if normalized <= 1 else 2 if normalized <= 2 else 5 if normalized <= 5 else 10
    step *= magnitude
    start = math.floor(low / step) * step
    values: list[float] = []
    value = start
    while value <= high + step * 0.01:
        values.append(value)
        value += step
    return values


def fmt(value: float, digits: int = 3) -> str:
    formatted = f"{value:.{digits}f}"
    return formatted if digits == 0 else formatted.rstrip("0").rstrip(".")


def bar_chart(path: Path, title: str, labels: list[str], values: list[float], y_label: str, colors_: list[tuple[int, int, int]], baseline: float | None = None, suffix: str = "") -> None:
    image, draw, (left, top, right, bottom) = chart_base(title, y_label)
    margin = (max(values) - min(values)) * 0.18 if values else 1
    low = min(0 if baseline is not None else min(values), min(values) - margin)
    high = max(0 if baseline is not None else max(values), max(values) + margin)
    if math.isclose(low, high):
        low -= 1
        high += 1
    ticks = nice_ticks(low, high, 5)
    for tick in ticks:
        y = int(bottom - (tick - low) / (high - low) * (bottom - top))
        draw.line((left, y, right, y), fill=(224, 231, 239), width=1)
        text(draw, (10, y - 10), fmt(tick, 3), 17, (82, 96, 109))
    axis_y = int(bottom - (0 - low) / (high - low) * (bottom - top)) if low <= 0 <= high else bottom
    draw.line((left, top, left, bottom), fill=(82, 96, 109), width=2)
    draw.line((left, axis_y, right, axis_y), fill=(82, 96, 109), width=2)
    slot = (right - left) / max(len(values), 1)
    bar_width = slot * 0.58
    for index, (label, value) in enumerate(zip(labels, values)):
        x = left + slot * index + (slot - bar_width) / 2
        y_value = int(bottom - (value - low) / (high - low) * (bottom - top))
        y0 = min(axis_y, y_value)
        y1 = max(axis_y, y_value)
        draw.rectangle((int(x), y0, int(x + bar_width), max(y1, y0 + 2)), fill=colors_[index % len(colors_)])
        text(draw, (int(x + bar_width / 2 - 38), max(48, y0 - 32)), fmt(value, 4) + suffix, 17, (31, 41, 55), True)
        text(draw, (int(x + bar_width / 2 - 65), bottom + 18), label, 17, (82, 96, 109))
    image.save(path)


def line_chart(path: Path, title: str, xs: list[float], ys: list[float], x_label: str, y_label: str, target: float | None = None, target_label: str = "reported target") -> None:
    image, draw, (left, top, right, bottom) = chart_base(title, y_label)
    low = min(ys + ([target] if target is not None else []))
    high = max(ys + ([target] if target is not None else []))
    margin = max((high - low) * 0.12, 0.1)
    low -= margin
    high += margin
    x_low, x_high = min(xs), max(xs)
    for tick in nice_ticks(low, high, 5):
        y = int(bottom - (tick - low) / (high - low) * (bottom - top))
        draw.line((left, y, right, y), fill=(224, 231, 239), width=1)
        text(draw, (10, y - 10), fmt(tick, 3), 17, (82, 96, 109))
    draw.line((left, top, left, bottom), fill=(82, 96, 109), width=2)
    draw.line((left, bottom, right, bottom), fill=(82, 96, 109), width=2)
    if target is not None:
        y = int(bottom - (target - low) / (high - low) * (bottom - top))
        draw.line((left, y, right, y), fill=(183, 121, 31), width=3)
        text(draw, (right - 220, max(top, y - 26)), f"{target_label}: {fmt(target, 3)}", 17, (183, 121, 31), True)
    points: list[tuple[int, int]] = []
    for x, value in zip(xs, ys):
        px = int(left + (x - x_low) / (x_high - x_low) * (right - left))
        py = int(bottom - (value - low) / (high - low) * (bottom - top))
        points.append((px, py))
    draw.line(points, fill=(37, 99, 235), width=4)
    for px, py in points[:: max(1, len(points) // 10)]:
        draw.ellipse((px - 4, py - 4, px + 4, py + 4), fill=(37, 99, 235))
    for tick in nice_ticks(x_low, x_high, 6):
        x = int(left + (tick - x_low) / (x_high - x_low) * (right - left))
        draw.line((x, bottom, x, bottom + 8), fill=(82, 96, 109), width=1)
        text(draw, (x - 20, bottom + 16), fmt(tick, 0), 17, (82, 96, 109))
    text(draw, (right - 130, bottom + 48), x_label, 18, (82, 96, 109))
    image.save(path)


def interval_chart(path: Path, title: str, labels: list[str], values: list[float], minimums: list[float], maximums: list[float], x_label: str) -> None:
    image, draw, (left, top, right, bottom) = chart_base(title, x_label)
    low = min(minimums + values) - 0.2
    high = max(maximums + values) + 0.2
    for tick in nice_ticks(low, high, 5):
        x = int(left + (tick - low) / (high - low) * (right - left))
        draw.line((x, top, x, bottom), fill=(224, 231, 239), width=1)
        text(draw, (x - 20, bottom + 16), fmt(tick, 2), 17, (82, 96, 109))
    draw.line((left, bottom, right, bottom), fill=(82, 96, 109), width=2)
    row_height = (bottom - top) / max(len(labels), 1)
    for index, (label, value, minimum, maximum) in enumerate(zip(labels, values, minimums, maximums)):
        y = int(top + row_height * (index + 0.5))
        x0 = int(left + (minimum - low) / (high - low) * (right - left))
        x1 = int(left + (maximum - low) / (high - low) * (right - left))
        xv = int(left + (value - low) / (high - low) * (right - left))
        text(draw, (12, y - 12), label, 18, (31, 41, 55), True)
        draw.line((x0, y, x1, y), fill=(15, 118, 110), width=12)
        draw.ellipse((xv - 9, y - 9, xv + 9, y + 9), fill=(37, 99, 235))
        text(draw, (min(right - 130, xv + 16), y - 12), fmt(value, 2), 18, (37, 99, 235), True)
    image.save(path)


def horizontal_difference_chart(path: Path, title: str, labels: list[str], values: list[float], x_label: str) -> None:
    image, draw, (left, top, right, bottom) = chart_base(title, x_label)
    limit = max(abs(value) for value in values) * 1.2
    zero = int(left + (0 + limit) / (2 * limit) * (right - left))
    for tick in nice_ticks(-limit, limit, 5):
        x = int(left + (tick + limit) / (2 * limit) * (right - left))
        draw.line((x, top, x, bottom), fill=(224, 231, 239), width=1)
        text(draw, (x - 25, bottom + 16), fmt(tick, 1), 17, (82, 96, 109))
    draw.line((zero, top, zero, bottom), fill=(82, 96, 109), width=2)
    row_height = (bottom - top) / max(len(labels), 1)
    for index, (label, value) in enumerate(zip(labels, values)):
        y = int(top + row_height * (index + 0.5))
        xv = int(left + (value + limit) / (2 * limit) * (right - left))
        text(draw, (12, y - 12), label, 17, (31, 41, 55), True)
        draw.rectangle((min(zero, xv), y - 12, max(zero, xv), y + 12), fill=(183, 121, 31) if value >= 0 else (37, 99, 235))
        text(draw, (min(right - 100, max(left + 4, xv + 10 if value >= 0 else xv - 74)), y - 12), fmt(value, 1) + "%", 17, (31, 41, 55), True)
    image.save(path)


def p(text_value: str, style: ParagraphStyle) -> Paragraph:
    return Paragraph(text_value, style)


def table(data: list[list[Any]], widths: list[float], header: bool = True, small: bool = False) -> Table:
    normalized = [list(row) for row in data]
    if header and normalized:
        header_style = ParagraphStyle(
            "TableHeader",
            fontName="Helvetica-Bold",
            fontSize=7.5 if small else 8.5,
            leading=9 if small else 10,
            textColor=colors.white,
        )
        normalized[0] = [
            Paragraph(cell.getPlainText() if isinstance(cell, Paragraph) else str(cell), header_style)
            for cell in normalized[0]
        ]
    style = TableStyle([
        ("BACKGROUND", (0, 0), (-1, 0), NAVY if header else PALE),
        ("TEXTCOLOR", (0, 0), (-1, 0), colors.white if header else INK),
        ("FONTNAME", (0, 0), (-1, 0), "Helvetica-Bold" if header else "Helvetica"),
        ("FONTSIZE", (0, 0), (-1, -1), 7.5 if small else 8.5),
        ("LEADING", (0, 0), (-1, -1), 9 if small else 10),
        ("GRID", (0, 0), (-1, -1), 0.35, GRID),
        ("VALIGN", (0, 0), (-1, -1), "TOP"),
        ("LEFTPADDING", (0, 0), (-1, -1), 5),
        ("RIGHTPADDING", (0, 0), (-1, -1), 5),
        ("TOPPADDING", (0, 0), (-1, -1), 4),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 4),
    ])
    for row in range(1 if header else 0, len(data)):
        if row % 2 == 0:
            style.add("BACKGROUND", (0, row), (-1, row), PALE)
    result = Table(normalized, colWidths=widths, repeatRows=1 if header else 0, hAlign="LEFT")
    result.setStyle(style)
    return result


def fmt_cell(value: Any) -> str:
    if isinstance(value, float):
        return fmt(value, 5)
    if isinstance(value, int):
        return f"{value:,}"
    return str(value)


def page_footer(canvas: Any, document: Any) -> None:
    canvas.saveState()
    canvas.setStrokeColor(GRID)
    canvas.line(0.6 * inch, 0.48 * inch, 7.9 * inch, 0.48 * inch)
    canvas.setFont("Helvetica", 7.5)
    canvas.setFillColor(MUTED)
    canvas.drawString(0.6 * inch, 0.30 * inch, "P7-T08 | Synthetic literature-calibrated cases | source reproduction not claimed")
    canvas.drawRightString(7.9 * inch, 0.30 * inch, f"Page {document.page}")
    canvas.restoreState()


def source_link(url: str) -> str:
    return f'<link href="{url}" color="#2563EB">{url}</link>'


def main() -> None:
    artifact = load_json(APPROVED)
    manifest = load_json(APPROVED_MANIFEST)
    definition = load_json(DEFINITION)
    if sha256(APPROVED) != manifest["artifact_sha256"]:
        raise RuntimeError("approved artifact hash does not match its manifest")
    if APPROVED.stat().st_size != manifest["artifact_byte_length"]:
        raise RuntimeError("approved artifact byte length does not match its manifest")
    if sha256(DEFINITION) != manifest["definition_sha256"]:
        raise RuntimeError("definition hash does not match the approved manifest")
    if sha256(DEFINITION_MANIFEST) != manifest["source_manifest_sha256"]:
        raise RuntimeError("source manifest hash does not match the approved manifest")
    TMP.mkdir(parents=True, exist_ok=True)
    OUT.parent.mkdir(parents=True, exist_ok=True)

    cases = {case["case_id"]: case for case in artifact["cases"]}
    a = cases["p7-t08-s4-lattice-37-bundle-kcross-v1"]
    b = cases["p7-t08-s5-lattice-depletion-300d-v1"]
    c = cases["p7-t08-s1-fullcore-atf-summary-v1"]
    d = cases["p7-t08-s4-fullcore-refueling-summary-v1"]

    a_chart = TMP / "s4-lattice-k.png"
    a_values = [a["input_snapshot"]["reported_dragon_k_effective"], a["outputs"]["synthetic_k_effective"], a["input_snapshot"]["reported_serpent_k_effective"]]
    bar_chart(a_chart, "S4 lattice k-effective: printed values and synthetic midpoint", ["DRAGON", "Synthetic", "SERPENT"], a_values, "k-effective", [(37, 99, 235), (15, 118, 110), (183, 121, 31)])

    b_history = b["outputs"]["history"]
    b_chart = TMP / "s5-burnup.png"
    line_chart(b_chart, "S5 constant-power burnup identity", [row["day"] for row in b_history], [row["burnup_gwd_per_t"] for row in b_history], "full-power day", "burnup [GWd/t]", b["input_snapshot"]["reported_exit_burnup_gwd_per_t"])
    b_delta_chart = TMP / "s5-k-deltas.png"
    bar_chart(b_delta_chart, "S5 printed initial k-difference values", [f"Case {index}" for index in range(1, 7)], b["input_snapshot"]["reported_initial_k_difference_mk"], "difference [mk]", [(37, 99, 235)] * 6, baseline=0)

    c_chart = TMP / "s1-schedule.png"
    c_history = c["outputs"]["history"]
    line_chart(c_chart, "S1 synthetic full-core refuelling schedule", [row["full_power_day"] for row in c_history], [row["cumulative_refueled_channels"] for row in c_history], "full-power day", "cumulative channels", c["outputs"]["total_refueled_channels"], "synthetic end")
    c_interval = TMP / "s1-boron.png"
    c_comp = c["comparisons"][1]
    interval_chart(c_interval, "S1 reported enriched-boron interval", ["Boron delta"], [c_comp["synthetic_value"]], [c_comp["reference_minimum"]], [c_comp["reference_maximum"]], "delta [ppm]")

    d_chart = TMP / "s4-refueling-differences.png"
    d_values = [item["relative_difference_to_time_average_percent"] for item in d["comparisons"]]
    d_labels = [item["observable"].replace("_", " ").replace("exit burnup", "burnup")[:22] for item in d["comparisons"]]
    horizontal_difference_chart(d_chart, "S4 reported simulation vs time-average reference", d_labels, d_values, "relative difference [%]")

    styles = getSampleStyleSheet()
    title = ParagraphStyle("Title", parent=styles["Title"], fontName="Helvetica-Bold", fontSize=24, leading=29, textColor=NAVY, alignment=TA_LEFT, spaceAfter=16)
    h1 = ParagraphStyle("H1", parent=styles["Heading1"], fontName="Helvetica-Bold", fontSize=17, leading=21, textColor=NAVY, spaceBefore=7, spaceAfter=8)
    h2 = ParagraphStyle("H2", parent=styles["Heading2"], fontName="Helvetica-Bold", fontSize=12.5, leading=16, textColor=TEAL, spaceBefore=8, spaceAfter=5)
    body = ParagraphStyle("Body", parent=styles["BodyText"], fontName="Helvetica", fontSize=9.2, leading=13, textColor=INK, spaceAfter=7)
    small = ParagraphStyle("Small", parent=body, fontSize=7.6, leading=10, textColor=MUTED)
    caption = ParagraphStyle("Caption", parent=small, alignment=TA_CENTER, spaceBefore=3, spaceAfter=7)
    note = ParagraphStyle("Note", parent=body, backColor=PALE, borderColor=GRID, borderWidth=0.5, borderPadding=8, spaceBefore=5, spaceAfter=10)
    link_style = ParagraphStyle("Link", parent=small, textColor=BLUE)

    story: list[Any] = []
    story.append(Spacer(1, 0.35 * inch))
    story.append(p("P7-T08", title))
    story.append(p("Literature-calibrated synthetic golden cases", ParagraphStyle("Subtitle", parent=h1, fontSize=18, textColor=TEAL)))
    story.append(Spacer(1, 0.12 * inch))
    story.append(p("Detailed case report with comparison plots, tables, provenance, and admission limits", ParagraphStyle("Deck", parent=body, fontSize=12, leading=16, textColor=MUTED)))
    story.append(Spacer(1, 0.25 * inch))
    story.append(p("Outcome", h2))
    story.append(p("The package is a deterministic, project-authored synthetic authority for Golden-data audit and report regression. It is calibrated to published summary values and an independent unit identity; it does not claim that DRAGON/DONJON/SERPENT inputs, nuclear data, or source outputs were reproduced.", note))
    story.append(table([
        [p("Artifact", small), p("Value", small)],
        [p("Status", small), p(artifact["artifact_status"], small)],
        [p("Coverage", small), p(artifact["coverage_class"], small)],
        [p("Cases", small), p(str(len(artifact["cases"])), small)],
        [p("Approved artifact SHA-256", small), p(manifest["artifact_sha256"], small)],
        [p("Definition SHA-256", small), p(manifest["definition_sha256"], small)],
        [p("Generated bytes", small), p(f'{manifest["artifact_byte_length"]:,}', small)],
    ], [1.75 * inch, 5.55 * inch], small=True))
    story.append(Spacer(1, 0.2 * inch))
    story.append(p("Reading rule", h2))
    story.append(p("Values labelled <b>ReportedTarget</b> are source claims, not tolerances. Values labelled <b>ComputedTheory</b> are independently calculated identities. Values labelled <b>SyntheticCalibrated</b> are generated from the frozen definition. No row is a direct external solver reproduction.", body))
    story.append(PageBreak())

    story.append(p("1. Case inventory and authority boundary", h1))
    story.append(p("The four cases intentionally cover lattice and full-core shapes while remaining outside the runtime physics authority boundary. The paper locators are preserved in the generated JSON and repeated here for auditability.", body))
    inventory = [[p("Case", small), p("Model shape", small), p("Primary comparison", small), p("Disposition", small)]]
    for case in artifact["cases"]:
        inventory.append([
            p(case["case_id"], small),
            p(case["title"], small),
            p(case["coverage_status"], small),
            p("Synthetic only; no Core mapping", small),
        ])
    story.append(table(inventory, [1.7 * inch, 2.25 * inch, 1.85 * inch, 1.55 * inch], small=True))
    story.append(Spacer(1, 0.16 * inch))
    story.append(p("Source artifact boundary", h2))
    story.append(p("The official records expose the papers, but the complete solver package is not present. In the accessible Holmes thesis, the refuelling script names external CPO, Data, Results, procedure, and archive directories plus `Candu6_*.x2m` and `.result` files. The S1 and S5 records likewise establish the study and headline quantities without a repository-compatible input/output manifest. The package therefore preserves the P7-T07 admission gap.", body))
    story.append(p("Licensing decision: facts-only attribution. The approved artifact contains project-authored synthetic values and concise attributed facts; source bytes, figures, full tables, input decks, nuclear data, and source-result payloads remain external. No derived source run is claimed. Permission is still required for digitization, full-table redistribution, or raw input/output bundles.", note))
    source_rows = [[p("Source", small), p("Official record", small), p("License / use", small)]]
    for source in definition["sources"]:
        source_rows.append([p(source["source_id"] + ": " + source["title"], small), p(source_link(source["official_url"]), link_style), p(source["license_status"] + "<br/>" + source["use_boundary"], small)])
    story.append(table(source_rows, [2.25 * inch, 2.1 * inch, 3.0 * inch], small=True))
    story.append(PageBreak())

    # Case A
    story.append(p("2. Case A - S4 lattice k-effective cross-check", h1))
    story.append(p("The Holmes thesis reports a 37-element CANDU lattice comparison between DRAGON and SERPENT. The synthetic output is the minimax midpoint of the two printed k-effective values. This is a transparent calibration rule, not a claim that either external solver was run.", body))
    story.append(p("Source locators: Holmes thesis printed pp. 17-18, Table 3.1; Annex 2 printed pp. 47-55.", small))
    story.append(ReportImage(str(a_chart), width=7.15 * inch, height=3.69 * inch))
    story.append(p("Figure 1. The synthetic midpoint minimizes the maximum absolute difference to the two printed method values.", caption))
    a_rows = [[p("Observable", small), p("Reported target", small), p("Synthetic", small), p("Signed diff", small), p("Relative diff", small)]]
    for item in a["comparisons"]:
        a_rows.append([p(item["observable"], small), p(fmt_cell(item["reference_value"]), small), p(fmt_cell(item["synthetic_value"]), small), p(f'{item["signed_difference"] * 1000:.3f} mk', small), p(f'{item["relative_difference_percent"]:.4f}%', small)])
    story.append(table(a_rows, [2.2 * inch, 1.15 * inch, 1.15 * inch, 1.2 * inch, 1.2 * inch], small=True))
    story.append(Spacer(1, 0.1 * inch))
    story.append(p("Interpretation: the printed pair spans 5.37 mk. The midpoint is 2.685 mk from each method. The source's printed error label is retained as literature evidence only; it is not promoted to a repository tolerance.", note))
    story.append(PageBreak())

    # Case B
    story.append(p("3. Case B - S5 lattice depletion and burnup handoff", h1))
    story.append(p("The source reports a 300-day constant-power cycle at 31.9713 kW/kg and a rounded exit burnup of 9.6 GWd/t. The generator independently evaluates the unit identity and emits a complete day-by-day synthetic history.", body))
    story.append(ReportImage(str(b_chart), width=7.15 * inch, height=3.2 * inch))
    story.append(p("Figure 2. Independent constant-power burnup history versus the reported rounded exit target.", caption))
    story.append(ReportImage(str(b_delta_chart), width=7.15 * inch, height=3.2 * inch))
    story.append(p("Figure 3. Six printed initial k-difference values retained as reported targets; the paper's below-2-mk statement is not converted into a tolerance.", caption))
    b_output = b["outputs"]
    b_rows = [
        [p("Quantity", small), p("Value", small), p("Comparison", small)],
        [p("Power density", small), p("31.9713 kW/kg", small), p("Reported input", small)],
        [p("Duration", small), p("300 days", small), p("Reported input", small)],
        [p("Synthetic exit burnup", small), p(f'{b_output["synthetic_exit_burnup_gwd_per_t"]:.5f} GWd/t', small), p("ComputedTheory", small)],
        [p("Reported exit burnup", small), p("9.6 GWd/t", small), p(f'{b["comparisons"][0]["absolute_difference"]:.5f} GWd/t absolute difference ({b["comparisons"][0]["relative_difference_percent"]:.4f}%)', small)],
    ]
    story.append(table(b_rows, [1.75 * inch, 1.7 * inch, 3.85 * inch], small=True))
    story.append(PageBreak())

    # Case C
    story.append(p("4. Case C - S1 full-core schedule and alternate-cladding summary", h1))
    story.append(p("The S1 source describes a 380-channel, 12-bundle/channel CANDU-6 model with alternating coolant directions, an 8-bundle shift, 4 channels/day, and a 300 full-power-day history. The synthetic history makes those schedule quantities complete and deterministic.", body))
    story.append(ReportImage(str(c_chart), width=7.15 * inch, height=3.0 * inch))
    story.append(p("Figure 4. Complete synthetic cumulative refuelling schedule; the final 1200 channel events are 4 channels/day for 300 days.", caption))
    story.append(ReportImage(str(c_interval), width=7.15 * inch, height=2.4 * inch))
    story.append(p("Figure 5. The synthetic +1.25 ppm boron value is placed at the midpoint of the reported +1.1 to +1.4 ppm range.", caption))
    c_rows = [
        [p("Observable", small), p("Reported target", small), p("Synthetic value", small), p("Disposition", small)],
        [p("SiC reactivity change", small), p("+3.4 mk", small), p("+3.4 mk", small), p("Reported-summary replay", small)],
        [p("Enriched boron change", small), p("+1.1 to +1.4 ppm", small), p("+1.25 ppm", small), p("Inside reported interval", small)],
        [p("Enriched channel power change", small), p("-246 kW", small), p("-246 kW", small), p("Reported-summary replay", small)],
        [p("History", small), p("380 channels; 12 bundles/channel; 8-bundle shift; 4 channels/day; 300 FPD", small), p("1200 channels; 9600 shifted bundles", small), p("Synthetic schedule", small)],
    ]
    story.append(table(c_rows, [1.7 * inch, 2.05 * inch, 1.75 * inch, 1.8 * inch], small=True))
    story.append(p("These values are deliberately not represented as extracted source curves or a runtime crosswalk. They are small reported targets paired with a deterministic project-authored schedule.", note))
    story.append(PageBreak())

    # Case D
    story.append(p("5. Case D - S4 full-core refuelling aggregate fixture", h1))
    story.append(p("The Holmes thesis Table 5.2 compares a time-average reference with a reported simulation. The synthetic package replays the seven small simulation aggregates exactly and retains the time-average values so their differences remain visible.", body))
    story.append(ReportImage(str(d_chart), width=7.15 * inch, height=3.69 * inch))
    story.append(p("Figure 6. Relative difference between the reported simulation aggregate and the thesis time-average reference. The bars describe the source comparison; they are not solver error tolerances.", caption))
    d_rows = [[p("Metric", small), p("TA reference", small), p("Reported simulation", small), p("Synthetic", small), p("Sim-TA", small)]]
    for metric, item in zip(d["outputs"]["aggregate_metrics"], d["comparisons"]):
        d_rows.append([p(metric["metric"].replace("_", " "), small), p(fmt_cell(metric["reference_value"]), small), p(fmt_cell(metric["reported_simulation_value"]), small), p(fmt_cell(metric["synthetic_value"]), small), p(fmt_cell(item["difference_to_time_average_reference"]), small)])
    story.append(table(d_rows, [2.4 * inch, 1.0 * inch, 1.25 * inch, 1.0 * inch, 1.1 * inch], small=True))
    story.append(Spacer(1, 0.1 * inch))
    story.append(p("The deterministic schedule contains 408 refuelling events across 100 days, exactly 4.08 channels/day, with eight bundles per event. This is a reproducible provenance/table fixture; it is not a DONJON4 or CANFUEL run.", note))
    story.append(PageBreak())

    story.append(p("6. Reproducibility, validation, and next admission step", h1))
    story.append(p("The standalone generator is independent of ReactorSim.Core. It reads the frozen definition, produces canonical UTF-8 JSON, validates exact artifact bytes and hashes, and checks the case invariants used by the focused Golden boundary tests. Candidate and approved artifacts differ only in their explicit disposition fields and are regenerated independently.", body))
    validation_rows = [
        [p("Check", small), p("Evidence", small)],
        [p("Definition binding", small), p(manifest["definition_sha256"], small)],
        [p("Approved artifact", small), p(manifest["artifact_sha256"] + " / " + f'{manifest["artifact_byte_length"]:,} bytes', small)],
        [p("Independent regeneration", small), p(str(manifest["independent_repeat_equal"]) + " / " + manifest["independent_repeat_sha256"], small)],
        [p("Cases and schedule", small), p("4 cases; 301-day S5 history; 301-day S1 schedule; 101-day S4 schedule; 7 S4 aggregate metrics", small)],
        [p("Physical tolerance", small), p("Not selected", small)],
        [p("Core mapping", small), p("Prohibited by artifact boundary", small)],
    ]
    story.append(table(validation_rows, [1.65 * inch, 5.7 * inch], small=True))
    story.append(Spacer(1, 0.17 * inch))
    story.append(p("Required next step for any physical/external claim", h2))
    story.append(p("A separate owner-authorized admission task must supply exact source build identity, nuclear-data files/checksums, complete geometry and state/history, units/normalization, output ordering, independent rerun evidence, and lawful artifact handling. Until then, the package is an authoritative synthetic regression artifact only.", note))
    story.append(p("References", h2))
    references = [
        "P1-T08 digest and source manifest in the repository.",
        "S1: " + source_link("https://publications.polymtl.ca/5048/") + " (official PolyPublie record).",
        "S5: " + source_link("https://publications.polymtl.ca/5047/") + " (official PolyPublie record).",
        "S4: " + source_link("https://publications.polymtl.ca/1307/1/2013_BradfordHolmes.pdf") + " (official thesis PDF).",
    ]
    for reference in references:
        story.append(p("- " + reference, small))

    document = SimpleDocTemplate(
        str(OUT),
        pagesize=letter,
        rightMargin=0.6 * inch,
        leftMargin=0.6 * inch,
        topMargin=0.55 * inch,
        bottomMargin=0.65 * inch,
        title="P7-T08 Literature-Calibrated Synthetic Golden Cases",
        author="ReactorSim project",
        subject="Synthetic comparison cases and evidence boundary",
    )
    document.build(story, onFirstPage=page_footer, onLaterPages=page_footer, canvasmaker=DeterministicCanvas)
    print(f"WROTE {OUT}")
    print(f"PDF_SHA256 {sha256(OUT)}")


if __name__ == "__main__":
    main()
