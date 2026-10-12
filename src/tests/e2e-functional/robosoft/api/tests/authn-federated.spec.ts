// Contexto acotado «authn-federated» — carril B (API, caja negra) · G-112
//
// Re-verifica lo verificable de la autenticación federada (FR-011/FR-012/FR-042) SIN un IdP externo
// real: registro de IdP por inquilino con estrategia (Result Pattern, sin excepciones) y la regla de
// que un usuario federado no puede tener contraseña local. Fuente: matriz de certificación.
//
// La federación (OIDC/SAML) es ÁREA DIFERIDA en el piloto (auth local funciona; el ADR de
// federación y el Keycloak real están pendientes). Por eso la resolución dinámica por reglas
// (prioridad/suite/dominio/fallback — AF02..AF05), la desactivación de contraseña al VINCULAR
// (AF07) y la autenticación federada real contra Keycloak (AF08) quedan PEND, documentadas como
// brecha de cobertura, no como fallo.
import { test, expect, type APIRequestContext } from '@playwright/test';
import { invariante, invariantePendiente, esperarEstado } from '../helpers/invariant';
import { API, EVOLITH_TENANT_ID , devAuthHeaders } from '../helpers/auth';
import { uid, codigoUnico , identidadConPermisos } from '../helpers/provision';

const CTX = 'authn-federated';
// G-280: la identidad que ACTUA tiene que existir y traer permisos. Era `X-User-Id: uid()`, un
// GUID al azar que solo funcionaba mientras el atajo de Admin Root dejaba pasar sin mirar scopes.
const H = async (request: APIRequestContext) => ({
  ...devAuthHeaders(await identidadConPermisos(request)),
  'Content-Type': 'application/json',
});

test.describe('AuthnFederated', () => {
  invariante(
    { id: 'INV-AF01', contexto: CTX, descripcion: 'Registro de IdP por inquilino con estrategia; sin endpoint de edición, la estrategia es inmutable', referencia: 'RegisterIdentityProviderCommand (FR-011); sin PUT de IdP (G-157)' },
    async ({ request }) => {
      const code = codigoUnico('IDP');
      // Registro con estrategia válida → 201 con IdentityProviderId.
      const crear = await request.post(`${API}/tenants/${EVOLITH_TENANT_ID}/identity-providers`, {
        headers: await H(request), data: { code, name: 'IdP Corporativo', description: 'Federación de prueba', strategy: 'AzureAd' },
      });
      await esperarEstado(crear, 201);
      expect((await crear.json()).identityProviderId, 'el registro devuelve el id del IdP').toBeTruthy();
      // Persistencia + unicidad (vía de escritura, consistente de inmediato): re-registrar el mismo
      // código —incluso con OTRA estrategia— se rechaza; la estrategia no se puede «cambiar» reusando
      // el código, y no existe endpoint de edición → estrategia inmutable por construcción (G-157).
      // Nota: la QUERY de lista de IdP tiene desfase read-after-write (G-162); aquí se asevera por la
      // vía de escritura, que es consistente.
      const reRegistro = await request.post(`${API}/tenants/${EVOLITH_TENANT_ID}/identity-providers`, {
        headers: await H(request), data: { code, name: 'IdP Otro', description: 'reintento con otra estrategia', strategy: 'Okta' },
      });
      expect([400, 409]).toContain(reRegistro.status());
    },
  );

  invariante(
    { id: 'INV-AF06', contexto: CTX, descripcion: 'Un usuario federado (con identityReference) NO puede tener contraseña local activa', referencia: 'AddUserAccountPasswordCommandHandler (rechaza si IdentityReference != null, FR-012)' },
    async ({ request }) => {
      // Cuenta federada: identityReference/type provistos (vínculo con IdP externo).
      const email = `robosoft.af06.${codigoUnico('u').toLowerCase()}@evolith.example`;
      const crear = await request.post(`${API}/user-accounts`, {
        headers: await H(request), data: { tenantId: EVOLITH_TENANT_ID, branchId: null, email, category: 'Internal', identityReference: `ext-${uid()}`, identityReferenceType: 'HrId' },
      });
      await esperarEstado(crear, 201);
      const id = (await crear.json()).userAccountId as string;
      // Intentar fijar una contraseña local a un usuario federado debe rechazarse.
      const pwd = await request.post(`${API}/user-accounts/${id}/passwords`, { headers: await H(request), data: { password: 'Local.Pwd.2026!' } });
      expect([400, 409]).toContain(pwd.status());
    },
  );

  invariante(
    { id: 'INV-AF09', contexto: CTX, descripcion: 'Patrón Result (sin excepciones): registrar un IdP con estrategia inválida devuelve 4xx controlado, nunca 500', referencia: 'RegisterIdentityProviderCommandHandler (Result.Failure ante estrategia inválida)' },
    async ({ request }) => {
      const invalido = await request.post(`${API}/tenants/${EVOLITH_TENANT_ID}/identity-providers`, {
        headers: await H(request), data: { code: codigoUnico('IDP'), name: 'IdP Malo', description: 'estrategia inexistente', strategy: 'NoExisteEstaEstrategia' },
      });
      expect(invalido.status(), 'estrategia inválida ⇒ 400 controlado, no 500').toBe(400);
      expect(invalido.status()).not.toBe(500);
    },
  );

  // ── PEND: federación diferida (resolución dinámica por reglas, vinculación, Keycloak real) ──
  const pend = [
    { id: 'INV-AF02', d: 'Reglas de resolución por tenant+suite con PRIORIDAD', m: 'Resolución dinámica de IdP por reglas priorizadas; la federación es área diferida (sin ADR/cableado en el piloto).' },
    { id: 'INV-AF03', d: 'Resolución por dominio (domainHints)', m: 'Resolución por domainHints no cableada en el piloto (federación diferida).' },
    { id: 'INV-AF04', d: 'Fallback ENCADENADO (chained fallback)', m: 'El fallback encadenado de resolución de IdP no está implementado (FAIL en auditoría; federación diferida).' },
    { id: 'INV-AF05', d: 'Resolución dinámica en LOGIN usa las reglas (prioridad/suite/fallback)', m: 'El login no ejerce la resolución dinámica por reglas en el piloto (federación diferida).' },
    { id: 'INV-AF07', d: 'La contraseña se desactiva AL VINCULAR (transición local→federado)', m: 'La transición local→federado (vinculación) no tiene endpoint en el piloto; AF06 cubre el invariante estático (federado sin contraseña local).' },
    { id: 'INV-AF08', d: 'Autenticación federada REAL contra Keycloak (OIDC) tras IdpAdapter', m: 'Requiere un IdP OIDC real (Keycloak) detrás del adaptador; no disponible de forma determinista en este arnés (Testcontainers vive en el carril de integración).' },
  ];
  for (const p of pend) {
    invariantePendiente({ id: p.id, contexto: CTX, descripcion: p.d, referencia: 'federación diferida (auth local funciona)', motivo: p.m }, async () => { /* PEND */ });
  }
});
