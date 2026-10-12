import { useQuery } from '@tanstack/react-query';
import { useNotifiedMutation } from '@app/hooks/use-notified-mutation';
import { parameterCatalogService } from '@infrastructure/configuration/services/parameter-catalog/parameter-catalog.service';
import type {
  ParameterDefinitionFilter,
  CreateParameterDefinitionPayload,
  UpdateParameterDefinitionPayload,
} from '@domain/configuration/schemas/parameter-catalog/parameter-definition.schema';

export function useParameterDefinitions(filter?: ParameterDefinitionFilter) {
  return useQuery({
    queryKey: ['parameter-definitions', filter],
    queryFn: () => parameterCatalogService.getAll(filter),
    staleTime: 5 * 60 * 1000,
  });
}

export function useParameterDefinitionById(id: string) {
  return useQuery({
    queryKey: ['parameter-definition', id],
    queryFn: () => parameterCatalogService.getById(id),
    enabled: !!id,
  });
}

// Los 3 mutadores usan `useNotifiedMutation` (G-146): es dueño único del toast success/error y
// de la invalidación → un rechazo del backend (p. ej. borrar un parámetro con valores dependientes
// → 409 `ParameterHasActiveValues`) ahora SÍ da feedback visual, en la línea de las demás cards.
export function useCreateParameterDefinition() {
  return useNotifiedMutation({
    mutationFn: (payload: CreateParameterDefinitionPayload) =>
      parameterCatalogService.createParameterDefinition(payload),
    invalidateKeys: [['parameter-definitions']],
    successNotif: () => ({
      title: 'Parámetro Creado',
      message: 'La definición de parámetro fue creada exitosamente.',
    }),
    errorNotif: () => ({
      title: 'Error al Crear Parámetro',
      message: 'No se pudo crear la definición de parámetro.',
    }),
  });
}

export function useUpdateParameterDefinition() {
  return useNotifiedMutation({
    mutationFn: ({ id, payload }: { id: string; payload: UpdateParameterDefinitionPayload }) =>
      parameterCatalogService.updateParameterDefinition(id, payload),
    // Refresca lista + detalle (prefijo `['parameter-definition']` matchea `['parameter-definition', id]`).
    invalidateKeys: [['parameter-definitions'], ['parameter-definition']],
    successNotif: () => ({
      title: 'Parámetro Actualizado',
      message: 'La definición de parámetro fue actualizada.',
    }),
    errorNotif: () => ({
      title: 'Error al Actualizar Parámetro',
      message: 'No se pudo actualizar la definición de parámetro.',
    }),
  });
}

export function useDeleteParameterDefinition() {
  return useNotifiedMutation({
    mutationFn: (id: string) => parameterCatalogService.deleteParameterDefinition(id),
    invalidateKeys: [['parameter-definitions']],
    successNotif: () => ({
      title: 'Parámetro Eliminado',
      message: 'La definición de parámetro fue eliminada.',
    }),
    errorNotif: () => ({
      title: 'Error al Eliminar Parámetro',
      message: 'No se pudo eliminar el parámetro (puede tener valores asociados).',
    }),
  });
}
