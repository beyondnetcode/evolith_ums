import { useQuery } from '@tanstack/react-query';
import profileService from '@infra/authorization/services/profile.service';
import { useNotifiedMutation } from '@app/hooks/use-notified-mutation';
import {
  type CreateProfilePayload,
  type ProfilePage,
  type Profile,
} from '@domain/authorization/schemas/profile.schema';
import { getHttpStatus, getRetryOptions } from '@app/utils/error-utils';
import { CONTEXT_QUERY_CONFIG } from '@app/shared/config/query.config';

// ─── Query params ────────────────────────────────────────────────────────────

export interface ProfileQueryParams {
  page: number;
  pageSize: number;
  search?: string;
  criteria?: string;
  status?: string;
  sortBy?: string;
  sortOrder?: 'asc' | 'desc';
  tenantId?: string;
  userId?: string;
}

// ─── Queries ─────────────────────────────────────────────────────────────────

export const useGetAllProfiles = (params: ProfileQueryParams | null) =>
  useQuery<ProfilePage>({
    queryKey: [
      'profiles',
      params?.page,
      params?.pageSize,
      params?.search,
      params?.criteria,
      params?.status,
      params?.sortBy,
      params?.sortOrder,
      params?.tenantId,
      params?.userId,
    ],
    queryFn: () => profileService.getAll(params!),
    enabled: !!params,
    ...CONTEXT_QUERY_CONFIG.PROFILE,
    ...getRetryOptions({ maxRetries: 1 }),
  });

export const useGetProfile = (profileId: string | null) =>
  useQuery<Profile | null>({
    queryKey: ['profiles', profileId],
    queryFn: async () => {
      if (!profileId) throw new Error('Profile ID required');
      try {
        return await profileService.getById(profileId);
      } catch (err) {
        if (getHttpStatus(err) === 404) return null;
        throw err;
      }
    },
    enabled: !!profileId,
    ...CONTEXT_QUERY_CONFIG.PROFILE,
    ...getRetryOptions({ maxRetries: 1 }),
  });

// ─── Mutations ───────────────────────────────────────────────────────────────

export const useCreateProfile = () =>
  useNotifiedMutation({
    mutationFn: (payload: CreateProfilePayload) => profileService.create(payload),
    invalidateKeys: [['profiles']],
    successNotif: () => ({
      title: 'Perfil Creado',
      message: 'El perfil de autorización fue creado con éxito.',
    }),
    errorNotif: () => ({
      title: 'Error al Crear Perfil',
      message: 'No se pudo crear el perfil.',
    }),
  });

export const useAssignProfileTemplate = () =>
  useNotifiedMutation({
    mutationFn: ({ profileId, templateId }: { profileId: string; templateId: string }) =>
      profileService.assignTemplate(profileId, templateId),
    invalidateKeys: [['profiles']],
    successNotif: () => ({
      title: 'Plantilla Vinculada',
      message: 'La plantilla de permisos fue vinculada al perfil con éxito.',
    }),
    errorNotif: () => ({
      title: 'Error al Vincular Plantilla',
      message: 'No se pudo vincular la plantilla de permisos.',
    }),
  });

export const useOverrideProfilePermission = () =>
  useNotifiedMutation({
    mutationFn: ({
      profileId,
      permissionId,
      effect,
    }: {
      profileId: string;
      permissionId: string;
      effect: 'allow' | 'deny' | 'neutral';
    }) => profileService.overridePermission(profileId, permissionId, effect),
    invalidateKeys: [['profiles']],
    successNotif: () => ({
      title: 'Anulación Aplicada',
      message: 'La anulación del permiso fue guardada con éxito.',
    }),
    errorNotif: () => ({
      title: 'Error en Anulación',
      message: 'No se pudo aplicar la anulación del permiso.',
    }),
  });

export const useActivateProfilePermission = () =>
  useNotifiedMutation({
    mutationFn: ({ profileId, permissionId }: { profileId: string; permissionId: string }) =>
      profileService.activatePermission(profileId, permissionId),
    invalidateKeys: [['profiles']],
    successNotif: () => ({
      title: 'Permiso Activado',
      message: 'El permiso individual fue activado.',
    }),
    errorNotif: () => ({
      title: 'Error al Activar',
      message: 'No se pudo activar el permiso.',
    }),
  });

export const useDeactivateProfilePermission = () =>
  useNotifiedMutation({
    mutationFn: ({ profileId, permissionId }: { profileId: string; permissionId: string }) =>
      profileService.deactivatePermission(profileId, permissionId),
    invalidateKeys: [['profiles']],
    successNotif: () => ({
      title: 'Permiso Desactivado',
      message: 'El permiso individual fue desactivado.',
      type: 'info' as const,
    }),
    errorNotif: () => ({
      title: 'Error al Desactivar',
      message: 'No se pudo desactivar el permiso.',
    }),
  });

export const useActivateProfile = () =>
  useNotifiedMutation({
    mutationFn: (profileId: string) => profileService.activate(profileId),
    invalidateKeys: [['profiles']],
    successNotif: () => ({
      title: 'Perfil Activado',
      message: 'El perfil de autorización ha sido activado.',
    }),
    errorNotif: () => ({
      title: 'Error al Activar Perfil',
      message: 'No se pudo activar el perfil.',
    }),
  });

export const useDeactivateProfile = () =>
  useNotifiedMutation({
    mutationFn: (profileId: string) => profileService.deactivate(profileId),
    invalidateKeys: [['profiles']],
    successNotif: () => ({
      title: 'Perfil Desactivado',
      message: 'El perfil de autorización ha sido desactivado.',
      type: 'warning' as const,
    }),
    errorNotif: () => ({
      title: 'Error al Desactivar Perfil',
      message: 'No se pudo desactivar el perfil.',
    }),
  });
