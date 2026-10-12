import { describe, it, expect, vi, beforeEach } from 'vitest';
import { tenantService } from './tenant.service';
import * as httpClientModule from '@infra/http/httpClient';
import * as loggerModule from '@app/utils/logger';
import { useAuthStore } from '@app/stores/auth.store';

vi.mock('@infra/http/httpClient', () => ({
  httpClient: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
  },
}));

vi.mock('@app/utils/logger', () => ({
  logger: {
    error: vi.fn(),
    warn: vi.fn(),
  },
}));

vi.mock('@app/stores/auth.store', () => ({
  useAuthStore: {
    getState: vi.fn(),
  },
}));

describe('tenantService', () => {
  beforeEach(() => {
    vi.mocked(httpClientModule.httpClient.get).mockClear();
    vi.mocked(httpClientModule.httpClient.post).mockClear();
    vi.mocked(httpClientModule.httpClient.put).mockClear();
    vi.mocked(httpClientModule.httpClient.delete).mockClear();
    vi.mocked(loggerModule.logger.error).mockClear();

    vi.mocked(useAuthStore.getState).mockReturnValue({
      isAuthenticated: true,
      user: { isInternalAdmin: true },
    } as any);
  });

  describe('getAll', () => {
    it('calls REST with default params', async () => {
      vi.mocked(httpClientModule.httpClient.get).mockResolvedValue({
        data: { items: [], page: 1, pageSize: 20, totalItems: 0, totalPages: 0 },
      });

      const result = await tenantService.getAll();

      expect(result.items).toHaveLength(0);
      expect(httpClientModule.httpClient.get).toHaveBeenCalledWith('/tenants?page=1&pageSize=20');
    });

    it('passes custom params', async () => {
      vi.mocked(httpClientModule.httpClient.get).mockResolvedValue({
        data: {
          items: [
            {
              tenantId: '12345678-1234-1234-1234-123456789012',
              code: 'T1',
              name: 'Tenant 1',
              type: 'Enterprise',
              status: 'Active',
              parentTenantId: null,
            },
          ],
          page: 2,
          pageSize: 10,
          totalItems: 1,
          totalPages: 1,
        },
      });

      const result = await tenantService.getAll({ page: 2, pageSize: 10, search: 'test' });

      expect(result.items).toHaveLength(1);
      expect(result.page).toBe(2);
      expect(httpClientModule.httpClient.get).toHaveBeenCalledWith(
        '/tenants?page=2&pageSize=10&search=test'
      );
    });

    it('throws on invalid response shape', async () => {
      vi.mocked(httpClientModule.httpClient.get).mockResolvedValue({
        data: { invalid: 'shape' },
      });

      await expect(tenantService.getAll()).rejects.toThrow('Invalid REST response shape');
      expect(loggerModule.logger.error).toHaveBeenCalled();
    });
  });

  describe('getById', () => {
    it('returns parsed tenant', async () => {
      vi.mocked(httpClientModule.httpClient.get).mockResolvedValue({
        data: {
          tenantId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
          code: 'T1',
          name: 'Tenant 1',
          type: 'Enterprise',
          status: 'Active',
          parentTenantId: null,
        },
      });

      const result = await tenantService.getById('3fa85f64-5717-4562-b3fc-2c963f66afa6');

      expect(result.tenantId).toBe('3fa85f64-5717-4562-b3fc-2c963f66afa6');
      expect(httpClientModule.httpClient.get).toHaveBeenCalledWith(
        '/tenants/3fa85f64-5717-4562-b3fc-2c963f66afa6'
      );
    });

    it('throws when the response cannot be parsed', async () => {
      vi.mocked(httpClientModule.httpClient.get).mockResolvedValue({ data: null });

      await expect(tenantService.getById('nonexistent')).rejects.toThrow();
    });
  });

  describe('getBranches', () => {
    it('returns parsed branches', async () => {
      vi.mocked(httpClientModule.httpClient.get).mockResolvedValue({
        data: [
          {
            branchId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
            code: 'B1',
            name: 'Branch 1',
            isActive: true,
          },
        ],
      });

      const result = await tenantService.getBranches('3fa85f64-5717-4562-b3fc-2c963f66afa7');

      expect(result).toHaveLength(1);
      expect(result[0].branchId).toBe('3fa85f64-5717-4562-b3fc-2c963f66afa6');
      expect(httpClientModule.httpClient.get).toHaveBeenCalledWith(
        '/tenants/3fa85f64-5717-4562-b3fc-2c963f66afa7/branches'
      );
    });

    // ADR-0164: por defecto el backend ya no devuelve las cerradas; hay que pedirlas.
    it('pide las cerradas solo cuando se solicitan', async () => {
      vi.mocked(httpClientModule.httpClient.get).mockResolvedValue({ data: [] });

      await tenantService.getBranches('3fa85f64-5717-4562-b3fc-2c963f66afa7', true);

      expect(httpClientModule.httpClient.get).toHaveBeenCalledWith(
        '/tenants/3fa85f64-5717-4562-b3fc-2c963f66afa7/branches?includeClosed=true'
      );
    });

    it('normaliza a false la sucursal que llega sin isClosed', async () => {
      vi.mocked(httpClientModule.httpClient.get).mockResolvedValue({
        data: [
          {
            branchId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
            code: 'B1',
            name: 'Branch 1',
            isActive: true,
          },
        ],
      });

      const result = await tenantService.getBranches('3fa85f64-5717-4562-b3fc-2c963f66afa7');

      expect(result[0].isClosed).toBe(false);
      expect(result[0].closedAtUtc).toBeNull();
    });
  });

  describe('getBranchLifecycle', () => {
    it('llama al endpoint de bitácora y valida los asientos', async () => {
      vi.mocked(httpClientModule.httpClient.get).mockResolvedValue({
        data: [
          {
            entryId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
            episode: 'Closed',
            occurredAtUtc: '2026-07-31T15:04:05Z',
            actorId: 'admin@evolith.example',
            nameSnapshot: 'Sucursal Callao',
            geofencingSnapshot: null,
            reason: 'Traslado de operaciones al terminal norte',
          },
        ],
      });

      const result = await tenantService.getBranchLifecycle('t1', 'b1');

      expect(httpClientModule.httpClient.get).toHaveBeenCalledWith(
        '/tenants/t1/branches/b1/bitacora'
      );
      expect(result[0].episode).toBe('Closed');
      expect(result[0].reason).toBe('Traslado de operaciones al terminal norte');
    });
  });

  describe('createTenant', () => {
    it('calls POST and returns parsed response', async () => {
      vi.mocked(httpClientModule.httpClient.post).mockResolvedValue({
        data: { tenantId: '3fa85f64-5717-4562-b3fc-2c963f66afa6', code: 'NEW', name: 'New Tenant' },
      });

      const result = await tenantService.createTenant({
        code: 'NEW',
        name: 'New Tenant',
        type: 'Enterprise',
      });

      expect(result.tenantId).toBe('3fa85f64-5717-4562-b3fc-2c963f66afa6');
      expect(httpClientModule.httpClient.post).toHaveBeenCalledWith('/tenants', expect.any(Object));
    });
  });

  describe('activateTenant', () => {
    it('calls activate endpoint', async () => {
      vi.mocked(httpClientModule.httpClient.post).mockResolvedValue({});

      await tenantService.activateTenant('t1');

      expect(httpClientModule.httpClient.post).toHaveBeenCalledWith('/tenants/t1/activate');
    });
  });

  describe('suspendTenant', () => {
    it('calls suspend endpoint', async () => {
      vi.mocked(httpClientModule.httpClient.post).mockResolvedValue({});

      await tenantService.suspendTenant('t1');

      expect(httpClientModule.httpClient.post).toHaveBeenCalledWith('/tenants/t1/suspend');
    });
  });

  describe('addBranch', () => {
    it('calls POST branches endpoint', async () => {
      vi.mocked(httpClientModule.httpClient.post).mockResolvedValue({
        data: {
          branchId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
          tenantId: '3fa85f64-5717-4562-b3fc-2c963f66afa7',
          code: 'B1',
        },
      });

      const result = await tenantService.addBranch('3fa85f64-5717-4562-b3fc-2c963f66afa7', {
        code: 'B1',
        name: 'Branch 1',
      });

      expect(result.branchId).toBe('3fa85f64-5717-4562-b3fc-2c963f66afa6');
      expect(httpClientModule.httpClient.post).toHaveBeenCalledWith(
        '/tenants/3fa85f64-5717-4562-b3fc-2c963f66afa7/branches',
        expect.any(Object)
      );
    });
  });

  // ADR-0164: el DELETE conserva ruta y contrato, pero ya no borra: CIERRA definitivamente.
  describe('closeBranch', () => {
    it('calls DELETE branch endpoint', async () => {
      vi.mocked(httpClientModule.httpClient.delete).mockResolvedValue({});

      await tenantService.closeBranch('t1', 'b1');

      expect(httpClientModule.httpClient.delete).toHaveBeenCalledWith('/tenants/t1/branches/b1');
    });

    it('adjunta el motivo del cierre cuando se indica', async () => {
      vi.mocked(httpClientModule.httpClient.delete).mockResolvedValue({});

      await tenantService.closeBranch('t1', 'b1', 'Cese de operaciones en Callao');

      expect(httpClientModule.httpClient.delete).toHaveBeenCalledWith(
        '/tenants/t1/branches/b1?reason=Cese%20de%20operaciones%20en%20Callao'
      );
    });

    it('omite el motivo si viene vacío', async () => {
      vi.mocked(httpClientModule.httpClient.delete).mockResolvedValue({});

      await tenantService.closeBranch('t1', 'b1', '   ');

      expect(httpClientModule.httpClient.delete).toHaveBeenCalledWith('/tenants/t1/branches/b1');
    });
  });

  describe('deactivateBranch', () => {
    it('calls deactivate endpoint', async () => {
      vi.mocked(httpClientModule.httpClient.post).mockResolvedValue({});

      await tenantService.deactivateBranch('t1', 'b1');

      expect(httpClientModule.httpClient.post).toHaveBeenCalledWith(
        '/tenants/t1/branches/b1/deactivate'
      );
    });
  });

  describe('reactivateBranch', () => {
    it('calls reactivate endpoint', async () => {
      vi.mocked(httpClientModule.httpClient.post).mockResolvedValue({});

      await tenantService.reactivateBranch('t1', 'b1');

      expect(httpClientModule.httpClient.post).toHaveBeenCalledWith(
        '/tenants/t1/branches/b1/reactivate'
      );
    });
  });
});
