/**
 * Design System — Unified tokens for all UI elements
 *
 * These tokens ensure consistent visual design across the entire application.
 * Use these classes instead of hardcoding values to maintain uniformity.
 */

// ═══════════════════════════════════════════════════════════════════════════
// SPACING — Standard padding/margin scale
// ═══════════════════════════════════════════════════════════════════════════

export const SPACING = {
  /** Compact padding (cards, panels) */
  compact: 'p-3',
  /** Standard padding (cards, panels) */
  standard: 'p-4',
  /** Large padding (cards, modals) */
  large: 'p-5',
  /** Extra large padding */
  xl: 'p-6',
} as const;

export const GAP = {
  xs: 'gap-1',
  sm: 'gap-2',
  md: 'gap-3',
  lg: 'gap-4',
  xl: 'gap-5',
} as const;

// ═══════════════════════════════════════════════════════════════════════════
// TYPOGRAPHY — Text sizing and weights
// ═══════════════════════════════════════════════════════════════════════════

export const TEXT = {
  /** Page/section titles */
  title: 'text-base font-semibold text-m3-on-surface',
  /** Card/section headers */
  header: 'text-sm font-semibold text-m3-on-surface',
  /** Primary content text */
  body: 'text-sm text-m3-on-surface',
  /** Secondary/supporting text */
  secondary: 'text-xs text-m3-secondary',
  /** Small labels, badges */
  label: 'text-[10px] font-medium text-m3-secondary',
  /** Uppercase section labels */
  labelUpper: 'text-[10px] font-semibold uppercase tracking-wider text-m3-secondary',
  /** Extra small text */
  xs: 'text-[9px] text-m3-secondary',
  /** Code/monospace text */
  code: 'text-xs font-mono text-m3-secondary',
} as const;

// ═══════════════════════════════════════════════════════════════════════════
// BADGES — Status and code badge sizing
// ═══════════════════════════════════════════════════════════════════════════

export const BADGE = {
  /** Standard badge (StatusBadge default) */
  standard: 'text-[10px] font-medium px-2.5 py-0.5 rounded-full border',
  /** Small badge (CodeBadge xs) */
  small: 'text-[9px] font-medium px-1.5 py-0.5 rounded',
  /** Large badge variant */
  large: 'text-xs font-medium px-3 py-1 rounded-full border',
} as const;

export const BADGE_COLORS = {
  success: 'bg-emerald-500/10 border-emerald-500/25 text-emerald-500',
  warning: 'bg-amber-500/10 border-amber-500/25 text-amber-500',
  error: 'bg-rose-500/10 border-rose-500/25 text-rose-500',
  info: 'bg-blue-500/10 border-blue-500/25 text-blue-500',
  neutral: 'bg-m3-outline/10 border-m3-outline/25 text-m3-secondary',
  primary: 'bg-m3-primary/10 border-m3-primary/25 text-m3-primary',
} as const;

// ═══════════════════════════════════════════════════════════════════════════
// ICON CONTAINERS — Standard icon wrapper sizing
// ═══════════════════════════════════════════════════════════════════════════

export const ICON_CONTAINER = {
  /** Small icon wrapper (inline actions) */
  sm: 'p-1.5 rounded',
  /** Standard icon wrapper (list rows, cards) */
  md: 'p-2 rounded-lg',
  /** Large icon wrapper (featured icons, cards) */
  lg: 'p-2.5 rounded-lg',
  /** Extra large icon wrapper (empty states, heroes) */
  xl: 'p-3 rounded-xl',
} as const;

export const ICON_SIZE = {
  sm: 'w-3 h-3',
  md: 'w-4 h-4',
  lg: 'w-5 h-5',
  xl: 'w-6 h-6',
} as const;

// ═══════════════════════════════════════════════════════════════════════════
// CARDS — Standard card styling
// ═══════════════════════════════════════════════════════════════════════════

export const CARD = {
  /** Elevated card variant (default for detail panels) */
  elevated: 'rounded-xl border border-m3-outline/25 bg-m3-surface-container/20 shadow-sm',
  /** Filled card variant (alternative) */
  filled: 'rounded-xl border border-transparent bg-m3-surface-container/50',
  /** Outlined card variant */
  outlined: 'rounded-xl border border-m3-outline/30 bg-transparent',
  /** Standard padding */
  padding: 'p-4',
  /** Large padding */
  paddingLg: 'p-5',
} as const;

// ═══════════════════════════════════════════════════════════════════════════
// FORM CONTROLS — Input, select, textarea sizing
// ═══════════════════════════════════════════════════════════════════════════

export const FORM = {
  /** Standard input height */
  inputHeight: 'h-8',
  /** Compact input height (toolbar selects) */
  inputHeightCompact: 'h-6',
  /** Textarea height */
  textareaHeight: 'h-16',
  /** Standard input padding */
  inputPadding: 'px-3',
  /** Compact input padding (selects) */
  inputPaddingCompact: 'px-2.5',
  /** Standard text size */
  inputText: 'text-[12px]',
  /** Label style */
  label: 'text-[10px] font-medium text-m3-on-surface-variant uppercase tracking-wide',
  /** Error text */
  error: 'text-[9px] text-rose-500',
} as const;

// ═══════════════════════════════════════════════════════════════════════════
// BUTTONS — Standard button sizing and variants
// ═══════════════════════════════════════════════════════════════════════════

export const BUTTON = {
  /** Standard button height */
  height: 'h-10',
  /** Compact button height (form actions) */
  heightCompact: 'h-8',
  /** Small button height */
  heightSmall: 'h-7',
  /** Standard padding */
  padding: 'px-6 py-2.5',
  /** Compact padding */
  paddingCompact: 'px-4 py-2',
  /** Small padding */
  paddingSmall: 'px-3 py-1.5',
  /** Text size */
  text: 'text-sm font-medium',
  /** Small text */
  textSmall: 'text-[11px]',
  /** Extra small text (form buttons) */
  textXs: 'text-[10px]',
} as const;

// ═══════════════════════════════════════════════════════════════════════════
// ENTITY ROWS — Standard list row styling
// ═══════════════════════════════════════════════════════════════════════════

export const ROW = {
  /** Standard row padding */
  padding: 'px-3 py-2.5',
  /** Minimum row height */
  minHeight: 'min-h-[3.5rem]',
  /** Row gap between items */
  gap: 'my-0.5 mx-2',
  /** Border radius */
  radius: 'rounded-xl',
  /** Leading icon area width */
  leadingWidth: 'w-4',
} as const;

// ═══════════════════════════════════════════════════════════════════════════
// BORDERS — Standard border styling
// ═══════════════════════════════════════════════════════════════════════════

export const BORDER = {
  /** Subtle border (dividers, section separators) */
  subtle: 'border-m3-outline/10',
  /** Standard border (cards, inputs) */
  standard: 'border-m3-outline/25',
  /** Strong border (active states) */
  strong: 'border-m3-outline/40',
  /** Focus ring */
  focus: 'ring-1 ring-m3-primary/40',
  /** Divider (horizontal rules) */
  divider: 'border-b border-m3-outline/10',
} as const;

// ═══════════════════════════════════════════════════════════════════════════
// DETAIL PANEL — Standard detail panel section styling
// ═══════════════════════════════════════════════════════════════════════════

export const DETAIL = {
  /** Standard detail panel content wrapper */
  contentWrapper: 'p-4 space-y-4',
  /** Section with background and border */
  section: 'rounded-xl border border-m3-outline/10 bg-m3-surface-container/10 p-4',
  /** Section header (title + subtitle + actions) */
  sectionHeader: 'flex items-center justify-between gap-4',
  /** Section title */
  sectionTitle: 'text-xs font-semibold text-m3-on-surface',
  /** Section subtitle */
  sectionSubtitle: 'text-[10px] text-m3-secondary font-normal',
  /** Tab content area */
  tabContent: 'p-5 rounded-xl border border-m3-outline/25 bg-m3-surface-container/20 shadow-sm',
} as const;

// ═══════════════════════════════════════════════════════════════════════════
// LAYOUT — Common layout patterns
// ═══════════════════════════════════════════════════════════════════════════

export const LAYOUT = {
  /** Page content padding */
  pagePadding: 'p-4',
  /** Card content padding */
  cardPadding: 'p-5',
  /** Section spacing */
  sectionGap: 'gap-4',
  /** Grid gap */
  gridGap: 'gap-3',
  /** Toolbar height */
  toolbarHeight: 'h-10',
} as const;

// ═══════════════════════════════════════════════════════════════════════════
// ANIMATION — Standard transition timing
// ═══════════════════════════════════════════════════════════════════════════

export const ANIMATION = {
  /** Fast transition (hover states) */
  fast: 'transition-all duration-150',
  /** Standard transition (expand/collapse) */
  standard: 'transition-all duration-200',
  /** Slow transition (modals, drawers) */
  slow: 'transition-all duration-300',
} as const;
