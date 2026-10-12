import { test, expect, type Page, type APIResponse } from '@playwright/test';

/**
 * Proveedores de Identidad (IdP) por inquilino — Matriz de Cobertura de UI, sección C
 * (ver `reference/qa/ui-coverage-matrix.md`).
 *
 * Superficie: `IdpPanel` (`src/presentation/identity/tenant/components/IdpPanel.tsx`), montado en la
 * pestaña «Prov. Identidad» del detalle de un Tenant (`TenantDetailPanel`, tab `providers`). Se opera
 * sobre la organización PROPIA del admin (botón «Mi Organización» → tenant raíz EVOLITH, cuyo id es el
 * DEFAULT_TENANT_ID), evitando el caso hijo («IdP gestionado por la organización padre»).
 *
 * ── ⚠ Dos bugs de contrato bloquean el ALTA por UI (ver reporte; NO se corrige producción) ──────────
 *  (B1) Estrategia inválida: el `<select>` de protocolo sólo ofrece `OIDC` / `SAML2` / `OAuth2`
 *       (`IDP_STRATEGIES`, idp.constants.ts), pero el backend sólo acepta los nombres de su enum
 *       `IdpStrategy` (InternalBcrypt, Zitadel, AzureAd, Okta, Keycloak, Auth0, Google, Ldap, Saml2,
 *       GenericOidc). Cualquier alta por UI responde **400** «Invalid identity provider strategy: OIDC».
 *  (B2) Descripción exigida: el backend valida `Description` NO vacío, pero el formulario la trata como
 *       opcional (sin marca de requerido) → 400 «'Description' no debería estar vacío» si se deja en blanco.
 * Consecuencia: el registro por UI NO es alcanzable en verde. Se certifica en su lugar lo REACHABLE:
 *
 *   1. LISTA · un IdP creado por API con contrato VÁLIDO (estrategia `GenericOidc`, descripción no vacía)
 *      APARECE en la lista del panel al cargar la pestaña (certifica render + refresco de `loadProviders`).
 *   2. Guarda de modo LOCAL (anti-[G-129]) · el proveedor nace INACTIVO y, con el tenant en modo Local
 *      (switch `auth-mode-toggle` sin marcar), su botón de activación está DESHABILITADO — el estado real
 *      manda, sin activación optimista. Se asevera sólo cuando el switch confirma modo Local.
 *   3. BAJA por UI · eliminar el IdP desde la fila → **DELETE /tenants/{id}/identity-providers/{id}** →
 *      la fila desaparece (este camino de la UI NO está roto). Limpieza idempotente por API como respaldo.
 *
 * ── Qué se OMITE y por qué ────────────────────────────────────────────────────────────
 *  • Alta por UI: bloqueada por B1/B2 (bugs de contrato) → documentada, no aseverada en verde.
 *  • Activar / desactivar: la activación exige poner el tenant en modo IDPs, cambio que «afecta a todos
 *    los usuarios del inquilino» y requiere ya un IdP activo (huevo-y-gallina) → no idempotente ni seguro
 *    en entorno compartido. Se certifica la guarda de modo Local en su lugar.
 *  • Editar por UI: `saveProviderEdit` NO invoca ningún servicio (sólo toast + recarga) → el cambio no se
 *    persiste (bug real; ver reporte). Aseverar persistencia fallaría; se omite.
 *
 * Certificación por señales robustas: status HTTP (`waitForResponse` / `APIResponse`), `entity-row` +
 * `hasText`, y lectura del estado real del switch de modo. Nada de texto frágil ni esperas ciegas.
 */
const ADMIN = { email: 'admin@evolith.example', password: 'Evolith.Dev.2026' };
/** Organización propia del admin (EVOLITH raíz) = DEFAULT_TENANT_ID del frontend (request-context.ts). */
const TENANT_ID = '5f4e3d2c-1b0a-9f8e-7d6c-543210987654';
/** Estrategia VÁLIDA del enum backend `IdpStrategy` (a diferencia de las que ofrece la UI). */
const VALID_STRATEGY = 'GenericOidc';

const stamp = Date.now();
const IDP_NAME = `E2E IdP ${stamp}`;
const IDP_CODE = `E2E_IDP_${stamp}`;

const ownOrgBtn = (page: Page) => page.getByRole('button', { name: /mi organización/i }).first();
// Pestaña del detalle del tenant (label «Prov. Identidad»); evita el nav lateral «Identidad».
const idpTab = (page: Page) => page.getByRole('button', { name: /prov\. identidad/i }).first();
// El «Agregar» del panel de IdP es el ÚLTIMO en el DOM (la lista de tenants aporta el primero).
const addBtn = (page: Page) => page.locator('button[title="Agregar"]').last();
const authModeSwitch = (page: Page) => page.locator('#auth-mode-toggle');
const idpRow = (page: Page, text: string) =>
  page.locator('[data-testid="entity-row"]').filter({ hasText: text }).first();

function apiHeaders(cookies: { name: string; value: string }[]): Record<string, string> {
  const headers: Record<string, string> = { 'X-Tenant-Id': TENANT_ID };
  const xsrf = cookies.find(c => c.name === 'XSRF-TOKEN')?.value;
  if (xsrf) headers['X-CSRF-Token'] = xsrf;
  return headers;
}

/** Registra un IdP por API con contrato VÁLIDO (fixture); devuelve el id creado. */
async function registerIdpViaApi(page: Page): Promise<string> {
  const headers = apiHeaders(await page.context().cookies());
  const resp: APIResponse = await page.request.post(
    `/api/v1/tenants/${TENANT_ID}/identity-providers`,
    {
      headers,
      data: {
        name: IDP_NAME,
        code: IDP_CODE,
        description: 'https://e2e.idp.example/oidc',
        strategy: VALID_STRATEGY,
      },
    }
  );
  expect(resp.status(), await resp.text()).toBe(201);
  const raw = (await resp.text()).trim();
  try {
    const parsed = JSON.parse(raw);
    return typeof parsed === 'string' ? parsed : (parsed.identityProviderId ?? parsed.id);
  } catch {
    return raw.replace(/^"|"$/g, '');
  }
}

/** Baja por API (respaldo idempotente por si la baja por UI no llegó a ejecutarse). */
async function removeIdpViaApi(page: Page, idpId: string): Promise<void> {
  if (!idpId) return;
  const headers = apiHeaders(await page.context().cookies());
  await page.request.delete(`/api/v1/tenants/${TENANT_ID}/identity-providers/${idpId}`, { headers });
}

/** Selecciona la organización propia (tenant raíz) y abre su pestaña de proveedores de identidad. */
async function openOwnTenantIdpTab(page: Page): Promise<void> {
  await expect(ownOrgBtn(page)).toBeVisible({ timeout: 15000 });
  await ownOrgBtn(page).click();
  await expect(idpTab(page)).toBeVisible({ timeout: 15000 });
  await idpTab(page).click();
  await expect(addBtn(page)).toBeVisible({ timeout: 15000 }); // panel IdP montado
}

test.describe('Proveedores de Identidad · listado, guarda de modo y baja por inquilino', () => {
  let idpId = '';

  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
    await page.getByLabel(/correo electrónico/i).fill(ADMIN.email);
    await page.getByLabel(/contraseña/i).fill(ADMIN.password);
    await page.getByRole('button', { name: /ingresar/i }).click();
    await expect(page).toHaveURL(/\/tenants/);

    // Fixture con contrato válido ANTES de abrir la pestaña → `loadProviders` lo trae al montar.
    idpId = await registerIdpViaApi(page);

    await openOwnTenantIdpTab(page);
  });

  test.afterEach(async ({ page }) => {
    await removeIdpViaApi(page, idpId); // respaldo (no-op si la UI ya lo borró)
  });

  test('LISTA muestra el IdP, la activación está bloqueada en modo Local, y la baja por UI lo elimina', async ({
    page,
  }) => {
    // ── (1) El IdP creado por API aparece en la lista del panel ───────────────────
    const row = idpRow(page, IDP_NAME);
    await expect(row).toBeVisible({ timeout: 15000 });
    await expect(row).toContainText(IDP_CODE);

    // ── (2) Guarda de modo Local: el IdP nace inactivo y su activación está deshabilitada ─
    const isLocalMode = !(await authModeSwitch(page).isChecked().catch(() => false));
    if (isLocalMode) {
      // Anti-G-129: sin flip optimista — en modo Local el botón de activar está deshabilitado.
      await expect(row.locator('button[disabled]')).toHaveCount(1, { timeout: 15000 });
    }

    // ── (3) Baja por UI: el camino de eliminación NO está roto → DELETE + fila desaparece ─
    const delResp = page.waitForResponse(
      r =>
        /\/identity-providers\/[0-9a-fA-F-]+$/.test(new URL(r.url()).pathname) &&
        r.request().method() === 'DELETE',
      { timeout: 20000 }
    );
    await row.locator('button[aria-label="Eliminar ubicación"]').click();
    expect([200, 204]).toContain((await delResp).status());

    await expect(idpRow(page, IDP_NAME)).toHaveCount(0, { timeout: 15000 });
  });
});
