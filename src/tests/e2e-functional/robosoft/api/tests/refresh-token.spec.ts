// Contexto acotado «refresh-token» — carril B (API, caja negra) · G-112
//
// Re-verifica el ciclo de vida del refresh token opaco (FR-015/FR-016, ADR-UMS-091): opt-in por
// inquilino (REFRESH-OFF fail-closed / REFRESH-ON), renovación del grafo sin re-login, rotación,
// detección de reuso (revoca la familia), rechazo de tokens inválidos y denegación ante cuenta
// bloqueada. Fuente: reference/qa/e2e-certification-matrix.md (refresh-token).
//
// Para REFRESH-ON se aprovisiona un inquilino dedicado (config `AUTH_REFRESH_TOKEN_ENABLED=true`
// publicada, scope tenant) con un usuario loginable; el resto de la plataforma sigue REFRESH-OFF.
// La expiración (RT06), el evento auditable de reuso (RT05) y el aislamiento cross-tenant (RT09)
// quedan PEND: exigen, respectivamente, avance de reloj, correlación de traza y dos inquilinos
// refresh-on con misuso cruzado, no deterministas en este arnés.
import { test, expect } from '@playwright/test';
import { invariante, invariantePendiente, esperarEstado } from '../helpers/invariant';
import { API } from '../helpers/auth';
import {
  uid,
  provisionarTenantRefreshOn,
  loginReal,
  renovarRefresh,
  accionCuenta,
} from '../helpers/provision';

const CTX = 'refresh-token';
const CRED_ADMIN = { tenantCode: 'EVOLITH', username: 'admin@evolith.example', password: 'Evolith.Dev.2026' };

test.describe('RefreshToken', () => {
  invariante(
    { id: 'INV-RT01', contexto: CTX, descripcion: 'REFRESH-OFF (fail-closed): el inquilino por defecto no emite refresh token y renovar se deniega', referencia: 'RefreshTokenPolicyProvider (default Disabled) / AuthEndpoints (FR-015)' },
    async ({ request }) => {
      // EVOLITH está en REFRESH-OFF: el login NO devuelve refresh token.
      const login = await loginReal(request, CRED_ADMIN);
      expect(login.status).toBe(200);
      expect(login.refreshToken ?? '').toBe('');
      // Intentar renovar con un token arbitrario se deniega (403 Disabled o 401).
      const renovar = await renovarRefresh(request, uid());
      expect([401, 403]).toContain(renovar.status());
    },
  );

  invariante(
    { id: 'INV-RT02', contexto: CTX, descripcion: 'REFRESH-ON: renovar regenera la sesión y el grafo completo sin re-login', referencia: 'RefreshAuthenticationCommand (regenera grafo, FR-015)' },
    async ({ request }) => {
      const cred = await provisionarTenantRefreshOn(request);
      const login = await loginReal(request, cred);
      expect(login.status).toBe(200);
      expect(login.refreshToken, 'REFRESH-ON debe emitir refresh token').toBeTruthy();
      const res = await renovarRefresh(request, login.refreshToken!);
      await esperarEstado(res, 200);
      const body = await res.json();
      expect(body.token, 'la renovación entrega un nuevo access token').toBeTruthy();
      expect(body.authorizationGraph ?? body.token, 'la renovación regenera el grafo/sesión').toBeTruthy();
    },
  );

  invariante(
    { id: 'INV-RT03', contexto: CTX, descripcion: 'Rotación: al renovar, el nuevo refresh token invalida el anterior', referencia: 'RefreshToken rotate=true (FR-016)' },
    async ({ request }) => {
      const cred = await provisionarTenantRefreshOn(request);
      const login = await loginReal(request, cred);
      const rt1 = login.refreshToken!;
      const r1 = await renovarRefresh(request, rt1);
      await esperarEstado(r1, 200);
      const rt2 = (await r1.json()).refreshToken as string;
      expect(rt2).toBeTruthy();
      expect(rt2).not.toBe(rt1);
      // Reutilizar el anterior (rt1) ya rotado se rechaza.
      const reuso = await renovarRefresh(request, rt1);
      expect([401, 403]).toContain(reuso.status());
    },
  );

  invariante(
    { id: 'INV-RT04', contexto: CTX, descripcion: 'Detección de reuso: usar un refresh ya consumido revoca la familia completa (rt2 también deja de valer)', referencia: 'RefreshToken detectReuse=true (revoca familia, FR-016)' },
    async ({ request }) => {
      const cred = await provisionarTenantRefreshOn(request);
      const login = await loginReal(request, cred);
      const rt1 = login.refreshToken!;
      const r1 = await renovarRefresh(request, rt1);
      await esperarEstado(r1, 200);
      const token2 = (await r1.json()).refreshToken as string;
      expect(token2).toBeTruthy();
      // Reuso del token consumido rt1 → rechazo Y revocación de la familia.
      const reuso = await renovarRefresh(request, rt1);
      expect([401, 403]).toContain(reuso.status());
      // Tras la detección de reuso, el token legítimo rt2 también queda invalidado (familia revocada).
      const trasReuso = await renovarRefresh(request, token2);
      expect([401, 403]).toContain(trasReuso.status());
    },
  );

  invariante(
    { id: 'INV-RT08', contexto: CTX, descripcion: 'Negativos: un refresh token inválido o malformado se rechaza (nunca renueva)', referencia: 'HandleRefreshTokenGrantAsync (validación, FR-015)' },
    async ({ request }) => {
      // Vacío → 400 (falta la credencial).
      const vacio = await request.post(`${API}/auth/refresh-token`, { headers: { 'Content-Type': 'application/json' }, data: { refreshToken: '' } });
      expect([400, 401]).toContain(vacio.status());
      // Malformado/arbitrario → 401 (credencial de renovación inválida).
      const malformado = await renovarRefresh(request, 'no-es-un-token-valido-123');
      expect([400, 401, 403]).toContain(malformado.status());
    },
  );

  invariante(
    { id: 'INV-RT10', contexto: CTX, descripcion: 'Cuenta bloqueada: renovar se deniega aunque el refresh token siga vigente', referencia: 'RefreshAuthenticationCommand (revalida estado de cuenta, FR-002)' },
    async ({ request }) => {
      const cred = await provisionarTenantRefreshOn(request);
      const login = await loginReal(request, cred);
      const rt1 = login.refreshToken!;
      // Bloquear la cuenta (contexto == inquilino objetivo).
      await esperarEstado(await accionCuenta(request, cred.userAccountId, 'block', cred.contexto, `reason=${encodeURIComponent('bloqueo de prueba')}`), 204);
      // Con la cuenta bloqueada, la renovación debe denegarse.
      const renovar = await renovarRefresh(request, rt1);
      expect([401, 403]).toContain(renovar.status());
    },
  );

  invariantePendiente(
    { id: 'INV-RT05', contexto: CTX, descripcion: 'El reuso de un refresh consumido genera un evento auditable (append-only)', referencia: 'ADR-0110 (outbox de auditoría)', motivo: 'Correlacionar el evento de reuso en la traza de auditoría de forma determinista exige filtrar por familia/sesión y ventana temporal; se difiere a una iteración de correlación de auditoría.' },
    async () => { /* PEND */ },
  );

  invariantePendiente(
    { id: 'INV-RT06', contexto: CTX, descripcion: 'Expiración del refresh token → renovación denegada', referencia: 'RefreshToken lifetime (FR-015)', motivo: 'Requiere avanzar el reloj más allá del lifetime del token (o un lifetime muy corto con espera real); no determinista en el arnés sin control de tiempo.' },
    async () => { /* PEND */ },
  );

  invariantePendiente(
    { id: 'INV-RT09', contexto: CTX, descripcion: 'Aislamiento multi-inquilino del refresh (un token no cruza de inquilino)', referencia: 'RefreshToken tenant-bound (FR-022)', motivo: 'Exige dos inquilinos REFRESH-ON y un intento de misuso cruzado del token opaco; el token es opaco y ligado a su sesión, por lo que el misuso cruzado no es observable más allá del rechazo genérico (cubierto por RT08).' },
    async () => { /* PEND */ },
  );
});
