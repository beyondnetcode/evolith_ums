/**
 * Shared Form Components
 * Minimalist, professional design system for forms
 */
import React from 'react';

/**
 * Campo de formulario: etiqueta, control y error, ATADOS entre sí.
 *
 * La etiqueta no tenía `htmlFor` y el control llegaba por `children`, así que tampoco quedaba
 * envuelto: el campo NO tenía nombre accesible. Con lector de pantalla, 61 controles del árbol se
 * anunciaban como «cuadro de edición» y nada más. Ahora la etiqueta apunta al control y el control
 * recibe de vuelta su `id`, su `aria-invalid`, su `aria-required` y el `aria-describedby` del error.
 *
 * El error se anuncia además con `role="alert"`: escribirlo en pantalla no sirve de nada si quien
 * no lo ve nunca se entera de que apareció.
 *
 * El asterisco deja de ser el único portador del significado —color y glifo— y viaja con
 * `aria-required` y con texto para el lector (ADR-0228 §2.4).
 */
export const FormField = ({
  label,
  required,
  error,
  children,
  className = '',
}: {
  label: string;
  required?: boolean;
  error?: string;
  children: React.ReactNode;
  className?: string;
}) => {
  const idBase = React.useId();
  const idControl = `${idBase}-control`;
  const idError = `${idBase}-error`;

  // El control llega como `children`: se le inyecta lo que la etiqueta necesita para nombrarlo.
  const controlados = React.Children.map(children, hijo =>
    React.isValidElement(hijo)
      ? React.cloneElement(hijo as React.ReactElement<Record<string, unknown>>, {
          id: (hijo.props as { id?: string }).id ?? idControl,
          'aria-invalid': error ? true : undefined,
          'aria-required': required || undefined,
          'aria-describedby': error ? idError : undefined,
        })
      : hijo
  );

  return (
    <div className={`flex flex-col gap-1 ${className}`}>
      <label
        htmlFor={idControl}
        className="text-[10px] font-medium text-m3-on-surface-variant uppercase tracking-wide"
      >
        {label}
        {required && (
          <span className="text-m3-error ml-0.5" aria-hidden="true">
            *
          </span>
        )}
        {required && <span className="sr-only"> (obligatorio)</span>}
      </label>
      {controlados}
      {error && (
        <span id={idError} role="alert" className="text-[10px] text-m3-error">
          {error}
        </span>
      )}
    </div>
  );
};

export const FormInput = ({
  className = '',
  error,
  ...props
}: React.InputHTMLAttributes<HTMLInputElement> & { error?: boolean }) => (
  <input
    className={`h-8 px-3 text-[12px] border rounded-md bg-m3-surface focus:outline-none focus:ring-1 focus:ring-m3-primary/40 transition-shadow ${
      error ? 'border-m3-error' : 'border-m3-outline'
    } ${className}`}
    {...props}
  />
);

export const FormSelect = ({
  className = '',
  error,
  ...props
}: React.SelectHTMLAttributes<HTMLSelectElement> & { error?: boolean }) => (
  <select
    className={`h-8 px-2 text-[12px] border rounded-md bg-m3-surface focus:outline-none focus:ring-1 focus:ring-m3-primary/40 transition-shadow ${
      error ? 'border-m3-error' : 'border-m3-outline'
    } ${className}`}
    {...props}
  />
);

export interface FieldSelectOption {
  value: string;
  label: string;
}

export const FieldSelect = ({
  label,
  required,
  error,
  options,
  placeholder,
  value,
  onChange,
  className = '',
}: {
  label: string;
  required?: boolean;
  error?: string;
  options: FieldSelectOption[];
  placeholder?: string;
  value: string;
  onChange: (value: string) => void;
  className?: string;
}) => (
  <FormField label={label} required={required} error={error} className={className}>
    {/* `error` no se propagaba: el borde de error del select no se pintaba NUNCA. Defecto
        funcional, no de estilo. */}
    <FormSelect value={value} onChange={e => onChange(e.target.value)} error={!!error}>
      {placeholder && (
        <option value="" disabled>
          {placeholder}
        </option>
      )}
      {options.map(opt => (
        <option key={opt.value} value={opt.value}>
          {opt.label}
        </option>
      ))}
    </FormSelect>
  </FormField>
);

export const FormTextarea = ({
  className = '',
  error,
  ...props
}: React.TextareaHTMLAttributes<HTMLTextAreaElement> & { error?: boolean }) => (
  <textarea
    className={`h-16 px-3 py-2 text-[12px] border rounded-md bg-m3-surface focus:outline-none focus:ring-1 focus:ring-m3-primary/40 transition-shadow resize-none ${
      error ? 'border-m3-error' : 'border-m3-outline'
    } ${className}`}
    {...props}
  />
);

export const Toggle = ({
  checked,
  onChange,
  label,
  disabled,
}: {
  checked: boolean;
  onChange: (v: boolean) => void;
  label: string;
  disabled?: boolean;
}) => (
  <label className={`flex items-center gap-2 cursor-pointer ${disabled ? 'opacity-50' : ''}`}>
    <div
      onClick={() => !disabled && onChange(!checked)}
      className={`w-8 h-[18px] rounded-full transition-colors relative ${checked ? 'bg-m3-primary' : 'bg-m3-outline'}`}
    >
      <div
        className={`absolute top-[2px] w-[14px] h-[14px] rounded-full bg-white shadow-sm transition-transform ${checked ? 'translate-x-[18px]' : 'translate-x-[2px]'}`}
      />
    </div>
    <span className="text-[11px] text-m3-on-surface">{label}</span>
  </label>
);

export const FormActions = ({
  children,
  className = '',
}: {
  children: React.ReactNode;
  className?: string;
}) => (
  <div className={`flex items-center gap-2 pt-2 border-t border-m3-outline/10 ${className}`}>
    {children}
  </div>
);

export const FormButton = ({
  variant = 'filled',
  size = 'md',
  loading,
  icon,
  children,
  className = '',
  ...props
}: {
  variant?: 'filled' | 'outlined' | 'text';
  size?: 'sm' | 'md';
  loading?: boolean;
  icon?: React.ReactNode;
  children: React.ReactNode;
  className?: string;
} & React.ButtonHTMLAttributes<HTMLButtonElement>) => {
  const baseClasses =
    'inline-flex items-center justify-center gap-1.5 font-medium rounded-md transition-colors disabled:opacity-50';
  const sizeClasses = size === 'sm' ? 'h-7 px-3 text-[10px]' : 'h-8 px-4 text-[11px]';
  const variantClasses = {
    filled: 'bg-m3-primary text-white hover:bg-m3-primary/90',
    outlined: 'border border-m3-outline/30 text-m3-on-surface hover:bg-m3-surface-variant',
    text: 'text-m3-secondary hover:bg-m3-surface-variant',
  };

  return (
    <button
      className={`${baseClasses} ${sizeClasses} ${variantClasses[variant]} ${className}`}
      disabled={loading || props.disabled}
      {...props}
    >
      {loading ? (
        <div className="w-3.5 h-3.5 border-2 border-current/30 border-t-current rounded-full animate-spin" />
      ) : icon ? (
        <span className="w-3.5 h-3.5">{icon}</span>
      ) : null}
      {children}
    </button>
  );
};
