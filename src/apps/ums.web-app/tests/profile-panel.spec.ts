import { test, expect } from '@playwright/test';

test.describe('Profile Panel (Connected User Drawer)', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
    await page.getByLabel(/correo electrónico/i).fill('admin@evolith.example');
    await page.getByLabel(/contraseña/i).fill('Evolith.Dev.2026');
    await page.getByRole('button', { name: /ingresar/i }).click();
    await expect(page).toHaveURL(/\/tenants/);
  });

  test('should display user initials in profile avatar', async ({ page }) => {
    const avatar = page.locator('button[aria-label="View connected user details"]');
    await expect(avatar).toBeVisible();
    const text = await avatar.textContent();
    expect(text?.length).toBe(2);
    expect(text?.toUpperCase()).toBe(text);
  });

  test('should show tooltip on avatar hover', async ({ page }) => {
    const avatar = page.locator('button[aria-label="View connected user details"]');
    await avatar.hover();
    await expect(page.getByText(/admin/i).first()).toBeVisible();
    await expect(page.getByText(/tenant:/i)).toBeVisible();
  });

  test('should open drawer when clicking avatar', async ({ page }) => {
    const avatar = page.locator('button[aria-label="View connected user details"]');
    await avatar.click();
    await expect(
      page.getByRole('heading', { name: /(connected user|estadísticas de perfil|profile stats)/i })
    ).toBeVisible();
  });

  test('should display user section with correct info', async ({ page }) => {
    const avatar = page.locator('button[aria-label="View connected user details"]');
    await avatar.click();
    await expect(page.getByText(/username/i)).toBeVisible();
    await expect(page.getByText(/email/i)).toBeVisible();
    await expect(page.getByText(/admin/i).first()).toBeVisible();
  });

  test('should display tenant section', async ({ page }) => {
    const avatar = page.locator('button[aria-label="View connected user details"]');
    await avatar.click();
    await expect(page.getByText(/tenant name/i)).toBeVisible();
    await expect(page.getByText(/tenant code/i)).toBeVisible();
    await expect(page.getByText(/tenant id/i)).toBeVisible();
  });

  test('should display profile section', async ({ page }) => {
    const avatar = page.locator('button[aria-label="View connected user details"]');
    await avatar.click();
    // Acotado fuera de la barra: `role` casa también con la opción «Roles» del menú desde que el
    // catálogo la declara, y esta prueba habla del cajón del usuario conectado, no de la
    // navegación. Sin acotar, Playwright falla por ambigüedad y no por defecto.
    // Exacto, no `/role/i`: desde que el catálogo declara su menú, «Roles» existe en la barra y
    // el patrón laxo casaba con los dos. El cajón rotula «Role» en singular, así que anclar el
    // patrón basta y deja la prueba mirando lo que le importa.
    await expect(page.getByText(/^role$/i).first()).toBeVisible();
    await expect(page.getByText(/permissions/i)).toBeVisible();
  });

  test('should display session section', async ({ page }) => {
    const avatar = page.locator('button[aria-label="View connected user details"]');
    await avatar.click();
    await expect(page.getByText(/session tracking id/i)).toBeVisible();
    await expect(page.getByText(/language/i)).toBeVisible();
    await expect(page.getByText(/environment/i)).toBeVisible();
  });

  test('should display access summary section', async ({ page }) => {
    const avatar = page.locator('button[aria-label="View connected user details"]');
    await avatar.click();
    await page.getByRole('button', { name: /access summary/i }).click();
    await expect(page.getByText(/modules/i)).toBeVisible();
    await expect(page.getByText(/resources/i)).toBeVisible();
    await expect(page.getByText(/actions/i)).toBeVisible();
    await expect(page.getByText(/flags/i).first()).toBeVisible();
  });

  test('should expand technical details section', async ({ page }) => {
    const avatar = page.locator('button[aria-label="View connected user details"]');
    await avatar.click();
    await page.getByRole('button', { name: /technical details/i }).click();
    await expect(page.getByText(/"userId":/i)).toBeVisible();
  });

  test('should have copy buttons for IDs', async ({ page }) => {
    const avatar = page.locator('button[aria-label="View connected user details"]');
    await avatar.click();
    const copyButtons = page.locator('button[title^="Copy"]');
    await expect(copyButtons.first()).toBeVisible();
  });

  test('should close drawer when clicking close button', async ({ page }) => {
    const avatar = page.locator('button[aria-label="View connected user details"]');
    await avatar.click();
    await page.locator('[aria-label="Close drawer"]').click();
    await expect(
      page.getByRole('heading', { name: /(connected user|estadísticas de perfil|profile stats)/i })
    ).not.toBeVisible();
  });

  test('should logout from drawer', async ({ page }) => {
    const avatar = page.locator('button[aria-label="View connected user details"]');
    await avatar.click();
    await page
      .getByRole('button', { name: /(logout|cerrar sesión)/i })
      .last()
      .click();
    await expect(page).toHaveURL(/\/login/);
  });
});
