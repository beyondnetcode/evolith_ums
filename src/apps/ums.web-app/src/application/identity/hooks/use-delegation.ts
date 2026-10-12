/**
 * use-delegation.ts — TanStack Query hooks for UserManagementDelegation bounded context
 *
 * Queries use useQuery directly (REST reads).
 * Mutations use useNotifiedMutation factory (REST writes).
 */
import { useQuery } from '@tanstack/react-query';
import delegationService from '@infra/identity/services/delegation.service';
import { useNotifiedMutation } from '@app/hooks/use-notified-mutation';
import { useI18n } from '@app/i18n/use-i18n';
import type { CreateDelegationPayload, Delegation } from '@domain/identity/models/delegation.model';
import {
  getHttpStatus,
  isNonRecoverable,
  isNetworkError,
  getRetryOptions,
} from '@app/utils/error-utils';
import { CONTEXT_QUERY_CONFIG } from '@app/shared/config/query.config';

// ─── Queries ────────────────────────────────────────────────────────────────

export const useGetDelegation = (delegationId: string | null) => {
  return useQuery<Delegation | null>({
    queryKey: ['delegations', delegationId],
    queryFn: async () => {
      if (!delegationId) throw new Error('Delegation ID required');
      try {
        return await delegationService.getDelegationById(delegationId);
      } catch (err: unknown) {
        if (getHttpStatus(err) === 404) return null;
        throw err;
      }
    },
    enabled: !!delegationId,
    ...CONTEXT_QUERY_CONFIG.DELEGATION,
    ...getRetryOptions({ maxRetries: 1, networkErrorMaxRetries: 2 }),
  });
};

export const useGetDelegationsByDelegatedAdmin = (
  delegatedAdminId: string | null,
  tenantId: string | null
) => {
  return useQuery<Delegation[]>({
    queryKey: ['delegations', 'by-delegated-admin', delegatedAdminId, tenantId],
    queryFn: () =>
      delegationService.getDelegationsByDelegatedAdmin(
        delegatedAdminId as string,
        tenantId as string
      ),
    enabled: !!delegatedAdminId && !!tenantId,
    ...CONTEXT_QUERY_CONFIG.DELEGATION,
    ...getRetryOptions({ maxRetries: 1, networkErrorMaxRetries: 2 }),
  });
};

export const useGetDelegationsByDelegatingAdmin = (
  delegatingAdminId: string | null,
  tenantId: string | null
) => {
  return useQuery<Delegation[]>({
    queryKey: ['delegations', 'by-delegating-admin', delegatingAdminId, tenantId],
    queryFn: () =>
      delegationService.getDelegationsByDelegatingAdmin(
        delegatingAdminId as string,
        tenantId as string
      ),
    enabled: !!delegatingAdminId && !!tenantId,
    ...CONTEXT_QUERY_CONFIG.DELEGATION,
    ...getRetryOptions({ maxRetries: 1, networkErrorMaxRetries: 2 }),
  });
};

// ─── Mutations ──────────────────────────────────────────────────────────────

export const useCreateDelegation = () => {
  const t = useI18n();
  return useNotifiedMutation({
    mutationFn: (payload: CreateDelegationPayload) => delegationService.createDelegation(payload),
    invalidateKeys: [['delegations']],
    successNotif: () => ({
      title: t.notifActivated ?? 'Delegation created',
      message: t.notifUserActivatedMsg ?? 'The delegation was created successfully.',
    }),
    errorNotif: () => ({
      title: t.notifActivateFailed ?? 'Create failed',
      message: t.notifUserActivateFailedMsg ?? 'Could not create the delegation.',
    }),
  });
};

export const useActivateDelegation = (delegationId: string) => {
  const t = useI18n();
  return useNotifiedMutation({
    mutationFn: () => delegationService.activateDelegation(delegationId),
    invalidateKeys: [['delegations']], // prefijo amplio: refresca listas by-*-admin + detalle (G-152)
    successNotif: () => ({
      title: t.notifActivated ?? 'Delegation activated',
      message: t.notifUserActivatedMsg ?? 'The delegation is now active.',
    }),
    errorNotif: () => ({
      title: t.notifActivateFailed ?? 'Activate failed',
      message: t.notifUserActivateFailedMsg ?? 'Could not activate the delegation.',
    }),
  });
};

export const useRevokeDelegation = (delegationId: string) => {
  const t = useI18n();
  return useNotifiedMutation({
    mutationFn: (reason: string) => delegationService.revokeDelegation(delegationId, reason),
    invalidateKeys: [['delegations']], // prefijo amplio: refresca listas by-*-admin + detalle (G-152)
    successNotif: () => ({
      title: t.notifBlocked ?? 'Delegation revoked',
      message: t.notifUserBlockedMsg ?? 'The delegation has been revoked.',
      type: 'warning',
    }),
    errorNotif: () => ({
      title: t.notifBlockFailed ?? 'Revoke failed',
      message: t.notifUserBlockFailedMsg ?? 'Could not revoke the delegation.',
    }),
  });
};

// G-132: máquina de aprobación de delegaciones (espejo de IGA RolePromotion, ADR-UMS-086).

export const useSubmitDelegationForApproval = (delegationId: string) => {
  return useNotifiedMutation({
    mutationFn: () => delegationService.submitDelegationForApproval(delegationId),
    invalidateKeys: [['delegations']], // prefijo amplio: refresca listas by-*-admin + detalle (G-152)
    successNotif: () => ({
      title: 'Delegación enviada a aprobación',
      message: 'La delegación quedó pendiente de aprobación.',
    }),
    errorNotif: () => ({
      title: 'No se pudo enviar a aprobación',
      message: 'No se pudo enviar la delegación a aprobación.',
    }),
  });
};

export const useApproveDelegation = (delegationId: string) => {
  return useNotifiedMutation({
    mutationFn: () => delegationService.approveDelegation(delegationId),
    invalidateKeys: [['delegations']], // prefijo amplio: refresca listas by-*-admin + detalle (G-152)
    successNotif: () => ({
      title: 'Delegación aprobada',
      message: 'La delegación fue aprobada y ahora está activa.',
    }),
    errorNotif: () => ({
      title: 'No se pudo aprobar',
      message: 'No se pudo aprobar la delegación.',
    }),
  });
};

export const useRejectDelegation = (delegationId: string) => {
  return useNotifiedMutation({
    mutationFn: (reason: string) => delegationService.rejectDelegation(delegationId, reason),
    invalidateKeys: [['delegations']], // prefijo amplio: refresca listas by-*-admin + detalle (G-152)
    successNotif: () => ({
      title: 'Delegación rechazada',
      message: 'La delegación fue rechazada.',
      type: 'warning',
    }),
    errorNotif: () => ({
      title: 'No se pudo rechazar',
      message: 'No se pudo rechazar la delegación.',
    }),
  });
};
