import React from 'react';

/**
 * SectionHeader — consistent section title bar with optional actions.
 *
 * Used inside detail panels (BranchManager, IdpPanel)
 * to provide a uniform header with bottom border, title, subtitle,
 * and an action slot.
 */

export interface SectionHeaderProps {
  /** Section title. */
  title: string;
  /** Optional subtitle shown below the title. */
  subtitle?: string;
  /** Action buttons / badges rendered on the right. */
  actions?: React.ReactNode;
  /** Extra Tailwind classes. */
  className?: string;
}

export const SectionHeader: React.FC<SectionHeaderProps> = React.memo(
  ({ title, subtitle, actions, className = '' }) => (
    <div
      className={`flex justify-between items-center pb-3 border-b border-m3-outline/15 ${className}`}
    >
      <div>
        <h3 className="text-[12px] font-semibold text-m3-on-surface uppercase tracking-wide">
          {title}
        </h3>
        {subtitle && <p className="text-[11px] text-m3-secondary mt-0.5">{subtitle}</p>}
      </div>
      {actions && <div className="flex items-center gap-2 shrink-0">{actions}</div>}
    </div>
  )
);
