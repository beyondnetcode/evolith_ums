import React from 'react';
import { useNotificationStore } from '@app/stores/notification.store';
import { useI18n } from '@app/i18n/use-i18n';
import { Trash2, CheckCheck, Info } from 'lucide-react';
import { M3Drawer } from './M3Drawer';
import { NOTIFICATION_ICONS, NOTIFICATION_ICON_CLASSES } from '../theme/notification-theme';
import type { NotificationType } from '../theme/notification-theme';

export const NotificationCenter: React.FC = React.memo(() => {
  const { notifications, isOpen, setIsOpen, clearAll, markAllAsRead, markAsRead } =
    useNotificationStore();
  const t = useI18n();

  const unreadCount = notifications.filter(n => !n.read).length;
  const latestNotification = notifications[0];

  return (
    <>
      <div aria-live="polite" aria-atomic="true" className="sr-only">
        {latestNotification ? latestNotification.title + ': ' + latestNotification.message : ''}
      </div>

      <M3Drawer
        open={isOpen}
        onClose={() => setIsOpen(false)}
        title={t.notifDrawerTitle}
        subtitle={unreadCount > 0 ? t.notifDrawerUnread(unreadCount) : undefined}
        actions={
          <>
            <button
              onClick={markAllAsRead}
              disabled={unreadCount === 0}
              className="inline-flex items-center gap-1.5 text-m3-primary hover:text-m3-primary/80 font-bold uppercase tracking-wider text-[10px] disabled:opacity-40"
            >
              <CheckCheck className="w-3.5 h-3.5" /> {t.notifReadAll}
            </button>
            <span className="text-m3-outline">|</span>
            <button
              onClick={clearAll}
              disabled={notifications.length === 0}
              className="inline-flex items-center gap-1.5 text-m3-error hover:text-m3-error/80 font-bold uppercase tracking-wider text-[10px] disabled:opacity-40"
            >
              <Trash2 className="w-3.5 h-3.5" /> {t.notifClearAll}
            </button>
          </>
        }
      >
        {notifications.length === 0 ? (
          <div className="h-64 flex flex-col items-center justify-center text-center text-xs text-m3-secondary/70 gap-2">
            <Info className="w-6 h-6 text-m3-outline" />
            {t.notifEmpty}
          </div>
        ) : (
          notifications.map(n => (
            <div
              key={n.id}
              onClick={() => markAsRead(n.id)}
              className="p-4 rounded-2xl border transition-all cursor-pointer hover:border-m3-primary/30 relative overflow-hidden flex gap-3"
            >
              {!n.read && (
                <span className="absolute top-3 right-3 w-2 h-2 rounded-full bg-m3-primary animate-pulse" />
              )}
              <div className="flex-shrink-0 mt-0.5">
                {React.createElement(NOTIFICATION_ICONS[n.type as NotificationType], {
                  className: 'w-4 h-4 ' + NOTIFICATION_ICON_CLASSES[n.type as NotificationType],
                })}
              </div>
              <div className="flex-1">
                <h4 className="text-xs font-bold text-m3-on-surface">{n.title}</h4>
                <p className="text-[11px] text-m3-secondary mt-1 leading-relaxed">{n.message}</p>
                <span className="block text-[9px] text-m3-secondary/50 font-bold tracking-wider uppercase mt-2.5">
                  {new Date(n.timestamp).toLocaleTimeString()}
                </span>
              </div>
            </div>
          ))
        )}
      </M3Drawer>
    </>
  );
});
