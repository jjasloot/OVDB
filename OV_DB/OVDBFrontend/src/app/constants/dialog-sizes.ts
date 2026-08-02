/**
 * Dialog sizing used across the app.
 *
 * Percentage widths do not work on both ends: "50%" is unusably narrow on a
 * phone and sprawls on a wide monitor. These fill most of a small screen and
 * cap out at a readable width instead.
 */
export const STANDARD_DIALOG = { width: "90vw", maxWidth: "600px" } as const;

/** For dialogs with a form grid, table, or map inside. */
export const WIDE_DIALOG = { width: "90vw", maxWidth: "1000px" } as const;

/** For the operator editor, which shows several region pickers side by side. */
export const EXTRA_WIDE_DIALOG = { width: "95vw", maxWidth: "1200px" } as const;
