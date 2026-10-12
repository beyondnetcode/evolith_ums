/**
 * ToastQueue — Auto-dismissing floating toasts for every AppNotification.
 *
 * Subscribes to the notification store and surfaces each new entry as a
 * temporary toast at the bottom-right of the viewport. The toast disappears
 * automatically (error: 6 s, others: 4 s) or on manual dismiss. At most
 * MAX_VISIBLE toasts are shown at once; extras are dropped silently (they
 * remain accessible in the NotificationCenter drawer).
 *
 * Mount once, alongside <NotificationCenter />, inside <TopAppBar />.
 */
import React, { useCallback, useEffect, useRef, useState } from 'react';
import { X } from 'lucide-react';
import { useNotificationStore, type AppNotification } from '@app/stores/notification.store';
import {
  NOTIFICATION_ICONS,
  NOTIFICATION_ICON_CLASSES,
  NOTIFICATION_BORDERS,
  type NotificationType,
} from '../theme/notification-theme';

// ─── Constants ───────────────────────────────────────────────────────────────

const DISMISS_DELAY_MS: Record<NotificationType, number> = {
  error: 6_000,
  warning: 5_000,
  info: 4_000,
  success: 3_500,
};

const LEAVE_ANIMATION_MS = 300;
const MAX_VISIBLE = 4;

// ─── Types ────────────────────────────────────────────────────────────────────

type ToastEntry = AppNotification & { leaving: boolean };

// ─── Sub-component ────────────────────────────────────────────────────────────

interface ToastItemProps {
  toast: ToastEntry;
  onDismiss: (id: string) => void;
}

const ToastItem: React.FC<ToastItemProps> = React.memo(({ toast, onDismiss }) => {
  const Icon = NOTIFICATION_ICONS[toast.type as NotificationType];
  const iconClass = NOTIFICATION_ICON_CLASSES[toast.type as NotificationType];
  const borderClass = NOTIFICATION_BORDERS[toast.type as NotificationType];

  return (
    <div
      role="alert"
      data-testid="toast"
      data-toast-type={toast.type}
      aria-live={toast.type === 'error' ? 'assertive' : 'polite'}
      className={[
        'pointer-events-auto flex items-start gap-3 p-3.5 rounded-2xl border',
        'shadow-xl backdrop-blur-md bg-m3-surface/95',
        'transition-all duration-300 ease-in-out will-change-transform',
        borderClass,
        toast.leaving ? 'opacity-0 translate-x-4 scale-95' : 'opacity-100 translate-x-0 scale-100',
      ].join(' ')}
    >
      <Icon className={`w-4 h-4 mt-0.5 flex-shrink-0 ${iconClass}`} />

      <div className="flex-1 min-w-0">
        <p className="text-xs font-bold text-m3-on-surface leading-snug">{toast.title}</p>
        {toast.message && (
          <p className="text-[11px] text-m3-secondary mt-1 leading-relaxed break-words whitespace-pre-wrap">
            {toast.message}
          </p>
        )}
      </div>

      <button
        type="button"
        onClick={() => onDismiss(toast.id)}
        aria-label="Cerrar notificación"
        className="flex-shrink-0 p-1 -mr-0.5 -mt-0.5 rounded-full hover:bg-m3-surface-variant transition-colors text-m3-secondary hover:text-m3-on-surface"
      >
        <X className="w-3 h-3" />
      </button>
    </div>
  );
});

ToastItem.displayName = 'ToastItem';

// ─── Main component ───────────────────────────────────────────────────────────

export const ToastQueue: React.FC = React.memo(() => {
  const notifications = useNotificationStore(s => s.notifications);
  const removeNotification = useNotificationStore(s => s.removeNotification);
  const [toasts, setToasts] = useState<ToastEntry[]>([]);

  // Track which notification IDs have already been shown so re-renders
  // of the store (e.g. markAsRead) don't re-add existing toasts.
  const seenRef = useRef(new Set<string>());
  // Map of notificationId → setTimeout handle for cleanup on unmount.
  const timersRef = useRef(new Map<string, ReturnType<typeof setTimeout>>());

  // Cleanup all timers when the component unmounts.
  useEffect(() => {
    const timers = timersRef.current;
    return () => {
      timers.forEach(t => clearTimeout(t));
    };
  }, []);

  // Detect new notifications by comparing against the seen-set.
  useEffect(() => {
    for (const n of notifications) {
      if (seenRef.current.has(n.id)) continue;
      seenRef.current.add(n.id);

      setToasts(prev => {
        // Cap visible toasts — oldest entries are removed first (they are at
        // the tail of the array because we prepend).
        const next = [{ ...n, leaving: false }, ...prev];
        return next.length > MAX_VISIBLE ? next.slice(0, MAX_VISIBLE) : next;
      });

      const delay = DISMISS_DELAY_MS[n.type as NotificationType] ?? 4_000;

      const leavingTimer = setTimeout(() => {
        // Trigger slide-out animation.
        setToasts(prev => prev.map(t => (t.id === n.id ? { ...t, leaving: true } : t)));
        // Remove from DOM after animation completes.
        const removeTimer = setTimeout(() => {
          setToasts(prev => prev.filter(t => t.id !== n.id));
          timersRef.current.delete(n.id + '_remove');
        }, LEAVE_ANIMATION_MS);

        timersRef.current.set(n.id + '_remove', removeTimer);
        timersRef.current.delete(n.id);
      }, delay);

      timersRef.current.set(n.id, leavingTimer);
    }
  }, [notifications]);

  const dismiss = useCallback(
    (id: string) => {
      // Cancel the auto-dismiss timer so it doesn't double-fire.
      const timer = timersRef.current.get(id);
      if (timer !== undefined) {
        clearTimeout(timer);
        timersRef.current.delete(id);
      }

      removeNotification(id);
      setToasts(prev => prev.map(t => (t.id === id ? { ...t, leaving: true } : t)));
      const removeTimer = setTimeout(() => {
        setToasts(prev => prev.filter(t => t.id !== id));
        timersRef.current.delete(id + '_remove');
      }, LEAVE_ANIMATION_MS);
      timersRef.current.set(id + '_remove', removeTimer);
    },
    [removeNotification]
  );

  if (toasts.length === 0) return null;

  return (
    <div
      aria-label="Notificaciones"
      className="fixed bottom-6 right-6 z-[100] flex flex-col-reverse gap-2.5 w-80 pointer-events-none"
    >
      {toasts.map(toast => (
        <ToastItem key={toast.id} toast={toast} onDismiss={dismiss} />
      ))}
    </div>
  );
});

ToastQueue.displayName = 'ToastQueue';
