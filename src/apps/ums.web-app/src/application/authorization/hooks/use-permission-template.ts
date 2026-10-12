import { useCallback } from 'react';
import { useQuery } from '@tanstack/react-query';
import permissionTemplateService from '@infra/authorization/services/permission-template.service';
import { useNotifiedMutation } from '@app/hooks/use-notified-mutation';
import {
  type CreatePermissionTemplatePayload,
  type AddTemplateItemPayload,
  type ExclusiveArcTarget,
  type PermissionEffect,
  type PermissionTemplateItem,
  type PermissionTemplatePage,
  type PermissionTemplateDetail,
  isRetiredItem,
  itemStoredEffect,
} from '@domain/authorization/models/permission-template.model';
import { getHttpStatus, getRetryOptions } from '@app/utils/error-utils';
import { CONTEXT_QUERY_CONFIG } from '@app/shared/config/query.config';

// ─── Query params ────────────────────────────────────────────────────────────

export interface PermissionTemplateQueryParams {
  page: number;
  pageSize: number;
  search?: string;
  criteria?: string;
  status?: string;
  sortBy?: string;
  sortOrder?: 'asc' | 'desc';
  tenantId?: string;
  systemSuiteId?: string;
  roleId?: string;
}

// ─── Queries ─────────────────────────────────────────────────────────────────

export const useGetAllPermissionTemplates = (params: PermissionTemplateQueryParams | null) =>
  useQuery<PermissionTemplatePage>({
    queryKey: [
      'permission-templates',
      params?.page,
      params?.pageSize,
      params?.search,
      params?.criteria,
      params?.status,
      params?.sortBy,
      params?.sortOrder,
      params?.tenantId,
    ],
    queryFn: () => permissionTemplateService.getAll(params!),
    enabled: !!params,
    ...CONTEXT_QUERY_CONFIG.PERMISSION_TEMPLATE,
    ...getRetryOptions({ maxRetries: 1 }),
  });

export const useGetPermissionTemplate = (templateId: string | null) =>
  useQuery<PermissionTemplateDetail | null>({
    queryKey: ['permission-templates', templateId],
    queryFn: async () => {
      if (!templateId) throw new Error('Template ID required');
      try {
        return await permissionTemplateService.getById(templateId);
      } catch (err) {
        if (getHttpStatus(err) === 404) return null;
        throw err;
      }
    },
    enabled: !!templateId,
    ...CONTEXT_QUERY_CONFIG.PERMISSION_TEMPLATE,
    ...getRetryOptions({ maxRetries: 1 }),
  });

// ─── Template lifecycle mutations ─────────────────────────────────────────────

export const useCreatePermissionTemplate = () =>
  useNotifiedMutation({
    mutationFn: (payload: CreatePermissionTemplatePayload) =>
      permissionTemplateService.create(payload),
    invalidateKeys: [['permission-templates']],
    successNotif: () => ({
      title: 'Plantilla Creada',
      message: 'La plantilla de permisos fue creada en estado Borrador.',
    }),
    errorNotif: () => ({
      title: 'Error al Crear Plantilla',
      message: 'No se pudo crear la plantilla de permisos.',
    }),
  });

export const usePublishPermissionTemplate = (templateId: string) =>
  useNotifiedMutation({
    mutationFn: () => permissionTemplateService.publish(templateId),
    invalidateKeys: [['permission-templates', templateId], ['permission-templates']],
    successNotif: () => ({
      title: 'Plantilla Publicada',
      message: 'La plantilla fue publicada y está disponible para asignación a perfiles.',
    }),
    errorNotif: () => ({
      title: 'Error al Publicar',
      message: 'No se pudo publicar la plantilla. Verifique que esté en estado Borrador.',
    }),
  });

export const useDeprecatePermissionTemplate = (templateId: string) =>
  useNotifiedMutation({
    mutationFn: () => permissionTemplateService.deprecate(templateId),
    invalidateKeys: [['permission-templates', templateId], ['permission-templates']],
    successNotif: () => ({
      title: 'Plantilla Descontinuada',
      message: 'La plantilla fue descontinuada. Ya no puede asignarse a nuevos perfiles.',
      type: 'warning' as const,
    }),
    errorNotif: () => ({
      title: 'Error al Descontinuar',
      message: 'No se pudo descontinuar la plantilla.',
    }),
  });

export const useDeletePermissionTemplate = () =>
  useNotifiedMutation({
    mutationFn: (templateId: string) => permissionTemplateService.delete(templateId),
    invalidateKeys: [['permission-templates']],
    successNotif: () => ({
      title: 'Plantilla Eliminada',
      message: 'La plantilla de permisos fue eliminada.',
      type: 'warning' as const,
    }),
    errorNotif: () => ({
      title: 'Error al Eliminar',
      message:
        'No se pudo eliminar la plantilla. Solo se pueden eliminar plantillas en estado Borrador.',
    }),
  });

// ─── Item mutations ───────────────────────────────────────────────────────────

export const useAddTemplateItem = (templateId: string) =>
  useNotifiedMutation({
    mutationFn: (payload: AddTemplateItemPayload) =>
      permissionTemplateService.addItem(templateId, payload),
    invalidateKeys: [['permission-templates', templateId]],
    successNotif: () => ({
      title: 'Permiso Agregado',
      message: 'El ítem de permiso fue agregado a la plantilla.',
    }),
    errorNotif: () => ({
      title: 'Error al Agregar Permiso',
      message: 'No se pudo agregar el ítem de permiso.',
    }),
  });

/**
 * Retira el ítem: el DELETE conserva ruta y contrato pero ya es el verbo lógico (ADR-0164), así que
 * la fila sobrevive con `isActive: false` y sigue ocupando su clave (objetivo + acción). No sirve
 * para expresar «Neutral» —para eso está `useApplyTemplateItemEffect`— sino para dar de baja la
 * concesión entera.
 */
export const useRemoveTemplateItem = (templateId: string) =>
  useNotifiedMutation({
    mutationFn: (itemId: string) => permissionTemplateService.removeItem(templateId, itemId),
    invalidateKeys: [['permission-templates', templateId]],
    successNotif: () => ({
      title: 'Permiso Retirado',
      message: 'El ítem quedó retirado de la plantilla y ya no concede nada.',
      type: 'warning' as const,
    }),
    errorNotif: () => ({
      title: 'Error al Retirar Permiso',
      message: 'No se pudo retirar el ítem de permiso.',
    }),
  });

export const useSetTemplateItemEffect = (templateId: string) =>
  useNotifiedMutation({
    mutationFn: ({ itemId, effect }: { itemId: string; effect: 'Allow' | 'Deny' | 'Neutral' }) =>
      permissionTemplateService.setItemEffect(templateId, itemId, effect),
    invalidateKeys: [['permission-templates', templateId]],
    successNotif: () => ({
      title: 'Efecto Actualizado',
      message: 'El efecto del permiso fue actualizado.',
    }),
    errorNotif: () => ({
      title: 'Error al Actualizar Efecto',
      message: 'No se pudo actualizar el efecto.',
    }),
  });

export const useActivateTemplateItem = (templateId: string) =>
  useNotifiedMutation({
    mutationFn: (itemId: string) => permissionTemplateService.activateItem(templateId, itemId),
    invalidateKeys: [['permission-templates', templateId]],
    successNotif: () => ({ title: 'Ítem Activado', message: 'El ítem de permiso fue activado.' }),
    errorNotif: () => ({ title: 'Error al Activar', message: 'No se pudo activar el ítem.' }),
  });

export const useDeactivateTemplateItem = (templateId: string) =>
  useNotifiedMutation({
    mutationFn: (itemId: string) => permissionTemplateService.deactivateItem(templateId, itemId),
    invalidateKeys: [['permission-templates', templateId]],
    successNotif: () => ({
      title: 'Ítem Desactivado',
      message: 'El ítem de permiso fue desactivado.',
      type: 'info' as const,
    }),
    errorNotif: () => ({ title: 'Error al Desactivar', message: 'No se pudo desactivar el ítem.' }),
  });

// ─── Aplicación de efecto sobre una casilla (ADR-0164) ────────────────────────

/** Clave de la concesión: a qué objeto y con qué acción se refiere la casilla que se edita. */
export interface TemplateItemTarget {
  targetType: ExclusiveArcTarget;
  targetId: string;
  actionId: string;
}

export interface ApplyTemplateItemEffectArgs {
  /** Efecto que pide el usuario en la pantalla. */
  effect: PermissionEffect;
  /** Fila existente para esa clave, vigente o retirada; `undefined` si nunca se creó. */
  item?: PermissionTemplateItem | null;
  /** Datos del alta, usados solo cuando hay que crear la fila. */
  target: TemplateItemTarget;
}

/**
 * Traduce «quiero Allow/Deny/Neutral en esta casilla» a los verbos del backend, en un único sitio.
 *
 * Existe porque desde ADR-0164 la aritmética dejó de ser obvia:
 *  - `Neutral` NO es borrar. El ítem sobrevive; se le fija el efecto Neutral (PUT .../effect).
 *  - Sobre una fila retirada no se puede volver a hacer alta: la clave (objetivo, acción) sigue
 *    ocupada y el backend responde 409. Se reactiva (POST .../activate) y, si el efecto guardado
 *    no coincide con el pedido, se corrige.
 *  - Si el efecto pedido ya es el vigente no se escribe nada, para no emitir un toast que mienta.
 *
 * Devuelve `true` si hubo escritura, para que el llamador sepa si tiene algo que anunciar.
 */
export const useApplyTemplateItemEffect = (templateId: string) => {
  const addItem = useAddTemplateItem(templateId);
  const setEffect = useSetTemplateItemEffect(templateId);
  const activateItem = useActivateTemplateItem(templateId);

  const applyEffect = useCallback(
    async ({ effect, item, target }: ApplyTemplateItemEffectArgs): Promise<boolean> => {
      if (!item) {
        // No hay fila: solo se crea si el usuario pide un efecto real. «Neutral» es la ausencia
        // de opinión, y para eso basta con no tener fila.
        if (effect === 'Neutral') return false;
        await addItem.mutateAsync({
          ...target,
          isAllowed: effect === 'Allow',
          isDenied: effect === 'Deny',
        });
        return true;
      }

      if (isRetiredItem(item)) {
        // Una fila retirada ya no concede nada: pedirle Neutral no cambiaría el estado efectivo.
        if (effect === 'Neutral') return false;
        await activateItem.mutateAsync(item.itemId);
        if (itemStoredEffect(item) !== effect) {
          await setEffect.mutateAsync({ itemId: item.itemId, effect });
        }
        return true;
      }

      if (itemStoredEffect(item) === effect) return false;
      await setEffect.mutateAsync({ itemId: item.itemId, effect });
      return true;
    },
    [addItem, setEffect, activateItem]
  );

  return {
    applyEffect,
    isPending: addItem.isPending || setEffect.isPending || activateItem.isPending,
  };
};
