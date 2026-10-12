import { test, expect, type Page, type Locator } from '@playwright/test';

/**
 * Módulos (Sistemas/Suites) · CRUD anidado + árbol de nodos
 *   Matriz de Cobertura de UI — reference/qa/ui-coverage-matrix.md, sección E
 *   (Autorización · Sistemas/Suites — `SystemSuiteDashboardScreen`).
 *
 * El CRUD de módulos vive DENTRO de la pestaña «Módulos» del detalle de un Suite
 * (src/presentation/authorization/system-suite/components/SystemSuiteDetailPanel.tsx):
 *   /system-suites → seleccionar suite «Aduanas» (sembrada, 1ª página)
 *     → pestaña «Módulos»
 *     → ListToolbar «Nuevo Módulo Estructural» (AddButton) abre un InlineAddForm
 *       (Código/Nombre/Descripción/Orden · submit «Guardar Módulo»).
 *     → cada módulo lo renderiza `SuiteNodeTree`: cabecera con nombre + CodeBadge +
 *       StatusBadge, y en hover los IconButton Activar/Desactivar/Eliminar (aria-label).
 *     → el árbol de nodos (menú/submenú/opción) se maneja con `SuiteNode` (ADR-0090):
 *       «Agregar nodo raíz» (CTA visible cuando el módulo está Activo y sin nodos) y
 *       «Agregar nodo hijo» (IconButton por fila).
 *
 * HECHOS DE PRODUCCIÓN VERIFICADOS (leídos del .tsx/hook, no adivinados):
 *   - Un módulo NACE `Inactive` (SuiteNodeTree l.82-84): el backend rechaza `AddNode`
 *     sobre un módulo inactivo (G-107). Por eso el árbol exige activar primero.
 *   - Toasts tipados (use-notified-mutation.ts): crear/activar módulo → `success`;
 *     DESACTIVAR módulo → `info`; ELIMINAR módulo → `warning`; agregar nodo → `success`.
 *   - Anti-[G-129]: el StatusBadge del módulo refleja el estado REAL (el patch llega
 *     tras la confirmación del backend vía invalidación de queries), no un flip optimista.
 *
 * SIN ERROR-PATH FORZABLE (verificado en el backend, no asumido):
 *   La matriz (sección C, fila «SystemSuite · Activar/Desactivar Módulo → ✅
 *   ModuleHasActiveMenus») está desactualizada. En el dominio:
 *     - `SystemSuite.DeactivateModule` (SystemSuite.cs:186) NO tiene guarda por menús
 *       activos; `Module.Deactivate` (Module.cs:77) solo rechaza `ModuleAlreadyInactive`,
 *       transición que la UI nunca ofrece (solo muestra la transición válida) → siempre éxito.
 *     - La guarda `ModuleHasActiveMenus` vive en `SystemSuite.RemoveModule` (SystemSuite.cs:112,
 *       parámetro `activeMenuCount`), PERO `RemoveModuleCommandHandler` (l.26) invoca
 *       `RemoveModule` SIN pasar `activeMenuCount` → usa el default 0 → la guarda es
 *       INALCANZABLE desde la API (guarda muerta, reportado como bug al ingeniero QA).
 *   Por tanto: solo happy-path para el ciclo de estado del módulo (como en
 *   `system-suite-state.spec.ts` para el estado de la suite).
 *
 * IDEMPOTENCIA: cada módulo se crea con un código/nombre únicos por timestamp y se
 * ELIMINA al final del test (deja la suite sembrada «Aduanas» como la encontró).
 */
const ADMIN = { email: 'admin@evolith.example', password: 'Evolith.Dev.2026' };
/** Suite sembrada (AuthorizationDevDataSeeder) presente en la 1ª página (orden A-Z). */
const SUITE_NAME = 'Aduanas';

const successToast = (page: Page) =>
  page.locator('[data-testid="toast"][data-toast-type="success"]').first();
const infoToast = (page: Page) =>
  page.locator('[data-testid="toast"][data-toast-type="info"]').first();
const warningToast = (page: Page) =>
  page.locator('[data-testid="toast"][data-toast-type="warning"]').first();
const errorToast = (page: Page) =>
  page.locator('[data-testid="toast"][data-toast-type="error"]').first();

/**
 * Bloque de un módulo (raíz de `SuiteNodeTree`: única con `overflow-hidden` + `rounded-lg`),
 * scopeado por su nombre único. Un módulo recién creado no tiene nodos → contiene UN solo
 * StatusBadge (el de su cabecera).
 */
const moduleBlock = (page: Page, name: string): Locator =>
  page.locator('div.overflow-hidden.rounded-lg').filter({ hasText: name }).first();

/**
 * Cabecera del módulo (`div.select-none`, único por bloque): aloja el StatusBadge y los
 * IconButton Activar/Desactivar/Eliminar del MÓDULO. Necesario porque las filas de nodo
 * exponen los mismos aria-label (Desactivar/Eliminar) y colisionarían con el bloque entero.
 */
const moduleHeader = (page: Page, name: string): Locator =>
  moduleBlock(page, name).locator('div.select-none').first();

/** StatusBadge de la cabecera del módulo. */
const moduleStatus = (page: Page, name: string): Locator =>
  moduleHeader(page, name).locator('[data-testid="status-badge"]').first();

/** Fila de un nodo del árbol, scopeada por su etiqueta única. */
const nodeRow = (page: Page, label: string): Locator =>
  page.locator('div.group\\/node').filter({ hasText: label }).first();

/** Abre «Nuevo Módulo Estructural», completa código+nombre y confirma el alta. */
async function createModule(page: Page, code: string, name: string): Promise<void> {
  await page.locator('button[title="Nuevo Módulo Estructural"]').click();
  await page.getByLabel(/código del módulo/i).fill(code);
  await page.getByLabel(/nombre del módulo/i).fill(name);
  await page.getByRole('button', { name: 'Guardar Módulo' }).click();
  await expect(successToast(page)).toBeVisible({ timeout: 15000 });
  await expect(moduleBlock(page, name)).toBeVisible({ timeout: 15000 });
}

/** Activa un módulo recién creado (nace Inactive) y aseveramos «Active» real. */
async function activateModule(page: Page, name: string): Promise<void> {
  await moduleHeader(page, name).getByRole('button', { name: 'Activar', exact: true }).click();
  await expect(successToast(page)).toBeVisible({ timeout: 15000 });
  await expect(moduleStatus(page, name)).toHaveAttribute('data-status', 'Active', {
    timeout: 15000,
  });
}

/** Limpieza idempotente: elimina el módulo (arrastra su subárbol) → toast `warning`. */
async function removeModule(page: Page, name: string): Promise<void> {
  const block = moduleBlock(page, name);
  if (!(await block.count())) return;
  await moduleHeader(page, name).getByRole('button', { name: 'Eliminar', exact: true }).click();
  await expect(warningToast(page)).toBeVisible({ timeout: 15000 });
  await expect(moduleBlock(page, name)).toHaveCount(0, { timeout: 15000 });
}

test.describe('Módulos · CRUD anidado + árbol de nodos (happy)', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
    await page.getByLabel(/correo electrónico/i).fill(ADMIN.email);
    await page.getByLabel(/contraseña/i).fill(ADMIN.password);
    await page.getByRole('button', { name: /ingresar/i }).click();
    await expect(page).toHaveURL(/\/tenants/);

    await page.goto('/system-suites');
    await expect(page).toHaveURL(/\/system-suites/);

    // Seleccionar la suite sembrada y abrir su detalle.
    const row = page.locator('[data-testid="entity-row"]').filter({ hasText: SUITE_NAME }).first();
    await expect(row).toBeVisible({ timeout: 20000 });
    await row.click();

    // Abrir la pestaña «Módulos»; su ListToolbar expone el AddButton.
    const modulesTab = page.getByRole('button', { name: /^módulos$/i });
    await expect(modulesTab).toBeVisible({ timeout: 15000 });
    await modulesTab.click();
    await expect(page.locator('button[title="Nuevo Módulo Estructural"]')).toBeVisible({
      timeout: 15000,
    });
  });

  test('HAPPY · crear un módulo emite éxito y nace «Inactive» (G-107)', async ({ page }) => {
    const stamp = Date.now();
    const name = `E2E Modulo Crear ${stamp}`;
    const code = `E2EMODC${stamp}`;

    await createModule(page, code, name);

    // Un módulo nace Inactive (el backend rechaza AddNode sobre módulo inactivo).
    await expect(moduleStatus(page, name)).toHaveAttribute('data-status', 'Inactive');
    // Al estar inactivo, la cabecera ofrece «Activar» (no «Desactivar»).
    await expect(
      moduleHeader(page, name).getByRole('button', { name: 'Activar', exact: true }),
    ).toBeVisible();

    await removeModule(page, name);
  });

  test('HAPPY · activar y luego desactivar un módulo hoja refleja el estado REAL', async ({
    page,
  }) => {
    const stamp = Date.now();
    const name = `E2E Modulo Toggle ${stamp}`;
    const code = `E2EMODT${stamp}`;

    await createModule(page, code, name);
    await expect(moduleStatus(page, name)).toHaveAttribute('data-status', 'Inactive');

    // Inactive → Active (toast success).
    await activateModule(page, name);

    // Active → Inactive (un módulo hoja SIN menús activos se desactiva; toast `info`).
    await moduleHeader(page, name).getByRole('button', { name: 'Desactivar', exact: true }).click();
    await expect(infoToast(page)).toBeVisible({ timeout: 15000 });
    // El badge pasa de verdad a Inactive (no flip optimista: refleja el estado persistido).
    await expect(moduleStatus(page, name)).toHaveAttribute('data-status', 'Inactive', {
      timeout: 15000,
    });

    await removeModule(page, name);
  });

  test('HAPPY · árbol: agregar un nodo raíz (menú) y un nodo hijo (opción)', async ({ page }) => {
    const stamp = Date.now();
    const name = `E2E Modulo Arbol ${stamp}`;
    const code = `E2EMODA${stamp}`;
    const menuLabel = `E2E Menu ${stamp}`;
    const optLabel = `E2E Opcion ${stamp}`;

    await createModule(page, code, name);
    // El alta de nodos exige el módulo Activo (G-107).
    await activateModule(page, name);

    const block = moduleBlock(page, name);

    // ── Nodo raíz (Menú): CTA visible cuando el módulo está Activo y no tiene nodos ──
    // (Se targetea por texto visible: el IconButton de cabecera tiene el mismo aria-label
    //  pero sin texto, así que `hasText` desambigua hacia el CTA de ancho completo.)
    await block.locator('button', { hasText: 'Agregar nodo raíz' }).click();
    await page.getByLabel(/tipo de nodo/i).selectOption('Menu');
    await page.getByLabel(/^código/i).fill(`MENU${stamp}`);
    await page.getByLabel(/etiqueta/i).fill(menuLabel);
    await page.getByRole('button', { name: 'Guardar Nodo' }).click();
    await expect(successToast(page)).toBeVisible({ timeout: 15000 });
    await expect(nodeRow(page, menuLabel)).toBeVisible({ timeout: 15000 });

    // ── Nodo hijo (Opción) colgando del menú recién creado ──
    await nodeRow(page, menuLabel)
      .getByRole('button', { name: 'Agregar nodo hijo', exact: true })
      .click();
    await page.getByLabel(/tipo de nodo/i).selectOption('Option');
    await page.getByLabel(/^código/i).fill(`OPT${stamp}`);
    await page.getByLabel(/etiqueta/i).fill(optLabel);
    await page.getByRole('button', { name: 'Guardar Nodo' }).click();
    await expect(successToast(page)).toBeVisible({ timeout: 15000 });
    await expect(nodeRow(page, optLabel)).toBeVisible({ timeout: 15000 });

    // ── ERROR-PATH (G-154): con un nodo-menú ACTIVO, eliminar el módulo es RECHAZADO ──
    // La invariante ModuleHasActiveMenus (antes muerta: el handler no poblaba activeMenuCount)
    // ahora la evalúa el agregado contando sus nodos raíz activos. Familia G-129: el backend
    // rechaza y el frontend NO debe falsear éxito → el módulo SIGUE presente.
    await moduleHeader(page, name).getByRole('button', { name: 'Eliminar', exact: true }).click();
    await expect(errorToast(page)).toBeVisible({ timeout: 15000 });
    await expect(moduleBlock(page, name)).toBeVisible(); // no se eliminó

    // Limpieza: desactivar el nodo-menú raíz (baja el conteo de menús activos) y ya sí eliminar.
    await nodeRow(page, menuLabel).getByRole('button', { name: 'Desactivar', exact: true }).click();
    await expect(successToast(page).or(infoToast(page)).first()).toBeVisible({ timeout: 15000 });
    await removeModule(page, name);
  });
});
