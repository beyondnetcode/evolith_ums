import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook } from '@testing-library/react';
import { useDateFormat } from './use-date-format';

/** Ajustes que publica el sistema en el grafo (G-178). */
let settings: Record<string, Record<string, string>> = {};
/** Parámetros del inquilino que viajan en la sesión (ADR-0076). */
let sessionParameters: { defaultTimezone?: string } | undefined;

vi.mock('@app/stores/auth.store', () => ({
  useAuthStore: (selector: (s: unknown) => unknown) =>
    selector({ user: { authorizationGraph: { settings }, sessionParameters } }),
}));

vi.mock('@app/stores/i18n.store', () => ({
  useI18nStore: (selector: (s: unknown) => unknown) => selector({ language: 'es' }),
}));

describe('useDateFormat — precedencia sistema → inquilino', () => {
  beforeEach(() => {
    settings = {};
    sessionParameters = { defaultTimezone: 'America/Lima' };
  });

  it('la zona del sistema manda sobre la del inquilino', () => {
    settings = { locale: { timezone: 'Europe/Madrid' } };

    const { result } = renderHook(() => useDateFormat());
    expect(result.current.timezone).toBe('Europe/Madrid');
  });

  it('sin zona del sistema queda la del inquilino', () => {
    const { result } = renderHook(() => useDateFormat());
    expect(result.current.timezone).toBe('America/Lima');
  });

  it('sin ninguna de las dos, decide el navegador', () => {
    // `undefined` deja que Intl use la zona local: mejor eso que inventar una.
    sessionParameters = undefined;

    const { result } = renderHook(() => useDateFormat());
    expect(result.current.timezone).toBeUndefined();
  });

  it('formatea con el idioma completo que publica el sistema', () => {
    // `es-PE` da el orden día/mes; el idioma de la interfaz solo distingue es de en.
    settings = { locale: { language: 'es-PE', timezone: 'America/Lima' } };

    const { result } = renderHook(() => useDateFormat());
    expect(result.current.locale).toBe('es-PE');
    expect(
      result.current.formatDate('2026-08-01T05:30:00Z', {
        day: '2-digit',
        month: '2-digit',
        year: 'numeric',
      })
    ).toBe('01/08/2026');
  });

  it('la zona convierte la fecha, no solo la etiqueta', () => {
    // 2026-08-01T02:00Z es aún 31 de julio en Lima (UTC-5): si la conversión no ocurre,
    // el usuario ve un día que no corresponde.
    settings = { locale: { language: 'es-PE', timezone: 'America/Lima' } };

    const { result } = renderHook(() => useDateFormat());
    expect(
      result.current.formatDate('2026-08-01T02:00:00Z', {
        day: '2-digit',
        month: '2-digit',
        year: 'numeric',
      })
    ).toBe('31/07/2026');
  });

  it('sin ajustes del sistema formatea con el idioma de la interfaz', () => {
    const { result } = renderHook(() => useDateFormat());
    expect(result.current.locale).toBe('es');
  });
});
