import React from 'react';

/**
 * CodeBadge — small monospace chip for entity codes (tenant code, branch code, etc.)
 *
 * Provides a consistent visual treatment for short identifier strings
 * across list rows, profile cards, and detail panels.
 */

export interface CodeBadgeProps {
  /** The code string to display. */
  code: string;
  /** Size variant. @default "sm" */
  size?: 'xs' | 'sm';
  /** Extra Tailwind classes. */
  className?: string;
}

const SIZE_CLASSES: Record<string, string> = {
  xs: 'text-[10px] px-1.5 py-0.5',
  sm: 'text-[11px] px-2 py-0.5',
};

export const CodeBadge: React.FC<CodeBadgeProps> = React.memo(
  ({ code, size = 'sm', className = '' }) => (
    <span
      className={`font-medium rounded bg-m3-outline/30 text-m3-secondary font-mono ${SIZE_CLASSES[size]} ${className}`}
    >
      {code}
    </span>
  )
);
