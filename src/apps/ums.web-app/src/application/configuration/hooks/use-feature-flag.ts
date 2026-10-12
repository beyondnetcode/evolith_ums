/**
 * use-feature-flag.ts — TanStack Query hooks for FeatureFlag
 *
 * All queries use REST via featureFlagService.
 * Mutations invalidate affected query keys and emit toast notifications.
 */
import { useQuery } from '@tanstack/react-query';
import featureFlagService from '@infra/configuration/services/feature-flag.service';
import { useNotifiedMutation } from '@app/hooks/use-notified-mutation';
import type { FeatureFlag, FeatureFlagPage } from '@domain/configuration/models/feature-flag.model';
import type {
  CreateFeatureFlagPayload,
  UpdateFeatureFlagPayload,
  AddFeatureFlagCriteriaPayload,
} from '@domain/configuration/models/feature-flag.model';
import { getHttpStatus, getRetryOptions } from '@app/utils/error-utils';
import { CONTEXT_QUERY_CONFIG } from '@app/shared/config/query.config';

// ── Query params ─────────────────────────────────────────────────────────────

export interface FeatureFlagQueryParams {
  page: number;
  pageSize: number;
  search?: string;
  criteria?: string;
  status?: string;
  sortBy?: string;
  sortOrder?: 'asc' | 'desc';
  flagType?: string;
}

// ── Queries ──────────────────────────────────────────────────────────────────

export const useGetAllFeatureFlags = (params: FeatureFlagQueryParams | null) =>
  useQuery<FeatureFlagPage>({
    queryKey: [
      'feature-flags',
      params?.page,
      params?.pageSize,
      params?.search,
      params?.criteria,
      params?.status,
      params?.sortBy,
      params?.sortOrder,
      params?.flagType,
    ],
    queryFn: () => featureFlagService.getAll(params!),
    enabled: !!params,
    ...CONTEXT_QUERY_CONFIG.FEATURE_FLAG,
    ...getRetryOptions({ maxRetries: 1 }),
  });

export const useGetFeatureFlagsBySystemSuite = (systemSuiteId: string | null) =>
  useQuery<FeatureFlag[]>({
    queryKey: ['feature-flags', 'by-suite', systemSuiteId],
    queryFn: async () => {
      if (!systemSuiteId) throw new Error('SystemSuite ID required');
      return featureFlagService.getFeatureFlagsBySystemSuite(systemSuiteId);
    },
    enabled: !!systemSuiteId,
    ...CONTEXT_QUERY_CONFIG.FEATURE_FLAG,
    ...getRetryOptions({ maxRetries: 1 }),
  });

export const useGetFeatureFlagById = (featureFlagId: string | null) =>
  useQuery<FeatureFlag | null>({
    queryKey: ['feature-flags', featureFlagId],
    queryFn: async () => {
      if (!featureFlagId) throw new Error('FeatureFlag ID required');
      try {
        return await featureFlagService.getById(featureFlagId);
      } catch (err: unknown) {
        if (getHttpStatus(err) === 404) return null;
        throw err;
      }
    },
    enabled: !!featureFlagId,
    ...CONTEXT_QUERY_CONFIG.FEATURE_FLAG,
    ...getRetryOptions({ maxRetries: 1 }),
  });

// ── Mutations ────────────────────────────────────────────────────────────────

export const useCreateFeatureFlag = () =>
  useNotifiedMutation({
    mutationFn: (payload: CreateFeatureFlagPayload) =>
      featureFlagService.createFeatureFlag(payload),
    invalidateKeys: [['feature-flags']],
    successNotif: data => ({
      title: 'Feature Flag Creado',
      message: `Flag registrado con ID ${data.featureFlagId.slice(0, 8)}…`,
    }),
    errorNotif: () => ({
      title: 'Error al Crear Flag',
      message: 'No se pudo registrar el feature flag.',
    }),
  });

export const useUpdateFeatureFlag = (featureFlagId: string) =>
  useNotifiedMutation({
    mutationFn: (payload: UpdateFeatureFlagPayload) =>
      featureFlagService.updateFeatureFlag(featureFlagId, payload),
    invalidateKeys: [['feature-flags', featureFlagId], ['feature-flags']],
    successNotif: () => ({
      title: 'Feature Flag Actualizado',
      message: 'Las propiedades del flag fueron actualizadas.',
    }),
    errorNotif: () => ({
      title: 'Error al Actualizar Flag',
      message: 'No se pudo actualizar el feature flag.',
    }),
  });

export const useActivateFlag = (featureFlagId: string) =>
  useNotifiedMutation({
    mutationFn: () => featureFlagService.activateFlag(featureFlagId),
    invalidateKeys: [['feature-flags', featureFlagId], ['feature-flags']],
    successNotif: () => ({
      title: 'Flag Activado',
      message: 'El feature flag está ahora activo.',
    }),
    errorNotif: () => ({
      title: 'Error al Activar Flag',
      message: 'No se pudo activar el feature flag.',
    }),
  });

export const useDeactivateFlag = (featureFlagId: string) =>
  useNotifiedMutation({
    mutationFn: () => featureFlagService.deactivateFlag(featureFlagId),
    invalidateKeys: [['feature-flags', featureFlagId], ['feature-flags']],
    successNotif: () => ({
      title: 'Flag Desactivado',
      message: 'El feature flag fue desactivado.',
      type: 'info' as const,
    }),
    errorNotif: () => ({
      title: 'Error al Desactivar Flag',
      message: 'No se pudo desactivar el feature flag.',
    }),
  });

export const useArchiveFlag = (featureFlagId: string) =>
  useNotifiedMutation({
    mutationFn: () => featureFlagService.archiveFlag(featureFlagId),
    invalidateKeys: [['feature-flags', featureFlagId], ['feature-flags']],
    successNotif: () => ({
      title: 'Flag Archivado',
      message: 'El feature flag fue archivado (estado terminal).',
      type: 'warning' as const,
    }),
    errorNotif: () => ({
      title: 'Error al Archivar Flag',
      message: 'No se pudo archivar el feature flag.',
    }),
  });

export const useAddFeatureFlagCriteria = (featureFlagId: string) =>
  useNotifiedMutation({
    mutationFn: (payload: AddFeatureFlagCriteriaPayload) =>
      featureFlagService.addCriteria(featureFlagId, payload),
    invalidateKeys: [
      ['feature-flags', featureFlagId],
      ['feature-flags', 'by-suite'],
    ],
    successNotif: () => ({
      title: 'Criterio Agregado',
      message: 'El criterio de evaluación fue registrado.',
    }),
    errorNotif: () => ({
      title: 'Error al Agregar Criterio',
      message: 'No se pudo agregar el criterio.',
    }),
  });

export const useRemoveFeatureFlagCriteria = (featureFlagId: string) =>
  useNotifiedMutation({
    mutationFn: (criteriaId: string) =>
      featureFlagService.removeCriteria(featureFlagId, criteriaId),
    invalidateKeys: [
      ['feature-flags', featureFlagId],
      ['feature-flags', 'by-suite'],
    ],
    successNotif: () => ({
      title: 'Criterio Eliminado',
      message: 'El criterio de evaluación fue removido.',
      type: 'warning' as const,
    }),
    errorNotif: () => ({
      title: 'Error al Eliminar Criterio',
      message: 'No se pudo eliminar el criterio.',
    }),
  });
