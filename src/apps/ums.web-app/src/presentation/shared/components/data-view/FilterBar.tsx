import React from 'react';
import { Filter, ArrowUpDown, ArrowUp, ArrowDown, List, Grid } from 'lucide-react';
import type { FilterOption, SortOption } from '../M3DataView';

interface FilterBarProps {
  filterOptions?: FilterOption[];
  sortOptions?: SortOption[];
  activeFilter?: string;
  activeSort?: string;
  sortOrder?: 'asc' | 'desc';
  viewMode?: 'list' | 'thumbnail';
  onFilterChange?: (val: string) => void;
  onSortChange?: (val: string) => void;
  onSortOrderToggle?: () => void;
  onViewModeChange?: (mode: 'list' | 'thumbnail') => void;
}

export const FilterBar: React.FC<FilterBarProps> = ({
  filterOptions,
  sortOptions,
  activeFilter,
  activeSort,
  sortOrder,
  viewMode,
  onFilterChange,
  onSortChange,
  onSortOrderToggle,
  onViewModeChange,
}) => {
  const hasFilters = filterOptions && onFilterChange && activeFilter !== undefined;
  const hasSort =
    sortOptions && onSortChange && activeSort !== undefined && sortOrder !== undefined;

  if (!hasFilters && !hasSort && !onViewModeChange) {
    return null;
  }

  return (
    <div className="flex items-center gap-2 py-2 px-3 border-b border-m3-outline/10">
      {/* Filter */}
      {hasFilters && (
        <div className="flex items-center gap-1.5">
          <div className="p-1 bg-m3-surface-container rounded text-m3-secondary">
            <Filter className="w-3.5 h-3.5" />
          </div>
          <select
            value={activeFilter}
            onChange={e => onFilterChange(e.target.value)}
            aria-label="Filter"
            className="h-7 px-2 rounded border border-m3-outline/20 bg-m3-surface text-[12px] text-m3-on-surface focus:outline-none focus:border-m3-primary/40 cursor-pointer"
          >
            {filterOptions?.map(f => (
              <option key={f.value} value={f.value}>
                {f.label}
              </option>
            ))}
          </select>
        </div>
      )}

      {/* Sort */}
      {hasSort && (
        <div className="flex items-center gap-1">
          <button
            onClick={onSortOrderToggle}
            className="p-1.5 rounded hover:bg-m3-surface-variant text-m3-secondary hover:text-m3-on-surface transition-colors"
            title={sortOrder === 'asc' ? 'Ascending' : 'Descending'}
          >
            {sortOrder === 'asc' ? (
              <ArrowUp className="w-3.5 h-3.5" />
            ) : (
              <ArrowDown className="w-3.5 h-3.5" />
            )}
          </button>
          <select
            value={activeSort}
            onChange={e => onSortChange(e.target.value)}
            aria-label="Sort by"
            className="h-7 px-2 rounded border border-m3-outline/20 bg-m3-surface text-[12px] text-m3-on-surface focus:outline-none focus:border-m3-primary/40 cursor-pointer"
          >
            {sortOptions?.map(s => (
              <option key={s.value} value={s.value}>
                {s.label}
              </option>
            ))}
          </select>
        </div>
      )}

      {/* Spacer */}
      <div className="flex-1" />

      {/* View Mode Toggle */}
      {onViewModeChange && (
        <div className="flex items-center gap-0.5 bg-m3-surface-container rounded p-0.5">
          <button
            onClick={() => onViewModeChange('list')}
            className={`p-1 rounded transition-colors ${viewMode === 'list' ? 'bg-m3-primary text-white' : 'text-m3-secondary hover:text-m3-on-surface'}`}
            title="List view"
          >
            <List className="w-3.5 h-3.5" />
          </button>
          <button
            onClick={() => onViewModeChange('thumbnail')}
            className={`p-1 rounded transition-colors ${viewMode === 'thumbnail' ? 'bg-m3-primary text-white' : 'text-m3-secondary hover:text-m3-on-surface'}`}
            title="Thumbnail view"
          >
            <Grid className="w-3.5 h-3.5" />
          </button>
        </div>
      )}
    </div>
  );
};
