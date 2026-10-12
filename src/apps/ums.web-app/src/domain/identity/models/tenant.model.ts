/**
 * tenant.model.ts
 *
 * Re-exports types from the Zod schema (single source of truth).
 * The schema file owns both the runtime validator and the inferred TS types.
 */
export type {
  Tenant,
  TenantPage,
  TenantStatus,
  CreateTenantPayload,
  CreateTenantResponse,
} from '../schemas/tenant.schema';
