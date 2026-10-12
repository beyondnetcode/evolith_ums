import React, { useState } from 'react';
import { RefreshCw, ChevronUp, ChevronDown } from 'lucide-react';
import { M3Card } from './M3Card';
import { M3SegmentedButton } from './M3SegmentedButton';
import type { SegmentOption } from './M3SegmentedButton';

// ─── Types ──────────────────────────────────────────────────────────────────

export interface DetailTab<T extends string = string> {
  key: T;
  label: string;
  icon?: React.ReactNode;
}

export interface DetailPanelShellProps<T extends string = string> {
  // ── State ──────────────────────────────────────────────────────────────────
  isLoading: boolean;
  /** True when no entity is currently selected. */
  isEmpty: boolean;

  // ── Customisable labels ────────────────────────────────────────────────────
  loadingLabel?: string;
  emptyLabel?: string;

  // ── Header bar ─────────────────────────────────────────────────────────────
  title?: string;
  subtitle?: string;
  headerActions?: React.ReactNode;

  // ─ Profile / header section ───────────────────────────────────────────────
  /** Slot rendered above the tab bar (e.g. a profile card, summary strip). */
  header?: React.ReactNode;
  /** When true, the header can be collapsed/expanded via a splitter toggle. */
  headerCollapsible?: boolean;
  /** Optional informational banner between header and tabs. */
  banner?: React.ReactNode;

  // ── Tabs ───────────────────────────────────────────────────────────────────
  tabs?: DetailTab<T>[];
  activeTab?: T;
  onTabChange?: (tab: T) => void;

  // ── Tab content ────────────────────────────────────────────────────────────
  /** Renders the body for the currently-active tab. */
  children: React.ReactNode;

  /** Unique key to reset animation when the selected entity changes. */
  entityKey?: string;
}

// ─── Component ──────────────────────────────────────────────────────────────

export function DetailPanelShell<T extends string = string>({
  isLoading,
  isEmpty,
  loadingLabel = 'Cargando...',
  emptyLabel = 'Seleccione un elemento para ver los detalles.',
  title,
  subtitle,
  headerActions,
  header,
  headerCollapsible = false,
  banner,
  tabs = [],
  activeTab,
  onTabChange,
  children,
  entityKey,
}: DetailPanelShellProps<T>) {
  const [headerCollapsed, setHeaderCollapsed] = useState(false);

  if (isLoading) {
    return (
      <M3Card
        variant="elevated"
        className="flex flex-col items-center justify-center min-h-[200px] border border-m3-outline/20 bg-m3-surface-container/10"
      >
        <RefreshCw className="w-6 h-6 animate-spin text-m3-primary mb-3" />
        <p className="text-[12px] text-m3-secondary">{loadingLabel}</p>
      </M3Card>
    );
  }

  if (isEmpty) {
    return (
      <M3Card
        variant="elevated"
        className="flex flex-col items-center justify-center min-h-[200px] border border-m3-outline/20 bg-m3-surface-container/10"
      >
        <p className="text-[12px] text-m3-secondary/60">{emptyLabel}</p>
      </M3Card>
    );
  }

  return (
    <div key={entityKey} className="space-y-4">
      {/* Title bar */}
      {(title || subtitle || headerActions) && (
        <div className="flex items-start justify-between gap-4 px-1">
          <div className="flex-1 min-w-0">
            {title && <h2 className="text-[12px] font-semibold text-m3-on-surface">{title}</h2>}
            {subtitle && <p className="text-[11px] text-m3-secondary mt-0.5">{subtitle}</p>}
          </div>
          {headerActions && <div className="shrink-0">{headerActions}</div>}
        </div>
      )}

      {/* Profile / header slot */}
      {header && (
        <div
          className={`overflow-hidden transition-all duration-200 ${
            headerCollapsed ? 'max-h-0 opacity-0' : 'max-h-[600px] opacity-100'
          }`}
        >
          {header}
        </div>
      )}

      {/* Splitter / toggle between header and tabs */}
      {header && headerCollapsible && (
        <div className="flex items-center gap-2 -mx-4 px-4">
          <div className="flex-1 h-px bg-m3-outline/15" />
          <button
            type="button"
            onClick={() => setHeaderCollapsed(!headerCollapsed)}
            className="flex items-center gap-1 text-[10px] text-m3-secondary/60 hover:text-m3-primary
              transition-colors px-2 py-1 rounded-md hover:bg-m3-primary/10"
            title={headerCollapsed ? 'Mostrar cabecera' : 'Ocultar cabecera'}
          >
            {headerCollapsed ? (
              <ChevronDown className="w-3 h-3" />
            ) : (
              <ChevronUp className="w-3 h-3" />
            )}
            {headerCollapsed ? 'Mostrar' : 'Ocultar'}
          </button>
          <div className="flex-1 h-px bg-m3-outline/15" />
        </div>
      )}

      {/* Optional informational banner */}
      {banner}

      {/* Tab bar */}
      {tabs.length > 0 && (
        <div className="-mx-4 px-4">
          <M3SegmentedButton
            options={tabs.map(
              (tab): SegmentOption<T> => ({
                value: tab.key,
                label: (
                  <span className="flex items-center gap-1.5">
                    {tab.icon} {tab.label}
                  </span>
                ),
              })
            )}
            value={activeTab}
            onChange={onTabChange}
            size="md"
          />
        </div>
      )}

      {/* Tab content */}
      <M3Card
        variant="elevated"
        className="p-4 border border-m3-outline/20 bg-m3-surface-container/10"
      >
        {children}
      </M3Card>
    </div>
  );
}
