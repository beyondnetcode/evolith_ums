import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, waitFor, act } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import React from 'react';
import {
  useGetDelegation,
  useGetDelegationsByDelegatedAdmin,
  useGetDelegationsByDelegatingAdmin,
  useCreateDelegation,
  useActivateDelegation,
  useRevokeDelegation,
} from './use-delegation';
import delegationService from '@infra/identity/services/delegation.service';

vi.mock('@infra/identity/services/delegation.service', () => ({
  delegationService: {
    getDelegationById: vi.fn(),
    getDelegationsByDelegatedAdmin: vi.fn(),
    getDelegationsByDelegatingAdmin: vi.fn(),
    createDelegation: vi.fn(),
    activateDelegation: vi.fn(),
    revokeDelegation: vi.fn(),
  },
  default: {
    getDelegationById: vi.fn(),
    getDelegationsByDelegatedAdmin: vi.fn(),
    getDelegationsByDelegatingAdmin: vi.fn(),
    createDelegation: vi.fn(),
    activateDelegation: vi.fn(),
    revokeDelegation: vi.fn(),
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

describe('use-delegation hooks', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('useGetDelegation returns delegation by id', async () => {
    const mockDelegation = {
      delegationId: 'd1',
      delegatingAdminId: 'admin1',
      delegatedAdminId: 'admin2',
      scopeType: 'Tenant',
      status: 'Active',
      actions: [],
    };

    vi.mocked(delegationService.getDelegationById).mockResolvedValue(mockDelegation);

    const wrapper = createWrapper();
    const { result } = renderHook(() => useGetDelegation('d1'), { wrapper });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(result.current.data?.delegationId).toBe('d1');
    expect(delegationService.getDelegationById).toHaveBeenCalledWith('d1');
  });

  it('useGetDelegation returns null on 404', async () => {
    const error = new Error('Not Found');
    (error as any).response = { status: 404 };
    vi.mocked(delegationService.getDelegationById).mockRejectedValue(error);

    const wrapper = createWrapper();
    const { result } = renderHook(() => useGetDelegation('nonexistent'), { wrapper });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(result.current.data).toBeNull();
  });

  it('useGetDelegationsByDelegatedAdmin returns list', async () => {
    const mockDelegations = [
      {
        delegationId: '12345678-1234-1234-1234-123456789012',
        delegatingAdminId: '12345678-1234-1234-1234-123456789012',
        delegatedAdminId: '12345678-1234-1234-1234-123456789012',
        scopeType: 'Tenant',
        status: 'Active',
        actions: [],
      },
    ];

    vi.mocked(delegationService.getDelegationsByDelegatedAdmin).mockResolvedValue(mockDelegations);

    const wrapper = createWrapper();
    const { result } = renderHook(() => useGetDelegationsByDelegatedAdmin('admin2', 't1'), {
      wrapper,
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(result.current.data?.[0].delegationId).toBe('12345678-1234-1234-1234-123456789012');
    expect(delegationService.getDelegationsByDelegatedAdmin).toHaveBeenCalledWith('admin2', 't1');
  });

  it('useGetDelegationsByDelegatingAdmin returns list', async () => {
    const mockDelegations = [
      {
        delegationId: '12345678-1234-1234-1234-123456789012',
        delegatingAdminId: '12345678-1234-1234-1234-123456789012',
        delegatedAdminId: '12345678-1234-1234-1234-123456789012',
        scopeType: 'Tenant',
        status: 'Active',
        actions: [],
      },
    ];

    vi.mocked(delegationService.getDelegationsByDelegatingAdmin).mockResolvedValue(mockDelegations);

    const wrapper = createWrapper();
    const { result } = renderHook(() => useGetDelegationsByDelegatingAdmin('admin1', 't1'), {
      wrapper,
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(result.current.data?.[0].delegationId).toBe('12345678-1234-1234-1234-123456789012');
    expect(delegationService.getDelegationsByDelegatingAdmin).toHaveBeenCalledWith('admin1', 't1');
  });

  it('useCreateDelegation calls service successfully', async () => {
    vi.mocked(delegationService.createDelegation).mockResolvedValue({
      delegationId: 'new-delegation-id',
    });

    const wrapper = createWrapper();
    const { result } = renderHook(() => useCreateDelegation(), { wrapper });

    await act(async () => {
      result.current.mutate({
        delegatingAdminId: 'admin1',
        delegatedAdminId: 'admin2',
        scopeType: 'Tenant',
        actions: [],
      });
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(delegationService.createDelegation).toHaveBeenCalledWith(expect.any(Object));
  });

  it('useActivateDelegation calls service successfully', async () => {
    vi.mocked(delegationService.activateDelegation).mockResolvedValue();

    const wrapper = createWrapper();
    const { result } = renderHook(() => useActivateDelegation('d1'), { wrapper });

    await act(async () => {
      result.current.mutate();
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(delegationService.activateDelegation).toHaveBeenCalledWith('d1');
  });

  it('useRevokeDelegation calls service successfully', async () => {
    vi.mocked(delegationService.revokeDelegation).mockResolvedValue();

    const wrapper = createWrapper();
    const { result } = renderHook(() => useRevokeDelegation('d1'), { wrapper });

    await act(async () => {
      result.current.mutate('No longer needed');
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(delegationService.revokeDelegation).toHaveBeenCalledWith('d1', 'No longer needed');
  });
});
