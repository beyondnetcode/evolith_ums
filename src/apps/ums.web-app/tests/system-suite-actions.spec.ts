import { test, expect, type Page, type Locator } from '@playwright/test';

/**
 * Acciones del Sistema (Sistemas/Suites) · catálogo CRUD
 *   Matriz de Cobertura de UI — reference/qa/ui-coverage-matrix.md, sección E
 *   (fila «Acciones: register/remove (`use*Action`)»).
 *
 * El catálogo de acciones vive en la pestaña «Acciones» del detalle de un Suite
 * (SystemSuiteDetailPanel.tsx):
 *   /system-suites → seleccionar «Aduanas» → pestaña «Acciones»
 *     → ListToolbar «Nueva Acción del Sistema» abre un InlineAddForm
 *       (Código de Acción / Nombre de la Acción · submit «Guardar Acción»).
 *     → cada acción es una tarjeta `group/action` con nombre + código y, en hover,
 *       Editar nombre (Pencil, SOLO en vista «lista») y Remover Acción (Trash2).
 *     → la vista por defecto es «thumbnail» (sin editar); el renombrado exige
 *       conmutar a «lista» con el M3SegmentedButton del toolbar.
 *
 * HECHOS DE PRODUCCIÓN VERIFICADOS (leídos del dominio/hook, no adivinados):
 *   - `SystemSuite.RegisterAction` (SystemSuite.cs:342-347) RECHAZA un código duplicado
 *     (`DomainErrors.Common.Duplicate`) → error-path forzable por UI.
 *   - Toasts tipados (use-notified-mutation.ts): registrar/renombrar → `success`;
 *     remover acción → `warning`; fallo → `error`.
 *   - Anti-[G-129]: ante el rechazo del backend, `handleRegisterAction` cae al `catch`
 *     (SystemSuiteDetailPanel.tsx:206) → el formulario NO se limpia ni se cierra y NO
 *     aparece una tarjeta duplicada (nada de éxito falso/flip optimista).
 *
 * IDEMPOTENCIA: cada acción usa un código único por timestamp y se ELIMINA al final.
 */
const ADMIN = { email: 'admin@evolith.example', password: 'Evolith.Dev.2026' };
/** Suite sembrada (AuthorizationDevDataSeeder) presente en la 1ª página (orden A-Z). */
const SUITE_NAME = 'Aduanas';

const successToast = (page: Page) =>
  page.locator('[data-testid="toast"][data-toast-type="success"]').first();
const errorToast = (page: Page) =>
  page.locator('[data-testid="toast"][data-toast-type="error"]').first();
const warningToast = (page: Page) =>
  page.locator('[data-testid="toast"][data-toast-type="warning"]').first();

/** Tarjeta de una acción (div `group/action`), scopeada por su código único. */
const actionCard = (page: Page, code: string): Locator =>
  page.locator('[class*="group/action"]').filter({ hasText: code });

/** Panel de la pestaña «Acciones»: contenedor que aloja el AddButton (lo desambigua del panel de listado). */
const actionsPanel = (page: Page): Locator =>
  page
    .locator('div.space-y-1\\.5')
    .filter({ has: page.locator('button[title="Nueva Acción del Sistema"]') })
    .first();

/** Abre «Nueva Acción del Sistema», completa código+nombre y confirma. */
async function registerAction(page: Page, code: string, name: string): Promise<void> {
  await page.locator('button[title="Nueva Acción del Sistema"]').click();
  await page.getByLabel(/código de acción/i).fill(code);
  await page.getByLabel(/nombre de la acción/i).fill(name);
  await page.getByRole('button', { name: 'Guardar Acción' }).click();
  await expect(successToast(page)).toBeVisible({ timeout: 15000 });
  await expect(actionCard(page, code)).toBeVisible({ timeout: 15000 });
}

/** Limpieza idempotente: remueve la acción por su código → toast `warning`. */
async function removeAction(page: Page, code: string): Promise<void> {
  const card = actionCard(page, code);
  if (!(await card.count())) return;
  await card.first().getByRole('button', { name: 'Remover Acción', exact: true }).click();
  await expect(warningToast(page)).toBeVisible({ timeout: 15000 });
  await expect(actionCard(page, code)).toHaveCount(0, { timeout: 15000 });
}

test.describe('Acciones del Sistema · catálogo (happy + error)', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
    await page.getByLabel(/correo electrónico/i).fill(ADMIN.email);
    await page.getByLabel(/contraseña/i).fill(ADMIN.password);
    await page.getByRole('button', { name: /ingresar/i }).click();
    await expect(page).toHaveURL(/\/tenants/);

    await page.goto('/system-suites');
    await expect(page).toHaveURL(/\/system-suites/);

    const row = page.locator('[data-testid="entity-row"]').filter({ hasText: SUITE_NAME }).first();
    await expect(row).toBeVisible({ timeout: 20000 });
    await row.click();

    const actionsTab = page.getByRole('button', { name: /^acciones$/i });
    await expect(actionsTab).toBeVisible({ timeout: 15000 });
    await actionsTab.click();
    await expect(page.locator('button[title="Nueva Acción del Sistema"]')).toBeVisible({
      timeout: 15000,
    });
  });

  test('HAPPY · registrar una acción del sistema emite éxito y aparece la tarjeta', async ({
    page,
  }) => {
    const stamp = Date.now();
    const code = `E2E_ACT_C_${stamp}`;
    const name = `E2E Accion Crear ${stamp}`;

    await registerAction(page, code, name);
    // La tarjeta muestra tanto el nombre como el código registrado (formatSystemCode → mayúsculas).
    await expect(actionCard(page, code)).toContainText(name);

    await removeAction(page, code);
  });

  test('HAPPY · renombrar una acción (vista lista) refleja el nuevo nombre', async ({ page }) => {
    const stamp = Date.now();
    const code = `E2E_ACT_R_${stamp}`;
    const name = `E2E Accion Editar ${stamp}`;
    const nuevoNombre = `E2E Accion Editada ${stamp}`;

    await registerAction(page, code, name);

    // El renombrado solo existe en vista «lista»: conmutar con el 1er botón del segmentado
    // (LayoutList) DENTRO del toolbar de acciones (evita el segmentado del panel de listado).
    await actionsPanel(page).locator('.group\\/seg-btn button').first().click();

    const card = actionCard(page, code);
    await card.getByRole('button', { name: 'Editar nombre', exact: true }).click();
    // El nombre se reemplaza por un <input> autofocus dentro de la tarjeta.
    const input = card.locator('input');
    await input.fill(nuevoNombre);
    await input.press('Enter');

    await expect(successToast(page)).toBeVisible({ timeout: 15000 });
    await expect(actionCard(page, code)).toContainText(nuevoNombre, { timeout: 15000 });

    await removeAction(page, code);
  });

  test('ERROR · registrar un código duplicado falla y NO crea tarjeta doble (G-129)', async ({
    page,
  }) => {
    const stamp = Date.now();
    const code = `E2E_ACT_DUP_${stamp}`;
    const name = `E2E Accion Dup ${stamp}`;

    // Alta legítima.
    await registerAction(page, code, name);
    await expect(actionCard(page, code)).toHaveCount(1);

    // Segundo intento con el MISMO código → backend rechaza (Common.Duplicate).
    await page.locator('button[title="Nueva Acción del Sistema"]').click();
    await page.getByLabel(/código de acción/i).fill(code);
    await page.getByLabel(/nombre de la acción/i).fill(`${name} bis`);
    await page.getByRole('button', { name: 'Guardar Acción' }).click();

    // (1) Sale un toast de ERROR (no un aviso de éxito prematuro).
    await expect(errorToast(page)).toBeVisible({ timeout: 15000 });
    // (2) No hay éxito falso: sigue existiendo UNA sola tarjeta con ese código.
    await expect(actionCard(page, code)).toHaveCount(1);

    // Cerrar el formulario abierto y limpiar.
    await page.getByRole('button', { name: /cancelar/i }).first().click();
    await removeAction(page, code);
  });
});
