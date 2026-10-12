import { test, expect, type Page } from '@playwright/test';

/**
 * Rol — CRUD y toggle de estado · Matriz de Cobertura de UI (G-130), sección F.
 *
 * El CRUD/toggle de Rol vive DENTRO de la pestaña «Roles» del detalle de un
 * Suite del Sistema (src/presentation/authorization/system-suite):
 *   /system-suites → (auto-selección del primer suite) → pestaña «Roles»
 *     → ListToolbar «Nuevo rol» (InlineAddForm) para Crear
 *     → cada fila (SystemSuiteRolesPanel · RoleRow) trae 2 controles: Editar y
 *       Activar/Desactivar (useSetRoleActive).
 *
 * REGRESIÓN de G-129: cuando el backend RECHAZA una acción de estado, el control
 * NO debe cambiar (nada de flip optimista) y debe surgir el error real.
 *
 * Clasificación de rechazo (Role.cs · Deactivate):
 *   - RoleHasActiveChildRoles → desactivar un rol PADRE con un rol-hijo activo falla.
 *   (RoleAlreadyActive/RoleAlreadyInactive NO son alcanzables por UI: el botón sólo
 *    ofrece la transición contraria al estado vigente.)
 *
 * NOTA DE INSTRUMENTACIÓN (reportada como gap, no corregida aquí):
 *   RoleRow NO usa los data-testid compartidos: la fila es un <div> plano (sin
 *   `entity-row`) y el estado es un <span> de texto (sin `status-badge`). Además
 *   los IconButton de Editar/Toggle NO exponen nombre accesible (el tooltip no se
 *   enlaza vía aria-*). Por eso aquí se targetea:
 *     - la fila, por su texto único (valor con timestamp) sobre `div.group`;
 *     - los controles, por posición dentro de la fila (Editar = primer botón,
 *       Toggle = último botón);
 *     - el estado, por el texto «Activo»/«Inactivo» (i18n) dentro de la fila.
 *   El único testid compartido disponible en este flujo es `toast`.
 */
const ADMIN = { email: 'admin@evolith.example', password: 'Evolith.Dev.2026' };

/** Fila de rol scopeada por su valor único (RoleRow renderiza `div.group`). */
const roleRow = (page: Page, value: string) =>
  page.locator('[data-testid="entity-row"]').filter({ hasText: value }).first();

/** Abre «Nuevo rol», rellena código + nombre visible y (opcional) selecciona rol padre. */
async function createRole(
  page: Page,
  code: string,
  value: string,
  parentValue?: string,
): Promise<void> {
  await page.locator('button[title="Nuevo rol"]').click();
  await page.getByLabel(/código/i).fill(code);
  await page.getByLabel(/nombre visible/i).fill(value);
  if (parentValue) {
    await page.getByLabel(/rol padre/i).selectOption({ label: parentValue });
  }
  await page.getByRole('button', { name: /guardar rol/i }).click();
  await expect(
    page.locator('[data-testid="toast"][data-toast-type="success"]').first(),
  ).toBeVisible({ timeout: 15000 });
  await expect(roleRow(page, value)).toBeVisible({ timeout: 15000 });
}

test.describe('Rol · CRUD y toggle de estado (happy + error)', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
    await page.getByLabel(/correo electrónico/i).fill(ADMIN.email);
    await page.getByLabel(/contraseña/i).fill(ADMIN.password);
    await page.getByRole('button', { name: /ingresar/i }).click();
    await expect(page).toHaveURL(/\/tenants/);

    // La sesión persiste en localStorage (zustand persist) → el goto directo sobrevive.
    await page.goto('/system-suites');

    // El primer suite queda auto-seleccionado (useSystemSuiteDashboard) → aparecen las
    // pestañas del detalle. Abrimos «Roles».
    // Acotado a `main`: desde que el catálogo declara su propio menú «Roles», el nombre existe
    // dos veces en la pantalla —la opción de la barra y la pestaña del detalle— y sin acotar
    // Playwright falla por ambigüedad. La prueba mira el detalle del sistema, no la navegación.
    const rolesTab = page.getByRole('main').getByRole('button', { name: /^roles$/i });
    await expect(rolesTab).toBeVisible({ timeout: 20000 });
    await rolesTab.click();

    // El panel de Roles quedó montado (su ListToolbar expone «Nuevo rol»).
    await expect(page.locator('button[title="Nuevo rol"]')).toBeVisible({ timeout: 15000 });
  });

  test('HAPPY · crear un rol emite éxito y la fila nace Activa', async ({ page }) => {
    const stamp = Date.now();
    const value = `E2E Rol Crear ${stamp}`;
    await createRole(page, `E2EROLC${stamp}`, value);

    // La fila recién creada nace activa (Role se crea IsActive=true).
    await expect(roleRow(page, value).getByText(/^activo$/i)).toBeVisible();
  });

  test('HAPPY · actualizar el nombre visible de un rol emite éxito', async ({ page }) => {
    const stamp = Date.now();
    const value = `E2E Rol Editar ${stamp}`;
    const nuevoValor = `E2E Rol Editado ${stamp}`;
    await createRole(page, `E2EROLU${stamp}`, value);

    // Editar = primer botón de la fila (Pencil). Al entrar en edición, RoleRow
    // reemplaza la fila por un formulario con «Nombre visible» + «Guardar rol».
    await roleRow(page, value).getByRole('button', { name: /editar/i }).click();
    await page.getByLabel(/nombre visible/i).fill(nuevoValor);
    await page.getByRole('button', { name: /guardar rol/i }).click();

    await expect(
      page.locator('[data-testid="toast"][data-toast-type="success"]').first(),
    ).toBeVisible({ timeout: 15000 });
    await expect(roleRow(page, nuevoValor)).toBeVisible({ timeout: 15000 });
  });

  test('HAPPY · desactivar un rol hoja (sin hijos ni perfiles) refleja «Inactivo»', async ({
    page,
  }) => {
    const stamp = Date.now();
    const value = `E2E Rol Hoja ${stamp}`;
    await createRole(page, `E2EROLD${stamp}`, value);

    const fila = roleRow(page, value);
    await expect(fila.getByText(/^activo$/i)).toBeVisible();

    // Toggle = último botón de la fila (ShieldOff cuando está activo). Un rol recién
    // creado no tiene hijos ni perfiles → Deactivate procede (camino feliz).
    await fila.getByRole('button', { name: /desactivar/i }).click();

    await expect(
      page.locator('[data-testid="toast"][data-toast-type="success"]').first(),
    ).toBeVisible({ timeout: 15000 });
    // El estado pasa de verdad a Inactivo (no flip optimista: el patch refleja el real).
    await expect(roleRow(page, value).getByText(/^inactivo$/i)).toBeVisible({ timeout: 15000 });
  });

  test('ERROR · desactivar un rol padre con hijo activo falla y NO cambia de estado (G-129)', async ({
    page,
  }) => {
    const stamp = Date.now();
    const padre = `E2E Rol Padre ${stamp}`;
    const hijo = `E2E Rol Hijo ${stamp}`;

    // Construimos el estado de rechazo: padre activo + hijo activo colgando de él.
    await createRole(page, `E2EROLP${stamp}`, padre);
    await createRole(page, `E2EROLH${stamp}`, hijo, padre);

    const filaPadre = roleRow(page, padre);
    await expect(filaPadre.getByText(/^activo$/i)).toBeVisible();

    // Intentar desactivar el padre → backend rechaza (RoleHasActiveChildRoles).
    await filaPadre.getByRole('button', { name: /desactivar/i }).click();

    // (1) Sale un toast de ERROR (no un aviso de éxito prematuro).
    await expect(
      page.locator('[data-testid="toast"][data-toast-type="error"]').first(),
    ).toBeVisible({ timeout: 15000 });

    // (2) El control NO flipó: el padre sigue «Activo» y NUNCA muestra «Inactivo»
    //     (corazón de G-129: sin flip optimista).
    await expect(roleRow(page, padre).getByText(/^activo$/i)).toBeVisible();
    await expect(roleRow(page, padre).getByText(/^inactivo$/i)).toHaveCount(0);
  });
});
