import { describe, it, expect, beforeEach, vi } from 'vitest';
import { useNotificationStore } from './notification.store';

vi.stubGlobal('crypto', {
  randomUUID: () => 'test-uuid-123',
});

describe('notification.store', () => {
  beforeEach(() => {
    useNotificationStore.setState({
      notifications: [],
      isOpen: false,
    });
  });

  it('initializes with empty notifications', () => {
    expect(useNotificationStore.getState().notifications).toEqual([]);
    expect(useNotificationStore.getState().isOpen).toBe(false);
  });

  it('adds a notification with generated id and timestamp', () => {
    useNotificationStore.getState().addNotification({
      title: 'Test',
      message: 'Test message',
      type: 'info',
    });

    const state = useNotificationStore.getState();
    expect(state.notifications).toHaveLength(1);
    expect(state.notifications[0].id).toBe('test-uuid-123');
    expect(state.notifications[0].title).toBe('Test');
    expect(state.notifications[0].read).toBe(false);
    expect(state.notifications[0].type).toBe('info');
  });

  it('marks a notification as read', () => {
    useNotificationStore.getState().addNotification({
      title: 'Test',
      message: 'Test message',
      type: 'success',
    });

    useNotificationStore.getState().markAsRead('test-uuid-123');
    expect(useNotificationStore.getState().notifications[0].read).toBe(true);
  });

  it('removes a notification when it is dismissed', () => {
    useNotificationStore.getState().addNotification({
      title: 'Dismiss',
      message: 'Dismiss this message',
      type: 'error',
    });

    useNotificationStore.getState().removeNotification('test-uuid-123');

    expect(useNotificationStore.getState().notifications).toEqual([]);
  });

  it('marks all notifications as read', () => {
    useNotificationStore.getState().addNotification({
      title: 'First',
      message: 'First message',
      type: 'info',
    });
    useNotificationStore.getState().addNotification({
      title: 'Second',
      message: 'Second message',
      type: 'warning',
    });

    useNotificationStore.getState().markAllAsRead();

    const state = useNotificationStore.getState();
    expect(state.notifications.every(n => n.read)).toBe(true);
  });

  it('clears all notifications', () => {
    useNotificationStore.getState().addNotification({
      title: 'Test',
      message: 'Test message',
      type: 'error',
    });

    useNotificationStore.getState().clearAll();
    expect(useNotificationStore.getState().notifications).toEqual([]);
  });

  it('toggles drawer open state', () => {
    useNotificationStore.getState().setIsOpen(true);
    expect(useNotificationStore.getState().isOpen).toBe(true);

    useNotificationStore.getState().setIsOpen(false);
    expect(useNotificationStore.getState().isOpen).toBe(false);
  });

  it('caps notifications at 50', () => {
    for (let i = 0; i < 55; i++) {
      useNotificationStore.getState().addNotification({
        title: `Notif ${i}`,
        message: `Message ${i}`,
        type: 'info',
      });
    }

    expect(useNotificationStore.getState().notifications).toHaveLength(50);
  });

  it('prepends new notifications to the list', () => {
    useNotificationStore.getState().addNotification({
      title: 'First',
      message: 'First message',
      type: 'info',
    });
    useNotificationStore.getState().addNotification({
      title: 'Second',
      message: 'Second message',
      type: 'success',
    });

    expect(useNotificationStore.getState().notifications[0].title).toBe('Second');
    expect(useNotificationStore.getState().notifications[1].title).toBe('First');
  });
});
