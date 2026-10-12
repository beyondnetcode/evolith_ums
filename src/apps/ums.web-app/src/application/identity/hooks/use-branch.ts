import { useQuery } from '@tanstack/react-query';
import tenantService from '@infra/identity/services/tenant.service';
import { useNotifiedMutation } from '@app/hooks/use-notified-mutation';
import { useI18n } from '@app/i18n/use-i18n';
import {
  AddBranchPayload,
  Branch,
  BranchLifecycleEntry,
} from '@domain/identity/models/branch.model';
import { getHttpStatus, getRetryOptions } from '@app/utils/error-utils';
import { getBlockedOperation } from '@app/errors/http-error';
import { CONTEXT_QUERY_CONFIG } from '@app/shared/config/query.config';

// ─── Query ──────────────────────────────────────────────────────────────────

/**
 * ADR-0164: por defecto el backend NO devuelve las sucursales cerradas. `includeClosed` entra en la
 * clave de caché porque son dos conjuntos distintos, no dos vistas del mismo.
 */
export const useGetBranches = (tenantId: string | null, includeClosed = false) => {
  return useQuery<Branch[]>({
    queryKey: ['tenants', tenantId, 'branches', { includeClosed }],
    queryFn: async () => {
      if (!tenantId) throw new Error('Tenant ID required');
      try {
        return await tenantService.getBranches(tenantId, includeClosed);
      } catch (err: unknown) {
        if (getHttpStatus(err) === 404) return [];
        throw err;
      }
    },
    enabled: !!tenantId,
    ...CONTEXT_QUERY_CONFIG.BRANCH,
    ...getRetryOptions({ maxRetries: 1 }),
  });
};

/**
 * Bitácora de la sucursal. Se consulta solo cuando alguien la abre (`enabled`), porque es una
 * pregunta de auditoría puntual y no algo que haya que traer con cada listado.
 */
export const useGetBranchLifecycle = (tenantId: string | null, branchId: string | null) => {
  return useQuery<BranchLifecycleEntry[]>({
    queryKey: ['tenants', tenantId, 'branches', branchId, 'bitacora'],
    queryFn: async () => {
      if (!tenantId || !branchId) throw new Error('Tenant ID and Branch ID required');
      return await tenantService.getBranchLifecycle(tenantId, branchId);
    },
    enabled: !!tenantId && !!branchId,
    ...CONTEXT_QUERY_CONFIG.BRANCH,
    ...getRetryOptions({ maxRetries: 1 }),
  });
};

// ─── Mutations ──────────────────────────────────────────────────────────────

export const useAddBranch = (tenantId: string) => {
  const t = useI18n();
  return useNotifiedMutation({
    mutationFn: (payload: AddBranchPayload) => tenantService.addBranch(tenantId, payload),
    invalidateKeys: [['tenants', tenantId, 'branches']],
    successNotif: data => ({
      title: t.notifBranchAdded,
      message: t.notifBranchAddedMsg(data.code),
    }),
    errorNotif: () => ({
      title: t.notifBranchAddFailed ?? 'Error al Registrar Sucursal',
      message: t.notifBranchAddFailedMsg ?? 'No se pudo registrar la sucursal.',
    }),
  });
};

export const useUpdateBranch = (tenantId: string) => {
  const t = useI18n();
  return useNotifiedMutation({
    mutationFn: async (vars: {
      branchId: string;
      name: string;
      geofencingMetadata?: string | null;
    }) => {
      await tenantService.updateBranch(tenantId, vars.branchId, {
        name: vars.name,
        geofencingMetadata: vars.geofencingMetadata,
      });
      return vars;
    },
    invalidateKeys: [['tenants', tenantId, 'branches']],
    successNotif: data => ({
      title: t.notifBranchUpdated,
      message: t.notifBranchUpdatedMsg(data.name),
    }),
    errorNotif: () => ({
      title: 'Error al Actualizar Sucursal',
      message: 'No se pudo actualizar la sucursal.',
    }),
  });
};

/**
 * Cómo se nombra cada clase de dependencia que puede bloquear el cierre. Sin esto el desglose diría
 * «UserAccount: 3», que es el nombre del agregado, no algo que quien opera reconozca.
 */
const BLOCKING_LABELS: Record<string, { singular: string; plural: string }> = {
  UserAccount: { singular: 'cuenta de usuario activa', plural: 'cuentas de usuario activas' },
  Profile: { singular: 'perfil activo', plural: 'perfiles activos' },
};

/** «3 cuentas de usuario activas y 1 perfil activo»: enumera cuanto bloquea, no solo lo primero. */
export function describeBlockingDependencies(
  dependencies: { entityType: string; count: number }[]
): string {
  const parts = dependencies.map(dep => {
    const labels = BLOCKING_LABELS[dep.entityType];
    if (!labels) return `${dep.count} × ${dep.entityType}`;
    return `${dep.count} ${dep.count === 1 ? labels.singular : labels.plural}`;
  });

  if (parts.length <= 1) return parts.join('');
  return `${parts.slice(0, -1).join(', ')} y ${parts[parts.length - 1]}`;
}

/**
 * Cierra la sucursal (ADR-0164). Antes se llamaba «eliminar»; el endpoint es el mismo, lo que
 * cambió es que ya no borra nada y que no hay vuelta atrás.
 *
 * Si el backend responde 409 el mensaje NOMBRA lo que bloquea —cuántas cuentas y cuántos perfiles
 * siguen activos—, porque es lo que hay que resolver antes de reintentar.
 */
export const useCloseBranch = (tenantId: string) => {
  const t = useI18n();
  return useNotifiedMutation({
    mutationFn: (vars: { branchId: string; reason?: string }) =>
      tenantService.closeBranch(tenantId, vars.branchId, vars.reason),
    invalidateKeys: [['tenants', tenantId, 'branches']],
    successNotif: () => ({
      title: t.notifBranchClosed,
      message: t.notifBranchClosedMsg,
      type: 'warning' as const,
    }),
    errorNotif: error => {
      const blocked = getBlockedOperation(error);
      if (blocked) {
        return {
          title: t.notifBranchCloseBlocked,
          message: t.notifBranchCloseBlockedMsg(describeBlockingDependencies(blocked.dependencies)),
        };
      }
      return {
        title: t.notifBranchCloseFailed,
        message: t.notifBranchCloseFailedMsg,
      };
    },
  });
};

export const useDeactivateBranch = (tenantId: string) => {
  const t = useI18n();
  return useNotifiedMutation({
    mutationFn: (branchId: string) => tenantService.deactivateBranch(tenantId, branchId),
    invalidateKeys: [['tenants', tenantId, 'branches']],
    successNotif: () => ({
      title: t.notifBranchDeactivated,
      message: t.notifBranchDeactivatedMsg,
      type: 'info' as const,
    }),
    errorNotif: () => ({
      title: t.notifBranchDeactivateFailed ?? 'Error al Desactivar Sucursal',
      message: t.notifBranchDeactivateFailedMsg ?? 'No se pudo desactivar la sucursal.',
    }),
  });
};

export const useReactivateBranch = (tenantId: string) => {
  const t = useI18n();
  return useNotifiedMutation({
    mutationFn: (branchId: string) => tenantService.reactivateBranch(tenantId, branchId),
    invalidateKeys: [['tenants', tenantId, 'branches']],
    successNotif: () => ({
      title: t.notifBranchReactivated,
      message: t.notifBranchReactivatedMsg,
    }),
    errorNotif: () => ({
      title: t.notifBranchReactivateFailed ?? 'Error al Reactivar Sucursal',
      message: t.notifBranchReactivateFailedMsg ?? 'No se pudo reactivar la sucursal.',
    }),
  });
};
