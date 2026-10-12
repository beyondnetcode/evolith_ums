import { describe, it, expect } from 'vitest';
import {
  NOTIFICATION_ICONS,
  NOTIFICATION_BORDERS,
  NOTIFICATION_ICON_CLASSES,
} from '../theme/notification-theme';
import { Info, CheckCircle, AlertTriangle, XCircle } from 'lucide-react';

describe('notification-theme', () => {
  it('exports NOTIFICATION_ICONS with correct icons', () => {
    expect(NOTIFICATION_ICONS.info).toBe(Info);
    expect(NOTIFICATION_ICONS.success).toBe(CheckCircle);
    expect(NOTIFICATION_ICONS.warning).toBe(AlertTriangle);
    expect(NOTIFICATION_ICONS.error).toBe(XCircle);
  });

  it('exports NOTIFICATION_BORDERS with correct classes', () => {
    expect(NOTIFICATION_BORDERS.info).toContain('border-sky-500/20');
    expect(NOTIFICATION_BORDERS.success).toContain('border-emerald-500/20');
    expect(NOTIFICATION_BORDERS.warning).toContain('border-amber-500/20');
    expect(NOTIFICATION_BORDERS.error).toContain('border-rose-500/20');
  });

  it('exports NOTIFICATION_ICON_CLASSES with correct classes', () => {
    expect(NOTIFICATION_ICON_CLASSES.info).toBe('text-sky-500');
    expect(NOTIFICATION_ICON_CLASSES.success).toBe('text-emerald-500');
    expect(NOTIFICATION_ICON_CLASSES.warning).toBe('text-amber-500');
    expect(NOTIFICATION_ICON_CLASSES.error).toBe('text-rose-500');
  });

  it('has all four notification types', () => {
    const types = ['info', 'success', 'warning', 'error'];
    types.forEach(type => {
      expect(NOTIFICATION_ICONS).toHaveProperty(type);
      expect(NOTIFICATION_BORDERS).toHaveProperty(type);
      expect(NOTIFICATION_ICON_CLASSES).toHaveProperty(type);
    });
  });
});
