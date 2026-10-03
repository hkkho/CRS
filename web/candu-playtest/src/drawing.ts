import Phaser from "phaser";

export const COLORS = {
  void: 0x07100b,
  navy: 0x0b1911,
  indigo: 0x102419,
  indigoLight: 0x254830,
  panel: 0x0b1911,
  panelRaised: 0x183321,
  ink: 0x07100b,
  ivory: 0xb5edb0,
  ivoryMuted: 0x8fba93,
  gold: 0xf2c66d,
  goldSoft: 0xc49a4c,
  cyan: 0xa8eda3,
  cyanDark: 0x42784d,
  magenta: 0xffc56c,
  magentaDark: 0x88652d,
  red: 0xf35d79,
  green: 0x65e4aa,
  grid: 0x315b3b,
  white: 0xffffff,
} as const;

export const FONTS = {
  display: "Consolas, Courier New, monospace",
  body: "Consolas, Courier New, monospace",
  mono: "Consolas, SFMono-Regular, monospace",
} as const;

export interface TacticalButton {
  gameObject: Phaser.GameObjects.Container;
  setEnabled: (enabled: boolean) => void;
  setLabel: (label: string) => void;
}

export interface ButtonOptions {
  tone?: "gold" | "cyan" | "magenta" | "quiet" | "danger";
  fontSize?: number;
  compact?: boolean;
}

export function makeText(
  scene: Phaser.Scene,
  x: number,
  y: number,
  value: string,
  style: Phaser.Types.GameObjects.Text.TextStyle = {},
): Phaser.GameObjects.Text {
  return scene.add.text(x, y, value, {
    fontFamily: FONTS.body,
    fontSize: "16px",
    color: colorString(COLORS.ivory),
    resolution: 1,
    ...style,
  });
}

export function drawPanelFrame(
  graphics: Phaser.GameObjects.Graphics,
  x: number,
  y: number,
  width: number,
  height: number,
  options: { alpha?: number; accent?: number; fill?: number; lineWidth?: number } = {},
): void {
  const accent = options.accent ?? COLORS.gold;
  graphics.fillStyle(options.fill ?? COLORS.panel, options.alpha ?? 0.96);
  graphics.fillRoundedRect(x, y, width, height, 8);
  graphics.lineStyle(options.lineWidth ?? 1.5, accent, 0.76);
  graphics.strokeRoundedRect(x, y, width, height, 8);
  graphics.lineStyle(1, COLORS.ivoryMuted, 0.14);
  graphics.strokeRoundedRect(x + 6, y + 6, width - 12, height - 12, 5);
  drawCornerBrackets(graphics, x, y, width, height, accent);
}

export function drawCornerBrackets(
  graphics: Phaser.GameObjects.Graphics,
  x: number,
  y: number,
  width: number,
  height: number,
  color: number,
): void {
  const length = 12;
  graphics.lineStyle(2, color, 0.9);
  graphics.lineBetween(x + 1, y + length, x + 1, y + 1);
  graphics.lineBetween(x + 1, y + 1, x + length, y + 1);
  graphics.lineBetween(x + width - length, y + 1, x + width - 1, y + 1);
  graphics.lineBetween(x + width - 1, y + 1, x + width - 1, y + length);
  graphics.lineBetween(x + 1, y + height - length, x + 1, y + height - 1);
  graphics.lineBetween(x + 1, y + height - 1, x + length, y + height - 1);
  graphics.lineBetween(x + width - length, y + height - 1, x + width - 1, y + height - 1);
  graphics.lineBetween(x + width - 1, y + height - length, x + width - 1, y + height - 1);
}

export function makeButton(
  scene: Phaser.Scene,
  x: number,
  y: number,
  width: number,
  height: number,
  label: string,
  onClick: () => void,
  options: ButtonOptions = {},
): TacticalButton {
  let enabled = true;
  let hovered = false;
  let currentLabel = label;
  const container = scene.add.container(x, y);
  const background = scene.add.graphics();
  const caption = makeText(scene, 0, 0, label, {
    fontFamily: FONTS.mono,
    fontSize: `${options.fontSize ?? (options.compact ? 12 : 14)}px`,
    color: colorString(COLORS.ivory),
    fontStyle: "bold",
    letterSpacing: options.compact ? 0.5 : 1,
  });
  caption.setOrigin(0.5);
  container.add([background, caption]);
  container.setSize(width, height);

  const toneColor = (): number => {
    switch (options.tone) {
      case "cyan": return COLORS.cyan;
      case "magenta": return COLORS.magenta;
      case "quiet": return COLORS.indigoLight;
      case "danger": return COLORS.red;
      default: return COLORS.gold;
    }
  };
  const repaint = (): void => {
    const accent = toneColor();
    background.clear();
    background.fillStyle(enabled ? (hovered ? accent : COLORS.panelRaised) : COLORS.indigo, enabled ? 0.98 : 0.52);
    background.fillRoundedRect(-width / 2, -height / 2, width, height, options.compact ? 5 : 7);
    background.lineStyle(enabled ? (hovered ? 2 : 1.25) : 1, accent, enabled ? 0.92 : 0.28);
    background.strokeRoundedRect(-width / 2, -height / 2, width, height, options.compact ? 5 : 7);
    if (hovered && enabled) {
      background.fillStyle(COLORS.white, 0.06);
      background.fillRoundedRect(-width / 2 + 2, -height / 2 + 2, width - 4, height * 0.42, 4);
    }
    caption.setColor(colorString(enabled && hovered ? COLORS.ink : enabled ? COLORS.ivory : COLORS.ivoryMuted));
  };
  repaint();

  const hitHeight = Math.max(height, 38);
  container.setInteractive(
    new Phaser.Geom.Rectangle(0, (height - hitHeight) / 2, width, hitHeight),
    Phaser.Geom.Rectangle.Contains,
  );
  container.input!.cursor = "pointer";
  container.on("pointerover", () => { hovered = true; repaint(); });
  container.on("pointerout", () => { hovered = false; repaint(); });
  container.on("pointerup", () => { if (enabled) onClick(); });

  return {
    gameObject: container,
    setEnabled: (value: boolean) => { enabled = value; container.input!.cursor = enabled ? "pointer" : "default"; repaint(); },
    setLabel: (value: string) => { currentLabel = value; caption.setText(currentLabel); },
  };
}

export function colorString(value: number): string {
  return `#${value.toString(16).padStart(6, "0")}`;
}

export function colorFromRgb(value: string): number {
  const channels = value.match(/\d+/g);
  if (channels === null || channels.length < 3) {
    return COLORS.indigo;
  }
  return (Number(channels[0]) << 16) | (Number(channels[1]) << 8) | Number(channels[2]);
}

export function mixColor(left: number, right: number, amount: number): number {
  const t = Phaser.Math.Clamp(amount, 0, 1);
  const r = Math.round(((left >> 16) & 0xff) + (((right >> 16) & 0xff) - ((left >> 16) & 0xff)) * t);
  const g = Math.round(((left >> 8) & 0xff) + (((right >> 8) & 0xff) - ((left >> 8) & 0xff)) * t);
  const b = Math.round((left & 0xff) + ((right & 0xff) - (left & 0xff)) * t);
  return (r << 16) | (g << 8) | b;
}
