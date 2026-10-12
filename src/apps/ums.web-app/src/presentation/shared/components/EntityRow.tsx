import React from 'react';

/**
 * EntityRow — reusable row for entity lists (branches, providers, etc.)
 *
 * Renders an M3-compliant list item row with:
 *   - leading: icon or avatar slot
 *   - children: primary content (headline, supporting text)
 *   - trailingColumns: array of fixed-width column slots for aligned badges/actions
 *   - trailing (legacy freeform): unstructured trailing content
 *   - Interactive states (hover, selected, disabled)
 *
 * Use `trailingColumns` for aligned grid layout across rows.
 * Use `trailing` for simple, single-element trailing content.
 */

export interface TrailingColumn {
  /** Content to render in the column. */
  content: React.ReactNode;
  /** Fixed width class for alignment (e.g. 'w-20', 'w-24'). */
  width?: string;
  /** Horizontal alignment within the column. Default: 'end'. */
  align?: 'start' | 'center' | 'end';
}

export interface EntityRowProps {
  /** Unique key for the entity (used as React key externally, optional here). */
  id?: string;
  /** Whether the entity is active in the business logic (fades out if false). */
  isActive?: boolean;
  /** Whether the row is currently selected in the UI. */
  selected?: boolean;
  /** Primary content slot (name, badges, metadata). */
  children: React.ReactNode;
  /** Leading slot for icons, checkboxes, or avatars. */
  leading?: React.ReactNode;
  /** Structured trailing columns with fixed widths for cross-row alignment. */
  trailingColumns?: TrailingColumn[];
  /** Trailing slot for action buttons or trailing badges (legacy freeform). */
  trailing?: React.ReactNode;
  /** Legacy alias for trailing. */
  actions?: React.ReactNode;
  /** Click handler for the entire row. */
  onClick?: () => void;
  /** Called on double-click (typically opens inline edit). */
  onDoubleClick?: () => void;
  /** Extra Tailwind classes. */
  className?: string;
}

const ALIGN_MAP = {
  start: 'justify-start',
  center: 'justify-center',
  end: 'justify-end',
} as const;

export const EntityRow: React.FC<EntityRowProps> = ({
  id,
  isActive = true,
  selected = false,
  children,
  leading,
  trailingColumns,
  trailing,
  actions,
  onClick,
  onDoubleClick,
  className = '',
}) => {
  const isInteractive = !!onClick || !!onDoubleClick;

  // Determine trailing content: prefer structured columns, fall back to freeform
  const hasStructuredTrailing = trailingColumns && trailingColumns.length > 0;
  const hasFreeformTrailing = !!(trailing || actions);

  return (
    <div
      data-testid="entity-row"
      data-entity-id={id}
      data-selected={selected || undefined}
      // Una fila que responde al ratón y no al teclado deja el listado inoperable para quien no usa
      // ratón, y este es el primitivo de TODOS los listados. Se le da la semántica que ya tenía de
      // hecho: es una opción de una lista, se puede enfocar, y Enter o Espacio hacen lo que el clic.
      // `aria-selected` dice en voz alta lo que hasta ahora solo decía el color (P-UX-02, WCAG 2.2).
      role={isInteractive ? 'option' : undefined}
      aria-selected={isInteractive ? !!selected : undefined}
      tabIndex={isInteractive ? 0 : undefined}
      onClick={onClick}
      onDoubleClick={onDoubleClick}
      onKeyDown={
        isInteractive
          ? event => {
              if (event.key !== 'Enter' && event.key !== ' ') return;
              // El espacio hace scroll si no se detiene; en una fila enfocada eso mueve la página
              // bajo los pies de quien acaba de elegirla.
              event.preventDefault();
              onClick?.(event as unknown as React.MouseEvent<HTMLDivElement>);
            }
          : undefined
      }
      className={[
        'group/row flex flex-col sm:flex-row sm:items-center px-4 py-[var(--densidad-y-sm)] min-h-[3rem]',
        'transition-all duration-200 outline-none',
        // El contorno no se quita, se sustituye: el foco tiene que verse, y sobre `primary` supera
        // el 3:1 que el piso exige.
        isInteractive
          ? 'focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-[-2px] focus-visible:outline-m3-primary'
          : '',
        isInteractive ? 'cursor-pointer' : 'cursor-default',
        isActive ? 'opacity-100' : 'opacity-65',
        selected
          ? 'bg-m3-primary/10 border border-m3-primary/20'
          : isInteractive
            ? 'bg-transparent hover:bg-m3-surface-container/20 border border-transparent hover:border-m3-outline/5'
            : 'bg-transparent border border-transparent',
        className,
      ].join(' ')}
    >
      {/* Leading + Content area — takes available space */}
      <div className="flex items-center gap-3 flex-1 min-w-0">
        {leading && <div className="shrink-0">{leading}</div>}
        <div className="flex-1 min-w-0 flex flex-col justify-center">{children}</div>
      </div>

      {/* Structured trailing columns — fixed-width grid for alignment */}
      {hasStructuredTrailing && (
        <div className="flex items-center gap-3 shrink-0 flex-wrap mt-1.5 sm:mt-0 sm:ml-4">
          {trailingColumns.map((col, i) => (
            <div
              key={i}
              className={`${col.width ?? 'w-auto'} flex items-center ${ALIGN_MAP[col.align ?? 'end']} shrink-0`}
            >
              {col.content}
            </div>
          ))}
        </div>
      )}

      {/* Legacy freeform trailing — only when no structured columns */}
      {!hasStructuredTrailing && hasFreeformTrailing && (
        <div className="flex items-center gap-2 shrink-0 flex-wrap mt-1.5 sm:mt-0 sm:ml-4">
          {trailing || actions}
        </div>
      )}
    </div>
  );
};
