/**
 * permission-template.service.ts
 *
 * Queries  → REST via httpClient
 * Commands → REST via httpClient
 * All responses are validated at runtime with Zod before returning.
 */
import { httpClient } from '@infra/http/httpClient';
import { logger } from '@app/utils/logger';
import {
  PermissionTemplatePageSchema,
  PermissionTemplateDetailSchema,
  CreatePermissionTemplateResponseSchema,
  type PermissionTemplatePage,
  type PermissionTemplateDetail,
  type CreatePermissionTemplatePayload,
  type CreatePermissionTemplateResponse,
  type AddTemplateItemPayload,
} from '@domain/authorization/schemas/permission-template.schema';

type PermissionTemplateListParams = {
  page?: number;
  pageSize?: number;
  search?: string;
  criteria?: string;
  status?: string;
  sortBy?: string;
  sortOrder?: string;
  tenantId?: string;
  systemSuiteId?: string;
  roleId?: string;
};

function buildPermissionTemplateQueryString(
  params: Required<Pick<PermissionTemplateListParams, 'page' | 'pageSize'>> &
    PermissionTemplateListParams
): string {
  const searchParams = new URLSearchParams();
  searchParams.set('page', String(params.page));
  searchParams.set('pageSize', String(params.pageSize));
  if (params.search !== undefined) searchParams.set('search', params.search);
  if (params.criteria !== undefined) searchParams.set('criteria', params.criteria);
  if (params.status !== undefined) searchParams.set('status', params.status);
  if (params.sortBy !== undefined) searchParams.set('sortBy', params.sortBy);
  if (params.sortOrder !== undefined) searchParams.set('sortOrder', params.sortOrder);
  if (params.tenantId !== undefined) searchParams.set('tenantId', params.tenantId);
  if (params.systemSuiteId !== undefined) searchParams.set('systemSuiteId', params.systemSuiteId);
  if (params.roleId !== undefined) searchParams.set('roleId', params.roleId);
  return searchParams.toString();
}

export const permissionTemplateService = {
  // ── Queries (REST) ────────────────────────────────────────────────────────────

  getAll: async (params?: PermissionTemplateListParams): Promise<PermissionTemplatePage> => {
    const queryString = buildPermissionTemplateQueryString({
      page: params?.page ?? 1,
      pageSize: params?.pageSize ?? 20,
      search: params?.search,
      criteria: params?.criteria,
      status: params?.status,
      sortBy: params?.sortBy,
      sortOrder: params?.sortOrder,
      tenantId: params?.tenantId,
      systemSuiteId: params?.systemSuiteId,
      roleId: params?.roleId,
    });

    const { data } = await httpClient.get<PermissionTemplatePage>(
      `/permission-templates?${queryString}`
    );

    const result = PermissionTemplatePageSchema.safeParse(data);
    if (!result.success) {
      logger.error('Invalid REST response for permission templates', result.error);
      throw new Error('Invalid response shape for permission templates');
    }
    return result.data;
  },

  getById: async (templateId: string): Promise<PermissionTemplateDetail> => {
    const { data } = await httpClient.get<PermissionTemplateDetail>(
      `/permission-templates/${templateId}`
    );
    if (!data) throw new Error('Permission template not found');
    return PermissionTemplateDetailSchema.parse(data);
  },

  // ── Commands (REST) ──────────────────────────────────────────────────────────

  create: async (
    payload: CreatePermissionTemplatePayload
  ): Promise<CreatePermissionTemplateResponse> => {
    const { data } = await httpClient.post('/permission-templates', payload);
    return CreatePermissionTemplateResponseSchema.parse(data);
  },

  publish: async (templateId: string): Promise<void> => {
    await httpClient.post(`/permission-templates/${templateId}/publish`);
  },

  deprecate: async (templateId: string): Promise<void> => {
    await httpClient.post(`/permission-templates/${templateId}/deprecate`);
  },

  delete: async (templateId: string): Promise<void> => {
    await httpClient.delete(`/permission-templates/${templateId}`);
  },

  addItem: async (templateId: string, payload: AddTemplateItemPayload): Promise<void> => {
    await httpClient.post(`/permission-templates/${templateId}/items`, payload);
  },

  removeItem: async (templateId: string, itemId: string): Promise<void> => {
    await httpClient.delete(`/permission-templates/${templateId}/items/${itemId}`);
  },

  setItemEffect: async (
    templateId: string,
    itemId: string,
    effect: 'Allow' | 'Deny' | 'Neutral'
  ): Promise<void> => {
    await httpClient.put(`/permission-templates/${templateId}/items/${itemId}/effect`, { effect });
  },

  activateItem: async (templateId: string, itemId: string): Promise<void> => {
    await httpClient.post(`/permission-templates/${templateId}/items/${itemId}/activate`);
  },

  deactivateItem: async (templateId: string, itemId: string): Promise<void> => {
    await httpClient.post(`/permission-templates/${templateId}/items/${itemId}/deactivate`);
  },
};

export default permissionTemplateService;
