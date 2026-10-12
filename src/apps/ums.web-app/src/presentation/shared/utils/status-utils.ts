export interface StatusColorConfig {
  bg: string;
  border: string;
  text: string;
}

export const STATUS_COLORS = {
  Active: { bg: 'bg-emerald-500/10', border: 'border-emerald-500/25', text: 'text-emerald-500' },
  Inactive: { bg: 'bg-amber-500/10', border: 'border-amber-500/25', text: 'text-amber-500' },
  Archived: { bg: 'bg-rose-500/10', border: 'border-rose-500/25', text: 'text-rose-500' },
  Published: { bg: 'bg-emerald-500/10', border: 'border-emerald-500/25', text: 'text-emerald-500' },
  Draft: { bg: 'bg-amber-500/10', border: 'border-amber-500/25', text: 'text-amber-500' },
  Deprecated: { bg: 'bg-rose-500/10', border: 'border-rose-500/25', text: 'text-rose-500' },
} as const;

export const STATUS_LABELS_ES: Record<string, string> = {
  active: 'Activo',
  inactive: 'Inactivo',
  archived: 'Archivado',
  published: 'Publicado',
  draft: 'Borrador',
  deprecated: 'Descontinuado',
  Active: 'Activo',
  Inactive: 'Inactivo',
  Archived: 'Archivado',
  Published: 'Publicado',
  Draft: 'Borrador',
  Deprecated: 'Descontinuada',
};

export const STATUS_LABELS_EN: Record<string, string> = {
  active: 'Active',
  inactive: 'Inactive',
  archived: 'Archived',
  published: 'Published',
  draft: 'Draft',
  deprecated: 'Deprecated',
  Active: 'Active',
  Inactive: 'Inactive',
  Archived: 'Archived',
  Published: 'Published',
  Draft: 'Draft',
  Deprecated: 'Deprecated',
};

export function getStatusColors(status: string): StatusColorConfig {
  return (
    STATUS_COLORS[status as keyof typeof STATUS_COLORS] ?? {
      bg: 'bg-m3-surface-variant',
      border: 'border-m3-outline/20',
      text: 'text-m3-secondary',
    }
  );
}

export function getStatusLabel(status: string, locale: 'es' | 'en' = 'es'): string {
  const labels = locale === 'es' ? STATUS_LABELS_ES : STATUS_LABELS_EN;
  return labels[status] ?? status;
}
