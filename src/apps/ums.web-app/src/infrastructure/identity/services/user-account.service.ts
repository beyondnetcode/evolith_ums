/**
 * user-account.service.ts
 *
 * Infrastructure service for the Identity / UserAccount bounded context.
 * Queries and commands currently use REST via httpClient.
 * All responses are validated at runtime with Zod before returning.
 */
import { httpClient } from '@infra/http/httpClient';
import { logger } from '@app/utils/logger';
import {
  UserAccountPageSchema,
  UserAccountSchema,
  CreateUserAccountResponseSchema,
  type UserAccount,
  type UserAccountPage,
  type CreateUserAccountPayload,
  type CreateUserAccountResponse,
  SetUserAccountPasswordPayloadSchema,
  SetUserAccountPasswordResponseSchema,
  type SetUserAccountPasswordPayload,
  type SetUserAccountPasswordResponse,
} from '@domain/identity/schemas/user-account.schema';

export interface UserAccountQueryParams {
  page?: number;
  pageSize?: number;
  search?: string;
  criteria?: string;
  status?: string;
  sortBy?: string;
  sortOrder?: string;
  tenantId?: string;
}

function buildUserAccountQueryString(
  params: Required<Pick<UserAccountQueryParams, 'page' | 'pageSize'>> & UserAccountQueryParams
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
  return searchParams.toString();
}

export const userAccountService = {
  // ── Queries (REST) ────────────────────────────────────────────────────────

  getAll: async (params?: UserAccountQueryParams): Promise<UserAccountPage> => {
    const normalizedParams = {
      page: params?.page ?? 1,
      pageSize: params?.pageSize ?? 20,
      search: params?.search,
      criteria: params?.criteria,
      status: params?.status,
      sortBy: params?.sortBy,
      sortOrder: params?.sortOrder,
      tenantId: params?.tenantId,
    };

    const { data } = await httpClient.get<UserAccountPage>(
      `/user-accounts?${buildUserAccountQueryString(normalizedParams)}`
    );

    const pageResult = UserAccountPageSchema.safeParse(data);
    if (!pageResult.success) {
      logger.error('Invalid REST response shape for userAccounts query', pageResult.error);
      throw new Error('Invalid REST response shape for userAccounts query');
    }
    return pageResult.data;
  },

  getById: async (userAccountId: string): Promise<UserAccount> => {
    const { data } = await httpClient.get<UserAccount>(`/user-accounts/${userAccountId}`);
    return UserAccountSchema.parse(data);
  },

  // ── Commands (REST) ───────────────────────────────────────────────────────

  createUserAccount: async (
    payload: CreateUserAccountPayload
  ): Promise<CreateUserAccountResponse> => {
    const { data } = await httpClient.post('/user-accounts', payload);
    return CreateUserAccountResponseSchema.parse(data);
  },

  activateUserAccount: async (userAccountId: string): Promise<void> => {
    await httpClient.post(`/user-accounts/${userAccountId}/activate`);
  },

  blockUserAccount: async (userAccountId: string, reason: string): Promise<void> => {
    await httpClient.post(`/user-accounts/${userAccountId}/block`, null, { params: { reason } });
  },

  restoreUserAccount: async (userAccountId: string): Promise<void> => {
    await httpClient.post(`/user-accounts/${userAccountId}/restore`);
  },

  setUserAccountPassword: async (
    payload: SetUserAccountPasswordPayload
  ): Promise<SetUserAccountPasswordResponse> => {
    const validPayload = SetUserAccountPasswordPayloadSchema.parse(payload);
    const { data } = await httpClient.post(
      `/user-accounts/${validPayload.userAccountId}/passwords`,
      validPayload
    );
    return SetUserAccountPasswordResponseSchema.parse(data);
  },

  forcePasswordReset: async (userAccountId: string): Promise<{ temporaryPassword: string }> => {
    const { data } = await httpClient.post(`/user-accounts/${userAccountId}/passwords/force-reset`);
    return data as { temporaryPassword: string };
  },
};

export default userAccountService;
