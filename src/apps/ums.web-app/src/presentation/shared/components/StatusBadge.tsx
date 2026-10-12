import React from 'react';

// ─── Status-colour presets ──────────────────────────────────────────────────
// Consumers can supply their own map via `colorMap`; defaults cover the common
// Active / Suspended / Pending pattern used across most maintenance screens.

export interface StatusBadgeColorSet {
  bg: string;
  border: string;
  text: string;
}

const DEFAULT_COLOR_MAP: Record<string, StatusBadgeColorSet> = {
  Active: { bg: 'bg-emerald-500/10', border: 'border-emerald-500/25', text: 'text-emerald-500' },
  Suspended: { bg: 'bg-rose-500/10', border: 'border-rose-500/25', text: 'text-rose-500' },
  Pending: { bg: 'bg-amber-500/10', border: 'border-amber-500/25', text: 'text-amber-500' },
};

const FALLBACK: StatusBadgeColorSet = {
  bg: 'bg-m3-outline/10',
  border: 'border-m3-outline/25',
  text: 'text-m3-secondary',
};

// ─── Component ──────────────────────────────────────────────────────────────

export interface StatusBadgeProps {
  /** The raw status key (e.g. "Active", "Suspended"). */
  status: string;
  /** Human-readable label displayed inside the badge. Falls back to `status`. */
  label?: string;
  /** Optional domain-specific colour overrides keyed by status value. */
  colorMap?: Record<string, StatusBadgeColorSet>;
  /** Extra Tailwind classes forwarded to the root <span>. */
  className?: string;
}

export const StatusBadge: React.FC<StatusBadgeProps> = React.memo(
  ({ status, label, colorMap, className = '' }) => {
    const map = colorMap ?? DEFAULT_COLOR_MAP;
    const { bg, border, text } = map[status] ?? FALLBACK;

    return (
      <span
        data-testid="status-badge"
        data-status={status}
        className={`text-[10px] font-medium px-2.5 py-0.5 rounded-full border ${bg} ${border} ${text} ${className}`}
      >
        {label ?? status}
      </span>
    );
  }
);
