import React from 'react';
import {
  LayoutList,
  LayoutGrid,
  Filter,
  ArrowUpDown,
  ChevronsUpDown,
  ChevronsDownUp,
} from 'lucide-react';
import { M3SegmentedButton } from './M3SegmentedButton';
import type { SegmentOption } from './M3SegmentedButton';
import { formatItemCountLabel } from '@shared/utils/pluralize';

export interface PermissionSectionSortOption {
  label: string;
  value: string;
}

export interface PermissionSectionFilterOption {
  label: string;
  value: string;
}

interface PermissionSectionToolbarProps {
  viewMode: 'list' | 'thumbnail' | 'tree';
  onViewModeChange: (mode: 'list' | 'thumbnail' | 'tree') => void;
  filterOptions?: PermissionSectionFilterOption[];
  activeFilter?: string;
  onFilterChange?: (value: string) => void;
  sortOptions?: PermissionSectionSortOption[];
  sortBy?: string;
  onSortByChange?: (value: string) => void;
  sortOrder?: 'asc' | 'desc';
  onSortOrderToggle?: () => void;
  itemCount: number;
  itemLabel: string;
  /** Plural explícito para préstamos/irregulares; si se omite se deriva del singular. */
  itemLabelPlural?: string;
  showExpandCollapse?: boolean;
  allExpanded?: boolean;
  onToggleExpandAll?: () => void;
  viewModeOptions?: SegmentOption<'list' | 'thumbnail' | 'tree'>[];
}

const DEFAULT_VIEW_MODE_OPTIONS: SegmentOption<'list' | 'thumbnail' | 'tree'>[] = [
  { value: 'list', label: <LayoutList className="w-3.5 h-3.5" /> },
  { value: 'thumbnail', label: <LayoutGrid className="w-3.5 h-3.5" /> },
];

const TREE_VIEW_MODE_OPTIONS: SegmentOption<'list' | 'thumbnail' | 'tree'>[] = [
  { value: 'tree', label: <LayoutList className="w-3.5 h-3.5" /> },
];

export const PermissionSectionToolbar: React.FC<PermissionSectionToolbarProps> = ({
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
  showExpandCollapse,
  allExpanded,
  onToggleExpandAll,
  viewModeOptions,
}) => {
  const modes =
    viewModeOptions ?? (viewMode === 'tree' ? TREE_VIEW_MODE_OPTIONS : DEFAULT_VIEW_MODE_OPTIONS);

  return (
    <div className="flex items-center justify-between gap-2 py-2 px-1 border-b border-m3-outline/10 mb-3">
      <div className="flex items-center gap-2 flex-wrap">
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

        {showExpandCollapse && onToggleExpandAll && (
          <button
            type="button"
            onClick={onToggleExpandAll}
            className="flex items-center gap-1 text-[10px] font-medium text-m3-secondary hover:text-m3-on-surface transition-colors px-1.5 py-0.5 rounded-md hover:bg-m3-surface-variant/25 border border-transparent hover:border-m3-outline/20"
            title={allExpanded ? 'Colapsar todo' : 'Expandir todo'}
          >
            {allExpanded ? (
              <>
                <ChevronsDownUp className="w-3 h-3" /> Colapsar
              </>
            ) : (
              <>
                <ChevronsUpDown className="w-3 h-3" /> Expandir
              </>
            )}
          </button>
        )}
      </div>

      <M3SegmentedButton options={modes} value={viewMode} onChange={onViewModeChange} size="sm" />
    </div>
  );
};
