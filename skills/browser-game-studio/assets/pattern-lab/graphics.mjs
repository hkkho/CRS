// Original vector art. These drawings explain model observations; they never
// create catches, eggs, delivered water, trial fitness or other resources.
import { connected, served, requests } from "./model.mjs";

const TAU = Math.PI * 2;
const ink = "#293b35",
  cream = "#fff6dc";
function box(c, x, y, w, h, fill, r = 0, stroke) {
  c.beginPath();
  c.roundRect(x, y, w, h, r);
  c.fillStyle = fill;
  c.fill();
  if (stroke) {
    c.strokeStyle = stroke;
    c.stroke();
  }
}
function oval(c, x, y, rx, ry, fill) {
  c.beginPath();
  c.ellipse(x, y, rx, ry, 0, 0, TAU);
  c.fillStyle = fill;
  c.fill();
}
function path(c, points, fill, stroke, width = 2) {
  c.beginPath();
  points.forEach(([x, y], i) => (i ? c.lineTo(x, y) : c.moveTo(x, y)));
  if (fill) {
    c.closePath();
    c.fillStyle = fill;
    c.fill();
  }
  if (stroke) {
    c.strokeStyle = stroke;
    c.lineWidth = width;
    c.stroke();
  }
}
function line(c, x, y, x2, y2, color, width = 2) {
  path(
    c,
    [
      [x, y],
      [x2, y2],
    ],
    null,
    color,
    width,
  );
}
function text(
  c,
  value,
  x,
  y,
  size = 18,
  color = ink,
  align = "left",
  font = "sans-serif",
) {
  c.fillStyle = color;
  c.font = `${size}px ${font}`;
  c.textAlign = align;
  c.fillText(value, x, y);
}
function words(c, value, x, y, width, size = 21, color = ink) {
  c.font = `${size}px sans-serif`;
  let row = "",
    yy = y;
  for (const word of value.split(" ")) {
    const trial = row + word + " ";
    if (c.measureText(trial).width > width && row) {
      text(c, row, x, yy, size, color);
      yy += size * 1.4;
      row = word + " ";
    } else row = trial;
  }
  text(c, row, x, yy, size, color);
}
function sky(c, top, bottom) {
  const g = c.createLinearGradient(0, 0, 0, 540);
  g.addColorStop(0, top);
  g.addColorStop(1, bottom);
  box(c, 0, 0, 960, 540, g);
}
function cloud(c, x, y, size = 1, color = "#fff9e9") {
  c.save();
  c.translate(x, y);
  c.scale(size, size);
  oval(c, 0, 0, 55, 19, color);
  oval(c, -26, -7, 28, 22, color);
  oval(c, 13, -16, 34, 27, color);
  oval(c, 43, -3, 29, 18, color);
  c.restore();
}
function hash(n) {
  return (Math.sin(n * 127.1 + 311.7) * 43758.5453) % 1;
}
function house(c, x, y, scale = 1, roof = "#b96848", body = "#f2e7c6") {
  c.save();
  c.translate(x, y);
  c.scale(scale, scale);
  oval(c, 0, 4, 47, 12, "#253c3b22");
  box(c, -34, -66, 68, 70, body, 3);
  path(
    c,
    [
      [-46, -64],
      [0, -103],
      [46, -64],
    ],
    roof,
    ink,
    2,
  );
  box(c, -10, -29, 20, 33, "#486253", 3);
  box(c, -27, -51, 14, 15, "#81acb5", 1);
  box(c, 14, -51, 14, 15, "#81acb5", 1);
  line(c, -20, -51, -20, -36, cream, 1);
  line(c, 21, -51, 21, -36, cream, 1);
  box(c, 20, -98, 10, 24, roof, 1);
  c.restore();
}
function tree(c, x, y, size = 1) {
  c.save();
  c.translate(x, y);
  c.scale(size, size);
  oval(c, 0, 5, 25, 8, "#1c3c3220");
  box(c, -5, -48, 10, 53, "#8b6748", 2);
  path(
    c,
    [
      [-30, -25],
      [0, -86],
      [30, -25],
    ],
    "#558365",
  );
  path(
    c,
    [
      [-25, -47],
      [0, -101],
      [25, -47],
    ],
    "#6f9a72",
  );
  path(
    c,
    [
      [-17, -71],
      [0, -110],
      [17, -71],
    ],
    "#80a37b",
  );
  c.restore();
}
function person(c, x, y, t, index = 0, size = 1) {
  c.save();
  c.translate(x, y);
  c.scale(size, size);
  const walk = Math.sin(t * 5 + index),
    col = ["#c16d48", "#687ca0", "#d4a34d", "#6c977d"][index % 4];
  oval(c, 0, 3, 17, 5, "#1e352a24");
  line(c, -5, -17, -6 + walk * 4, 0, ink, 5);
  line(c, 5, -17, 6 - walk * 4, 0, ink, 5);
  box(c, -12, -42, 24, 29, col, 7);
  oval(c, 0, -52, 15, 16, "#edc7a0");
  path(
    c,
    [
      [-15, -53],
      [-13, -65],
      [0, -71],
      [14, -63],
      [15, -53],
      [3, -57],
    ],
    ink,
  );
  oval(c, -5, -52, 1.6, 1.8, ink);
  oval(c, 5, -52, 1.6, 1.8, ink);
  line(c, -2, -45, 3, -45, ink, 1.4);
  line(c, -10, -34, -19 - walk * 2, -22, col, 6);
  line(c, 10, -34, 20 + walk * 2, -25, col, 6);
  c.restore();
}
function council(c, s, t) {
  sky(c, "#bfdedb", "#f2ead1");
  for (let i = 0; i < 5; i++)
    cloud(
      c,
      ((i * 235 + t * 5) % 1250) - 130,
      95 + (i % 2) * 80,
      0.7 + i * 0.09,
      "#fffaf0b0",
    );
  oval(c, 480, 483, 370, 35, "#637d6824");
  path(
    c,
    [
      [80, 412],
      [850, 412],
      [770, 485],
      [230, 500],
    ],
    "#839c83",
  );
  oval(c, 470, 406, 390, 58, "#99b29a");
  for (let i = 0; i < 5; i++)
    house(
      c,
      160 + i * 92,
      347 - (i % 2) * 27,
      0.8,
      ["#bb754f", "#658777", "#c28f58"][i % 3],
    );
  path(
    c,
    [
      [210, 403],
      [580, 353],
      [790, 404],
      [480, 447],
    ],
    "#d5c8a8",
  );
  oval(c, 613, 406, 99, 29, "#8a684c");
  box(c, 597, 407, 13, 42, "#74563e");
  box(c, 664, 402, 12, 40, "#74563e");
  oval(c, 629, 393, 108, 33, "#c79662");
  person(c, 550, 388, 0, 1, 1.2);
  person(c, 702, 440, t * 0.35, Math.min(s.turn, 7) % 4, 1.35);
  box(c, 361, 63, 534, 174, "#1e3a3330", 12);
  box(c, 355, 55, 534, 174, cream, 12, ink);
  path(
    c,
    [
      [705, 228],
      [740, 255],
      [746, 228],
    ],
    cream,
    ink,
    1.5,
  );
  const r = requests[Math.min(s.turn, 7)];
  text(c, r.who, 380, 92, 19, "#a65e3b");
  words(c, r.text, 380, 125, 478, 21);
  text(c, "THE FLOATING TOWN", 110, 438, 13, "#334c3c");
  for (let i = 0; i < Math.min(8, Math.floor(s.people / 3)); i++)
    person(c, 175 + i * 37, 422 - (i % 2) * 8, t * 0.3, i, 0.55);
}
function parcel(c, x, y, stamp, route) {
  c.save();
  c.translate(x, y);
  c.rotate(-0.035);
  oval(c, 0, 63, 120, 20, "#34251925");
  box(c, -110, -60, 220, 121, "#ba925e", 8, "#6f553d");
  box(c, -109, -60, 220, 20, "#c9a572", 4);
  line(c, -110, 0, 110, 0, "#eee0b3", 3);
  line(c, 0, -60, 0, 60, "#eee0b3", 4);
  box(c, -80, -32, 128, 54, "#f7edce", 2);
  text(c, route.toUpperCase(), -69, -8, 16, ink);
  text(c, "ROUTE / MOONPOST", -69, 9, 9, "#627065");
  if (stamp) {
    box(c, 58, -43, 37, 43, "#657d8a", 2);
    oval(c, 76, -23, 11, 11, cream);
    text(c, stamp, 76, -17, 15, ink, "center");
  }
  c.restore();
}
function post(c, s, t, ui) {
  sky(c, "#34494b", "#293d3d");
  box(c, 0, 190, 960, 350, "#b28b64");
  for (let y = 205; y < 540; y += 50) {
    line(c, 0, y, 960, y, "#a17a5545", 2);
    for (let i = 0; i < 8; i++)
      line(
        c,
        i * 137 + Math.sin(y) * 30,
        y + 23,
        i * 137 + 80,
        y + 23,
        "#c7a37d60",
        1,
      );
  }
  box(c, 50, 38, 280, 145, "#d5ddc7", 4, "#826a50");
  box(c, 64, 51, 252, 120, "#a1c7c6");
  path(
    c,
    [
      [64, 133],
      [145, 103],
      [220, 140],
      [316, 112],
      [316, 171],
      [64, 171],
    ],
    "#6d9991",
  );
  cloud(c, 220, 80, 0.5);
  line(c, 190, 51, 190, 171, "#d8dfc4", 8);
  box(c, 42, 172, 297, 13, "#d5ddc7", 2);
  box(c, 53, 241, 245, 223, "#e8dab4", 3, "#80684d");
  text(c, "POSTAGE TABLE", 75, 277, 16);
  text(c, "1–3 units → exact stamp", 75, 310, 14);
  text(c, `This parcel: ${s.parcel.weight} units`, 75, 345, 18);
  line(c, 75, 367, 276, 367, "#bba884", 1);
  words(
    c,
    "Pick a stamp. Drag the parcel to its labelled route, or use the route buttons.",
    75,
    395,
    200,
    14,
  );
  for (const [i, name] of ["harbor", "forest"].entries()) {
    const y = 220 + i * 170;
    box(c, 708, y - 65, 203, 127, "#405c66", 7, "#2a4249");
    box(c, 720, y - 53, 180, 86, "#53747b", 4);
    text(c, name.toUpperCase(), 810, y + 15, 18, cream, "center");
    text(c, "OUTGOING", 810, y + 44, 10, "#c4d8d5", "center");
  }
  const p = ui.drag ?? { x: 480, y: 327 };
  parcel(c, p.x, p.y, s.parcel.stamp, s.parcel.route);
  box(c, 359, 455, 210, 43, "#776658", 7);
  text(c, "INK / POSTMARK", 464, 482, 13, cream, "center");
}
export function waterCell(x, y) {
  const col = Math.floor((x - 102) / 120),
    row = Math.floor((y - 90) / 95);
  return col >= 0 && col < 6 && row >= 0 && row < 4 ? row * 6 + col : null;
}
function water(c, s, t) {
  sky(c, "#cbded2", "#dee5ca");
  box(c, 0, 465, 960, 75, "#8eb9bb");
  for (let i = 0; i < 16; i++)
    line(
      c,
      i * 72 + ((t * 8) % 72),
      491 + (i % 2) * 18,
      i * 72 + 35 + ((t * 8) % 72),
      491 + (i % 2) * 18,
      "#d6e7d7",
      2,
    );
  const network = connected(s),
    homes = [5, 17, 23],
    ordered = homes.filter((n) => network.has(n));
  for (let n = 0; n < 24; n++) {
    const x = 102 + (n % 6) * 120,
      y = 90 + Math.floor(n / 6) * 95;
    box(
      c,
      x + 3,
      y + 3,
      114,
      89,
      (n + Math.floor(n / 6)) % 2 ? "#bed0ad" : "#c5d5b6",
      5,
    );
    if (n === s.selected)
      box(c, x + 5, y + 5, 110, 85, "#fff6d137", 5, "#996c37");
    if (s.pipes.includes(n)) {
      const cx = x + 60,
        cy = y + 47;
      for (const d of [-6, 6, n % 6 ? -1 : 99, n % 6 < 5 ? 1 : 99])
        if (s.pipes.includes(n + d) && n + d >= 0 && n + d < 24) {
          const dx = d === 1 ? 60 : d === -1 ? -60 : 0,
            dy = d === 6 ? 47 : d === -6 ? -47 : 0;
          line(c, cx, cy, cx + dx, cy + dy, "#716b50", 13);
          line(
            c,
            cx,
            cy,
            cx + dx,
            cy + dy,
            network.has(n) ? "#69afbe" : "#a1a18a",
            7,
          );
          if (network.has(n)) {
            const q = (t * 0.6 + n * 0.13) % 1;
            oval(c, cx + dx * q, cy + dy * q, 3, 3, "#d6f4e9");
          }
        }
      oval(c, cx, cy, 9, 9, "#847b56");
      oval(c, cx, cy, 5, 5, network.has(n) ? "#9ee1df" : "#b4b49d");
    }
    if (n === 6) {
      box(c, x + 35, y + 10, 50, 50, "#9a8a69", 5);
      oval(c, x + 60, y + 17, 26, 13, "#e0caa0");
      oval(c, x + 60, y + 17, 17, 8, "#5295a9");
      line(c, x + 31, y + 13, x + 31, y - 15, "#65573b", 5);
      line(c, x + 89, y + 13, x + 89, y - 15, "#65573b", 5);
      path(
        c,
        [
          [x + 23, y - 15],
          [x + 60, y - 38],
          [x + 97, y - 15],
        ],
        "#b46c4c",
      );
    }
    if (homes.includes(n)) {
      house(c, x + 60, y + 65, 0.6, network.has(n) ? "#557f78" : "#ba825c");
      const good = ordered.indexOf(n) >= 0 && ordered.indexOf(n) < s.capacity;
      oval(
        c,
        x + 96,
        y + 19,
        11,
        11,
        good ? "#447c66" : network.has(n) ? "#d2a451" : "#b1795c",
      );
      text(
        c,
        good ? "✓" : network.has(n) ? "!" : "×",
        x + 96,
        y + 24,
        16,
        cream,
        "center",
      );
    }
  }
  text(c, "WELL", 160, 87, 12, ink, "center");
  text(c, `${served(s)} / 3 homes supplied`, 480, 512, 17, "#26454a", "center");
}
function harvest(c, s, t) {
  sky(c, "#142631", "#35515b");
  oval(c, 773, 105, 45, 45, "#e2d9ac");
  oval(c, 789, 94, 39, 39, "#203742");
  for (let i = 0; i < 35; i++)
    oval(
      c,
      30 + Math.abs(hash(i + 1)) * 900,
      32 + Math.abs(hash(i + 88)) * 190,
      1.2,
      1.2,
      "#d7d6af",
    );
  box(c, 0, 279, 960, 261, "#1a3641");
  for (let i = 0; i < 40; i++) {
    const x = Math.abs(hash(i + 4)) * 1000 - 20,
      y = 300 + Math.abs(hash(i + 74)) * 225;
    line(
      c,
      x + Math.sin(t + i) * 9,
      y,
      x + 25 + Math.sin(t + i) * 9,
      y,
      "#5a7a7a75",
      1.5,
    );
  }
  const bob = Math.sin(t * 1.2) * 4,
    sy = 300 + bob;
  path(
    c,
    [
      [292, sy],
      [619, sy],
      [576, sy + 53],
      [337, sy + 53],
    ],
    "#b99361",
    ink,
    2,
  );
  path(
    c,
    [
      [305, sy + 10],
      [606, sy + 10],
      [588, sy + 25],
      [322, sy + 25],
    ],
    "#d4bb81",
  );
  box(c, 367, sy - 60, 95, 59, "#d4caa0", 3);
  box(c, 385, sy - 48, 32, 25, "#456371", 2);
  box(c, 430, sy - 48, 20, 25, "#456371", 2);
  line(c, 485, sy - 116, 485, sy, "#d9c79d", 5);
  line(c, 485, sy - 110, 650, sy - 50, "#c0ab80", 3);
  for (let n = 0; n < Math.max(1, s.nets); n++) {
    const nx = 610 + n * 27,
      ny = sy + 50 + Math.sin(t * 1.4 + n) * 12;
    line(c, 650, sy - 50, nx, ny, "#d8c796", 1);
    path(
      c,
      [
        [nx - 24, ny],
        [nx + 24, ny],
        [nx + 8, ny + 45],
        [nx - 8, ny + 45],
      ],
      "#a5c0ae25",
      "#96a99b",
      1,
    );
    for (let k = 0; k < 3; k++)
      line(c, nx - 18, ny + k * 12, nx + 18, ny + k * 12, "#96a99b", 0.7);
  }
  for (let i = 0; i < 18; i++) {
    const x = 60 + Math.abs(hash(i + 31)) * 830 + Math.sin(t * 0.7 + i) * 15,
      y = 370 + Math.abs(hash(i + 230)) * 100;
    oval(c, x, y, 5, 2, "#dfc989");
    line(c, x - 4, y, x - 10, y - 4, "#dfc989", 1);
    line(c, x - 4, y, x - 10, y + 4, "#dfc989", 1);
  }
  text(
    c,
    `VESSEL ${s.vessel} / ${s.nets} automatic nets`,
    70,
    70,
    15,
    "#dcd9b7",
  );
}
function chicken(c, x, y, t, index) {
  c.save();
  c.translate(x, y);
  c.scale(index % 2 ? -0.95 : 0.95, 1);
  const peck = Math.max(0, Math.sin(t * 2 + index)) * 5;
  oval(c, 0, 9, 22, 7, "#31513722");
  line(c, -9, 0, -11 + Math.sin(t * 5 + index) * 3, 12, "#86613a", 3);
  line(c, 7, 0, 9 - Math.sin(t * 5 + index) * 3, 12, "#86613a", 3);
  const col = ["#f3e9c9", "#b97a4c", "#d0ac70", "#e7d1a3"][index % 4];
  oval(c, 0, -9, 24, 19, col);
  path(
    c,
    [
      [-15, -12],
      [-31, -29],
      [-29, -10],
    ],
    col,
  );
  oval(c, 18, -23 + peck, 13, 14, col);
  path(
    c,
    [
      [27, -24 + peck],
      [39, -18 + peck],
      [27, -15 + peck],
    ],
    "#d5a54a",
  );
  box(c, 9, -42 + peck, 17, 8, "#b9513b", 3);
  oval(c, 21, -27 + peck, 2, 2, ink);
  c.restore();
}
function flock(c, s, t) {
  sky(c, "#c6dfce", "#e3e8c7");
  box(c, 0, 230, 960, 310, "#9ab56f");
  for (let i = 0; i < 180; i++) {
    const x = Math.abs(hash(i + 1)) * 960,
      y = 244 + Math.abs(hash(i + 120)) * 290;
    line(c, x, y, x - 3, y - 6, "#7e9b58", 1);
  }
  for (let i = 0; i < 13; i++) {
    box(c, i * 80, 223, 9, 65, "#d5bb88", 2);
    line(c, i * 80, 243, i * 80 + 80, 243, "#dec99b", 7);
    line(c, i * 80, 270, i * 80 + 80, 270, "#dec99b", 7);
  }
  house(c, 158, 276, 1.55, "#aa674d", "#e5dab4");
  box(c, 116, 221, 84, 62, "#5f654e", 3);
  text(c, "MEADOW COOP", 158, 191, 12, cream, "center");
  for (let i = 0; i < s.hens; i++) {
    const x = 320 + (i % 4) * 135 + Math.sin(t * 0.45 + i) * 22,
      y = 318 + Math.floor(i / 4) * 95 + Math.cos(t * 0.7 + i) * 15;
    chicken(c, x, y, t, i);
  }
  for (let i = 0; i < s.hens; i++) {
    const x = 300 + i * 70;
    oval(c, x, 467, 29, 12, "#bd9d55");
    oval(c, x, 464, 22, 8, "#8e793f");
    if (s.eggs > i * 0.8) oval(c, x, 456, 8, 11, cream);
  }
  for (let i = 0; i < Math.min(40, s.feed * 5); i++)
    oval(
      c,
      740 + Math.sin(i * 5) * 42,
      423 + Math.cos(i * 3) * 22,
      2.4,
      1.5,
      "#e8c46c",
    );
  text(
    c,
    s.feed > 0
      ? "Fed hens produce. Clear the nests to keep room."
      : "The flock needs feed.",
    490,
    509,
    16,
    ink,
    "center",
  );
}
function factory(c, s, t, ui) {
  sky(c, "#d3dbce", "#eee4cc");
  for (let row = 0; row < 5; row++)
    for (let col = 0; col < 12; col++)
      box(c, col * 90 - (row % 2) * 45, row * 47, 86, 43, "#b6bba333", 2);
  box(c, 0, 401, 960, 139, "#8d9380");
  box(c, 108, 352, 748, 38, "#47584d", 8);
  for (let i = 0; i < 18; i++) {
    oval(c, 125 + i * 42, 373, 12, 12, "#929b82");
    oval(c, 125 + i * 42, 373, 4, 4, "#34493d");
  }
  line(c, 150, 398, 150, 469, "#455448", 15);
  line(c, 814, 398, 814, 469, "#455448", 15);
  for (let i = 0; i < Math.min(3, Math.max(1, s.machines)); i++) {
    const x = 360 + i * 155;
    box(c, x - 50, 178, 100, 177, "#7e9887", 8, ink);
    box(c, x - 28, 248, 56, 62, "#2d443e", 2);
    const manual =
      !ui.reducedMotion &&
      (ui.effects ?? []).some(
        (e) => e.kind === "work" && t - e.at >= 0 && t - e.at < 0.6,
      );
    const working = (s.machines > 0 && s.ore > 0 && s.parts < 8) || manual;
    const offset = working
      ? Math.max(0, Math.sin(t * (2 + s.machines) + i)) * 27
      : 0;
    box(c, x - 25, 248 + offset, 50, 16, "#bd7952", 2);
    box(c, x - 8, 218, 16, 35 + offset, "#a9b39a", 1);
    oval(c, x, 205, 22, 22, "#e0c798");
    const phase = working ? t : 0;
    line(
      c,
      x,
      205,
      x + Math.sin(phase) * 14,
      205 - Math.cos(phase) * 14,
      "#6b7057",
      3,
    );
    box(c, x - 37, 326, 74, 15, "#c69a65");
  }
  for (let i = 0; i < Math.min(16, s.ore); i++)
    path(
      c,
      [
        [94 + (i % 4) * 17, 351 - Math.floor(i / 4) * 15],
        [105 + (i % 4) * 17, 338 - Math.floor(i / 4) * 15],
        [117 + (i % 4) * 17, 352 - Math.floor(i / 4) * 15],
      ],
      "#bb8d61",
      ink,
      1,
    );
  for (let i = 0; i < s.parts; i++) {
    const x = 775 + (i % 2) * 38,
      y = 348 - Math.floor(i / 2) * 28;
    box(c, x, y - 22, 29, 23, "#ce9c69", 2, "#846745");
    line(c, x + 14, y - 22, x + 14, y, "#edcf99", 2);
  }
  text(c, "ORE", 100, 406, 13, cream);
  text(c, "PRESS", 365, 406, 13, cream, "center");
  text(c, "OUTPUT / 8", 805, 406, 13, cream, "center");
  text(
    c,
    s.parts >= 8
      ? "BUFFER FULL · SHIP TO CONTINUE"
      : s.ore === 0
        ? "WAITING FOR ORE"
        : "MATERIAL → TRANSFORMATION → DELIVERY",
    480,
    495,
    16,
    ink,
    "center",
  );
}
function plant(c, x, y, growth, index, t) {
  const colors = ["#578564", "#71976e", "#4e7c69", "#849661"];
  oval(c, x, y + 13, 37, 10, "#3d54321c");
  const h = 45 + growth * 125;
  line(c, x, y - 12, x, y - h, colors[index % 4], 5);
  for (let j = 0; j < Math.ceil(growth * 6) + 2; j++) {
    const yy = y - 30 - j * 19,
      side = j % 2 ? 1 : -1,
      len = 16 + growth * 17;
    line(c, x, yy, x + side * len * 0.6, yy - 8, colors[index % 4], 2);
    c.save();
    c.translate(x + side * len * 0.6, yy - 8);
    c.rotate(side * (-0.6 + Math.sin(t * 0.7 + index) * 0.025));
    oval(c, side * 8, 0, len * 0.65, 8 + growth * 5, colors[(index + j) % 4]);
    line(c, -len * 0.3, 0, len * 0.6, 0, "#afc292", 1);
    c.restore();
  }
  const col = ["#b87450", "#718b86", "#c49f6b"][index % 3];
  path(
    c,
    [
      [x - 28, y - 25],
      [x + 28, y - 25],
      [x + 21, y + 17],
      [x - 21, y + 17],
    ],
    col,
    ink,
    1,
  );
  box(c, x - 33, y - 32, 66, 13, col, 3);
  oval(c, x, y - 31, 29, 6, growth >= 1 ? "#c6aa68" : "#5d5641");
  if (growth >= 1) {
    oval(c, x, y - h - 4, 8, 8, "#e4be66");
    oval(c, x, y - h - 4, 3, 3, "#856544");
  }
}
function garden(c, s, t) {
  sky(c, "#f0e9cc", "#d9dbc2");
  box(c, 68, 40, 824, 305, "#b7c5a8", 7);
  box(c, 84, 55, 792, 272, "#d0dfca", 3);
  cloud(c, 248 + ((t * 2) % 100), 130, 1.3, "#edf0d9");
  cloud(c, 680, 92, 0.9, "#edf0d9");
  path(
    c,
    [
      [84, 277],
      [270, 203],
      [460, 266],
      [680, 188],
      [876, 258],
      [876, 327],
      [84, 327],
    ],
    "#a5c0aa",
  );
  for (const x of [84, 348, 612, 876]) line(c, x, 55, x, 327, "#eee6c8", 13);
  line(c, 84, 190, 876, 190, "#eee6c8", 10);
  path(
    c,
    [
      [84, 55],
      [320, 55],
      [685, 540],
      [260, 540],
    ],
    "#fff3c224",
  );
  box(c, 45, 419, 870, 19, "#a0815d", 4);
  box(c, 66, 438, 24, 78, "#947454");
  box(c, 868, 438, 24, 78, "#947454");
  s.plants.forEach((g, i) =>
    plant(c, 115 + i * (730 / Math.max(6, s.plants.length - 1)), 403, g, i, t),
  );
  box(c, 768, 466, 66, 35, "#7b9d99", 7);
  path(
    c,
    [
      [832, 476],
      [863, 463],
      [869, 474],
      [830, 487],
    ],
    "#7b9d99",
  );
  c.beginPath();
  c.arc(762, 479, 17, 0, TAU);
  c.strokeStyle = "#7b9d99";
  c.lineWidth = 7;
  c.stroke();
  text(
    c,
    s.water > 0.15
      ? "A little care, a visible change."
      : "Dry soil pauses growth. The plants will wait.",
    70,
    490,
    16,
    ink,
  );
}
function evolution(c, s, t) {
  sky(c, "#e4e4e9", "#f5eee0");
  for (let k = 0; k <= 8; k++) {
    const x = 145 + k * 85;
    line(c, x, 66, x, 487, "#b8b8c160", 1);
    text(c, `${k} m`, x, 54, 12, "#737284", "center");
  }
  let leader = 0;
  s.creatures.forEach((g, i) => {
    if (g.distance > s.creatures[leader].distance) leader = i;
  });
  s.creatures.forEach((g, i) => {
    const y = 112 + i * 68,
      x = 145 + g.distance * 85;
    line(c, 126, y + 13, 885, y + 13, "#a3a3ae", 1);
    text(c, String.fromCharCode(65 + i), 56, y - 11, 18, "#665e7c");
    text(
      c,
      `${g.stride.toFixed(2)} / ${g.rhythm.toFixed(2)}`,
      56,
      y + 7,
      9,
      "#777383",
    );
    const phase = s.running ? t * (3 + g.rhythm * 5) : 0,
      step = Math.sin(phase) * g.stride * 20,
      col = ["#7b7199", "#6d9b91", "#ba8a66", "#7498ac", "#9b9370", "#a27487"][
        i
      ];
    oval(c, x, y + 13, 26, 5, "#292d3a16");
    line(c, x - 14, y - 21, x + 17, y - 27, col, 9);
    for (const side of [-1, 1]) {
      const xx = x + side * 14,
        knee = xx + side * step;
      line(c, xx, y - 22, knee, y - 8, col, 5);
      line(c, knee, y - 8, xx - side * step, y + 11, ink, 4);
      oval(c, knee, y - 8, 3, 3, cream);
    }
    oval(c, x + 19, y - 27, 7, 7, col);
    oval(c, x + 22, y - 29, 1.5, 1.5, ink);
    if (!s.running && s.trial > 0 && i === leader) {
      c.beginPath();
      c.arc(x, y - 19, 37, 0, TAU);
      c.strokeStyle = "#d3a64e";
      c.lineWidth = 3;
      c.stroke();
      text(c, "BEST", x, y - 62, 10, "#976f2d", "center");
    }
  });
  box(c, 130, 510, 720, 5, "#c6c5cf", 3);
  box(c, 130, 510, (720 * s.trial) / 6, 5, "#7c7596", 3);
}
function orb(c, s, t) {
  sky(c, "#1e273c", "#38344b");
  for (let i = 0; i < 55; i++)
    oval(
      c,
      Math.abs(hash(i + 8)) * 960,
      Math.abs(hash(i + 99)) * 540,
      1,
      1,
      "#d4c7a382",
    );
  const g = c.createRadialGradient(480, 280, 10, 480, 280, 200);
  g.addColorStop(0, "#b1a6d44a");
  g.addColorStop(1, "#b1a6d400");
  oval(c, 480, 280, 205, 205, g);
  for (let i = 0; i < 3; i++) {
    c.beginPath();
    c.ellipse(
      480,
      275,
      113 + i * 30,
      113 + i * 30,
      (i % 2 ? 1 : -1) * t * 0.05,
      0,
      TAU,
    );
    c.strokeStyle = i === 1 ? "#b7a47c" : "#a1a4bf55";
    c.lineWidth = i === 1 ? 3 : 1;
    c.stroke();
  }
  const nodes = [
    [178, 300, "CHARGE", s.charge, 5, "#9dc4d0"],
    [480, 95, "LIGHT", s.light, s.capacity, "#e0cc95"],
    [782, 300, "MATERIAL", s.wood, 3, "#b4c19b"],
  ];
  for (const [x, y, label, n, cap, col] of nodes) {
    line(c, 480, 275, x, y, "#a89ba878", 2);
    const p = (t * 0.3) % 1;
    oval(c, 480 + (x - 480) * p, 275 + (y - 275) * p, 3, 3, col);
    oval(c, x, y, 39, 39, "#333a51");
    c.beginPath();
    c.arc(x, y, 42, -Math.PI / 2, -Math.PI / 2 + TAU * Math.min(1, n / cap));
    c.strokeStyle = col;
    c.lineWidth = 4;
    c.stroke();
    text(c, Math.floor(n), x, y + 8, 24, cream, "center");
    text(c, label, x, y + 66, 12, col, "center");
  }
  path(
    c,
    [
      [480, 168],
      [530, 270],
      [480, 364],
      [430, 270],
    ],
    s.status === "won" ? "#a4c7a4" : "#b8b0ce",
    "#e3dcc0",
    2,
  );
  path(
    c,
    [
      [480, 168],
      [480, 364],
      [430, 270],
    ],
    s.status === "won" ? "#6c9f81" : "#7c79a8",
  );
  path(
    c,
    [
      [462, 207],
      [489, 260],
      [465, 300],
    ],
    "#e1d9e8a0",
  );
  box(c, 403, 387, 154, 18, "#b7a484", 4);
  box(c, 429, 405, 102, 23, "#82745f", 3);
  text(
    c,
    s.status === "won"
      ? "A WORLD AWAKENS"
      : "CONVERSION / CAPACITY / DISCOVERY",
    480,
    485,
    15,
    "#d0c4b0",
    "center",
  );
}
export function isoPoint(cell) {
  const x = cell % 7,
    y = Math.floor(cell / 7);
  return { x: 480 + (x - y) * 52, y: 117 + (x + y) * 27 };
}
export function hamletCell(x, y) {
  let best = null,
    d = Infinity;
  for (let n = 0; n < 35; n++) {
    const p = isoPoint(n),
      q = ((x - p.x) / 52) ** 2 + ((y - p.y) / 27) ** 2;
    if (q < d && q < 0.82) {
      d = q;
      best = n;
    }
  }
  return best;
}
function hamlet(c, s, t) {
  sky(c, "#cce2db", "#b1d0cb");
  cloud(c, 120, 145, 0.9, "#eef1dc90");
  cloud(c, 850, 75, 0.8, "#eef1dc90");
  oval(c, 520, 452, 320, 45, "#365e5c1c");
  const sorted = Array.from({ length: 35 }, (_, i) => i).sort((a, b) => {
    const p = isoPoint(a),
      q = isoPoint(b);
    return p.y - q.y;
  });
  for (const n of sorted) {
    const p = isoPoint(n);
    path(
      c,
      [
        [p.x - 51, p.y],
        [p.x, p.y + 27],
        [p.x, p.y + 49],
        [p.x - 51, p.y + 22],
      ],
      "#789b8a",
    );
    path(
      c,
      [
        [p.x, p.y + 27],
        [p.x + 51, p.y],
        [p.x + 51, p.y + 22],
        [p.x, p.y + 49],
      ],
      "#648a7e",
    );
    path(
      c,
      [
        [p.x, p.y - 26],
        [p.x + 51, p.y],
        [p.x, p.y + 26],
        [p.x - 51, p.y],
      ],
      n === s.selected
        ? "#d9d7a4"
        : (n + Math.floor(n / 7)) % 2
          ? "#a9c49a"
          : "#b1ca9f",
      n === s.selected ? "#8e784e" : "#9ab58b",
      1,
    );
    const tile = s.tiles.find((v) => v.cell === n);
    if (tile) {
      if (tile.type === "tree") tree(c, p.x, p.y, 0.66);
      else
        house(
          c,
          p.x,
          p.y,
          0.7,
          tile.type === "workshop" ? "#6b8593" : "#bc7954",
        );
      if (tile.type === "workshop") {
        const q = (t * 0.3 + n * 0.1) % 1;
        oval(
          c,
          p.x + 18 + q * 10,
          p.y - 75 - q * 24,
          6 + q * 5,
          4 + q * 3,
          "#fbf3d08a",
        );
      }
    }
  }
  // Decorative residents take actual grid routes, avoiding occupied plots. Their
  // walks communicate an inhabited arrangement; no jobs or economy are implied.
  const occupied = new Set(s.tiles.map((tile) => tile.cell));
  const targets = s.tiles.filter((v) => v.type !== "tree").map((v) => v.cell);
  for (let i = 0; i < 4; i++) {
    const end = targets.length ? targets[i % targets.length] : 13 + i;
    const neighbours = (n) =>
      [n - 7, n + 7, n % 7 ? n - 1 : -1, n % 7 < 6 ? n + 1 : -1].filter(
        (v) => v >= 0 && v < 35 && !occupied.has(v),
      );
    const door = neighbours(end)[0] ?? end,
      start =
        [...Array(35).keys()].find((n) => !occupied.has(n) && n !== door) ??
        door;
    const pending = [[start]],
      seen = new Set([start]);
    let route = [start];
    while (pending.length) {
      const r = pending.shift(),
        n = r.at(-1);
      if (n === door) {
        route = r;
        break;
      }
      for (const next of neighbours(n)) {
        if (!seen.has(next)) {
          seen.add(next);
          pending.push([...r, next]);
        }
      }
    }
    const at = (t * 0.7 + i * 2) % (route.length + 2),
      k = Math.min(route.length - 1, Math.floor(at)),
      a = isoPoint(route[k]),
      b = isoPoint(route[Math.min(k + 1, route.length - 1)]),
      u = at - k;
    person(c, a.x + (b.x - a.x) * u, a.y + (b.y - a.y) * u + 5, t, i, 0.42);
  }
  text(c, "MAKE A PLACE YOU WANT TO STAY", 480, 505, 15, "#42634f", "center");
}

export function drawScene(c, s, time, ui = {}) {
  const t = ui.reducedMotion ? 0 : time;
  c.save();
  c.clearRect(0, 0, 960, 540);
  c.lineJoin = "round";
  ({
    council,
    post,
    water,
    harvest,
    flock,
    factory,
    garden,
    evolution,
    orb,
    hamlet,
  })[s.id](c, s, t, ui);
  if (!ui.reducedMotion)
    for (const e of ui.effects ?? []) {
      const age = time - e.at;
      if (age < 0 || age > 0.75) continue;
      c.globalAlpha = 1 - age / 0.75;
      for (let i = 0; i < 9; i++) {
        const angle = (i / 9) * TAU,
          rad = age * 80;
        oval(
          c,
          e.x + Math.cos(angle) * rad,
          e.y + Math.sin(angle) * rad - age * 20,
          2.5,
          2.5,
          e.color ?? "#f1cf76",
        );
      }
      c.globalAlpha = 1;
    }
  c.restore();
}
