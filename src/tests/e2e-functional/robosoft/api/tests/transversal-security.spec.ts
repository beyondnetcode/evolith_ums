// Contexto acotado «transversal-security» — carril B (API, caja negra) · G-112
//
// Re-verifica los invariantes transversales de seguridad del PRD (FR-022, G-020 y contrato REST):
// aislamiento multi-inquilino en lectura y escritura, idempotencia por Idempotency-Key, contrato
// REST (verbos), mapeo de errores del Result Pattern y no fuga de secretos en las proyecciones.
// Fuente: reference/qa/e2e-certification-matrix.md (sección transversal-security).
//
// El aislamiento se ejerce con una identidad REGULAR de otro inquilino (X-Is-Internal-Admin:false,
// X-Tenant-Id ajeno): el filtro de aplicación por OrganizationId debe bastar aun con RLS de BD
// inactivo. El switch-tenant auditado (TS03) y la aseveración de configuración de RLS (TS04) quedan
// PEND: requieren, respectivamente, un flujo de cambio de inquilino y una comprobación de infra.
import { test, expect, type APIRequestContext } from '@playwright/test';
import { invariante, invariantePendiente, esperarEstado } from '../helpers/invariant';
import { API, EVOLITH_TENANT_ID , devAuthHeaders, type DevIdentity } from '../helpers/auth';
import {
  identidadConPermisos,
  uid,
  codigoUnico,
  provisionarUsuarioInterno,
  crearSuite,
  registrarAuditoria,
  obtenerAuditoria,
  identidadConPermisosEn,
  identidadConCatalogoEn,
  resolverTenantIdPorCodigo,
} from '../helpers/provision';

const CTX = 'transversal-security';

// G-280: identidad REAL y con permisos; un GUID al azar no tiene grafo ni scopes.
const adminEvolith = (request: APIRequestContext) => identidadConPermisos(request);
// Un usuario REAL de otro inquilino, con permisos EN EL SUYO. Antes eran dos GUIDs al azar, y
// bastaba mientras el atajo de Admin Root no miraba scopes; con el guardia aplicando, esa identidad
// no existe y el 403 llega antes que el filtro por inquilino — la prueba dejaba de medir el
// aislamiento para medir la ausencia de permisos, que es otra cosa (G-280).
const regularOtroTenant = async (request: APIRequestContext): Promise<DevIdentity> => {
  const otro = await resolverTenantIdPorCodigo(request, 'APM_CALLAO');
  if (!otro) throw new Error('falta el inquilino sembrado APM_CALLAO');
    // Rol AUDITOR y no administrador: la prueba mira si el FILTRO por inquilino le esconde lo ajeno,
  // asi que el sujeto necesita poder leer en el suyo. Con «Administrador del Sistema» de APM le
  // faltaba `view_audit` y el 403 llegaba antes que el filtro, midiendo otra cosa.
  // El catalogo sembrado de APM_CALLAO no publica `view_audit`, asi que el rol «Auditor» de ese
  // inquilino no alcanza para leer auditoria y el 403 llegaba antes que el filtro. Se aprovisiona
  // un catalogo propio con ese permiso exacto para que la prueba mida el AISLAMIENTO, no el guardia.
  return identidadConCatalogoEn(request, otro, [{ recurso: 'view_audit', accion: 'VIEW' }]);
};

const jsonHeaders = (extra: Record<string, string> = {}) => ({ 'Content-Type': 'application/json', ...extra });
// G-280: el administrador que actua es una cuenta REAL con permisos. Con `uid()` la identidad no
// existia, no tenia grafo y solo pasaba mientras el atajo de Admin Root miraba para otro lado.
const adminHeaders = async (request: APIRequestContext, extra: Record<string, string> = {}) => ({
  ...devAuthHeaders(await identidadConPermisos(request)),
  ...jsonHeaders(extra),
});

test.describe('TransversalSecurity', () => {
  invariante(
    { id: 'INV-TS01', contexto: CTX, descripcion: 'Aislamiento multi-inquilino en LECTURA: un usuario de otro inquilino no ve registros ajenos (filtro por OrganizationId)', referencia: 'GetAllAuditRecordsQueryHandler / filtro por inquilino (FR-022)' },
    async ({ request }) => {
      const admin = await adminEvolith(request);
      const eventType = codigoUnico('TS01_EVT');
      const { res } = await registrarAuditoria(request, admin, { eventType }); // bajo EVOLITH
      await esperarEstado(res, 201);
      // Un usuario REGULAR de otro inquilino consulta ese eventType: no debe ver nada (aislamiento).
      const ajeno = await regularOtroTenant(request);
      const rAjeno = await request.get(`${API}/audit-records?page=1&pageSize=50&eventType=${eventType}`, {
        headers: { 'X-User-Id': ajeno.userId, 'X-Tenant-Id': ajeno.tenantId!, 'X-Is-Internal-Admin': 'false' },
      });
      expect(rAjeno.status()).toBe(200);
      expect((await rAjeno.json()).totalItems, 'un inquilino no debe leer la auditoría de otro').toBe(0);
      // El admin de EVOLITH sí lo ve.
      const rPropio = await request.get(`${API}/audit-records?page=1&pageSize=50&eventType=${eventType}`, { headers: await adminHeaders(request) });
      expect((await rPropio.json()).totalItems).toBeGreaterThanOrEqual(1);
    },
  );

  invariante(
    { id: 'INV-TS02', contexto: CTX, descripcion: 'Aislamiento multi-inquilino en ESCRITURA: un usuario regular no puede provisionar en un inquilino ajeno', referencia: 'TenantScopePolicy.EnsureManagementOwnerScope (FR-022, INV-AT05)' },
    async ({ request }) => {
      // Usuario regular de otro inquilino intenta crear una suite BAJO EVOLITH → denegado (no 201).
      const { res } = await crearSuite(request, EVOLITH_TENANT_ID, await regularOtroTenant(request));
      expect([400, 401, 403]).toContain(res.status());
    },
  );

  invariante(
    { id: 'INV-TS05', contexto: CTX, descripcion: 'Idempotencia: reintentar un POST con el mismo Idempotency-Key produce efecto único (respuesta replayada)', referencia: 'IdempotencyMiddleware.cs (ADR-UMS-063)' },
    async ({ request }) => {
      const key = uid();
      const cuerpo = {
        whoActed: uid(), subjectType: 'User', whatChanged: 'idempotencia', eventType: codigoUnico('TS05_EVT'),
        auditResult: 'Success', affectedEntityId: uid(), affectedEntityType: 'UserAccount', rootTenantId: EVOLITH_TENANT_ID, metadata: null,
      };
      const headers = await adminHeaders(request, { 'Idempotency-Key': key });
      const r1 = await request.post(`${API}/audit-records`, { headers, data: cuerpo });
      await esperarEstado(r1, 201);
      const id1 = (await r1.json()).auditRecordId as string;
      // Segunda llamada con el mismo key: respuesta cacheada (mismo id, sin doble efecto).
      const r2 = await request.post(`${API}/audit-records`, { headers, data: cuerpo });
      expect(r2.headers()['x-idempotency-replayed']).toBe('true');
      expect((await r2.json()).auditRecordId).toBe(id1);
    },
  );

  invariante(
    { id: 'INV-TS06', contexto: CTX, descripcion: 'Contrato REST: GET es de solo lectura (no muta) y los verbos no soportados se rechazan', referencia: 'Contrato REST (verbos correctos)' },
    async ({ request }) => {
      const admin = await adminEvolith(request);
      const { id, res } = await registrarAuditoria(request, admin);
      await esperarEstado(res, 201);
      // GET repetido no muta el recurso (mismo id, misma lectura).
      const a = await obtenerAuditoria(request, id, admin);
      const b = await obtenerAuditoria(request, id, admin);
      expect(a.dto!.id).toBe(b.dto!.id);
      expect(a.dto!.whenOccurred).toBe(b.dto!.whenOccurred);
      // Un verbo no soportado en el recurso de auditoría (PATCH/DELETE) se rechaza (append-only).
      const patch = await request.patch(`${API}/audit-records/${id}`, { headers: await adminHeaders(request), data: {} });
      expect([404, 405]).toContain(patch.status());
    },
  );

  invariante(
    { id: 'INV-TS07', contexto: CTX, descripcion: 'Result Pattern: mapeo de errores a 400/401/404/409 (sin 500 en violaciones de dominio)', referencia: 'Result Pattern → ProblemDetails (mapeo de estados)' },
    async ({ request }) => {
      // 400: cuerpo inválido (metadata no es JSON bien formado) en un POST autenticado.
      const r400 = await request.post(`${API}/audit-records`, {
        headers: await adminHeaders(request),
        data: { whoActed: uid(), subjectType: 'User', whatChanged: 'x', eventType: codigoUnico('E'), auditResult: 'Success', affectedEntityId: uid(), affectedEntityType: 'UserAccount', rootTenantId: EVOLITH_TENANT_ID, metadata: 'no-es-json{' },
      });
      expect(r400.status(), 'metadata inválida ⇒ 400 controlado, no 500').toBe(400);
      // 401: sin credencial real (X-Disable-Dev-Auth desactiva la identidad de dev).
      const r401 = await request.get(`${API}/audit-records/${uid()}`, { headers: { 'X-Disable-Dev-Auth': 'true' } });
      expect(r401.status()).toBe(401);
      // 404: recurso inexistente (autenticado).
      const r404 = await request.get(`${API}/audit-records/${uid()}`, { headers: await adminHeaders(request) });
      expect(r404.status()).toBe(404);
      // 409: colisión de unicidad (email de cuenta duplicado en el mismo inquilino).
      const email = `robosoft.ts07.${codigoUnico('e').toLowerCase()}@evolith.example`;
      const cuerpoCuenta = { tenantId: EVOLITH_TENANT_ID, branchId: null, email, category: 'Internal', identityReference: null, identityReferenceType: null };
      const c1 = await request.post(`${API}/user-accounts`, { headers: await adminHeaders(request), data: cuerpoCuenta });
      await esperarEstado(c1, 201);
      const c2 = await request.post(`${API}/user-accounts`, { headers: await adminHeaders(request), data: cuerpoCuenta });
      expect([400, 409]).toContain(c2.status());
    },
  );

  invariante(
    { id: 'INV-TS08', contexto: CTX, descripcion: 'Seguridad: las proyecciones de lectura NO exponen contraseñas/hashes/secretos', referencia: 'ReadModels sin campos sensibles (FR-010)' },
    async ({ request }) => {
      const admin = await adminEvolith(request);
      const userId = await provisionarUsuarioInterno(request);
      const res = await request.get(`${API}/user-accounts/${userId}`, { headers: await adminHeaders(request) });
      expect(res.status()).toBe(200);
      const cuerpo = (await res.text()).toLowerCase();
      // La proyección no debe contener valores de secreto ni la clave passwordHash con contenido.
      expect(cuerpo).not.toContain('passwordhash');
      expect(cuerpo).not.toMatch(/"(password|secret|refreshtoken|passwordhash)"\s*:\s*"[^"]+"/);
    },
  );

  invariante(
    { id: 'INV-TS09', contexto: CTX, descripcion: 'Idempotencia end-to-end sobre un agregado real: dos altas con el mismo Idempotency-Key crean una sola cuenta', referencia: 'IdempotencyMiddleware + CreateUserAccount (ADR-UMS-063)' },
    async ({ request }) => {
      const key = uid();
      const email = `robosoft.ts09.${codigoUnico('e').toLowerCase()}@evolith.example`;
      const cuerpo = { tenantId: EVOLITH_TENANT_ID, branchId: null, email, category: 'Internal', identityReference: null, identityReferenceType: null };
      const headers = await adminHeaders(request, { 'Idempotency-Key': key });
      const r1 = await request.post(`${API}/user-accounts`, { headers, data: cuerpo });
      await esperarEstado(r1, 201);
      const id1 = (await r1.json()).userAccountId as string;
      // Reintento con el mismo key: respuesta replayada, MISMA cuenta (sin duplicar el agregado).
      const r2 = await request.post(`${API}/user-accounts`, { headers, data: cuerpo });
      expect(r2.headers()['x-idempotency-replayed']).toBe('true');
      expect((await r2.json()).userAccountId).toBe(id1);
    },
  );

  invariantePendiente(
    { id: 'INV-TS03', contexto: CTX, descripcion: 'Acceso cross-tenant SOLO para INTERNAL_ADMIN vía switch-tenant, auditado', referencia: 'Switch-tenant + auditoría (FR-022)', motivo: 'Requiere ejercer el flujo de cambio de inquilino de un administrador interno y correlacionar la traza de auditoría resultante; no hay un endpoint de switch-tenant determinista aprovisionable en este arnés.' },
    async () => { /* no aprovisionable de caja negra */ },
  );

  invariantePendiente(
    { id: 'INV-TS04', contexto: CTX, descripcion: 'RLS de BD inactivo ⇒ el filtro de aplicación por inquilino es suficiente', referencia: 'G-020 (aislamiento por filtro de aplicación)', motivo: 'Aseveración de configuración de infraestructura (estado del RLS en PostgreSQL). La suficiencia del filtro de aplicación se demuestra empíricamente en INV-TS01 (lectura) e INV-TS02 (escritura).' },
    async () => { /* aseveración de configuración; demostrada por TS01/TS02 */ },
  );
});
