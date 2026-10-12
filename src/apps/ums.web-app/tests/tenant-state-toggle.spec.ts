import { test, expect } from '@playwright/test';

/**
 * Toggle de estado de Tenant — Ola 1 de la Matriz de Cobertura de UI (G-130).
 *
 * REGRESIÓN de G-129: cuando el backend RECHAZA una acción de estado, el control NO
 * debe cambiar (nada de flip optimista) y debe surgir el error real.
 *
 * Selección robusta vía data-testid instrumentados en componentes compartidos (G-130):
 *   - `entity-row` (+ hasText del código) → seleccionar una fila de la lista
 *   - `status-badge` (+ data-status) → asertar el estado real
 *   - `toast` (+ data-toast-type) → asertar success/error sin depender del texto
 *   - `list-search` → el buscador de la lista (desambigua el de onboarding)
 */
const ADMIN = { email: 'admin@evolith.example', password: 'Evolith.Dev.2026' };

test.describe('Tenant · toggle de estado (happy + error)', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
    await page.getByLabel(/correo electrónico/i).fill(ADMIN.email);
    await page.getByLabel(/contraseña/i).fill(ADMIN.password);
    await page.getByRole('button', { name: /ingresar/i }).click();
    await expect(page).toHaveURL(/\/tenants/);
  });

  test('ERROR · suspender un tenant con usuarios activos falla y el botón NO cambia (G-129)', async ({
    page,
  }) => {
    // EVOLITH (Mi Organización) tiene usuarios + sucursales activas → el backend rechaza.
    await page.getByRole('button', { name: /mi organización/i }).click();

    const suspender = page.getByRole('button', { name: /^suspender$/i });
    await expect(suspender).toBeVisible({ timeout: 15000 });
    await suspender.click();

    // (1) Sale un toast de ERROR (no un aviso de éxito prematuro).
    await expect(
      page.locator('[data-testid="toast"][data-toast-type="error"]').first(),
    ).toBeVisible({ timeout: 15000 });

    // (2) El botón NO flipó: sigue «Suspender» y NUNCA aparece «Activar» (el corazón de G-129).
    await expect(page.getByRole('button', { name: /^suspender$/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /^activar$/i })).toHaveCount(0);
  });

  test('HAPPY · suspender un tenant sin usuarios refleja el estado real (Suspended)', async ({
    page,
  }) => {
    // Un tenant recién creado = 0 usuarios → el suspend procede (camino feliz).
    const code = `E2ETG${Date.now()}`.slice(0, 20);

    await page.locator('button[title="Agregar"]').first().click();
    await expect(page.getByText(/crear nuevo tenant/i)).toBeVisible();
    await page.getByPlaceholder('TRANS_LIMA').fill(code);
    await page.getByPlaceholder('Transportes Lima S.A.C.').fill(`E2E Toggle ${code}`);
    await page.getByRole('button', { name: /registrar tenant/i }).click();
    await expect(
      page.locator('[data-testid="toast"][data-toast-type="success"]').first(),
    ).toBeVisible({ timeout: 15000 });

    // Esperar a que el drawer (y su scrim) se cierre para que no tape el panel de detalle.
    await expect(page.getByText(/crear nuevo tenant/i)).toBeHidden({ timeout: 10000 });

    // El tenant recién creado NO queda auto-seleccionado: handleCreateSuccess sólo resetea la
    // consulta (página 1 + limpiar filtro), y como la lista pagina 10/pág ordenada por nombre,
    // el nuevo tenant no está necesariamente en pantalla. Hay que LOCALIZARLO explícitamente en
    // la LISTA de Tenants (no en «Solicitudes de Tenant») y seleccionarlo.
    //
    // Hay dos ListToolbar en pantalla (Solicitudes de Tenant + lista de Tenants) → el buscador de
    // la lista es el `.last()`. La búsqueda es del lado servidor: se dispara con «Buscar», no Enter.
    // El nombre del tenant incluye su código, así que buscar por el código (criterio por defecto:
    // nombre) lo devuelve de forma unívoca.
    const search = page.locator('[data-testid="list-search"]').last();
    await expect(search).toBeVisible({ timeout: 15000 });
    await search.fill(code);
    await page.getByRole('button', { name: /^buscar$/i }).last().click();

    const row = page.locator('[data-testid="entity-row"]').filter({ hasText: code }).first();
    await expect(row).toBeVisible({ timeout: 15000 });
    await row.click();

    // Su panel de Control de Estado aparece con «Suspender».
    // El test es auto-corrector: sólo un tenant sin usuarios llega de verdad a «Suspended», así
    // que si por error se actuara sobre otro con usuarios, el assert de estado fallaría.
    const suspender = page.getByRole('button', { name: /^suspender$/i });
    await expect(suspender).toBeVisible({ timeout: 15000 });
    await suspender.click();

    // Éxito confirmado por el servidor → el estado pasa de verdad a Suspended (el patch onSuccess
    // sobre knownTenants refleja el estado REAL, no un flip optimista de G-129) y el botón del
    // panel cambia a «Activar».
    await expect(
      page.locator('[data-testid="status-badge"][data-status="Suspended"]').first(),
    ).toBeVisible({ timeout: 15000 });
    await expect(page.getByRole('button', { name: /^activar$/i })).toBeVisible();
  });
});
