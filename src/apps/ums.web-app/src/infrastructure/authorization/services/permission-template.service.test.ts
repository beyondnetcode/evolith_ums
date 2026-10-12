import { describe, it, expect, vi, beforeEach } from 'vitest';
import { permissionTemplateService } from './permission-template.service';
import * as httpClientModule from '@infra/http/httpClient';
import * as loggerModule from '@app/utils/logger';

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
  },
}));

describe('permissionTemplateService', () => {
  beforeEach(() => {
    vi.mocked(httpClientModule.httpClient.get).mockClear();
    vi.mocked(httpClientModule.httpClient.post).mockClear();
    vi.mocked(httpClientModule.httpClient.put).mockClear();
    vi.mocked(httpClientModule.httpClient.delete).mockClear();
    vi.mocked(loggerModule.logger.error).mockClear();
  });

  describe('getAll', () => {
    it('calls GET with default params', async () => {
      vi.mocked(httpClientModule.httpClient.get).mockResolvedValue({
        data: {
          items: [],
          page: 1,
          pageSize: 20,
          totalItems: 0,
          totalPages: 0,
        },
      });

      const result = await permissionTemplateService.getAll();

      expect(result.items).toHaveLength(0);
      expect(httpClientModule.httpClient.get).toHaveBeenCalledWith(
        expect.stringContaining('/permission-templates?')
      );
      expect(httpClientModule.httpClient.get).toHaveBeenCalledWith(
        expect.stringContaining('page=1')
      );
    });

    it('passes custom params', async () => {
      vi.mocked(httpClientModule.httpClient.get).mockResolvedValue({
        data: {
          items: [
            {
              templateId: '12345678-1234-1234-1234-123456789012',
              tenantId: '12345678-1234-1234-1234-123456789012',
              roleId: '12345678-1234-1234-1234-123456789012',
              roleName: 'Admin',
              systemSuiteId: '12345678-1234-1234-1234-123456789012',
              systemSuiteName: 'Suite 1',
              version: '1.0',
              status: 'Draft',
            },
          ],
          page: 2,
          pageSize: 10,
          totalItems: 1,
          totalPages: 1,
        },
      });

      const result = await permissionTemplateService.getAll({
        page: 2,
        pageSize: 10,
        search: 'test',
      });

      expect(result.items).toHaveLength(1);
      expect(result.page).toBe(2);
      const calledUrl = vi.mocked(httpClientModule.httpClient.get).mock.calls[0][0];
      expect(calledUrl).toContain('page=2');
      expect(calledUrl).toContain('pageSize=10');
      expect(calledUrl).toContain('search=test');
    });

    it('throws on invalid response', async () => {
      vi.mocked(httpClientModule.httpClient.get).mockResolvedValue({
        data: { invalid: 'shape' },
      });

      await expect(permissionTemplateService.getAll()).rejects.toThrow('Invalid response shape');
      expect(loggerModule.logger.error).toHaveBeenCalled();
    });
  });

  describe('getById', () => {
    it('returns parsed template detail', async () => {
      vi.mocked(httpClientModule.httpClient.get).mockResolvedValue({
        data: {
          templateId: '12345678-1234-1234-1234-123456789012',
          tenantId: '12345678-1234-1234-1234-123456789012',
          roleId: '12345678-1234-1234-1234-123456789012',
          roleName: 'Admin',
          systemSuiteId: '12345678-1234-1234-1234-123456789012',
          systemSuiteName: 'Suite 1',
          version: '1.0',
          status: 'Draft',
          items: [],
        },
      });

      const result = await permissionTemplateService.getById('t1');

      expect(result.templateId).toBe('12345678-1234-1234-1234-123456789012');
      expect(httpClientModule.httpClient.get).toHaveBeenCalledWith('/permission-templates/t1');
    });

    it('throws when not found', async () => {
      vi.mocked(httpClientModule.httpClient.get).mockResolvedValue({
        data: null,
      });

      await expect(permissionTemplateService.getById('nonexistent')).rejects.toThrow(
        'Permission template not found'
      );
    });
  });

  describe('create', () => {
    it('calls POST and returns parsed response', async () => {
      vi.mocked(httpClientModule.httpClient.post).mockResolvedValue({
        data: { templateId: '12345678-1234-1234-1234-123456789012' },
      });

      const result = await permissionTemplateService.create({
        tenantId: '12345678-1234-1234-1234-123456789012',
        roleId: '12345678-1234-1234-1234-123456789012',
        systemSuiteId: '12345678-1234-1234-1234-123456789012',
      });

      expect(result.templateId).toBe('12345678-1234-1234-1234-123456789012');
    });
  });

  describe('publish', () => {
    it('calls publish endpoint', async () => {
      vi.mocked(httpClientModule.httpClient.post).mockResolvedValue({});

      await permissionTemplateService.publish('t1');

      expect(httpClientModule.httpClient.post).toHaveBeenCalledWith(
        '/permission-templates/t1/publish'
      );
    });
  });

  describe('deprecate', () => {
    it('calls deprecate endpoint', async () => {
      vi.mocked(httpClientModule.httpClient.post).mockResolvedValue({});

      await permissionTemplateService.deprecate('t1');

      expect(httpClientModule.httpClient.post).toHaveBeenCalledWith(
        '/permission-templates/t1/deprecate'
      );
    });
  });

  describe('delete', () => {
    it('calls DELETE endpoint', async () => {
      vi.mocked(httpClientModule.httpClient.delete).mockResolvedValue({});

      await permissionTemplateService.delete('t1');

      expect(httpClientModule.httpClient.delete).toHaveBeenCalledWith('/permission-templates/t1');
    });
  });

  describe('addItem', () => {
    it('calls POST items endpoint', async () => {
      vi.mocked(httpClientModule.httpClient.post).mockResolvedValue({});

      await permissionTemplateService.addItem('t1', {
        targetType: 'SystemSuite',
        targetId: '12345678-1234-1234-1234-123456789012',
        actionId: '12345678-1234-1234-1234-123456789012',
        isAllowed: true,
        isDenied: false,
      });

      expect(httpClientModule.httpClient.post).toHaveBeenCalledWith(
        '/permission-templates/t1/items',
        expect.objectContaining({ targetType: 'SystemSuite' })
      );
    });
  });

  describe('removeItem', () => {
    it('calls DELETE item endpoint', async () => {
      vi.mocked(httpClientModule.httpClient.delete).mockResolvedValue({});

      await permissionTemplateService.removeItem('t1', 'item1');

      expect(httpClientModule.httpClient.delete).toHaveBeenCalledWith(
        '/permission-templates/t1/items/item1'
      );
    });
  });

  describe('setItemEffect', () => {
    it('calls PUT effect endpoint', async () => {
      vi.mocked(httpClientModule.httpClient.put).mockResolvedValue({});

      await permissionTemplateService.setItemEffect('t1', 'item1', 'Allow');

      expect(httpClientModule.httpClient.put).toHaveBeenCalledWith(
        '/permission-templates/t1/items/item1/effect',
        { effect: 'Allow' }
      );
    });
  });

  describe('activateItem', () => {
    it('calls activate item endpoint', async () => {
      vi.mocked(httpClientModule.httpClient.post).mockResolvedValue({});

      await permissionTemplateService.activateItem('t1', 'item1');

      expect(httpClientModule.httpClient.post).toHaveBeenCalledWith(
        '/permission-templates/t1/items/item1/activate'
      );
    });
  });

  describe('deactivateItem', () => {
    it('calls deactivate item endpoint', async () => {
      vi.mocked(httpClientModule.httpClient.post).mockResolvedValue({});

      await permissionTemplateService.deactivateItem('t1', 'item1');

      expect(httpClientModule.httpClient.post).toHaveBeenCalledWith(
        '/permission-templates/t1/items/item1/deactivate'
      );
    });
  });
});
