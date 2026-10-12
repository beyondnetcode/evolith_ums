import React, { useState, useId } from 'react';

interface M3TextFieldProps extends React.InputHTMLAttributes<HTMLInputElement> {
  label: string;
  error?: string;
  helperText?: string;
  icon?: React.ReactNode;
  iconPosition?: 'start' | 'end';
  /**
   * compact – renders a 48 dp field instead of the standard 56 dp.
   * Use in dense settings panels or toolbars where vertical space is limited.
   */
  compact?: boolean;
  /** dense – renders a 40 dp field for compact search and filter toolbars. */
  dense?: boolean;
}

/**
 * MD3 Outlined Text Field
 *
 * Uses <fieldset> + <legend> so the browser natively notches the top border
 * for the floating label — no background-color trick needed, works correctly
 * on every container regardless of its background.
 *
 * Spec references:
 *   • Height           56 dp  (h-14)
 *   • Corner radius     4 dp  (rounded-[4px])
 *   • Border            1 px  resting / hover; 2 px focused (MD3 §Text fields)
 *   • Label resting     Body Large  14 sp → text-sm
 *   • Label floating    Body Small  12 sp → text-xs
 *   • Supporting text   Body Small  12 sp → text-xs
 *   • Horizontal pad   16 dp  (px-4)
 */
export const M3TextField: React.FC<M3TextFieldProps> = ({
  label,
  error,
  helperText,
  icon,
  iconPosition = 'start',
  compact = false,
  dense = false,
  className = '',
  id,
  ...props
}) => {
  const {
    placeholder: hint,
    required: req,
    value,
    defaultValue,
    onFocus: extOnFocus,
    onBlur: extOnBlur,
    ...inputProps
  } = props;

  const autoId = useId();
  const inputId = id || autoId;
  const hasMbClass = /\bmb-/.test(className);
  const defaultMb = compact || dense ? 'mb-3' : 'mb-4';
  const fieldHeightClass = dense ? 'h-10' : compact ? 'h-12' : 'h-14';
  const inputPaddingClass = dense ? 'px-3' : 'px-4';
  const inputSpacingClass = dense
    ? 'pt-3 pb-1 text-xs'
    : `${compact ? 'pt-4 pb-1' : 'pt-5 pb-2'} text-sm`;
  const hasStartIcon = !!icon && iconPosition === 'start';
  const startIconInsetClass = dense ? 'left-2.5' : 'left-3';
  const labelStartOffsetClass = hasStartIcon
    ? dense
      ? 'left-9'
      : 'left-10'
    : dense
      ? 'left-3'
      : 'left-4';
  const hintStartOffsetClass = hasStartIcon
    ? dense
      ? 'left-9 right-3'
      : 'left-10 right-4'
    : dense
      ? 'left-3 right-3'
      : 'left-4 right-4';

  const [focused, setFocused] = useState(false);

  // Controlled: derive from value prop; uncontrolled: derive from defaultValue
  const hasValue =
    value !== undefined
      ? String(value).length > 0
      : defaultValue !== undefined && String(defaultValue).length > 0;

  const alwaysFloatTypes = ['date', 'datetime-local', 'time', 'month', 'week'];
  const isFloated = focused || hasValue || alwaysFloatTypes.includes(inputProps.type || '');

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
        <fieldset> draws the outline; <legend> tells the browser to leave a
        transparent gap (the "notch") in the top border exactly as wide as
        the legend content. We animate max-width to grow/shrink the notch in
        sync with the floating label. The label itself is absolutely positioned
        on top and does NOT need a background color — the gap IS transparent.
      */}
      <fieldset
        className={[
          `relative w-full ${fieldHeightClass} m-0 p-0 min-w-0 rounded-[4px]`,
          'bg-m3-surface-container/30 dark:bg-m3-surface-container/20',
          'transition-colors duration-150',
          borderClass,
        ].join(' ')}
      >
        {/* Invisible legend — creates the notch; rendered at text-xs to match floated label */}
        <legend
          aria-hidden="true"
          className="ml-3 h-0 overflow-hidden whitespace-nowrap text-xs opacity-0 pointer-events-none select-none"
          style={{
            maxWidth: isFloated ? '1000px' : '0.01px',
            padding: isFloated ? '0 4px' : '0',
            transition: 'max-width 150ms ease-in-out, padding 150ms ease-in-out',
          }}
        >
          {label}
          {req && ' *'}
        </legend>

        {hasStartIcon && (
          <div
            className={`absolute ${startIconInsetClass} top-1/2 -translate-y-1/2 text-m3-secondary pointer-events-none z-10`}
          >
            {icon}
          </div>
        )}

        <input
          id={inputId}
          value={value}
          defaultValue={defaultValue}
          required={req}
          onFocus={e => {
            setFocused(true);
            extOnFocus?.(e);
          }}
          onBlur={e => {
            setFocused(false);
            extOnBlur?.(e);
          }}
          className={[
            'peer absolute inset-0 w-full h-full bg-transparent rounded-[4px]',
            `${inputPaddingClass} ${inputSpacingClass} text-m3-on-surface`,
            compact ? 'text-[11px]' : '',
            'focus:outline-none',
            'disabled:opacity-50 disabled:cursor-not-allowed',
            hasStartIcon ? 'pl-10' : '',
            icon && iconPosition === 'end' ? 'pr-10' : '',
          ].join(' ')}
          {...inputProps}
        />

        {/* Floating label — sits in the notch when floated, centred vertically when resting */}
        <label
          htmlFor={inputId}
          className={[
            'absolute pointer-events-none font-normal',
            'transition-all duration-150 ease-in-out',
            isFloated
              ? `${labelStartOffsetClass} px-1 top-0 -translate-y-1/2 text-xs`
              : `${labelStartOffsetClass} ${dense ? 'text-xs' : 'text-sm'} top-1/2 -translate-y-1/2`,
            labelColorClass,
          ].join(' ')}
        >
          {label}
          {req && <span className="ml-0.5 text-m3-error">*</span>}
        </label>

        {/* Supporting hint — visible only when focused and empty */}
        {hint && (
          <span
            className={[
              `absolute ${hintStartOffsetClass} ${dense ? 'text-xs' : 'text-sm'} text-m3-secondary/50`,
              'pointer-events-none select-none transition-opacity duration-150',
              focused && !hasValue ? 'opacity-100' : 'opacity-0',
            ].join(' ')}
            style={{ top: '62%', transform: 'translateY(-50%)' }}
          >
            {hint}
          </span>
        )}

        {icon && iconPosition === 'end' && (
          <div className="absolute right-3 top-1/2 -translate-y-1/2 text-m3-secondary pointer-events-none z-10">
            {icon}
          </div>
        )}
      </fieldset>

      {/* Supporting text (error | helper) — 16 dp left indent per MD3 */}
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
