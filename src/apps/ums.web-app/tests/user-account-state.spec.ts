import { test, expect, type Page } from '@playwright/test';
import { borrarResiduo } from './helpers/limpieza';

/**
 * Cuentas de Usuario · ciclo de estado — Ola 1 de la Matriz de Cobertura de UI (G-130).
 *
 * Cubre las acciones de estado alcanzables desde la `UserAccountProfileCard`:
 *   - Crear cuenta   → nace en `Pending`.
 *   - Activar        (botón visible SOLO en `Pending`)  → `Active`.
 *   - Bloquear       (botón visible SOLO en `Active`)   → `Blocked`  (diálogo con motivo).
 *   - Restaurar      (botón visible SOLO en `Blocked`)  → `Active`   (diálogo de confirmación).
 *
 * ── Por qué NO hay error-path spec para UserAccount ────────────────────────────
 * La card renderiza EXACTAMENTE UN botón por estado (`renderActions`), y las tres
 * guardas de dominio (`UserAccount.cs`) son sobre el PROPIO estado de la cuenta:
 *   - Activate → `CannotActivate` si `Status != Pending`   (pero el botón solo sale en Pending)
 *   - Block    → `AlreadyBlocked` si `Status == Blocked`   (pero en Blocked la card muestra «Restaurar», no «Bloquear»)
 *   - Restore  → `CannotRestore`  si `Status != Blocked`   (pero el botón solo sale en Blocked)
 * Es decir: cada botón visible solo existe en el estado donde su acción SIEMPRE tiene
 * éxito. A diferencia de Tenant·Suspender (cuyo rechazo depende de OTRO agregado —usuarios/
 * sucursales activas— con el botón visible), aquí NO se puede forzar un rechazo del backend
 * desde un control visible en una sesión determinista. Por tanto solo hay happy-path.
 * (Ver `reference/qa/ui-coverage-matrix.md`, sección C + clasificación de rechazo.)
 *
 * ── Sutileza de refresco (relevante para las asserts) ──────────────────────────
 * Los mutations Activar/Bloquear/Restaurar invalidan la clave `['user-accounts', <id>]`,
 * que NO hace prefix-match con la clave de la LISTA `['user-accounts', page, ...]`. Además el
 * dashboard NO parchea el estado localmente en `onSuccess`. Resultado: tras la acción la card
 * NO cambia de botón ni de badge por sí sola (no hay flip optimista — bien, en la línea de
 * [G-129]: no hay falso éxito). Para verificar el ESTADO REAL persistido se recarga la página
 * (limpia la caché en memoria de React Query → refetch fresco del servidor) y se re-selecciona
 * la cuenta. Esa re-lectura es la que certifica el cambio de estado real.
 *
 * Selección robusta vía data-testid instrumentados (G-130):
 *   - `entity-row` (+ hasText email) → seleccionar la fila de la cuenta.
 *   - `status-badge` (+ data-status)  → asertar el estado REAL (crudo: Active/Blocked/Pending).
 *   - `toast` (+ data-toast-type)     → asertar success/warning sin depender del texto.
 *   - `list-search`                   → buscador de la lista (`.last()` desambigua).
 */
const ADMIN = { email: 'admin@evolith.example', password: 'Evolith.Dev.2026' };

/** Rellena el buscador de la lista principal con el email y lanza la búsqueda. */
async function searchByEmail(page: Page, email: string): Promise<void> {
  const search = page.locator('[data-testid="list-search"]').last();
  await expect(search).toBeVisible({ timeout: 15000 });
  await search.fill('');
  await search.fill(email);
  // La búsqueda de la lista es del lado servidor: se dispara con el botón «Buscar», no con Enter.
  await page.getByRole('button', { name: /^buscar$/i }).last().click();
}

/** Localiza la fila de la cuenta por email y la selecciona (abre su detalle). */
async function selectAccount(page: Page, email: string): Promise<void> {
  const row = page.locator('[data-testid="entity-row"]').filter({ hasText: email }).first();
  await expect(row).toBeVisible({ timeout: 15000 });
  await row.click();
}

/** Re-selecciona la cuenta desde la lista (que se auto-refresca tras cada mutación, G-134
 *  corregido) para leer su estado REAL persistido. Sin reload (evita resetear el filtro). */
async function reloadAndSelect(page: Page, email: string): Promise<void> {
  await searchByEmail(page, email);
  await selectAccount(page, email);
}

/** Crea una cuenta de usuario mínima (solo email) y espera el toast de éxito. */
async function createAccount(page: Page, email: string): Promise<void> {
  await page.locator('button[title="Agregar"]').first().click();
  await expect(page.getByText(/crear nueva cuenta de usuario/i)).toBeVisible({ timeout: 15000 });
  await page.getByPlaceholder('user@company.com').fill(email);
  await page.getByRole('button', { name: /^crear usuario$/i }).click();
  await expect(
    page.locator('[data-testid="toast"][data-toast-type="success"]').first(),
  ).toBeVisible({ timeout: 15000 });
  // El diálogo de creación se cierra en onSuccess.
  await expect(page.getByText(/crear nueva cuenta de usuario/i)).toBeHidden({ timeout: 10000 });
}

test.describe('Cuentas de Usuario · ciclo de estado (happy path)', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
    await page.getByLabel(/correo electrónico/i).fill(ADMIN.email);
    await page.getByLabel(/contraseña/i).fill(ADMIN.password);
    await page.getByRole('button', { name: /ingresar/i }).click();
    await expect(page).toHaveURL(/\/tenants/);

    // La sesión persiste en localStorage (zustand persist) → navegación directa a /users.
    await page.goto('/users');
    await expect(page).toHaveURL(/\/users/);
    await expect(page.locator('[data-testid="list-search"]').last()).toBeVisible({
      timeout: 15000,
    });
  });

  test('HAPPY · crear cuenta nace en Pending y ofrece «Activar»', async ({ page }) => {
    const email = `e2e.ua.create.${Date.now()}@e2e.evolith.test`;

    await createAccount(page, email);
    await searchByEmail(page, email);
    await selectAccount(page, email);

    // Estado real recién creado = Pending; la card muestra el botón «Activar».
    await expect(
      page.locator('[data-testid="status-badge"][data-status="Pending"]').first(),
    ).toBeVisible({ timeout: 15000 });
    await expect(page.getByRole('button', { name: /^activar$/i })).toBeVisible();
    // En Pending NO existen los botones de otros estados.
    await expect(page.getByRole('button', { name: /^bloquear$/i })).toHaveCount(0);
    await expect(page.getByRole('button', { name: /^restaurar$/i })).toHaveCount(0);
  });

  test('HAPPY · ciclo Activar → Bloquear → Restaurar refleja el estado REAL (G-129)', async ({
    page,
  }) => {
    const email = `e2e.ua.cycle.${Date.now()}@e2e.evolith.test`;

    // ── Arranque: cuenta nueva en Pending ────────────────────────────────────
    await createAccount(page, email);
    await searchByEmail(page, email);
    await selectAccount(page, email);
    await expect(page.getByRole('button', { name: /^activar$/i })).toBeVisible({ timeout: 15000 });

    // ── (1) ACTIVAR: Pending → Active ────────────────────────────────────────
    await page.getByRole('button', { name: /^activar$/i }).click();
    await expect(
      page.locator('[data-testid="toast"][data-toast-type="success"]').first(),
    ).toBeVisible({ timeout: 15000 });

    // Re-lectura del estado REAL persistido (la card no se auto-refresca; ver cabecera).
    await reloadAndSelect(page, email);
    await expect(
      page.locator('[data-testid="status-badge"][data-status="Active"]').first(),
    ).toBeVisible({ timeout: 15000 });
    // En Active la card ofrece «Bloquear» (y desaparece «Activar»).
    await expect(page.getByRole('button', { name: /^bloquear$/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /^activar$/i })).toHaveCount(0);

    // ── (2) BLOQUEAR: Active → Blocked (diálogo con motivo) ───────────────────
    await page.getByRole('button', { name: /^bloquear$/i }).first().click();
    // Diálogo de motivo: el campo se ancla por su nombre accesible (label «Motivo de Bloqueo»).
    // Nota: M3TextField renderiza el `placeholder` como hint flotante (nodo de texto), NO como
    // atributo `placeholder` nativo del input → `getByPlaceholder` no lo encuentra; usamos el rol.
    const reason = page.getByRole('textbox', { name: /motivo de bloqueo/i });
    await expect(reason).toBeVisible({ timeout: 10000 });
    await reason.fill('Prueba E2E de bloqueo');
    // El confirmar se acota al panel del modal (M3FormDialog no usa portal ni role=dialog, y
    // `modalOverlays` se renderiza antes que la card → el «Bloquear» del diálogo es .first(),
    // no .last(); usar .last() clicaría el botón de la card, tapado por el scrim del modal).
    const blockDialog = page
      .locator('div.fixed.inset-0.z-50')
      .filter({ hasText: /bloquear cuenta de usuario/i });
    await blockDialog.getByRole('button', { name: /^bloquear$/i }).click();
    // El bloqueo notifica con tipo `warning` (no `success`).
    await expect(
      page.locator('[data-testid="toast"][data-toast-type="warning"]').first(),
    ).toBeVisible({ timeout: 15000 });

    await reloadAndSelect(page, email);
    await expect(
      page.locator('[data-testid="status-badge"][data-status="Blocked"]').first(),
    ).toBeVisible({ timeout: 15000 });
    // En Blocked la card ofrece «Restaurar» (y desaparece «Bloquear»).
    await expect(page.getByRole('button', { name: /^restaurar$/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /^bloquear$/i })).toHaveCount(0);

    // ── (3) RESTAURAR: Blocked → Active (diálogo de confirmación) ─────────────
    await page.getByRole('button', { name: /^restaurar$/i }).first().click();
    // El diálogo de confirmación (ConfirmDialog, role=dialog) añade un 2º botón «Restaurar»;
    // se confirma dentro del propio diálogo (el .last() clicaría el de la card, tapado por el scrim).
    await expect(page.getByRole('button', { name: /^restaurar$/i })).toHaveCount(2, { timeout: 10000 });
    await page.getByRole('dialog').getByRole('button', { name: /^restaurar$/i }).click();
    await expect(
      page.locator('[data-testid="toast"][data-toast-type="success"]').first(),
    ).toBeVisible({ timeout: 15000 });

    await reloadAndSelect(page, email);
    // Restaurar deja la cuenta de nuevo en Active → vuelve a ofrecer «Bloquear».
    await expect(
      page.locator('[data-testid="status-badge"][data-status="Active"]').first(),
    ).toBeVisible({ timeout: 15000 });
    await expect(page.getByRole('button', { name: /^bloquear$/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /^restaurar$/i })).toHaveCount(0);
  });

  /*
   * Borra las cuentas que esta prueba creo. Sin esto la suite es ADITIVA: cada corrida deja una
   * cuenta mas en el catalogo, y las pruebas que buscan una fila o abren un desplegable trabajan
   * sobre listas cada vez mas largas. Medido el 2026-08-03: 40 de las 376 cuentas del cluster eran
   * residuo de corridas anteriores. Es la forma del parpadeo que documenta G-245.
   */
  test.afterAll(async () => {
    await borrarResiduo('user-accounts', /e2e\.ua\.create\./i);
  });
});
