import { test, expect, type Page } from '@playwright/test';

/**
 * Catálogo de Parametrización (ParameterDefinition) · CRUD — Matriz de Cobertura de UI, sección C
 * (ver `reference/qa/ui-coverage-matrix.md`).
 *
 * Certifica los fixes G-141, G-142 y G-145 sobre el dominio `/parameter-catalog`
 * (pantalla `ParameterCatalogScreen`, hook `use-parameter-catalog-dashboard`):
 *
 *   1. HAPPY · crear una Definición de Parámetro nueva (certifica [G-145]) · tras `POST 201` el
 *      servicio hace `getById` y devuelve la entidad completa → el diálogo de alta **se cierra** y la
 *      definición queda **auto-seleccionada en el detalle** (código y controles Editar/Eliminar
 *      visibles). Antes el alta lanzaba al parsear el 201 (que trae sólo el Guid) y el diálogo
 *      quedaba abierto sin seleccionar.
 *
 *   2. HAPPY · editar (certifica [G-141]) · el guardado usa **PUT /parameter-definitions/{id}**
 *      (antes PATCH → 405) y **relee con GET**. Se verifica el contrato (respuesta **204**, NO 405)
 *      y que el cambio de descripción se **refleja** en el detalle.
 *
 *   3. HAPPY · borrar SIN dependientes (certifica [G-142]) · existe `DELETE /parameter-definitions/{id}`;
 *      una definición recién creada no tiene valores globales/de inquilino → responde **204** y
 *      desaparece de la lista y del detalle (sin residuo).
 *
 *   4. ERROR · borrar CON dependientes (certifica [G-142] error-path, familia [G-129]) · el backend
 *      RECHAZA con **409** (BlockedOperation `ParameterHasActiveValues`) si la definición tiene valores
 *      asociados. Se ejercita sobre el parámetro sembrado «Incoterm» (`INCOTERM`), que el
 *      `ParameterCatalogSeeder` dota de valores de inquilino (tenant EVOLITH). Se verifica que el
 *      DELETE devuelve **409** y que la definición **sigue existiendo** (sin falso éxito).
 *
 * ── Certificación por contrato + estado del panel (no por texto frágil) ─────────────
 * El catálogo NO emite toasts en su CRUD (las mutaciones usan `useMutation` plano, sin
 * `useNotifiedMutation`), así que las señales robustas son: (a) el **status HTTP** de la respuesta
 * de la mutación (interceptado con `waitForResponse`), que ata cada aserción al contrato del fix, y
 * (b) la **presencia/ausencia de fila** y el **estado del panel de detalle**.
 * Nota UX (no bloqueante): el error-path (409) no muestra feedback visual; el diálogo de confirmación
 * simplemente no se cierra. Por eso el rechazo se certifica por status 409 + persistencia de la fila.
 */
const ADMIN = { email: 'admin@evolith.example', password: 'Evolith.Dev.2026' };

/** Parámetro sembrado por `ParameterCatalogSeeder` CON valores de inquilino (tenant EVOLITH) → borrar da 409. */
const SEEDED_WITH_DEPENDENTS = { code: 'INCOTERM', name: 'Incoterm' };

/** Código único por corrida: evita colisión `ParameterCodeNotUnique` aunque una corrida previa dejara residuo. */
const uniqueCode = () => `E2E_PARAM_${Date.now()}`;

const addBtn = (page: Page) => page.locator('button[title="Agregar"]').first();
const form = (page: Page) => page.locator('#parameter-form');
const submitFormBtn = (page: Page) => page.locator('button[type="submit"][form="parameter-form"]');
const listSearch = (page: Page) => page.locator('[data-testid="list-search"]');
const searchSubmitBtn = (page: Page) => page.getByRole('button', { name: /^buscar$/i });
const detailEditBtn = (page: Page) => page.locator('button[title="Editar"]').first();
const detailDeleteBtn = (page: Page) => page.locator('button[title="Eliminar"]').first();
const confirmDialog = (page: Page) => page.getByRole('dialog');
const rowByText = (page: Page, text: string) =>
  page.locator('[data-testid="entity-row"]').filter({ hasText: text });

/**
 * Abre el alta y crea una definición. Verifica el 201 del POST y — gracias a [G-145] — que el diálogo
 * se cierra y la definición queda AUTO-SELECCIONADA en el detalle. Campos del `ParameterDefinitionForm`:
 * inputs → [0]=código [1]=nombre [2]=descripción [3]=valor por defecto [4]=orden.
 */
async function createDefinition(
  page: Page,
  opts: { code: string; name: string; description: string; defaultValue: string }
): Promise<void> {
  await addBtn(page).click();
  await expect(form(page)).toBeVisible({ timeout: 15000 });
  const inputs = form(page).locator('input');
  await inputs.nth(0).fill(opts.code); // el onChange fuerza mayúsculas
  await inputs.nth(1).fill(opts.name);
  await inputs.nth(2).fill(opts.description);
  await inputs.nth(3).fill(opts.defaultValue);

  const createResp = page.waitForResponse(
    r => r.url().includes('/parameter-definitions') && r.request().method() === 'POST',
    { timeout: 20000 }
  );
  await submitFormBtn(page).click();
  expect((await createResp).status()).toBe(201); // el recurso se creó en el backend

  // G-145: el diálogo se cierra y la definición queda seleccionada en el detalle (relectura con GET).
  await expect(form(page)).toBeHidden({ timeout: 15000 });
  await expect(detailEditBtn(page)).toBeVisible({ timeout: 15000 });
  await expect(page.getByText(opts.code, { exact: false }).first()).toBeVisible({ timeout: 15000 });
}

/** Borra la definición seleccionada y devuelve el status HTTP del DELETE (204 éxito, 409 con dependientes). */
async function deleteSelected(page: Page): Promise<number> {
  await detailDeleteBtn(page).click();
  await expect(confirmDialog(page)).toBeVisible({ timeout: 15000 });
  const delResp = page.waitForResponse(
    r => r.url().includes('/parameter-definitions/') && r.request().method() === 'DELETE',
    { timeout: 20000 }
  );
  await confirmDialog(page)
    .getByRole('button', { name: /^eliminar$/i })
    .click();
  const resp = await delResp;
  return resp.status();
}

test.describe('Parametrización · CRUD de definiciones', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
    await page.getByLabel(/correo electrónico/i).fill(ADMIN.email);
    await page.getByLabel(/contraseña/i).fill(ADMIN.password);
    await page.getByRole('button', { name: /ingresar/i }).click();
    await expect(page).toHaveURL(/\/tenants/);

    await page.goto('/parameter-catalog');
    await expect(page).toHaveURL(/\/parameter-catalog/);
  });

  test('HAPPY · crear cierra el diálogo y auto-selecciona la definición (G-145)', async ({ page }) => {
    const code = uniqueCode();
    // createDefinition ya certifica: 201 + diálogo cerrado + auto-selección en el detalle.
    await createDefinition(page, {
      code,
      name: 'Parámetro E2E de prueba',
      description: 'Descripción inicial E2E',
      defaultValue: '30',
    });

    // Limpieza: borrar sin dependientes → 204.
    const status = await deleteSelected(page);
    expect(status).toBe(204);
  });

  test('HAPPY · editar refleja el cambio vía PUT + relectura (G-141)', async ({ page }) => {
    const code = uniqueCode();
    const oldDesc = `Desc inicial ${code}`;
    const newDesc = `Desc EDITADA ${code}`;

    await createDefinition(page, {
      code,
      name: 'Parámetro editable E2E',
      description: oldDesc,
      defaultValue: '10',
    });
    // La definición ya está auto-seleccionada (G-145); el detalle muestra la descripción inicial.
    await expect(page.getByText(oldDesc).first()).toBeVisible({ timeout: 15000 });

    // Abrir edición: el código va deshabilitado; cambiamos la descripción (input [2]).
    await detailEditBtn(page).click();
    await expect(form(page)).toBeVisible({ timeout: 15000 });
    await form(page).locator('input').nth(2).fill(newDesc);

    // G-141: el guardado usa PUT (antes PATCH→405). Verificamos el contrato: 204, NO 405.
    const putResp = page.waitForResponse(
      r => r.url().includes('/parameter-definitions/') && r.request().method() === 'PUT',
      { timeout: 20000 }
    );
    await submitFormBtn(page).click();
    expect((await putResp).status()).toBe(204);

    // El fix relee con GET → el detalle debe reflejar la nueva descripción y ya no la vieja.
    await expect(form(page)).toBeHidden({ timeout: 15000 });
    await expect(page.getByText(newDesc).first()).toBeVisible({ timeout: 15000 });
    await expect(page.getByText(oldDesc)).toHaveCount(0);

    // Limpieza.
    const status = await deleteSelected(page);
    expect(status).toBe(204);
  });

  test('HAPPY · borrar SIN dependientes responde 204 y desaparece (G-142)', async ({ page }) => {
    const code = uniqueCode();
    await createDefinition(page, {
      code,
      name: 'Parámetro borrable E2E',
      description: 'Sin valores asociados',
      defaultValue: '5',
    });

    // La definición está auto-seleccionada (G-145): borrar directamente desde el detalle.
    const status = await deleteSelected(page);
    expect(status).toBe(204);

    // Al borrar con éxito: se limpia la selección (sin botón de borrar) y no aparece al buscar.
    await expect(detailDeleteBtn(page)).toHaveCount(0, { timeout: 15000 });
    await listSearch(page).fill(code);
    await searchSubmitBtn(page).click();
    await expect(rowByText(page, code)).toHaveCount(0, { timeout: 15000 });
  });

  test('ERROR · borrar CON dependientes es rechazado con 409 y la definición persiste (G-142)', async ({
    page,
  }) => {
    // Cargar el parámetro sembrado con valores (INCOTERM) filtrando por su código.
    await listSearch(page).fill(SEEDED_WITH_DEPENDENTS.code);
    await searchSubmitBtn(page).click();

    const row = rowByText(page, SEEDED_WITH_DEPENDENTS.name).first();
    await expect(row).toBeVisible({ timeout: 15000 });
    await row.click();

    // Intentar borrar: el backend rechaza con 409 (ParameterHasActiveValues) porque tiene valores.
    const status = await deleteSelected(page);
    expect(status).toBe(409);

    // Sin falso éxito (familia G-129): la definición sigue existiendo (fila presente tras el rechazo).
    await expect(rowByText(page, SEEDED_WITH_DEPENDENTS.name)).toBeVisible({ timeout: 15000 });
  });
});
