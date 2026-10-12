import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, waitFor, act } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import React from 'react';
import {
  useGetAllTenants,
  useGetTenant,
  useCreateTenant,
  useActivateTenant,
  useSuspendTenant,
  useSetManagementOwner,
} from './use-tenant';
import tenantService from '@infra/identity/services/tenant.service';

vi.mock('@infra/identity/services/tenant.service', () => ({
  tenantService: {
    getAll: vi.fn(),
    getById: vi.fn(),
    getBranches: vi.fn(),
    createTenant: vi.fn(),
    activateTenant: vi.fn(),
    suspendTenant: vi.fn(),
    setManagementOwner: vi.fn(),
  },
  default: {
    getAll: vi.fn(),
    getById: vi.fn(),
    getBranches: vi.fn(),
    createTenant: vi.fn(),
    activateTenant: vi.fn(),
    suspendTenant: vi.fn(),
    setManagementOwner: vi.fn(),
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

describe('use-tenant hooks', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('useGetAllTenants returns paged list', async () => {
    const mockPage = {
      items: [
        { tenantId: 't1', code: 'T1', name: 'Tenant 1', status: 'Active', createdAt: '2024-01-01' },
      ],
      page: 1,
      pageSize: 20,
      totalItems: 1,
      totalPages: 1,
    };

    vi.mocked(tenantService.getAll).mockResolvedValue(mockPage);

    const wrapper = createWrapper();
    const { result } = renderHook(() => useGetAllTenants({ page: 1, pageSize: 20 }), { wrapper });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(result.current.data?.items[0].name).toBe('Tenant 1');
    expect(tenantService.getAll).toHaveBeenCalledWith({ page: 1, pageSize: 20 });
  });

  it('useGetTenant returns tenant by id', async () => {
    const mockTenant = {
      tenantId: 't1',
      code: 'T1',
      name: 'Tenant 1',
      status: 'Active',
      createdAt: '2024-01-01',
    };

    vi.mocked(tenantService.getById).mockResolvedValue(mockTenant);

    const wrapper = createWrapper();
    const { result } = renderHook(() => useGetTenant('t1'), { wrapper });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(result.current.data?.name).toBe('Tenant 1');
    expect(tenantService.getById).toHaveBeenCalledWith('t1');
  });

  it('useGetTenant returns null on 404', async () => {
    const error = new Error('Not Found');
    (error as any).response = { status: 404 };
    vi.mocked(tenantService.getById).mockRejectedValue(error);

    const wrapper = createWrapper();
    const { result } = renderHook(() => useGetTenant('nonexistent'), { wrapper });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(result.current.data).toBeNull();
  });

  it('useCreateTenant calls service successfully', async () => {
    vi.mocked(tenantService.createTenant).mockResolvedValue({ tenantId: 'new-tenant-id' });

    const wrapper = createWrapper();
    const { result } = renderHook(() => useCreateTenant(), { wrapper });

    await act(async () => {
      result.current.mutate({ code: 'NEW', name: 'New Tenant' });
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(tenantService.createTenant).toHaveBeenCalledWith({ code: 'NEW', name: 'New Tenant' });
  });

  it('useActivateTenant calls service successfully', async () => {
    vi.mocked(tenantService.activateTenant).mockResolvedValue();

    const wrapper = createWrapper();
    const { result } = renderHook(() => useActivateTenant('t1'), { wrapper });

    await act(async () => {
      result.current.mutate();
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(tenantService.activateTenant).toHaveBeenCalledWith('t1');
  });

  it('useSuspendTenant calls service successfully', async () => {
    vi.mocked(tenantService.suspendTenant).mockResolvedValue();

    const wrapper = createWrapper();
    const { result } = renderHook(() => useSuspendTenant('t1'), { wrapper });

    await act(async () => {
      result.current.mutate();
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(tenantService.suspendTenant).toHaveBeenCalledWith('t1');
  });

  it('useSetManagementOwner calls service successfully', async () => {
    vi.mocked(tenantService.setManagementOwner).mockResolvedValue();

    const wrapper = createWrapper();
    const { result } = renderHook(() => useSetManagementOwner('t1'), { wrapper });

    await act(async () => {
      result.current.mutate(true);
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(tenantService.setManagementOwner).toHaveBeenCalledWith('t1', true);
  });
});
