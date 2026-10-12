import { describe, it, expect, vi, beforeEach } from 'vitest';
import { featureFlagService } from './feature-flag.service';
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

describe('featureFlagService', () => {
  beforeEach(() => {
    vi.mocked(httpClientModule.httpClient.get).mockClear();
    vi.mocked(httpClientModule.httpClient.post).mockClear();
    vi.mocked(httpClientModule.httpClient.put).mockClear();
    vi.mocked(httpClientModule.httpClient.delete).mockClear();
    vi.mocked(loggerModule.logger.error).mockClear();
  });

  describe('getAllFeatureFlags', () => {
    it('calls REST endpoint with default params', async () => {
      vi.mocked(httpClientModule.httpClient.get).mockResolvedValue({
        data: {
          items: [],
          page: 1,
          pageSize: 20,
          totalItems: 0,
          totalPages: 0,
        },
      });

      const result = await featureFlagService.getAllFeatureFlags();

      expect(result.items).toHaveLength(0);
      expect(httpClientModule.httpClient.get).toHaveBeenCalledWith(
        '/feature-flags?page=1&pageSize=20'
      );
    });

    it('passes custom params to REST query', async () => {
      vi.mocked(httpClientModule.httpClient.get).mockResolvedValue({
        data: {
          items: [
            {
              featureFlagId: '12345678-1234-1234-1234-123456789012',
              systemSuiteId: '12345678-1234-1234-1234-123456789012',
              systemSuiteCode: 'SUITE-01',
              systemSuiteName: 'Suite Alpha',
              flagCode: 'FLAG_1',
              flagType: 'Boolean',
              flagTargets: 'all',
              status: 'Active',
              criteria: [],
            },
          ],
          page: 2,
          pageSize: 10,
          totalItems: 1,
          totalPages: 1,
        },
      });

      const result = await featureFlagService.getAllFeatureFlags({
        page: 2,
        pageSize: 10,
        search: 'test',
      });

      expect(result.items).toHaveLength(1);
      expect(result.page).toBe(2);
      expect(httpClientModule.httpClient.get).toHaveBeenCalledWith(
        '/feature-flags?page=2&pageSize=10&search=test'
      );
    });

    it('throws on invalid response shape', async () => {
      vi.mocked(httpClientModule.httpClient.get).mockResolvedValue({
        data: { invalid: 'shape' },
      });

      await expect(featureFlagService.getAllFeatureFlags()).rejects.toThrow(
        'Invalid REST response shape for feature flags query'
      );
      expect(loggerModule.logger.error).toHaveBeenCalled();
    });
  });

  describe('getFeatureFlagById', () => {
    it('returns parsed feature flag', async () => {
      vi.mocked(httpClientModule.httpClient.get).mockResolvedValue({
        data: {
          featureFlagId: '12345678-1234-1234-1234-123456789012',
          systemSuiteId: '12345678-1234-1234-1234-123456789012',
          systemSuiteCode: 'SUITE-01',
          systemSuiteName: 'Suite Alpha',
          flagCode: 'FLAG_1',
          flagType: 'Boolean',
          flagTargets: 'all',
          status: 'Active',
          criteria: [],
        },
      });

      const result = await featureFlagService.getFeatureFlagById('flag-1');

      expect(result.featureFlagId).toBe('12345678-1234-1234-1234-123456789012');
    });

    it('throws when flag not found', async () => {
      vi.mocked(httpClientModule.httpClient.get).mockResolvedValue({
        data: null,
      });

      await expect(featureFlagService.getFeatureFlagById('nonexistent')).rejects.toThrow(
        'FeatureFlag not found'
      );
    });
  });

  describe('getFeatureFlagsBySystemSuite', () => {
    it('calls REST endpoint and returns flags', async () => {
      const mockFlags = [
        {
          featureFlagId: '12345678-1234-1234-1234-123456789012',
          systemSuiteId: '12345678-1234-1234-1234-123456789012',
          systemSuiteCode: 'SUITE-01',
          systemSuiteName: 'Suite Alpha',
          flagCode: 'FLAG_1',
          flagType: 'Boolean',
          flagTargets: 'all',
          status: 'Active',
          criteria: [],
        },
      ];
      vi.mocked(httpClientModule.httpClient.get).mockResolvedValue({ data: mockFlags });

      const result = await featureFlagService.getFeatureFlagsBySystemSuite('suite-1');

      expect(result).toHaveLength(1);
      expect(httpClientModule.httpClient.get).toHaveBeenCalledWith(
        '/system-suites/suite-1/feature-flags'
      );
    });
  });

  describe('createFeatureFlag', () => {
    it('calls POST and returns parsed response', async () => {
      vi.mocked(httpClientModule.httpClient.post).mockResolvedValue({
        data: { featureFlagId: '12345678-1234-1234-1234-123456789012' },
      });

      const result = await featureFlagService.createFeatureFlag({
        flagCode: 'NEW_FLAG',
        flagType: 'Boolean',
        flagTargets: 'all',
      });

      expect(result.featureFlagId).toBe('12345678-1234-1234-1234-123456789012');
      expect(httpClientModule.httpClient.post).toHaveBeenCalledWith(
        '/feature-flags',
        expect.any(Object)
      );
    });
  });

  describe('updateFeatureFlag', () => {
    it('calls PUT endpoint', async () => {
      vi.mocked(httpClientModule.httpClient.put).mockResolvedValue({});

      await featureFlagService.updateFeatureFlag('flag-1', { name: 'Updated' });

      expect(httpClientModule.httpClient.put).toHaveBeenCalledWith('/feature-flags/flag-1', {
        name: 'Updated',
      });
    });
  });

  describe('activateFlag', () => {
    it('calls activate endpoint', async () => {
      vi.mocked(httpClientModule.httpClient.post).mockResolvedValue({});

      await featureFlagService.activateFlag('flag-1');

      expect(httpClientModule.httpClient.post).toHaveBeenCalledWith(
        '/feature-flags/flag-1/activate'
      );
    });
  });

  describe('deactivateFlag', () => {
    it('calls deactivate endpoint', async () => {
      vi.mocked(httpClientModule.httpClient.post).mockResolvedValue({});

      await featureFlagService.deactivateFlag('flag-1');

      expect(httpClientModule.httpClient.post).toHaveBeenCalledWith(
        '/feature-flags/flag-1/deactivate'
      );
    });
  });

  describe('archiveFlag', () => {
    it('calls archive endpoint', async () => {
      vi.mocked(httpClientModule.httpClient.post).mockResolvedValue({});

      await featureFlagService.archiveFlag('flag-1');

      expect(httpClientModule.httpClient.post).toHaveBeenCalledWith(
        '/feature-flags/flag-1/archive'
      );
    });
  });

  describe('addCriteria', () => {
    it('calls POST criteria endpoint', async () => {
      vi.mocked(httpClientModule.httpClient.post).mockResolvedValue({
        data: { criteriaId: '12345678-1234-1234-1234-123456789012' },
      });

      const result = await featureFlagService.addCriteria('flag-1', {
        criteriaType: 'TenantId',
        operator: 'Equals',
        value: 'test',
      });

      expect(result.criteriaId).toBe('12345678-1234-1234-1234-123456789012');
      expect(httpClientModule.httpClient.post).toHaveBeenCalledWith(
        '/feature-flags/flag-1/criteria',
        expect.any(Object)
      );
    });
  });

  describe('removeCriteria', () => {
    it('calls DELETE criteria endpoint', async () => {
      vi.mocked(httpClientModule.httpClient.delete).mockResolvedValue({});

      await featureFlagService.removeCriteria('flag-1', 'crit-1');

      expect(httpClientModule.httpClient.delete).toHaveBeenCalledWith(
        '/feature-flags/flag-1/criteria/crit-1'
      );
    });
  });
});
