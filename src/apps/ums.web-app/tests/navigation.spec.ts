import { test, expect } from '@playwright/test';

test.describe('Navigation', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
    await page.getByLabel(/correo electrónico/i).fill('admin@evolith.example');
    await page.getByLabel(/contraseña/i).fill('Evolith.Dev.2026');
    await page.getByRole('button', { name: /ingresar/i }).click();
    await expect(page).toHaveURL(/\/tenants/);
  });

  /**
   * Los localizadores se acotan al riel de navegación (`nav-rail`): fuera de él
   * las páginas tienen botones cuyo nombre accesible también contiene "tenant"
   * (p. ej. el filtro "25 tenants · Ocultar filtros"), lo que rompe el modo
   * estricto de Playwright.
   */
  const rail = (page: import('@playwright/test').Page) => page.getByTestId('nav-rail');

  test('should display navigation rail with all sections', async ({ page }) => {
    await expect(
      rail(page)
        .getByText(/(identidad|identity)/i)
        .first()
    ).toBeVisible();
    await expect(
      rail(page)
        .getByText(/(autorización|authorization)/i)
        .first()
    ).toBeVisible();
    await expect(
      rail(page)
        .getByText(/(configuración|configuration)/i)
        .first()
    ).toBeVisible();
  });

  test('should navigate to Tenants page', async ({ page }) => {
    await rail(page)
      .getByRole('button', { name: /tenant/i })
      .first()
      .click();
    await expect(page).toHaveURL(/\/tenants/);
  });

  test('should navigate to Users page', async ({ page }) => {
    await rail(page)
      .getByRole('button', { name: /(user accounts|cuentas de usuario)/i })
      .first()
      .click();
    await expect(page).toHaveURL(/\/users/);
  });

  test('should navigate to System Suites page', async ({ page }) => {
    // El riel usa la etiqueta corta `systemSuitesNav` ("Sistemas" / "Systems"),
    // no `systemSuites` ("Suites del Sistema").
    await rail(page)
      .getByRole('button', { name: /^(sistemas|systems)$/i })
      .first()
      .click();
    await expect(page).toHaveURL(/\/system-suites/);
  });

  test('should navigate to Permission Templates page', async ({ page }) => {
    await rail(page)
      .getByRole('button', { name: /(permission templates|plantillas de permisos)/i })
      .first()
      .click();
    await expect(page).toHaveURL(/\/permission-templates/);
  });

  test('should navigate to Profiles page', async ({ page }) => {
    await rail(page)
      .getByRole('button', { name: /(authorization profiles|perfiles de autorización)/i })
      .first()
      .click();
    await expect(page).toHaveURL(/\/profiles/);
  });

  test('should navigate to Feature Flags page', async ({ page }) => {
    await rail(page)
      .getByRole('button', { name: /(feature flags|flags|banderas)/i })
      .first()
      .click();
    await expect(page).toHaveURL(/\/feature-flags/);
  });

  test('should navigate to Profile page', async ({ page }) => {
    await rail(page)
      .getByRole('button', { name: /(profile stats|estadísticas de perfil)/i })
      .first()
      .click();
    await expect(page).toHaveURL(/\/profile/);
  });

  test('should collapse and expand navigation sections', async ({ page }) => {
    const identitySection = rail(page)
      .getByRole('button', { name: /(identidad|identity)/i })
      .first();
    const tenantItem = rail(page)
      .getByRole('button', { name: /tenant/i })
      .first();

    await identitySection.click();
    await expect(tenantItem).not.toBeVisible();
    await identitySection.click();
    await expect(tenantItem).toBeVisible();
  });

  /**
   * Lo que se afirma es que la hamburguesa ALTERNA, no en qué estado arranca el riel.
   *
   * La version anterior daba por hecho que arranca desplegado —clic → `w-20`, clic → `w-64`— y eso
   * no es una propiedad del componente: el estado inicial sale de `ui.layout`, un ajuste del
   * SISTEMA que viaja en el grafo (`MainLayout.tsx`, `useState(ui.layout === 'nav-rail-compact')`).
   * Contra un backend compartido, cualquier prueba que toque los ajustes cambia ese arranque y el
   * primer clic despliega en vez de colapsar.
   *
   * Sintoma medido el 2026-08-03: 15/15 en aislamiento y un fallo dentro de la suite completa, la
   * clase de rojo que parece azar y es dependencia de orden. Leer el estado y comprobar que cambia
   * —y que vuelve— verifica lo mismo sin presuponer nada.
   */
  test('should toggle navigation rail collapsed state', async ({ page }) => {
    const menuButton = page.locator('button[aria-label="Toggle navigation"]');
    const riel = page.getByTestId('nav-rail');

    const anchoDe = async () => ((await riel.getAttribute('class')) ?? '').includes('w-20') ? 'w-20' : 'w-64';

    const inicial = await anchoDe();
    const contrario = inicial === 'w-20' ? 'w-64' : 'w-20';

    await menuButton.click();
    await expect(riel).toHaveClass(new RegExp(contrario));

    await menuButton.click();
    await expect(riel).toHaveClass(new RegExp(inicial));
  });

  test('should highlight active navigation item', async ({ page }) => {
    const tenantButton = rail(page)
      .getByRole('button', { name: /tenant/i })
      .first();
    await tenantButton.click();
    await expect(tenantButton).toHaveClass(/bg-m3-primary-container/);
  });
});
