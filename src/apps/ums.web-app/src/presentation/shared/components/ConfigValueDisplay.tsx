/**
 * ConfigValueDisplay.tsx
 *
 * Displays configuration values with smart inference.
 * Read-only display - no editing.
 */
import React from 'react';
import { inferConfigValueType } from '@app/utils/config-value-types';
import { M3Switch } from '@shared/components/M3Switch';

interface ConfigValueDisplayProps {
  value: string;
  className?: string;
}

export function ConfigValueDisplay({
  value,
  className = '',
}: ConfigValueDisplayProps): React.JSX.Element {
  const type = inferConfigValueType(value);

  switch (type) {
    case 'boolean':
      return <M3Switch checked={value === 'true'} disabled size="small" />;

    case 'number':
      return <span className={`font-mono text-[10px] ${className}`}>{value}</span>;

    case 'json':
      return (
        <code
          className={`text-[9px] bg-m3-surface-container-high px-1.5 py-0.5 rounded font-mono ${className}`}
        >
          {value.length > 30 ? `${value.substring(0, 30)}...` : value}
        </code>
      );

    case 'string':
    default:
      return (
        <span className={`text-[10px] font-mono truncate max-w-[180px] inline-block ${className}`}>
          {value.length > 40 ? `${value.substring(0, 40)}...` : value}
        </span>
      );
  }
}
