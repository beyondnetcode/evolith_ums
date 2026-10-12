import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, waitFor, act } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import React from 'react';
import {
  useGetAllFeatureFlags,
  useGetFeatureFlagsBySystemSuite,
  useGetFeatureFlagById,
  useCreateFeatureFlag,
  useActivateFlag,
  useDeactivateFlag,
  useArchiveFlag,
} from './use-feature-flag';
import featureFlagService from '@infra/configuration/services/feature-flag.service';

vi.mock('@infra/configuration/services/feature-flag.service', () => ({
  featureFlagService: {
    getAll: vi.fn(),
    getById: vi.fn(),
    getFeatureFlagsBySystemSuite: vi.fn(),
    createFeatureFlag: vi.fn(),
    updateFeatureFlag: vi.fn(),
    activateFlag: vi.fn(),
    deactivateFlag: vi.fn(),
    archiveFlag: vi.fn(),
    addCriteria: vi.fn(),
    removeCriteria: vi.fn(),
  },
  default: {
    getAll: vi.fn(),
    getById: vi.fn(),
    getFeatureFlagsBySystemSuite: vi.fn(),
    createFeatureFlag: vi.fn(),
    updateFeatureFlag: vi.fn(),
    activateFlag: vi.fn(),
    deactivateFlag: vi.fn(),
    archiveFlag: vi.fn(),
    addCriteria: vi.fn(),
    removeCriteria: vi.fn(),
  },
}));

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );
}

describe('use-feature-flag hooks', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('useGetAllFeatureFlags returns paged list', async () => {
    const mockPage = {
      items: [
        {
          featureFlagId: 'f1',
          code: 'FLAG_1',
          name: 'Flag 1',
          description: '',
          flagType: 'boolean',
          status: 'Active',
          sortOrder: 0,
          criteria: [],
        },
      ],
      page: 1,
      pageSize: 20,
      totalItems: 1,
      totalPages: 1,
    };

    vi.mocked(featureFlagService.getAll).mockResolvedValue(mockPage);

    const wrapper = createWrapper();
    const { result } = renderHook(() => useGetAllFeatureFlags({ page: 1, pageSize: 20 }), {
      wrapper,
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(result.current.data?.items[0].name).toBe('Flag 1');
    expect(featureFlagService.getAll).toHaveBeenCalledWith({ page: 1, pageSize: 20 });
  });

  it('useGetFeatureFlagsBySystemSuite returns flags list', async () => {
    const mockFlags = [
      {
        featureFlagId: '12345678-1234-1234-1234-123456789012',
        systemSuiteId: '12345678-1234-1234-1234-123456789012',
        flagCode: 'FLAG_1',
        flagType: 'Boolean',
        flagTargets: 'all',
        status: 'Active',
        criteria: [],
      },
    ];

    vi.mocked(featureFlagService.getFeatureFlagsBySystemSuite).mockResolvedValue(mockFlags);

    const wrapper = createWrapper();
    const { result } = renderHook(() => useGetFeatureFlagsBySystemSuite('suite-1'), { wrapper });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(result.current.data?.[0].flagCode).toBe('FLAG_1');
    expect(featureFlagService.getFeatureFlagsBySystemSuite).toHaveBeenCalledWith('suite-1');
  });

  it('useGetFeatureFlagById returns flag by id', async () => {
    const mockFlag = {
      featureFlagId: '12345678-1234-1234-1234-123456789012',
      systemSuiteId: '12345678-1234-1234-1234-123456789012',
      flagCode: 'FLAG_1',
      flagType: 'Boolean',
      flagTargets: 'all',
      status: 'Active',
      criteria: [],
    };

    vi.mocked(featureFlagService.getById).mockResolvedValue(mockFlag);

    const wrapper = createWrapper();
    const { result } = renderHook(() => useGetFeatureFlagById('f1'), { wrapper });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(result.current.data?.flagCode).toBe('FLAG_1');
    expect(featureFlagService.getById).toHaveBeenCalledWith('f1');
  });

  it('useGetFeatureFlagById returns null on 404', async () => {
    const error = new Error('Not Found');
    (error as any).response = { status: 404 };
    vi.mocked(featureFlagService.getById).mockRejectedValue(error);

    const wrapper = createWrapper();
    const { result } = renderHook(() => useGetFeatureFlagById('nonexistent'), { wrapper });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(result.current.data).toBeNull();
  });

  it('useCreateFeatureFlag calls service successfully', async () => {
    vi.mocked(featureFlagService.createFeatureFlag).mockResolvedValue({
      featureFlagId: 'new-flag-id',
    });

    const wrapper = createWrapper();
    const { result } = renderHook(() => useCreateFeatureFlag(), { wrapper });

    await act(async () => {
      result.current.mutate({
        code: 'NEW_FLAG',
        name: 'New Flag',
        description: '',
        flagType: 'boolean',
      });
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(featureFlagService.createFeatureFlag).toHaveBeenCalledWith(
      expect.objectContaining({ code: 'NEW_FLAG' })
    );
  });

  it('useActivateFlag calls service successfully', async () => {
    vi.mocked(featureFlagService.activateFlag).mockResolvedValue();

    const wrapper = createWrapper();
    const { result } = renderHook(() => useActivateFlag('f1'), { wrapper });

    await act(async () => {
      result.current.mutate();
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(featureFlagService.activateFlag).toHaveBeenCalledWith('f1');
  });

  it('useDeactivateFlag calls service successfully', async () => {
    vi.mocked(featureFlagService.deactivateFlag).mockResolvedValue();

    const wrapper = createWrapper();
    const { result } = renderHook(() => useDeactivateFlag('f1'), { wrapper });

    await act(async () => {
      result.current.mutate();
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(featureFlagService.deactivateFlag).toHaveBeenCalledWith('f1');
  });

  it('useArchiveFlag calls service successfully', async () => {
    vi.mocked(featureFlagService.archiveFlag).mockResolvedValue();

    const wrapper = createWrapper();
    const { result } = renderHook(() => useArchiveFlag('f1'), { wrapper });

    await act(async () => {
      result.current.mutate();
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(featureFlagService.archiveFlag).toHaveBeenCalledWith('f1');
  });
});
