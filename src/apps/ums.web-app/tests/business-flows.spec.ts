import { test, expect, type Page } from '@playwright/test';

/**
 * FLUJOS DE NEGOCIO DE GOBIERNO (multi-paso) — G-084.
 *
 * Cierra huecos E2E de flujos de gobierno multi-paso que NO cubren los specs existentes.
 * Ref: `reference/qa/ui-coverage-matrix.md` (secciones D · Delegaciones y G · Plantillas).
 *
 * Se certifican DOS ciclos de vida completos, ambos DETERMINISTAS e IDEMPOTENTES:
 *
 *   FLUJO 1 · Delegación sin aprobación → ACTIVACIÓN DIRECTA → REVOCACIÓN (sección D).
 *     Complementa `delegation-approval-gate.spec.ts` (que sólo cubre el GATE de auto-aprobación de
 *     una delegación que SÍ requiere aprobación). Aquí se crea una delegación que NO requiere
 *     aprobación: nace en Draft y ofrece «Activar» directo (Draft→Active, `useActivateDelegation`),
 *     y luego se REVOCA (Active→Revoked, `useRevokeDelegation`) — dos transiciones «clase G-129»
 *     antes sin cobertura de UI. El estado se MIDE contra el servidor (status HTTP + status-badge).
 *
 *   FLUJO 2 · Plantilla de permisos: crear Draft → MATERIALIZAR un ítem → PUBLICAR (sección G).
 *     Complementa `permission-template-state.spec.ts` (que cubre crear-Draft, publicar-vacía-ERROR y
 *     el gating de una Published ya sembrada). Aquí se certifica el HAPPY de PUBLICAR
 *     (`usePublishPermissionTemplate`, acción de gobierno crítica — Ola 2): se agrega un ítem real
 *     al Draft (`useAddTemplateItem`) y se publica, aseverando la transición REAL Draft→Published.
 *
 * Descartados (no deterministas / sin valor incremental, documentados por transparencia):
 *   · Aprobar una solicitud de PERFIL por UI: `useApproveProfileRequest` materializa un perfil y
 *     exige una terna válida usuario+rol+plantilla-publicada; `profile-state.spec.ts` (G-151) ya
 *     documentó esa no-determinación y cubre el RECHAZO. No se re-intenta el approve.
 *   · Flag «peligroso» archivar: YA cubierto por `feature-flag-state.spec.ts` («Archivar un flag
 *     Inactive lo lleva al estado terminal»). No se duplica.
 *   · Rechazo de delegación pendiente: YA ejercido en el cleanup de `delegation-approval-gate`.
 *
 * Idempotencia: cada corrida crea entidades NUEVAS (delegación con ID único; plantilla VERSIONADA
 * por G-140) y las lleva a un estado terminal (Revoked / Deprecated) que no colisiona entre runs.
 */
const ADMIN = { email: 'admin@evolith.example', password: 'Evolith.Dev.2026' };

const toast = (page: Page, type: 'success' | 'info' | 'warning' | 'error') =>
  page.locator(`[data-testid="toast"][data-toast-type="${type}"]`).first();

const statusBadge = (page: Page, status: string) =>
  page.locator(`[data-testid="status-badge"][data-status="${status}"]`).first();

/** Formatea una fecha a `YYYY-MM-DDTHH:mm` (valor nativo de un input datetime-local). */
function toLocalInput(d: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

async function login(page: Page): Promise<void> {
  await page.goto('/login');
  await page.getByLabel(/correo electrónico/i).fill(ADMIN.email);
  await page.getByLabel(/contraseña/i).fill(ADMIN.password);
  await page.getByRole('button', { name: /ingresar/i }).click();
  await expect(page).toHaveURL(/\/tenants/);
}

// ── FLUJO 1 · DELEGACIÓN: activación directa + revocación ──────────────────────────
test.describe('Gobierno · Delegación sin aprobación (activación directa → revocación) — G-084', () => {
  test.beforeEach(async ({ page }) => {
    await login(page);
    await page.goto('/delegations');
    await expect(page).toHaveURL(/\/delegations/);
  });

  /** Busca la delegación por su ID (búsqueda «By ID», client-side) y selecciona la única fila. */
  async function openGrantedAndSelect(page: Page, delegationId: string): Promise<void> {
    await page.getByRole('button', { name: /granted delegations/i }).click();
    const search = page.locator('[data-testid="list-search"]').last();
    await expect(search).toBeVisible({ timeout: 15000 });
    await search.fill('');
    await search.fill(delegationId);
    await page
      .getByRole('button', { name: /^buscar$/i })
      .last()
      .click();
    const row = page.locator('[data-testid="entity-row"]');
    await expect(row).toHaveCount(1, { timeout: 15000 });
    await row.first().click();
  }

  test('CICLO · crear (sin aprobación) → activar directo (Draft→Active) → revocar (Active→Revoked)', async ({
    page,
  }) => {
    // ── (1) CREAR una delegación que NO requiere aprobación → nace en Draft ──
    await page.locator('button[title="Agregar"]').first().click();
    const dialog = page.getByRole('dialog').filter({ hasText: /crear delegaci/i });
    await expect(dialog).toBeVisible({ timeout: 15000 });

    // requiresApproval queda SIN marcar (default false) → activación directa habilitada.
    await expect(dialog.locator('#requiresApproval')).not.toBeChecked();

    // Período de validez: desde hace 1h (ya vigente) hasta +30 días → la activación es válida.
    const from = new Date(Date.now() - 60 * 60 * 1000);
    const until = new Date(Date.now() + 30 * 24 * 60 * 60 * 1000);
    const dates = dialog.locator('input[type="datetime-local"]');
    await dates.nth(0).fill(toLocalInput(from));
    await dates.nth(1).fill(toLocalInput(until));

    const createRespP = page.waitForResponse(
      r => r.request().method() === 'POST' && /\/delegations$/.test(new URL(r.url()).pathname),
      { timeout: 20000 }
    );
    await dialog.getByRole('button', { name: /^crear/i }).click();
    const createResp = await createRespP;
    expect(
      createResp.ok(),
      `POST /delegations debe crear (2xx); status=${createResp.status()}`
    ).toBeTruthy();
    const { delegationId } = (await createResp.json()) as { delegationId: string };
    expect(delegationId, 'la respuesta de creación debe traer delegationId').toBeTruthy();

    await expect(toast(page, 'success')).toBeVisible({ timeout: 15000 });

    // ── (2) Seleccionar la delegación recién creada → Draft + botón «Activar» directo ──
    await openGrantedAndSelect(page, delegationId);
    await expect(statusBadge(page, 'Draft')).toBeVisible({ timeout: 15000 });
    const activarBtn = page.getByRole('button', { name: /^activar$/i });
    await expect(activarBtn).toBeVisible({ timeout: 15000 });
    // Un Draft sin aprobación NO ofrece «Enviar a aprobación» ni «Revoke» todavía.
    await expect(page.getByRole('button', { name: /enviar a aprobaci/i })).toHaveCount(0);

    // ── (3) ACTIVAR directo: Draft→Active (useActivateDelegation) ──
    const activateRespP = page.waitForResponse(
      r =>
        r.request().method() === 'POST' &&
        /\/delegations\/[^/]+\/activate$/.test(new URL(r.url()).pathname),
      { timeout: 20000 }
    );
    await activarBtn.click();
    const activateResp = await activateRespP;
    expect(
      activateResp.ok(),
      `POST .../activate debe persistir (2xx); status=${activateResp.status()}`
    ).toBeTruthy();

    // Estado REAL Active (invalidación amplia ['delegations'] refresca la card): badge Active +
    // aparece «Revoke» y desaparece «Activar» (sin flip optimista; anti-G-129).
    await expect(statusBadge(page, 'Active')).toBeVisible({ timeout: 15000 });
    const revokeBtn = page.getByRole('button', { name: /revoke/i });
    await expect(revokeBtn).toBeVisible({ timeout: 15000 });
    await expect(page.getByRole('button', { name: /^activar$/i })).toHaveCount(0);

    // ── (4) REVOCAR: Active→Revoked (useRevokeDelegation) — transición terminal + limpieza ──
    const revokeRespP = page.waitForResponse(
      r =>
        r.request().method() === 'POST' &&
        /\/delegations\/[^/]+\/revoke$/.test(new URL(r.url()).pathname),
      { timeout: 20000 }
    );
    await revokeBtn.click();
    const revokeResp = await revokeRespP;
    expect(
      revokeResp.ok(),
      `POST .../revoke debe persistir (2xx); status=${revokeResp.status()}`
    ).toBeTruthy();

    // Estado REAL Revoked (terminal): badge Revoked y ya no se ofrece «Revoke».
    await expect(statusBadge(page, 'Revoked')).toBeVisible({ timeout: 15000 });
    await expect(page.getByRole('button', { name: /revoke/i })).toHaveCount(0);
    await expect(page.locator('[data-testid="toast"][data-toast-type="error"]')).toHaveCount(0);
  });
});

// ── FLUJO 2 · PLANTILLA DE PERMISOS: crear → materializar ítem → publicar ────────────
test.describe('Gobierno · Publicación de plantilla de permisos (Draft→Published) — G-084', () => {
  const SUITE_NAME = 'Aduanas';
  const SEEDED_ROLE = 'Agente de Aduanas';

  test.beforeEach(async ({ page }) => {
    await login(page);
    await page.goto('/permission-templates');
    await expect(page).toHaveURL(/\/permission-templates/);
  });

  test('CICLO · crear Draft → agregar un ítem (Permitir) → PUBLICAR expone Descontinuar', async ({
    page,
  }) => {
    // ── (1) CREAR una plantilla → nueva VERSIÓN de una terna sembrada (G-140, sin colisión) ──
    await page.locator('button[title="Agregar"]').first().click();
    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible({ timeout: 15000 });
    const selects = dialog.locator('select');
    await selects.nth(0).selectOption({ label: SUITE_NAME });
    await expect(selects.nth(1).locator('option')).not.toHaveCount(1, { timeout: 15000 });
    await selects.nth(1).selectOption({ label: SEEDED_ROLE });
    await dialog.getByRole('button', { name: /crear plantilla/i }).click();
    await expect(dialog).toBeHidden({ timeout: 15000 });
    // El dashboard auto-selecciona la nueva Draft en el detalle.
    await expect(statusBadge(page, 'Draft')).toBeVisible({ timeout: 15000 });

    // ── (2) MATERIALIZAR un ítem: pestaña «Módulos» → nodo → «Permitir» (useAddTemplateItem) ──
    await page
      .getByRole('button', { name: /^módulos$/i })
      .first()
      .click();
    const node = page.getByText(/^Declaraciones$/).first();
    await expect(node).toBeVisible({ timeout: 15000 });
    await node.click();

    const addItemRespP = page.waitForResponse(
      r =>
        r.request().method() === 'POST' &&
        /\/permission-templates\/[^/]+\/items$/.test(new URL(r.url()).pathname),
      { timeout: 20000 }
    );
    await page
      .getByRole('button', { name: /^permitir$/i })
      .first()
      .click();
    const addItemResp = await addItemRespP;
    expect(
      addItemResp.ok(),
      `POST .../items debe agregar el ítem (2xx); status=${addItemResp.status()}`
    ).toBeTruthy();
    await expect(toast(page, 'success')).toBeVisible({ timeout: 15000 });

    // ── (3) PUBLICAR: Draft→Published (usePublishPermissionTemplate) ──
    const publishRespP = page.waitForResponse(
      r =>
        r.request().method() === 'POST' &&
        /\/permission-templates\/[^/]+\/publish$/.test(new URL(r.url()).pathname),
      { timeout: 20000 }
    );
    await page.getByRole('button', { name: /^publicar$/i }).click();
    const publishResp = await publishRespP;
    expect(
      publishResp.ok(),
      `POST .../publish debe persistir (2xx); status=${publishResp.status()}`
    ).toBeTruthy();

    // Estado REAL Published: el badge del detalle pasa a Published y el gating de controles cambia —
    // aparece «Descontinuar» y desaparecen «Publicar»/«Eliminar» (sin flip optimista; anti-G-129).
    await expect(statusBadge(page, 'Published')).toBeVisible({ timeout: 15000 });
    await expect(page.getByRole('button', { name: /^descontinuar$/i })).toBeVisible({
      timeout: 15000,
    });
    await expect(page.getByRole('button', { name: /^publicar$/i })).toHaveCount(0);
    await expect(page.getByRole('button', { name: /^eliminar$/i })).toHaveCount(0);
    await expect(page.locator('[data-testid="toast"][data-toast-type="error"]')).toHaveCount(0);

    // ── (4) LIMPIEZA: descontinuar (estado terminal; el residuo Deprecated es único por versión) ──
    await page.getByRole('button', { name: /^descontinuar$/i }).click();
    await expect(toast(page, 'warning')).toBeVisible({ timeout: 15000 });
  });
});
