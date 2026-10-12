import { describe, it, expect, vi, beforeEach } from 'vitest';
import { systemSuiteService } from './system-suite.service';
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

describe('systemSuiteService', () => {
  beforeEach(() => {
    vi.mocked(httpClientModule.httpClient.get).mockClear();
    vi.mocked(httpClientModule.httpClient.post).mockClear();
    vi.mocked(httpClientModule.httpClient.put).mockClear();
    vi.mocked(httpClientModule.httpClient.delete).mockClear();
    // Cleared HTTP GET mock in the loop
    vi.mocked(loggerModule.logger.error).mockClear();
  });

  describe('getAllSystemSuites', () => {
    it('calls httpClient with default params', async () => {
      vi.mocked(httpClientModule.httpClient.get).mockResolvedValue({
        data: {
          items: [],
          page: 1,
          pageSize: 20,
          totalItems: 0,
          totalPages: 0,
        },
      });

      const result = await systemSuiteService.getAllSystemSuites();

      expect(result.items).toHaveLength(0);
      expect(httpClientModule.httpClient.get).toHaveBeenCalledWith('/system-suites?');
    });

    it('passes custom params', async () => {
      vi.mocked(httpClientModule.httpClient.get).mockResolvedValue({
        data: {
          items: [
            {
              systemSuiteId: '12345678-1234-1234-1234-123456789012',
              tenantId: '12345678-1234-1234-1234-123456789012',
              code: 'S1',
              name: 'Suite 1',
              description: '',
              status: 'Active',
              modules: [],
              actions: [],
              domainResources: [],
            },
          ],
          page: 2,
          pageSize: 10,
          totalItems: 1,
          totalPages: 1,
        },
      });

      const result = await systemSuiteService.getAllSystemSuites({
        page: 2,
        pageSize: 10,
        search: 'test',
      });

      expect(result.items).toHaveLength(1);
      expect(result.page).toBe(2);
      expect(httpClientModule.httpClient.get).toHaveBeenCalledWith(
        '/system-suites?page=2&pageSize=10&search=test'
      );
    });

    it('throws on invalid response', async () => {
      vi.mocked(httpClientModule.httpClient.get).mockResolvedValue({
        data: { invalid: 'shape' },
      });

      await expect(systemSuiteService.getAllSystemSuites()).rejects.toThrow(
        'Invalid REST response shape'
      );
      expect(loggerModule.logger.error).toHaveBeenCalled();
    });
  });

  describe('getSystemSuiteById', () => {
    it('returns parsed system suite', async () => {
      vi.mocked(httpClientModule.httpClient.get).mockResolvedValue({
        data: {
          systemSuiteId: '12345678-1234-1234-1234-123456789012',
          tenantId: '12345678-1234-1234-1234-123456789012',
          code: 'S1',
          name: 'Suite 1',
          description: '',
          status: 'Active',
          modules: [],
          actions: [],
          domainResources: [],
        },
      });

      const result = await systemSuiteService.getSystemSuiteById('s1');

      expect(result.systemSuiteId).toBe('12345678-1234-1234-1234-123456789012');
      expect(httpClientModule.httpClient.get).toHaveBeenCalledWith('/system-suites/s1');
    });

    it('throws when not found', async () => {
      vi.mocked(httpClientModule.httpClient.get).mockResolvedValue({
        data: null,
      });

      await expect(systemSuiteService.getSystemSuiteById('nonexistent')).rejects.toThrow(
        'SystemSuite not found'
      );
    });
  });

  describe('createSystemSuite', () => {
    it('calls POST and returns parsed response', async () => {
      vi.mocked(httpClientModule.httpClient.post).mockResolvedValue({
        data: { systemSuiteId: '3fa85f64-5717-4562-b3fc-2c963f66afa6' },
      });

      const result = await systemSuiteService.createSystemSuite({
        tenantId: '3fa85f64-5717-4562-b3fc-2c963f66afa7',
        code: 'NEW',
        name: 'New Suite',
        description: 'A new suite',
      });

      expect(result.systemSuiteId).toBe('3fa85f64-5717-4562-b3fc-2c963f66afa6');
    });
  });

  describe('updateSystemSuite', () => {
    it('calls PUT endpoint', async () => {
      vi.mocked(httpClientModule.httpClient.put).mockResolvedValue({});

      await systemSuiteService.updateSystemSuite('s1', 'Updated Name', 'Updated Desc');

      expect(httpClientModule.httpClient.put).toHaveBeenCalledWith('/system-suites/s1', {
        name: 'Updated Name',
        description: 'Updated Desc',
      });
    });
  });

  describe('setSystemSuiteStatus', () => {
    it('calls status endpoint via PUT with the status in the body (G-139)', async () => {
      vi.mocked(httpClientModule.httpClient.put).mockResolvedValue({});

      await systemSuiteService.setSystemSuiteStatus('s1', 'Active');

      // El endpoint es `PUT /{id}/status` con el estado en el CUERPO (SetSystemSuiteStatusPayload).
      expect(httpClientModule.httpClient.put).toHaveBeenCalledWith('/system-suites/s1/status', {
        status: 'Active',
      });
    });
  });

  describe('addModule', () => {
    it('calls POST modules endpoint', async () => {
      vi.mocked(httpClientModule.httpClient.post).mockResolvedValue({});

      await systemSuiteService.addModule('s1', { code: 'MOD1', name: 'Module 1', sortOrder: 1 });

      expect(httpClientModule.httpClient.post).toHaveBeenCalledWith(
        '/system-suites/s1/modules',
        expect.objectContaining({ code: 'MOD1' })
      );
    });

    it('trims description', async () => {
      vi.mocked(httpClientModule.httpClient.post).mockResolvedValue({});

      await systemSuiteService.addModule('s1', {
        code: 'MOD1',
        name: 'Module 1',
        description: '  test  ',
        sortOrder: 1,
      });

      expect(httpClientModule.httpClient.post).toHaveBeenCalledWith(
        '/system-suites/s1/modules',
        expect.objectContaining({ description: 'test' })
      );
    });
  });

  describe('removeModule', () => {
    it('calls DELETE module endpoint', async () => {
      vi.mocked(httpClientModule.httpClient.delete).mockResolvedValue({});

      await systemSuiteService.removeModule('s1', 'mod1');

      expect(httpClientModule.httpClient.delete).toHaveBeenCalledWith(
        '/system-suites/s1/modules/mod1'
      );
    });
  });

  describe('activateModule', () => {
    it('calls activate endpoint', async () => {
      vi.mocked(httpClientModule.httpClient.post).mockResolvedValue({});

      await systemSuiteService.activateModule('s1', 'mod1');

      expect(httpClientModule.httpClient.post).toHaveBeenCalledWith(
        '/system-suites/s1/modules/mod1/activate'
      );
    });
  });

  describe('deactivateModule', () => {
    it('calls deactivate endpoint', async () => {
      vi.mocked(httpClientModule.httpClient.post).mockResolvedValue({});

      await systemSuiteService.deactivateModule('s1', 'mod1');

      expect(httpClientModule.httpClient.post).toHaveBeenCalledWith(
        '/system-suites/s1/modules/mod1/deactivate'
      );
    });
  });

  describe('addNode', () => {
    it('calls POST nodes endpoint with parentNodeId', async () => {
      vi.mocked(httpClientModule.httpClient.post).mockResolvedValue({});

      await systemSuiteService.addNode('s1', 'mod1', {
        parentNodeId: 'root1',
        kind: 'Option',
        code: 'NODE1',
        label: 'Node 1',
        sortOrder: 1,
      });

      expect(httpClientModule.httpClient.post).toHaveBeenCalledWith(
        '/system-suites/s1/modules/mod1/nodes',
        expect.objectContaining({ code: 'NODE1', kind: 'Option', parentNodeId: 'root1' })
      );
    });

    it('sends parentNodeId null for a root node', async () => {
      vi.mocked(httpClientModule.httpClient.post).mockResolvedValue({});

      await systemSuiteService.addNode('s1', 'mod1', {
        kind: 'Menu',
        code: 'ROOT',
        label: 'Root',
        sortOrder: 1,
      });

      expect(httpClientModule.httpClient.post).toHaveBeenCalledWith(
        '/system-suites/s1/modules/mod1/nodes',
        expect.objectContaining({ parentNodeId: null })
      );
    });
  });

  describe('updateNode', () => {
    it('calls PUT node endpoint', async () => {
      vi.mocked(httpClientModule.httpClient.put).mockResolvedValue({});

      await systemSuiteService.updateNode('s1', 'mod1', 'node1', {
        label: 'Nuevo',
        sortOrder: 2,
      });

      expect(httpClientModule.httpClient.put).toHaveBeenCalledWith(
        '/system-suites/s1/modules/mod1/nodes/node1',
        expect.objectContaining({ label: 'Nuevo', sortOrder: 2 })
      );
    });
  });

  describe('removeNode', () => {
    it('calls DELETE node endpoint', async () => {
      vi.mocked(httpClientModule.httpClient.delete).mockResolvedValue({});

      await systemSuiteService.removeNode('s1', 'mod1', 'node1');

      expect(httpClientModule.httpClient.delete).toHaveBeenCalledWith(
        '/system-suites/s1/modules/mod1/nodes/node1'
      );
    });
  });

  describe('setNodeStatus', () => {
    it('calls PUT node status endpoint', async () => {
      vi.mocked(httpClientModule.httpClient.put).mockResolvedValue({});

      await systemSuiteService.setNodeStatus('s1', 'mod1', 'node1', false);

      expect(httpClientModule.httpClient.put).toHaveBeenCalledWith(
        '/system-suites/s1/modules/mod1/nodes/node1/status',
        { active: false }
      );
    });
  });

  describe('linkNodeAction / unlinkNodeAction (N:M)', () => {
    it('calls POST node actions endpoint', async () => {
      vi.mocked(httpClientModule.httpClient.post).mockResolvedValue({});

      await systemSuiteService.linkNodeAction('s1', 'mod1', 'node1', 'VIEW');

      expect(httpClientModule.httpClient.post).toHaveBeenCalledWith(
        '/system-suites/s1/modules/mod1/nodes/node1/actions',
        { actionCode: 'VIEW' }
      );
    });

    it('calls DELETE node action endpoint', async () => {
      vi.mocked(httpClientModule.httpClient.delete).mockResolvedValue({});

      await systemSuiteService.unlinkNodeAction('s1', 'mod1', 'node1', 'VIEW');

      expect(httpClientModule.httpClient.delete).toHaveBeenCalledWith(
        '/system-suites/s1/modules/mod1/nodes/node1/actions/VIEW'
      );
    });
  });

  describe('setNodeMetadata', () => {
    it('calls PUT node metadata endpoint', async () => {
      vi.mocked(httpClientModule.httpClient.put).mockResolvedValue({});

      await systemSuiteService.setNodeMetadata('s1', 'mod1', 'node1', {
        responsable: 'QA',
        criticidad: 'Alta',
      });

      expect(httpClientModule.httpClient.put).toHaveBeenCalledWith(
        '/system-suites/s1/modules/mod1/nodes/node1/metadata',
        expect.objectContaining({ responsable: 'QA', criticidad: 'Alta' })
      );
    });
  });

  describe('registerAction', () => {
    it('calls POST actions endpoint', async () => {
      vi.mocked(httpClientModule.httpClient.post).mockResolvedValue({});

      await systemSuiteService.registerAction('s1', { code: 'ACT1', name: 'Action 1' });

      expect(httpClientModule.httpClient.post).toHaveBeenCalledWith('/system-suites/s1/actions', {
        code: 'ACT1',
        name: 'Action 1',
      });
    });
  });

  describe('removeAction', () => {
    it('calls DELETE action endpoint', async () => {
      vi.mocked(httpClientModule.httpClient.delete).mockResolvedValue({});

      await systemSuiteService.removeAction('s1', 'ACT1');

      expect(httpClientModule.httpClient.delete).toHaveBeenCalledWith(
        '/system-suites/s1/actions/ACT1'
      );
    });
  });

  describe('addDomainResource', () => {
    it('calls POST domain-resources endpoint', async () => {
      vi.mocked(httpClientModule.httpClient.post).mockResolvedValue({});

      await systemSuiteService.addDomainResource('s1', {
        type: 'Aggregate',
        code: 'DR1',
        name: 'Resource 1',
        description: 'Test',
      });

      expect(httpClientModule.httpClient.post).toHaveBeenCalledWith(
        '/system-suites/s1/domain-resources',
        expect.objectContaining({ code: 'DR1' })
      );
    });
  });

  describe('removeDomainResource', () => {
    it('calls DELETE domain-resource endpoint', async () => {
      vi.mocked(httpClientModule.httpClient.delete).mockResolvedValue({});

      await systemSuiteService.removeDomainResource('s1', 'dr1');

      expect(httpClientModule.httpClient.delete).toHaveBeenCalledWith(
        '/system-suites/s1/domain-resources/dr1'
      );
    });
  });
});
