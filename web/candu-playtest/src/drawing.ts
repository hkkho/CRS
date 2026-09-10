import Phaser from "phaser";

export const COLORS = {
  void: 0x070b1b,
  navy: 0x0b1026,
  indigo: 0x15183b,
  indigoLight: 0x25245a,
  panel: 0x111733,
  panelRaised: 0x191b45,
  ink: 0x090d20,
  ivory: 0xf4eee0,
  ivoryMuted: 0xbeb8c9,
  gold: 0xf2c66d,
  goldSoft: 0xc49a4c,
  cyan: 0x58e2df,
  cyanDark: 0x1c7c94,
  magenta: 0xd75bd9,
  magentaDark: 0x703c83,
  red: 0xf35d79,
  green: 0x65e4aa,
  grid: 0x445184,
  white: 0xffffff,
} as const;

export const FONTS = {
  display: "Georgia, Palatino Linotype, serif",
  body: "Trebuchet MS, Segoe UI, sans-serif",
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
    caption.setColor(colorString(enabled ? COLORS.ivory : COLORS.ivoryMuted));
  };
  repaint();

  container.setInteractive(
    new Phaser.Geom.Rectangle(-width / 2, -height / 2, width, height),
    Phaser.Geom.Rectangle.Contains,
  );
  container.on("pointerover", () => { hovered = true; repaint(); });
  container.on("pointerout", () => { hovered = false; repaint(); });
  container.on("pointerdown", () => { if (enabled) onClick(); });

  return {
    gameObject: container,
    setEnabled: (value: boolean) => { enabled = value; repaint(); },
    setLabel: (value: string) => { currentLabel = value; caption.setText(currentLabel); },
  };
}

export function drawMeter(
  graphics: Phaser.GameObjects.Graphics,
  x: number,
  y: number,
  width: number,
  height: number,
  value: number,
  color: number,
): void {
  const clamped = Phaser.Math.Clamp(value, 0, 1);
  graphics.fillStyle(COLORS.ink, 0.85);
  graphics.fillRoundedRect(x, y, width, height, height / 2);
  graphics.fillStyle(color, 0.88);
  graphics.fillRoundedRect(x, y, width * clamped, height, height / 2);
  graphics.lineStyle(1, COLORS.ivoryMuted, 0.22);
  graphics.strokeRoundedRect(x, y, width, height, height / 2);
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
