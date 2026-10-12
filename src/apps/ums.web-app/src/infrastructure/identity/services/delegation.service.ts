/**
 * delegation.service.ts
 *
 * Infrastructure service for the Identity / UserManagementDelegation bounded context.
 * Queries use REST via httpClient.
 * Commands/transactions use REST via httpClient.
 * All responses are validated at runtime with Zod before returning.
 */
import { httpClient } from '@infra/http/httpClient';
import { logger } from '@app/utils/logger';
import {
  DelegationSchema,
  DelegationListSchema,
  CreateDelegationResponseSchema,
  type Delegation,
  type CreateDelegationPayload,
  type CreateDelegationResponse,
} from '@domain/identity/schemas/delegation.schema';

export const delegationService = {
  // ── Queries (REST) ────────────────────────────────────────────────────────

  getDelegationById: async (delegationId: string): Promise<Delegation> => {
    if (!delegationId || delegationId.trim() === '') {
      throw new Error('delegationId must be a non-empty string');
    }
    const { data } = await httpClient.get<Delegation>(`/delegations/${delegationId}`);
    return DelegationSchema.parse(data);
  },

  getDelegationsByDelegatedAdmin: async (
    delegatedAdminId: string,
    tenantId: string
  ): Promise<Delegation[]> => {
    if (!delegatedAdminId || !tenantId) {
      throw new Error('delegatedAdminId and tenantId are required');
    }
    const { data } = await httpClient.get<Delegation[]>(
      `/delegations/by-delegated-admin/${delegatedAdminId}?tenantId=${tenantId}`
    );
    const result = DelegationListSchema.safeParse(data);
    if (!result.success) {
      logger.error(
        'Invalid REST response shape for delegationsByDelegatedAdmin query',
        result.error
      );
      throw new Error('Invalid REST response shape for delegationsByDelegatedAdmin query');
    }
    return result.data;
  },

  getDelegationsByDelegatingAdmin: async (
    delegatingAdminId: string,
    tenantId: string
  ): Promise<Delegation[]> => {
    if (!delegatingAdminId || !tenantId) {
      throw new Error('delegatingAdminId and tenantId are required');
    }
    const { data } = await httpClient.get<Delegation[]>(
      `/delegations/by-delegating-admin/${delegatingAdminId}?tenantId=${tenantId}`
    );
    const result = DelegationListSchema.safeParse(data);
    if (!result.success) {
      logger.error(
        'Invalid REST response shape for delegationsByDelegatingAdmin query',
        result.error
      );
      throw new Error('Invalid REST response shape for delegationsByDelegatingAdmin query');
    }
    return result.data;
  },

  // ── Commands (REST) ───────────────────────────────────────────────────────

  createDelegation: async (payload: CreateDelegationPayload): Promise<CreateDelegationResponse> => {
    const { data } = await httpClient.post('/delegations', payload);
    return CreateDelegationResponseSchema.parse(data);
  },

  activateDelegation: async (delegationId: string): Promise<void> => {
    await httpClient.post(`/delegations/${delegationId}/activate`);
  },

  revokeDelegation: async (delegationId: string, reason: string): Promise<void> => {
    await httpClient.post(`/delegations/${delegationId}/revoke`, null, { params: { reason } });
  },

  submitDelegationForApproval: async (delegationId: string): Promise<void> => {
    await httpClient.post(`/delegations/${delegationId}/submit-for-approval`);
  },

  approveDelegation: async (delegationId: string): Promise<void> => {
    await httpClient.post(`/delegations/${delegationId}/approve`);
  },

  rejectDelegation: async (delegationId: string, reason: string): Promise<void> => {
    await httpClient.post(`/delegations/${delegationId}/reject`, null, { params: { reason } });
  },

  expireDelegation: async (delegationId: string): Promise<void> => {
    await httpClient.post(`/delegations/${delegationId}/expire`);
  },
};

export default delegationService;
