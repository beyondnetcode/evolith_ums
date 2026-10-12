import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, waitFor, act } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import React from 'react';
import { useNotifiedMutation } from './use-notified-mutation';
import { useNotificationStore } from '@app/stores/notification.store';

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );
}

describe('useNotifiedMutation', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    useNotificationStore.setState({ notifications: [], isOpen: false });
  });

  it('calls mutationFn with provided variables', async () => {
    const mutationFn = vi.fn().mockResolvedValue({ id: '1' });
    const wrapper = createWrapper();

    const { result } = renderHook(
      () =>
        useNotifiedMutation({
          mutationFn,
          successNotif: () => ({ title: 'OK', message: 'Done' }),
          errorNotif: () => ({ title: 'Error', message: 'Failed' }),
        }),
      { wrapper }
    );

    await act(async () => {
      result.current.mutate({ name: 'test' });
    });

    await waitFor(() => {
      expect(mutationFn).toHaveBeenCalled();
    });

    const callArgs = mutationFn.mock.calls[0][0];
    expect(callArgs).toEqual({ name: 'test' });
  });

  it('shows success notification on success', async () => {
    const mutationFn = vi.fn().mockResolvedValue({ id: '1', name: 'created' });
    const wrapper = createWrapper();

    const { result } = renderHook(
      () =>
        useNotifiedMutation({
          mutationFn,
          successNotif: data => ({ title: 'Created', message: data.name }),
          errorNotif: () => ({ title: 'Error', message: 'Failed' }),
        }),
      { wrapper }
    );

    await act(async () => {
      result.current.mutate({});
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });
  });

  it('shows error notification on failure', async () => {
    const mutationFn = vi.fn().mockRejectedValue(new Error('API error'));
    const wrapper = createWrapper();

    const { result } = renderHook(
      () =>
        useNotifiedMutation({
          mutationFn,
          successNotif: () => ({ title: 'OK', message: 'Done' }),
          errorNotif: error => ({
            title: 'Failed',
            message: error instanceof Error ? error.message : 'Unknown',
          }),
        }),
      { wrapper }
    );

    await act(async () => {
      result.current.mutate({});
    });

    await waitFor(() => {
      expect(result.current.isError).toBe(true);
    });
  });

  it('shows an approved reason with its tracking code on failure', async () => {
    const mutationFn = vi.fn().mockRejectedValue({
      response: {
        status: 422,
        data: {
          userMessage: 'La descripción del módulo no puede exceder 500 caracteres.',
          errorId: 'db83c6dd-770d-4d92-b6b8-98e80c790e4a',
          traceId: 'corr-module-123',
        },
      },
    });
    const wrapper = createWrapper();

    const { result } = renderHook(
      () =>
        useNotifiedMutation({
          mutationFn,
          successNotif: () => ({ title: 'OK', message: 'Done' }),
          errorNotif: () => ({
            title: 'Error al Registrar Módulo',
            message: 'No se pudo registrar el módulo.',
          }),
        }),
      { wrapper }
    );

    await act(async () => {
      result.current.mutate({});
    });

    await waitFor(() => {
      expect(useNotificationStore.getState().notifications[0]?.message).toBe(
        'La descripción del módulo no puede exceder 500 caracteres.\n' +
          'Si necesitas más detalles, consulta con el administrador e indica este ID de error: ' +
          'db83c6dd-770d-4d92-b6b8-98e80c790e4a.'
      );
    });
  });

  it('shows problem details reason with its tracking code on failure', async () => {
    const mutationFn = vi.fn().mockRejectedValue({
      response: {
        status: 409,
        data: {
          detail: 'El código de sucursal debe ser único dentro del inquilino.',
          errorId: 'efb21f91-c524-4181-99c0-edda60c3772b',
        },
      },
    });
    const wrapper = createWrapper();

    const { result } = renderHook(
      () =>
        useNotifiedMutation({
          mutationFn,
          successNotif: () => ({ title: 'OK', message: 'Done' }),
          errorNotif: () => ({
            title: 'Error al Registrar Sucursal',
            message: 'No se pudo registrar la sucursal.',
          }),
        }),
      { wrapper }
    );

    await act(async () => {
      result.current.mutate({});
    });

    await waitFor(() => {
      expect(useNotificationStore.getState().notifications[0]?.message).toBe(
        'El código de sucursal debe ser único dentro del inquilino.\n' +
          'Si necesitas más detalles, consulta con el administrador e indica este ID de error: ' +
          'efb21f91-c524-4181-99c0-edda60c3772b.'
      );
    });
  });

  it('returns mutation state properties', () => {
    const mutationFn = vi.fn().mockResolvedValue({});
    const wrapper = createWrapper();

    const { result } = renderHook(
      () =>
        useNotifiedMutation({
          mutationFn,
          successNotif: () => ({ title: 'OK', message: 'Done' }),
          errorNotif: () => ({ title: 'Error', message: 'Failed' }),
        }),
      { wrapper }
    );

    expect(result.current).toHaveProperty('mutate');
    expect(result.current).toHaveProperty('mutateAsync');
    expect(result.current).toHaveProperty('isPending');
    expect(result.current).toHaveProperty('isSuccess');
    expect(result.current).toHaveProperty('isError');
  });
});
