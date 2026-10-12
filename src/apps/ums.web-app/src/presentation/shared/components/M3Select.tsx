import React, { useState, useId } from 'react';
import { ChevronDown } from 'lucide-react';

interface M3SelectProps extends React.SelectHTMLAttributes<HTMLSelectElement> {
  label: string;
  error?: string;
  helperText?: string;
  /** compact – 48 dp height instead of standard 56 dp */
  compact?: boolean;
}

/**
 * MD3 Outlined Select
 *
 * Uses the same <fieldset>/<legend> notch technique as M3TextField.
 * A <select> always displays a selected value so the label is permanently
 * in the floated position — the notch is always open.
 *
 * Spec references:
 *   • Height           56 dp  (h-14)
 *   • Corner radius     4 dp
 *   • Border            1 px  resting / hover; 2 px focused
 *   • Label floating    Body Small  12 sp → text-xs
 */
export const M3Select: React.FC<M3SelectProps> = ({
  label,
  error,
  helperText,
  compact = false,
  className = '',
  id,
  children,
  onFocus: extOnFocus,
  onBlur: extOnBlur,
  ...props
}) => {
  const autoId = useId();
  const selectId = id || autoId;
  const hasMbClass = /\bmb-/.test(className);
  const defaultMb = compact ? 'mb-3' : 'mb-4';

  const [focused, setFocused] = useState(false);

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
    <div className={`relative w-full ${hasMbClass ? '' : defaultMb} ${className}`}>
      {/*
        Selects always show a value, so the label is always floated.
        The notch is always open (max-width: 1000px).
      */}
      <fieldset
        className={[
          `relative w-full ${compact ? 'h-12' : 'h-14'} m-0 p-0 min-w-0 rounded-[4px]`,
          'bg-m3-surface-container/30 dark:bg-m3-surface-container/20',
          'transition-colors duration-150',
          borderClass,
        ].join(' ')}
      >
        {/* Always-open notch */}
        <legend
          aria-hidden="true"
          className="ml-3 h-0 overflow-hidden whitespace-nowrap text-xs opacity-0 pointer-events-none select-none"
          style={{ maxWidth: '1000px', padding: '0 4px' }}
        >
          {label}
        </legend>

        <select
          id={selectId}
          onFocus={e => {
            setFocused(true);
            extOnFocus?.(e);
          }}
          onBlur={e => {
            setFocused(false);
            extOnBlur?.(e);
          }}
          className={[
            'absolute inset-0 w-full h-full bg-transparent rounded-[4px]',
            `pl-4 pr-10 ${compact ? 'pt-4 pb-1' : 'pt-5 pb-2'} text-sm text-m3-on-surface`,
            'focus:outline-none appearance-none cursor-pointer',
            'disabled:opacity-50 disabled:cursor-not-allowed',
          ].join(' ')}
          {...props}
        >
          {children}
        </select>

        {/* Floated label — always in notch position */}
        <label
          htmlFor={selectId}
          className={[
            'absolute left-3 px-1 top-0 -translate-y-1/2',
            'pointer-events-none text-xs font-normal',
            'transition-colors duration-150',
            labelColorClass,
          ].join(' ')}
        >
          {label}
        </label>

        {/* Dropdown chevron */}
        <div className="absolute right-3 top-1/2 -translate-y-1/2 pointer-events-none text-m3-secondary">
          <ChevronDown className="w-4 h-4" />
        </div>
      </fieldset>

      {(error || helperText) && (
        <span
          className={`block text-xs mt-1 ml-4 ${error ? 'text-m3-error' : 'text-m3-secondary/75'}`}
        >
          {error || helperText}
        </span>
      )}
    </div>
  );
};
