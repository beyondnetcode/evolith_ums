import { useQuery } from '@tanstack/react-query';
import { useNotifiedMutation } from '@app/hooks/use-notified-mutation';
import inboxService from '@infra/identity/services/inbox.service';
import { CONTEXT_QUERY_CONFIG } from '@app/shared/config/query.config';

export const useGetUserSignups = (enabled = true) =>
  useQuery({
    queryKey: ['inbox-user-signups'],
    queryFn: () => inboxService.getUserSignups(),
    enabled,
    ...CONTEXT_QUERY_CONFIG.TENANT,
  });

export const useGetProfileRequests = (enabled = true) =>
  useQuery({
    queryKey: ['inbox-profile-requests'],
    queryFn: () => inboxService.getProfileRequests(),
    enabled,
    ...CONTEXT_QUERY_CONFIG.TENANT,
  });

export const useActivateUser = () =>
  useNotifiedMutation({
    mutationFn: (userAccountId: string) => inboxService.activateUser(userAccountId),
    invalidateKeys: [['inbox-user-signups']],
    successNotif: () => ({
      title: 'Usuario activado',
      message: 'La cuenta de usuario fue activada correctamente.',
    }),
    errorNotif: () => ({ title: 'Error al activar', message: 'No se pudo activar el usuario.' }),
  });

export const useDenyUserSignup = () =>
  useNotifiedMutation({
    mutationFn: ({ id, reason }: { id: string; reason?: string }) =>
      inboxService.denyUserSignup(id, reason),
    invalidateKeys: [['inbox-user-signups']],
    successNotif: () => ({
      title: 'Solicitud rechazada',
      message: 'La solicitud de usuario fue rechazada.',
    }),
    errorNotif: () => ({
      title: 'Error al rechazar',
      message: 'No se pudo rechazar la solicitud.',
    }),
  });

export const useApproveProfileRequest = () =>
  useNotifiedMutation({
    mutationFn: ({ id, roleId, reason }: { id: string; roleId: string; reason?: string }) =>
      inboxService.approveProfileRequest(id, roleId, reason),
    invalidateKeys: [['inbox-profile-requests']],
    successNotif: () => ({
      title: 'Perfil aprobado',
      message: 'La solicitud de perfil fue aprobada.',
    }),
    errorNotif: () => ({ title: 'Error al aprobar', message: 'No se pudo aprobar la solicitud.' }),
  });

export const useRejectProfileRequest = () =>
  useNotifiedMutation({
    mutationFn: ({ id, reason }: { id: string; reason?: string }) =>
      inboxService.rejectProfileRequest(id, reason),
    invalidateKeys: [['inbox-profile-requests']],
    successNotif: () => ({
      title: 'Solicitud rechazada',
      message: 'La solicitud de perfil fue rechazada.',
    }),
    errorNotif: () => ({
      title: 'Error al rechazar',
      message: 'No se pudo rechazar la solicitud.',
    }),
  });
