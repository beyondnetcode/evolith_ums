import { describe, it, expect, vi, beforeEach } from 'vitest';
import { userAccountService } from './user-account.service';
import * as httpClientModule from '@infra/http/httpClient';

vi.mock('@infra/http/httpClient', () => ({
  httpClient: {
    get: vi.fn(),
    post: vi.fn(),
  },
}));

describe('userAccountService', () => {
  beforeEach(() => {
    vi.mocked(httpClientModule.httpClient.get).mockClear();
    vi.mocked(httpClientModule.httpClient.post).mockClear();
  });

  describe('getAll', () => {
    it('returns parsed user accounts page', async () => {
      vi.mocked(httpClientModule.httpClient.get).mockResolvedValue({
        data: {
          items: [],
          page: 1,
          pageSize: 20,
          totalItems: 0,
          totalPages: 0,
        },
      });

      const result = await userAccountService.getAll();

      expect(result.page).toBe(1);
      expect(result.items).toEqual([]);
    });

    it('uses default pagination', async () => {
      vi.mocked(httpClientModule.httpClient.get).mockResolvedValue({
        data: {
          items: [],
          page: 1,
          pageSize: 20,
          totalItems: 0,
          totalPages: 0,
        },
      });

      await userAccountService.getAll();

      expect(httpClientModule.httpClient.get).toHaveBeenCalledWith(
        '/user-accounts?page=1&pageSize=20'
      );
    });

    it('passes custom pagination', async () => {
      vi.mocked(httpClientModule.httpClient.get).mockResolvedValue({
        data: {
          items: [],
          page: 2,
          pageSize: 10,
          totalItems: 0,
          totalPages: 0,
        },
      });

      await userAccountService.getAll({ page: 2, pageSize: 10 });

      expect(httpClientModule.httpClient.get).toHaveBeenCalledWith(
        '/user-accounts?page=2&pageSize=10'
      );
    });
  });

  describe('getById', () => {
    it('returns parsed user account', async () => {
      vi.mocked(httpClientModule.httpClient.get).mockResolvedValue({
        data: {
          userAccountId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
          tenantId: '3fa85f64-5717-4562-b3fc-2c963f66afa7',
          branchId: null,
          email: 'test@example.com',
          category: 'Internal',
          status: 'Active',
          identityReference: 'EMP-123',
          identityReferenceType: 'HrId',
        },
      });

      const result = await userAccountService.getById('3fa85f64-5717-4562-b3fc-2c963f66afa6');

      expect(result.email).toBe('test@example.com');
    });
  });

  describe('createUserAccount', () => {
    it('calls httpClient.post and returns parsed response', async () => {
      vi.mocked(httpClientModule.httpClient.post).mockResolvedValue({
        data: { userAccountId: '3fa85f64-5717-4562-b3fc-2c963f66afa6' },
      });

      const result = await userAccountService.createUserAccount({
        tenantId: '3fa85f64-5717-4562-b3fc-2c963f66afa7',
        email: 'test@example.com',
        category: 'Internal',
        identityReference: 'EMP-123',
        identityReferenceType: 'HrId',
      });

      expect(result.userAccountId).toBe('3fa85f64-5717-4562-b3fc-2c963f66afa6');
    });
  });

  describe('activateUserAccount', () => {
    it('calls activate endpoint', async () => {
      vi.mocked(httpClientModule.httpClient.post).mockResolvedValue({});

      await userAccountService.activateUserAccount('3fa85f64-5717-4562-b3fc-2c963f66afa6');

      expect(httpClientModule.httpClient.post).toHaveBeenCalledWith(
        '/user-accounts/3fa85f64-5717-4562-b3fc-2c963f66afa6/activate'
      );
    });
  });

  describe('blockUserAccount', () => {
    it('calls block endpoint with reason', async () => {
      vi.mocked(httpClientModule.httpClient.post).mockResolvedValue({});

      await userAccountService.blockUserAccount(
        '3fa85f64-5717-4562-b3fc-2c963f66afa6',
        'Security violation'
      );

      expect(httpClientModule.httpClient.post).toHaveBeenCalledWith(
        '/user-accounts/3fa85f64-5717-4562-b3fc-2c963f66afa6/block',
        null,
        { params: { reason: 'Security violation' } }
      );
    });
  });

  describe('restoreUserAccount', () => {
    it('calls restore endpoint', async () => {
      vi.mocked(httpClientModule.httpClient.post).mockResolvedValue({});

      await userAccountService.restoreUserAccount('3fa85f64-5717-4562-b3fc-2c963f66afa6');

      expect(httpClientModule.httpClient.post).toHaveBeenCalledWith(
        '/user-accounts/3fa85f64-5717-4562-b3fc-2c963f66afa6/restore'
      );
    });
  });

  describe('setUserAccountPassword', () => {
    it('calls password endpoint', async () => {
      vi.mocked(httpClientModule.httpClient.post).mockResolvedValue({
        data: { credentialId: '3fa85f64-5717-4562-b3fc-2c963f66afa6' },
      });

      const result = await userAccountService.setUserAccountPassword({
        userAccountId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
        password: 'SecureP@ssw0rd123',
      });

      expect(httpClientModule.httpClient.post).toHaveBeenCalledWith(
        '/user-accounts/3fa85f64-5717-4562-b3fc-2c963f66afa6/passwords',
        expect.objectContaining({
          userAccountId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
          password: 'SecureP@ssw0rd123',
        })
      );
      expect(result.credentialId).toBe('3fa85f64-5717-4562-b3fc-2c963f66afa6');
    });
  });
});
