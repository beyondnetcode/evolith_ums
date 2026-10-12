import React from 'react';
import { Filter, ArrowUpDown, List, LayoutGrid } from 'lucide-react';

export interface FilterOption {
  label: string;
  value: string;
}

export interface SortOption {
  label: string;
  value: string;
}

interface FilterPanelProps {
  filterOptions?: FilterOption[];
  activeFilter?: string;
  onFilterChange?: (val: string) => void;

  sortOptions?: SortOption[];
  sortBy?: string;
  onSortByChange?: (val: string) => void;

  sortOrder?: 'asc' | 'desc';
  onSortOrderToggle?: () => void;

  viewMode?: 'list' | 'thumbnail';
  onViewModeChange?: (mode: 'list' | 'thumbnail') => void;
}

const SelectBase = ({
  children,
  className = '',
}: {
  children: React.ReactNode;
  className?: string;
}) => (
  <div
    className={`flex items-center gap-1.5 rounded-lg border border-m3-outline/20 bg-m3-surface-container/20 px-2 py-1.5 text-xs text-m3-on-surface transition-colors hover:border-m3-outline/40 ${className}`}
  >
    {children}
  </div>
);

export const FilterPanel: React.FC<FilterPanelProps> = ({
  filterOptions,
  activeFilter,
  onFilterChange,
  sortOptions,
  sortBy,
  onSortByChange,
  sortOrder,
  onSortOrderToggle,
  viewMode,
  onViewModeChange,
}) => {
  return (
    <div className="flex items-center gap-2">
      {/* Filter */}
      {filterOptions && onFilterChange && activeFilter !== undefined && (
        <SelectBase>
          <Filter className="w-3.5 h-3.5 text-m3-secondary/60 shrink-0" />
          <select
            value={activeFilter}
            onChange={e => onFilterChange(e.target.value)}
            aria-label="Filter"
            className="bg-transparent border-none outline-none text-xs font-medium text-m3-on-surface cursor-pointer pr-1"
          >
            {filterOptions.map(f => (
              <option key={f.value} value={f.value}>
                {f.label}
              </option>
            ))}
          </select>
        </SelectBase>
      )}

      {/* Sort */}
      {sortOptions && onSortByChange && sortBy !== undefined && (
        <SelectBase>
          <ArrowUpDown className="w-3.5 h-3.5 text-m3-secondary/60 shrink-0" />
          <select
            value={sortBy}
            onChange={e => onSortByChange(e.target.value)}
            aria-label="Sort by"
            className="bg-transparent border-none outline-none text-xs font-medium text-m3-on-surface cursor-pointer pr-1"
          >
            {sortOptions.map(s => (
              <option key={s.value} value={s.value}>
                {s.label}
              </option>
            ))}
          </select>
          {onSortOrderToggle && sortOrder && (
            <button
              type="button"
              onClick={onSortOrderToggle}
              className="shrink-0 p-0.5 rounded hover:bg-m3-outline/10 transition-colors"
              title={sortOrder === 'asc' ? 'Ascendente' : 'Descendente'}
            >
              <ArrowUpDown
                className={`w-3 h-3 transition-transform duration-200 ${
                  sortOrder === 'desc' ? 'rotate-180 text-m3-primary' : 'text-m3-secondary/60'
                }`}
              />
            </button>
          )}
        </SelectBase>
      )}

      {/* View Mode */}
      {viewMode && onViewModeChange && (
        <div className="flex items-center rounded-lg border border-m3-outline/20 bg-m3-surface-container/20 overflow-hidden">
          <button
            type="button"
            onClick={() => onViewModeChange('list')}
            className={`p-1.5 transition-colors ${
              viewMode === 'list'
                ? 'bg-m3-primary/10 text-m3-primary'
                : 'text-m3-secondary/60 hover:text-m3-on-surface hover:bg-m3-outline/10'
            }`}
            title="Vista lista"
          >
            <List className="w-3.5 h-3.5" />
          </button>
          <button
            type="button"
            onClick={() => onViewModeChange('thumbnail')}
            className={`p-1.5 transition-colors ${
              viewMode === 'thumbnail'
                ? 'bg-m3-primary/10 text-m3-primary'
                : 'text-m3-secondary/60 hover:text-m3-on-surface hover:bg-m3-outline/10'
            }`}
            title="Vista cuadrícula"
          >
            <LayoutGrid className="w-3.5 h-3.5" />
          </button>
        </div>
      )}
    </div>
  );
};
