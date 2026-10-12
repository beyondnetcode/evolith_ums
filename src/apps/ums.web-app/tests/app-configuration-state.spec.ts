import { test, expect, type Page, type APIResponse } from '@playwright/test';

/**
 * Configuraciones de Aplicación (AppConfiguration) · ciclo de vida — Matriz de Cobertura de UI,
 * sección C (ver `reference/qa/ui-coverage-matrix.md`).
 *
 * Certifica el fix [G-143]: existe `DELETE /app-configurations/{id}` (agregado hoja, sin guarda de
 * dependencias → 204). Pantalla `AppConfigurationDashboardScreen`. Ahora que [G-144] arregló el
 * picker (`ParameterDefinitionPickerDialog` usa `parameterCatalogService.getAll(...)`), el alta por
 * UI carga parámetros y el clic del usuario SÍ llega al backend.
 *
 * Casos:
 *   1. HAPPY · crear por UI (certifica G-144 + G-143) · «Agregar» → picker de parámetros (ya poblado
 *      por G-144) → seleccionar una Definición Global&Tenant con valor por defecto y SIN
 *      AppConfiguration Global previa (`MFA_REQUIRED_FOR_ADMIN`) → «Add» → **POST 201** (la config
 *      Global se crea en el backend). Luego se localiza esa config Global por búsqueda y se elimina
 *      por UI → **DELETE 204**, y desaparece de la lista.
 *   2. LIFECYCLE · Draft → Publicar → Archivar sobre una config fixture, certificando que los
 *      controles del detalle reflejan el estado REAL (badge `data-status`) sin flip optimista; se
 *      limpia con Eliminar al final (idempotente).
 *
 * ── ⚠ Bug #3 en el ALTA por UI (NO es G-143/144/145; ver reporte) ───────────────────
 * El clic «Add» dispara `POST /app-configurations` que responde **201** con `{ appConfigurationId }`
 * (backend `CreateAppConfigurationResponse` sólo trae el id). Pero el servicio del frontend
 * (`app-configuration.service.ts` · `createAppConfiguration`) valida la respuesta con
 * `CreateAppConfigurationResponseSchema` (`app-configuration.schema.ts`), que exige además `code`
 * (string) → el parseo LANZA (`invalid_type` en `code`). Consecuencia: la config SÍ se crea (201)
 * pero la UI muestra un toast de ERROR y NO auto-selecciona ni notifica éxito. Por eso el caso 1 NO
 * asevera toast de éxito ni auto-select tras «Add»; certifica el 201 y luego localiza+elimina la
 * config por UI (que es el fix G-143 bajo prueba).
 *
 * Idempotencia: el caso 1 pre-limpia cualquier config Global residual con ese código (por si una
 * corrida previa se interrumpió) y siempre elimina lo que crea; el caso 2 usa un código sintético
 * único por corrida. Auth por cookie de sesión (JWT); `page.request` comparte cookies del contexto.
 */
const ADMIN = { email: 'admin@evolith.example', password: 'Evolith.Dev.2026' };
/** DEFAULT_TENANT_ID del frontend (request-context.ts); el backend deriva IsInternalAdmin del JWT. */
const TENANT_ID = '5f4e3d2c-1b0a-9f8e-7d6c-543210987654';

/**
 * Definición sembrada scope Global&Tenant, valor por defecto ("false") y SIN AppConfiguration Global
 * sembrada (sólo por inquilino) → crear su config Global es determinista y no colisiona.
 */
const PICK_PARAM_NAME = 'MFA Required for Admin';
const EXPECTED_CODE = 'MFA_REQUIRED_FOR_ADMIN';

const uniqueCode = () => `E2E_APPCFG_${Date.now()}`;

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

function apiHeaders(cookies: { name: string; value: string }[]): Record<string, string> {
  const headers: Record<string, string> = { 'X-Tenant-Id': TENANT_ID };
  const xsrf = cookies.find(c => c.name === 'XSRF-TOKEN')?.value;
  if (xsrf) headers['X-CSRF-Token'] = xsrf;
  return headers;
}

/** Crea una AppConfiguration Global (Draft) vía API como fixture. */
async function createConfigViaApi(page: Page, code: string): Promise<void> {
  const headers = apiHeaders(await page.context().cookies());
  const resp: APIResponse = await page.request.post('/api/v1/app-configurations', {
    headers,
    data: { code, value: 'e2e', description: 'Fixture E2E G-143' },
  });
  expect(resp.status(), await resp.text()).toBe(201);
}

/** Idempotencia defensiva: borra por API cualquier AppConfiguration Global con ese código (residuo). */
async function purgeGlobalConfig(page: Page, code: string): Promise<void> {
  const headers = apiHeaders(await page.context().cookies());
  const resp = await page.request.get(
    `/api/v1/app-configurations?page=1&pageSize=50&search=${encodeURIComponent(code)}`,
    { headers }
  );
  if (!resp.ok()) return;
  const body = await resp.json();
  for (const c of body.items ?? []) {
    if (c.code === code && c.scope === 'Global') {
      await page.request.delete(`/api/v1/app-configurations/${c.appConfigurationId}`, { headers });
    }
  }
}

/** Busca por código y abre la configuración en el detalle. */
async function selectByCode(page: Page, code: string): Promise<void> {
  await listSearch(page).fill(code);
  await searchSubmitBtn(page).click();
  const row = rowByText(page, code).first();
  await expect(row).toBeVisible({ timeout: 15000 });
  await row.click();
  await expect(detailDeleteBtn(page)).toBeVisible({ timeout: 15000 }); // detalle cargado
}

/** Elimina la configuración seleccionada por la UI y devuelve el status HTTP del DELETE. */
async function deleteSelected(page: Page): Promise<number> {
  await detailDeleteBtn(page).click();
  await expect(confirmDialog(page)).toBeVisible({ timeout: 15000 });
  const delResp = page.waitForResponse(
    r => r.url().includes('/app-configurations/') && r.request().method() === 'DELETE',
    { timeout: 20000 }
  );
  await confirmDialog(page)
    .getByRole('button', { name: /^eliminar$/i })
    .click();
  const resp = await delResp;
  return resp.status();
}

test.describe('Configuraciones de Aplicación · ciclo de vida', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
    await page.getByLabel(/correo electrónico/i).fill(ADMIN.email);
    await page.getByLabel(/contraseña/i).fill(ADMIN.password);
    await page.getByRole('button', { name: /ingresar/i }).click();
    await expect(page).toHaveURL(/\/tenants/);

    await page.goto('/app-configurations');
    await expect(page).toHaveURL(/\/app-configurations/);
  });

  test('HAPPY · crear por UI (picker) y eliminar (DELETE 204) una config Global (G-144 + G-143)', async ({
    page,
  }) => {
    await purgeGlobalConfig(page, EXPECTED_CODE); // idempotencia: limpiar residuo previo

    // ── Crear por UI: «Agregar» → picker de parámetros (poblado por G-144) ──────
    await addBtn(page).click();
    await expect(pickerSearch(page)).toBeVisible({ timeout: 15000 });
    await pickerSearch(page).fill('MFA'); // acota la lista del picker (getAll con search)

    const paramRow = page.getByRole('button', { name: new RegExp(PICK_PARAM_NAME, 'i') });
    await expect(paramRow).toBeVisible({ timeout: 15000 });
    await paramRow.click();

    const createResp = page.waitForResponse(
      r => r.url().includes('/app-configurations') && r.request().method() === 'POST',
      { timeout: 20000 }
    );
    await page.getByRole('button', { name: /^add(\s*\(\d+\))?$/i }).click();
    // El clic del usuario crea la config Global en el backend (201) y, tras corregir [G-147]
    // (el schema del alta exigía `code`, ausente en la respuesta → el parseo lanzaba y salía un
    // toast de ERROR pese al 201), la UI ahora notifica ÉXITO sin falso error.
    expect((await createResp).status()).toBe(201);
    await expect(
      page.locator('[data-testid="toast"][data-toast-type="success"]').first(),
    ).toBeVisible({ timeout: 15000 });

    // ── Localizar la config Global creada y eliminarla por UI (certifica G-143) ─
    await listSearch(page).fill(EXPECTED_CODE);
    await searchSubmitBtn(page).click();
    const globalRow = page
      .locator('[data-testid="entity-row"]')
      .filter({ hasText: EXPECTED_CODE })
      .filter({ hasText: 'Global' })
      .first();
    await expect(globalRow).toBeVisible({ timeout: 15000 });
    await globalRow.click();
    await expect(detailDeleteBtn(page)).toBeVisible({ timeout: 15000 });

    const status = await deleteSelected(page);
    expect(status).toBe(204);

    // Desaparece: la fila Global con ese código ya no existe (las de inquilino permanecen intactas).
    await expect(
      page
        .locator('[data-testid="entity-row"]')
        .filter({ hasText: EXPECTED_CODE })
        .filter({ hasText: 'Global' })
    ).toHaveCount(0, { timeout: 15000 });
  });

  test('LIFECYCLE · Draft → Publicar → Archivar refleja el estado real; limpieza por Eliminar', async ({
    page,
  }) => {
    const code = uniqueCode();
    await createConfigViaApi(page, code);
    await selectByCode(page, code);

    // Nace en Draft.
    await expect(statusBadge(page, 'Draft')).toBeVisible({ timeout: 15000 });

    // Draft → Publicar (el botón sólo aparece en Draft; el badge refleja el estado persistido).
    const pubResp = page.waitForResponse(
      r => r.url().includes('/publish') && r.request().method() === 'POST',
      { timeout: 20000 }
    );
    await page.getByRole('button', { name: /^publicar$/i }).click();
    expect((await pubResp).status()).toBe(204);
    await expect(statusBadge(page, 'Published')).toBeVisible({ timeout: 15000 });

    // Published → Archivar.
    const arcResp = page.waitForResponse(
      r => r.url().includes('/archive') && r.request().method() === 'POST',
      { timeout: 20000 }
    );
    await page.getByRole('button', { name: /^archivar$/i }).click();
    expect((await arcResp).status()).toBe(204);
    await expect(statusBadge(page, 'Archived')).toBeVisible({ timeout: 15000 });

    // Limpieza: eliminar la configuración archivada (G-143 no tiene guarda de estado).
    const status = await deleteSelected(page);
    expect(status).toBe(204);
    await expect(rowByText(page, code)).toHaveCount(0, { timeout: 15000 });
  });
});
