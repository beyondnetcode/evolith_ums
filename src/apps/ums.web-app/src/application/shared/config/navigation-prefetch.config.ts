/**
 * navigation-prefetch.config.ts
 *
 * Defines prefetch strategies for navigation items.
 * Uses dynamic imports to avoid bloating the main bundle -
 * services are only loaded when prefetch actually executes on hover.
 *
 * Stale time is set to 30s to balance freshness with prefetch benefits.
 */
import type { NavItemId } from '@shared/layouts/navigation.config';

export interface PrefetchEntry {
  queryKey: string[];
  queryFn: () => Promise<unknown>;
  staleTime?: number;
}

const DEFAULT_PAGE = 1;
const DEFAULT_PAGE_SIZE = 10;
const PREFETCH_STALE_TIME = 30_000;

export const NAV_PREFETCH_CONFIG: Partial<Record<NavItemId, PrefetchEntry>> = {
  tenants: {
    queryKey: ['tenants', DEFAULT_PAGE, DEFAULT_PAGE_SIZE, undefined, 'name', 'all', 'name', 'asc'],
    queryFn: () =>
      import('@infra/identity/services/tenant.service').then(m =>
        m.default.getAll({
          page: DEFAULT_PAGE,
          pageSize: DEFAULT_PAGE_SIZE,
          criteria: 'name',
          status: 'all',
          sortBy: 'name',
          sortOrder: 'asc',
        })
      ),
    staleTime: PREFETCH_STALE_TIME,
  },
  users: {
    queryKey: [
      'user-accounts',
      DEFAULT_PAGE,
      DEFAULT_PAGE_SIZE,
      undefined,
      'name',
      'all',
      'name',
      'asc',
    ],
    queryFn: () =>
      import('@infra/identity/services/user-account.service').then(m =>
        m.default.getAll({
          page: DEFAULT_PAGE,
          pageSize: DEFAULT_PAGE_SIZE,
          criteria: 'name',
          status: 'all',
          sortBy: 'name',
          sortOrder: 'asc',
        })
      ),
    staleTime: PREFETCH_STALE_TIME,
  },
  delegations: {
    queryKey: [
      'delegations',
      DEFAULT_PAGE,
      DEFAULT_PAGE_SIZE,
      undefined,
      'name',
      'all',
      'name',
      'asc',
    ],
    queryFn: () =>
      import('@infra/identity/services/delegation.service').then(m =>
        m.default.getAll({
          page: DEFAULT_PAGE,
          pageSize: DEFAULT_PAGE_SIZE,
          criteria: 'name',
          status: 'all',
          sortBy: 'name',
          sortOrder: 'asc',
        })
      ),
    staleTime: PREFETCH_STALE_TIME,
  },
  systemSuites: {
    queryKey: [
      'system-suites',
      DEFAULT_PAGE,
      DEFAULT_PAGE_SIZE,
      undefined,
      'name',
      'all',
      'name',
      'asc',
    ],
    queryFn: () =>
      import('@infra/authorization/services/system-suite.service').then(m =>
        m.default.getAll({
          page: DEFAULT_PAGE,
          pageSize: DEFAULT_PAGE_SIZE,
          criteria: 'name',
          status: 'all',
          sortBy: 'name',
          sortOrder: 'asc',
        })
      ),
    staleTime: PREFETCH_STALE_TIME,
  },
  permissionTemplates: {
    queryKey: [
      'permission-templates',
      DEFAULT_PAGE,
      DEFAULT_PAGE_SIZE,
      undefined,
      'name',
      'all',
      'name',
      'asc',
    ],
    queryFn: () =>
      import('@infra/authorization/services/permission-template.service').then(m =>
        m.default.getAll({
          page: DEFAULT_PAGE,
          pageSize: DEFAULT_PAGE_SIZE,
          criteria: 'name',
          status: 'all',
          sortBy: 'name',
          sortOrder: 'asc',
        })
      ),
    staleTime: PREFETCH_STALE_TIME,
  },
  profiles: {
    queryKey: [
      'profiles',
      DEFAULT_PAGE,
      DEFAULT_PAGE_SIZE,
      undefined,
      'name',
      'all',
      'name',
      'asc',
    ],
    queryFn: () =>
      import('@infra/authorization/services/profile.service').then(m =>
        m.default.getAll({
          page: DEFAULT_PAGE,
          pageSize: DEFAULT_PAGE_SIZE,
          criteria: 'name',
          status: 'all',
          sortBy: 'name',
          sortOrder: 'asc',
        })
      ),
    staleTime: PREFETCH_STALE_TIME,
  },
  featureFlags: {
    queryKey: [
      'feature-flags',
      DEFAULT_PAGE,
      DEFAULT_PAGE_SIZE,
      undefined,
      'name',
      'all',
      'name',
      'asc',
    ],
    queryFn: () =>
      import('@infra/configuration/services/feature-flag.service').then(m =>
        m.default.getAll({
          page: DEFAULT_PAGE,
          pageSize: DEFAULT_PAGE_SIZE,
          criteria: 'name',
          status: 'all',
          sortBy: 'name',
          sortOrder: 'asc',
        })
      ),
    staleTime: PREFETCH_STALE_TIME,
  },
  appConfigurations: {
    queryKey: ['app-configurations', 'list', { page: DEFAULT_PAGE, pageSize: DEFAULT_PAGE_SIZE }],
    queryFn: () =>
      import('@infra/configuration/services/app-configuration.service').then(m =>
        m.default.getAll({ page: DEFAULT_PAGE, pageSize: DEFAULT_PAGE_SIZE })
      ),
    staleTime: PREFETCH_STALE_TIME,
  },
  parameterCatalog: {
    queryKey: [
      'parameter-catalogs',
      DEFAULT_PAGE,
      DEFAULT_PAGE_SIZE,
      undefined,
      'name',
      'all',
      'name',
      'asc',
    ],
    queryFn: () =>
      import('@infra/configuration/services/parameter-catalog/parameter-catalog.service').then(m =>
        m.default.getAll({
          page: DEFAULT_PAGE,
          pageSize: DEFAULT_PAGE_SIZE,
          criteria: 'name',
          status: 'all',
          sortBy: 'name',
          sortOrder: 'asc',
        })
      ),
    staleTime: PREFETCH_STALE_TIME,
  },
};
