// Aprovisionamiento de data de prueba vía API para el arnés RoboSoft (carril B).
//
// Todo se crea con identificadores/códigos ÚNICOS por corrida (idempotente y re-corrible:
// re-ejecutar no colisiona 409 contra data previa) y de forma determinista. No se destruye la
// semilla. La data se genera con semántica del dominio logístico EVOLITH (SD-08 + memoria de
// datos sintéticos), no genérica.
import { randomUUID } from 'node:crypto';
import { request as contextoApi } from '@playwright/test';
import type { APIRequestContext, APIResponse } from '@playwright/test';
import { API, EVOLITH_TENANT_ID, EVOLITH_ADMIN_USER_ID, devAuthHeaders, type DevIdentity, type Credenciales } from './auth';

/** GUID nuevo (para userId de documentos, roleId/systemId de solicitudes, etc.). */
export const uid = (): string => randomUUID();

/** Sufijo único por corrida para códigos legibles y sin colisión. Solo [A-Za-z0-9_]: algunos
 * validadores (módulo/rol de suite) exigen letras, números y guion bajo — nada de guiones. */
const RUN = `${Date.now().toString(36)}_${Math.floor(Math.random() * 1e4)}`;
let seq = 0;
export const codigoUnico = (prefijo: string): string => `${prefijo}_${RUN}_${++seq}`;

const iso = (d: Date): string => d.toISOString();
export const enUnAno = (): string => iso(new Date(Date.now() + 365 * 864e5));
export const hoy = (): string => iso(new Date());
export const enUnDia = (): string => iso(new Date(Date.now() + 864e5));

/** Parámetros de paginación completos (los GET de este contexto los exigen; faltarlos → 400). */
const pagina = (extra: Record<string, string | number | undefined> = {}): string => {
  const base: Record<string, string | number> = {
    page: 1,
    pageSize: 100,
    criteria: 'status',
    status: 'all',
    sortBy: 'status',
    sortOrder: 'asc',
  };
  const params = new URLSearchParams();
  for (const [k, v] of Object.entries({ ...base, ...extra })) {
    if (v !== undefined) params.set(k, String(v));
  }
  return params.toString();
};

// ─────────────────────────── Document Types ───────────────────────────

export async function crearTipoDocumento(
  request: APIRequestContext,
  criticidad: 'Low' | 'Medium' | 'High' | 'Critical' = 'High',
): Promise<string> {
  const res = await request.post(`${API}/document-types`, {
    headers: devAuthHeaders(),
    data: {
      tenantId: EVOLITH_TENANT_ID,
      code: codigoUnico('DT_ADUANA'),
      name: 'Certificado de operador aduanero',
      description: 'Documento habilitante del agente de aduanas (RoboSoft)',
      criticity: criticidad,
    },
  });
  if (res.status() !== 201) throw new Error(`crearTipoDocumento ${res.status()}: ${await res.text()}`);
  return (await res.json()).documentTypeId as string;
}

// ─────────────────────────── User Documents ───────────────────────────

export interface DocumentoUsuarioOpts {
  userId?: string;
  documentTypeId: string;
  issueDate?: string;
  expirationDate?: string;
  criticity?: 'Low' | 'Medium' | 'High' | 'Critical';
  fileChecksum?: string;
}

export async function subirDocumentoUsuario(
  request: APIRequestContext,
  opts: DocumentoUsuarioOpts,
): Promise<{ id: string; userId: string; res: APIResponse }> {
  const userId = opts.userId ?? uid();
  const res = await request.post(`${API}/user-documents`, {
    headers: devAuthHeaders(),
    data: {
      userId,
      documentTypeId: opts.documentTypeId,
      issueDate: opts.issueDate ?? hoy(),
      expirationDate: opts.expirationDate ?? enUnAno(),
      criticity: opts.criticity ?? 'High',
      fileStoragePath: `/robosoft/${codigoUnico('doc')}.pdf`,
      fileChecksum: opts.fileChecksum ?? `sha256-${uid()}`,
    },
  });
  let id = '';
  if (res.status() === 201) id = (await res.json()).userDocumentId as string;
  return { id, userId, res };
}

export const validarDocumento = (request: APIRequestContext, id: string): Promise<APIResponse> =>
  request.post(`${API}/user-documents/${id}/validate`, { headers: devAuthHeaders() });

export const rechazarDocumento = (request: APIRequestContext, id: string, motivo = 'Ilegible'): Promise<APIResponse> =>
  request.post(`${API}/user-documents/${id}/reject`, {
    headers: devAuthHeaders(),
    data: { rejectionReason: motivo },
  });

export const expirarDocumento = (request: APIRequestContext, id: string): Promise<APIResponse> =>
  request.post(`${API}/user-documents/${id}/expire`, { headers: devAuthHeaders() });

export const reSubirDocumento = (
  request: APIRequestContext,
  id: string,
  checksum = `sha256-${uid()}`,
): Promise<APIResponse> =>
  request.post(`${API}/user-documents/${id}/re-upload`, {
    headers: devAuthHeaders(),
    data: {
      newIssueDate: hoy(),
      newExpirationDate: enUnAno(),
      newFileStoragePath: `/robosoft/${codigoUnico('reupload')}.pdf`,
      newFileChecksum: checksum,
    },
  });

/** Estado actual de un documento de usuario (lee el read model paginado). */
export async function estadoDocumento(
  request: APIRequestContext,
  userId: string,
  documentId: string,
): Promise<string | undefined> {
  const res = await request.get(`${API}/user-documents?${pagina({ userId })}`, { headers: devAuthHeaders() });
  const body = await res.json();
  const item = (body.items ?? []).find((i: { userDocumentId: string }) => i.userDocumentId === documentId);
  return item?.status;
}

// ─────────────────────────── Approval Workflows ───────────────────────────

export async function crearWorkflow(
  request: APIRequestContext,
  opts: { requiresApproval?: boolean; targetUserCategory?: 'Internal' | 'Client' } = {},
): Promise<string> {
  const res = await request.post(`${API}/approval-workflows`, {
    headers: devAuthHeaders(),
    data: {
      tenantId: EVOLITH_TENANT_ID,
      systemSuiteId: null,
      code: codigoUnico('WF_ADUANAS'),
      name: 'Alta de perfil — agente de aduanas',
      description: 'Flujo de aprobación de perfil para operaciones aduaneras (RoboSoft)',
      targetUserCategory: opts.targetUserCategory ?? 'Internal',
      requiresApproval: opts.requiresApproval ?? true,
    },
  });
  if (res.status() !== 201) throw new Error(`crearWorkflow ${res.status()}: ${await res.text()}`);
  return (await res.json()).approvalWorkflowId as string;
}

export const agregarDocumentoRequerido = (
  request: APIRequestContext,
  workflowId: string,
  documentTypeId: string,
  isMandatory = true,
): Promise<APIResponse> =>
  request.post(`${API}/approval-workflows/${workflowId}/required-documents`, {
    headers: devAuthHeaders(),
    data: { documentTypeId, isMandatory },
  });

export const quitarDocumentoRequerido = (
  request: APIRequestContext,
  workflowId: string,
  requiredDocumentId: string,
): Promise<APIResponse> =>
  request.delete(`${API}/approval-workflows/${workflowId}/required-documents/${requiredDocumentId}`, {
    headers: devAuthHeaders(),
  });

export interface WorkflowDto {
  approvalWorkflowId: string;
  requiredDocuments?: Array<{ requiredDocumentId: string; documentTypeId: string; isMandatory: boolean }>;
}

// Búsqueda paginada por id: las listas de este contexto no ofrecen filtro por id y crecen sin
// límite (un backend compartido acumula data de muchas corridas). Se recorren páginas hasta hallar
// el elemento o agotar, garantizando determinismo sin depender de que caiga en la primera página.
async function buscarPaginado<T>(
  request: APIRequestContext,
  ruta: string,
  baseParams: Record<string, string>,
  coincide: (item: T) => boolean,
  maxPaginas = 60,
): Promise<T | undefined> {
  for (let page = 1; page <= maxPaginas; page++) {
    const q = new URLSearchParams({ ...baseParams, page: String(page) }).toString();
    const res = await request.get(`${ruta}?${q}`, { headers: devAuthHeaders() });
    const body = await res.json();
    const items: T[] = body.items ?? [];
    const hit = items.find(coincide);
    if (hit) return hit;
    if (page >= (body.totalPages ?? 1) || items.length === 0) break;
  }
  return undefined;
}

export function obtenerWorkflow(
  request: APIRequestContext,
  workflowId: string,
): Promise<WorkflowDto | undefined> {
  return buscarPaginado<WorkflowDto>(
    request,
    `${API}/approval-workflows`,
    { pageSize: '200', criteria: 'name', sortBy: 'name', sortOrder: 'asc', tenantId: EVOLITH_TENANT_ID },
    (i) => i.approvalWorkflowId === workflowId,
  );
}

// ─────────────────────────── Approval Requests ───────────────────────────

export interface SolicitudOpts {
  workflowId: string;
  targetUserId: string;
  requestedSystemId?: string;
  requestedRoleId?: string;
  justification?: string;
  /** Identidad del solicitante (createdBy). Único por corrida para observar SoD. */
  solicitante: DevIdentity;
}

export async function crearSolicitud(
  request: APIRequestContext,
  opts: SolicitudOpts,
): Promise<{ id: string; roleId: string; systemId: string; res: APIResponse }> {
  const roleId = opts.requestedRoleId ?? uid();
  const systemId = opts.requestedSystemId ?? uid();
  const res = await request.post(`${API}/approval-requests`, {
    headers: devAuthHeaders(opts.solicitante),
    data: {
      workflowId: opts.workflowId,
      targetUserId: opts.targetUserId,
      targetProfileId: null,
      requestedSystemId: systemId,
      requestedBranchId: null,
      requestedRoleId: roleId,
      justification: opts.justification ?? 'Solicitud RoboSoft',
    },
  });
  let id = '';
  if (res.status() === 201) id = (await res.json()).approvalRequestId as string;
  return { id, roleId, systemId, res };
}

/** Aprueba. Sin `aprobador` usa la identidad DevAuth por defecto (admin EVOLITH, propietario de gestión). */
export const aprobarSolicitud = (
  request: APIRequestContext,
  id: string,
  grantedRoleId: string,
  aprobador: DevIdentity = {},
): Promise<APIResponse> =>
  request.post(`${API}/approval-requests/${id}/approve`, {
    headers: devAuthHeaders(aprobador),
    data: { grantedRoleId },
  });

export const rechazarSolicitud = (
  request: APIRequestContext,
  id: string,
  aprobador: DevIdentity = {},
  motivo = 'No procede',
): Promise<APIResponse> =>
  request.post(`${API}/approval-requests/${id}/reject`, {
    headers: devAuthHeaders(aprobador),
    data: { decisionReason: motivo },
  });

export async function estadoSolicitud(
  request: APIRequestContext,
  id: string,
): Promise<string | undefined> {
  const q = new URLSearchParams({
    page: '1',
    pageSize: '200',
    criteria: 'status',
    status: 'all',
    sortBy: 'status',
    sortOrder: 'asc',
  }).toString();
  const res = await request.get(`${API}/approval-requests?${q}`, { headers: devAuthHeaders() });
  const body = await res.json();
  const item = (body.items ?? []).find((i: { approvalRequestId: string }) => i.approvalRequestId === id);
  return item?.status;
}

/**
 * Lista las solicitudes filtrando por usuario objetivo (`userId`). G-159: el backend ahora
 * aplica ese filtro por `TargetUserId`. Devuelve los `targetUserId` de la primera página
 * (suficiente para aseverar aislamiento con un usuario recién provisionado).
 */
export async function listarSolicitudesDeUsuario(
  request: APIRequestContext,
  userId: string,
): Promise<{ status: number; targetUserIds: string[]; total: number }> {
  const q = new URLSearchParams({ page: '1', pageSize: '200', userId }).toString();
  const res = await request.get(`${API}/approval-requests?${q}`, { headers: devAuthHeaders() });
  const body = await res.json();
  const items: { targetUserId: string }[] = body.items ?? [];
  return { status: res.status(), targetUserIds: items.map((i) => i.targetUserId), total: body.totalItems ?? items.length };
}

// ─────────────────────────── Access Enforcement Policies ───────────────────────────

export interface PoliticaOpts {
  profileId?: string | null;
  roleId?: string | null;
  enforcementAction?: 'BlockUser' | 'RestrictProfile' | 'LogOnly';
  gracePeriodDays?: number;
}

export async function crearPolitica(
  request: APIRequestContext,
  opts: PoliticaOpts,
): Promise<{ id: string; res: APIResponse }> {
  const data: Record<string, unknown> = {
    tenantId: EVOLITH_TENANT_ID,
    profileId: opts.profileId ?? null,
    roleId: opts.roleId === undefined ? uid() : opts.roleId,
    enforcementAction: opts.enforcementAction ?? 'RestrictProfile',
  };
  if (opts.gracePeriodDays !== undefined) data.gracePeriodDays = opts.gracePeriodDays;
  const res = await request.post(`${API}/access-enforcement-policies`, { headers: devAuthHeaders(), data });
  let id = '';
  if (res.status() === 201) id = (await res.json()).accessEnforcementPolicyId as string;
  return { id, res };
}

export const desactivarPolitica = (request: APIRequestContext, id: string): Promise<APIResponse> =>
  request.post(`${API}/access-enforcement-policies/${id}/deactivate`, { headers: devAuthHeaders() });

export function obtenerPolitica(
  request: APIRequestContext,
  id: string,
): Promise<Record<string, unknown> | undefined> {
  return buscarPaginado<Record<string, unknown>>(
    request,
    `${API}/access-enforcement-policies`,
    {
      pageSize: '200',
      criteria: 'enforcementAction',
      status: 'all',
      sortBy: 'enforcementAction',
      sortOrder: 'asc',
      tenantId: EVOLITH_TENANT_ID,
    },
    (i) => i.accessEnforcementPolicyId === id,
  );
}

// ─────────────────────────── Cuentas desechables (para tests destructivos) ───────────────────────────
//
// REGLA CRÍTICA: cualquier invariante destructiva (lockout, bloqueo, intentos fallidos, cambio de
// contraseña) DEBE ejercerse sobre una cuenta propia y desechable, NUNCA sobre admin@ ni cuentas
// sembradas — hacerlo bloquearía el clúster compartido. Este helper crea un UserAccount nuevo (id
// único por corrida) en EVOLITH, lo activa y le fija una contraseña, devolviendo credenciales usables.

export interface CuentaDesechable extends Credenciales {
  userId: string;
}

export async function provisionarCuentaDesechable(
  request: APIRequestContext,
  password = `Robosoft.${randomUUID()}!Aa1`,
): Promise<CuentaDesechable> {
  const email = `robosoft.desechable.${codigoUnico('u').toLowerCase()}@evolith.example`;
  const crear = await request.post(`${API}/user-accounts`, {
    headers: devAuthHeaders(),
    data: {
      tenantId: EVOLITH_TENANT_ID,
      branchId: null,
      email,
      category: 'Internal',
      identityReference: null,
      identityReferenceType: null,
    },
  });
  if (crear.status() !== 201) throw new Error(`crear cuenta desechable ${crear.status()}: ${await crear.text()}`);
  const userId = (await crear.json()).userAccountId as string;
  const activar = await request.post(`${API}/user-accounts/${userId}/activate`, { headers: devAuthHeaders() });
  if (activar.status() !== 204) throw new Error(`activar cuenta desechable ${activar.status()}`);
  const pass = await request.post(`${API}/user-accounts/${userId}/passwords`, {
    headers: devAuthHeaders(),
    data: { userAccountId: userId, password },
  });
  if (pass.status() !== 201) throw new Error(`fijar contraseña ${pass.status()}: ${await pass.text()}`);
  return { userId, tenantCode: 'EVOLITH', username: email, password };
}

/**
 * Provisiona un usuario interno DESECHABLE (crear + activar) y devuelve su id. Pensado como
 * OBJETIVO de solicitudes de aprobación: aprobar una solicitud materializa un Profile en el
 * usuario objetivo, por lo que NUNCA debe apuntarse a admin@ ni a cuentas sembradas (se
 * acumularían perfiles con roles aleatorios y se corrompería su login → AUTH_000). Cada test
 * usa su propio usuario objetivo, aislado y desechable.
 */
export async function provisionarUsuarioInterno(request: APIRequestContext): Promise<string> {
  const email = `robosoft.objetivo.${codigoUnico('t').toLowerCase()}@evolith.example`;
  const crear = await request.post(`${API}/user-accounts`, {
    headers: devAuthHeaders(),
    data: {
      tenantId: EVOLITH_TENANT_ID,
      branchId: null,
      email,
      category: 'Internal',
      identityReference: null,
      identityReferenceType: null,
    },
  });
  if (crear.status() !== 201) throw new Error(`provisionarUsuarioInterno ${crear.status()}: ${await crear.text()}`);
  const userId = (await crear.json()).userAccountId as string;
  await request.post(`${API}/user-accounts/${userId}/activate`, { headers: devAuthHeaders() });
  await darlePermisosDeAdministrador(request, userId);
  return userId;
}

/**
 * Una identidad que EXISTE y tiene permisos, para actuar en las pruebas (G-280).
 *
 * El arnés venía fabricando identidades con `{ userId: uid() }` — un GUID al azar que no
 * corresponde a ninguna cuenta—. Con el atajo de Admin Root encendido daba igual: la cabecera
 * `X-Is-Internal-Admin` bastaba para atravesar los 182 manejadores guardados. Con el atajo apagado
 * ese usuario no existe, no tiene grafo y no tiene un solo scope, así que la petición muere en el
 * guardia y la prueba falla por su andamiaje y no por lo que mide.
 *
 * `nueva: true` provisiona una identidad DISTINTA, que es lo que exige la segregación de deberes:
 * quien solicita no puede ser quien aprueba. Sin el parámetro se reutiliza una por trabajador,
 * porque provisionar una cuenta por petición sería pagar tres llamadas por invariante.
 */
let identidadCacheada: DevIdentity | null = null;

export async function identidadConPermisos(
  request: APIRequestContext,
  opciones: { nueva?: boolean } = {},
): Promise<DevIdentity> {
  if (!opciones.nueva && identidadCacheada) return identidadCacheada;

  const userId = await provisionarUsuarioInterno(request);
  const identidad: DevIdentity = { userId, tenantId: EVOLITH_TENANT_ID, internalAdmin: true };
  if (!opciones.nueva) identidadCacheada = identidad;
  return identidad;
}

/**
 * Una identidad con permisos DENTRO de otro inquilino (G-280, ADR-UMS-105).
 *
 * Sirve para las pruebas cuyo sujeto es la restriccion misma: quien tiene el permiso pero NO es
 * propietario de gestion, o quien mira datos de un inquilino ajeno. Con una identidad sin perfil el
 * guardia responde 403 y la prueba deja de medir lo que dice medir —la politica de ambito, el
 * filtro por inquilino—, porque nunca llega hasta ellos.
 *
 * Toma la plantilla PUBLICADA mas nueva de ese inquilino: no todos tienen las mismas. Medido:
 * `COMEX_ANDINA` solo tiene `PORTAL_CLIENTE`, asi que ninguno de sus usuarios puede sostener
 * `manage_suites` ni aunque se le conceda; `APM_CALLAO` tiene `WMS` y `UMS` con diez plantillas, y
 * por eso es el sujeto correcto para ejercer AUTH_015.
 */
/**
 * Provisiona CATALOGO dentro de otro inquilino y devuelve una identidad que lo sostiene (G-280).
 *
 * Es lo que faltaba para las pruebas de aislamiento. Un inquilino distinto de EVOLITH no tiene por
 * que conceder los permisos que la prueba necesita —medido: la plantilla de «Auditor de
 * Cumplimiento» de APM_CALLAO da UN scope, y un inquilino recien creado nace sin catalogo—, asi
 * que no basta con crear una cuenta: hay que crear la suite, el modulo, el nodo, la accion, el rol
 * y la plantilla, publicarla y asignarla.
 *
 * Con eso, el rechazo que la prueba espera lo da quien debe darlo —el filtro por inquilino o la
 * politica de ambito— y no el guardia de permisos, que es lo que convertia estos invariantes en una
 * medicion de otra cosa.
 */
export async function identidadConCatalogoEn(
  request: APIRequestContext,
  tenantId: string,
  permisos: Array<{ recurso: string; accion: string }>,
): Promise<DevIdentity> {
  const H = { ...devAuthHeaders({ userId: EVOLITH_ADMIN_USER_ID, tenantId }), 'Content-Type': 'application/json' };
  const post = async (ruta: string, data: unknown) => {
    const res = await request.post(`${API}${ruta}`, { headers: H, data });
    return { estado: res.status(), cuerpo: res.status() < 300 ? await res.json().catch(() => ({})) : {} };
  };

  const suite = await post('/system-suites', {
    tenantId, code: codigoUnico('AISL'), name: 'Suite de aislamiento', description: 'provisionada por el arnes',
  });
  const suiteId = (suite.cuerpo as Record<string, string>).systemSuiteId;
  if (!suiteId) throw new Error(`suite en ${tenantId}: ${suite.estado}`);

  const modulo = await post(`/system-suites/${suiteId}/modules`, {
    systemSuiteId: suiteId, code: codigoUnico('MOD'), name: 'Modulo', description: 'x', sortOrder: 1,
  });
  const moduleId = (modulo.cuerpo as Record<string, string>).moduleId;
  await request.post(`${API}/system-suites/${suiteId}/modules/${moduleId}/activate`, { headers: H });

  const rol = await post(`/system-suites/${suiteId}/roles`, {
    systemSuiteId: suiteId, code: codigoUnico('ROL_AISL'), value: 'Rol de aislamiento',
    description: 'provisionado por el arnes', parentRoleId: null, hierarchyLevel: 0, promotionOrder: 0,
  });
  const roleId = (rol.cuerpo as Record<string, string>).roleId;
  const plantilla = await post('/permission-templates', { tenantId, roleId, systemSuiteId: suiteId });
  const templateId = (plantilla.cuerpo as Record<string, string>).templateId;

  // El scope sale del CODIGO DEL NODO y del de la accion —`view_audit` + `VIEW` da
  // `view_audit.view`—, asi que el nodo se llama como el recurso que el guardia exige. Con un
  // codigo cualquiera el permiso existe pero no es el que se pide, que es lo que hacia fallar
  // estas pruebas con un catalogo recien creado.
  for (const { recurso, accion } of permisos) {
    const nodo = await post(`/system-suites/${suiteId}/modules/${moduleId}/nodes`, {
      systemSuiteId: suiteId, moduleId, parentNodeId: null, kind: 'Option',
      code: recurso.toUpperCase(), label: recurso, description: 'x', sortOrder: 1,
    });
    const nodeId = (nodo.cuerpo as Record<string, string>).nodeId;
    if (!nodeId) continue;

    const acc = await post(`/system-suites/${suiteId}/actions`, {
      systemSuiteId: suiteId, code: accion.toUpperCase(), name: `Accion ${accion}`,
    });
    const actionId = (acc.cuerpo as Record<string, string>).actionId;
    if (!actionId) continue;

    await request.post(`${API}/system-suites/${suiteId}/modules/${moduleId}/nodes/${nodeId}/actions`, {
      headers: H, data: { systemSuiteId: suiteId, moduleId, nodeId, actionCode: accion.toUpperCase() },
    });
    await request.post(`${API}/permission-templates/${templateId}/items`, {
      headers: H,
      data: { templateId, targetType: 'Option', targetId: nodeId, actionId, isAllowed: true, isDenied: false },
    });
  }

  await request.post(`${API}/permission-templates/${templateId}/publish`, { headers: H });

  const correo = `robosoft.${codigoUnico('ais').toLowerCase()}@externo.local`;
  const cuenta = await post('/user-accounts', {
    tenantId, branchId: null, email: correo, category: 'Internal', identityReference: null, identityReferenceType: null,
  });
  const userId = (cuenta.cuerpo as Record<string, string>).userAccountId;
  if (!userId) throw new Error(`cuenta en ${tenantId}: ${cuenta.estado}`);
  await request.post(`${API}/user-accounts/${userId}/activate`, { headers: H });

  const perfil = await post('/profiles', { tenantId, userId, roleId, branchId: null });
  const profileId = (perfil.cuerpo as Record<string, string>).profileId;
  if (profileId) {
    await request.post(`${API}/profiles/${profileId}/templates/${templateId}`, { headers: H });
  }

  return { userId, tenantId, internalAdmin: false };
}

export async function identidadConPermisosEn(
  request: APIRequestContext,
  tenantId: string,
  opciones: { rol?: string; interno?: boolean } = {},
): Promise<DevIdentity> {
  const cabecerasDelInquilino = { ...devAuthHeaders({ userId: EVOLITH_ADMIN_USER_ID, tenantId }), 'Content-Type': 'application/json' };

  const rPl = await request.get(`${API}/permission-templates?page=1&pageSize=200`, { headers: cabecerasDelInquilino });
  if (rPl.status() !== 200) throw new Error(`plantillas de ${tenantId}: ${rPl.status()}`);
  const plantillas = (((await rPl.json()).items ?? []) as Array<Record<string, string>>)
    .filter(i => i.tenantId === tenantId && i.status === 'Published')
    .filter(i => (opciones.rol ? (i.roleName ?? '').includes(opciones.rol) : true))
    .sort((a, b) => (a.version ?? '').localeCompare(b.version ?? '', undefined, { numeric: true }));
  const plantilla = plantillas.at(-1);
  if (!plantilla) throw new Error(`sin plantilla publicada en ${tenantId}`);

  const correo = `robosoft.${codigoUnico('ext').toLowerCase()}@externo.local`;
  const rCuenta = await request.post(`${API}/user-accounts`, {
    headers: cabecerasDelInquilino,
    data: { tenantId, branchId: null, email: correo, category: 'Internal', identityReference: null, identityReferenceType: null },
  });
  if (rCuenta.status() !== 201) throw new Error(`cuenta en ${tenantId}: ${rCuenta.status()}`);
  const userId = (await rCuenta.json()).userAccountId as string;
  await request.post(`${API}/user-accounts/${userId}/activate`, { headers: cabecerasDelInquilino });

  const rPerfil = await request.post(`${API}/profiles`, {
    headers: cabecerasDelInquilino,
    data: { tenantId, userId, roleId: plantilla.roleId, branchId: null },
  });
  if (rPerfil.status() === 201) {
    const profileId = (await rPerfil.json()).profileId as string;
    await request.post(`${API}/profiles/${profileId}/templates/${plantilla.templateId}`, { headers: cabecerasDelInquilino });
  }

  return { userId, tenantId, internalAdmin: opciones.interno ?? false };
}

/** Cache de proceso: la suite y la plantilla no cambian entre pruebas de una misma corrida. */
let plantillaAdminUms: { templateId: string; roleId: string; systemSuiteId: string } | null = null;

/**
 * Le da a una cuenta recién provisionada el perfil de administrador de UMS.
 *
 * Existe por G-280. El arnés suplanta a estas cuentas con `X-User-Id` para ejercer segregación de
 * deberes —quien solicita no puede aprobar—, y hasta ahora eso funcionaba porque el atajo de Admin
 * Root dejaba pasar a cualquiera del inquilino propietario sin mirar un scope. Con el atajo apagado
 * una cuenta sin perfil no puede ni crear un inquilino, así que el arnés dejaba de poder montar sus
 * propios escenarios: fallaba por falta de permisos y no por lo que cada invariante mide.
 *
 * Darles el perfil NO debilita nada: lo que estas pruebas comprueban es que dos personas distintas
 * hagan dos pasos distintos, no que una de ellas carezca de permisos. Donde el permiso ES el sujeto
 * —aislamiento entre inquilinos, provisión por quien no es propietario— la prueba sigue usando su
 * identidad restringida a propósito.
 */
export async function darlePermisosDeAdministrador(
  request: APIRequestContext,
  userId: string,
): Promise<void> {
  if (!plantillaAdminUms) {
    const r = await request.get(`${API}/permission-templates?page=1&pageSize=200`, { headers: devAuthHeaders() });
    if (r.status() !== 200) return;
    const items = ((await r.json()).items ?? []) as Array<Record<string, string>>;
    // La MÁS NUEVA de las publicadas, no la primera que aparezca. Una plantilla publicada no admite
    // ítems nuevos, así que ampliar el catálogo crea otra versión: quedarse con la 0.1.0 daba 18
    // scopes donde la 0.2.0 da 30, y el arnés fallaba pidiendo permisos que su plantilla no tenía.
    const publicadas = items
      .filter(i => i.roleName === 'Administrador de UMS' && i.status === 'Published')
      .sort((a, b) => (a.version ?? '').localeCompare(b.version ?? '', undefined, { numeric: true }));
    const elegida = publicadas.at(-1);
    if (!elegida) return;
    plantillaAdminUms = {
      templateId: elegida.templateId,
      roleId: elegida.roleId,
      systemSuiteId: elegida.systemSuiteId,
    };
  }

  const perfil = await request.post(`${API}/profiles`, {
    headers: devAuthHeaders(),
    // El contrato pide `userId`, no `userAccountId`: enviarlo mal devuelve 400 «'User Id' must not
    // be empty» y, como este ayudante calla los fallos, el perfil no se creaba y la prueba fallaba
    // mucho mas tarde por falta de permisos.
    data: {
      tenantId: EVOLITH_TENANT_ID,
      userId,
      roleId: plantillaAdminUms.roleId,
      branchId: null,
    },
  });
  if (perfil.status() !== 201) return;

  const profileId = (await perfil.json()).profileId as string;
  await request.post(`${API}/profiles/${profileId}/templates/${plantillaAdminUms.templateId}`, {
    headers: devAuthHeaders(),
  });
}

// ─────────────────────────── Topología de autorización ───────────────────────────

export async function resolverTenantIdPorCodigo(
  request: APIRequestContext,
  code: string,
): Promise<string | undefined> {
  const res = await request.get(`${API}/tenants?page=1&pageSize=100`, { headers: devAuthHeaders() });
  const body = await res.json();
  return (body.items ?? []).find((t: { code: string }) => t.code === code)?.tenantId;
}

export async function crearSuite(
  request: APIRequestContext,
  tenantId = EVOLITH_TENANT_ID,
  identidad: DevIdentity = {},
): Promise<{ id: string; code: string; res: APIResponse }> {
  const code = codigoUnico('ROBO_SUITE');
  const res = await request.post(`${API}/system-suites`, {
    headers: devAuthHeaders(identidad),
    data: { tenantId, code, name: 'Suite RoboSoft', description: 'Suite de prueba del arnés (topología)' },
  });
  let id = '';
  if (res.status() === 201) id = (await res.json()).systemSuiteId as string;
  return { id, code, res };
}

export async function agregarModulo(request: APIRequestContext, suiteId: string): Promise<string> {
  const res = await request.post(`${API}/system-suites/${suiteId}/modules`, {
    headers: devAuthHeaders(),
    data: { systemSuiteId: suiteId, code: codigoUnico('MOD'), name: 'Módulo', description: 'x', sortOrder: 1 },
  });
  if (res.status() !== 201) throw new Error(`agregarModulo ${res.status()}: ${await res.text()}`);
  return (await res.json()).moduleId as string;
}

export const activarModulo = (request: APIRequestContext, suiteId: string, moduleId: string): Promise<APIResponse> =>
  request.post(`${API}/system-suites/${suiteId}/modules/${moduleId}/activate`, { headers: devAuthHeaders() });

export async function agregarNodo(
  request: APIRequestContext,
  suiteId: string,
  moduleId: string,
  opts: { kind: string; parentNodeId?: string | null; code?: string },
): Promise<{ id: string; res: APIResponse }> {
  const res = await request.post(`${API}/system-suites/${suiteId}/modules/${moduleId}/nodes`, {
    headers: devAuthHeaders(),
    data: {
      systemSuiteId: suiteId,
      moduleId,
      parentNodeId: opts.parentNodeId ?? null,
      kind: opts.kind,
      code: opts.code ?? codigoUnico('NODE'),
      label: 'Nodo',
      description: 'x',
      sortOrder: 1,
    },
  });
  let id = '';
  if (res.status() === 201) id = (await res.json()).nodeId as string;
  return { id, res };
}

export async function registrarAccion(
  request: APIRequestContext,
  suiteId: string,
  code = 'READ',
): Promise<{ actionId: string; res: APIResponse }> {
  const res = await request.post(`${API}/system-suites/${suiteId}/actions`, {
    headers: devAuthHeaders(),
    data: { systemSuiteId: suiteId, code, name: `Acción ${code}` },
  });
  let actionId = '';
  if (res.status() === 201) actionId = (await res.json()).actionId as string;
  return { actionId, res };
}

export const vincularAccionNodo = (
  request: APIRequestContext,
  suiteId: string,
  moduleId: string,
  nodeId: string,
  actionCode: string,
): Promise<APIResponse> =>
  request.post(`${API}/system-suites/${suiteId}/modules/${moduleId}/nodes/${nodeId}/actions`, {
    headers: devAuthHeaders(),
    data: { systemSuiteId: suiteId, moduleId, nodeId, actionCode },
  });

export async function crearRol(
  request: APIRequestContext,
  suiteId: string,
  opts: { code?: string; parentRoleId?: string | null; hierarchyLevel: number; promotionOrder?: number },
): Promise<{ id: string; res: APIResponse }> {
  const res = await request.post(`${API}/system-suites/${suiteId}/roles`, {
    headers: devAuthHeaders(),
    data: {
      systemSuiteId: suiteId,
      code: opts.code ?? codigoUnico('ROLE'),
      value: 'Rol RoboSoft',
      description: 'x',
      parentRoleId: opts.parentRoleId ?? null,
      hierarchyLevel: opts.hierarchyLevel,
      promotionOrder: opts.promotionOrder ?? 1,
    },
  });
  let id = '';
  if (res.status() === 201) id = (await res.json()).roleId as string;
  return { id, res };
}

/**
 * Aprovisiona un ROL REAL persistido (suite + rol) y devuelve su id. Necesario para las
 * aprobaciones: desde G-160, aprobar exige que el `grantedRoleId` corresponda a un rol
 * existente (antes un id arbitrario dejaba un Profile con rol fantasma que rompía el login).
 */
export async function provisionarRolReal(request: APIRequestContext): Promise<string> {
  const suite = await crearSuite(request);
  if (suite.res.status() !== 201) throw new Error(`provisionarRolReal/suite ${suite.res.status()}: ${await suite.res.text()}`);
  const rol = await crearRol(request, suite.id, { hierarchyLevel: 0 });
  if (rol.res.status() !== 201) throw new Error(`provisionarRolReal/rol ${rol.res.status()}: ${await rol.res.text()}`);
  return rol.id;
}

export const actualizarRol = (
  request: APIRequestContext,
  suiteId: string,
  roleId: string,
  opts: { parentRoleId?: string | null; hierarchyLevel: number; promotionOrder?: number },
): Promise<APIResponse> =>
  request.put(`${API}/system-suites/${suiteId}/roles/${roleId}`, {
    headers: devAuthHeaders(),
    data: {
      roleId,
      value: 'Rol RoboSoft',
      description: 'x',
      parentRoleId: opts.parentRoleId ?? null,
      hierarchyLevel: opts.hierarchyLevel,
      promotionOrder: opts.promotionOrder ?? 1,
    },
  });

export async function crearPlantilla(
  request: APIRequestContext,
  roleId: string,
  suiteId: string,
): Promise<{ id: string; res: APIResponse }> {
  const res = await request.post(`${API}/permission-templates`, {
    headers: devAuthHeaders(),
    data: { tenantId: EVOLITH_TENANT_ID, roleId, systemSuiteId: suiteId },
  });
  let id = '';
  if (res.status() === 201) id = (await res.json()).templateId as string;
  return { id, res };
}

export const publicarPlantilla = (request: APIRequestContext, templateId: string): Promise<APIResponse> =>
  request.post(`${API}/permission-templates/${templateId}/publish`, { headers: devAuthHeaders() });

export const agregarItemPlantilla = (
  request: APIRequestContext,
  templateId: string,
  opts: { targetType: string; targetId: string; actionId: string; isAllowed?: boolean; isDenied?: boolean },
): Promise<APIResponse> =>
  request.post(`${API}/permission-templates/${templateId}/items`, {
    headers: devAuthHeaders(),
    data: {
      templateId,
      targetType: opts.targetType,
      targetId: opts.targetId,
      actionId: opts.actionId,
      isAllowed: opts.isAllowed ?? true,
      isDenied: opts.isDenied ?? false,
    },
  });

/** Construye un árbol mínimo suite→módulo(activo)→menu→opción con una acción registrada y vinculada. */
export async function construirArbolMinimo(request: APIRequestContext): Promise<{
  suiteId: string;
  moduleId: string;
  menuId: string;
  optionId: string;
  actionId: string;
}> {
  const suiteId = (await crearSuite(request)).id;
  const moduleId = await agregarModulo(request, suiteId);
  await activarModulo(request, suiteId, moduleId);
  const menuId = (await agregarNodo(request, suiteId, moduleId, { kind: 'Menu', parentNodeId: null })).id;
  const optionId = (await agregarNodo(request, suiteId, moduleId, { kind: 'Option', parentNodeId: menuId })).id;
  const actionId = (await registrarAccion(request, suiteId, 'READ')).actionId;
  return { suiteId, moduleId, menuId, optionId, actionId };
}

// ─────────────────────────── IGA · Promoción de rol (FSM auditada) ───────────────────────────

export interface PromocionOpts {
  targetUserId: string;
  currentRoleId: string;
  targetRoleId: string;
  /** Identidad del solicitante (RequesterId = X-User-Id). Debe diferir del objetivo (SoD). */
  solicitante: DevIdentity;
}

export interface PromocionDto {
  id: string;
  status: string;
  riskScore: number | null;
  approverId: string | null;
  securityReviewerId: string | null;
  executorId: string | null;
  verifierId: string | null;
  targetUserId: string;
  requesterId: string;
}

/** Crea una solicitud de promoción de rol en estado Draft (FR-060). */
export async function crearPromocionRol(
  request: APIRequestContext,
  opts: PromocionOpts,
): Promise<{ id: string; res: APIResponse }> {
  const res = await request.post(`${API}/role-promotion-requests`, {
    headers: devAuthHeaders(opts.solicitante),
    data: {
      tenantId: EVOLITH_TENANT_ID,
      targetUserId: opts.targetUserId,
      currentRoleId: opts.currentRoleId,
      targetRoleId: opts.targetRoleId,
    },
  });
  let id = '';
  if (res.status() === 201) id = (await res.json()).rolePromotionRequestId as string;
  return { id, res };
}

/** Ejecuta una transición de la FSM de promoción (`submit`, `confirm-eligibility`, `manager-approve`, …). */
export const accionPromocion = (
  request: APIRequestContext,
  id: string,
  accion: string,
  identidad: DevIdentity = {},
  body?: Record<string, unknown>,
): Promise<APIResponse> =>
  request.post(`${API}/role-promotion-requests/${id}/${accion}`, {
    headers: devAuthHeaders(identidad),
    ...(body ? { data: body } : {}),
  });

/** Lee una solicitud de promoción por id (proyección determinista con Status/RiskScore/actores). */
export async function obtenerPromocion(
  request: APIRequestContext,
  id: string,
  identidad: DevIdentity = {},
): Promise<PromocionDto | undefined> {
  const res = await request.get(`${API}/role-promotion-requests/${id}`, { headers: devAuthHeaders(identidad) });
  if (res.status() !== 200) return undefined;
  const d = await res.json();
  return {
    id: d.id, status: d.status, riskScore: d.riskScore ?? null,
    approverId: d.approverId ?? null, securityReviewerId: d.securityReviewerId ?? null,
    executorId: d.executorId ?? null, verifierId: d.verifierId ?? null,
    targetUserId: d.targetUserId, requesterId: d.requesterId,
  };
}

// ─────────────────────────── IGA · Delegación de gestión (análogo IGA) ───────────────────────────

export interface DelegacionOpts {
  delegatingAdminId: string;
  delegatedAdminId: string;
  allowedActions?: string[];
  requiresApproval?: boolean;
}

export interface DelegacionDto {
  delegationId: string;
  status: string;
  delegatingAdminId: string;
  delegatedAdminId: string;
  requiresApproval: boolean;
}

/**
 * Crea una delegación de gestión. El llamante (X-User-Id) DEBE ser el `delegatingAdminId`
 * (regla del handler) y ambos admins cuentas Active del mismo tenant. Ventana de validez
 * de 30 días desde ahora.
 */
export async function crearDelegacion(
  request: APIRequestContext,
  opts: DelegacionOpts,
): Promise<{ id: string; res: APIResponse }> {
  const validFrom = new Date();
  const validUntil = new Date(validFrom.getTime() + 30 * 24 * 60 * 60 * 1000);
  const res = await request.post(`${API}/delegations`, {
    headers: devAuthHeaders({ userId: opts.delegatingAdminId }),
    data: {
      tenantId: EVOLITH_TENANT_ID,
      delegatingAdminId: opts.delegatingAdminId,
      delegatedAdminId: opts.delegatedAdminId,
      scopeType: 'Tenant',
      scopeId: null,
      allowedActions: opts.allowedActions ?? ['AssignProfile'],
      validFrom: validFrom.toISOString(),
      validUntil: validUntil.toISOString(),
      maxDurationDays: 90,
      requiresApproval: opts.requiresApproval ?? false,
    },
  });
  let id = '';
  if (res.status() === 201) id = (await res.json()).delegationId as string;
  return { id, res };
}

// ─────────────────────────── Núcleo: Tenant / Branch / UserAccount (ciclo de vida) ───────────────────────────

export interface TenantOpts {
  code?: string;
  name?: string;
  type?: string; // INTERNAL | SUPPLIER | CLIENT
  isManagementOwner?: boolean;
  idpStrategy?: string | null;
  companyReference?: string | null;
}

export interface TenantDto {
  tenantId: string;
  code: string;
  name: string;
  type: string;
  status: string;
  isManagementOwner: boolean;
}

export async function crearTenant(
  request: APIRequestContext,
  opts: TenantOpts = {},
  identidad: DevIdentity = {},
): Promise<{ id: string; code: string; res: APIResponse }> {
  const code = opts.code ?? codigoUnico('ROBO_TEN');
  const res = await request.post(`${API}/tenants`, {
    headers: devAuthHeaders(identidad),
    data: {
      code,
      name: opts.name ?? 'Inquilino RoboSoft',
      type: opts.type ?? 'INTERNAL',
      idpStrategy: opts.idpStrategy ?? null,
      companyReference: opts.companyReference ?? null,
      isManagementOwner: opts.isManagementOwner ?? false,
    },
  });
  let id = '';
  if (res.status() === 201) id = (await res.json()).tenantId as string;
  return { id, code, res };
}

export async function obtenerTenant(
  request: APIRequestContext,
  id: string,
  identidad: DevIdentity = {},
): Promise<TenantDto | undefined> {
  const res = await request.get(`${API}/tenants/${id}`, { headers: devAuthHeaders(identidad) });
  if (res.status() !== 200) return undefined;
  return (await res.json()) as TenantDto;
}

export const accionTenant = (
  request: APIRequestContext,
  id: string,
  accion: string,
  identidad: DevIdentity = {},
): Promise<APIResponse> =>
  request.post(`${API}/tenants/${id}/${accion}`, { headers: devAuthHeaders(identidad) });

export const crearBranch = (
  request: APIRequestContext,
  tenantId: string,
  opts: { code?: string; name?: string; geofencingMetadata?: string | null } = {},
  identidad: DevIdentity = {},
): Promise<APIResponse> =>
  request.post(`${API}/tenants/${tenantId}/branches`, {
    headers: devAuthHeaders(identidad),
    data: {
      code: opts.code ?? codigoUnico('SUC'),
      name: opts.name ?? 'Sucursal Callao',
      geofencingMetadata: opts.geofencingMetadata ?? JSON.stringify({ lat: -12.0564, lng: -77.1181, radiusMeters: 500 }),
    },
  });

export interface CuentaDto {
  userAccountId: string;
  email: string;
  status: string;
  hasActivePassword: boolean;
}

export async function crearCuenta(
  request: APIRequestContext,
  opts: { email?: string; tenantId?: string; branchId?: string | null; category?: string } = {},
  identidad: DevIdentity = {},
): Promise<{ id: string; email: string; res: APIResponse }> {
  const email = opts.email ?? `robosoft.core.${codigoUnico('c').toLowerCase()}@evolith.example`;
  const res = await request.post(`${API}/user-accounts`, {
    headers: devAuthHeaders(identidad),
    data: {
      tenantId: opts.tenantId ?? EVOLITH_TENANT_ID,
      branchId: opts.branchId ?? null,
      email,
      category: opts.category ?? 'Internal',
      identityReference: null,
      identityReferenceType: null,
    },
  });
  let id = '';
  if (res.status() === 201) id = (await res.json()).userAccountId as string;
  return { id, email, res };
}

export const accionCuenta = (
  request: APIRequestContext,
  id: string,
  accion: string,
  identidad: DevIdentity = {},
  query?: string,
): Promise<APIResponse> =>
  request.post(`${API}/user-accounts/${id}/${accion}${query ? `?${query}` : ''}`, { headers: devAuthHeaders(identidad) });

export async function obtenerCuenta(
  request: APIRequestContext,
  id: string,
  identidad: DevIdentity = {},
): Promise<{ status: number; dto?: CuentaDto }> {
  const res = await request.get(`${API}/user-accounts/${id}`, { headers: devAuthHeaders(identidad) });
  if (res.status() !== 200) return { status: res.status() };
  return { status: 200, dto: (await res.json()) as CuentaDto };
}

// ─────────────────────────── Refresh token: aprovisionar tenant REFRESH-ON con usuario loginable ───────────────────────────

export interface CredencialesRefresh {
  tenantId: string;
  tenantCode: string;
  username: string;
  password: string;
  userAccountId: string;
  contexto: DevIdentity;
}

/**
 * Aprovisiona un inquilino con REFRESH-ON (AppConfiguration `AUTH_REFRESH_TOKEN_ENABLED=true`
 * publicada, scope tenant) y un usuario loginable (cuenta Active con contraseña local). Todas las
 * operaciones por-inquilino se ejecutan con contexto == inquilino objetivo (X-Tenant-Id=TID) para
 * evitar el debilitamiento cross-tenant ([[G-161]]). El tenant queda REFRESH-ON; el resto de la
 * plataforma sigue en su default (REFRESH-OFF).
 */
export async function provisionarTenantRefreshOn(request: APIRequestContext): Promise<CredencialesRefresh> {
  const tenant = await crearTenant(request, { type: 'INTERNAL' }, await adminEvolith(request));
  if (tenant.res.status() !== 201) throw new Error(`refresh/tenant ${tenant.res.status()}: ${await tenant.res.text()}`);
  // El actor es una cuenta real de EVOLITH —con sus permisos— actuando sobre el inquilino recien
  // creado: `X-Tenant-Id` apunta al objetivo para no debilitar la unicidad cross-tenant ([[G-161]]).
  const admin = await identidadConPermisos(request);
  const ctx: DevIdentity = { userId: admin.userId, tenantId: tenant.id, internalAdmin: true };
  // Config refresh-on publicada, scope tenant.
  const cfg = await request.post(`${API}/app-configurations`, {
    headers: devAuthHeaders(ctx),
    data: { tenantId: tenant.id, systemSuiteId: null, moduleId: null, code: 'AUTH_REFRESH_TOKEN_ENABLED', value: 'true', description: 'refresh-on RoboSoft', isInheritable: true, isEncrypted: false },
  });
  if (cfg.status() !== 201) throw new Error(`refresh/config ${cfg.status()}: ${await cfg.text()}`);
  const cfgId = (await cfg.json()).appConfigurationId as string;
  const pub = await request.post(`${API}/app-configurations/${cfgId}/publish`, { headers: devAuthHeaders(ctx) });
  if (pub.status() !== 204) throw new Error(`refresh/publish ${pub.status()}: ${await pub.text()}`);
  // Usuario loginable.
  const email = `robosoft.rt.${codigoUnico('u').toLowerCase()}@evolith.example`;
  const password = 'Rt.Robosoft.2026!';
  const cuenta = await crearCuenta(request, { tenantId: tenant.id, email }, ctx);
  if (cuenta.res.status() !== 201) throw new Error(`refresh/cuenta ${cuenta.res.status()}: ${await cuenta.res.text()}`);
  const pwd = await request.post(`${API}/user-accounts/${cuenta.id}/passwords`, { headers: devAuthHeaders(ctx), data: { password } });
  if (pwd.status() !== 201) throw new Error(`refresh/password ${pwd.status()}: ${await pwd.text()}`);
  const act = await accionCuenta(request, cuenta.id, 'activate', ctx);
  if (act.status() !== 204) throw new Error(`refresh/activate ${act.status()}: ${await act.text()}`);
  return { tenantId: tenant.id, tenantCode: tenant.code, username: email, password, userAccountId: cuenta.id, contexto: ctx };
}

/** Login real (POST /auth/login). Devuelve el cuerpo parseado (incluye `refreshToken` si REFRESH-ON). */
export async function loginReal(
  _request: APIRequestContext,
  creds: { tenantCode: string; username: string; password: string; rememberMe?: boolean },
): Promise<{ status: number; token?: string; refreshToken?: string; body: Record<string, unknown> }> {
  // El login va en un contexto AISLADO a proposito. Un login exitoso deja la COOKIE de sesion en el
  // contexto que lo emitio, y esa cookie autentica las peticiones siguientes: el middleware de
  // desarrollo se aparta cuando la peticion ya viene autenticada, asi que las cabeceras `X-User-Id`
  // dejaban de mandar y el arnes seguia actuando como el usuario recien logueado —sin permisos— en
  // vez del administrador. Medido en INV-RT10: `activate` concedido antes del login y `block`
  // denegado despues, con las mismas cabeceras. Aislar el login deja intacta la identidad del arnes.
  const aislado = await contextoApi.newContext({ baseURL: process.env.E2E_BASE_URL ?? 'http://localhost:8080' });
  try {
    const res = await aislado.post(`${API}/auth/login`, {
      headers: { 'Content-Type': 'application/json' },
      data: { tenantCode: creds.tenantCode, username: creds.username, password: creds.password, rememberMe: creds.rememberMe ?? true },
    });
    const body = res.status() === 200 ? await res.json() : {};
    return { status: res.status(), token: body.token, refreshToken: body.refreshToken, body };
  } finally {
    await aislado.dispose();
  }
}

/** Renovación por refresh token opaco (POST /auth/refresh-token). Devuelve el APIResponse crudo. */
export const renovarRefresh = (request: APIRequestContext, refreshToken: string): Promise<APIResponse> =>
  request.post(`${API}/auth/refresh-token`, { headers: { 'Content-Type': 'application/json' }, data: { refreshToken } });

// G-280: identidad REAL. Era `{ userId: uid() }` —un GUID que no corresponde a ninguna cuenta—, y
// con el atajo apagado eso no puede hacer nada: sin usuario no hay grafo, y sin grafo no hay scopes.
const adminEvolith = (request: APIRequestContext): Promise<DevIdentity> => identidadConPermisos(request);

// ─────────────────────────── Auditoría (append-only, no repudio, aislamiento) ───────────────────────────

export interface AuditoriaOpts {
  whoActed?: string;
  subjectType?: string;
  whatChanged?: string;
  eventType?: string;
  auditResult?: string;
  affectedEntityId?: string;
  affectedEntityType?: string;
  rootTenantId?: string;
  metadata?: string;
}

export interface AuditoriaDto {
  id: string;
  whoActed: string;
  subjectType: string;
  whenOccurred: string;
  whatChanged: string;
  eventType: string;
  auditResult: string;
  affectedEntityId: string;
  affectedEntityType: string;
  rootTenantId: string;
  metadata: string | null;
}

/**
 * Registra una entrada de auditoría por la vía manual (POST /audit-records). El actor y el inquilino
 * se derivan del contexto autenticado (no del cuerpo) — INV-AU08. Requiere una identidad con GUID
 * válido (el default `dev-user` no lo es).
 */
export async function registrarAuditoria(
  request: APIRequestContext,
  identidad: DevIdentity,
  opts: AuditoriaOpts = {},
): Promise<{ id: string; res: APIResponse }> {
  const res = await request.post(`${API}/audit-records`, {
    headers: devAuthHeaders(identidad),
    data: {
      whoActed: opts.whoActed ?? uid(),
      subjectType: opts.subjectType ?? 'User',
      whatChanged: opts.whatChanged ?? 'Prueba de auditoría RoboSoft',
      eventType: opts.eventType ?? codigoUnico('EVT'),
      auditResult: opts.auditResult ?? 'Success',
      affectedEntityId: opts.affectedEntityId ?? uid(),
      affectedEntityType: opts.affectedEntityType ?? 'UserAccount',
      rootTenantId: opts.rootTenantId ?? EVOLITH_TENANT_ID,
      metadata: opts.metadata ?? null,
    },
  });
  let id = '';
  if (res.status() === 201) id = (await res.json()).auditRecordId as string;
  return { id, res };
}

/** Lee un registro de auditoría por id. Devuelve el estado HTTP y el DTO (crudo, para aseverar UTC). */
export async function obtenerAuditoria(
  request: APIRequestContext,
  id: string,
  identidad: DevIdentity,
): Promise<{ status: number; dto?: AuditoriaDto }> {
  const res = await request.get(`${API}/audit-records/${id}`, { headers: devAuthHeaders(identidad) });
  if (res.status() !== 200) return { status: res.status() };
  return { status: 200, dto: (await res.json()) as AuditoriaDto };
}

/** Ejecuta una transición de la FSM de delegación (`activate`, `submit-for-approval`, `approve`, `revoke`, …). */
export const accionDelegacion = (
  request: APIRequestContext,
  id: string,
  accion: string,
  identidad: DevIdentity,
  query?: string,
): Promise<APIResponse> =>
  request.post(`${API}/delegations/${id}/${accion}${query ? `?${query}` : ''}`, {
    headers: devAuthHeaders(identidad),
  });

/** Lee una delegación por id (Status y admins). */
export async function obtenerDelegacion(
  request: APIRequestContext,
  id: string,
  identidad: DevIdentity,
): Promise<DelegacionDto | undefined> {
  const res = await request.get(`${API}/delegations/${id}`, { headers: devAuthHeaders(identidad) });
  if (res.status() !== 200) return undefined;
  const d = await res.json();
  return {
    delegationId: d.delegationId, status: d.status,
    delegatingAdminId: d.delegatingAdminId, delegatedAdminId: d.delegatedAdminId,
    requiresApproval: d.requiresApproval,
  };
}
