/**
 * Black or white, whichever stays readable on the given background.
 *
 * Route type colours are user-chosen and range from near-black to bright
 * yellow, so a fixed text colour is unreadable on part of the range. Uses the
 * WCAG relative-luminance threshold.
 */
export function contrastTextColour(background: string | null | undefined): string {
  const rgb = parseColour(background);
  if (!rgb) {
    return "inherit";
  }
  const [r, g, b] = rgb.map((channel) => {
    const c = channel / 255;
    return c <= 0.03928 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4);
  });
  const luminance = 0.2126 * r + 0.7152 * g + 0.0722 * b;
  return luminance > 0.179 ? "#000000" : "#ffffff";
}

function parseColour(value: string | null | undefined): [number, number, number] | null {
  if (!value) {
    return null;
  }
  const hex = value.trim().replace(/^#/, "");
  const expanded = hex.length === 3 ? hex.split("").map((c) => c + c).join("") : hex;
  if (!/^[0-9a-f]{6}$/i.test(expanded)) {
    return null;
  }
  return [
    parseInt(expanded.slice(0, 2), 16),
    parseInt(expanded.slice(2, 4), 16),
    parseInt(expanded.slice(4, 6), 16),
  ];
}
