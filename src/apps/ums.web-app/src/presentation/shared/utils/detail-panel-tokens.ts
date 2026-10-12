/**
 * DetailPanel Design Tokens
 *
 * Standardized styles for all detail panels to ensure consistent UI.
 * Following Material Design 3 principles with minimal, clean aesthetics.
 */

/** Outer container padding and spacing */
export const DETAIL_PANEL_STYLES = {
  container: 'p-4 space-y-4',
  section: 'rounded-xl border border-m3-outline/10 bg-m3-surface-container/10 p-4',
} as const;

/** Label styles - uppercase, small, secondary */
export const DETAIL_LABEL_STYLES = {
  wrapper: 'flex items-center gap-2',
  text: 'text-[10px] font-semibold uppercase tracking-wider text-m3-secondary',
} as const;

/** Value styles - normal weight, readable size */
export const DETAIL_VALUE_STYLES = {
  text: 'text-sm font-medium text-m3-on-surface',
  small: 'text-xs text-m3-secondary',
} as const;

/** Section header styles */
export const DETAIL_SECTION_STYLES = {
  title: 'text-xs font-semibold text-m3-on-surface',
  subtitle: 'text-[10px] text-m3-secondary font-normal',
} as const;

/** Icon sizes */
export const DETAIL_ICON_SIZES = {
  inline: 'w-4 h-4',
  featured: 'w-5 h-5',
} as const;

/** Button variants for detail panels */
export const DETAIL_BUTTON_VARIANTS = {
  primary: 'filled' as const,
  secondary: 'outlined' as const,
  tertiary: 'tonal' as const,
  text: 'text' as const,
} as const;

/** Standard spacing scale */
export const DETAIL_SPACING = {
  xs: 'gap-1',
  sm: 'gap-2',
  md: 'gap-3',
  lg: 'gap-4',
} as const;

/** Standard padding scale */
export const DETAIL_PADDING = {
  sm: 'p-2',
  md: 'p-3',
  lg: 'p-4',
} as const;
