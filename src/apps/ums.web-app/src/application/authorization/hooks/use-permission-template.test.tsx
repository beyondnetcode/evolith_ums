import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, waitFor, act } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import React from 'react';
import {
  useGetAllPermissionTemplates,
  useGetPermissionTemplate,
  useCreatePermissionTemplate,
  usePublishPermissionTemplate,
  useDeprecatePermissionTemplate,
  useAddTemplateItem,
  useRemoveTemplateItem,
  useApplyTemplateItemEffect,
} from './use-permission-template';
import permissionTemplateService from '@infra/authorization/services/permission-template.service';
import type { PermissionTemplateItem } from '@domain/authorization/models/permission-template.model';

vi.mock('@infra/authorization/services/permission-template.service', () => ({
  permissionTemplateService: {
    getAll: vi.fn(),
    getById: vi.fn(),
    create: vi.fn(),
    publish: vi.fn(),
    deprecate: vi.fn(),
    addItem: vi.fn(),
    removeItem: vi.fn(),
    setItemEffect: vi.fn(),
    activateItem: vi.fn(),
  },
  default: {
    getAll: vi.fn(),
    getById: vi.fn(),
    create: vi.fn(),
    publish: vi.fn(),
    deprecate: vi.fn(),
    addItem: vi.fn(),
    removeItem: vi.fn(),
    setItemEffect: vi.fn(),
    activateItem: vi.fn(),
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

describe('use-permission-template hooks', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('useGetAllPermissionTemplates returns paged list', async () => {
    const mockPage = {
      items: [
        {
          templateId: 't1',
          code: 'T1',
          name: 'Template 1',
          status: 'Active',
          createdAt: '2024-01-01',
        },
      ],
      page: 1,
      pageSize: 20,
      totalItems: 1,
      totalPages: 1,
    };

    vi.mocked(permissionTemplateService.getAll).mockResolvedValue(mockPage);

    const wrapper = createWrapper();
    const { result } = renderHook(() => useGetAllPermissionTemplates({ page: 1, pageSize: 20 }), {
      wrapper,
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(result.current.data?.items[0].name).toBe('Template 1');
    expect(permissionTemplateService.getAll).toHaveBeenCalledWith({ page: 1, pageSize: 20 });
  });

  it('useGetPermissionTemplate returns template by id', async () => {
    const mockTemplate = {
      templateId: 't1',
      code: 'T1',
      name: 'Template 1',
      status: 'Active',
      createdAt: '2024-01-01',
      items: [],
    };

    vi.mocked(permissionTemplateService.getById).mockResolvedValue(mockTemplate);

    const wrapper = createWrapper();
    const { result } = renderHook(() => useGetPermissionTemplate('t1'), { wrapper });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(result.current.data?.name).toBe('Template 1');
    expect(permissionTemplateService.getById).toHaveBeenCalledWith('t1');
  });

  it('useGetPermissionTemplate returns null on 404', async () => {
    const error = new Error('Not Found');
    (error as any).response = { status: 404 };
    vi.mocked(permissionTemplateService.getById).mockRejectedValue(error);

    const wrapper = createWrapper();
    const { result } = renderHook(() => useGetPermissionTemplate('nonexistent'), { wrapper });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(result.current.data).toBeNull();
  });

  it('useCreatePermissionTemplate calls service successfully', async () => {
    vi.mocked(permissionTemplateService.create).mockResolvedValue({
      templateId: 'new-template-id',
    });

    const wrapper = createWrapper();
    const { result } = renderHook(() => useCreatePermissionTemplate(), { wrapper });

    await act(async () => {
      result.current.mutate({ code: 'NEW', name: 'New Template' });
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(permissionTemplateService.create).toHaveBeenCalledWith({
      code: 'NEW',
      name: 'New Template',
    });
  });

  it('usePublishPermissionTemplate calls service successfully', async () => {
    vi.mocked(permissionTemplateService.publish).mockResolvedValue();

    const wrapper = createWrapper();
    const { result } = renderHook(() => usePublishPermissionTemplate('t1'), { wrapper });

    await act(async () => {
      result.current.mutate();
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(permissionTemplateService.publish).toHaveBeenCalledWith('t1');
  });

  it('useDeprecatePermissionTemplate calls service successfully', async () => {
    vi.mocked(permissionTemplateService.deprecate).mockResolvedValue();

    const wrapper = createWrapper();
    const { result } = renderHook(() => useDeprecatePermissionTemplate('t1'), { wrapper });

    await act(async () => {
      result.current.mutate();
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(permissionTemplateService.deprecate).toHaveBeenCalledWith('t1');
  });

  it('useAddTemplateItem calls service successfully', async () => {
    vi.mocked(permissionTemplateService.addItem).mockResolvedValue();

    const wrapper = createWrapper();
    const { result } = renderHook(() => useAddTemplateItem('t1'), { wrapper });

    await act(async () => {
      result.current.mutate({ code: 'ITEM1', effect: 'Allow', sortOrder: 1 });
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(permissionTemplateService.addItem).toHaveBeenCalledWith('t1', expect.any(Object));
  });

  // El DELETE conserva ruta y contrato, pero desde ADR-0164 retira la fila en vez de borrarla.
  // El hook sigue existiendo para dar de baja la concesión entera; ya no para expresar «Neutral».
  it('useRemoveTemplateItem retira el ítem contra el mismo endpoint', async () => {
    vi.mocked(permissionTemplateService.removeItem).mockResolvedValue();

    const wrapper = createWrapper();
    const { result } = renderHook(() => useRemoveTemplateItem('t1'), { wrapper });

    await act(async () => {
      result.current.mutate('item1');
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(permissionTemplateService.removeItem).toHaveBeenCalledWith('t1', 'item1');
  });
});

// ─── ADR-0164: Neutral no borra, y lo retirado se reactiva ───────────────────

describe('useApplyTemplateItemEffect', () => {
  const target = {
    targetType: 'SystemSuite' as const,
    targetId: 'suite-1',
    actionId: 'action-1',
  };

  const item = (overrides: Partial<PermissionTemplateItem>): PermissionTemplateItem => ({
    itemId: 'item-1',
    targetType: 'SystemSuite',
    targetId: 'suite-1',
    targetName: 'Suite',
    actionId: 'action-1',
    actionName: 'Leer',
    isAllowed: false,
    isDenied: false,
    isActive: true,
    ...overrides,
  });

  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(permissionTemplateService.addItem).mockResolvedValue();
    vi.mocked(permissionTemplateService.removeItem).mockResolvedValue();
    vi.mocked(permissionTemplateService.setItemEffect).mockResolvedValue();
    vi.mocked(permissionTemplateService.activateItem).mockResolvedValue();
  });

  it('pone Neutral con el verbo de efecto y no borra el ítem', async () => {
    const wrapper = createWrapper();
    const { result } = renderHook(() => useApplyTemplateItemEffect('t1'), { wrapper });

    await act(async () => {
      await result.current.applyEffect({
        effect: 'Neutral',
        item: item({ isAllowed: true }),
        target,
      });
    });

    expect(permissionTemplateService.setItemEffect).toHaveBeenCalledWith('t1', 'item-1', 'Neutral');
    expect(permissionTemplateService.removeItem).not.toHaveBeenCalled();
  });

  it('reactiva el ítem retirado en vez de volver a darlo de alta (evita el 409)', async () => {
    const wrapper = createWrapper();
    const { result } = renderHook(() => useApplyTemplateItemEffect('t1'), { wrapper });

    await act(async () => {
      await result.current.applyEffect({
        effect: 'Allow',
        item: item({ isActive: false, isAllowed: true }),
        target,
      });
    });

    expect(permissionTemplateService.activateItem).toHaveBeenCalledWith('t1', 'item-1');
    expect(permissionTemplateService.addItem).not.toHaveBeenCalled();
    // El efecto guardado ya era Allow: reactivar basta, no hace falta reescribirlo.
    expect(permissionTemplateService.setItemEffect).not.toHaveBeenCalled();
  });

  it('al reactivar corrige el efecto si el guardado no es el pedido', async () => {
    const wrapper = createWrapper();
    const { result } = renderHook(() => useApplyTemplateItemEffect('t1'), { wrapper });

    await act(async () => {
      await result.current.applyEffect({
        effect: 'Deny',
        item: item({ isActive: false, isAllowed: true }),
        target,
      });
    });

    expect(permissionTemplateService.activateItem).toHaveBeenCalledWith('t1', 'item-1');
    expect(permissionTemplateService.setItemEffect).toHaveBeenCalledWith('t1', 'item-1', 'Deny');
  });

  it('no toca nada si se pide Neutral sobre un ítem ya retirado', async () => {
    const wrapper = createWrapper();
    const { result } = renderHook(() => useApplyTemplateItemEffect('t1'), { wrapper });

    let wrote = true;
    await act(async () => {
      wrote = await result.current.applyEffect({
        effect: 'Neutral',
        item: item({ isActive: false, isAllowed: true }),
        target,
      });
    });

    expect(wrote).toBe(false);
    expect(permissionTemplateService.setItemEffect).not.toHaveBeenCalled();
    expect(permissionTemplateService.activateItem).not.toHaveBeenCalled();
    expect(permissionTemplateService.removeItem).not.toHaveBeenCalled();
  });

  it('crea la fila cuando la casilla nunca tuvo ítem', async () => {
    const wrapper = createWrapper();
    const { result } = renderHook(() => useApplyTemplateItemEffect('t1'), { wrapper });

    await act(async () => {
      await result.current.applyEffect({ effect: 'Allow', item: undefined, target });
    });

    expect(permissionTemplateService.addItem).toHaveBeenCalledWith('t1', {
      ...target,
      isAllowed: true,
      isDenied: false,
    });
  });

  it('no crea fila para pedir Neutral sobre una casilla sin ítem', async () => {
    const wrapper = createWrapper();
    const { result } = renderHook(() => useApplyTemplateItemEffect('t1'), { wrapper });

    await act(async () => {
      await result.current.applyEffect({ effect: 'Neutral', item: undefined, target });
    });

    expect(permissionTemplateService.addItem).not.toHaveBeenCalled();
  });

  it('no reescribe el efecto que ya está vigente', async () => {
    const wrapper = createWrapper();
    const { result } = renderHook(() => useApplyTemplateItemEffect('t1'), { wrapper });

    let wrote = true;
    await act(async () => {
      wrote = await result.current.applyEffect({
        effect: 'Allow',
        item: item({ isAllowed: true }),
        target,
      });
    });

    expect(wrote).toBe(false);
    expect(permissionTemplateService.setItemEffect).not.toHaveBeenCalled();
  });
});
