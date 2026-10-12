import React from 'react';
import { LayoutList, LayoutGrid, Filter, ArrowUpDown } from 'lucide-react';
import { M3SegmentedButton } from './M3SegmentedButton';
import type { SegmentOption } from './M3SegmentedButton';
import { formatItemCountLabel } from '@shared/utils/pluralize';

export interface ChildEntitySortOption {
  label: string;
  value: string;
}

export interface ChildEntityFilterOption {
  label: string;
  value: string;
}

interface ChildEntityToolbarProps {
  viewMode: 'list' | 'thumbnail';
  onViewModeChange: (mode: 'list' | 'thumbnail') => void;
  filterOptions?: ChildEntityFilterOption[];
  activeFilter?: string;
  onFilterChange?: (value: string) => void;
  sortOptions?: ChildEntitySortOption[];
  sortBy?: string;
  onSortByChange?: (value: string) => void;
  sortOrder?: 'asc' | 'desc';
  onSortOrderToggle?: () => void;
  itemCount: number;
  itemLabel: string;
  /** Plural explícito para préstamos/irregulares; si se omite se deriva del singular. */
  itemLabelPlural?: string;
}

const VIEW_MODE_OPTIONS: SegmentOption<'list' | 'thumbnail'>[] = [
  { value: 'list', label: <LayoutList className="w-3.5 h-3.5" /> },
  { value: 'thumbnail', label: <LayoutGrid className="w-3.5 h-3.5" /> },
];

export const ChildEntityToolbar: React.FC<ChildEntityToolbarProps> = ({
  viewMode,
  onViewModeChange,
  filterOptions,
  activeFilter,
  onFilterChange,
  sortOptions,
  sortBy,
  onSortByChange,
  sortOrder,
  onSortOrderToggle,
  itemCount,
  itemLabel,
  itemLabelPlural,
}) => {
  return (
    <div className="flex items-center justify-between gap-2 py-2 px-1">
      <div className="flex items-center gap-2">
        <span className="text-[10px] font-medium text-m3-secondary/70">
          {formatItemCountLabel(itemCount, itemLabel, itemLabelPlural)}
        </span>

        {filterOptions && onFilterChange && activeFilter !== undefined && (
          <div className="flex items-center gap-1.5">
            <Filter className="w-3 h-3 text-m3-secondary/50" />
            <select
              value={activeFilter}
              onChange={e => onFilterChange(e.target.value)}
              className="h-6 px-2 rounded-md border border-m3-outline/20 bg-m3-surface text-[10px] font-medium text-m3-secondary focus:outline-none focus:border-m3-primary/40 transition-colors cursor-pointer"
            >
              {filterOptions.map(f => (
                <option key={f.value} value={f.value}>
                  {f.label}
                </option>
              ))}
            </select>
          </div>
        )}

        {sortOptions && onSortByChange && sortBy !== undefined && (
          <div className="flex items-center gap-1.5">
            <ArrowUpDown className="w-3 h-3 text-m3-secondary/50" />
            <select
              value={sortBy}
              onChange={e => onSortByChange(e.target.value)}
              className="h-6 px-2 rounded-md border border-m3-outline/20 bg-m3-surface text-[10px] font-medium text-m3-secondary focus:outline-none focus:border-m3-primary/40 transition-colors cursor-pointer"
            >
              {sortOptions.map(s => (
                <option key={s.value} value={s.value}>
                  {s.label}
                </option>
              ))}
            </select>

            {onSortOrderToggle && (
              <button
                type="button"
                onClick={onSortOrderToggle}
                className="h-6 w-6 flex items-center justify-center rounded-md border border-m3-outline/20 bg-m3-surface hover:bg-m3-primary/10 hover:border-m3-primary/30 transition-all text-m3-secondary"
                title={sortOrder === 'asc' ? 'Ascending' : 'Descending'}
              >
                <ArrowUpDown
                  className={`w-3 h-3 transition-transform duration-200 ${sortOrder === 'desc' ? 'rotate-180 text-m3-primary' : ''}`}
                />
              </button>
            )}
          </div>
        )}
      </div>

      <M3SegmentedButton
        options={VIEW_MODE_OPTIONS}
        value={viewMode}
        onChange={onViewModeChange}
        size="sm"
      />
    </div>
  );
};
