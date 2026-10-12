/**
 * idp.service.ts
 *
 * Infrastructure service for Identity Provider bounded context.
 * Queries use REST via httpClient.
 * Commands/transactions use REST via httpClient.
 * All responses are validated at runtime with Zod before returning.
 */
import { httpClient } from '@infra/http/httpClient';
import {
  IdentityProviderListSchema,
  type IdentityProvider,
  type RegisterIdentityProviderPayload,
} from '@domain/identity/schemas/identity-provider.schema';

export const idpService = {
  // ── Queries (REST) ────────────────────────────────────────────────────────

  getByIdentityProviders: async (tenantId: string): Promise<IdentityProvider[]> => {
    if (!tenantId || tenantId.trim() === '') {
      throw new Error('Invalid tenantId parameter');
    }
    const { data } = await httpClient.get<IdentityProvider[]>(
      `/tenants/${tenantId}/identity-providers`
    );
    return IdentityProviderListSchema.parse(data);
  },

  // ── Commands (REST) ───────────────────────────────────────────────────────

  registerIdentityProvider: async (
    tenantId: string,
    payload: RegisterIdentityProviderPayload
  ): Promise<{ identityProviderId: string }> => {
    const { data } = await httpClient.post<{ identityProviderId: string }>(
      `/tenants/${tenantId}/identity-providers`,
      payload
    );
    return data;
  },

  activateIdentityProvider: async (tenantId: string, idpId: string): Promise<void> => {
    await httpClient.post(`/tenants/${tenantId}/identity-providers/${idpId}/activate`);
  },

  deactivateIdentityProvider: async (tenantId: string, idpId: string): Promise<void> => {
    await httpClient.post(`/tenants/${tenantId}/identity-providers/${idpId}/deactivate`);
  },

  removeIdentityProvider: async (tenantId: string, idpId: string): Promise<void> => {
    await httpClient.delete(`/tenants/${tenantId}/identity-providers/${idpId}`);
  },
};
