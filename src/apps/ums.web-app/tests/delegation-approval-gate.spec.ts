import { test, expect, type Page } from '@playwright/test';

/**
 * Delegaciones · GATE de aprobación (separación de funciones) — certificación EN VIVO.
 *
 * Qué certifica (corazón de la seguridad de G-132 / G-150, invariante INV-DEL8):
 *   El admin que CREA/solicita una delegación NO puede AUTO-APROBARLA. Como `admin@evolith`
 *   es el `delegatingAdmin` (creador) y también quien pulsa «Aprobar», el backend DEBE
 *   RECHAZAR la auto-aprobación. Y —clave, familia [G-129]— el frontend NO puede simular
 *   éxito: la delegación SIGUE en `PendingApproval`, sale un toast de tipo `error`, y el botón
 *   «Aprobar» NO flipa a estado activo (sin flip optimista pre-commit).
 *   (Ver `reference/qa/ui-coverage-matrix.md`: la lección de [G-129] es que el estado se
 *    MIDE contra el servidor, no se afirma desde un flip local.)
 *
 * ── Sutileza de refresco (relevante para las asserts) ──────────────────────────
 * `submit/approve/reject` invalidan SÓLO la clave `['delegations', <id>]`, que NO hace
 * prefix-match con la clave de la LISTA `['delegations', 'by-delegating-admin', ...]`. Por
 * tanto la card NO se auto-refresca tras esas mutaciones. Para leer el ESTADO REAL persistido
 * se RECARGA la página (limpia la caché en memoria de React Query → refetch fresco del
 * servidor) y se re-selecciona la delegación. Esa re-lectura es la que certifica el estado real
 * (mismo patrón que `user-account-state.spec.ts`).
 *
 * ── Selección robusta ──────────────────────────────────────────────────────────
 * Todas las filas de delegación se ven idénticas («Delegación de Acceso» + scope), así que se
 * captura el `delegationId` de la respuesta del POST de creación y se filtra la lista por ese ID
 * (búsqueda «By ID», client-side sobre `delegationId.includes(term)`) → queda UNA sola fila.
 *
 * Idempotencia: cada corrida crea una delegación NUEVA (ID único) y, como limpieza best-effort,
 * la RECHAZA (único control terminal disponible en `PendingApproval`; no hay «eliminar»). El
 * residuo queda en estado `Rejected`, fuera de las vistas activas y sin colisionar entre corridas.
 */

const ADMIN = { email: 'admin@evolith.example', password: 'Evolith.Dev.2026' };

/** Formatea una fecha a `YYYY-MM-DDTHH:mm` (valor nativo de un input datetime-local). */
function toLocalInput(d: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

/** Rellena el buscador de la lista y lanza la búsqueda server-side por el botón «Buscar». */
async function searchById(page: Page, delegationId: string): Promise<void> {
  const search = page.locator('[data-testid="list-search"]').last();
  await expect(search).toBeVisible({ timeout: 15000 });
  await search.fill('');
  await search.fill(delegationId);
  await page.getByRole('button', { name: /^buscar$/i }).last().click();
}

/**
 * Abre la pestaña «Granted Delegations» (donde vive lo que admin@evolith ha otorgado),
 * filtra por el ID y selecciona la única fila resultante. Deja la card de detalle abierta.
 */
async function openGrantedAndSelect(page: Page, delegationId: string): Promise<void> {
  await page.getByRole('button', { name: /granted delegations/i }).click();
  await searchById(page, delegationId);
  const row = page.locator('[data-testid="entity-row"]');
  await expect(row).toHaveCount(1, { timeout: 15000 });
  await row.first().click();
}

test.describe('Delegaciones · GATE de aprobación (self-approval rechazada)', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
    await page.getByLabel(/correo electrónico/i).fill(ADMIN.email);
    await page.getByLabel(/contraseña/i).fill(ADMIN.password);
    await page.getByRole('button', { name: /ingresar/i }).click();
    await expect(page).toHaveURL(/\/tenants/);

    await page.goto('/delegations');
    await expect(page).toHaveURL(/\/delegations/);
  });

  test('INV-DEL8 · el creador NO puede auto-aprobar: rechazo en vivo, sin falso éxito (G-150/anti-G-129)', async ({
    page,
  }) => {
    // ── (1) CREAR una delegación que REQUIERE aprobación (admin@evolith = delegante/creador) ──
    await page.locator('button[title="Agregar"]').first().click();
    const createDialog = page.getByRole('dialog').filter({ hasText: /crear delegaci/i });
    await expect(createDialog).toBeVisible({ timeout: 15000 });

    // Marca «Requiere Aprobación» para que nazca en Draft y pueda pasar a PendingApproval.
    await createDialog.locator('#requiresApproval').check();

    // Período de validez (campos requeridos): desde mañana, hasta +30 días.
    const from = new Date(Date.now() + 24 * 60 * 60 * 1000);
    const until = new Date(Date.now() + 30 * 24 * 60 * 60 * 1000);
    const dates = createDialog.locator('input[type="datetime-local"]');
    await dates.nth(0).fill(toLocalInput(from));
    await dates.nth(1).fill(toLocalInput(until));

    // Captura el delegationId de la respuesta del POST /delegations (para filtrar por ID).
    const createRespP = page.waitForResponse(
      r => r.request().method() === 'POST' && /\/delegations$/.test(new URL(r.url()).pathname),
      { timeout: 20000 }
    );
    await createDialog.getByRole('button', { name: /^crear/i }).click();
    const createResp = await createRespP;
    expect(createResp.ok(), `POST /delegations debe crear (2xx); status=${createResp.status()}`).toBeTruthy();
    const { delegationId } = (await createResp.json()) as { delegationId: string };
    expect(delegationId, 'la respuesta de creación debe traer delegationId').toBeTruthy();

    // Toast de creación (success) — la mutación es dueña de la notificación.
    await expect(
      page.locator('[data-testid="toast"][data-toast-type="success"]').first()
    ).toBeVisible({ timeout: 15000 });

    // ── (2) Localizar la delegación recién creada (vista Granted) y verla en Draft ──
    // handleCreateSuccess ya conmuta a 'granted' y resetea la query; re-seleccionamos por ID.
    await openGrantedAndSelect(page, delegationId);
    await expect(
      page.locator('[data-testid="status-badge"][data-status="Draft"]').first()
    ).toBeVisible({ timeout: 15000 });
    const submitBtn = page.getByRole('button', { name: /enviar a aprobaci/i });
    await expect(submitBtn).toBeVisible({ timeout: 15000 });

    // ── (3) Enviar a aprobación: Draft → PendingApproval ──
    // Se SINCRONIZA contra la RESPUESTA de red del POST (no contra el toast: el toast de
    // creación —también `success`— puede seguir visible y un `page.reload()` prematuro abortaría
    // el POST en vuelo dejando la delegación en Draft). El status HTTP es la fuente de verdad.
    const submitRespP = page.waitForResponse(
      r => r.request().method() === 'POST' && /\/submit-for-approval$/.test(new URL(r.url()).pathname),
      { timeout: 20000 }
    );
    await submitBtn.click();
    const submitResp = await submitRespP;
    expect(
      submitResp.ok(),
      `submit-for-approval debe persistir (2xx); status=${submitResp.status()}`
    ).toBeTruthy();

    // La card NO se auto-refresca (la invalidación de submit no toca la clave de la lista):
    // recargamos para leer el ESTADO REAL persistido = PendingApproval.
    await page.reload();
    await openGrantedAndSelect(page, delegationId);
    await expect(
      page.locator('[data-testid="status-badge"][data-status="PendingApproval"]').first()
    ).toBeVisible({ timeout: 15000 });
    const approveBtn = page.getByRole('button', { name: /^aprobar$/i });
    await expect(approveBtn).toBeVisible({ timeout: 15000 });
    await expect(page.getByRole('button', { name: /^rechazar$/i })).toBeVisible();

    // ── (4) INTENTAR AUTO-APROBAR → el backend DEBE rechazar (INV-DEL8) ──
    // Sincroniza contra la RESPUESTA HTTP del POST /approve: el gate debe devolver un status de
    // ERROR (4xx). Ese status NO-2xx es el certificado EN VIVO de que la auto-aprobación se rechaza.
    const approveRespP = page.waitForResponse(
      r => r.request().method() === 'POST' && /\/approve$/.test(new URL(r.url()).pathname),
      { timeout: 20000 }
    );
    await approveBtn.click();
    const approveResp = await approveRespP;
    expect(
      approveResp.ok(),
      `AUTO-APROBACIÓN debe ser RECHAZADA por el gate (status NO-2xx); status=${approveResp.status()}`
    ).toBeFalsy();

    // (a) Toast de tipo ERROR = el frontend refleja el rechazo (no traga el error, no simula éxito).
    await expect(
      page.locator('[data-testid="toast"][data-toast-type="error"]').first()
    ).toBeVisible({ timeout: 15000 });

    // (b) SIN falso éxito, IN-PLACE: el badge sigue PendingApproval y «Aprobar» NO flipó
    //     (no hay flip optimista pre-commit; anti-[G-129]).
    await expect(
      page.locator('[data-testid="status-badge"][data-status="PendingApproval"]').first()
    ).toBeVisible();
    await expect(page.getByRole('button', { name: /^aprobar$/i })).toBeVisible();
    // Nunca debe haber flipado a Activa ni ofrecido «Revoke» (control exclusivo de Active).
    await expect(
      page.locator('[data-testid="status-badge"][data-status="Active"]')
    ).toHaveCount(0);
    await expect(page.getByRole('button', { name: /revoke/i })).toHaveCount(0);

    // (c) CERTIFICADO DEFINITIVO: el ESTADO PERSISTIDO en el servidor NO cambió a Active.
    //     Recarga fresca → re-lectura del backend. Si aquí saliera «Active» sería un HALLAZGO
    //     CRÍTICO de seguridad (el gate no habría rechazado la auto-aprobación).
    await page.reload();
    await openGrantedAndSelect(page, delegationId);
    await expect(
      page.locator('[data-testid="status-badge"][data-status="PendingApproval"]').first()
    ).toBeVisible({ timeout: 15000 });
    await expect(
      page.locator('[data-testid="status-badge"][data-status="Active"]')
    ).toHaveCount(0);
    await expect(page.getByRole('button', { name: /^aprobar$/i })).toBeVisible();

    // ── (5) LIMPIEZA best-effort: rechazar la delegación (control terminal en PendingApproval).
    //     No se asevera su resultado: si la separación de funciones también acotara el rechazo
    //     por el creador, el residuo queda en PendingApproval (ID único → sin colisión entre runs).
    try {
      await page.getByRole('button', { name: /^rechazar$/i }).click();
      await page
        .locator('[data-testid="toast"]')
        .first()
        .waitFor({ state: 'visible', timeout: 8000 });
    } catch {
      // Limpieza no crítica: el ID único evita interferencia entre corridas.
    }
  });
});
