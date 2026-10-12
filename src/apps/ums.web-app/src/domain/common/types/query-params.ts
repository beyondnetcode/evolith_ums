export type SortOrder = 'asc' | 'desc';

export interface BaseQueryParams {
  page: number;
  pageSize: number;
  search?: string;
  criteria?: string;
  status?: string;
  sortBy?: string;
  sortOrder?: SortOrder;
  tenantId?: string;
}

export interface PaginationState {
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
  startIndex?: number;
}

export interface QueryState {
  searchValue: string;
  searchCriteria: string;
  activeFilter: string;
  sortBy: string;
  sortOrder: SortOrder;
  appliedQuery: {
    term: string;
    filterApplied: boolean;
  };
  setSearchValue: (value: string) => void;
  setSearchCriteria: (criteria: string) => void;
  setActiveFilter: (filter: string) => void;
  setSortBy: (sortBy: string) => void;
  setSortOrder: (order: SortOrder) => void;
  handleQuerySubmit: () => void;
  handleResetQuery: () => void;
}
