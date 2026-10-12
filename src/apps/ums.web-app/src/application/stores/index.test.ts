import { describe, it, expect } from 'vitest';
import * as stores from '@app/stores';

describe('stores index', () => {
  it('exports useAuthStore', () => {
    expect(stores.useAuthStore).toBeDefined();
    expect(typeof stores.useAuthStore).toBe('function');
  });

  it('exports useThemeStore', () => {
    expect(stores.useThemeStore).toBeDefined();
    expect(typeof stores.useThemeStore).toBe('function');
  });

  it('exports useNotificationStore', () => {
    expect(stores.useNotificationStore).toBeDefined();
    expect(typeof stores.useNotificationStore).toBe('function');
  });

  it('exports useDevToolsStore', () => {
    expect(stores.useDevToolsStore).toBeDefined();
    expect(typeof stores.useDevToolsStore).toBe('function');
  });

  it('exports useI18nStore', () => {
    expect(stores.useI18nStore).toBeDefined();
    expect(typeof stores.useI18nStore).toBe('function');
  });
});
