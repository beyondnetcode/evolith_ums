/**
 * notification-theme.tsx — Shared visual config for notification types.
 *
 * L-4: Stores component types instead of pre-rendered elements so they
 * always reflect the latest lucide-react implementation.
 */
import { Info, CheckCircle, AlertTriangle, XCircle } from 'lucide-react';

export type NotificationType = 'info' | 'success' | 'warning' | 'error';

export const NOTIFICATION_ICONS: Record<NotificationType, typeof Info> = {
  info: Info,
  success: CheckCircle,
  warning: AlertTriangle,
  error: XCircle,
};

export const NOTIFICATION_BORDERS: Record<NotificationType, string> = {
  info: 'border-sky-500/20 bg-sky-500/5',
  success: 'border-emerald-500/20 bg-emerald-500/5',
  warning: 'border-amber-500/20 bg-amber-500/5',
  error: 'border-rose-500/20 bg-rose-500/5',
};

export const NOTIFICATION_ICON_CLASSES: Record<NotificationType, string> = {
  info: 'text-sky-500',
  success: 'text-emerald-500',
  warning: 'text-amber-500',
  error: 'text-rose-500',
};
