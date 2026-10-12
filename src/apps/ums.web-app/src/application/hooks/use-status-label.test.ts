import { describe, expect, it, beforeEach } from 'vitest';
import { renderHook } from '@testing-library/react';
import { useStatusLabel } from './use-status-label';
import { useI18nStore } from '@app/stores/i18n.store';

describe('useStatusLabel', () => {
  beforeEach(() => {
    useI18nStore.getState().setLanguage('en');
  });

  it('returns translated label for Active status', () => {
    const { result } = renderHook(() => useStatusLabel());
    const getStatusLabel = result.current;

    expect(getStatusLabel('Active')).toBe('Active');
  });

  it('returns translated label for Suspended status', () => {
    const { result } = renderHook(() => useStatusLabel());
    const getStatusLabel = result.current;

    expect(getStatusLabel('Suspended')).toBe('Suspended');
  });

  it('returns translated label for Pending status', () => {
    const { result } = renderHook(() => useStatusLabel());
    const getStatusLabel = result.current;

    expect(getStatusLabel('Pending')).toBe('Pending');
  });

  it('returns the raw value for unmapped status (G-153: no lo disfraza de «Pendiente»)', () => {
    const { result } = renderHook(() => useStatusLabel());
    const getStatusLabel = result.current;

    // Un estado sin clave i18n (p. ej. Draft/PendingApproval) se muestra crudo, no como «Pendiente».
    expect(getStatusLabel('Unknown')).toBe('Unknown');
    expect(getStatusLabel('PendingApproval')).toBe('PendingApproval');
  });

  it('returns spanish labels when language is es', () => {
    useI18nStore.getState().setLanguage('es');

    const { result } = renderHook(() => useStatusLabel());
    const getStatusLabel = result.current;

    expect(getStatusLabel('Active')).toBe('Activo');
    expect(getStatusLabel('Suspended')).toBe('Suspendido');
    expect(getStatusLabel('Pending')).toBe('Pendiente');
  });
});
