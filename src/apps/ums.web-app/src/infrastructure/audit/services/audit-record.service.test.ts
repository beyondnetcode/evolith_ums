import { describe, it, expect, vi, beforeEach } from 'vitest';
import { auditRecordService } from './audit-record.service';
import * as httpClientModule from '@infra/http/httpClient';
import * as loggerModule from '@app/utils/logger';

vi.mock('@infra/http/httpClient', () => ({
  httpClient: {
    get: vi.fn(),
  },
}));

vi.mock('@app/utils/logger', () => ({
  logger: {
    error: vi.fn(),
  },
}));

const record = {
  auditRecordId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
  whoActed: '3fa85f64-5717-4562-b3fc-2c963f66afa7',
  subjectType: 'UserAccount',
  whenOccurred: '2024-01-01T00:00:00Z',
  whatChanged: 'status changed',
  eventType: 'UserSuspended',
  auditResult: 'Success',
  affectedEntityId: '3fa85f64-5717-4562-b3fc-2c963f66afa8',
  affectedEntityType: 'UserAccount',
  rootTenantId: '3fa85f64-5717-4562-b3fc-2c963f66afa9',
  metadata: null,
};

const emptyPage = {
  items: [],
  page: 1,
  pageSize: 20,
  totalItems: 0,
  totalPages: 0,
};

describe('auditRecordService', () => {
  beforeEach(() => {
    vi.mocked(httpClientModule.httpClient.get).mockClear();
    vi.mocked(loggerModule.logger.error).mockClear();
  });

  describe('getAll', () => {
    it('llama al endpoint sin querystring cuando no se pasan parámetros', async () => {
      vi.mocked(httpClientModule.httpClient.get).mockResolvedValue({ data: emptyPage });

      const result = await auditRecordService.getAll();

      expect(result.items).toHaveLength(0);
      expect(result.page).toBe(1);
      expect(httpClientModule.httpClient.get).toHaveBeenCalledWith('/audit-records?');
    });

    it('aplica page y pageSize por defecto cuando se pasa un objeto vacío', async () => {
      vi.mocked(httpClientModule.httpClient.get).mockResolvedValue({ data: emptyPage });

      await auditRecordService.getAll({});

      expect(httpClientModule.httpClient.get).toHaveBeenCalledWith(
        '/audit-records?page=1&pageSize=20'
      );
    });

    it('propaga todos los filtros al querystring', async () => {
      vi.mocked(httpClientModule.httpClient.get).mockResolvedValue({
        data: { ...emptyPage, items: [record], page: 2, pageSize: 10, totalItems: 1, totalPages: 1 },
      });

      const result = await auditRecordService.getAll({
        page: 2,
        pageSize: 10,
        eventType: 'UserSuspended',
        actorId: '3fa85f64-5717-4562-b3fc-2c963f66afa7',
        entityId: '3fa85f64-5717-4562-b3fc-2c963f66afa8',
        entityType: 'UserAccount',
        tenantId: '3fa85f64-5717-4562-b3fc-2c963f66afa9',
        from: '2024-01-01',
        to: '2024-12-31',
      });

      expect(result.items).toHaveLength(1);
      expect(result.page).toBe(2);
      expect(httpClientModule.httpClient.get).toHaveBeenCalledWith(
        '/audit-records?page=2&pageSize=10&eventType=UserSuspended' +
          '&actorId=3fa85f64-5717-4562-b3fc-2c963f66afa7' +
          '&entityId=3fa85f64-5717-4562-b3fc-2c963f66afa8' +
          '&entityType=UserAccount' +
          '&tenantId=3fa85f64-5717-4562-b3fc-2c963f66afa9' +
          '&from=2024-01-01&to=2024-12-31'
      );
    });

    it('omite los filtros opcionales no provistos', async () => {
      vi.mocked(httpClientModule.httpClient.get).mockResolvedValue({ data: emptyPage });

      await auditRecordService.getAll({ page: 3, pageSize: 5, eventType: 'RoleAssigned' });

      expect(httpClientModule.httpClient.get).toHaveBeenCalledWith(
        '/audit-records?page=3&pageSize=5&eventType=RoleAssigned'
      );
    });

    it('lanza y registra el error cuando la respuesta REST tiene forma inválida', async () => {
      vi.mocked(httpClientModule.httpClient.get).mockResolvedValue({
        data: { invalid: 'shape' },
      });

      await expect(auditRecordService.getAll()).rejects.toThrow(
        'Invalid REST response shape for audit records query'
      );
      expect(loggerModule.logger.error).toHaveBeenCalled();
    });

    it('lanza cuando un item de la página no valida contra el esquema', async () => {
      vi.mocked(httpClientModule.httpClient.get).mockResolvedValue({
        data: { ...emptyPage, items: [{ ...record, auditRecordId: 'no-uuid' }], totalItems: 1 },
      });

      await expect(auditRecordService.getAll({})).rejects.toThrow(
        'Invalid REST response shape for audit records query'
      );
      expect(loggerModule.logger.error).toHaveBeenCalled();
    });
  });
});
