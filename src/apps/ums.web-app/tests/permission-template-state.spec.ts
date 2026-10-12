import { test, expect, type Page } from '@playwright/test';

/**
 * Plantillas de Permisos (PermissionTemplate) · ciclo de vida — Matriz de Cobertura de UI, sección C
 * (ver `reference/qa/ui-coverage-matrix.md`).
 *
 * Tras corregir [G-140] (el alta versiona: crear una plantilla para una terna ya plantillada genera
 * la versión SIGUIENTE en vez de colisionar 500), el happy-path de creación es alcanzable de forma
 * determinista. Este spec certifica:
 *
 *   1. HAPPY · «crear plantilla nace en Draft y ofrece Publicar/Eliminar» + limpieza por Eliminar.
 *      Crea una nueva versión para una suite+rol sembrados; el dashboard auto-selecciona la Draft
 *      (`setSelectedId`), y se limpia con «Eliminar» → «Sí, eliminar» (no deja residuo).
 *
 *   2. ERROR · «publicar una Draft vacía es rechazado (TemplateItemsRequired) sin falso éxito»
 *      (familia [G-129]). El dominio (`PermissionTemplate.Publish`) exige ≥1 ítem; una Draft recién
 *      creada está vacía → Publish falla. El frontend NO debe simular éxito: sigue en Draft y no
 *      aparece «Descontinuar». Limpieza por Eliminar.
 *
 *   3. ESTADO · «una plantilla Published expone Descontinuar y oculta Publicar/Eliminar»: certifica
 *      que los controles del panel de detalle reflejan el estado REAL (gating por `status`), sin
 *      flip optimista. Read-only sobre datos sembrados.
 *
 * ── Fidelidad de estado por botones (no por badge) ─────────────────────────────
 * El badge (`StatusBadge`, `data-status` crudo) se comparte entre la fila de lista y el detalle, así
 * que es ambiguo. Los botones «Publicar»/«Eliminar»/«Descontinuar» son EXCLUSIVOS del panel de
 * detalle y su presencia depende del estado real del agregado (`isDraft`/`isPublished`).
 */
const ADMIN = { email: 'admin@evolith.example', password: 'Evolith.Dev.2026' };
/** Suite sembrada (AuthorizationDevDataSeeder). */
const SUITE_NAME = 'Aduanas';
/** Rol sembrado de esa suite; el alta genera una nueva versión (G-140), no colisiona. */
const SEEDED_ROLE = 'Agente de Aduanas';

const successToast = (page: Page) =>
  page.locator('[data-testid="toast"][data-toast-type="success"]').first();
const errorToast = (page: Page) =>
  page.locator('[data-testid="toast"][data-toast-type="error"]').first();

const draftBadge = (page: Page) => page.locator('[data-testid="status-badge"][data-status="Draft"]');
const publishedBadge = (page: Page) =>
  page.locator('[data-testid="status-badge"][data-status="Published"]');

const publishBtn = (page: Page) => page.getByRole('button', { name: /^publicar$/i });
const deprecateBtn = (page: Page) => page.getByRole('button', { name: /^descontinuar$/i });
const deleteBtn = (page: Page) => page.getByRole('button', { name: /^eliminar$/i });

/** Abre el formulario de alta y crea una plantilla (suite+rol sembrados). Espera la Draft. */
async function createTemplate(page: Page): Promise<void> {
  await page.locator('button[title="Agregar"]').first().click();
  const dialog = page.getByRole('dialog');
  await expect(dialog).toBeVisible({ timeout: 15000 });
  const selects = dialog.locator('select');
  await selects.nth(0).selectOption({ label: SUITE_NAME });
  // El select de rol se llena tras elegir suite (deja de tener solo el placeholder).
  await expect(selects.nth(1).locator('option')).not.toHaveCount(1, { timeout: 15000 });
  await selects.nth(1).selectOption({ label: SEEDED_ROLE });
  await dialog.getByRole('button', { name: /crear plantilla/i }).click();
  // Éxito: el diálogo se cierra y el dashboard auto-selecciona la nueva Draft en el detalle.
  await expect(dialog).toBeHidden({ timeout: 15000 });
  await expect(draftBadge(page).first()).toBeVisible({ timeout: 15000 });
}

/** Elimina la plantilla actualmente seleccionada (Draft) y verifica que el detalle se limpia. */
async function deleteSelectedDraft(page: Page): Promise<void> {
  await deleteBtn(page).click();
  await page.getByRole('button', { name: /^sí,?\s*eliminar$/i }).click();
  // Al borrar, el dashboard limpia la selección → desaparecen los botones EXCLUSIVOS del panel de
  // detalle (Publicar/Eliminar). Se asevera por botón, no por badge: el badge es compartido entre la
  // fila de lista y el detalle (ver nota de cabecera), y otras Draft residuales del clúster compartido
  // lo dejarían con conteo > 0 aunque la selección sí se haya limpiado.
  await expect(deleteBtn(page)).toHaveCount(0, { timeout: 15000 });
  await expect(publishBtn(page)).toHaveCount(0);
}

test.describe('Plantillas de Permisos · ciclo de vida', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
    await page.getByLabel(/correo electrónico/i).fill(ADMIN.email);
    await page.getByLabel(/contraseña/i).fill(ADMIN.password);
    await page.getByRole('button', { name: /ingresar/i }).click();
    await expect(page).toHaveURL(/\/tenants/);

    await page.goto('/permission-templates');
    await expect(page).toHaveURL(/\/permission-templates/);
  });

  test('HAPPY · crear plantilla nace en Draft y ofrece Publicar/Eliminar (G-140)', async ({
    page,
  }) => {
    await createTemplate(page);

    // La Draft recién creada ofrece «Publicar» y «Eliminar»; nunca «Descontinuar» (eso es Published).
    await expect(publishBtn(page)).toBeVisible({ timeout: 15000 });
    await expect(deleteBtn(page)).toBeVisible();
    await expect(deprecateBtn(page)).toHaveCount(0);

    // Limpieza: no dejar residuo.
    await deleteSelectedDraft(page);
  });

  test('ERROR · publicar una Draft vacía es rechazado (TemplateItemsRequired) sin falso éxito', async ({
    page,
  }) => {
    await createTemplate(page);

    // Publicar sin ítems: el dominio lo rechaza (TemplateItemsRequired).
    await publishBtn(page).click();
    await expect(errorToast(page)).toBeVisible({ timeout: 15000 });

    // Sin falso éxito (G-129): sigue en Draft y NO apareció «Descontinuar» (no pasó a Published).
    await expect(draftBadge(page).first()).toBeVisible({ timeout: 15000 });
    await expect(publishBtn(page)).toBeVisible();
    await expect(deprecateBtn(page)).toHaveCount(0);

    // Limpieza.
    await deleteSelectedDraft(page);
  });

  test('ESTADO · una plantilla Published expone Descontinuar y oculta Publicar/Eliminar', async ({
    page,
  }) => {
    // La lista exige filtro para cargar. El <select> de filtro es el único con opción value="Published".
    await page.locator('select:has(option[value="Published"])').selectOption('Published');

    const firstRow = page.locator('[data-testid="entity-row"]').first();
    await expect(firstRow).toBeVisible({ timeout: 15000 });
    await firstRow.click();

    // Published ⇒ «Descontinuar» presente; «Publicar»/«Eliminar» ausentes (gating por estado).
    await expect(deprecateBtn(page)).toBeVisible({ timeout: 15000 });
    await expect(publishedBadge(page).first()).toBeVisible({ timeout: 15000 });
    await expect(publishBtn(page)).toHaveCount(0);
    await expect(deleteBtn(page)).toHaveCount(0);
  });
});
