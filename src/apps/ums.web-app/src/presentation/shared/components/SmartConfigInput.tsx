/**
 * SmartConfigInput.tsx
 *
 * Renders the appropriate input control based on the value type.
 * Compact and minimalist design.
 */
import React from 'react';
import { inferConfigValueType } from '@app/utils/config-value-types';
import { M3TextField } from '@presentation/shared/components/M3TextField';
import { M3Switch } from '@shared/components/M3Switch';

interface SmartConfigInputProps {
  code: string;
  value: string;
  onChange: (newValue: string) => void;
  disabled?: boolean;
}

export const SmartConfigInput: React.FC<SmartConfigInputProps> = ({
  value,
  onChange,
  disabled = false,
}) => {
  const type = inferConfigValueType(value);

  switch (type) {
    case 'boolean':
      return (
        <M3Switch
          checked={value === 'true'}
          onChange={(_, checked) => onChange(checked ? 'true' : 'false')}
          disabled={disabled}
          size="small"
        />
      );

    case 'number':
      return (
        <M3TextField
          type="number"
          value={value}
          onChange={e => onChange(e.target.value)}
          disabled={disabled}
          size="small"
          className="!w-24"
          min={0}
          step={1}
        />
      );

    case 'json':
      return (
        <span className="text-[10px] font-mono text-m3-on-surface-variant bg-m3-surface-container px-1.5 py-0.5 rounded truncate max-w-[150px] block">
          {value.length > 30 ? `${value.substring(0, 30)}...` : value}
        </span>
      );

    case 'string':
    default:
      return (
        <input
          type="text"
          value={value}
          onChange={e => onChange(e.target.value)}
          disabled={disabled}
          className="h-6 px-3 text-[12px] border border-m3-outline/30 rounded bg-m3-surface focus:outline-none focus:ring-1 focus:ring-m3-primary/40 disabled:opacity-60 w-64"
        />
      );
  }
};
