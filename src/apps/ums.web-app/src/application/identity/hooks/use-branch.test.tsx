import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, waitFor, act } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import React from 'react';
import {
  useGetBranches,
  useGetBranchLifecycle,
  useAddBranch,
  useCloseBranch,
  useDeactivateBranch,
  useReactivateBranch,
  describeBlockingDependencies,
} from './use-branch';
import tenantService from '@infra/identity/services/tenant.service';

vi.mock('@infra/identity/services/tenant.service', () => ({
  tenantService: {
    getBranches: vi.fn(),
    getBranchLifecycle: vi.fn(),
    addBranch: vi.fn(),
    closeBranch: vi.fn(),
    deactivateBranch: vi.fn(),
    reactivateBranch: vi.fn(),
  },
  default: {
    getBranches: vi.fn(),
    getBranchLifecycle: vi.fn(),
    addBranch: vi.fn(),
    closeBranch: vi.fn(),
    deactivateBranch: vi.fn(),
    reactivateBranch: vi.fn(),
  },
}));

vi.mock('@app/i18n/use-i18n', () => ({
  useI18n: () => ({
    notifBranchAdded: 'Branch Added',
    notifBranchAddedMsg: (code: string) => `Branch ${code} added`,
    notifBranchAddFailed: 'Add Failed',
    notifBranchAddFailedMsg: 'Could not add branch',
    notifBranchClosed: 'Branch Closed',
    notifBranchClosedMsg: 'Branch closed permanently',
    notifBranchCloseFailed: 'Close Failed',
    notifBranchCloseFailedMsg: 'Could not close branch',
    notifBranchCloseBlocked: 'Cannot close',
    notifBranchCloseBlockedMsg: (detalle: string) => `Blocked by ${detalle}`,
    notifBranchDeactivated: 'Branch Deactivated',
    notifBranchDeactivatedMsg: 'Branch deactivated',
    notifBranchDeactivateFailed: 'Deactivate Failed',
    notifBranchDeactivateFailedMsg: 'Could not deactivate branch',
    notifBranchReactivated: 'Branch Reactivated',
    notifBranchReactivatedMsg: 'Branch reactivated',
    notifBranchReactivateFailed: 'Reactivate Failed',
    notifBranchReactivateFailedMsg: 'Could not reactivate branch',
  }),
}));

vi.mock('@app/hooks/use-notified-mutation', () => ({
  useNotifiedMutation: (config: any) => {
    const mutationFn = config.mutationFn;
    return {
      mutate: vi.fn((vars: any) => mutationFn(vars)),
      mutateAsync: vi.fn(async (vars: any) => mutationFn(vars)),
      isPending: false,
      isSuccess: true,
      isError: false,
      data: { branchId: 'new-branch' },
    };
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

describe('use-branch hooks', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('useGetBranches returns branches for tenant', async () => {
    const mockBranches = [
      {
        branchId: 'b1',
        code: 'B1',
        name: 'Branch 1',
        isActive: true,
        geofencingMetadata: null,
        isClosed: false,
        closedAtUtc: null,
      },
    ];
    vi.mocked(tenantService.getBranches).mockResolvedValue(mockBranches);

    const wrapper = createWrapper();
    const { result } = renderHook(() => useGetBranches('t1'), { wrapper });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(result.current.data?.[0].name).toBe('Branch 1');
    // Por defecto NO se piden las cerradas (ADR-0164).
    expect(tenantService.getBranches).toHaveBeenCalledWith('t1', false);
  });

  it('useGetBranches pide las cerradas cuando se le indica', async () => {
    vi.mocked(tenantService.getBranches).mockResolvedValue([]);

    const wrapper = createWrapper();
    const { result } = renderHook(() => useGetBranches('t1', true), { wrapper });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(tenantService.getBranches).toHaveBeenCalledWith('t1', true);
  });

  it('useGetBranches returns empty array on 404', async () => {
    const error = new Error('Not Found');
    (error as any).response = { status: 404 };
    vi.mocked(tenantService.getBranches).mockRejectedValue(error);

    const wrapper = createWrapper();
    const { result } = renderHook(() => useGetBranches('t1'), { wrapper });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(result.current.data).toEqual([]);
  });

  it('useGetBranches is disabled when tenantId is null', () => {
    const wrapper = createWrapper();
    const { result } = renderHook(() => useGetBranches(null), { wrapper });

    expect(result.current.isFetching).toBe(false);
    expect(result.current.data).toBeUndefined();
  });

  it('useAddBranch returns mutation object', () => {
    const wrapper = createWrapper();
    const { result } = renderHook(() => useAddBranch('t1'), { wrapper });

    expect(result.current.mutate).toBeDefined();
    expect(typeof result.current.mutate).toBe('function');
  });

  it('useCloseBranch cierra la sucursal contra el servicio de cierre, con su motivo', async () => {
    vi.mocked(tenantService.closeBranch).mockResolvedValue();

    const wrapper = createWrapper();
    const { result } = renderHook(() => useCloseBranch('t1'), { wrapper });

    await act(async () => {
      await result.current.mutateAsync({ branchId: 'b1', reason: 'Cese de operaciones' });
    });

    expect(tenantService.closeBranch).toHaveBeenCalledWith('t1', 'b1', 'Cese de operaciones');
  });

  it('useGetBranchLifecycle no consulta sin sucursal seleccionada', () => {
    const wrapper = createWrapper();
    const { result } = renderHook(() => useGetBranchLifecycle('t1', null), { wrapper });

    expect(result.current.isFetching).toBe(false);
    expect(tenantService.getBranchLifecycle).not.toHaveBeenCalled();
  });

  it('useGetBranchLifecycle trae los episodios de la sucursal', async () => {
    vi.mocked(tenantService.getBranchLifecycle).mockResolvedValue([
      {
        entryId: 'e1',
        episode: 'Closed',
        occurredAtUtc: '2026-07-31T15:04:05Z',
        actorId: 'admin@evolith.example',
        nameSnapshot: 'Sucursal Callao',
        geofencingSnapshot: null,
        reason: null,
      },
    ]);

    const wrapper = createWrapper();
    const { result } = renderHook(() => useGetBranchLifecycle('t1', 'b1'), { wrapper });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(result.current.data?.[0].episode).toBe('Closed');
    expect(tenantService.getBranchLifecycle).toHaveBeenCalledWith('t1', 'b1');
  });

  it('useDeactivateBranch returns mutation object', () => {
    const wrapper = createWrapper();
    const { result } = renderHook(() => useDeactivateBranch('t1'), { wrapper });

    expect(result.current.mutate).toBeDefined();
    expect(typeof result.current.mutate).toBe('function');
  });

  it('useReactivateBranch returns mutation object', () => {
    const wrapper = createWrapper();
    const { result } = renderHook(() => useReactivateBranch('t1'), { wrapper });

    expect(result.current.mutate).toBeDefined();
    expect(typeof result.current.mutate).toBe('function');
  });
});

// El 409 del cierre trae las dos clases desglosadas; un «no se pudo» a secas obliga a adivinar.
describe('describeBlockingDependencies', () => {
  it('nombra una sola clase en singular', () => {
    expect(describeBlockingDependencies([{ entityType: 'Profile', count: 1 }])).toBe(
      '1 perfil activo'
    );
  });

  it('pluraliza y enumera las dos clases', () => {
    expect(
      describeBlockingDependencies([
        { entityType: 'UserAccount', count: 3 },
        { entityType: 'Profile', count: 2 },
      ])
    ).toBe('3 cuentas de usuario activas y 2 perfiles activos');
  });

  it('no oculta una clase desconocida: la muestra tal cual', () => {
    expect(describeBlockingDependencies([{ entityType: 'Delegation', count: 4 }])).toBe(
      '4 × Delegation'
    );
  });
});
