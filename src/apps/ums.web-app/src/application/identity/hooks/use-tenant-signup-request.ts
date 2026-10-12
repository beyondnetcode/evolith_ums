import { useQuery } from '@tanstack/react-query';
import { useNotifiedMutation } from '@app/hooks/use-notified-mutation';
import tenantSignupRequestService from '@infra/identity/services/tenant-signup-request.service';
import { useI18n } from '@app/i18n/use-i18n';
import { CONTEXT_QUERY_CONFIG } from '@app/shared/config/query.config';
import {
  type ApproveTenantSignupResponse,
  type TenantSignupRequest,
} from '@domain/identity/models/tenant-signup-request.model';

export const useGetTenantSignupRequests = (enabled = true) => {
  return useQuery<TenantSignupRequest[]>({
    queryKey: ['tenant-signup-requests'],
    queryFn: () => tenantSignupRequestService.getPending(),
    enabled,
    ...CONTEXT_QUERY_CONFIG.TENANT,
  });
};

export const useApproveTenantSignupRequest = () => {
  const t = useI18n();

  return useNotifiedMutation<ApproveTenantSignupResponse, string>({
    mutationFn: (tenantSignupRequestId: string) =>
      tenantSignupRequestService.approve(tenantSignupRequestId),
    invalidateKeys: [['tenant-signup-requests'], ['tenants']],
    successNotif: data => ({
      title: t.notifActivated ?? 'Solicitud aprobada',
      message: data.message,
      type: 'success',
    }),
    errorNotif: () => ({
      title: 'No se pudo aprobar',
      message: 'La solicitud de tenant no pudo ser aprobada.',
      type: 'error',
    }),
  });
};
