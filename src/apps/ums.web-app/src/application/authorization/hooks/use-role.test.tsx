import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, waitFor, act } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import React from 'react';
import { useRolesBySystemSuite, useCreateRole, useUpdateRole, useSetRoleActive } from './use-role';
import roleService from '@infra/authorization/services/role.service';

vi.mock('@infra/authorization/services/role.service', () => ({
  roleService: {
    getBySystemSuite: vi.fn(),
    create: vi.fn(),
    update: vi.fn(),
    setActive: vi.fn(),
  },
  default: {
    getBySystemSuite: vi.fn(),
    create: vi.fn(),
    update: vi.fn(),
    setActive: vi.fn(),
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

describe('use-role hooks', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('useRolesBySystemSuite returns roles list', async () => {
    const mockRoles = [
      {
        roleId: 'role-1',
        systemSuiteId: 'suite-1',
        code: 'ADMIN',
        value: 'Administrator',
        description: 'Admin role',
        hierarchyLevel: 0,
        promotionOrder: 1,
        isActive: true,
      },
    ];

    vi.mocked(roleService.getBySystemSuite).mockResolvedValue(mockRoles);

    const wrapper = createWrapper();
    const { result } = renderHook(() => useRolesBySystemSuite('suite-1'), { wrapper });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(result.current.data?.[0].code).toBe('ADMIN');
    expect(roleService.getBySystemSuite).toHaveBeenCalledWith('suite-1');
  });

  it('useCreateRole calls service successfully', async () => {
    vi.mocked(roleService.create).mockResolvedValue({ roleId: 'new-role-id' });

    const wrapper = createWrapper();
    const { result } = renderHook(() => useCreateRole('suite-1'), { wrapper });

    await act(async () => {
      result.current.mutate({
        code: 'NEW_ROLE',
        value: 'New Role',
        description: 'Test',
        hierarchyLevel: 0,
        promotionOrder: 1,
      });
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(roleService.create).toHaveBeenCalledWith('suite-1', expect.any(Object));
  });

  it('useUpdateRole calls service successfully', async () => {
    vi.mocked(roleService.update).mockResolvedValue();

    const wrapper = createWrapper();
    const { result } = renderHook(() => useUpdateRole('suite-1', 'role-1'), { wrapper });

    await act(async () => {
      result.current.mutate({
        code: 'UPDATED',
        value: 'Updated',
        description: 'Test',
        hierarchyLevel: 0,
        promotionOrder: 1,
      });
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(roleService.update).toHaveBeenCalledWith('suite-1', 'role-1', expect.any(Object));
  });

  it('useSetRoleActive activates a role', async () => {
    vi.mocked(roleService.setActive).mockResolvedValue();

    const wrapper = createWrapper();
    const { result } = renderHook(() => useSetRoleActive('suite-1'), { wrapper });

    await act(async () => {
      result.current.mutate({ roleId: 'role-1', isActive: true });
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(roleService.setActive).toHaveBeenCalledWith('suite-1', 'role-1', true);
  });

  it('useSetRoleActive deactivates a role', async () => {
    vi.mocked(roleService.setActive).mockResolvedValue();

    const wrapper = createWrapper();
    const { result } = renderHook(() => useSetRoleActive('suite-1'), { wrapper });

    await act(async () => {
      result.current.mutate({ roleId: 'role-1', isActive: false });
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(roleService.setActive).toHaveBeenCalledWith('suite-1', 'role-1', false);
  });
});
