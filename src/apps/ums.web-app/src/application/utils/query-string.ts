export interface PaginationParams {
  page?: number;
  pageSize?: number;
}

export interface SortParams {
  sortBy?: string;
  sortOrder?: 'asc' | 'desc';
}

export interface SearchParams {
  search?: string;
  criteria?: string;
  status?: string;
}

export type BaseQueryParams = PaginationParams & SortParams & SearchParams;

export function buildQueryString<T extends Record<string, unknown>>(
  params: T,
  options?: {
    defaults?: Partial<PaginationParams>;
    excludeKeys?: (keyof T)[];
  }
): string {
  const { defaults = { page: 1, pageSize: 20 }, excludeKeys = [] } = options ?? {};

  const p = new URLSearchParams();

  const entries = Object.entries(params).filter(([key]) => !excludeKeys.includes(key as keyof T));

  for (const [key, value] of entries) {
    if (value !== undefined && value !== null && value !== '') {
      if (key === 'page' || key === 'pageSize') {
        p.set(key, String(value ?? (key === 'page' ? defaults.page : defaults.pageSize)));
      } else {
        p.set(key, String(value));
      }
    }
  }

  if (!p.has('page')) p.set('page', String(defaults.page ?? 1));
  if (!p.has('pageSize')) p.set('pageSize', String(defaults.pageSize ?? 20));

  return p.toString();
}

export function buildPaginationParams(page?: number, pageSize?: number): string {
  const p = new URLSearchParams();
  p.set('page', String(page ?? 1));
  p.set('pageSize', String(pageSize ?? 20));
  return p.toString();
}
