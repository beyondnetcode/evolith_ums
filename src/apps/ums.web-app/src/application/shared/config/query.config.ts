/**
 * Query Configuration Constants
 *
 * Centralized staleTime and gcTime configuration for TanStack Query.
 * Based on data volatility analysis per bounded context.
 *
 * Categories:
 * - STATIC: Reference data that rarely changes (5-10 min)
 * - DYNAMIC: Operational data that changes frequently (30s-1min)
 * - USER_CONTEXT: User-specific data (1-2 min)
 * - LOOKUP: One-time lookups (shorter staleTime to ensure freshness)
 */

export const QUERY_CONFIG = {
  // Static reference data - rarely changes
  STATIC: {
    staleTime: 5 * 60 * 1000, // 5 minutes
    gcTime: 10 * 60 * 1000, // 10 minutes
  },

  // Semi-static configuration data
  CONFIGURATION: {
    staleTime: 2 * 60 * 1000, // 2 minutes
    gcTime: 5 * 60 * 1000, // 5 minutes
  },

  // Dynamic operational data - changes frequently
  DYNAMIC: {
    staleTime: 30 * 1000, // 30 seconds
    gcTime: 1 * 60 * 1000, // 1 minute
  },

  // User context data
  USER_CONTEXT: {
    staleTime: 60 * 1000, // 1 minute
    gcTime: 2 * 60 * 1000, // 2 minutes
  },

  // List views with pagination
  LIST: {
    staleTime: 30 * 1000, // 30 seconds
    gcTime: 5 * 60 * 1000, // 5 minutes
  },

  // Detail views (single item)
  DETAIL: {
    staleTime: 60 * 1000, // 1 minute
    gcTime: 5 * 60 * 1000, // 5 minutes
  },

  // Lookup queries (by ID) - should be fresh
  LOOKUP: {
    staleTime: 30 * 1000, // 30 seconds
    gcTime: 2 * 60 * 1000, // 2 minutes
  },
} as const;

// Bounded Context mapping
export const CONTEXT_QUERY_CONFIG = {
  // Identity bounded context
  TENANT: QUERY_CONFIG.STATIC,
  USER_ACCOUNT: QUERY_CONFIG.STATIC,
  DELEGATION: QUERY_CONFIG.DYNAMIC,
  BRANCH: QUERY_CONFIG.CONFIGURATION,

  // Authorization bounded context
  SYSTEM_SUITE: QUERY_CONFIG.STATIC,
  PROFILE: QUERY_CONFIG.USER_CONTEXT,
  PERMISSION_TEMPLATE: QUERY_CONFIG.CONFIGURATION,
  ROLE: QUERY_CONFIG.CONFIGURATION,

  // Configuration bounded context
  FEATURE_FLAG: QUERY_CONFIG.DYNAMIC,
  APP_CONFIGURATION: QUERY_CONFIG.CONFIGURATION,
  PARAMETER_CATALOG: QUERY_CONFIG.STATIC,
} as const;

export type QueryConfigKey = keyof typeof QUERY_CONFIG;
