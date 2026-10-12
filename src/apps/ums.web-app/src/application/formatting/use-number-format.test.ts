import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook } from '@testing-library/react';
import { useNumberFormat } from './use-number-format';

/** Ajustes que publica el sistema en el grafo (G-178). */
let settings: Record<string, Record<string, string>> = {};

vi.mock('@app/stores/auth.store', () => ({
  useAuthStore: (selector: (s: unknown) => unknown) =>
    selector({ user: { authorizationGraph: { settings } } }),
}));

vi.mock('@app/stores/i18n.store', () => ({
  useI18nStore: (selector: (s: unknown) => unknown) => selector({ language: 'es' }),
}));

/** Intl separa el símbolo con un espacio duro; compararlo a ciegas rompe por un carácter invisible. */
const normalizar = (s: string) => s.replace(/[\u00a0\u202f]/g, ' ');

describe('useNumberFormat', () => {
  beforeEach(() => {
    settings = {};
  });

  it('formatea con la moneda y el idioma que publica el sistema', () => {
    settings = { locale: { language: 'es-PE', currency: 'PEN' } };

    const { result } = renderHook(() => useNumberFormat());
    expect(normalizar(result.current.formatCurrency(1234.5))).toBe('S/ 1,234.50');
  });

  it('la moneda explícita gana sobre la del sistema', () => {
    // Un flete en dólares dentro de un sistema en soles: forzar la del sistema sería una mentira.
    settings = { locale: { language: 'es-PE', currency: 'PEN' } };

    const { result } = renderHook(() => useNumberFormat());
    expect(normalizar(result.current.formatCurrency(1234.5, 'USD'))).toBe('USD 1,234.50');
  });

  it('sin moneda del sistema no inventa una', () => {
    // El ayudante crudo cae en USD por defecto; el hook no lo tapa, solo no aporta contexto falso.
    const { result } = renderHook(() => useNumberFormat());
    expect(result.current.currency).toBeUndefined();
  });

  it('usa el idioma de la interfaz cuando el sistema no publica el suyo', () => {
    const { result } = renderHook(() => useNumberFormat());
    expect(result.current.locale).toBe('es');
  });

  it('formatea cifras y porcentajes con el mismo idioma', () => {
    settings = { locale: { language: 'es-PE' } };

    const { result } = renderHook(() => useNumberFormat());
    expect(result.current.formatNumber(1234567.89)).toBe('1,234,567.89');
    expect(result.current.formatPercentage(12.345, 2)).toBe('12.35%');
  });

  it('devuelve un guion ante la ausencia de valor', () => {
    const { result } = renderHook(() => useNumberFormat());
    expect(result.current.formatNumber(null)).toBe('-');
    expect(result.current.formatCurrency(undefined)).toBe('-');
  });
});
