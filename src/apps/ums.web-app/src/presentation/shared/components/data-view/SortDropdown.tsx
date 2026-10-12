import React from 'react';
import { ArrowUpDown } from 'lucide-react';
import type { SortOption } from '../M3DataView';

interface SortDropdownProps {
  sortOptions?: SortOption[];
  sortBy?: string;
  onSortByChange?: (val: string) => void;
  sortOrder?: 'asc' | 'desc';
  onSortOrderToggle?: () => void;
}

export const SortDropdown: React.FC<SortDropdownProps> = ({
  sortOptions,
  sortBy,
  onSortByChange,
  sortOrder,
  onSortOrderToggle,
}) => {
  if (!sortOptions || !onSortByChange || sortBy === undefined) {
    return null;
  }

  return (
    <div className="flex items-center gap-2">
      <div className="p-1 bg-m3-primary/10 rounded-md text-m3-primary border border-m3-primary/10">
        <ArrowUpDown className="w-3.5 h-3.5" />
      </div>
      <select
        value={sortBy}
        onChange={e => onSortByChange(e.target.value)}
        aria-label="Sort by"
        className="h-8 px-2.5 rounded-lg border border-m3-outline bg-m3-surface text-[12px] font-medium text-m3-secondary focus:outline-none focus:border-m3-primary transition-colors cursor-pointer"
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
          className="h-8 w-8 flex items-center justify-center rounded-lg border border-m3-outline bg-m3-surface hover:bg-m3-primary/10 hover:border-m3-primary/30 transition-all text-m3-secondary"
          title={sortOrder === 'asc' ? 'Ascending' : 'Descending'}
        >
          <ArrowUpDown
            className={`w-3.5 h-3.5 transition-transform duration-200 ${sortOrder === 'desc' ? 'rotate-180 text-m3-primary' : ''}`}
          />
        </button>
      )}
    </div>
  );
};
