export { formatDate, formatDateTime, formatRelativeTime } from './date';
export { formatNumber, formatCurrency, formatPercentage, formatCompact } from './number';

// Los hooks inyectan idioma, zona y moneda de la sesión. Son la vía preferente: los ayudantes de
// arriba traen valores por defecto genéricos ('en', 'USD') que casi nunca son los correctos aquí.
export { useDateFormat } from './use-date-format';
export { useNumberFormat } from './use-number-format';
