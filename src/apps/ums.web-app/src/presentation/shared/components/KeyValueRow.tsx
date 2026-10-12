import React from 'react';

/**
 * KeyValueRow — horizontal label + value display row.
 *
 * Used in profile/diagnostics screens where metadata is displayed
 * in a vertical list of key-value pairs separated by bottom borders.
 */

export interface KeyValueRowProps {
  /** Optional icon rendered before the label. */
  icon?: React.ReactNode;
  /** The label (left side). */
  label: string;
  /** The value (right side) — can be text or a rendered badge/chip. */
  value: React.ReactNode;
  /** Whether to show the bottom border. @default true */
  bordered?: boolean;
  /** Extra Tailwind classes on the root element. */
  className?: string;
}

export const KeyValueRow: React.FC<KeyValueRowProps> = React.memo(
  ({ icon, label, value, bordered = true, className = '' }) => (
    <div
      className={[
        'flex justify-between items-center',
        bordered ? 'border-b border-m3-outline/20 pb-2.5' : 'pb-1',
        className,
      ].join(' ')}
    >
      <span className="text-m3-secondary font-bold uppercase tracking-wider text-[10px] flex items-center gap-1.5">
        {icon} {label}
      </span>
      <span className="font-extrabold text-m3-on-surface text-xs">{value}</span>
    </div>
  )
);
