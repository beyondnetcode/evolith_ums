/**
 * useEntityList.ts — Generic hook for list state management.
 *
 * Combines pagination, search, filtering, sorting, and hierarchy
 * into a single reusable hook. Any aggregate list panel can use this
 * instead of duplicating state variables.
 *
 * Usage:
 *   const list = useEntityList({ pageSize: 10 });
 *   // list.page, list.setPage, list.searchValue, list.setSearchValue, etc.
 */
import { useState, useCallback } from 'react';

interface UseEntityListOptions {
  pageSize?: number;
  initialSortBy?: string;
  initialSortOrder?: 'asc' | 'desc';
  initialFilter?: string;
  initialSearchCriteria?: string;
}

interface UseEntityListReturn {
  viewMode: 'list' | 'thumbnail';
  setViewMode: React.Dispatch<React.SetStateAction<'list' | 'thumbnail'>>;
  searchCriteria: string;
  setSearchCriteria: React.Dispatch<React.SetStateAction<string>>;
  searchValue: string;
  setSearchValue: React.Dispatch<React.SetStateAction<string>>;
  appliedQuery: { criteria: string; term: string };
  setAppliedQuery: React.Dispatch<React.SetStateAction<{ criteria: string; term: string }>>;
  activeFilter: string;
  setActiveFilter: React.Dispatch<React.SetStateAction<string>>;
  sortBy: string;
  setSortBy: React.Dispatch<React.SetStateAction<string>>;
  sortOrder: 'asc' | 'desc';
  setSortOrder: React.Dispatch<React.SetStateAction<'asc' | 'desc'>>;
  page: number;
  setPage: React.Dispatch<React.SetStateAction<number>>;
  pageSize: number;
  handleQuerySubmit: (e: React.FormEvent) => void;
  handleResetQuery: () => void;
  handleFilterChange: (val: string) => void;
}

export function useEntityList(options: UseEntityListOptions = {}): UseEntityListReturn {
  const {
    pageSize = 10,
    initialSortBy = 'name',
    initialSortOrder = 'asc',
    initialFilter = 'all',
    initialSearchCriteria = 'name',
  } = options;

  const [viewMode, setViewMode] = useState<'list' | 'thumbnail'>('list');
  const [searchCriteria, setSearchCriteria] = useState(initialSearchCriteria);
  const [searchValue, setSearchValue] = useState('');
  const [appliedQuery, setAppliedQuery] = useState<{ criteria: string; term: string }>({
    criteria: initialSearchCriteria,
    term: '',
  });
  const [activeFilter, setActiveFilter] = useState(initialFilter);
  const [sortBy, setSortBy] = useState(initialSortBy);
  const [sortOrder, setSortOrder] = useState<'asc' | 'desc'>(initialSortOrder);
  const [page, setPage] = useState(1);

  const handleQuerySubmit = useCallback(
    (e: React.FormEvent) => {
      e.preventDefault();
      setPage(1);
      setAppliedQuery({ criteria: searchCriteria, term: searchValue });
    },
    [searchCriteria, searchValue]
  );

  const handleResetQuery = useCallback(() => {
    setSearchValue('');
    setAppliedQuery({ criteria: initialSearchCriteria, term: '' });
    setPage(1);
  }, [initialSearchCriteria]);

  const handleFilterChange = useCallback((val: string) => {
    setActiveFilter(val);
    setPage(1);
  }, []);

  return {
    viewMode,
    setViewMode,
    searchCriteria,
    setSearchCriteria,
    searchValue,
    setSearchValue,
    appliedQuery,
    setAppliedQuery,
    activeFilter,
    setActiveFilter,
    sortBy,
    setSortBy,
    sortOrder,
    setSortOrder,
    page,
    setPage,
    pageSize,
    handleQuerySubmit,
    handleResetQuery,
    handleFilterChange,
  };
}
