/**
 * use-app-configuration.ts
 *
 * React Query hooks for the AppConfiguration bounded context.
 */
import { useQuery } from '@tanstack/react-query';
import { useNotifiedMutation } from '@app/hooks/use-notified-mutation';
import { getRetryOptions } from '@app/utils/error-utils';
import { CONTEXT_QUERY_CONFIG } from '@app/shared/config/query.config';
import appConfigurationService from '@infra/configuration/services/app-configuration.service';
import type {
  AppConfiguration,
  AppConfigurationPage,
} from '@domain/configuration/schemas/app-configuration.schema';
import type {
  CreateAppConfigurationPayload,
  UpdateAppConfigurationPayload,
} from '@domain/configuration/schemas/app-configuration.commands.schema';

// ── Query Hooks ───────────────────────────────────────────────────────────────

export function useGetAllAppConfigurations(
  params?: {
    page?: number;
    pageSize?: number;
    search?: string;
    criteria?: string;
    status?: string;
    sortBy?: string;
    sortOrder?: string;
    scope?: string;
    tenantId?: string;
    systemSuiteId?: string;
    moduleId?: string;
  },
  enabled = true
) {
  return useQuery<AppConfigurationPage, Error>({
    queryKey: ['app-configurations', 'list', params],
    queryFn: () => appConfigurationService.getAll(params),
    enabled: enabled && !!params?.page,
    ...CONTEXT_QUERY_CONFIG.APP_CONFIGURATION,
  });
}

export function useGetAppConfigurationById(appConfigurationId: string | null) {
  return useQuery<AppConfiguration, Error>({
    queryKey: ['app-configurations', 'detail', appConfigurationId],
    queryFn: () => appConfigurationService.getById(appConfigurationId!),
    enabled: !!appConfigurationId,
    ...CONTEXT_QUERY_CONFIG.APP_CONFIGURATION,
    ...getRetryOptions({ maxRetries: 1 }),
  });
}

// ── Mutation Hooks ────────────────────────────────────────────────────────────

export const useCreateAppConfiguration = () =>
  useNotifiedMutation({
    mutationFn: (payload: CreateAppConfigurationPayload) =>
      appConfigurationService.createAppConfiguration(payload),
    invalidateKeys: [['app-configurations']],
    successNotif: () => ({
      title: 'Configuración Creada',
      message: 'La configuración fue creada exitosamente.',
    }),
    errorNotif: () => ({
      title: 'Error al Crear Configuración',
      message: 'No se pudo crear la configuración.',
    }),
  });

export const useUpdateAppConfiguration = () =>
  useNotifiedMutation({
    mutationFn: ({
      id,
      payload,
      rowVersion,
    }: {
      id: string;
      payload: UpdateAppConfigurationPayload;
      rowVersion?: string;
    }) => appConfigurationService.updateAppConfiguration(id, payload, rowVersion),
    invalidateKeys: [
      ['app-configurations', 'detail', ''],
      ['app-configurations', 'list'],
    ],
    successNotif: () => ({
      title: 'Configuración Actualizada',
      message: 'La configuración fue actualizada exitosamente.',
    }),
    errorNotif: () => ({
      title: 'Error al Actualizar Configuración',
      message: 'No se pudo actualizar la configuración.',
    }),
  });

export const usePublishAppConfiguration = () =>
  useNotifiedMutation({
    mutationFn: (appConfigurationId: string) =>
      appConfigurationService.publishAppConfiguration(appConfigurationId),
    invalidateKeys: [['app-configurations']],
    successNotif: () => ({
      title: 'Configuración Publicada',
      message: 'La configuración fue publicada exitosamente.',
    }),
    errorNotif: () => ({
      title: 'Error al Publicar',
      message: 'No se pudo publicar la configuración.',
    }),
  });

export const useArchiveAppConfiguration = () =>
  useNotifiedMutation({
    mutationFn: (appConfigurationId: string) =>
      appConfigurationService.archiveAppConfiguration(appConfigurationId),
    invalidateKeys: [['app-configurations']],
    successNotif: () => ({
      title: 'Configuración Archivada',
      message: 'La configuración fue archivada exitosamente.',
    }),
    errorNotif: () => ({
      title: 'Error al Archivar',
      message: 'No se pudo archivar la configuración.',
    }),
  });

export const useDeleteAppConfiguration = () =>
  useNotifiedMutation({
    mutationFn: (appConfigurationId: string) =>
      appConfigurationService.deleteAppConfiguration(appConfigurationId),
    invalidateKeys: [['app-configurations']],
    successNotif: () => ({
      title: 'Configuración Eliminada',
      message: 'La configuración fue eliminada exitosamente.',
    }),
    errorNotif: () => ({
      title: 'Error al Eliminar',
      message: 'No se pudo eliminar la configuración.',
    }),
  });
