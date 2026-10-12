// Contexto acotado «audit» — carril B (API, caja negra) · G-112
//
// Re-verifica contra el sistema VIVO las invariantes de la traza de auditoría (FR-070..FR-072):
// append-only, tupla no repudiable, UTC, aislamiento por inquilino, exigencia de autenticación,
// no-repudio (actor/tenant derivados del servidor) y desinfección de metadata sensible.
// Fuente: reference/qa/e2e-certification-matrix.md (sección audit) y la auditoría RoboSoft 2026-07-16.
//
// Las FAIL de julio (AU06/AU07: acceso anónimo y lectura cruzada) se corrigieron en G-040 y este
// arnés lo confirma contra el binario desplegado. La verificación de autenticación usa la vía real:
// `X-Disable-Dev-Auth: true` desactiva la identidad de conveniencia de dev y exige credencial real
// (fail-closed 401), de modo que RequireAuthorization se ejerce como en producción.
import { test, expect, type APIRequestContext } from '@playwright/test';
import { invariante, invariantePendiente, esperarEstado } from '../helpers/invariant';
import { EVOLITH_TENANT_ID, type DevIdentity } from '../helpers/auth';
import {
  uid,
  identidadConPermisos,
  identidadConPermisosEn,
  identidadConCatalogoEn,
  resolverTenantIdPorCodigo,
  registrarAuditoria,
  obtenerAuditoria,
} from '../helpers/provision';

const CTX = 'audit';

// Identidad admin EVOLITH con GUID válido (el default `dev-user` no lo es → el POST lo rechaza).
// G-280: identidad REAL y con permisos; un GUID al azar no tiene grafo ni scopes.
const adminEvolith = (request: APIRequestContext) => identidadConPermisos(request);
// Identidad de un usuario REGULAR de otro inquilino (no admin interno) para probar aislamiento.
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

test.describe('AuditRecord', () => {
  invariante(
    { id: 'INV-AU01', contexto: CTX, descripcion: 'Append-only: la traza no admite UPDATE ni DELETE por API', referencia: 'AuditRecordEndpoints.cs (sin PUT/DELETE; G-081)' },
    async ({ request }) => {
      const admin = await adminEvolith(request);
      const { id, res } = await registrarAuditoria(request, admin);
      await esperarEstado(res, 201);
      // No existe ruta de mutación: PUT/DELETE deben ser rechazados (404 sin ruta / 405 método no permitido).
      const put = await request.put(`/api/v1/audit-records/${id}`, { headers: { 'X-User-Id': admin.userId }, data: {} });
      expect([404, 405]).toContain(put.status());
      const del = await request.delete(`/api/v1/audit-records/${id}`, { headers: { 'X-User-Id': admin.userId } });
      expect([404, 405]).toContain(del.status());
    },
  );

  invariante(
    { id: 'INV-AU03', contexto: CTX, descripcion: 'Cada registro guarda la tupla no repudiable (actor, instante, cambio, evento, resultado, entidad, inquilino)', referencia: 'AuditRecord.Record / AuditRecordDto (FR-070)' },
    async ({ request }) => {
      const admin = await adminEvolith(request);
      const { id, res } = await registrarAuditoria(request, admin, {
        whatChanged: 'Alta de cuenta interna',
        affectedEntityType: 'UserAccount',
      });
      await esperarEstado(res, 201);
      const { status, dto } = await obtenerAuditoria(request, id, admin);
      expect(status).toBe(200);
      expect(dto!.whoActed).toBeTruthy();
      expect(dto!.whenOccurred).toBeTruthy();
      expect(dto!.whatChanged).toBe('Alta de cuenta interna');
      expect(dto!.eventType).toBeTruthy();
      expect(dto!.auditResult).toBeTruthy();
      expect(dto!.affectedEntityId).toBeTruthy();
      expect(dto!.affectedEntityType).toBe('UserAccount');
      expect(dto!.rootTenantId).toBe(EVOLITH_TENANT_ID);
    },
  );

  invariante(
    { id: 'INV-AU04', contexto: CTX, descripcion: 'El instante del registro se persiste en UTC', referencia: 'AuditRecord.WhenOccurred (UTC, FR-070)' },
    async ({ request }) => {
      const admin = await adminEvolith(request);
      const { id, res } = await registrarAuditoria(request, admin);
      await esperarEstado(res, 201);
      const { dto } = await obtenerAuditoria(request, id, admin);
      // ISO 8601 en UTC: sufijo 'Z' o desplazamiento +00:00; además parseable a fecha válida.
      expect(dto!.whenOccurred).toMatch(/Z$|\+00:00$/);
      expect(Number.isNaN(Date.parse(dto!.whenOccurred))).toBe(false);
    },
  );

  invariante(
    { id: 'INV-AU06', contexto: CTX, descripcion: 'Consulta acotada por inquilino: un usuario de otro inquilino NO puede leer un registro ajeno (sin lectura cruzada)', referencia: 'GetAuditRecordByIdQueryHandler.cs (aislamiento por inquilino, G-040)' },
    async ({ request }) => {
      const admin = await adminEvolith(request);
      const { id, res } = await registrarAuditoria(request, admin); // RootTenantId = EVOLITH
      await esperarEstado(res, 201);
      // Usuario regular de OTRO inquilino: no debe verlo (se responde "not found" para no revelar existencia).
      const ajeno = await obtenerAuditoria(request, id, await regularOtroTenant(request));
      expect(ajeno.status, 'un inquilino no puede leer la auditoría de otro').toBe(404);
      // El admin interno de EVOLITH sí lo lee.
      expect((await obtenerAuditoria(request, id, admin)).status).toBe(200);
    },
  );

  invariante(
    { id: 'INV-AU07', contexto: CTX, descripcion: 'Los endpoints de auditoría exigen autenticación (fail-closed sin credencial real)', referencia: 'AuditRecordEndpoints RequireAuthorization + DevAuth X-Disable-Dev-Auth (G-040/G-042)' },
    async ({ request }) => {
      // X-Disable-Dev-Auth desactiva la identidad de conveniencia; sin credencial real ⇒ 401.
      const sinAuth = { 'X-Disable-Dev-Auth': 'true' };
      expect((await request.get(`/api/v1/audit-records?page=1&pageSize=5`, { headers: sinAuth })).status()).toBe(401);
      expect((await request.get(`/api/v1/audit-records/${uid()}`, { headers: sinAuth })).status()).toBe(401);
      expect((await request.post(`/api/v1/audit-records`, { headers: sinAuth, data: {} })).status()).toBe(401);
    },
  );

  invariante(
    { id: 'INV-AU08', contexto: CTX, descripcion: 'No-repudio: el actor y el inquilino se derivan del servidor; el cuerpo no puede falsificarlos', referencia: 'RecordAuditCommandHandler.cs (actor/tenant desde IUserContext, G-040)' },
    async ({ request }) => {
      const admin = await adminEvolith(request);
      const tenantFalsificado = uid();
      const actorFalsificado = uid();
      // El cuerpo intenta falsificar actor e inquilino; el servidor debe ignorarlos.
      const { id, res } = await registrarAuditoria(request, admin, {
        whoActed: actorFalsificado,
        rootTenantId: tenantFalsificado,
      });
      await esperarEstado(res, 201);
      const { dto } = await obtenerAuditoria(request, id, admin);
      // El inquilino persistido es el del contexto autenticado (EVOLITH), NO el falsificado.
      expect(dto!.rootTenantId).toBe(EVOLITH_TENANT_ID);
      expect(dto!.rootTenantId).not.toBe(tenantFalsificado);
      // El actor persistido es el usuario autenticado (cabecera), NO el del cuerpo.
      expect(dto!.whoActed).toBe(admin.userId);
      expect(dto!.whoActed).not.toBe(actorFalsificado);
    },
  );

  invariante(
    { id: 'INV-AU10', contexto: CTX, descripcion: 'Desinfección de metadata en la vía manual (POST): los valores de claves sensibles se redactan antes de persistir', referencia: 'AuditMetadataSanitizer + RecordAuditCommandHandler (FR-072, G-040)' },
    async ({ request }) => {
      const admin = await adminEvolith(request);
      const secreto = 'sk-super-secreto-1234567890';
      const metadata = JSON.stringify({ apiKey: secreto, passwordHash: 'abc123hash', action: 'login' });
      const { id, res } = await registrarAuditoria(request, admin, { metadata });
      await esperarEstado(res, 201);
      const { dto } = await obtenerAuditoria(request, id, admin);
      const persistida = dto!.metadata ?? '';
      // El secreto no debe aparecer; las claves sensibles quedan redactadas; lo no-sensible se conserva.
      expect(persistida).not.toContain(secreto);
      expect(persistida).not.toContain('abc123hash');
      expect(persistida).toContain('[REDACTED]');
      expect(persistida).toContain('login');
    },
  );

  invariantePendiente(
    { id: 'INV-AU02', contexto: CTX, descripcion: 'Inmutabilidad a nivel de dominio: el agregado AuditRecord no expone mutadores', referencia: 'AuditRecord (sin métodos de mutación; G-081)', motivo: 'Propiedad estructural del código de dominio (ausencia de mutadores), no observable por API de caja negra; cubierta por pruebas unitarias del agregado.' },
    async () => { /* no verificable por API */ },
  );

  invariantePendiente(
    { id: 'INV-AU05', contexto: CTX, descripcion: 'Metadata desinfectada también en la vía automática (AuditTrailAspect)', referencia: 'AuditTrailAspect + AuditMetadataSanitizer (FR-072)', motivo: 'La metadata de la vía automática la genera el aspecto [AuditTrail] a partir de argumentos del comando; no es inyectable de forma controlada para forzar un secreto. El MISMO saneador se ejerce en la vía manual (INV-AU10) y por pruebas unitarias del sanitizador.' },
    async () => { /* la vía automática no es inyectable de caja negra */ },
  );

  invariantePendiente(
    { id: 'INV-AU09', contexto: CTX, descripcion: 'Durabilidad de la traza vía Transactional Outbox', referencia: 'ADR-0110 (outbox de auditoría)', motivo: 'Garantía de infraestructura (persistencia transaccional del outbox); no observable de forma determinista por el API de lectura sin inyectar fallos de infraestructura.' },
    async () => { /* garantía de infraestructura, no de caja negra */ },
  );
});
