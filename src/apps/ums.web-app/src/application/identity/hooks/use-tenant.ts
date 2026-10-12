import { useQuery } from '@tanstack/react-query';
import tenantService from '@infra/identity/services/tenant.service';
import { useNotifiedMutation } from '@app/hooks/use-notified-mutation';
import { useI18n } from '@app/i18n/use-i18n';
import { CreateTenantPayload, Tenant, TenantPage } from '@domain/identity/models/tenant.model';
import {
  getHttpStatus,
  isNonRecoverable,
  isNetworkError,
  getRetryOptions,
} from '@app/utils/error-utils';
import { CONTEXT_QUERY_CONFIG } from '@app/shared/config/query.config';

// ─── Query params ───────────────────────────────────────────────────────────

export interface TenantQueryParams {
  page: number;
  pageSize: number;
  search?: string;
  criteria?: string;
  status?: string;
  sortBy?: string;
  sortOrder?: 'asc' | 'desc';
}

// ─── Queries ────────────────────────────────────────────────────────────────

export const useGetAllTenants = (params: TenantQueryParams | null) => {
  return useQuery<TenantPage>({
    queryKey: [
      'tenants',
      params?.page,
      params?.pageSize,
      params?.search,
      params?.criteria,
      params?.status,
      params?.sortBy,
      params?.sortOrder,
    ],
    queryFn: () => tenantService.getAll(params!),
    enabled: !!params,
    ...CONTEXT_QUERY_CONFIG.TENANT,
    ...getRetryOptions({ maxRetries: 1, networkErrorMaxRetries: 2 }),
  });
};

export const useGetTenant = (tenantId: string | null) => {
  return useQuery<Tenant | null>({
    queryKey: ['tenants', tenantId],
    queryFn: async () => {
      if (!tenantId) throw new Error('Tenant ID required');
      try {
        return await tenantService.getById(tenantId);
      } catch (err: unknown) {
        if (getHttpStatus(err) === 404) return null;
        throw err;
      }
    },
    enabled: !!tenantId,
    ...CONTEXT_QUERY_CONFIG.TENANT,
    ...getRetryOptions({ maxRetries: 1, networkErrorMaxRetries: 2 }),
  });
};

// ─── Mutations ──────────────────────────────────────────────────────────────

export const useCreateTenant = () => {
  const t = useI18n();
  return useNotifiedMutation({
    mutationFn: (payload: CreateTenantPayload) => tenantService.createTenant(payload),
    invalidateKeys: [['tenants']],
    // El nombre/código vienen del payload enviado (`variables`), no de la respuesta
    // (que solo trae `tenantId`). Antes se leían de `data` → mensaje vacío / parse roto.
    successNotif: (_data, variables) => ({
      title: t.notifTenantCreated,
      message: t.notifTenantCreatedMsg(variables.name, variables.code),
    }),
    errorNotif: () => ({
      title: t.notifTenantCreateFailed,
      message: t.notifTenantCreateFailed,
    }),
  });
};

export const useActivateTenant = (tenantId: string) => {
  const t = useI18n();
  return useNotifiedMutation({
    mutationFn: () => tenantService.activateTenant(tenantId),
    invalidateKeys: [['tenants', tenantId]],
    successNotif: () => ({
      title: t.notifActivated,
      message: t.notifActivatedMsg,
    }),
    errorNotif: () => ({
      title: t.notifActivateFailed,
      message: t.notifActivateFailedMsg,
    }),
  });
};

export const useSuspendTenant = (tenantId: string) => {
  const t = useI18n();
  return useNotifiedMutation({
    mutationFn: () => tenantService.suspendTenant(tenantId),
    invalidateKeys: [['tenants', tenantId]],
    successNotif: () => ({
      title: t.notifSuspended,
      message: t.notifSuspendedMsg,
      type: 'warning' as const,
    }),
    errorNotif: () => ({
      title: t.notifSuspendFailed,
      message: t.notifSuspendFailedMsg,
    }),
  });
};

export const useUpdateTenant = () => {
  const t = useI18n();
  return useNotifiedMutation({
    mutationFn: async (vars: {
      tenantId: string;
      name: string;
      type: string;
      companyReference?: string | null;
    }) => {
      await tenantService.updateTenant(vars.tenantId, {
        name: vars.name,
        type: vars.type,
        companyReference: vars.companyReference,
      });
      return vars;
    },
    invalidateKeys: [['tenants']],
    successNotif: data => ({
      title: t.notifTenantUpdated,
      message: t.notifTenantUpdatedMsg(data.name),
    }),
    errorNotif: () => ({
      title: 'Error al Actualizar Tenant',
      message: 'No se pudo actualizar el tenant.',
    }),
  });
};

export const useSetManagementOwner = (tenantId: string) => {
  return useNotifiedMutation({
    mutationFn: (value: boolean) => tenantService.setManagementOwner(tenantId, value),
    invalidateKeys: [['tenants', tenantId], ['tenants']],
    successNotif: (_, variables) => ({
      title: variables ? 'Management access granted' : 'Management access revoked',
      message: variables
        ? 'Tenant can now manage the UMS portal.'
        : 'Tenant no longer has UMS management access.',
    }),
    errorNotif: () => ({
      title: 'Failed to update management access',
      message: 'Could not update management ownership. Try again.',
    }),
  });
};
