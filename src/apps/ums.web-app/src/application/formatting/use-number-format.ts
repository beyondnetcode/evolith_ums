/**
 * useNumberFormat — cifras con el idioma y la moneda del sistema.
 *
 * Hermano de `useDateFormat` y con la misma precedencia: **sistema → inquilino → navegador**. El
 * sistema publica `settings.locale.language` y `settings.locale.currency` (G-178); son lo más
 * específico que hay y mandan sobre el idioma de la interfaz.
 *
 * Existe porque los ayudantes crudos de `./number` traen `'USD'` y `'en'` por defecto: un importe
 * formateado sin contexto sale en dólares y con el separador equivocado, que en un operador
 * logístico peruano no es un detalle estético sino una cifra mal leída. Igual que con las fechas
 * (ADR-0076), la regla es usar el hook y no los ayudantes sueltos.
 */
import { useCallback } from 'react';
import { useI18nStore } from '@app/stores/i18n.store';
import { useSystemSettings } from '@app/authorization/hooks/use-system-settings';
import {
  formatNumber as _formatNumber,
  formatCurrency as _formatCurrency,
  formatPercentage as _formatPercentage,
  formatCompact as _formatCompact,
} from './number';

export function useNumberFormat() {
  const idiomaInterfaz = useI18nStore(s => s.language);
  const { locale: localeSistema } = useSystemSettings();

  const locale = localeSistema.language ?? idiomaInterfaz;
  const currency = localeSistema.currency;

  const formatNumber = useCallback(
    (value: number | null | undefined, options?: Intl.NumberFormatOptions) =>
      _formatNumber(value, options, locale),
    [locale]
  );

  /**
   * La moneda explícita gana sobre la del sistema: un importe puede venir en otra —un flete en
   * dólares dentro de un sistema en soles— y forzar la del sistema lo convertiría en una mentira.
   */
  const formatCurrency = useCallback(
    (value: number | null | undefined, monedaExplicita?: string) =>
      _formatCurrency(value, monedaExplicita ?? currency, locale),
    [currency, locale]
  );

  const formatPercentage = useCallback(
    (value: number | null | undefined, decimals?: number) =>
      _formatPercentage(value, decimals, locale),
    [locale]
  );

  const formatCompact = useCallback(
    (value: number | null | undefined) => _formatCompact(value, locale),
    [locale]
  );

  return { formatNumber, formatCurrency, formatPercentage, formatCompact, locale, currency };
}
