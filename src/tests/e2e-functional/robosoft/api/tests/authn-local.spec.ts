// Contexto acotado «authn-local» — carril B (API, caja negra) · G-112
//
// Re-verifica contra el sistema VIVO las invariantes de autenticación local (login BCrypt,
// grafo de autorización autocontenido, ventana de validez, mapeo de errores Result Pattern,
// endpoint de integración cliente e i18n). Fuente: bmad-tester-robosoft-audit-2026-07-16.md,
// «Cobertura por contexto → authn-local» (PASS 8 / FAIL 3 / PENDING 3).
//
// Determinismo e idempotencia + NO contaminación del clúster compartido:
//  - Los POSITIVOS usan tenants CLIENT sembrados (COMEX_ANDINA / AGRONORTE) con contraseña
//    correcta (el login exitoso resetea el contador de intentos).
//  - Los NEGATIVOS de credenciales usan usuarios INEXISTENTES: nunca contraseña errada sobre
//    una cuenta real, para no disparar el lockout (FR-017) de la semilla compartida.
import { test, expect } from '@playwright/test';
import { invariante, invariantePendiente, esperarEstado, codigoDeError } from '../helpers/invariant';
import {
  CRED_COMEX,
  CRED_AGRONORTE,
  postLogin,
  postClientAuthenticate,
} from '../helpers/auth';
import { provisionarCuentaDesechable } from '../helpers/provision';

const CTX = 'authn-local';

// Claves de nivel superior que el grafo autocontenido debe entregar (FR-034).
const CLAVES_GRAFO = [
  'context',
  'authentication',
  'actions',
  'menuAccess',
  'domainPermissions',
  'featureFlags',
  'effectiveConfig',
  'scopes',
  'generatedAt',
  'validUntil',
];

test.describe('authn-local', () => {
  invariante(
    { id: 'INV-AL01', contexto: CTX, descripcion: 'Login local BCrypt exitoso para tenant con authUseExternalIdp=off (COMEX_ANDINA)', referencia: 'AuthEndpoints /auth/login; FR-013' },
    async ({ request }) => {
      const res = await postLogin(request, CRED_COMEX);
      await esperarEstado(res, 200);
      const body = await res.json();
      expect(body.token, 'debe emitir token').toBeTruthy();
      expect(body.tenantCode).toBe('COMEX_ANDINA');
      expect(body.authorizationGraph, 'debe entregar el grafo de autorización').toBeTruthy();
    },
  );

  invariante(
    { id: 'INV-AL02', contexto: CTX, descripcion: 'El portal de gestión interna fuerza SIEMPRE autenticación local', referencia: 'AuthEndpoints.cs:151 AccessScope=PortalManagement; AuthMethodResolverService.cs:36-38' },
    async ({ request }) => {
      // /auth/login siempre resuelve por el portal de gestión → método Local, sin importar el IdP.
      const res = await postLogin(request, CRED_COMEX);
      await esperarEstado(res, 200);
      const graph = (await res.json()).authorizationGraph;
      expect(graph.authentication.method, 'el login del portal debe ser Local').toBe('Local');
    },
  );

  invariante(
    { id: 'INV-AL03', contexto: CTX, descripcion: 'Grafo autocontenido: context, actions, menuAccess, domainPermissions, featureFlags, effectiveConfig, scopes', referencia: 'FR-034; authorizationGraph' },
    async ({ request }) => {
      const res = await postLogin(request, CRED_COMEX);
      await esperarEstado(res, 200);
      const graph = (await res.json()).authorizationGraph;
      for (const clave of CLAVES_GRAFO) {
        expect(graph, `el grafo debe contener '${clave}'`).toHaveProperty(clave);
      }
    },
  );

  invariante(
    { id: 'INV-AL04', contexto: CTX, descripcion: 'validUntil = generatedAt + sessionTimeoutMinutes', referencia: 'FR-034; ventana de validez del grafo' },
    async ({ request }) => {
      const res = await postLogin(request, CRED_COMEX);
      await esperarEstado(res, 200);
      const body = await res.json();
      const graph = body.authorizationGraph;
      const generado = Date.parse(graph.generatedAt);
      const valido = Date.parse(graph.validUntil);
      const minutos = (valido - generado) / 60000;
      expect(minutos).toBe(body.sessionParameters.sessionTimeoutMinutes);
    },
  );

  invariante(
    { id: 'INV-AL05', contexto: CTX, descripcion: 'Grafo inmutable/integridad (token JWT firmado, ventana de validez fija)', referencia: 'JWT HS256; graph_generated_at/graph_valid_until' },
    async ({ request }) => {
      const res = await postLogin(request, CRED_COMEX);
      await esperarEstado(res, 200);
      const body = await res.json();
      // JWT firmado = tres segmentos (header.payload.signature).
      expect(String(body.token).split('.')).toHaveLength(3);
      // La ventana de validez viaja fija en el grafo (no se puede ampliar desde el cliente).
      expect(body.authorizationGraph.generatedAt).toBeTruthy();
      expect(body.authorizationGraph.validUntil).toBeTruthy();
      expect(Date.parse(body.authorizationGraph.validUntil)).toBeGreaterThan(
        Date.parse(body.authorizationGraph.generatedAt),
      );
    },
  );

  invariante(
    { id: 'INV-AL06', contexto: CTX, descripcion: 'Credenciales inválidas → 401 AUTH_006 controlado (Result Pattern)', referencia: 'Login handler; AUTH_006' },
    async ({ request }) => {
      // Usuario INEXISTENTE dentro de un tenant real: 401 AUTH_006 y sin lockout de cuenta real.
      const res = await postLogin(request, {
        tenantCode: 'COMEX_ANDINA',
        username: `fantasma_${Date.now()}@robosoft.test`,
        password: 'no-importa',
      });
      await esperarEstado(res, 401);
      expect(await codigoDeError(res)).toContain('AUTH_006');
    },
  );

  invariante(
    { id: 'INV-AL07', contexto: CTX, descripcion: 'Tenant inexistente → 400 AUTH_002 controlado, nunca 404', referencia: 'ADR-UMS-100 §2.2; MapAuthError; AUTH_002' },
    async ({ request }) => {
      const res = await postLogin(request, {
        tenantCode: `NO_SUCH_${Date.now()}`,
        username: 'x@y.com',
        password: 'z',
      });
      // 400 y no 404: NINGUNA rama del login devuelve 404, porque el estado distinguiria
      // desenlaces que los mensajes evitan distinguir (G-258, ADR-UMS-100 §2.2). Aqui el 400 no
      // filtra nada que el cuerpo no diga ya —«Verifique el codigo del tenant»—, elegido asi para
      // quien teclea mal el codigo de su organizacion.
      await esperarEstado(res, 400);
      expect(await codigoDeError(res)).toContain('AUTH_002');
      expect(res.status(), 'un 404 aqui volveria a ser un oraculo').not.toBe(404);
    },
  );

  invariante(
    { id: 'INV-AL08', contexto: CTX, descripcion: 'Validación de campos requeridos → 400 AUTH_001', referencia: 'Login validator; AUTH_001' },
    async ({ request }) => {
      const res = await postLogin(request, {});
      await esperarEstado(res, 400);
      expect(await codigoDeError(res)).toContain('AUTH_001');
    },
  );

  invariantePendiente(
    {
      id: 'INV-AL09',
      contexto: CTX,
      descripcion: 'Cuenta no activa → 401 AUTH_005',
      referencia: 'Login handler:144-150 (Status != Active → AUTH_005)',
      motivo:
        'PEND (auditoría): no hay cuenta inactiva sembrada. Provisionarla exigiría crear un usuario ' +
        'con contraseña y bloquearlo, cuyo login además choca con el bug de onboarding sin perfil ' +
        '(AUTH_000), enmascarando AUTH_005. No se altera el clúster compartido. Verificado por código.',
    },
    async () => {},
  );

  invariantePendiente(
    {
      id: 'INV-AL10',
      contexto: CTX,
      descripcion: 'Tenant suspendido → AUTH_003',
      referencia: 'Login handler (Tenant.Status != Active → AUTH_003)',
      motivo:
        'PEND (auditoría): ningún tenant sembrado está suspendido. Crear+suspender un tenant y ' +
        'provisionar un usuario loginable es destructivo/pesado y se confunde con el bug de perfil ' +
        '(AUTH_000). No se altera el clúster compartido. Verificado por código.',
    },
    async () => {},
  );

  invariantePendiente(
    {
      id: 'INV-AL11',
      contexto: CTX,
      descripcion: 'Reto MFA cuando el tenant lo exige',
      referencia: 'authn-local-04; mfaRequiredForAdmin',
      motivo:
        'PEND (auditoría): ningún tenant sembrado tiene mfaRequiredForAdmin=true (COMEX/EVOLITH=false). ' +
        'Además el MFA vivo es "gated por enrolamiento", no un challenge en el login. No verificable en vivo.',
    },
    async () => {},
  );

  invariante(
    { id: 'INV-AL12', contexto: CTX, descripcion: 'Usuario autenticado recibe grafo con accesos efectivos (scopes no vacíos)', referencia: 'H-05; authorizationGraph.scopes/menuAccess' },
    async ({ request }) => {
      const res = await postLogin(request, CRED_COMEX);
      await esperarEstado(res, 200);
      const graph = (await res.json()).authorizationGraph;
      // Antes (H-05): scopes=[], menuAccess todo NotGranted. Corregido: el grafo entrega accesos.
      expect(Array.isArray(graph.scopes)).toBe(true);
      expect(graph.scopes.length, 'el usuario debe recibir scopes efectivos').toBeGreaterThan(0);
    },
  );

  invariante(
    { id: 'INV-AL13', contexto: CTX, descripcion: 'Endpoint de integración cliente (/client/authenticate) usable por tenants CLIENT sembrados', referencia: 'authn-local-01; /client/authenticate' },
    async ({ request }) => {
      for (const cred of [CRED_COMEX, CRED_AGRONORTE]) {
        const res = await postClientAuthenticate(request, cred);
        await esperarEstado(res, 200);
        expect((await res.json()).token, `token para ${cred.tenantCode}`).toBeTruthy();
      }
    },
  );

  invariante(
    { id: 'INV-AL14', contexto: CTX, descripcion: 'Respuestas y textos en español (también en /client/authenticate)', referencia: 'SD-08; authn-local-03' },
    async ({ request }) => {
      const res = await postClientAuthenticate(request, {
        tenantCode: 'COMEX_ANDINA',
        username: `fantasma_${Date.now()}@robosoft.test`,
        password: 'no-importa',
      });
      await esperarEstado(res, 401);
      const msg = (await res.json()).message ?? '';
      expect(msg, 'el mensaje debe estar en español').toMatch(/verifique|credenciales|autenticar|sesión/i);
      expect(msg, 'no debe estar en inglés').not.toMatch(/invalid username or password/i);
    },
  );

  invariante(
    { id: 'INV-AL15', contexto: CTX, descripcion: 'Bloqueo por intentos fallidos (FR-017): tras maxLoginAttempts → 423 AUTH_017; credenciales correctas rechazadas mientras esté bloqueada', referencia: 'FR-017; lockout AUTH_017' },
    async ({ request }) => {
      // DESTRUCTIVO → cuenta PROPIA y desechable (id único por corrida). NUNCA sobre admin@ ni
      // cuentas sembradas: bloquearlas rompería el clúster compartido.
      const cuenta = await provisionarCuentaDesechable(request);
      const maxIntentos = 5; // maxLoginAttempts sembrado.
      // Los primeros N fallos devuelven credenciales inválidas (aún no bloqueada).
      for (let i = 0; i < maxIntentos; i++) {
        const res = await postLogin(request, { ...cuenta, password: 'contraseña-incorrecta' });
        await esperarEstado(res, 401);
        expect(await codigoDeError(res)).toContain('AUTH_006');
      }
      // El intento que supera el umbral bloquea la cuenta → 423 Locked, AUTH_017.
      const bloqueo = await postLogin(request, { ...cuenta, password: 'contraseña-incorrecta' });
      await esperarEstado(bloqueo, 423);
      expect(await codigoDeError(bloqueo)).toContain('AUTH_017');
      // Fail-closed: mientras esté bloqueada, incluso la contraseña CORRECTA se rechaza.
      const correcta = await postLogin(request, cuenta);
      await esperarEstado(correcta, 423);
      expect(await codigoDeError(correcta)).toContain('AUTH_017');
    },
  );
});
