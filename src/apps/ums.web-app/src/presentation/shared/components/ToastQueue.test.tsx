import { act, fireEvent, render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it } from 'vitest';
import { useNotificationStore } from '@app/stores/notification.store';
import { ToastQueue } from './ToastQueue';

describe('ToastQueue', () => {
  beforeEach(() => {
    useNotificationStore.setState({ notifications: [], isOpen: false });
  });

  it('removes a notification from the store when the close button is clicked', async () => {
    render(<ToastQueue />);

    act(() => {
      useNotificationStore.getState().addNotification({
        title: 'Error al Registrar Módulo',
        message: 'La descripción excede el máximo permitido.',
        type: 'error',
      });
    });

    expect(await screen.findByText('Error al Registrar Módulo')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Cerrar notificación' }));

    expect(useNotificationStore.getState().notifications).toEqual([]);
  });
});
