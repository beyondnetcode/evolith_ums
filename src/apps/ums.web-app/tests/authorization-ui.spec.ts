import { test, expect } from '@playwright/test';

test.describe('Dynamic Authorization UI Tests', () => {
  test('Admin user should have Agregar button enabled', async ({ page }) => {
    await page.goto('/login');
    // Admin Root de EVOLITH (DEV_PERSONAS); el campo de inquilino ya arranca en EVOLITH.
    await page.getByLabel(/correo electrónico/i).fill('admin@evolith.example');
    await page.getByLabel(/contraseña/i).fill('Evolith.Dev.2026');
    await page.click('button[type="submit"]');

    // Wait for tenants
    await page.waitForURL('**/tenants', { timeout: 10000 });

    // La barra de listado renderiza más de un botón "Agregar" (toolbar + vacío):
    // basta con verificar el primero.
    const addButton = page.locator('button[title="Agregar"]').first();
    await expect(addButton).toBeVisible();
    await expect(addButton).not.toBeDisabled();
  });

  test('Client user should see Access Denied on Tenants page', async ({ page }) => {
    await page.goto('/login');

    // G-263: el inquilino se TECLEA. Ya no es un desplegable cerrado con una lista compilada
    // en el bundle, así que la prueba escribe el código igual que lo haría el usuario.
    await page.getByRole('combobox', { name: /inquilino/i }).fill('COMEX_ANDINA');

    // Persona de inquilino CLIENT: no gobierna inquilinos.
    await page.getByLabel(/correo electrónico/i).fill('usuario.impo@comexandina.com.pe');
    await page.getByLabel(/contraseña/i).fill('Evolith.Dev.2026');
    await page.click('button[type="submit"]');

    // Wait for tenants
    await page.waitForURL('**/tenants', { timeout: 10000 });

    // Should show access denied
    await expect(page.getByText(/Acceso Denegado/i)).toBeVisible();
  });
});
