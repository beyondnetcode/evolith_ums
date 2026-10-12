import React from 'react';
import { Info } from 'lucide-react';
import { M3Card } from './M3Card';
import { Tooltip } from './Tooltip';

/**
 * EmptyState — standardised "no data" placeholder.
 *
 * Two visual variants:
 *   - "dashed": border-dashed container (for inline sections like branch lists)
 *   - "card":   elevated M3Card with icon circle (for full-panel empty states)
 */

export interface EmptyStateProps {
  /** Icon rendered above the message. Defaults to <Info />. Pass `null` to hide. */
  icon?: React.ReactNode | null;
  /** Optional title (shown only in "card" variant). */
  title?: string;
  /** Main descriptive message. */
  message: string;
  /** Visual style. @default "dashed" */
  variant?: 'dashed' | 'card';
  /** Optional tooltip text to show an Info icon next to the title. */
  tooltip?: string;
  /** Extra Tailwind classes. */
  className?: string;
}

export const EmptyState: React.FC<EmptyStateProps> = React.memo(
  ({ icon, title, message, variant = 'dashed', tooltip, className = '' }) => {
    const defaultIcon = <Info className="w-5 h-5" />;
    const iconNode = icon === null ? null : (icon ?? defaultIcon);

    if (variant === 'card') {
      return (
        <M3Card
          variant="elevated"
          className={`p-6 flex flex-col items-center justify-center text-center border border-m3-outline/20 bg-m3-surface-container/10 ${className}`}
        >
          {iconNode && (
            <div className="w-10 h-10 bg-m3-primary/10 border border-m3-primary/20 text-m3-primary rounded-full flex items-center justify-center mb-3">
              {iconNode}
            </div>
          )}
          {title && (
            <div className="flex items-center justify-center gap-1.5 mb-1">
              <h4 className="text-[12px] font-medium text-m3-on-surface">{title}</h4>
              {tooltip && (
                <Tooltip content={tooltip}>
                  <Info className="w-3.5 h-3.5 text-m3-secondary/60 hover:text-m3-primary cursor-help transition-colors" />
                </Tooltip>
              )}
            </div>
          )}
          <p className="text-[12px] text-m3-secondary/70 leading-relaxed max-w-xs">{message}</p>
        </M3Card>
      );
    }

    // "dashed" variant — lightweight inline empty state
    return (
      <div
        className={`py-6 px-4 flex flex-col items-center justify-center gap-2 border border-dashed border-m3-outline/20 rounded-xl ${className}`}
      >
        {iconNode && <div className="text-m3-secondary/40 mb-1">{iconNode}</div>}
        <p className="text-xs text-m3-secondary/60 text-center leading-relaxed">{message}</p>
      </div>
    );
  }
);
