/**
 * tenant.service.ts
 *
 * Infrastructure service for the Identity / Tenant bounded context.
 * Queries and commands use REST via httpClient.
 * All responses are validated at runtime with Zod before returning.
 */
import { httpClient } from '@infra/http/httpClient';
import { logger } from '@app/utils/logger';
import {
  TenantPageSchema,
  TenantSchema,
  CreateTenantResponseSchema,
  type Tenant,
  type TenantPage,
  type CreateTenantPayload,
  type CreateTenantResponse,
} from '@domain/identity/schemas/tenant.schema';
import {
  BranchListSchema,
  BranchLifecycleSchema,
  AddBranchResponseSchema,
  type Branch,
  type BranchLifecycleEntry,
  type AddBranchPayload,
  type AddBranchResponse,
} from '@domain/identity/schemas/branch.schema';

type TenantListParams = {
  page?: number;
  pageSize?: number;
  search?: string;
  criteria?: string;
  status?: string;
  sortBy?: string;
  sortOrder?: string;
};

function buildTenantQueryString(
  params: Required<Pick<TenantListParams, 'page' | 'pageSize'>> & TenantListParams
): string {
  const searchParams = new URLSearchParams();
  searchParams.set('page', String(params.page));
  searchParams.set('pageSize', String(params.pageSize));
  if (params.search !== undefined) searchParams.set('search', params.search);
  if (params.criteria !== undefined) searchParams.set('criteria', params.criteria);
  if (params.status !== undefined) searchParams.set('status', params.status);
  if (params.sortBy !== undefined) searchParams.set('sortBy', params.sortBy);
  if (params.sortOrder !== undefined) searchParams.set('sortOrder', params.sortOrder);
  return searchParams.toString();
}

async function getTenantsViaRest(
  params: Required<Pick<TenantListParams, 'page' | 'pageSize'>> & TenantListParams
): Promise<TenantPage> {
  const { data } = await httpClient.get<TenantPage>(`/tenants?${buildTenantQueryString(params)}`);
  const pageResult = TenantPageSchema.safeParse(data);
  if (!pageResult.success) {
    logger.error('Invalid REST response shape for tenants query', pageResult.error);
    throw new Error('Invalid REST response shape for tenants query');
  }
  return pageResult.data;
}

import { RequirePermission } from '@app/authorization/decorators/require-permission.decorator';

export class TenantService {
  // ── Queries (REST) ────────────────────────────────────────────────────────

  @RequirePermission('TENANT', 'VIEW')
  async getAll(params?: TenantListParams): Promise<TenantPage> {
    const normalizedParams = {
      page: params?.page ?? 1,
      pageSize: params?.pageSize ?? 20,
      search: params?.search,
      criteria: params?.criteria,
      status: params?.status,
      sortBy: params?.sortBy,
      sortOrder: params?.sortOrder,
    };

    return getTenantsViaRest(normalizedParams);
  }

  @RequirePermission('TENANT', 'VIEW')
  async getById(tenantId: string): Promise<Tenant> {
    const { data } = await httpClient.get<Tenant>(`/tenants/${tenantId}`);
    return TenantSchema.parse(data);
  }

  /**
   * ADR-0164: el listado excluye por defecto las sucursales CERRADAS. Solo se piden cuando alguien
   * quiere consultar el histórico, y entonces llegan marcadas con `isClosed`.
   */
  @RequirePermission('TENANT', 'VIEW')
  async getBranches(tenantId: string, includeClosed = false): Promise<Branch[]> {
    const query = includeClosed ? '?includeClosed=true' : '';
    const { data } = await httpClient.get<Branch[]>(`/tenants/${tenantId}/branches${query}`);
    return BranchListSchema.parse(data);
  }

  /** Bitácora de la sucursal: apertura, desactivaciones, reactivaciones y cierre definitivo. */
  @RequirePermission('TENANT', 'VIEW')
  async getBranchLifecycle(tenantId: string, branchId: string): Promise<BranchLifecycleEntry[]> {
    const { data } = await httpClient.get<BranchLifecycleEntry[]>(
      `/tenants/${tenantId}/branches/${branchId}/bitacora`
    );
    return BranchLifecycleSchema.parse(data);
  }

  // ── Commands (REST) ───────────────────────────────────────────────────────

  @RequirePermission('TENANT', 'CREATE')
  async createTenant(payload: CreateTenantPayload): Promise<CreateTenantResponse> {
    const { data } = await httpClient.post('/tenants', payload);
    return CreateTenantResponseSchema.parse(data);
  }

  @RequirePermission('TENANT', 'MANAGE')
  async activateTenant(tenantId: string): Promise<void> {
    await httpClient.post(`/tenants/${tenantId}/activate`);
  }

  @RequirePermission('TENANT', 'MANAGE')
  async setManagementOwner(tenantId: string, value: boolean): Promise<void> {
    await httpClient.post(`/tenants/${tenantId}/set-management-owner`, { value });
  }

  @RequirePermission('TENANT', 'MANAGE')
  async updateTenant(
    tenantId: string,
    payload: { name: string; type: string; companyReference?: string | null }
  ): Promise<void> {
    await httpClient.put(`/tenants/${tenantId}`, payload);
  }

  @RequirePermission('TENANT', 'MANAGE')
  async suspendTenant(tenantId: string): Promise<void> {
    await httpClient.post(`/tenants/${tenantId}/suspend`);
  }

  @RequirePermission('TENANT', 'MANAGE')
  async addBranch(tenantId: string, payload: AddBranchPayload): Promise<AddBranchResponse> {
    const { data } = await httpClient.post(`/tenants/${tenantId}/branches`, payload);
    return AddBranchResponseSchema.parse(data);
  }

  @RequirePermission('TENANT', 'MANAGE')
  async updateBranch(
    tenantId: string,
    branchId: string,
    payload: { name: string; geofencingMetadata?: string | null }
  ): Promise<void> {
    await httpClient.put(`/tenants/${tenantId}/branches/${branchId}`, payload);
  }

  /**
   * Cierra DEFINITIVAMENTE la sucursal (ADR-0164). El verbo HTTP sigue siendo DELETE y la ruta no
   * cambió, pero detrás ya no hay borrado: la fila permanece, el código queda ocupado y no se puede
   * reactivar. El nombre del método dice lo que pasa, no lo que pasaba.
   */
  @RequirePermission('TENANT', 'MANAGE')
  async closeBranch(tenantId: string, branchId: string, reason?: string): Promise<void> {
    const query = reason?.trim() ? `?reason=${encodeURIComponent(reason.trim())}` : '';
    await httpClient.delete(`/tenants/${tenantId}/branches/${branchId}${query}`);
  }

  @RequirePermission('TENANT', 'MANAGE')
  async deactivateBranch(tenantId: string, branchId: string): Promise<void> {
    await httpClient.post(`/tenants/${tenantId}/branches/${branchId}/deactivate`);
  }

  @RequirePermission('TENANT', 'MANAGE')
  async reactivateBranch(tenantId: string, branchId: string): Promise<void> {
    await httpClient.post(`/tenants/${tenantId}/branches/${branchId}/reactivate`);
  }
}

export const tenantService = new TenantService();
export default tenantService;
