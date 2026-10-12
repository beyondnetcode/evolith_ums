/**
 * feature-flag.service.ts
 *
 * Infrastructure service for the Configuration / FeatureFlag bounded context.
 * All queries and commands use REST via httpClient.
 * All responses are validated with Zod before returning.
 */
import { httpClient } from '@infra/http/httpClient';
import { logger } from '@app/utils/logger';
import {
  FeatureFlagSchema,
  FeatureFlagPageSchema,
  FeatureFlagListSchema,
  CreateFeatureFlagResponseSchema,
  AddFeatureFlagCriteriaResponseSchema,
  type FeatureFlag,
  type FeatureFlagPage,
  type CreateFeatureFlagResponse,
  type AddFeatureFlagCriteriaResponse,
} from '@domain/configuration/schemas/feature-flag.schema';
import type {
  CreateFeatureFlagPayload,
  UpdateFeatureFlagPayload,
  AddFeatureFlagCriteriaPayload,
} from '@domain/configuration/schemas/feature-flag.commands.schema';

function buildQueryString(params?: {
  page?: number;
  pageSize?: number;
  search?: string;
  criteria?: string;
  status?: string;
  sortBy?: string;
  sortOrder?: string;
  flagType?: string;
}): string {
  const p = new URLSearchParams();
  p.set('page', String(params?.page ?? 1));
  p.set('pageSize', String(params?.pageSize ?? 20));
  if (params?.search) p.set('search', params.search);
  if (params?.criteria) p.set('criteria', params.criteria);
  if (params?.status) p.set('status', params.status);
  if (params?.sortBy) p.set('sortBy', params.sortBy);
  if (params?.sortOrder) p.set('sortOrder', params.sortOrder);
  if (params?.flagType) p.set('flagType', params.flagType);
  return p.toString();
}

export const featureFlagService = {
  getAll: async (params?: {
    page?: number;
    pageSize?: number;
    search?: string;
    criteria?: string;
    status?: string;
    sortBy?: string;
    sortOrder?: string;
    flagType?: string;
  }): Promise<FeatureFlagPage> => {
    const qs = buildQueryString(params);
    const { data } = await httpClient.get<{
      items: FeatureFlag[];
      totalItems: number;
      totalPages: number;
    }>(`/feature-flags?${qs}`);
    const result = FeatureFlagPageSchema.safeParse(data);
    if (!result.success) {
      logger.error('Invalid REST response shape for feature flags query', result.error);
      throw new Error('Invalid REST response shape for feature flags query');
    }
    return result.data;
  },

  // Backward-compatible aliases used by hooks/tests during the REST migration.
  getAllFeatureFlags: async (params?: {
    page?: number;
    pageSize?: number;
    search?: string;
    criteria?: string;
    status?: string;
    sortBy?: string;
    sortOrder?: string;
    flagType?: string;
  }): Promise<FeatureFlagPage> => featureFlagService.getAll(params),

  getById: async (featureFlagId: string): Promise<FeatureFlag> => {
    const { data } = await httpClient.get<FeatureFlag>(`/feature-flags/${featureFlagId}`);
    if (!data) throw new Error('FeatureFlag not found');
    return FeatureFlagSchema.parse(data);
  },

  getFeatureFlagById: async (featureFlagId: string): Promise<FeatureFlag> =>
    featureFlagService.getById(featureFlagId),

  /** REST: returns all flags scoped to a given SystemSuite (no pagination). */
  getFeatureFlagsBySystemSuite: async (systemSuiteId: string): Promise<FeatureFlag[]> => {
    const { data } = await httpClient.get(`/system-suites/${systemSuiteId}/feature-flags`);
    const result = FeatureFlagListSchema.safeParse(data);
    if (!result.success) {
      logger.error('Invalid REST response shape for feature flags by system suite', result.error);
      throw new Error('Invalid REST response for feature flags by system suite');
    }
    return result.data;
  },

  createFeatureFlag: async (
    payload: CreateFeatureFlagPayload
  ): Promise<CreateFeatureFlagResponse> => {
    const { data } = await httpClient.post('/feature-flags', payload);
    return CreateFeatureFlagResponseSchema.parse(data);
  },

  updateFeatureFlag: async (
    featureFlagId: string,
    payload: UpdateFeatureFlagPayload
  ): Promise<void> => {
    await httpClient.put(`/feature-flags/${featureFlagId}`, payload);
  },

  activateFlag: async (featureFlagId: string): Promise<void> => {
    await httpClient.post(`/feature-flags/${featureFlagId}/activate`);
  },

  deactivateFlag: async (featureFlagId: string): Promise<void> => {
    await httpClient.post(`/feature-flags/${featureFlagId}/deactivate`);
  },

  archiveFlag: async (featureFlagId: string): Promise<void> => {
    await httpClient.post(`/feature-flags/${featureFlagId}/archive`);
  },

  addCriteria: async (
    featureFlagId: string,
    payload: AddFeatureFlagCriteriaPayload
  ): Promise<AddFeatureFlagCriteriaResponse> => {
    const { data } = await httpClient.post(`/feature-flags/${featureFlagId}/criteria`, payload);
    return AddFeatureFlagCriteriaResponseSchema.parse(data);
  },

  removeCriteria: async (featureFlagId: string, criteriaId: string): Promise<void> => {
    await httpClient.delete(`/feature-flags/${featureFlagId}/criteria/${criteriaId}`);
  },
};

export default featureFlagService;
