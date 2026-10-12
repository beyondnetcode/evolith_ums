import { test, expect, type Page } from '@playwright/test';

/**
 * Perfiles de Autorización · TRAMO DE PERFIL (US-009…012) — G-076.
 *
 * Cierra el hueco E2E del ciclo funcional de un Perfil de Autorización que NO cubren
 * `profile-state.spec.ts` (toggle Activar/Desactivar del perfil, crear-error, rechazo de
 * solicitud G-151, override-Allow idempotente) ni `profile-panel.spec.ts` (drawer del usuario
 * conectado). Ref: `reference/qa/ui-coverage-matrix.md`, sección H (Autorización · Perfiles).
 *
 * Pantalla: ProfileDashboardScreen (`/profiles`). Panel de detalle ProfileDetailPanel con dos
 * sub-pestañas: «Resumen» y «Permisos Efectivos».
 *
 * Qué certifica (materialización + asignación manual):
 *   1. MATERIALIZACIÓN (US-009/010/011): un perfil sembrado materializa su rol asociado y sus
 *      permisos efectivos (fusión plantilla base + overrides). Se asevera que el conteo de
 *      permisos de la LISTA coincide con el conteo «N permisos efectivos» del detalle (fuente de
 *      verdad cruzada) y que la sub-pestaña «Permisos Efectivos» renderiza el árbol materializado
 *      (controles Allow/Neutral/Deny reales), no el estado vacío.
 *   2. ASIGNACIÓN MANUAL (US-012): la activación/desactivación de un permiso INDIVIDUAL del perfil
 *      (`useActivateProfilePermission`/`useDeactivateProfilePermission`, hooks sin cobertura previa)
 *      persiste el efecto REAL contra el servidor (status HTTP 2xx + toast tipado success/info) y el
 *      control refleja el estado persistido tras el refetch (sin flip optimista; anti-G-129). El test
 *      es ADAPTATIVO al estado vigente y RESTAURA el estado inicial (idempotente sobre dato compartido).
 *
 * Perfil objetivo (determinista): el perfil SIL de `admin.callao@evolith.example`
 * (AuthorizationDevDataSeeder), rol ADMINISTRADOR, 40 permisos materializados. Es un perfil
 * sembrado DISTINTO del admin actuante, de modo que mutar un permiso individual no altera la
 * autorización de la propia sesión. Se selecciona por búsqueda server-side «Por Usuario».
 *
 * Selección robusta: data-testid compartidos (entity-row, list-search, toast) + la clase de icono
 * lucide del interruptor de estado (`lucide-toggle-left`/`lucide-toggle-right`), que es el único
 * interruptor de ese tipo presente en la pantalla (el diálogo de creación está cerrado).
 */
const ADMIN = { email: 'admin@evolith.example', password: 'Evolith.Dev.2026' };
const TARGET_USER = 'admin.callao@evolith.example';

const toast = (page: Page, type: 'success' | 'info' | 'warning' | 'error') =>
  page.locator(`[data-testid="toast"][data-toast-type="${type}"]`).first();

/** Interruptor de estado (activo/inactivo) de un permiso individual dentro del árbol efectivo. */
const permToggle = (page: Page) =>
  page.locator('button:has(svg.lucide-toggle-left), button:has(svg.lucide-toggle-right)').first();

/** ¿El interruptor está en «activo» (icono toggle-right)? */
async function isToggleActive(page: Page): Promise<boolean> {
  return (await permToggle(page).locator('svg.lucide-toggle-right').count()) > 0;
}

/** Extrae el primer entero de un texto (p. ej. «40 permisos» → 40). */
function firstInt(text: string): number {
  const m = text.match(/\d+/);
  return m ? parseInt(m[0], 10) : NaN;
}

/**
 * Busca el perfil objetivo «Por Usuario» (búsqueda server-side vía botón «Buscar») y lo selecciona.
 * Deja el ProfileDetailPanel abierto. Devuelve el conteo de permisos que muestra la fila de lista.
 */
async function selectTargetProfile(page: Page): Promise<number> {
  const search = page.locator('[data-testid="list-search"]').last();
  await expect(search).toBeVisible({ timeout: 15000 });
  await search.fill(TARGET_USER);
  await page
    .getByRole('button', { name: /^buscar$/i })
    .last()
    .click();

  const row = page.locator('[data-testid="entity-row"]').filter({ hasText: TARGET_USER }).first();
  await expect(row).toBeVisible({ timeout: 15000 });
  const rowPermisos = firstInt((await row.textContent()) ?? '');
  await row.click();

  // El detalle carga el Perfil Efectivo del perfil seleccionado.
  await expect(page.getByText(/perfil efectivo/i).first()).toBeVisible({ timeout: 15000 });
  return rowPermisos;
}

test.describe('Perfil · tramo de perfil (materialización + asignación manual) — G-076', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
    await page.getByLabel(/correo electrónico/i).fill(ADMIN.email);
    await page.getByLabel(/contraseña/i).fill(ADMIN.password);
    await page.getByRole('button', { name: /ingresar/i }).click();
    await expect(page).toHaveURL(/\/tenants/);

    await page.goto('/profiles');
    await expect(page).toHaveURL(/\/profiles/);
  });

  // ── US-009/010/011 · MATERIALIZACIÓN DEL PERFIL EFECTIVO ─────────────────────────
  test('MATERIALIZACIÓN · el perfil sembrado materializa rol + permisos efectivos y el conteo cuadra', async ({
    page,
  }) => {
    const rowPermisos = await selectTargetProfile(page);
    expect(rowPermisos, 'la fila de lista muestra un conteo de permisos > 0').toBeGreaterThan(0);

    // Sub-pestaña «Resumen»: el perfil expone su ROL ASOCIADO y sus PERMISOS MATERIALIZADOS.
    await expect(page.getByText(/rol asociado/i)).toBeVisible();
    await expect(page.getByText(/permisos materializados/i).first()).toBeVisible();

    // Fuente de verdad cruzada: «N permisos efectivos» del detalle == «N permisos» de la lista.
    // Regex en minúsculas y sensible a mayúsculas: matchea el texto del conteo («40 permisos
    // efectivos») sin colisionar con la pestaña homónima «Permisos Efectivos» ni usar cuantificadores.
    const efectivos = page.getByText(/permisos efectivos/).first();
    await expect(efectivos).toBeVisible({ timeout: 15000 });
    const detailCount = firstInt((await efectivos.textContent()) ?? '');
    expect(detailCount, 'el conteo materializado del detalle coincide con el de la lista').toBe(
      rowPermisos
    );

    // Sub-pestaña «Permisos Efectivos»: el árbol materializado se renderiza (controles reales),
    // NO el estado vacío «Ningún permiso materializado. Asocie una plantilla primero.».
    await page.getByRole('button', { name: /permisos efectivos/i }).click();
    await expect(page.getByText(/ningún permiso materializado/i)).toHaveCount(0);
    await expect(page.getByRole('button', { name: /^allow$/i }).first()).toBeVisible({
      timeout: 15000,
    });
    // El interruptor de estado por permiso también está materializado (asignación manual disponible).
    await expect(permToggle(page)).toBeVisible({ timeout: 15000 });
  });

  // ── US-012 · ASIGNACIÓN MANUAL: activar/desactivar un permiso individual ─────────
  test('ASIGNACIÓN MANUAL · activar/desactivar un permiso individual persiste el efecto real (anti-G-129)', async ({
    page,
  }) => {
    await selectTargetProfile(page);
    await page.getByRole('button', { name: /permisos efectivos/i }).click();

    const toggle = permToggle(page);
    await expect(toggle).toBeVisible({ timeout: 15000 });
    const startedActive = await isToggleActive(page);

    // Regex LITERAL (sin construcción dinámica): matchea ambos verbos; el efectivo se lee de la URL.
    const permMutRe = /\/profiles\/[^/]+\/permissions\/[^/]+\/(activate|deactivate)$/;
    const verbOf = (url: string): string =>
      new URL(url).pathname.endsWith('/activate') ? 'activate' : 'deactivate';

    // (1) Primer cambio: el efecto se persiste (POST activate|deactivate → 2xx) y el toast tipado
    //     lo confirma. La respuesta HTTP es la fuente de verdad (no un flip local).
    const resp1P = page.waitForResponse(
      r => r.request().method() === 'POST' && permMutRe.test(new URL(r.url()).pathname),
      { timeout: 20000 }
    );
    await toggle.click();
    const resp1 = await resp1P;
    expect(resp1.ok(), 'POST activate|deactivate debe persistir (2xx)').toBeTruthy();
    // El verbo disparado corresponde al estado inicial (activo→deactivate / inactivo→activate).
    expect(verbOf(resp1.url())).toBe(startedActive ? 'deactivate' : 'activate');

    // Toast tipado: activar=success / desactivar=info. Nunca error.
    await expect(toast(page, startedActive ? 'info' : 'success')).toBeVisible({ timeout: 15000 });
    await expect(page.locator('[data-testid="toast"][data-toast-type="error"]')).toHaveCount(0);

    // El control refleja el estado REAL tras el refetch (invalidación ['profiles']): el interruptor
    // quedó en el estado opuesto al inicial (sin flip optimista pre-commit).
    await expect.poll(() => isToggleActive(page), { timeout: 15000 }).toBe(!startedActive);

    // (2) RESTAURAR el estado inicial (idempotencia sobre dato compartido): nueva persistencia real.
    const resp2P = page.waitForResponse(
      r => r.request().method() === 'POST' && permMutRe.test(new URL(r.url()).pathname),
      { timeout: 20000 }
    );
    await permToggle(page).click();
    const resp2 = await resp2P;
    expect(resp2.ok(), 'POST de restauración debe persistir (2xx)').toBeTruthy();
    expect(verbOf(resp2.url())).toBe(startedActive ? 'activate' : 'deactivate');

    await expect.poll(() => isToggleActive(page), { timeout: 15000 }).toBe(startedActive);
  });
});
