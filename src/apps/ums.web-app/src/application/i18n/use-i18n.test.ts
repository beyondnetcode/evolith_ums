import { describe, expect, it, beforeEach } from 'vitest';
import { renderHook } from '@testing-library/react';
import { useI18n } from './use-i18n';
import { useI18nStore } from '@app/stores/i18n.store';

describe('useI18n', () => {
  beforeEach(() => {
    useI18nStore.getState().setLanguage('en');
  });

  it('returns english translations by default', () => {
    const { result } = renderHook(() => useI18n());
    const t = result.current;

    expect(t.appName).toBe('UMS ENTERPRISE');
  });

  it('returns spanish translations when language is es', () => {
    useI18nStore.getState().setLanguage('es');

    const { result } = renderHook(() => useI18n());
    const t = result.current;

    expect(t.appName).toBe('UMS EMPRESARIAL');
  });

  it('returns active translation', () => {
    const { result } = renderHook(() => useI18n());
    const t = result.current;

    expect(t.active).toBe('Active');
  });

  it('returns suspended translation', () => {
    const { result } = renderHook(() => useI18n());
    const t = result.current;

    expect(t.suspended).toBe('Suspended');
  });

  it('returns logoutBtn translation', () => {
    const { result } = renderHook(() => useI18n());
    const t = result.current;

    expect(t.logoutBtn).toBe('Log out session');
  });

  it('returns searchPlaceholder translation', () => {
    const { result } = renderHook(() => useI18n());
    const t = result.current;

    expect(t.searchPlaceholder).toBe('Enter search parameter...');
  });

  it('returns saveBtn translation', () => {
    const { result } = renderHook(() => useI18n());
    const t = result.current;

    expect(t.saveBtn).toBe('Save');
  });

  it('returns cancelEdit translation', () => {
    const { result } = renderHook(() => useI18n());
    const t = result.current;

    expect(t.cancelEdit).toBe('Cancel');
  });

  it('returns newBtn translation', () => {
    const { result } = renderHook(() => useI18n());
    const t = result.current;

    expect(t.newBtn).toBe('New');
  });

  it('returns tenant translation', () => {
    const { result } = renderHook(() => useI18n());
    const t = result.current;

    expect(t.tenant).toBe('Tenant');
  });

  it('returns authorizationContext translation', () => {
    const { result } = renderHook(() => useI18n());
    const t = result.current;

    expect(t.authorizationContext).toBe('Authorization');
  });

  it('returns featureFlags translation', () => {
    const { result } = renderHook(() => useI18n());
    const t = result.current;

    expect(t.featureFlags).toBe('Feature Flags');
  });
});
