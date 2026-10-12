import { test, expect, Page } from '@playwright/test';

/**
 * Cadena PROFUNDA de creación del MVP (US-003…US-012) — TE-09 Ola 1 / residual de G-076.
 *
 * A diferencia de `mvp-flows.spec.ts` (crear tenant + LECTURA autorizada del resto),
 * estos caminos ejercitan la **cadena de creación** de punta a punta como MUTACIONES
 * reales (form → API → confirmación), asertando sobre la confirmación real (toast/estado):
 *
 *   1. Topología:  SystemSuite → Módulo → Menú → Opción            (US-003/004/005)
 *   2. Plantilla:  Suite + Rol → Plantilla de permisos (Borrador)  (US-006/007)
 *
 * Patrones vinculantes (TE-09): P1 — credenciales desde `DEV_PERSONAS`, nunca fixtures
 * hardcodeadas; los localizadores de navegación se acotan al riel. P3 — el camino se
 * verifica contra el backend real antes de darlo por bueno.
 *
 * Notas de localización (bundle desplegado):
 *  • Los formularios inline (Módulo/Nodo/Rol) usan `M3TextField`/`M3Select`, que SÍ
 *    asocian `<label htmlFor>` ⇒ `getByLabel` es fiable. El `placeholder` de esos
 *    campos se pinta como hint decorativo (no como atributo del input).
 *  • Los botones de acción del árbol (`IconButton`) no exponen nombre accesible; se
 *    localizan estructuralmente por su icono lucide (`svg.lucide-plus`) dentro de la
 *    fila del nodo (`div[class~="group/node"]`).
 *  • El formulario de plantilla usa `FieldSelect` (native `<select>` sin `htmlFor`);
 *    se opera por orden dentro del `role="dialog"`.
 */

const ADMIN = { email: 'admin@evolith.example', password: 'Evolith.Dev.2026' };

const rail = (page: Page) => page.getByTestId('nav-rail');

async function login(page: Page): Promise<void> {
  // Admin Root de EVOLITH (DEV_PERSONAS). El selector de tenant arranca en EVOLITH.
  await page.goto('/login');
  await page.getByLabel(/correo electrónico/i).fill(ADMIN.email);
  await page.getByLabel(/contraseña/i).fill(ADMIN.password);
  await page.getByRole('button', { name: /ingresar/i }).click();
  await expect(page).toHaveURL(/\/tenants/);
}

async function gotoSystemSuites(page: Page): Promise<void> {
  await rail(page)
    .getByRole('button', { name: /^(sistemas|systems)$/i })
    .first()
    .click();
  await expect(page).toHaveURL(/\/system-suites/);
}

/** Crea una SystemSuite y la selecciona en la lista, dejando su panel de detalle activo. */
async function createAndSelectSuite(page: Page, code: string, name: string): Promise<void> {
  // Esperar a que la lista de suites cargue (toolbar estable). La `useEffect` de
  // auto-selección del dashboard provoca un re-render al llegar los datos que puede
  // tragarse un click prematuro sobre «Agregar»; esperamos + reintentamos.
  const addBtn = page.locator('button[title="Agregar"]').first();
  await expect(addBtn).toBeVisible({ timeout: 15000 });
  const dialogTitle = page.getByText(/crear nuevo suite del sistema|create new system suite/i);
  await expect(async () => {
    await addBtn.click();
    await expect(dialogTitle).toBeVisible({ timeout: 3000 });
  }).toPass({ timeout: 20000 });
  await page.getByPlaceholder('SUITE_CRM').fill(code);
  await page.getByPlaceholder('CRM System Suite').fill(name);
  // La descripción es OBLIGATORIA en el backend (`CreateSystemSuiteCommandValidator`
  // exige `Description.NotEmpty()`) aunque el formulario la trate como opcional.
  await page
    .getByPlaceholder('Customer relationship management module')
    .fill(`Suite E2E ${name}`);
  await page.getByRole('button', { name: /registrar suite|register system suite/i }).click();

  // Confirmación real de la mutación: toast de éxito (no la lista paginada).
  await expect(
    page.getByText(/suite del sistema creado|system suite created/i).first()
  ).toBeVisible({ timeout: 15000 });

  // Seleccionar la suite recién creada. Tras crear, la lista se resetea a la página 1
  // ordenada por nombre y la suite aparece en ella. Ampliamos a 50/página para no
  // depender de la paginación (el input de búsqueda del toolbar queda con ancho ~0 en
  // el layout maestro-detalle dividido) y hacemos click en su fila por nombre.
  const fifty = page.getByRole('button', { name: '50', exact: true }).first();
  if (await fifty.isVisible().catch(() => false)) await fifty.click();
  await page.getByText(name, { exact: true }).first().click();
}

test.describe('Cadena profunda de creación MVP', () => {
  test.beforeEach(async ({ page }) => {
    await login(page);
  });

  test('US-003…005 · topología SystemSuite → Módulo → Menú → Opción (mutaciones E2E)', async ({
    page,
  }) => {
    // Códigos únicos por corrida (schema `max(20)`) para no colisionar (409) entre ejecuciones.
    const stamp = Date.now();
    const suiteCode = `E2ETOP${stamp}`.slice(0, 20);
    const suiteName = `E2E Topo ${stamp}`;
    const modCode = `MOD${stamp}`.slice(0, 20);
    const menuCode = `MENU${stamp}`.slice(0, 20);
    const optCode = `OPT${stamp}`.slice(0, 20);

    await gotoSystemSuites(page);

    // ── 1) SystemSuite ────────────────────────────────────────────────────────
    await createAndSelectSuite(page, suiteCode, suiteName);

    // ── 2) Módulo (dentro de la suite) ────────────────────────────────────────
    await page
      .getByRole('button', { name: /^(módulos|modules)$/i })
      .first()
      .click();
    await page.locator('button[title="Nuevo Módulo Estructural"]').first().click();
    const modForm = page
      .locator('form')
      .filter({ hasText: /guardar módulo/i })
      .first();
    await modForm.getByLabel(/código del módulo/i).fill(modCode);
    await modForm.getByLabel(/nombre del módulo/i).fill(`Módulo ${stamp}`);
    await modForm.getByLabel(/^descripción$/i).fill(`Módulo estructural E2E ${stamp}`);
    await modForm.getByRole('button', { name: /guardar módulo/i }).click();
    await expect(page.getByText(/módulo registrado/i).first()).toBeVisible({ timeout: 15000 });
    await expect(page.getByText(modCode).first()).toBeVisible();

    // ── 3) Activar el módulo ──────────────────────────────────────────────────
    // El módulo nace en estado Inactive y el dominio RECHAZA agregar nodos a un
    // módulo inactivo (AddNode → HTTP 400). Se activa desde la cabecera del módulo
    // (IconButton «Activar», icono lucide-shield-check, sin nombre accesible).
    const moduleHeader = page
      .locator('div[class~="group/module"]')
      .filter({ hasText: modCode })
      .first();
    await moduleHeader.hover();
    await moduleHeader.locator('button:has(svg.lucide-shield-check)').first().click();
    // Confirmación por estado (el toast «Módulo Activado» se autodescarta muy rápido):
    // la insignia de estado del módulo pasa a «Active». `exact` evita casar «Inactive».
    await expect(
      page
        .locator('div[class~="group/module"]')
        .filter({ hasText: modCode })
        .getByText('Active', { exact: true })
    ).toBeVisible({ timeout: 15000 });

    // ── 4) Nodo raíz = Menú ───────────────────────────────────────────────────
    await page
      .getByRole('button', { name: /agregar nodo raíz/i })
      .first()
      .click();
    const menuForm = page
      .locator('form')
      .filter({ hasText: /guardar nodo/i })
      .first();
    await menuForm.getByLabel(/tipo de nodo/i).selectOption('Menu');
    await menuForm.getByLabel(/código/i).fill(menuCode);
    await menuForm.getByLabel(/etiqueta/i).fill(`Menú ${stamp}`);
    await menuForm.getByLabel(/^descripción$/i).fill(`Nodo menú E2E ${stamp}`);
    await menuForm.getByRole('button', { name: /guardar nodo/i }).click();
    await expect(page.getByText(/nodo registrado/i).first()).toBeVisible({ timeout: 15000 });
    await expect(page.getByText(menuCode).first()).toBeVisible();

    // ── 5) Nodo hijo = Opción (bajo el Menú) ──────────────────────────────────
    // La fila del nodo Menú se identifica por su CodeBadge; el botón «Agregar nodo
    // hijo» es el primer IconButton de la fila (icono lucide-plus).
    const menuRow = page
      .locator('div[class~="group/node"]')
      .filter({ hasText: menuCode })
      .first();
    await menuRow.hover();
    await menuRow.locator('button:has(svg.lucide-plus)').first().click();
    const optForm = page
      .locator('form')
      .filter({ hasText: /guardar nodo/i })
      .first();
    await optForm.getByLabel(/tipo de nodo/i).selectOption('Option');
    await optForm.getByLabel(/código/i).fill(optCode);
    await optForm.getByLabel(/etiqueta/i).fill(`Opción ${stamp}`);
    await optForm.getByLabel(/^descripción$/i).fill(`Nodo opción E2E ${stamp}`);
    await optForm.getByRole('button', { name: /guardar nodo/i }).click();
    await expect(page.getByText(/nodo registrado/i).first()).toBeVisible({ timeout: 15000 });
    await expect(page.getByText(optCode).first()).toBeVisible();
  });

  test('US-006/007 · definir plantilla de permisos (Suite + Rol → Borrador) como mutación E2E', async ({
    page,
  }) => {
    const stamp = Date.now();
    const suiteCode = `E2ETPL${stamp}`.slice(0, 20);
    const suiteName = `E2E Tpl ${stamp}`;
    const roleCode = `ROLE${stamp}`.slice(0, 20);
    const roleName = `Rol E2E ${stamp}`;

    await gotoSystemSuites(page);

    // ── 1) Suite + Rol (la plantilla exige un rol dentro de la suite) ─────────
    await createAndSelectSuite(page, suiteCode, suiteName);

    // Acotado a `main`: con el menú «Roles» del catálogo en la barra, `.first()` cae en la
    // navegación y no en la pestaña del detalle — y entonces el panel nunca monta.
    await page
      .getByRole('main')
      .getByRole('button', { name: /^roles$/i })
      .first()
      .click();
    await page.locator('button[title="Nuevo rol"]').first().click();
    const roleForm = page
      .locator('form')
      .filter({ hasText: /guardar rol/i })
      .first();
    await roleForm.getByLabel(/código/i).fill(roleCode);
    await roleForm.getByLabel(/nombre visible/i).fill(roleName);
    await roleForm.getByRole('button', { name: /guardar rol/i }).click();
    await expect(page.getByText(/rol registrado|role registered/i).first()).toBeVisible({
      timeout: 15000,
    });
    await expect(page.getByText(roleName).first()).toBeVisible();

    // ── 2) Plantilla de permisos (Borrador) para esa Suite + Rol ─────────────
    await rail(page)
      .getByRole('button', { name: /(plantillas de permisos|permission templates)/i })
      .first()
      .click();
    await expect(page).toHaveURL(/\/permission-templates/);

    await page.locator('button[title="Agregar"]').first().click();
    const dialog = page.getByRole('dialog');
    await expect(dialog.getByText(/nueva plantilla de permisos/i)).toBeVisible();

    const selects = dialog.locator('select');
    await selects.nth(0).selectOption({ label: suiteName });
    // El select de Rol se habilita cuando cargan los roles de la suite elegida.
    await expect(selects.nth(1)).toBeEnabled({ timeout: 15000 });
    await selects.nth(1).selectOption({ label: roleName });

    await dialog.getByRole('button', { name: /crear plantilla/i }).click();

    // Confirmación real: toast «Plantilla Creada» (la mutación persistió el Borrador).
    await expect(page.getByText(/plantilla creada/i).first()).toBeVisible({ timeout: 15000 });
  });
});
