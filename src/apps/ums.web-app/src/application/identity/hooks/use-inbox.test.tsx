import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, waitFor, act } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import React from 'react';
import {
  useGetUserSignups,
  useGetProfileRequests,
  useActivateUser,
  useDenyUserSignup,
  useApproveProfileRequest,
  useRejectProfileRequest,
} from './use-inbox';
import inboxService from '@infra/identity/services/inbox.service';

vi.mock('@infra/identity/services/inbox.service', () => ({
  inboxService: {
    getUserSignups: vi.fn(),
    getProfileRequests: vi.fn(),
    activateUser: vi.fn(),
    denyUserSignup: vi.fn(),
    approveProfileRequest: vi.fn(),
    rejectProfileRequest: vi.fn(),
  },
  default: {
    getUserSignups: vi.fn(),
    getProfileRequests: vi.fn(),
    activateUser: vi.fn(),
    denyUserSignup: vi.fn(),
    approveProfileRequest: vi.fn(),
    rejectProfileRequest: vi.fn(),
  },
}));

vi.mock('@app/hooks/use-notified-mutation', () => ({
  useNotifiedMutation: (config: any) => ({
    mutate: vi.fn((vars: any) => config.mutationFn(vars)),
    mutateAsync: vi.fn((vars: any) => config.mutationFn(vars)),
    isPending: false,
    isSuccess: false,
    isError: false,
    data: undefined,
  }),
}));

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );
}

describe('use-inbox hooks', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('useGetUserSignups returns pending signups', async () => {
    const mockSignups = [
      {
        userAccountId: 'user-1',
        tenantId: 'tenant-1',
        email: 'new.user@ums.local',
        displayName: 'New User',
        category: 'External',
        requestedAt: '2026-06-01T10:00:00Z',
      },
    ];

    vi.mocked(inboxService.getUserSignups).mockResolvedValue(mockSignups);

    const wrapper = createWrapper();
    const { result } = renderHook(() => useGetUserSignups(), { wrapper });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(result.current.data?.[0].email).toBe('new.user@ums.local');
    expect(inboxService.getUserSignups).toHaveBeenCalledTimes(1);
  });

  it('useGetProfileRequests returns pending profile requests', async () => {
    const mockRequests = [
      {
        approvalRequestId: '11111111-1111-1111-1111-111111111111',
        targetUserId: '22222222-2222-2222-2222-222222222222',
        requestedSystemId: '33333333-3333-3333-3333-333333333333',
        requestedBranchId: null,
        requestedRoleId: '44444444-4444-4444-4444-444444444444',
        justification: 'Need access for operations',
        requestedAt: '2026-06-01T10:00:00Z',
      },
    ];

    vi.mocked(inboxService.getProfileRequests).mockResolvedValue(mockRequests);

    const wrapper = createWrapper();
    const { result } = renderHook(() => useGetProfileRequests(), { wrapper });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(result.current.data?.[0].justification).toBe('Need access for operations');
    expect(inboxService.getProfileRequests).toHaveBeenCalledTimes(1);
  });

  it('useActivateUser calls inbox service with account id', async () => {
    vi.mocked(inboxService.activateUser).mockResolvedValue();

    const { result } = renderHook(() => useActivateUser());

    await act(async () => {
      result.current.mutate('user-1');
    });

    expect(inboxService.activateUser).toHaveBeenCalledWith('user-1');
  });

  it('useDenyUserSignup forwards reason to inbox service', async () => {
    vi.mocked(inboxService.denyUserSignup).mockResolvedValue();

    const { result } = renderHook(() => useDenyUserSignup());

    await act(async () => {
      result.current.mutate({ id: 'user-1', reason: 'Incomplete data' });
    });

    expect(inboxService.denyUserSignup).toHaveBeenCalledWith('user-1', 'Incomplete data');
  });

  it('useApproveProfileRequest forwards role and reason', async () => {
    vi.mocked(inboxService.approveProfileRequest).mockResolvedValue();

    const { result } = renderHook(() => useApproveProfileRequest());

    await act(async () => {
      result.current.mutate({ id: 'req-1', roleId: 'role-1', reason: 'Approved by manager' });
    });

    expect(inboxService.approveProfileRequest).toHaveBeenCalledWith(
      'req-1',
      'role-1',
      'Approved by manager'
    );
  });

  it('useRejectProfileRequest forwards reason to inbox service', async () => {
    vi.mocked(inboxService.rejectProfileRequest).mockResolvedValue();

    const { result } = renderHook(() => useRejectProfileRequest());

    await act(async () => {
      result.current.mutate({ id: 'req-1', reason: 'Not required' });
    });

    expect(inboxService.rejectProfileRequest).toHaveBeenCalledWith('req-1', 'Not required');
  });
});
