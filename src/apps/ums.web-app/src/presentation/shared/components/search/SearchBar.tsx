import React from 'react';
import { Search } from 'lucide-react';
import { M3TextField } from '../M3TextField';

export interface QueryCriteriaOption {
  label: string;
  value: string;
}

interface SearchBarProps {
  criteriaOptions?: QueryCriteriaOption[];
  activeCriteria?: string;
  onCriteriaChange?: (val: string) => void;
  searchValue: string;
  onSearchValueChange: (val: string) => void;
  onSubmit: (e: React.FormEvent) => void;
  searchPlaceholder?: string;
  criteriaLabel?: string;
  searchTermLabel?: string;
  searchButtonLabel?: string;
}

export const SearchBar: React.FC<SearchBarProps> = ({
  criteriaOptions,
  activeCriteria,
  onCriteriaChange,
  searchValue,
  onSearchValueChange,
  onSubmit,
  searchPlaceholder = 'Buscar...',
  criteriaLabel = 'Criterio',
  searchTermLabel = 'Término de búsqueda',
  searchButtonLabel = 'Buscar',
}) => {
  return (
    <form onSubmit={onSubmit} className="flex flex-col gap-2 sm:flex-row sm:items-end">
      {criteriaOptions && onCriteriaChange && activeCriteria !== undefined && (
        <div className="flex w-full flex-col sm:w-44 sm:flex-shrink-0">
          <label className="block text-[11px] font-medium text-m3-secondary mb-1 ml-1">
            {criteriaLabel}
          </label>
          <select
            value={activeCriteria}
            onChange={e => onCriteriaChange(e.target.value)}
            aria-label={criteriaLabel}
            className="w-full h-10 px-3 text-xs rounded-lg border border-m3-outline bg-m3-surface-container/30 text-m3-on-surface focus:outline-none focus:border-m3-primary transition-colors cursor-pointer"
          >
            {criteriaOptions.map(c => (
              <option key={c.value} value={c.value}>
                {c.label}
              </option>
            ))}
          </select>
        </div>
      )}

      <div className="w-full sm:min-w-0 sm:flex-1">
        <M3TextField
          dense
          label={searchTermLabel}
          value={searchValue}
          onChange={e => onSearchValueChange(e.target.value)}
          placeholder={searchPlaceholder}
          className="mb-0"
        />
      </div>

      <button
        type="submit"
        className="inline-flex h-10 shrink-0 items-center justify-center gap-1.5 rounded-lg border border-m3-primary/25 bg-m3-primary/10 px-4 text-xs font-medium text-m3-primary transition-colors hover:border-m3-primary/45 hover:bg-m3-primary/15"
      >
        <Search className="h-3.5 w-3.5" /> {searchButtonLabel}
      </button>
    </form>
  );
};
