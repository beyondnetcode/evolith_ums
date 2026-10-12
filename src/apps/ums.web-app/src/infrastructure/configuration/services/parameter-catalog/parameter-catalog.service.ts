import { httpClient } from '@infra/http/httpClient';
import { logger } from '@app/utils/logger';
import {
  ParameterDefinitionSchema,
  ParameterDefinitionPageSchema,
  type ParameterDefinition,
  type ParameterDefinitionPage,
  type ParameterDefinitionFilter,
  type CreateParameterDefinitionPayload,
  type UpdateParameterDefinitionPayload,
} from '@domain/configuration/schemas/parameter-catalog/parameter-definition.schema';

export const parameterCatalogService = {
  getAll: async (filter?: ParameterDefinitionFilter): Promise<ParameterDefinitionPage> => {
    const params = new URLSearchParams();
    if (filter?.search) params.set('search', filter.search);
    if (filter?.scopeId) params.set('scopeId', String(filter.scopeId));
    if (filter?.isActive !== undefined) params.set('isActive', String(filter.isActive));

    try {
      const { data } = await httpClient.get<{ items: ParameterDefinition[]; totalItems: number }>(
        `/parameter-definitions?${params.toString()}`
      );
      const result = ParameterDefinitionPageSchema.safeParse(data);
      if (!result.success) {
        logger.error('Invalid response for parameter definitions', result.error);
        throw new Error('Invalid response for parameter definitions');
      }
      return result.data;
    } catch (error) {
      logger.error('Failed to fetch parameter definitions', error);
      throw error;
    }
  },

  getById: async (id: string): Promise<ParameterDefinition> => {
    const { data } = await httpClient.get<ParameterDefinition>(`/parameter-definitions/${id}`);
    return ParameterDefinitionSchema.parse(data);
  },

  createParameterDefinition: async (
    payload: CreateParameterDefinitionPayload
  ): Promise<ParameterDefinition> => {
    try {
      // El endpoint responde 201 con SOLO el Guid (`.Produces<Guid>`); no es la entidad completa,
      // así que parsearla como ParameterDefinition lanzaba. Se relee con getById para devolver la
      // entidad fresca que consumen el diálogo de alta y el caché. (mismo patrón que G-141)
      const { data } = await httpClient.post<string>('/parameter-definitions', payload);
      const id = typeof data === 'string' ? data : (data as { id: string }).id;
      return parameterCatalogService.getById(id);
    } catch (error) {
      logger.error('Failed to create parameter definition', error);
      throw error;
    }
  },

  updateParameterDefinition: async (
    id: string,
    payload: UpdateParameterDefinitionPayload
  ): Promise<ParameterDefinition> => {
    try {
      // El endpoint es `PUT /{id}` y devuelve 204 NoContent (sin cuerpo); un PATCH daba 405 y,
      // aun corrigiendo el verbo, no hay cuerpo que parsear. Tras el PUT se relee con getById
      // para devolver la entidad fresca que el hook (`useUpdateParameterDefinition`) cachea. (G-141)
      await httpClient.put(`/parameter-definitions/${id}`, payload);
      return parameterCatalogService.getById(id);
    } catch (error) {
      logger.error('Failed to update parameter definition', error);
      throw error;
    }
  },

  deleteParameterDefinition: async (id: string): Promise<void> => {
    try {
      await httpClient.delete(`/parameter-definitions/${id}`);
    } catch (error) {
      logger.error('Failed to delete parameter definition', error);
      throw error;
    }
  },

  clearCache: () => {},
};

export default parameterCatalogService;
