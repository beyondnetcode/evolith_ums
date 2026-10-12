import { test, expect, type Page } from '@playwright/test';

/**
 * Sistemas (System Suites) · ciclo de estado — Ola 1 de la Matriz de Cobertura de UI (G-130).
 *
 * Cubre las acciones de estado de la `SystemSuiteProfileCard` (`renderActions`):
 *   - Active       → botón «Mantenimiento» → `Maintenance`.
 *   - Maintenance  → botón «Activar»       → `Active`.
 *   - (Descontinuar → `Deprecated` no se ejercita: dejaría la suite sembrada fuera de servicio
 *      para el resto de la suite E2E; el ciclo Active↔Maintenance es idempotente y suficiente.)
 *
 * ── Por qué NO hay error-path spec para SystemSuite ────────────────────────────
 * `SetSystemSuiteStatus` es una transición del PROPIO agregado y la card sólo ofrece la
 * transición válida para el estado actual (switch sobre `systemSuite.status`). No hay guarda
 * dependiente de OTRO agregado forzable desde un control visible (a diferencia de Tenant·Suspender).
 * Por tanto: sólo happy-path. (Ver `reference/qa/ui-coverage-matrix.md`, sección C.)
 *
 * ── Certificación por botones de la card (no por badge de lista) ────────────────
 * La lista y la card comparten el componente `StatusBadge` (mismo `data-testid`), así que el
 * badge es ambiguo. En cambio los botones «Mantenimiento» / «Activar» son EXCLUSIVOS de la card
 * y sólo cambian cuando la mutación tiene éxito y `onSuccess` parchea el estado local
 * (`onSystemSuiteUpdate(id, { status })`). Ese flip de botón ES el reflejo del estado REAL
 * persistido (no hay flip optimista pre-commit — en la línea de [G-129]).
 *
 * Robustez ante re-ejecuciones: la suite es sembrada y compartida; el test NORMALIZA a `Active`
 * al inicio (si viene de una corrida previa interrumpida en `Maintenance`) y restaura a `Active`
 * al final, dejando el entorno como lo encontró.
 */
const ADMIN = { email: 'admin@evolith.example', password: 'Evolith.Dev.2026' };
/** Suite sembrada (AuthorizationDevDataSeeder) presente en la 1ª página (orden A-Z). */
const SUITE_NAME = 'Aduanas';

const maintenanceBtn = (page: Page) => page.getByRole('button', { name: /^mantenimiento$/i });
const activateBtn = (page: Page) => page.getByRole('button', { name: /^activar$/i });
const successToast = (page: Page) =>
  page.locator('[data-testid="toast"][data-toast-type="success"]').first();

/** Selecciona la suite por nombre en la lista y abre su card de detalle. */
async function selectSuite(page: Page, name: string): Promise<void> {
  const row = page.locator('[data-testid="entity-row"]').filter({ hasText: name }).first();
  await expect(row).toBeVisible({ timeout: 15000 });
  await row.click();
}

test.describe('Sistemas · ciclo de estado (happy path)', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
    await page.getByLabel(/correo electrónico/i).fill(ADMIN.email);
    await page.getByLabel(/contraseña/i).fill(ADMIN.password);
    await page.getByRole('button', { name: /ingresar/i }).click();
    await expect(page).toHaveURL(/\/tenants/);

    await page.goto('/system-suites');
    await expect(page).toHaveURL(/\/system-suites/);
    await selectSuite(page, SUITE_NAME);
    await expect(page.getByRole('button', { name: /^descontinuar$/i })).toBeVisible({
      timeout: 15000,
    });
  });

  test('HAPPY · Active → Mantenimiento → Active refleja el estado REAL', async ({ page }) => {
    // ── Normalización: garantizar punto de partida Active ──────────────────────
    if (await activateBtn(page).isVisible().catch(() => false)) {
      await activateBtn(page).click();
      await expect(successToast(page)).toBeVisible({ timeout: 15000 });
    }
    await expect(maintenanceBtn(page)).toBeVisible({ timeout: 15000 });
    await expect(activateBtn(page)).toHaveCount(0);

    // ── (1) Active → Maintenance ───────────────────────────────────────────────
    await maintenanceBtn(page).click();
    await expect(successToast(page)).toBeVisible({ timeout: 15000 });
    // El botón se invierte SOLO cuando el backend confirma (onSuccess parchea el estado local).
    await expect(activateBtn(page)).toBeVisible({ timeout: 15000 });
    await expect(maintenanceBtn(page)).toHaveCount(0);

    // ── (2) Maintenance → Active (restaura la suite a su estado sembrado) ───────
    await activateBtn(page).click();
    await expect(successToast(page)).toBeVisible({ timeout: 15000 });
    await expect(maintenanceBtn(page)).toBeVisible({ timeout: 15000 });
    await expect(activateBtn(page)).toHaveCount(0);
  });
});
