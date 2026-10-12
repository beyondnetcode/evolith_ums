import { useAuthStore } from '@app/stores/auth.store';

/**
 * Returns the effective tenant ID for form submissions.
 * Uses the override value if provided, otherwise falls back to the session tenant.
 *
 * @param overrideTenantId - Optional tenant ID to use instead of session tenant
 * @returns The effective tenant ID, or undefined if none available
 *
 * @example
 * ```tsx
 * const tenantId = useEffectiveTenant(formValues.tenantId);
 * ```
 */
export function useEffectiveTenant(overrideTenantId?: string): string | undefined {
  const sessionTenantId = useAuthStore(state => state.user?.tenantId);
  return overrideTenantId || sessionTenantId;
}
