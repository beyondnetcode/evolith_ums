import { httpClient } from '@infra/http/httpClient';
import { logger } from '@app/utils/logger';
import {
  ProfilePageSchema,
  ProfileSchema,
  CreateProfileResponseSchema,
  type Profile,
  type ProfilePage,
  type CreateProfilePayload,
  type CreateProfileResponse,
} from '@domain/authorization/schemas/profile.schema';

type ProfileListParams = {
  page?: number;
  pageSize?: number;
  search?: string;
  criteria?: string;
  status?: string;
  sortBy?: string;
  sortOrder?: string;
  tenantId?: string;
  userId?: string;
};

function buildProfileQueryString(
  params: Required<Pick<ProfileListParams, 'page' | 'pageSize'>> & ProfileListParams
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
  if (params.userId !== undefined) searchParams.set('userId', params.userId);
  return searchParams.toString();
}

/** Response from GET /profiles/{id}/auth-graph/preview */
export interface PreviewAuthGraphResponse {
  format: string;
  graph: string;
  requestId: string;
  previewMode: string;
  profileId: string;
  userId: string;
  tenantId: string;
  tenantCode: string;
  authMethodUsed: string;
}

export const profileService = {
  // ── Queries (REST) ───────────────────────────────────────────────────────────

  getAll: async (params?: ProfileListParams): Promise<ProfilePage> => {
    const queryString = buildProfileQueryString({
      page: params?.page ?? 1,
      pageSize: params?.pageSize ?? 20,
      search: params?.search,
      criteria: params?.criteria,
      status: params?.status,
      sortBy: params?.sortBy,
      sortOrder: params?.sortOrder,
      tenantId: params?.tenantId,
      userId: params?.userId,
    });

    const { data } = await httpClient.get<ProfilePage>(`/profiles?${queryString}`);
    const result = ProfilePageSchema.safeParse(data);
    if (!result.success) {
      logger.error('Invalid REST response for profiles', result.error);
      throw new Error('Invalid response shape for profiles');
    }
    return result.data;
  },

  getById: async (profileId: string): Promise<Profile> => {
    const { data } = await httpClient.get<Profile>(`/profiles/${profileId}`);
    if (!data) throw new Error('Profile not found');
    return ProfileSchema.parse(data);
  },

  // ── Commands (REST) ──────────────────────────────────────────────────────────

  create: async (payload: CreateProfilePayload): Promise<CreateProfileResponse> => {
    const { data } = await httpClient.post('/profiles', payload);
    return CreateProfileResponseSchema.parse(data);
  },

  assignTemplate: async (profileId: string, templateId: string): Promise<void> => {
    await httpClient.post(`/profiles/${profileId}/templates/${templateId}`);
  },

  overridePermission: async (
    profileId: string,
    permissionId: string,
    effect: 'allow' | 'deny' | 'neutral'
  ): Promise<void> => {
    await httpClient.post(
      `/profiles/${profileId}/permissions/${permissionId}/override?effect=${effect}`
    );
  },

  activatePermission: async (profileId: string, permissionId: string): Promise<void> => {
    await httpClient.post(`/profiles/${profileId}/permissions/${permissionId}/activate`);
  },

  deactivatePermission: async (profileId: string, permissionId: string): Promise<void> => {
    await httpClient.post(`/profiles/${profileId}/permissions/${permissionId}/deactivate`);
  },

  activate: async (profileId: string): Promise<void> => {
    await httpClient.post(`/profiles/${profileId}/activate`);
  },

  deactivate: async (profileId: string): Promise<void> => {
    await httpClient.post(`/profiles/${profileId}/deactivate`);
  },

  exportGraph: async (
    profileId: string,
    format: 'json' | 'xml' | 'yaml' | 'csv'
  ): Promise<string> => {
    const { data } = await httpClient.get(`/profiles/${profileId}/export`, {
      params: { format },
      responseType: 'text',
    });
    return data as string;
  },

  previewGraph: async (profileId: string): Promise<string> => {
    const { data } = await httpClient.get(`/profiles/${profileId}/permission-graph`, {
      responseType: 'text',
    });
    return data as string;
  },

  /**
   * Calls the same auth-graph pipeline as POST /client/authenticate.
   * Returns the exact graph a client system would receive, without credential validation.
   * Requires an authenticated UMS admin session.
   */
  previewAuthGraph: async (
    profileId: string,
    format?: string
  ): Promise<PreviewAuthGraphResponse> => {
    const { data } = await httpClient.get<PreviewAuthGraphResponse>(
      `/profiles/${profileId}/auth-graph/preview`,
      { params: format ? { format } : undefined }
    );
    return data;
  },
};

export default profileService;
