import { create } from 'zustand';
import { randomUuid } from '@app/utils/security';

export interface AppNotification {
  id: string;
  title: string;
  message: string;
  type: 'info' | 'success' | 'warning' | 'error';
  timestamp: string;
  read: boolean;
}

interface NotificationState {
  notifications: AppNotification[];
  isOpen: boolean;
  addNotification: (notification: Omit<AppNotification, 'id' | 'timestamp' | 'read'>) => void;
  removeNotification: (id: string) => void;
  markAsRead: (id: string) => void;
  markAllAsRead: () => void;
  clearAll: () => void;
  setIsOpen: (isOpen: boolean) => void;
}

export const useNotificationStore = create<NotificationState>(set => ({
  notifications: [],
  isOpen: false,
  addNotification: n =>
    set(state => {
      const newN: AppNotification = {
        ...n,
        id: randomUuid(),
        timestamp: new Date().toISOString(),
        read: false,
      };
      return {
        notifications: [newN, ...state.notifications].slice(0, 50), // Cap at 50 logs
      };
    }),
  removeNotification: id =>
    set(state => ({
      notifications: state.notifications.filter(notification => notification.id !== id),
    })),
  markAsRead: id =>
    set(state => ({
      notifications: state.notifications.map(n => (n.id === id ? { ...n, read: true } : n)),
    })),
  markAllAsRead: () =>
    set(state => ({
      notifications: state.notifications.map(n => ({ ...n, read: true })),
    })),
  clearAll: () => set({ notifications: [] }),
  setIsOpen: isOpen => set({ isOpen }),
}));
