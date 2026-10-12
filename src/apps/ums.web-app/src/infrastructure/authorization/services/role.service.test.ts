import { describe, it, expect, vi, beforeEach } from 'vitest';
import { roleService } from './role.service';
import * as httpClientModule from '@infra/http/httpClient';

vi.mock('@infra/http/httpClient', () => ({
  httpClient: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
  },
}));

describe('roleService', () => {
  beforeEach(() => {
    vi.mocked(httpClientModule.httpClient.get).mockClear();
    vi.mocked(httpClientModule.httpClient.post).mockClear();
    vi.mocked(httpClientModule.httpClient.put).mockClear();
  });

  describe('getBySystemSuite', () => {
    it('calls httpClient.get and parses response', async () => {
      const mockRoles = [
        {
          roleId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
          tenantId: '3fa85f64-5717-4562-b3fc-2c963f66afa7',
          systemSuiteId: '3fa85f64-5717-4562-b3fc-2c963f66afa8',
          parentRoleId: null,
          code: 'ADMIN',
          value: 'Administrator',
          description: 'Admin role',
          hierarchyLevel: 0,
          promotionOrder: 1,
          isActive: true,
        },
      ];
      vi.mocked(httpClientModule.httpClient.get).mockResolvedValue({
        data: mockRoles,
      });

      const result = await roleService.getBySystemSuite('3fa85f64-5717-4562-b3fc-2c963f66afa8');

      expect(httpClientModule.httpClient.get).toHaveBeenCalledWith(
        '/system-suites/3fa85f64-5717-4562-b3fc-2c963f66afa8/roles'
      );
      expect(result).toHaveLength(1);
      expect(result[0].code).toBe('ADMIN');
    });
  });

  describe('create', () => {
    it('calls httpClient.post with correct payload', async () => {
      vi.mocked(httpClientModule.httpClient.post).mockResolvedValue({
        data: { roleId: '3fa85f64-5717-4562-b3fc-2c963f66afa6' },
      });

      await roleService.create('3fa85f64-5717-4562-b3fc-2c963f66afa8', {
        code: 'NEW_ROLE',
        value: 'New Role',
        description: 'A new role',
        hierarchyLevel: 0,
        promotionOrder: 1,
      });

      expect(httpClientModule.httpClient.post).toHaveBeenCalledWith(
        '/system-suites/3fa85f64-5717-4562-b3fc-2c963f66afa8/roles',
        expect.objectContaining({
          systemSuiteId: '3fa85f64-5717-4562-b3fc-2c963f66afa8',
          code: 'NEW_ROLE',
        })
      );
    });

    it('returns parsed response', async () => {
      vi.mocked(httpClientModule.httpClient.post).mockResolvedValue({
        data: { roleId: '3fa85f64-5717-4562-b3fc-2c963f66afa6' },
      });

      const result = await roleService.create('3fa85f64-5717-4562-b3fc-2c963f66afa8', {
        code: 'NEW_ROLE',
        value: 'New Role',
        description: 'A new role',
        hierarchyLevel: 0,
        promotionOrder: 1,
      });

      expect(result.roleId).toBe('3fa85f64-5717-4562-b3fc-2c963f66afa6');
    });
  });

  describe('update', () => {
    it('calls httpClient.put with correct payload', async () => {
      vi.mocked(httpClientModule.httpClient.put).mockResolvedValue({});

      await roleService.update(
        '3fa85f64-5717-4562-b3fc-2c963f66afa8',
        '3fa85f64-5717-4562-b3fc-2c963f66afa9',
        {
          code: 'UPDATED_ROLE',
          value: 'Updated Role',
          description: 'Updated description',
          hierarchyLevel: 0,
          promotionOrder: 1,
        }
      );

      expect(httpClientModule.httpClient.put).toHaveBeenCalledWith(
        '/system-suites/3fa85f64-5717-4562-b3fc-2c963f66afa8/roles/3fa85f64-5717-4562-b3fc-2c963f66afa9',
        expect.objectContaining({
          roleId: '3fa85f64-5717-4562-b3fc-2c963f66afa9',
          code: 'UPDATED_ROLE',
        })
      );
    });
  });

  describe('setActive', () => {
    it('calls activate endpoint when isActive is true', async () => {
      vi.mocked(httpClientModule.httpClient.post).mockResolvedValue({});

      await roleService.setActive(
        '3fa85f64-5717-4562-b3fc-2c963f66afa8',
        '3fa85f64-5717-4562-b3fc-2c963f66afa9',
        true
      );

      expect(httpClientModule.httpClient.post).toHaveBeenCalledWith(
        '/system-suites/3fa85f64-5717-4562-b3fc-2c963f66afa8/roles/3fa85f64-5717-4562-b3fc-2c963f66afa9/activate'
      );
    });

    it('calls deactivate endpoint when isActive is false', async () => {
      vi.mocked(httpClientModule.httpClient.post).mockResolvedValue({});

      await roleService.setActive(
        '3fa85f64-5717-4562-b3fc-2c963f66afa8',
        '3fa85f64-5717-4562-b3fc-2c963f66afa9',
        false
      );

      expect(httpClientModule.httpClient.post).toHaveBeenCalledWith(
        '/system-suites/3fa85f64-5717-4562-b3fc-2c963f66afa8/roles/3fa85f64-5717-4562-b3fc-2c963f66afa9/deactivate'
      );
    });
  });
});
