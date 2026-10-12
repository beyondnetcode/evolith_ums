/**
 * useDateFormat — React hook that wraps date formatting with the active
 * locale and timezone.
 *
 * Precedencia: **sistema → inquilino → navegador**. El sistema publica en el grafo su idioma y su
 * zona (`settings.locale.*`, G-178) y son los más específicos que hay: mandan sobre los parámetros
 * del inquilino, que a su vez mandan sobre lo que deduzca el navegador.
 *
 * El idioma del formato no es el mismo dato que el de la interfaz: aquí interesa la etiqueta
 * completa —`es-PE` da `31/12/2026`, `es` a secas no lo garantiza—, mientras que la interfaz solo
 * distingue `es` de `en` porque son los dos paquetes de traducción que existen.
 *
 * ADR-0076: Use this hook in all components instead of calling formatDate/
 * formatDateTime directly, so that locale and timezone are always consistent
 * with the authenticated session without passing them as props.
 *
 * Usage:
 *   const { formatDate, formatDateTime, formatRelativeTime, timezone } = useDateFormat();
 *   <span>{formatDate(record.createdAt)}</span>
 */
import { useCallback } from 'react';
import { useI18nStore } from '@app/stores/i18n.store';
import { useAuthStore } from '@app/stores/auth.store';
import { useSystemSettings } from '@app/authorization/hooks/use-system-settings';
import {
  formatDate as _formatDate,
  formatDateTime as _formatDateTime,
  formatRelativeTime as _formatRelativeTime,
} from './date';

/** Lo que llega de la API: ISO en UTC, o nada. */
type FechaFormateable = Date | string | null | undefined;

export function useDateFormat() {
  const idiomaInterfaz = useI18nStore(s => s.language);
  const zonaInquilino = useAuthStore(s => s.user?.sessionParameters?.defaultTimezone);
  const { locale: localeSistema } = useSystemSettings();

  const locale = localeSistema.language ?? idiomaInterfaz;
  const timezone = localeSistema.timezone ?? zonaInquilino;

  const formatDate = useCallback(
    (date: FechaFormateable, options?: Intl.DateTimeFormatOptions) =>
      _formatDate(date, locale, timezone, options),
    [locale, timezone]
  );

  const formatDateTime = useCallback(
    (date: FechaFormateable, options?: Intl.DateTimeFormatOptions) =>
      _formatDateTime(date, locale, timezone, options),
    [locale, timezone]
  );

  const formatRelativeTime = useCallback(
    (date: FechaFormateable) => _formatRelativeTime(date, locale),
    [locale]
  );

  return { formatDate, formatDateTime, formatRelativeTime, locale, timezone };
}
