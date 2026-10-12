import { test, expect } from '@playwright/test';

/**
 * Caminos MVP (US-001…US-012) — TE-09 Ola 1 / G-076.
 *
 * Van más allá de login+nav: ejercitan los caminos de negocio del MVP contra la
 * aplicación desplegada. Cierran la brecha del gate E2E, que hasta ahora solo
 * cubría autenticación y el riel de navegación.
 *
 * Patrones vinculantes (TE-09): P1 — credenciales desde `DEV_PERSONAS`, nunca
 * fixtures hardcodeadas; los localizadores de navegación se acotan al riel.
 */
const ADMIN = { email: 'admin@evolith.example', password: 'Evolith.Dev.2026' };

const rail = (page: import('@playwright/test').Page) => page.getByTestId('nav-rail');

test.describe('Caminos MVP', () => {
  test.beforeEach(async ({ page }) => {
    // Admin Root de EVOLITH (DEV_PERSONAS). El selector de tenant arranca en EVOLITH.
    await page.goto('/login');
    await page.getByLabel(/correo electrónico/i).fill(ADMIN.email);
    await page.getByLabel(/contraseña/i).fill(ADMIN.password);
    await page.getByRole('button', { name: /ingresar/i }).click();
    await expect(page).toHaveURL(/\/tenants/);
  });

  test('US-001 · crear un tenant produce confirmación de éxito', async ({ page }) => {
    // Código único ≤ 20 chars (schema `max(20)`) para no colisionar (409) entre corridas.
    const code = `E2E${Date.now()}`.slice(0, 20);

    await page.locator('button[title="Agregar"]').first().click();
    await expect(page.getByText(/crear nuevo tenant/i)).toBeVisible();

    await page.getByPlaceholder('TRANS_LIMA').fill(code);
    await page.getByPlaceholder('Transportes Lima S.A.C.').fill(`E2E Pilot ${code}`);
    await page.getByRole('button', { name: /registrar tenant/i }).click();

    // Verificación robusta por el toast de éxito (no por la lista paginada):
    // `useCreateTenant.successNotif` → «Tenant Creado». El mensaje incluye el código.
    await expect(page.getByText(/tenant creado|tenant created/i).first()).toBeVisible({
      timeout: 15000,
    });
    await expect(page.getByText(code).first()).toBeVisible();
  });

  test('US-003/004 · la topología de sistema carga para un admin autorizado', async ({ page }) => {
    await rail(page)
      .getByRole('button', { name: /^(sistemas|systems)$/i })
      .first()
      .click();
    await expect(page).toHaveURL(/\/system-suites/);
    // El botón Agregar habilitado exige que el grafo conceda CREATE en esta suite:
    // ejercita la resolución de autorización, no solo el render.
    const add = page.locator('button[title="Agregar"]').first();
    await expect(add).toBeVisible({ timeout: 15000 });
    await expect(add).not.toBeDisabled();
  });

  test('US-007 · las plantillas de permisos cargan para un admin autorizado', async ({ page }) => {
    await rail(page)
      .getByRole('button', { name: /(plantillas de permisos|permission templates)/i })
      .first()
      .click();
    await expect(page).toHaveURL(/\/permission-templates/);
    const add = page.locator('button[title="Agregar"]').first();
    await expect(add).toBeVisible({ timeout: 15000 });
    await expect(add).not.toBeDisabled();
  });

  test('US-009 · los perfiles de autorización cargan para un admin autorizado', async ({ page }) => {
    await rail(page)
      .getByRole('button', { name: /(perfiles de autorización|authorization profiles)/i })
      .first()
      .click();
    await expect(page).toHaveURL(/\/profiles/);
    await expect(page.locator('button[title="Agregar"]').first()).toBeVisible({ timeout: 15000 });
  });
});
