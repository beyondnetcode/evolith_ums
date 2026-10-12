import { test, expect, type Page, type APIResponse } from '@playwright/test';

/**
 * Valores de Parámetros · alcance GLOBAL — Matriz de Cobertura de UI, sección C
 * (ver `reference/qa/ui-coverage-matrix.md`).
 *
 * ── Hallazgo de superficie (importante) ──────────────────────────────────────────────
 * La superficie 1 pedida —«valores GLOBALES / por-INQUILINO expuestos en el detalle de una
 * Definición de Parámetro dentro de /parameter-catalog»— NO existe en el frontend. El
 * `ParameterCatalogDetailPanel` sólo muestra metadatos de la definición (código, tipo, alcance,
 * default, orden, versión); no hay panel de valores ni controles publicar/archivar, y no aparece
 * ninguna referencia a `global-values`/`tenant-values` en todo `src/` (los endpoints backend
 * existen pero ningún componente los consume). Ver reporte de gaps.
 *
 * La superficie de VALORES con UI real es la agregada `AppConfiguration` (un valor = una
 * AppConfiguration ligada al código de una Definición). Sus valores GLOBALES se administran en la
 * pantalla dedicada `GlobalAppConfigurationDashboardScreen` (ruta `/app-configurations/global`,
 * sólo admin interno), que NINGÚN spec cubría aún. Este archivo la certifica de punta a punta,
 * ligada a una definición creada por el propio test (no ensucia las sembradas):
 *
 *   1. HAPPY · crear un VALOR GLOBAL para una definición propia · «Agregar» → picker de
 *      definiciones → seleccionar la definición E2E (alcance Global, con valor por defecto) →
 *      «Add» → **POST /app-configurations 201** (el valor global se crea en el backend) → toast de
 *      éxito y auto-selección → el detalle nace en **Draft**.
 *   2. LIFECYCLE · Draft → Publicar → Archivar sobre ese valor, aseverando el estado REAL por el
 *      `status-badge` (`data-status`) tras cada transición confirmada por HTTP 204 (sin flip
 *      optimista; en la línea de [G-129]).
 *   3. Limpieza idempotente · se elimina el valor por UI (**DELETE 204**) y la definición propia
 *      por API. Código único por corrida (timestamp) → sin colisión aunque una corrida previa
 *      dejara residuo.
 *
 * ── Certificación por señales robustas ───────────────────────────────────────────────
 * A diferencia del CRUD de definiciones (que NO emite toasts, G-146), el alta de valores SÍ notifica
 * éxito; aun así cada aserción se ata al **status HTTP** (`waitForResponse`) y al **estado del panel**
 * (`status-badge[data-status]`, presencia/ausencia de fila), no a texto frágil.
 */
const ADMIN = { email: 'admin@evolith.example', password: 'Evolith.Dev.2026' };
/** DEFAULT_TENANT_ID del frontend (request-context.ts); el backend deriva IsInternalAdmin del JWT. */
const TENANT_ID = '5f4e3d2c-1b0a-9f8e-7d6c-543210987654';

/** Definición propia (alcance Global=1, con valor por defecto → seleccionable en el picker). */
const uniqueCode = () => `E2E_VAL_${Date.now()}`;

const addBtn = (page: Page) => page.locator('button[title="Agregar"]').first();
const pickerSearch = (page: Page) => page.locator('input[placeholder="Search parameters..."]');
const listSearch = (page: Page) => page.locator('[data-testid="list-search"]');
const searchSubmitBtn = (page: Page) => page.getByRole('button', { name: /^buscar$/i });
const detailDeleteBtn = (page: Page) => page.locator('button[aria-label="Eliminar"]').first();
const confirmDialog = (page: Page) => page.getByRole('dialog');
const rowByText = (page: Page, text: string) =>
  page.locator('[data-testid="entity-row"]').filter({ hasText: text });
const statusBadge = (page: Page, status: string) =>
  page.locator(`[data-testid="status-badge"][data-status="${status}"]`).first();
const successToast = (page: Page) =>
  page.locator('[data-testid="toast"][data-toast-type="success"]').first();

function apiHeaders(cookies: { name: string; value: string }[]): Record<string, string> {
  const headers: Record<string, string> = { 'X-Tenant-Id': TENANT_ID };
  const xsrf = cookies.find(c => c.name === 'XSRF-TOKEN')?.value;
  if (xsrf) headers['X-CSRF-Token'] = xsrf;
  return headers;
}

/**
 * Crea una Definición de Parámetro por API y devuelve su id. El endpoint responde 201 con SÓLO el
 * Guid (`.Produces<Guid>`), así que se parsea el cuerpo como string/objeto.
 */
async function createDefinitionViaApi(
  page: Page,
  opts: { code: string; name: string; defaultValue: string }
): Promise<string> {
  const headers = apiHeaders(await page.context().cookies());
  const resp: APIResponse = await page.request.post('/api/v1/parameter-definitions', {
    headers,
    data: {
      code: opts.code,
      name: opts.name,
      description: 'Definición E2E de valores globales',
      dataTypeId: 1,
      defaultValue: opts.defaultValue,
      scopeId: 1, // Global → seleccionable en el picker (ALLOWED_SCOPES = [1, 3])
      isMandatory: false,
      displayOrder: 0,
    },
  });
  expect(resp.status(), await resp.text()).toBe(201);
  const raw = (await resp.text()).trim();
  // El cuerpo es el Guid: puede venir como `"guid"` (JSON string) o como `{ "id": "guid" }`.
  try {
    const parsed = JSON.parse(raw);
    return typeof parsed === 'string' ? parsed : parsed.id;
  } catch {
    return raw.replace(/^"|"$/g, '');
  }
}

/** Elimina la definición propia por API (mejor esfuerzo; ya sin valores tras borrar el global). */
async function deleteDefinitionViaApi(page: Page, id: string): Promise<void> {
  if (!id) return;
  const headers = apiHeaders(await page.context().cookies());
  await page.request.delete(`/api/v1/parameter-definitions/${id}`, { headers });
}

/** Elimina el valor (AppConfiguration) seleccionado por UI y devuelve el status del DELETE. */
async function deleteSelectedValue(page: Page): Promise<number> {
  await detailDeleteBtn(page).click();
  await expect(confirmDialog(page)).toBeVisible({ timeout: 15000 });
  const delResp = page.waitForResponse(
    r => r.url().includes('/app-configurations/') && r.request().method() === 'DELETE',
    { timeout: 20000 }
  );
  await confirmDialog(page)
    .getByRole('button', { name: /^eliminar$/i })
    .click();
  return (await delResp).status();
}

test.describe('Valores de Parámetros · alcance Global (ciclo de vida)', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
    await page.getByLabel(/correo electrónico/i).fill(ADMIN.email);
    await page.getByLabel(/contraseña/i).fill(ADMIN.password);
    await page.getByRole('button', { name: /ingresar/i }).click();
    await expect(page).toHaveURL(/\/tenants/);

    // Pantalla dedicada de valores GLOBALES (requiere admin interno; si no lo fuera, redirige).
    await page.goto('/app-configurations/global');
    await expect(page).toHaveURL(/\/app-configurations\/global/, { timeout: 15000 });
  });

  test('HAPPY · crear valor global + Draft→Publicar→Archivar refleja el estado real; limpieza', async ({
    page,
  }) => {
    const code = uniqueCode();
    const name = `Parámetro valor E2E ${code}`;
    let definitionId = '';

    try {
      // ── Preparar: definición propia con valor por defecto (aparece en el picker) ──
      definitionId = await createDefinitionViaApi(page, { code, name, defaultValue: 'false' });
      await page.reload();
      await expect(page).toHaveURL(/\/app-configurations\/global/, { timeout: 15000 });

      // ── (1) Crear el VALOR GLOBAL por UI vía el picker de definiciones ────────────
      await addBtn(page).click();
      await expect(pickerSearch(page)).toBeVisible({ timeout: 15000 });
      await pickerSearch(page).fill(code); // acota el picker (getAll con search)

      const defRow = page.getByRole('button', { name: new RegExp(name, 'i') });
      await expect(defRow).toBeVisible({ timeout: 15000 });
      await defRow.click();

      const createResp = page.waitForResponse(
        r => r.url().includes('/app-configurations') && r.request().method() === 'POST',
        { timeout: 20000 }
      );
      await page.getByRole('button', { name: /^add(\s*\(\d+\))?$/i }).click();
      expect((await createResp).status()).toBe(201); // el valor global se creó en el backend
      await expect(successToast(page)).toBeVisible({ timeout: 15000 });

      // Auto-selección del valor recién creado → el detalle nace en Draft.
      await expect(statusBadge(page, 'Draft')).toBeVisible({ timeout: 15000 });

      // ── (2) Draft → Publicar (el badge refleja el estado persistido, no optimista) ─
      const pubResp = page.waitForResponse(
        r => r.url().includes('/publish') && r.request().method() === 'POST',
        { timeout: 20000 }
      );
      await page.getByRole('button', { name: /^publicar$/i }).click();
      expect((await pubResp).status()).toBe(204);
      await expect(statusBadge(page, 'Published')).toBeVisible({ timeout: 15000 });

      // ── (3) Published → Archivar ─────────────────────────────────────────────────
      const arcResp = page.waitForResponse(
        r => r.url().includes('/archive') && r.request().method() === 'POST',
        { timeout: 20000 }
      );
      await page.getByRole('button', { name: /^archivar$/i }).click();
      expect((await arcResp).status()).toBe(204);
      await expect(statusBadge(page, 'Archived')).toBeVisible({ timeout: 15000 });

      // ── (4) Limpieza: eliminar el valor por UI (DELETE 204) y confirmar que desaparece ─
      const delStatus = await deleteSelectedValue(page);
      expect(delStatus).toBe(204);
      await listSearch(page).fill(code);
      await searchSubmitBtn(page).click();
      await expect(rowByText(page, code)).toHaveCount(0, { timeout: 15000 });
    } finally {
      // La definición ya no tiene valores → DELETE limpio (idempotente).
      await deleteDefinitionViaApi(page, definitionId);
    }
  });
});
