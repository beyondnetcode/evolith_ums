const defaultLocale = 'en';

export function formatNumber(
  value: number | null | undefined,
  options?: Intl.NumberFormatOptions,
  locale: string = defaultLocale
): string {
  if (value === null || value === undefined) return '-';
  if (typeof value !== 'number' || isNaN(value)) return '-';

  return new Intl.NumberFormat(locale, options).format(value);
}

export function formatCurrency(
  value: number | null | undefined,
  currency: string = 'USD',
  locale: string = defaultLocale
): string {
  if (value === null || value === undefined) return '-';
  if (typeof value !== 'number' || isNaN(value)) return '-';

  return new Intl.NumberFormat(locale, {
    style: 'currency',
    currency,
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(value);
}

export function formatPercentage(
  value: number | null | undefined,
  decimals: number = 1,
  locale: string = defaultLocale
): string {
  if (value === null || value === undefined) return '-';
  if (typeof value !== 'number' || isNaN(value)) return '-';

  return new Intl.NumberFormat(locale, {
    style: 'percent',
    minimumFractionDigits: decimals,
    maximumFractionDigits: decimals,
  }).format(value / 100);
}

export function formatCompact(
  value: number | null | undefined,
  locale: string = defaultLocale
): string {
  if (value === null || value === undefined) return '-';
  if (typeof value !== 'number' || isNaN(value)) return '-';

  return new Intl.NumberFormat(locale, {
    notation: 'compact',
    compactDisplay: 'short',
  }).format(value);
}
