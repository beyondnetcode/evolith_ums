/**
 * system-suite.service.ts
 *
 * Infrastructure service for the Authorization / SystemSuite bounded context.
 * Queries and commands use REST via httpClient.
 * All responses are validated at runtime with Zod before returning.
 */
import { httpClient } from '@infra/http/httpClient';
import { logger } from '@app/utils/logger';
import {
  SystemSuitePageSchema,
  SystemSuiteSchema,
  CreateSystemSuiteResponseSchema,
  type SystemSuite,
  type SystemSuitePage,
  type CreateSystemSuitePayload,
  type CreateSystemSuiteResponse,
} from '@domain/authorization/schemas/system-suite.schema';

export const systemSuiteService = {
  // ── Queries (REST) ────────────────────────────────────────────────────────────────────────────────────────────────────────────

  getAll: async (params?: {
    page?: number;
    pageSize?: number;
    search?: string;
    criteria?: string;
    status?: string;
    sortBy?: string;
    sortOrder?: string;
    tenantId?: string;
  }): Promise<SystemSuitePage> => {
    const queryParams = new URLSearchParams();
    if (params?.page !== undefined) queryParams.append('page', params.page.toString());
    if (params?.pageSize !== undefined) queryParams.append('pageSize', params.pageSize.toString());
    if (params?.search) queryParams.append('search', params.search);
    if (params?.criteria) queryParams.append('criteria', params.criteria);
    if (params?.status) queryParams.append('status', params.status);
    if (params?.sortBy) queryParams.append('sortBy', params.sortBy);
    if (params?.sortOrder) queryParams.append('sortOrder', params.sortOrder);
    if (params?.tenantId) queryParams.append('tenantId', params.tenantId);

    const { data } = await httpClient.get(`/system-suites?${queryParams.toString()}`);

    const pageResult = SystemSuitePageSchema.safeParse(data);
    if (!pageResult.success) {
      logger.error('Invalid REST response shape for system suites query', pageResult.error);
      throw new Error('Invalid REST response shape for system suites query');
    }
    return pageResult.data;
  },

  // Backward-compatible aliases used by existing hooks/tests while the UI migrates.
  getAllSystemSuites: async (params?: {
    page?: number;
    pageSize?: number;
    search?: string;
    criteria?: string;
    status?: string;
    sortBy?: string;
    sortOrder?: string;
    tenantId?: string;
  }): Promise<SystemSuitePage> => systemSuiteService.getAll(params),

  getById: async (systemSuiteId: string): Promise<SystemSuite> => {
    const { data } = await httpClient.get(`/system-suites/${systemSuiteId}`);
    if (!data) throw new Error('SystemSuite not found');
    return SystemSuiteSchema.parse(data);
  },

  getSystemSuiteById: async (systemSuiteId: string): Promise<SystemSuite> =>
    systemSuiteService.getById(systemSuiteId),

  // ── Commands (REST) ───────────────────────────────────────────────────────

  createSystemSuite: async (
    payload: CreateSystemSuitePayload
  ): Promise<CreateSystemSuiteResponse> => {
    const { data } = await httpClient.post('/system-suites', payload);
    return CreateSystemSuiteResponseSchema.parse(data);
  },

  updateSystemSuite: async (
    systemSuiteId: string,
    name: string,
    description: string
  ): Promise<void> => {
    await httpClient.put(`/system-suites/${systemSuiteId}`, { name, description });
  },

  setSystemSuiteStatus: async (systemSuiteId: string, status: string): Promise<void> => {
    // El endpoint es `PUT /{id}/status` y liga el estado desde el CUERPO JSON
    // (`SetSystemSuiteStatusPayload { Status }`), no desde query. Un POST con query param
    // fallaba siempre (método/contrato incompatible) → el cambio de estado nunca surtía efecto.
    await httpClient.put(`/system-suites/${systemSuiteId}/status`, { status });
  },

  // ── Module Lifecycle REST Commands ─────────────────────────────────────────

  addModule: async (
    systemSuiteId: string,
    payload: { code: string; name: string; description?: string; sortOrder: number }
  ): Promise<void> => {
    await httpClient.post(`/system-suites/${systemSuiteId}/modules`, {
      ...payload,
      description: payload.description?.trim() ?? '',
    });
  },

  updateModule: async (
    systemSuiteId: string,
    moduleId: string,
    payload: { name: string; description?: string; sortOrder: number }
  ): Promise<void> => {
    await httpClient.put(`/system-suites/${systemSuiteId}/modules/${moduleId}`, {
      ...payload,
      description: payload.description?.trim() ?? '',
    });
  },

  removeModule: async (systemSuiteId: string, moduleId: string): Promise<void> => {
    await httpClient.delete(`/system-suites/${systemSuiteId}/modules/${moduleId}`);
  },

  activateModule: async (systemSuiteId: string, moduleId: string): Promise<void> => {
    await httpClient.post(`/system-suites/${systemSuiteId}/modules/${moduleId}/activate`);
  },

  deactivateModule: async (systemSuiteId: string, moduleId: string): Promise<void> => {
    await httpClient.post(`/system-suites/${systemSuiteId}/modules/${moduleId}/deactivate`);
  },

  // ── Node Lifecycle REST Commands (árbol recursivo, ADR-0090) ──────────────

  addNode: async (
    systemSuiteId: string,
    moduleId: string,
    payload: {
      parentNodeId?: string | null;
      kind: string;
      code: string;
      label: string;
      description?: string;
      sortOrder: number;
    }
  ): Promise<void> => {
    await httpClient.post(`/system-suites/${systemSuiteId}/modules/${moduleId}/nodes`, {
      parentNodeId: payload.parentNodeId ?? null,
      kind: payload.kind,
      code: payload.code,
      label: payload.label,
      description: payload.description?.trim() ?? '',
      sortOrder: payload.sortOrder,
    });
  },

  updateNode: async (
    systemSuiteId: string,
    moduleId: string,
    nodeId: string,
    payload: { label: string; description?: string; sortOrder: number }
  ): Promise<void> => {
    await httpClient.put(`/system-suites/${systemSuiteId}/modules/${moduleId}/nodes/${nodeId}`, {
      label: payload.label,
      description: payload.description?.trim() ?? '',
      sortOrder: payload.sortOrder,
    });
  },

  removeNode: async (systemSuiteId: string, moduleId: string, nodeId: string): Promise<void> => {
    await httpClient.delete(`/system-suites/${systemSuiteId}/modules/${moduleId}/nodes/${nodeId}`);
  },

  setNodeStatus: async (
    systemSuiteId: string,
    moduleId: string,
    nodeId: string,
    active: boolean
  ): Promise<void> => {
    await httpClient.put(
      `/system-suites/${systemSuiteId}/modules/${moduleId}/nodes/${nodeId}/status`,
      { active }
    );
  },

  linkNodeAction: async (
    systemSuiteId: string,
    moduleId: string,
    nodeId: string,
    actionCode: string
  ): Promise<void> => {
    await httpClient.post(
      `/system-suites/${systemSuiteId}/modules/${moduleId}/nodes/${nodeId}/actions`,
      { actionCode }
    );
  },

  unlinkNodeAction: async (
    systemSuiteId: string,
    moduleId: string,
    nodeId: string,
    actionCode: string
  ): Promise<void> => {
    await httpClient.delete(
      `/system-suites/${systemSuiteId}/modules/${moduleId}/nodes/${nodeId}/actions/${encodeURIComponent(
        actionCode
      )}`
    );
  },

  setNodeMetadata: async (
    systemSuiteId: string,
    moduleId: string,
    nodeId: string,
    payload: {
      responsable?: string | null;
      criticidad?: string | null;
      productoImpactado?: string | null;
      componenteTecnico?: string | null;
      dependencias?: string | null;
      evidencias?: string | null;
      trazabilidadSdlc?: string | null;
    }
  ): Promise<void> => {
    await httpClient.put(
      `/system-suites/${systemSuiteId}/modules/${moduleId}/nodes/${nodeId}/metadata`,
      payload
    );
  },

  // ── Action Registry REST Commands ─────────────────────────────────────────

  registerAction: async (
    systemSuiteId: string,
    payload: { code: string; name: string }
  ): Promise<void> => {
    await httpClient.post(`/system-suites/${systemSuiteId}/actions`, payload);
  },

  renameAction: async (systemSuiteId: string, code: string, name: string): Promise<void> => {
    await httpClient.put(`/system-suites/${systemSuiteId}/actions/${code}`, { name });
  },

  removeAction: async (systemSuiteId: string, code: string): Promise<void> => {
    await httpClient.delete(`/system-suites/${systemSuiteId}/actions/${code}`);
  },

  // ── Domain Resources REST Commands ────────────────────────────────────────

  addDomainResource: async (
    systemSuiteId: string,
    payload: {
      moduleId?: string | null;
      parentResourceId?: string | null;
      type: 'Aggregate' | 'Entity' | 'DomainMethod';
      code: string;
      name: string;
      description: string;
    }
  ): Promise<void> => {
    await httpClient.post(`/system-suites/${systemSuiteId}/domain-resources`, payload);
  },

  updateDomainResource: async (
    systemSuiteId: string,
    domainResourceId: string,
    payload: { name: string; description: string }
  ): Promise<void> => {
    await httpClient.put(
      `/system-suites/${systemSuiteId}/domain-resources/${domainResourceId}`,
      payload
    );
  },

  removeDomainResource: async (systemSuiteId: string, domainResourceId: string): Promise<void> => {
    await httpClient.delete(`/system-suites/${systemSuiteId}/domain-resources/${domainResourceId}`);
  },
};

export default systemSuiteService;
