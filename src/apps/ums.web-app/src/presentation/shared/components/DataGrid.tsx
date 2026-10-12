/**
 * DataGrid.tsx — Generic, configurable data table for any aggregate.
 *
 * Accepts column definitions and a data array, renders a styled table
 * with optional row selection, expand/collapse, and custom cell renderers.
 * Replaces hardcoded table markup in list panels.
 *
 * Responsive (G-028): bajo el breakpoint `lg` la tabla no cabe sin scroll
 * horizontal, así que se renderiza en **modo tarjeta** — una tarjeta apilable
 * por registro, con la primera columna como titular y el resto en pares
 * etiqueta/valor. Se conserva selección y jerarquía expand/colapsar.
 */
import React from 'react';
import { Check, ChevronRight } from 'lucide-react';
import { useIsMobile } from '../hooks/use-breakpoint';

export interface ColumnDef<T> {
  header: string;
  accessor?: keyof T;
  cell?: (item: T) => React.ReactNode;
  className?: string;
  headerClassName?: string;
}

export interface DataGridProps<T> {
  columns: ColumnDef<T>[];
  data: T[];
  idKey: keyof T;
  selectedId?: string;
  onSelect?: (id: string) => void;
  expandedIds?: Set<string>;
  onToggleExpand?: (id: string) => void;
  parentIdKey?: keyof T;
  className?: string;
  emptyLabel?: string;
  isLoading?: boolean;
}

function DataGrid<T extends Record<string, unknown>>({
  columns,
  data,
  idKey,
  selectedId,
  onSelect,
  expandedIds = new Set(),
  onToggleExpand,
  parentIdKey,
  className = '',
  emptyLabel = 'No records found.',
  isLoading = false,
}: DataGridProps<T>) {
  const isMobile = useIsMobile();

  if (isLoading) {
    return (
      <div className="py-12 text-center text-sm text-m3-secondary">
        <svg
          className="animate-spin h-6 w-6 text-m3-primary mx-auto mb-2"
          fill="none"
          viewBox="0 0 24 24"
        >
          <circle
            className="opacity-25"
            cx="12"
            cy="12"
            r="10"
            stroke="currentColor"
            strokeWidth="4"
          />
          <path
            className="opacity-75"
            fill="currentColor"
            d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
          />
        </svg>
        Loading...
      </div>
    );
  }

  if (data.length === 0) {
    return <div className="py-8 text-center text-sm text-m3-secondary">{emptyLabel}</div>;
  }

  const hasHierarchy = !!parentIdKey && !!onToggleExpand;

  // Texto de una columna basada en `accessor` (sin `cell` propio).
  const cellText = (col: ColumnDef<T>, item: T): string =>
    col.accessor ? String(item[col.accessor] ?? '') : '';

  // Titular de una fila/tarjeta: primera columna (cell o accessor).
  const renderPrimary = (item: T): React.ReactNode => {
    const [primary] = columns;
    if (!primary) return null;
    return primary.cell ? (
      primary.cell(item)
    ) : (
      <span className="font-medium text-m3-on-surface">
        {primary.accessor ? String(item[primary.accessor] ?? '') : ''}
      </span>
    );
  };

  // Botón de expandir/colapsar con área táctil de 32px (`before`) sin alterar
  // la densidad visual del icono.
  const renderToggle = (id: string, isExpanded: boolean) => (
    <button
      onClick={e => {
        e.stopPropagation();
        onToggleExpand?.(id);
      }}
      onMouseDown={e => e.stopPropagation()}
      className={[
        'relative p-0.5 rounded transition-transform duration-200',
        'before:absolute before:left-1/2 before:top-1/2 before:h-8 before:w-8',
        'before:-translate-x-1/2 before:-translate-y-1/2 before:content-[""]',
        isExpanded ? 'rotate-90' : '',
      ].join(' ')}
      aria-label={isExpanded ? 'Colapsar' : 'Expandir'}
    >
      <ChevronRight className="w-3.5 h-3.5 text-m3-secondary" />
    </button>
  );

  const selectedCheck = (
    <span className="h-5 w-5 bg-m3-primary text-m3-on-primary rounded-full flex items-center justify-center shrink-0">
      <Check className="w-3 h-3" />
    </span>
  );

  // ─── Modo tarjeta (móvil / tablet) ──────────────────────────────────────────
  const renderCard = (item: T, isChild = false): React.ReactNode => {
    const id = String(item[idKey]);
    const isSelected = selectedId === id;
    const hasChildren =
      hasHierarchy && parentIdKey && data.some(d => String(d[parentIdKey]) === id);
    const isExpanded = expandedIds.has(id);

    return (
      <React.Fragment key={id}>
        <div
          onClick={() => onSelect?.(id)}
          className={[
            'rounded-xl border p-3 transition-colors duration-150',
            onSelect ? 'cursor-pointer' : '',
            isSelected
              ? 'bg-m3-primary-container/30 border-m3-primary/30 text-m3-on-primary-container'
              : 'bg-m3-surface-container/20 border-m3-outline/25 hover:bg-m3-primary/5',
            isChild ? 'ml-4' : '',
          ].join(' ')}
        >
          {/* Titular */}
          <div className="flex items-start justify-between gap-2">
            <div className="flex items-center gap-2 min-w-0 flex-1 text-sm">
              {hasHierarchy &&
                (hasChildren ? renderToggle(id, isExpanded) : <span className="w-4 shrink-0" />)}
              <div className="min-w-0 flex-1">{renderPrimary(item)}</div>
            </div>
            {onSelect && isSelected && selectedCheck}
          </div>

          {/* Columnas restantes como pares etiqueta/valor */}
          {columns.length > 1 && (
            <dl className="mt-2 grid grid-cols-2 gap-x-3 gap-y-1.5">
              {columns.slice(1).map((col, idx) => (
                <div key={idx} className="flex flex-col min-w-0">
                  <dt className="text-xs text-m3-secondary">{col.header}</dt>
                  <dd className="text-sm text-m3-on-surface min-w-0">
                    {col.cell ? col.cell(item) : cellText(col, item)}
                  </dd>
                </div>
              ))}
            </dl>
          )}
        </div>
        {isExpanded &&
          hasHierarchy &&
          parentIdKey &&
          data.filter(d => String(d[parentIdKey]) === id).map(child => renderCard(child, true))}
      </React.Fragment>
    );
  };

  if (isMobile) {
    return (
      <div className={`flex flex-col gap-2 ${className}`}>
        {data.filter(item => !parentIdKey || !item[parentIdKey]).map(item => renderCard(item))}
      </div>
    );
  }

  // ─── Modo tabla (escritorio) ────────────────────────────────────────────────
  const renderRow = (item: T, isChild = false) => {
    const id = String(item[idKey]);
    const isSelected = selectedId === id;
    const hasChildren =
      hasHierarchy && parentIdKey && data.some(d => String(d[parentIdKey]) === id);
    const isExpanded = expandedIds.has(id);

    return (
      <React.Fragment key={id}>
        <tr
          onClick={() => onSelect?.(id)}
          className={`group cursor-pointer transition-colors duration-150 ${
            isSelected
              ? 'bg-m3-primary-container/30 text-m3-on-primary-container'
              : 'hover:bg-m3-primary/5 text-m3-secondary hover:text-m3-on-surface'
          } ${isChild ? 'bg-m3-surface-container/10' : ''}`}
        >
          {columns.map((col, idx) => (
            <td
              key={idx}
              className={`py-[var(--densidad-y)] ${isChild && idx === 0 ? 'pl-10' : (col.className ?? (idx === 0 ? 'px-5' : 'px-4'))}`}
            >
              {idx === 0 ? (
                <div className="flex items-center gap-3">
                  {hasHierarchy &&
                    (hasChildren ? renderToggle(id, isExpanded) : <span className="w-4" />)}
                  {col.cell ? (
                    col.cell(item)
                  ) : (
                    <span className="font-medium text-m3-on-surface">
                      {col.accessor ? String(item[col.accessor] ?? '') : ''}
                    </span>
                  )}
                </div>
              ) : col.cell ? (
                col.cell(item)
              ) : (
                <span className={isChild ? 'opacity-70' : ''}>
                  {col.accessor ? String(item[col.accessor] ?? '') : ''}
                </span>
              )}
            </td>
          ))}
          {onSelect && (
            <td className="py-[var(--densidad-y)] px-5 text-right">
              <div className="flex items-center justify-end">{isSelected && selectedCheck}</div>
            </td>
          )}
        </tr>
        {isExpanded &&
          hasHierarchy &&
          parentIdKey &&
          data.filter(d => String(d[parentIdKey]) === id).map(child => renderRow(child, true))}
      </React.Fragment>
    );
  };

  return (
    <div
      className={`overflow-x-auto border border-m3-outline/25 rounded-xl bg-m3-surface-container/20 ${className}`}
    >
      <table className="w-full text-left border-collapse">
        <thead>
          <tr className="border-b border-m3-outline/20 text-xs font-medium text-m3-secondary bg-m3-surface-container/40">
            {columns.map((col, idx) => (
              <th
                key={idx}
                className={`py-[var(--densidad-y)] ${col.headerClassName ?? (idx === 0 ? 'px-5' : 'px-4')}`}
              >
                {col.header}
              </th>
            ))}
            {onSelect && <th className="py-[var(--densidad-y)] px-5 text-right" />}
          </tr>
        </thead>
        <tbody className="divide-y divide-m3-outline/10 text-sm">
          {data.filter(item => !parentIdKey || !item[parentIdKey]).map(item => renderRow(item))}
        </tbody>
      </table>
    </div>
  );
}

export default DataGrid;
export { DataGrid };
