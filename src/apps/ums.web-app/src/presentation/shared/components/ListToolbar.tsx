import React, { useState } from 'react';
import { LayoutList, LayoutGrid, Search, X, ChevronsUp, ChevronsDown, Plus } from 'lucide-react';
import { M3SegmentedButton } from './M3SegmentedButton';
import type { SegmentOption } from './M3SegmentedButton';
import { formatItemCountLabel } from '@shared/utils/pluralize';

export interface ListToolbarSortOption {
  label: string;
  value: string;
}

export interface ListToolbarFilterOption {
  label: string;
  value: string;
}

export interface ListToolbarSearchOption {
  label: string;
  value: string;
}

interface ListToolbarProps {
  viewMode?: 'list' | 'thumbnail';
  onViewModeChange?: (mode: 'list' | 'thumbnail') => void;
  filterOptions?: ListToolbarFilterOption[];
  activeFilter?: string;
  onFilterChange?: (value: string) => void;
  sortOptions?: ListToolbarSortOption[];
  sortBy?: string;
  onSortByChange?: (value: string) => void;
  sortOrder?: 'asc' | 'desc';
  onSortOrderToggle?: () => void;
  searchOptions?: ListToolbarSearchOption[];
  activeSearchCriteria?: string;
  onSearchCriteriaChange?: (value: string) => void;
  searchValue?: string;
  onSearchValueChange?: (value: string) => void;
  onSearchSubmit?: () => void;
  onSearchClear?: () => void;
  itemCount: number;
  itemLabel: string;
  /** Plural explícito para préstamos/irregulares (p.ej. «tenants»); si se omite se deriva del singular. */
  itemLabelPlural?: string;
  secondaryActions?: React.ReactNode;
  onAdd?: () => void;
  onAddDisabled?: boolean;
  addLabel?: string;
}

const VIEW_MODE_OPTIONS: SegmentOption<'list' | 'thumbnail'>[] = [
  { value: 'list', label: <LayoutList className="w-4 h-4" /> },
  { value: 'thumbnail', label: <LayoutGrid className="w-4 h-4" /> },
];

const SelectField: React.FC<{
  value: string;
  onChange: (e: React.ChangeEvent<HTMLSelectElement>) => void;
  options: { label: string; value: string }[];
  className?: string;
}> = ({ value, onChange, options, className = '' }) => (
  <select
    value={value}
    onChange={onChange}
    className={`h-8 pl-2.5 pr-8 rounded-md border border-m3-outline/30 bg-m3-surface text-[12px] font-medium
      text-m3-secondary cursor-pointer appearance-none bg-no-repeat
      hover:border-m3-outline/50 focus:outline-none focus:ring-1 focus:ring-m3-primary/40 transition-colors
      ${className}`}
    style={{
      backgroundImage: `url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='12' height='12' viewBox='0 0 24 24' fill='none' stroke='%23757575' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'%3E%3Cpolyline points='6 9 12 15 18 9'%3E%3C/polyline%3E%3C/svg%3E")`,
      backgroundPosition: 'right 8px center',
    }}
  >
    {options.map(opt => (
      <option key={opt.value} value={opt.value}>
        {opt.label}
      </option>
    ))}
  </select>
);

const AddButton: React.FC<{ onClick: () => void; title?: string; disabled?: boolean }> = ({
  onClick,
  title = 'Agregar',
  disabled = false,
}) => (
  <button
    type="button"
    onClick={onClick}
    disabled={disabled}
    title={title}
    className={`inline-flex items-center justify-center w-8 h-8 rounded-full
      border border-m3-outline/30 text-m3-secondary
      transition-all focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-m3-primary
      active:scale-95 ${
        disabled
          ? 'opacity-50 cursor-not-allowed bg-m3-surface-variant'
          : 'hover:border-m3-primary/50 hover:text-m3-primary hover:bg-m3-primary/10'
      }`}
  >
    <Plus className="w-4 h-4" />
  </button>
);

export const ListToolbar: React.FC<ListToolbarProps> = ({
  viewMode = 'list',
  onViewModeChange,
  filterOptions,
  activeFilter,
  onFilterChange,
  sortOptions,
  sortBy,
  onSortByChange,
  sortOrder,
  onSortOrderToggle,
  searchOptions,
  activeSearchCriteria,
  onSearchCriteriaChange,
  searchValue,
  onSearchValueChange,
  onSearchSubmit,
  onSearchClear,
  itemCount,
  itemLabel,
  itemLabelPlural,
  secondaryActions,
  onAdd,
  onAddDisabled,
  addLabel = 'Agregar',
}) => {
  const [isExpanded, setIsExpanded] = useState(true);

  return (
    <div className="bg-m3-surface-container/5 border-b border-m3-outline/10">
      {/* Toolbar unificado en una sola fila: toggle · filtros · orden · vista · agregar */}
      <div className="flex items-center gap-1.5 px-1.5 py-1 flex-wrap">
        {/* Toggle solo-icono; el conteo vive en el tooltip para minimizar el ancho */}
        <button
          type="button"
          onClick={() => setIsExpanded(!isExpanded)}
          className="inline-flex items-center justify-center h-8 w-8 rounded-md bg-m3-surface border border-m3-outline/30 text-m3-on-surface hover:text-m3-primary hover:border-m3-primary/40 hover:bg-m3-primary/5 transition-all shrink-0"
          title={`${formatItemCountLabel(itemCount, itemLabel, itemLabelPlural)} · ${
            isExpanded ? 'Ocultar filtros' : 'Mostrar filtros'
          }`}
        >
          {isExpanded ? (
            <ChevronsUp className="w-4 h-4 text-m3-secondary" />
          ) : (
            <ChevronsDown className="w-4 h-4 text-m3-secondary" />
          )}
        </button>

        {isExpanded && (
          <>
          {/* Search group */}
          {searchOptions && onSearchCriteriaChange && activeSearchCriteria !== undefined && (
            <div className="flex items-center gap-2 flex-1 min-w-[14rem] max-w-xl">
              <div className="flex items-center gap-1.5 bg-m3-surface rounded-md border border-m3-outline/30 px-2.5 h-8 flex-1 min-w-0 focus-within:ring-1 focus-within:ring-m3-primary/40 focus-within:border-m3-primary/50">
                <Search className="w-3.5 h-3.5 text-m3-secondary/50 shrink-0" />
                <select
                  value={activeSearchCriteria}
                  onChange={e => onSearchCriteriaChange(e.target.value)}
                  className="h-6 w-auto bg-transparent text-[12px] font-medium text-m3-secondary cursor-pointer focus:outline-none border-none"
                >
                  {searchOptions.map(f => (
                    <option key={f.value} value={f.value}>
                      {f.label}
                    </option>
                  ))}
                </select>
                <div className="w-px h-4 bg-m3-outline/20" />
                <input
                  type="text"
                  data-testid="list-search"
                  value={searchValue ?? ''}
                  onChange={e => onSearchValueChange?.(e.target.value)}
                  onKeyDown={e => {
                    if (e.key === 'Enter' && onSearchSubmit) {
                      e.preventDefault();
                      onSearchSubmit();
                    }
                    if (e.key === 'Escape' && onSearchClear) {
                      onSearchClear();
                    }
                  }}
                  placeholder="Buscar..."
                  className="h-6 flex-1 min-w-[4rem] bg-transparent text-[12px] text-m3-on-surface
                    placeholder:text-m3-secondary/40 focus:outline-none"
                />
                {searchValue && onSearchClear && (
                  <button
                    type="button"
                    onClick={onSearchClear}
                    className="p-0.5 rounded text-m3-secondary/50 hover:text-m3-secondary hover:bg-m3-surface-variant transition-colors"
                  >
                    <X className="w-3 h-3" />
                  </button>
                )}
              </div>
              <button
                type="button"
                onClick={onSearchSubmit}
                className="h-8 px-3 rounded-md bg-m3-primary text-white text-[12px] font-medium
                  hover:bg-m3-primary/90 transition-colors shrink-0"
              >
                Buscar
              </button>
            </div>
          )}

          {/* Filter group */}
          {filterOptions && onFilterChange && activeFilter !== undefined && (
            <div className="flex items-center gap-2 shrink-0">
              <SelectField
                value={activeFilter}
                onChange={e => onFilterChange(e.target.value)}
                options={filterOptions}
              />
            </div>
          )}

          {/* Sort group */}
          {sortOptions && onSortByChange && sortBy !== undefined && (
            <div className="flex items-center gap-1.5 shrink-0">
              <SelectField
                value={sortBy}
                onChange={e => onSortByChange(e.target.value)}
                options={sortOptions}
              />
              {onSortOrderToggle && (
                <button
                  type="button"
                  onClick={onSortOrderToggle}
                  className="h-8 px-2 rounded-md border border-m3-outline/30 bg-m3-surface text-m3-secondary
                    hover:text-m3-primary hover:bg-m3-primary/10 transition-colors text-[12px]"
                  title={sortOrder === 'asc' ? 'Ascendente' : 'Descendente'}
                >
                  {sortOrder === 'asc' ? 'A-Z' : 'Z-A'}
                </button>
              )}
            </div>
          )}

          </>
        )}

        {/* Modos de vista + acciones (al expandir) + agregar (siempre) */}
        <div className="flex items-center gap-2 ml-auto shrink-0">
          {isExpanded && onViewModeChange && (
            <M3SegmentedButton
              options={VIEW_MODE_OPTIONS}
              value={viewMode}
              onChange={onViewModeChange}
              size="sm"
            />
          )}
          {isExpanded && secondaryActions}
          {onAdd && <AddButton onClick={onAdd} title={addLabel} disabled={onAddDisabled} />}
        </div>
      </div>
    </div>
  );
};
