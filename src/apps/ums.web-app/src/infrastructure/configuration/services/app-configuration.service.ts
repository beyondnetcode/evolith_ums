/**
 * app-configuration.service.ts
 *
 * Infrastructure service for the Configuration / AppConfiguration bounded context.
 * Queries and commands use REST via httpClient.
 */
import { httpClient } from '@infra/http/httpClient';
import { logger } from '@app/utils/logger';
import {
  AppConfigurationSchema,
  AppConfigurationPageSchema,
  CreateAppConfigurationResponseSchema,
  type AppConfiguration,
  type AppConfigurationPage,
  type CreateAppConfigurationResponse,
} from '@domain/configuration/schemas/app-configuration.schema';
import type {
  CreateAppConfigurationPayload,
  UpdateAppConfigurationPayload,
} from '@domain/configuration/schemas/app-configuration.commands.schema';

function buildQueryString(params?: {
  page?: number;
  pageSize?: number;
  search?: string;
  criteria?: string;
  status?: string;
  sortBy?: string;
  sortOrder?: string;
  scope?: string;
  tenantId?: string;
  systemSuiteId?: string;
  moduleId?: string;
}): string {
  const p = new URLSearchParams();
  if (params) {
    p.set('page', String(params.page ?? 1));
    p.set('pageSize', String(params.pageSize ?? 20));
    if (params.search) p.set('search', params.search);
    if (params.criteria) p.set('criteria', params.criteria);
    if (params.status) p.set('status', params.status);
    if (params.sortBy) p.set('sortBy', params.sortBy);
    if (params.sortOrder) p.set('sortOrder', params.sortOrder);
    if (params.scope) p.set('scope', params.scope);
    if (params.tenantId) p.set('tenantId', params.tenantId);
    if (params.systemSuiteId) p.set('systemSuiteId', params.systemSuiteId);
    if (params.moduleId) p.set('moduleId', params.moduleId);
  }
  return p.toString();
}

export const appConfigurationService = {
  getAll: async (params?: {
    page?: number;
    pageSize?: number;
    search?: string;
    criteria?: string;
    status?: string;
    sortBy?: string;
    sortOrder?: string;
    scope?: string;
    tenantId?: string;
    systemSuiteId?: string;
    moduleId?: string;
  }): Promise<AppConfigurationPage> => {
    const qs = buildQueryString(params);
    const { data } = await httpClient.get<{
      items: AppConfiguration[];
      totalItems: number;
      totalPages: number;
    }>(`/app-configurations?${qs}`);
    const result = AppConfigurationPageSchema.safeParse(data);
    if (!result.success) {
      logger.error('Invalid REST response shape for app configurations query', result.error);
      throw new Error('Invalid REST response shape for app configurations query');
    }
    return result.data;
  },

  getById: async (appConfigurationId: string): Promise<AppConfiguration> => {
    const { data } = await httpClient.get<{
      id: string;
      code: string;
      value: string;
      description: string;
      status: string;
      scope: string;
    }>(`/app-configurations/${appConfigurationId}`);
    return AppConfigurationSchema.parse(data);
  },

  createAppConfiguration: async (
    payload: CreateAppConfigurationPayload
  ): Promise<CreateAppConfigurationResponse> => {
    try {
      const { data } = await httpClient.post('/app-configurations', payload);
      logger.info('CreateAppConfiguration success', data);
      return CreateAppConfigurationResponseSchema.parse(data);
    } catch (error: any) {
      logger.error('CreateAppConfiguration failed', { payload, error: error.response?.data });
      throw error;
    }
  },

  updateAppConfiguration: async (
    appConfigurationId: string,
    payload: UpdateAppConfigurationPayload,
    rowVersion?: string
  ): Promise<void> => {
    const headers: Record<string, string> = {};
    if (rowVersion) {
      headers['If-Match'] = rowVersion;
    }
    await httpClient.put(`/app-configurations/${appConfigurationId}`, payload, { headers });
  },

  publishAppConfiguration: async (appConfigurationId: string): Promise<void> => {
    await httpClient.post(`/app-configurations/${appConfigurationId}/publish`);
  },

  archiveAppConfiguration: async (appConfigurationId: string): Promise<void> => {
    await httpClient.post(`/app-configurations/${appConfigurationId}/archive`);
  },

  deleteAppConfiguration: async (appConfigurationId: string): Promise<void> => {
    await httpClient.delete(`/app-configurations/${appConfigurationId}`);
  },
};

export default appConfigurationService;
