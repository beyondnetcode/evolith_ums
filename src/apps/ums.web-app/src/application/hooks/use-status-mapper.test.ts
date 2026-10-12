import { describe, expect, it, beforeEach } from 'vitest';
import { renderHook } from '@testing-library/react';
import { useStatusMapper } from './use-status-mapper';
import { useI18nStore } from '@app/stores/i18n.store';

describe('useStatusMapper', () => {
  beforeEach(() => {
    useI18nStore.getState().setLanguage('en');
  });

  it('returns label and colors for known status', () => {
    const config = {
      Active: {
        labelKey: 'active',
        colors: {
          bg: 'bg-emerald-500/10',
          border: 'border-emerald-500/20',
          text: 'text-emerald-700',
        },
      },
    };

    const { result } = renderHook(() => useStatusMapper(config));
    const mapper = result.current;
    const statusResult = mapper('Active');

    expect(statusResult.label).toBe('Active');
    expect(statusResult.colors.bg).toBe('bg-emerald-500/10');
  });

  it('returns raw status for unknown status', () => {
    const config = {
      Active: {
        labelKey: 'active',
        colors: {
          bg: 'bg-emerald-500/10',
          border: 'border-emerald-500/20',
          text: 'text-emerald-700',
        },
      },
    };

    const { result } = renderHook(() => useStatusMapper(config));
    const mapper = result.current;
    const statusResult = mapper('Unknown');

    expect(statusResult.label).toBe('Unknown');
    expect(statusResult.colors.bg).toBe('bg-m3-outline/10');
  });

  it('returns default colors for unknown status', () => {
    const config = {};

    const { result } = renderHook(() => useStatusMapper(config));
    const mapper = result.current;
    const statusResult = mapper('AnyStatus');

    expect(statusResult.colors).toEqual({
      bg: 'bg-m3-outline/10',
      border: 'border-m3-outline/20',
      text: 'text-m3-secondary',
    });
  });

  it('returns spanish label when language is es', () => {
    useI18nStore.getState().setLanguage('es');

    const config = {
      Active: {
        labelKey: 'active',
        colors: {
          bg: 'bg-emerald-500/10',
          border: 'border-emerald-500/20',
          text: 'text-emerald-700',
        },
      },
    };

    const { result } = renderHook(() => useStatusMapper(config));
    const mapper = result.current;
    const statusResult = mapper('Active');

    expect(statusResult.label).toBe('Activo');
  });
});
