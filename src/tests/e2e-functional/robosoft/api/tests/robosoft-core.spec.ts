// Contexto acotado «robosoft-core» — carril B (API, caja negra) · G-112
//
// Re-verifica el núcleo de gobernanza de organizaciones e identidades (FR-020, FR-021, FR-001,
// FR-002, FR-004): unicidad de códigos/emails, ciclos de vida auditados de tenant y cuenta,
// soft-delete con anonimización, provisión bajo el management-owner y control del segundo
// management-owner. Fuente: reference/qa/e2e-certification-matrix.md (sección robosoft-core).
//
// Los invariantes de AUTENTICACIÓN (login de cuentas no activas, BCrypt, MFA, requisito MFA del
// tenant y signup público — RC06..RC10) se ejercen en los contextos dedicados authn-local/
// authn-federated; aquí quedan PEND con referencia cruzada para no duplicar cobertura.
import { test, expect, type APIRequestContext } from '@playwright/test';
import { invariante, invariantePendiente, esperarEstado } from '../helpers/invariant';
import { EVOLITH_TENANT_ID } from '../helpers/auth';
import {
  identidadConPermisos,
  uid,
  codigoUnico,
  crearTenant,
  obtenerTenant,
  accionTenant,
  crearBranch,
  crearCuenta,
  accionCuenta,
  obtenerCuenta,
} from '../helpers/provision';

const CTX = 'robosoft-core';
// G-280: identidad REAL y con permisos; un GUID al azar no tiene grafo ni scopes.
const admin = (request: APIRequestContext) => identidadConPermisos(request);

test.describe('RobosoftCore', () => {
  invariante(
    { id: 'INV-RC01', contexto: CTX, descripcion: 'El código de tenant es globalmente único (alta duplicada rechazada)', referencia: 'CreateTenantCommandHandler (unicidad de código, FR-020)' },
    async ({ request }) => {
      const code = codigoUnico('ROBO_UNIQ');
      await esperarEstado((await crearTenant(request, { code }, await admin(request))).res, 201);
      const dup = await crearTenant(request, { code }, await admin(request));
      expect([400, 409]).toContain(dup.res.status());
    },
  );

  invariante(
    { id: 'INV-RC02', contexto: CTX, descripcion: 'Ciclo de vida del tenant: Active → Suspended → Active (auditado)', referencia: 'Suspend/ActivateTenantCommand (FR-020)' },
    async ({ request }) => {
      const { id, res } = await crearTenant(request, {}, await admin(request));
      await esperarEstado(res, 201);
      expect((await obtenerTenant(request, id, await admin(request)))?.status).toBe('Active');
      await esperarEstado(await accionTenant(request, id, 'suspend', await admin(request)), 204);
      expect((await obtenerTenant(request, id, await admin(request)))?.status).toBe('Suspended');
      await esperarEstado(await accionTenant(request, id, 'activate', await admin(request)), 204);
      expect((await obtenerTenant(request, id, await admin(request)))?.status).toBe('Active');
    },
  );

  invariante(
    { id: 'INV-RC03', contexto: CTX, descripcion: 'Código de sucursal único por tenant; metadata de geofencing aceptada', referencia: 'AddBranchCommand (unicidad por tenant + geofencing, FR-021)' },
    async ({ request }) => {
      // Bajo EVOLITH (contexto == tenant de la sucursal): la unicidad se evalúa sobre la colección
      // real del agregado. La provisión cross-tenant por un admin interno se rastrea aparte (G-161).
      const code = codigoUnico('SUC_UNIQ');
      // Alta con geofencing → 201.
      await esperarEstado(await crearBranch(request, EVOLITH_TENANT_ID, { code }, await admin(request)), 201);
      // Mismo código en el mismo tenant → rechazado.
      const dup = await crearBranch(request, EVOLITH_TENANT_ID, { code }, await admin(request));
      expect([400, 409]).toContain(dup.status());
    },
  );

  invariantePendiente(
    { id: 'INV-RC03b', contexto: CTX, descripcion: 'Unicidad de código de sucursal también cross-tenant: un admin interno que provisiona sobre OTRO inquilino no puede duplicar el código (G-161)', referencia: 'AddBranchCommandHandler.BranchCodeExistsAsync (verificación autoritativa que ignora el filtro por inquilino, G-161)', motivo: 'El fix está VERIFICADO EN VIVO por curl (dup cross-tenant → 409 inmediato) y por prueba unitaria (Handle_WhenBranchCodeExistsCrossTenant_ReturnsFailure). NO es aseverable vía el APIRequestContext de Playwright: sus lecturas no observan la escritura recién commiteada que curl sí ve de inmediato (mismo artefacto de conexión que [G-162]; persiste incluso con contexto de request fresco y reintentos). La integridad está garantizada por el índice único de BD (TenantId, Code).' },
    async () => { /* PEND: fix verificado por curl + unit test; artefacto de lectura del arnés impide la aserción black-box */ },
  );

  invariante(
    { id: 'INV-RC04', contexto: CTX, descripcion: 'Email de cuenta único por tenant (alta duplicada rechazada)', referencia: 'CreateUserAccountCommand (unicidad de email por tenant, FR-001)' },
    async ({ request }) => {
      const email = `robosoft.rc04.${codigoUnico('e').toLowerCase()}@evolith.example`;
      await esperarEstado((await crearCuenta(request, { email }, await admin(request))).res, 201);
      const dup = await crearCuenta(request, { email }, await admin(request));
      expect([400, 409]).toContain(dup.res.status());
    },
  );

  invariante(
    { id: 'INV-RC05', contexto: CTX, descripcion: 'Ciclo de vida de cuenta: Pending → Active → Blocked → Active (reactivación vía restore)', referencia: 'Activate/Block/RestoreUserAccountCommand (FR-002)' },
    async ({ request }) => {
      const { id, res } = await crearCuenta(request, {}, await admin(request));
      await esperarEstado(res, 201);
      expect((await obtenerCuenta(request, id, await admin(request))).dto?.status).toBe('Pending');
      await esperarEstado(await accionCuenta(request, id, 'activate', await admin(request)), 204);
      expect((await obtenerCuenta(request, id, await admin(request))).dto?.status).toBe('Active');
      await esperarEstado(await accionCuenta(request, id, 'block', await admin(request), `reason=${encodeURIComponent('prueba de bloqueo')}`), 204);
      expect((await obtenerCuenta(request, id, await admin(request))).dto?.status).toBe('Blocked');
      // Reactivación Blocked → Active vía /restore (la auditoría de julio la creyó inalcanzable).
      await esperarEstado(await accionCuenta(request, id, 'restore', await admin(request)), 204);
      expect((await obtenerCuenta(request, id, await admin(request))).dto?.status).toBe('Active');
    },
  );

  invariante(
    { id: 'INV-RC12', contexto: CTX, descripcion: 'Soft-delete terminal: la cuenta borrada se anonimiza (sin PII ni credencial activa) y es irreversible', referencia: 'DeleteUserAccountCommand (GDPR, REC-16)' },
    async ({ request }) => {
      const { id, email, res } = await crearCuenta(request, {}, await admin(request));
      await esperarEstado(res, 201);
      await esperarEstado(await accionCuenta(request, id, 'activate', await admin(request)), 204);
      const del = await request.delete(`/api/v1/user-accounts/${id}`, { headers: { 'X-User-Id': (await admin(request)).userId, 'X-Tenant-Id': EVOLITH_TENANT_ID, 'X-Is-Internal-Admin': 'true' } });
      await esperarEstado(del, 204);
      const post = await obtenerCuenta(request, id, await admin(request));
      // Tras el borrado: o no es legible (404) o queda en estado terminal Deleted con el email anonimizado.
      if (post.status === 200) {
        expect(post.dto!.email).not.toBe(email);
        expect(post.dto!.hasActivePassword).toBe(false);
        expect(post.dto!.status.toLowerCase()).toContain('delet');
      } else {
        expect(post.status).toBe(404);
      }
    },
  );

  invariante(
    { id: 'INV-RC13', contexto: CTX, descripcion: 'Provisión bajo EVOLITH (management-owner) funciona de extremo a extremo: tenant CLIENT + sucursal + cuenta', referencia: 'Provisión e2e bajo management-owner (FR-001)' },
    async ({ request }) => {
      const { id: tenantId, res } = await crearTenant(request, { type: 'CLIENT' }, await admin(request));
      await esperarEstado(res, 201);
      await esperarEstado(await crearBranch(request, tenantId, {}, await admin(request)), 201);
      // Cuenta bajo el tenant recién provisionado.
      const cuenta = await crearCuenta(request, { tenantId }, await admin(request));
      await esperarEstado(cuenta.res, 201);
    },
  );

  invariante(
    { id: 'INV-RC14', contexto: CTX, descripcion: 'Un tenant CLIENT puede provisionarse vía API (H-04, ADR-0111)', referencia: 'CreateTenantCommand type=CLIENT (FR-001)' },
    async ({ request }) => {
      const { res } = await crearTenant(request, { type: 'CLIENT' }, await admin(request));
      await esperarEstado(res, 201);
      const dto = await obtenerTenant(request, (await res.json()).tenantId, await admin(request));
      expect(dto?.type).toBe('CLIENT');
    },
  );

  invariante(
    { id: 'INV-RC15', contexto: CTX, descripcion: 'Crear un 2º management-owner devuelve Result.Failure/409 controlado, nunca 500', referencia: 'CreateTenantCommandHandler (ManagementOwnerAlreadyExists → 409, G-045)' },
    async ({ request }) => {
      // EVOLITH ya es el management-owner; un segundo con IsManagementOwner=true debe rechazarse.
      const { res } = await crearTenant(request, { isManagementOwner: true }, await admin(request));
      expect([400, 409]).toContain(res.status());
      expect(res.status()).not.toBe(500);
    },
  );

  invariante(
    { id: 'INV-RC11', contexto: CTX, descripcion: 'No se puede eliminar un agregado con dependencias activas (sucursal con cuenta asignada)', referencia: 'RemoveBranchCommand (integridad referencial, FR-004)' },
    async ({ request }) => {
      // Todo bajo EVOLITH (contexto == tenant) para que la sucursal y la cuenta sean coherentes.
      const branchRes = await crearBranch(request, EVOLITH_TENANT_ID, {}, await admin(request));
      await esperarEstado(branchRes, 201);
      const branchId = (await branchRes.json()).branchId as string;
      // Cuenta activa dependiente de la sucursal.
      const cuenta = await crearCuenta(request, { tenantId: EVOLITH_TENANT_ID, branchId }, await admin(request));
      await esperarEstado(cuenta.res, 201);
      await esperarEstado(await accionCuenta(request, cuenta.id, 'activate', await admin(request)), 204);
      // Eliminar la sucursal con una cuenta activa asignada debe rechazarse.
      const del = await request.delete(`/api/v1/tenants/${EVOLITH_TENANT_ID}/branches/${branchId}`, {
        headers: { 'X-User-Id': (await admin(request)).userId, 'X-Tenant-Id': EVOLITH_TENANT_ID, 'X-Is-Internal-Admin': 'true' },
      });
      expect([400, 409]).toContain(del.status());
    },
  );

  // ── Autenticación: cubierta en los contextos authn-local / authn-federated (no se duplica aquí) ──
  const authnMotivo = 'Invariante de autenticación (login/credenciales/MFA/signup); se ejerce en los contextos dedicados authn-local y authn-federated para no duplicar cobertura.';
  for (const rc of [
    { id: 'INV-RC06', d: 'Las cuentas no activas no autentican' },
    { id: 'INV-RC07', d: 'Credenciales locales BCrypt en API; el cliente nunca envía hash; credencial única activa' },
    { id: 'INV-RC08', d: 'MFA NotEnrolled → Enrolled → Verified vía enroll y verify' },
    { id: 'INV-RC09', d: 'Requisito MFA del tenant aplicado en el login' },
    { id: 'INV-RC10', d: 'Signup público → Pending → aprobación → Active con onboarding lobby' },
  ]) {
    invariantePendiente(
      { id: rc.id, contexto: CTX, descripcion: rc.d, referencia: 'authn-local / authn-federated', motivo: authnMotivo },
      async () => { /* cubierto en contextos de autenticación */ },
    );
  }
});
