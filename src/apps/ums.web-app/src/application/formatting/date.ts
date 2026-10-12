/**
 * ADR-0076: UTC Date Storage, Timezone Detection, and Language Resolution.
 *
 * Rules:
 *  - All dates received from the API are UTC (ISO 8601 with Z suffix).
 *  - Display always converts UTC → session timezone using Intl.DateTimeFormat.
 *  - The locale parameter must come from the active i18n store (never hardcoded).
 *
 * Use the `useDateFormat` hook from presentation layer to automatically inject
 * locale and timezone from the session instead of passing them manually.
 */

/**
 * Formats a UTC date value for display in the user's locale and timezone.
 *
 * @param date   UTC date (ISO string with Z suffix, or Date object).
 * @param locale IANA locale string from i18n store (e.g. "es", "en").
 * @param timeZone IANA timezone from session (e.g. "America/Lima"). Defaults to browser local.
 */
export function formatDate(
  date: Date | string | null | undefined,
  locale: string,
  timeZone?: string,
  options?: Intl.DateTimeFormatOptions
): string {
  if (!date) return '-';
  const d = typeof date === 'string' ? new Date(date) : date;
  if (isNaN(d.getTime())) return '-';

  return new Intl.DateTimeFormat(locale, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    timeZone,
    ...options,
  }).format(d);
}

/**
 * Formats a UTC datetime value for display including time in the user's timezone.
 */
export function formatDateTime(
  date: Date | string | null | undefined,
  locale: string,
  timeZone?: string,
  options?: Intl.DateTimeFormatOptions
): string {
  if (!date) return '-';
  const d = typeof date === 'string' ? new Date(date) : date;
  if (isNaN(d.getTime())) return '-';

  return new Intl.DateTimeFormat(locale, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    timeZone,
    ...options,
  }).format(d);
}

/**
 * Returns a relative time string ("hace 3 días") for a UTC date.
 */
export function formatRelativeTime(date: Date | string | null | undefined, locale: string): string {
  if (!date) return '-';
  const d = typeof date === 'string' ? new Date(date) : date;
  if (isNaN(d.getTime())) return '-';

  const now = new Date();
  const diffMs = now.getTime() - d.getTime();
  const diffSec = Math.floor(diffMs / 1000);
  const diffMin = Math.floor(diffSec / 60);
  const diffHour = Math.floor(diffMin / 60);
  const diffDay = Math.floor(diffHour / 24);

  const rtf = new Intl.RelativeTimeFormat(locale, { numeric: 'auto' });

  if (diffSec < 60) return rtf.format(-diffSec, 'second');
  if (diffMin < 60) return rtf.format(-diffMin, 'minute');
  if (diffHour < 24) return rtf.format(-diffHour, 'hour');
  if (diffDay < 30) return rtf.format(-diffDay, 'day');
  return formatDate(d, locale);
}
