import React, { useState } from 'react';

/**
 * M3FieldsetWrapper — MD3 outlined field container with notched label.
 *
 * Encapsulates the <fieldset> + <legend> notch pattern shared by
 * M3TextField, M3Select, and custom field wrappers (color picker,
 * toggle switches). The consumer provides the inner content via children.
 *
 * The notch is always open (label permanently floated) since this
 * wrapper is intended for fields that always display a value.
 */

export interface M3FieldsetWrapperProps {
  /** Label text shown in the notch. */
  label: string;
  /** Whether to use compact height (48dp) or standard (56dp). @default false */
  compact?: boolean;
  /** Whether there is an error state on this field. */
  error?: boolean;
  /** Extra Tailwind classes on the root div. */
  className?: string;
  /** Inner content (input, toggle, color picker, etc.). */
  children: React.ReactNode;
  /** Called when inner area receives focus. */
  onFocus?: () => void;
  /** Called when inner area loses focus. */
  onBlur?: () => void;
}

export const M3FieldsetWrapper: React.FC<M3FieldsetWrapperProps> = ({
  label,
  compact = false,
  error = false,
  className = '',
  children,
  onFocus,
  onBlur,
}) => {
  const [focused, setFocused] = useState(false);

  const handleFocus = () => {
    setFocused(true);
    onFocus?.();
  };
  const handleBlur = () => {
    setFocused(false);
    onBlur?.();
  };

  const borderClass = focused
    ? `border-2 ${error ? 'border-m3-error' : 'border-m3-primary'}`
    : error
      ? 'border border-m3-error'
      : 'border border-m3-outline hover:border-m3-on-surface';

  const labelColorClass = focused
    ? error
      ? 'text-m3-error'
      : 'text-m3-primary'
    : error
      ? 'text-m3-error'
      : 'text-m3-secondary';

  return (
    <div className={`relative ${className}`} onFocus={handleFocus} onBlur={handleBlur}>
      <fieldset
        className={[
          `relative w-full ${compact ? 'h-12' : 'h-14'} m-0 p-0 min-w-0 rounded-[4px]`,
          'bg-m3-surface-container/30 dark:bg-m3-surface-container/20',
          'transition-colors duration-150',
          borderClass,
        ].join(' ')}
      >
        <legend
          aria-hidden="true"
          className="ml-3 h-0 overflow-hidden whitespace-nowrap text-xs opacity-0 pointer-events-none select-none"
          style={{ maxWidth: '1000px', padding: '0 4px' }}
        >
          {label}
        </legend>

        <label
          className={[
            'absolute left-3 px-1 top-0 -translate-y-1/2',
            'pointer-events-none text-xs font-normal',
            'transition-colors duration-150',
            labelColorClass,
          ].join(' ')}
        >
          {label}
        </label>

        <div className="absolute inset-0 px-4 flex items-center">{children}</div>
      </fieldset>
    </div>
  );
};
