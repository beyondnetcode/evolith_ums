/**
 * useStatusLabel — shared i18n-aware status label resolver.
 *
 * H-4: Uses TenantStatusSchema enum values instead of hardcoded strings.
 */
import { useI18n } from '@app/i18n/use-i18n';
import { TenantStatusSchema } from '@domain/identity/schemas/tenant.schema';

// Mapa de estado→clave i18n abarcando los estados de varios dominios que comparten esta etiqueta
// (G-153). Los que aún no tienen clave i18n propia (Draft, PendingApproval, Published, Archived,
// Revoked, Rejected, Expired…) caen al fallback = valor CRUDO, no a «Pendiente» (que antes disfrazaba
// de «Pendiente» a Draft/PendingApproval y era engañoso). Localizarlos requiere nuevas claves i18n.
const STATUS_KEY_MAP: Record<string, keyof ReturnType<typeof useI18n>> = {
  [TenantStatusSchema.enum.Active]: 'active',
  [TenantStatusSchema.enum.Suspended]: 'suspended',
  [TenantStatusSchema.enum.Pending]: 'pending',
  Inactive: 'inactive',
  Blocked: 'blocked',
  Maintenance: 'maintenance',
  Deprecated: 'deprecated',
};

export const useStatusLabel = () => {
  const t = useI18n();
  return (status: string): string => {
    const key = STATUS_KEY_MAP[status];
    return key ? (t[key] as string) : status;
  };
};
