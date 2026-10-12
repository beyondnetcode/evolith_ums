import { test, expect, type Page } from '@playwright/test';

/**
 * Feature Flag · ciclo de estado y criterios — Matriz de Cobertura de UI, sección J (G-130).
 *
 * Máquina de estados del agregado (Evolith.Ums.Domain/Configuration/FeatureFlag/FeatureFlag.cs):
 *   - Se crea SIEMPRE en `Inactive` (Draft) → el panel muestra «Activar» + «Archivar».
 *   - `Active`   → el panel muestra «Desactivar».
 *   - `Archived` → estado terminal, sin botones («Archivado — acción terminal»).
 *   - Los criterios sólo se editan en estado Draft (Inactive): formulario + botón de eliminar.
 *
 * REGRESIÓN G-129 (rechazo desde botón visible): cuando el backend RECHAZA una acción, el
 * control NO debe cambiar y debe surgir el toast de error real. El único rechazo forzable
 * desde un botón visible en esta pantalla es el criterio DUPLICADO (FeatureFlag.AddCriteria →
 * DomainErrors.Configuration.DuplicateCriteria): las transiciones de estado (Activar/Desactivar/
 * Archivar) sólo exponen su botón desde el estado en el que SIEMPRE proceden, así que no son
 * rechazables desde la UI (ver el reporte para el detalle del análisis del dominio).
 *
 * Selección robusta vía data-testid instrumentados en componentes compartidos (G-130):
 *   - `entity-row` (+ hasText del código) → seleccionar/asertar una fila de la lista
 *   - `status-badge` (+ data-status)      → asertar el estado real en la lista
 *   - `toast` (+ data-toast-type)         → asertar success/info/warning/error sin depender del texto
 *   - `list-search`                       → el buscador de la lista
 *
 * Nota de assertions de estado: el StatusBadge (status-badge testid) sólo existe en la LISTA,
 * no en el panel de detalle. En el detalle asertamos la transición REAL vía (a) el texto
 * «Estado: X» y (b) el cambio de botones tras el refetch del onSuccess (no hay flip optimista:
 * la mutación invalida la query del flag y el detalle se vuelve a pedir al servidor).
 */
const ADMIN = { email: 'admin@evolith.example', password: 'Evolith.Dev.2026' };

const activarBtn = (page: Page) => page.getByRole('button', { name: /^activar$/i });
const desactivarBtn = (page: Page) => page.getByRole('button', { name: /^desactivar$/i });
const archivarBtn = (page: Page) => page.getByRole('button', { name: /^archivar$/i });

const toast = (page: Page, type: 'success' | 'info' | 'warning' | 'error') =>
  page.locator(`[data-testid="toast"][data-toast-type="${type}"]`).first();

/**
 * Crea un feature flag Boolean (estado inicial Inactive) y lo deja AUTO-SELECCIONADO en el
 * panel de detalle. Devuelve el código único usado. Depende de que exista al menos una
 * System Suite sembrada para la organización (AuthorizationDevDataSeeder → BuildEvolithSystemSuites).
 */
async function createFlag(page: Page): Promise<string> {
  const code = `e2e-ff-${Date.now()}`;

  // Sin flag seleccionado, el único botón «Agregar» es el de la toolbar de la lista
  // (el panel de detalle muestra el EmptyDetailState, sin botón de agregar).
  await page.locator('button[title="Agregar"]').first().click();
  await expect(page.getByText(/nuevo feature flag/i)).toBeVisible();

  await page.getByPlaceholder('dark-mode, new-checkout').fill(code);
  // El formulario tiene dos <select>: [0] Tipo, [1] Suite del Sistema. index 1 del suite
  // salta el placeholder «— Seleccionar —» y toma la primera suite real.
  await page.locator('#feature-flag-form').getByRole('combobox').nth(1).selectOption({ index: 1 });
  await page.getByRole('button', { name: /^crear$/i }).click();

  // (1) Éxito confirmado por el servidor.
  await expect(toast(page, 'success')).toBeVisible({ timeout: 15000 });
  // (2) El drawer se cierra (su scrim no debe tapar el panel de detalle).
  await expect(page.getByText(/nuevo feature flag/i)).toBeHidden({ timeout: 10000 });
  // (3) El flag recién creado queda auto-seleccionado (dashboard.onSuccess → setSelectedId) →
  //     su panel aparece en estado Inactive con «Activar».
  await expect(activarBtn(page)).toBeVisible({ timeout: 15000 });

  return code;
}

test.describe('Feature Flag · estado y criterios (happy + error)', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
    await page.getByLabel(/correo electrónico/i).fill(ADMIN.email);
    await page.getByLabel(/contraseña/i).fill(ADMIN.password);
    await page.getByRole('button', { name: /ingresar/i }).click();
    await expect(page).toHaveURL(/\/tenants/);

    // Navegar a la pantalla de Feature Flags (riel → «Feature Flags»).
    await page.goto('/feature-flags');
    await expect(page.locator('button[title="Agregar"]').first()).toBeVisible({ timeout: 15000 });
  });

  test('HAPPY · crear un flag lo deja Inactive y visible en la lista (list-search + status-badge)', async ({
    page,
  }) => {
    const code = await createFlag(page);

    // El detalle refleja el estado real persistido (no un placeholder): Inactive + acciones Draft.
    await expect(page.getByText(/estado:\s*inactive/i)).toBeVisible();
    await expect(archivarBtn(page)).toBeVisible();

    // Verificación cruzada por la LISTA con los testids compartidos: buscar el código y asertar
    // que la fila muestra el badge de estado real «Inactive».
    await page.locator('[data-testid="list-search"]').fill(code);
    // La búsqueda de la lista es del lado servidor: se dispara con el botón «Buscar», no con Enter.
    await page.getByRole('button', { name: /^buscar$/i }).last().click();

    const row = page.locator('[data-testid="entity-row"]').filter({ hasText: code });
    await expect(row).toBeVisible({ timeout: 15000 });
    await expect(
      row.locator('[data-testid="status-badge"][data-status="Inactive"]').first()
    ).toBeVisible();
  });

  test('HAPPY · Activar y luego Desactivar refleja el estado real (Inactive→Active→Inactive)', async ({
    page,
  }) => {
    await createFlag(page);

    // Inactive → Active
    await activarBtn(page).click();
    await expect(toast(page, 'success')).toBeVisible({ timeout: 15000 });
    // El detalle se re-pide al servidor tras el onSuccess → estado REAL Active, botón «Desactivar».
    await expect(page.getByText(/estado:\s*active/i)).toBeVisible({ timeout: 15000 });
    await expect(desactivarBtn(page)).toBeVisible();
    await expect(activarBtn(page)).toHaveCount(0);

    // Active → Inactive
    await desactivarBtn(page).click();
    // Desactivar emite un toast de tipo `info` (useDeactivateFlag).
    await expect(toast(page, 'info')).toBeVisible({ timeout: 15000 });
    await expect(page.getByText(/estado:\s*inactive/i)).toBeVisible({ timeout: 15000 });
    await expect(activarBtn(page)).toBeVisible();
    await expect(desactivarBtn(page)).toHaveCount(0);
  });

  test('HAPPY · Archivar un flag Inactive lo lleva al estado terminal (flag peligroso)', async ({
    page,
  }) => {
    await createFlag(page);

    await archivarBtn(page).click();
    // Archivar emite un toast de tipo `warning` (useArchiveFlag).
    await expect(toast(page, 'warning')).toBeVisible({ timeout: 15000 });

    // Estado terminal: «Archived», leyenda de acción terminal y NINGÚN botón de transición.
    await expect(page.getByText(/estado:\s*archived/i)).toBeVisible({ timeout: 15000 });
    await expect(page.getByText(/archivado\s*—\s*acción terminal/i)).toBeVisible();
    await expect(activarBtn(page)).toHaveCount(0);
    await expect(desactivarBtn(page)).toHaveCount(0);
    await expect(archivarBtn(page)).toHaveCount(0);
  });

  test('HAPPY · añadir y quitar un criterio en un flag Draft', async ({ page }) => {
    await createFlag(page);

    // Estado inicial: sin criterios.
    await expect(page.getByText(/criterios de evaluación\s*\(0\)/i)).toBeVisible();

    // Añadir un criterio (tipo/operador por defecto: TenantId / Equals).
    const criteriaValue = `crit-${Date.now()}`;
    await page.getByLabel(/^valor$/i).fill(criteriaValue);
    // El único <button> del formulario de criterios es el submit «+».
    await page.locator('form').getByRole('button').click();

    await expect(toast(page, 'success')).toBeVisible({ timeout: 15000 });
    await expect(page.getByText(/criterios de evaluación\s*\(1\)/i)).toBeVisible({ timeout: 15000 });

    // Quitar el criterio recién agregado.
    // NOTA (gap de testid): el botón de eliminar criterio (IconButton) no expone accessible-name
    // ni data-testid; se localiza por su clase de color (rose-500). Ver reporte.
    await page.locator('button[class*="text-rose-500"]').first().click();
    await expect(toast(page, 'warning')).toBeVisible({ timeout: 15000 });
    await expect(page.getByText(/criterios de evaluación\s*\(0\)/i)).toBeVisible({ timeout: 15000 });
  });

  test('ERROR · añadir un criterio DUPLICADO falla y el conteo NO cambia (G-129)', async ({
    page,
  }) => {
    await createFlag(page);

    const criteriaValue = `dup-${Date.now()}`;

    // Primer alta: procede (queda 1 criterio).
    await page.getByLabel(/^valor$/i).fill(criteriaValue);
    await page.locator('form').getByRole('button').click();
    await expect(toast(page, 'success')).toBeVisible({ timeout: 15000 });
    await expect(page.getByText(/criterios de evaluación\s*\(1\)/i)).toBeVisible({ timeout: 15000 });

    // Segundo alta con MISMO tipo/operador/valor → el dominio rechaza (DuplicateCriteria).
    await page.getByLabel(/^valor$/i).fill(criteriaValue);
    await page.locator('form').getByRole('button').click();

    // (1) Sale un toast de ERROR (no un aviso de éxito prematuro).
    await expect(toast(page, 'error')).toBeVisible({ timeout: 15000 });
    // (2) El control NO cambió: sigue habiendo 1 criterio y NUNCA aparece «(2)» (corazón de G-129).
    await expect(page.getByText(/criterios de evaluación\s*\(1\)/i)).toBeVisible();
    await expect(page.getByText(/criterios de evaluación\s*\(2\)/i)).toHaveCount(0);
  });
});
