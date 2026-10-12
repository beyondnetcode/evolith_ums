import { describe, it, expect, vi, beforeEach } from 'vitest';
import { delegationService } from './delegation.service';
import * as httpClientModule from '@infra/http/httpClient';

vi.mock('@infra/http/httpClient', () => ({
  httpClient: {
    get: vi.fn(),
    post: vi.fn(),
  },
}));

describe('delegationService', () => {
  beforeEach(() => {
    vi.mocked(httpClientModule.httpClient.get).mockClear();
    vi.mocked(httpClientModule.httpClient.post).mockClear();
  });

  describe('getDelegationById', () => {
    it('throws when delegationId is empty', async () => {
      await expect(delegationService.getDelegationById('')).rejects.toThrow(
        'delegationId must be a non-empty string'
      );
    });

    it('returns parsed delegation when found', async () => {
      vi.mocked(httpClientModule.httpClient.get).mockResolvedValue({
        data: {
          delegationId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
          tenantId: '3fa85f64-5717-4562-b3fc-2c963f66afa7',
          delegatingAdminId: '3fa85f64-5717-4562-b3fc-2c963f66afa8',
          delegatedAdminId: '3fa85f64-5717-4562-b3fc-2c963f66afa9',
          scopeType: 'Tenant',
          scopeId: null,
          allowedActions: ['CreateUser'],
          validFrom: '2024-01-01T00:00:00Z',
          validUntil: '2024-12-31T23:59:59Z',
          maxDurationDays: 30,
          requiresApproval: false,
          approvalRequestId: null,
          status: 'Active',
          revokedAt: null,
          revokedBy: null,
          revocationReason: null,
        },
      });

      const result = await delegationService.getDelegationById(
        '3fa85f64-5717-4562-b3fc-2c963f66afa6'
      );

      expect(httpClientModule.httpClient.get).toHaveBeenCalledWith(
        '/delegations/3fa85f64-5717-4562-b3fc-2c963f66afa6'
      );
      expect(result.delegationId).toBe('3fa85f64-5717-4562-b3fc-2c963f66afa6');
      expect(result.status).toBe('Active');
    });
  });

  describe('getDelegationsByDelegatedAdmin', () => {
    it('returns parsed delegations', async () => {
      vi.mocked(httpClientModule.httpClient.get).mockResolvedValue({
        data: [],
      });

      const result = await delegationService.getDelegationsByDelegatedAdmin(
        '3fa85f64-5717-4562-b3fc-2c963f66afa6',
        '3fa85f64-5717-4562-b3fc-2c963f66afa7'
      );

      expect(httpClientModule.httpClient.get).toHaveBeenCalledWith(
        '/delegations/by-delegated-admin/3fa85f64-5717-4562-b3fc-2c963f66afa6?tenantId=3fa85f64-5717-4562-b3fc-2c963f66afa7'
      );
      expect(result).toEqual([]);
    });
  });

  describe('getDelegationsByDelegatingAdmin', () => {
    it('returns parsed delegations', async () => {
      vi.mocked(httpClientModule.httpClient.get).mockResolvedValue({
        data: [],
      });

      const result = await delegationService.getDelegationsByDelegatingAdmin(
        '3fa85f64-5717-4562-b3fc-2c963f66afa6',
        '3fa85f64-5717-4562-b3fc-2c963f66afa7'
      );

      expect(httpClientModule.httpClient.get).toHaveBeenCalledWith(
        '/delegations/by-delegating-admin/3fa85f64-5717-4562-b3fc-2c963f66afa6?tenantId=3fa85f64-5717-4562-b3fc-2c963f66afa7'
      );
      expect(result).toEqual([]);
    });
  });

  describe('createDelegation', () => {
    it('calls httpClient.post and returns parsed response', async () => {
      vi.mocked(httpClientModule.httpClient.post).mockResolvedValue({
        data: { delegationId: '3fa85f64-5717-4562-b3fc-2c963f66afa6' },
      });

      const result = await delegationService.createDelegation({
        tenantId: '3fa85f64-5717-4562-b3fc-2c963f66afa7',
        delegatingAdminId: '3fa85f64-5717-4562-b3fc-2c963f66afa8',
        delegatedAdminId: '3fa85f64-5717-4562-b3fc-2c963f66afa9',
        scopeType: 'Tenant',
        allowedActions: ['CreateUser'],
        validFrom: '2024-01-01',
        validUntil: '2024-12-31',
        requiresApproval: false,
      });

      expect(result.delegationId).toBe('3fa85f64-5717-4562-b3fc-2c963f66afa6');
    });
  });

  describe('activateDelegation', () => {
    it('calls activate endpoint', async () => {
      vi.mocked(httpClientModule.httpClient.post).mockResolvedValue({});

      await delegationService.activateDelegation('3fa85f64-5717-4562-b3fc-2c963f66afa6');

      expect(httpClientModule.httpClient.post).toHaveBeenCalledWith(
        '/delegations/3fa85f64-5717-4562-b3fc-2c963f66afa6/activate'
      );
    });
  });

  describe('revokeDelegation', () => {
    it('calls revoke endpoint with reason', async () => {
      vi.mocked(httpClientModule.httpClient.post).mockResolvedValue({});

      await delegationService.revokeDelegation(
        '3fa85f64-5717-4562-b3fc-2c963f66afa6',
        'No longer needed'
      );

      expect(httpClientModule.httpClient.post).toHaveBeenCalledWith(
        '/delegations/3fa85f64-5717-4562-b3fc-2c963f66afa6/revoke',
        null,
        { params: { reason: 'No longer needed' } }
      );
    });
  });

  describe('expireDelegation', () => {
    it('calls expire endpoint', async () => {
      vi.mocked(httpClientModule.httpClient.post).mockResolvedValue({});

      await delegationService.expireDelegation('3fa85f64-5717-4562-b3fc-2c963f66afa6');

      expect(httpClientModule.httpClient.post).toHaveBeenCalledWith(
        '/delegations/3fa85f64-5717-4562-b3fc-2c963f66afa6/expire'
      );
    });
  });
});
