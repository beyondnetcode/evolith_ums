/**
 * system-info.constants.ts — System health and architecture info.
 *
 * M-6: Moved hardcoded values from ProfileScreen.tsx.
 * These should eventually come from a /health or /diagnostics endpoint.
 */

export const SYSTEM_INFO = {
  apiPort: '5293 [HTTP]',
  dbEngine: 'PostgreSQL 16',
  dbStatus: 'Connected',
  architecture: 'Clean Hexagonal (.NET 8)',
} as const;
