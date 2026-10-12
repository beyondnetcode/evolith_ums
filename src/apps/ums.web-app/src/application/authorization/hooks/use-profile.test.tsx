import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, waitFor, act } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import React from 'react';
import {
  useGetAllProfiles,
  useGetProfile,
  useCreateProfile,
  useAssignProfileTemplate,
  useActivateProfile,
  useDeactivateProfile,
} from './use-profile';
import profileService from '@infra/authorization/services/profile.service';

vi.mock('@infra/authorization/services/profile.service', () => ({
  profileService: {
    getAll: vi.fn(),
    getById: vi.fn(),
    create: vi.fn(),
    assignTemplate: vi.fn(),
    activate: vi.fn(),
    deactivate: vi.fn(),
  },
  default: {
    getAll: vi.fn(),
    getById: vi.fn(),
    create: vi.fn(),
    assignTemplate: vi.fn(),
    activate: vi.fn(),
    deactivate: vi.fn(),
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

describe('use-profile hooks', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('useGetAllProfiles returns paged list', async () => {
    const mockPage = {
      items: [
        {
          profileId: 'p1',
          code: 'P1',
          name: 'Profile 1',
          status: 'Active',
          createdAt: '2024-01-01',
        },
      ],
      page: 1,
      pageSize: 20,
      totalItems: 1,
      totalPages: 1,
    };

    vi.mocked(profileService.getAll).mockResolvedValue(mockPage);

    const wrapper = createWrapper();
    const { result } = renderHook(() => useGetAllProfiles({ page: 1, pageSize: 20 }), { wrapper });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(result.current.data?.items[0].name).toBe('Profile 1');
    expect(profileService.getAll).toHaveBeenCalledWith({ page: 1, pageSize: 20 });
  });

  it('useGetProfile returns profile by id', async () => {
    const mockProfile = {
      profileId: 'p1',
      code: 'P1',
      name: 'Profile 1',
      status: 'Active',
      createdAt: '2024-01-01',
    };

    vi.mocked(profileService.getById).mockResolvedValue(mockProfile);

    const wrapper = createWrapper();
    const { result } = renderHook(() => useGetProfile('p1'), { wrapper });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(result.current.data?.name).toBe('Profile 1');
    expect(profileService.getById).toHaveBeenCalledWith('p1');
  });

  it('useGetProfile returns null on 404', async () => {
    const error = new Error('Not Found');
    (error as any).response = { status: 404 };
    vi.mocked(profileService.getById).mockRejectedValue(error);

    const wrapper = createWrapper();
    const { result } = renderHook(() => useGetProfile('nonexistent'), { wrapper });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(result.current.data).toBeNull();
  });

  it('useCreateProfile calls service successfully', async () => {
    vi.mocked(profileService.create).mockResolvedValue({ profileId: 'new-profile-id' });

    const wrapper = createWrapper();
    const { result } = renderHook(() => useCreateProfile(), { wrapper });

    await act(async () => {
      result.current.mutate({ code: 'NEW', name: 'New Profile' });
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(profileService.create).toHaveBeenCalledWith({ code: 'NEW', name: 'New Profile' });
  });

  it('useAssignProfileTemplate calls service successfully', async () => {
    vi.mocked(profileService.assignTemplate).mockResolvedValue();

    const wrapper = createWrapper();
    const { result } = renderHook(() => useAssignProfileTemplate(), { wrapper });

    await act(async () => {
      result.current.mutate({ profileId: 'p1', templateId: 't1' });
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(profileService.assignTemplate).toHaveBeenCalledWith('p1', 't1');
  });

  it('useActivateProfile calls service successfully', async () => {
    vi.mocked(profileService.activate).mockResolvedValue();

    const wrapper = createWrapper();
    const { result } = renderHook(() => useActivateProfile(), { wrapper });

    await act(async () => {
      result.current.mutate('p1');
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(profileService.activate).toHaveBeenCalledWith('p1');
  });

  it('useDeactivateProfile calls service successfully', async () => {
    vi.mocked(profileService.deactivate).mockResolvedValue();

    const wrapper = createWrapper();
    const { result } = renderHook(() => useDeactivateProfile(), { wrapper });

    await act(async () => {
      result.current.mutate('p1');
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(profileService.deactivate).toHaveBeenCalledWith('p1');
  });
});
