/**
 * EntityCard.tsx — Generic entity card for thumbnail/grid views.
 *
 * Supports two usage modes:
 *
 * 1. **Simple mode** (recommended): Pass `title`, `subtitle`, `icon`, `badges`, `selected`, `onClick`.
 *    Used by SystemSuiteListPanel, PermissionTemplateListPanel, UserAccountListPanel.
 *
 * 2. **Generic mode** (legacy): Pass `item`, `idKey`, `title(fn)`, `subtitle(fn)`, `fields`.
 *    Used for fully dynamic card generation.
 */
import React from 'react';
import { M3Card } from './M3Card';

// ─── Simple mode props ─────────────────────────────────────────────────────

export interface EntityCardSimpleProps {
  selected?: boolean;
  onClick?: () => void;
  icon?: React.ReactNode;
  title: string | React.ReactNode;
  subtitle?: string | React.ReactNode;
  badges?: React.ReactNode;
  footer?: React.ReactNode;
  className?: string;
}

// ─── Generic mode props ────────────────────────────────────────────────────

export interface EntityField<T> {
  label: string;
  accessor: keyof T;
  formatter?: (value: unknown) => string;
}

export interface EntityCardGenericProps<T> {
  item: T;
  idKey: keyof T;
  icon: React.ReactNode;
  title: (item: T) => string;
  subtitle?: (item: T) => string;
  fields?: EntityField<T>[];
  badge?: React.ReactNode;
  isSelected?: boolean;
  onClick?: () => void;
  footer?: React.ReactNode;
  className?: string;
}

// ─── Simple EntityCard ─────────────────────────────────────────────────────

export const EntityCard: React.FC<EntityCardSimpleProps> = ({
  selected = false,
  onClick,
  icon,
  title,
  subtitle,
  badges,
  footer,
  className = '',
}) => {
  return (
    <M3Card
      onClick={onClick}
      variant={selected ? 'elevated' : 'filled'}
      className={`p-5 cursor-pointer border transition-all duration-150 hover:-translate-y-0.5 hover:shadow-md ${
        selected
          ? 'border-m3-primary bg-m3-primary-container/15'
          : 'border-m3-outline/25 hover:border-m3-primary/30'
      } ${className}`}
    >
      <div className="flex justify-between items-start gap-4">
        <div className="flex gap-3 flex-1 min-w-0">
          {icon && (
            <div
              className={`p-2.5 rounded-lg border shrink-0 ${
                selected
                  ? 'bg-m3-primary text-white border-m3-primary'
                  : 'bg-m3-primary/10 text-m3-primary border-m3-primary/10'
              }`}
            >
              {icon}
            </div>
          )}
          <div className="flex-1 min-w-0">
            <h4 className="text-sm font-medium text-m3-on-surface line-clamp-1">{title}</h4>
            {subtitle && (
              <div className="text-xs text-m3-secondary/70 mt-0.5 line-clamp-1">{subtitle}</div>
            )}
          </div>
        </div>
        {badges && <div className="shrink-0">{badges}</div>}
      </div>

      {footer && <div className="mt-3 pt-3 border-t border-m3-outline/10">{footer}</div>}
    </M3Card>
  );
};

// ─── Generic EntityCard (legacy support) ───────────────────────────────────

export function EntityCardGeneric<T extends Record<string, unknown>>({
  item,
  idKey,
  icon,
  title,
  subtitle,
  fields = [],
  badge,
  isSelected = false,
  onClick,
  footer,
  className = '',
}: EntityCardGenericProps<T>) {
  const id = String(item[idKey]);

  return (
    <M3Card
      key={id}
      onClick={onClick}
      variant={isSelected ? 'elevated' : 'filled'}
      className={`p-5 cursor-pointer border transition-all duration-150 hover:-translate-y-0.5 hover:shadow-md ${
        isSelected
          ? 'border-m3-primary bg-m3-primary-container/15'
          : 'border-m3-outline/25 hover:border-m3-primary/30'
      } ${className}`}
    >
      <div className="flex justify-between items-start gap-4">
        <div className="flex gap-3 flex-1">
          <div
            className={`p-2.5 rounded-lg border ${
              isSelected
                ? 'bg-m3-primary text-white border-m3-primary'
                : 'bg-m3-primary/10 text-m3-primary border-m3-primary/10'
            }`}
          >
            {icon}
          </div>
          <div className="flex-1 min-w-0">
            <h4 className="text-sm font-medium text-m3-on-surface line-clamp-1">{title(item)}</h4>
            {subtitle && (
              <p className="font-mono text-xs text-m3-secondary/70 mt-0.5">{subtitle(item)}</p>
            )}
          </div>
        </div>
        {badge && <div>{badge}</div>}
      </div>

      {fields.length > 0 && (
        <div className="mt-4 pt-3 border-t border-m3-outline/10 grid grid-cols-1 sm:grid-cols-2 gap-2 text-xs">
          {fields.map((field, idx) => {
            const raw = item[field.accessor];
            const display = field.formatter ? field.formatter(raw) : String(raw ?? '-');
            return (
              <div key={idx}>
                <p className="text-m3-secondary font-medium">{field.label}</p>
                <p className="font-medium text-m3-on-surface mt-0.5 truncate">{display}</p>
              </div>
            );
          })}
        </div>
      )}

      {footer && <div className="mt-2">{footer}</div>}
    </M3Card>
  );
}
