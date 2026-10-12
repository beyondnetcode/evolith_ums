import { test, expect, type Page } from '@playwright/test';

/**
 * Perfiles de Autorización (Profile) — Matriz de Cobertura de UI (G-130).
 *
 * Pantalla: ProfileDashboardScreen (ruta `/profiles`, riel «Perfiles de Autorización»).
 * Compone tres zonas:
 *   - ProfileRequestsInboxPanel  → Aprobar / Rechazar solicitudes de perfil.
 *   - ProfileListPanel (master)  → lista de perfiles + «Agregar» (Crear).
 *   - ProfileDetailPanel (detail)→ toggle «Activar/Desactivar Perfil» + overrides de permiso.
 *
 * Patrón G-129: cuando el backend/validación RECHAZA una acción, el control NO debe cambiar
 * de forma optimista y debe surgir el error real. Selección robusta vía data-testid
 * compartidos (entity-row, status-badge, toast, list-search).
 *
 * Datos: el tenant del admin (EVOLITH) trae perfiles sembrados
 * (AuthorizationDevDataSeeder → BuildEvolithProfiles), por lo que la lista carga al entrar.
 */
const ADMIN = { email: 'admin@evolith.example', password: 'Evolith.Dev.2026' };

const ACTIVAR = /^activar perfil$/i;
const DESACTIVAR = /^desactivar perfil$/i;

async function gotoProfiles(page: Page): Promise<void> {
  await page
    .getByTestId('nav-rail')
    .getByRole('button', { name: /(authorization profiles|perfiles de autorización)/i })
    .first()
    .click();
  await expect(page).toHaveURL(/\/profiles/);
}

test.describe('Profile · Perfiles de Autorización (happy + error) — G-130', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
    await page.getByLabel(/correo electrónico/i).fill(ADMIN.email);
    await page.getByLabel(/contraseña/i).fill(ADMIN.password);
    await page.getByRole('button', { name: /ingresar/i }).click();
    await expect(page).toHaveURL(/\/tenants/);
    await gotoProfiles(page);
  });

  // ── ACTIVAR / DESACTIVAR ─────────────────────────────────────────────────────
  // HAPPY: el toggle del panel de detalle refleja el estado REAL (no un flip optimista).
  // Es adaptativo al estado inicial del primer perfil sembrado y RESTAURA el estado al final
  // (higiene sobre datos compartidos del clúster). No existe un error-path forzable desde
  // este botón: la cabecera muestra siempre la transición válida para el estado vigente
  // (Activate rechaza si ya está activo; Deactivate si ya está inactivo — Profile.cs), de modo
  // que el botón nunca ofrece la acción que el dominio rechazaría (a diferencia de Tenant/G-129).
  test('HAPPY · desactivar y reactivar un perfil sembrado refleja el estado real', async ({
    page,
  }) => {
    const firstRow = page.locator('[data-testid="entity-row"]').first();
    await expect(firstRow).toBeVisible({ timeout: 15000 });
    await firstRow.click();

    // La cabecera del detalle expone el toggle de estado del perfil seleccionado.
    const toggle = page.getByRole('button', { name: /^(activar|desactivar) perfil$/i });
    await expect(toggle).toBeVisible({ timeout: 15000 });
    const wasActive = /desactivar/i.test((await toggle.textContent()) ?? '');

    const oppositeRe = wasActive ? ACTIVAR : DESACTIVAR;
    const originalRe = wasActive ? DESACTIVAR : ACTIVAR;

    // (1) Primer toggle → el servidor confirma y el botón FLIPA al estado opuesto (estado real).
    await toggle.click();
    await expect(page.getByRole('button', { name: oppositeRe })).toBeVisible({ timeout: 15000 });
    // (2) Salió una confirmación (desactivar=warning / activar=success), NUNCA un error.
    await expect(
      page.locator('[data-testid="toast"]:not([data-toast-type="error"])').first()
    ).toBeVisible({ timeout: 15000 });
    await expect(page.locator('[data-testid="toast"][data-toast-type="error"]')).toHaveCount(0);

    // (3) Restaurar el estado original: nuevo éxito real → el botón vuelve a su etiqueta inicial.
    await page.getByRole('button', { name: oppositeRe }).click();
    await expect(page.getByRole('button', { name: originalRe })).toBeVisible({ timeout: 15000 });
  });

  // ── CREAR ────────────────────────────────────────────────────────────────────
  // ERROR forzable desde botón visible (patrón G-129): «Crear & Persistir» sin usuario
  // seleccionado dispara la validación de cliente y el diálogo NO se cierra ni persiste.
  // El happy-path de creación NO es alcanzable de forma determinista: exige elegir usuario +
  // suite + rol + una plantilla PUBLICADA existente para esa combinación (cadena de datos que
  // este test no puede garantizar). Ver reporte.
  test('ERROR · crear perfil sin usuario dispara validación de cliente y no persiste (G-129)', async ({
    page,
  }) => {
    await page.locator('button[title="Agregar"]').first().click();

    const dialog = page.getByRole('dialog');
    await expect(dialog.getByText(/nuevo perfil de autorización/i)).toBeVisible({ timeout: 15000 });

    // Acción rechazable desde botón visible: persistir sin haber seleccionado un usuario.
    await dialog.getByRole('button', { name: /crear & persistir/i }).click();

    // Validación de cliente: banner de error dentro del diálogo (ProfileForm.handleSave).
    await expect(
      dialog.getByText(/usuario es requerido|tenant context no disponible/i)
    ).toBeVisible({ timeout: 10000 });

    // No hubo persistencia: ningún toast de éxito de creación de perfil.
    await expect(page.locator('[data-testid="toast"][data-toast-type="success"]')).toHaveCount(0);
    // El rechazo NO cerró el diálogo (no hay avance optimista).
    await expect(dialog.getByText(/nuevo perfil de autorización/i)).toBeVisible();
  });

  // ── APROBAR / RECHAZAR SOLICITUD DE PERFIL ───────────────────────────────────
  // HAPPY (G-151): el inbox de Perfiles rechaza una solicitud de perfil Pending.
  //
  // Determinismo e idempotencia (desbloqueo del antiguo FIXME): el test SIEMBRA su propia
  // solicitud Pending vía API en el setup y luego la rechaza por UI, sin mutar datos ajenos.
  //   · Alta por API: POST /api/v1/approval-requests. En Development, DevAuthMiddleware acepta la
  //     identidad por cabeceras (X-Tenant-Id=EVOLITH, X-User-Id=admin), igual que los tests de
  //     integración (ApprovalRequestRestEndpointTests). El backend no exige CSRF en este flujo.
  //   · Sobre el workflow ya sembrado para EVOLITH ALTA_CLIENTE (RequiresApproval=true → la solicitud
  //     nace y permanece Pending). GetPendingProfileRequestsQuery filtra por el tenant del workflow,
  //     así que el admin de EVOLITH SÍ la ve.
  //   · Identificación inequívoca: se elige un usuario objetivo EVOLITH real cuyo shortId (8 primeros
  //     caracteres, como los pinta la tarjeta «Usuario XXXXXXXX») NO colisione con ninguna Pending
  //     residual del clúster. system/role son GUIDs aleatorios → nunca chocan con el scope de otra
  //     Pending (evita el 409 de duplicado), y la validación sólo exige que sean no vacíos.
  //   · La solicitud recién creada es la más reciente y el inbox ordena por fecha desc → aparece en la
  //     primera página. Tras Rechazar (mutación real → «Solicitud rechazada»), su tarjeta sale del
  //     inbox. Anti-G-129: se exige el toast real y la desaparición de la solicitud, sin falso éxito.
  test('HAPPY · rechazar una solicitud de perfil pendiente emite confirmación (G-151)', async ({
    page,
    request,
  }) => {
    const API = '/api/v1';
    const EVOLITH_TENANT = '5f4e3d2c-1b0a-9f8e-7d6c-543210987654'; // Tenant Raíz EVOLITH (ADR-0071)
    const ADMIN_USER_ID = '5f4e3d14-1b0a-9f8e-7d6c-543210987654'; // admin@evolith.example (actor)
    const devHeaders = {
      'X-Tenant-Id': EVOLITH_TENANT,
      'X-User-Id': ADMIN_USER_ID,
      'Content-Type': 'application/json',
    };
    const shortId = (id: string): string => id.slice(0, 8).toUpperCase();

    // (setup 1) Workflow EVOLITH sembrado ALTA_CLIENTE (RequiresApproval → la solicitud queda Pending).
    const wfRes = await request.get(`${API}/approval-workflows?page=1&pageSize=100`, {
      headers: devHeaders,
    });
    expect(wfRes.ok(), 'GET /approval-workflows responde 2xx').toBeTruthy();
    const workflow = (await wfRes.json()).items.find(
      (w: { code: string }) => w.code === 'ALTA_CLIENTE'
    );
    expect(workflow, 'workflow ALTA_CLIENTE sembrado para EVOLITH').toBeTruthy();

    // (setup 2) shortIds de las Pending ya presentes, para escoger un objetivo que no colisione.
    const inboxRes = await request.get(`${API}/onboarding/inbox/profile-requests`, {
      headers: devHeaders,
    });
    const existing = (await inboxRes.json()) as Array<{ targetUserId: string }>;
    const taken = new Set(existing.map(r => shortId(r.targetUserId)));

    // (setup 3) Un usuario EVOLITH real cuyo shortId sea único en el inbox → tarjeta localizable.
    const usersRes = await request.get(`${API}/user-accounts?page=1&pageSize=200`, {
      headers: devHeaders,
    });
    const users = (await usersRes.json()).items as Array<{ userAccountId: string; email: string }>;
    const target = users.find(
      u => u.email.endsWith('@evolith.example') && !taken.has(shortId(u.userAccountId))
    );
    expect(target, 'existe un usuario EVOLITH con shortId único disponible').toBeTruthy();
    const targetShort = shortId(target!.userAccountId);

    // (setup 4) Crear la solicitud Pending. system/role aleatorios ⇒ sin colisión de scope (no 409).
    const token = `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
    const createRes = await request.post(`${API}/approval-requests`, {
      headers: devHeaders,
      data: {
        workflowId: workflow.approvalWorkflowId,
        targetUserId: target!.userAccountId,
        targetProfileId: null,
        requestedSystemId: crypto.randomUUID(),
        requestedBranchId: null,
        requestedRoleId: crypto.randomUUID(),
        justification: `E2E G-151 rechazo ${token}`,
      },
    });
    expect(createRes.status(), 'la solicitud Pending se crea (201)').toBe(201);

    // Recargar el dashboard para que el inbox recoja la solicitud recién sembrada.
    await page.reload();
    await expect(page).toHaveURL(/\/profiles/);

    // La tarjeta de MI solicitud («Usuario <shortId>») aparece; hay al menos un badge Pending.
    const card = page.getByText(new RegExp(`Usuario ${targetShort}`, 'i')).first();
    await expect(card).toBeVisible({ timeout: 20000 });
    await expect(
      page.locator('[data-testid="status-badge"][data-status="Pending"]').first()
    ).toBeVisible({ timeout: 15000 });

    // Expandir la tarjeta (vista thumbnail) revela las acciones; Rechazar dispara la mutación real.
    await card.click();
    await page.getByRole('button', { name: /rechazar/i }).first().click();

    // Confirmación real (useRejectProfileRequest → «Solicitud rechazada»), nunca un error.
    await expect(page.getByText(/solicitud rechazada/i).first()).toBeVisible({ timeout: 15000 });
    await expect(page.locator('[data-testid="toast"][data-toast-type="error"]')).toHaveCount(0);

    // La solicitud sale de Pending: su tarjeta ya no está en el inbox (sin falso éxito, G-129).
    await expect(page.getByText(new RegExp(`Usuario ${targetShort}`, 'i'))).toHaveCount(0, {
      timeout: 15000,
    });
  });

  // ── OVERRIDE DE PERMISO (solo happy) ─────────────────────────────────────────
  // Profile.OverridePermission{Allow,Deny,Neutral} NUNCA rechaza (no hay error-path), y eso es lo
  // único que esta prueba debe demostrar.
  //
  // Idempotencia: cada override PERSISTE sobre un perfil SEMBRADO y compartido. Cambiar el efecto
  // dispararía la cascada que auto-permite los ancestros, dejando residuo no restaurable. Por eso
  // se REAFIRMA el efecto VIGENTE: reafirmar no cambia el permiso ni gatilla la cascada.
  //
  // POR QUÉ SE LEE EL EFECTO EN VEZ DE DARLO POR SABIDO. Antes esta prueba exigía que el primer
  // nodo estuviera en «Allow», tratándolo como determinista, y fallaba cada vez que cambiaba algo
  // de los datos sembrados: qué perfil sale primero, qué plantilla se le materializó, qué nodo
  // encabeza el árbol. Ninguna de esas cosas es el producto — es el seeder. La prueba acusaba al
  // sistema de un fallo que no tenía (evolith-ums#G-245).
  //
  // Ahora lee cuál de los tres está activo y reafirma ESE. La propiedad que verifica es la misma y
  // deja de depender de con qué se sembró la base esta mañana.
  test('HAPPY · reafirmar el efecto vigente de un permiso siempre tiene éxito', async ({ page }) => {
    // Se BUSCA un perfil con permisos materializados en vez de confiar en el primero de la lista.
    //
    // El primero no sirve: depende de qué sembró la base y de cómo ordena el listado. En la corrida
    // del 2026-08-04 encabezaba un perfil de la suite SCM recién sembrada, sin plantilla asociada,
    // y el panel decía «Ningún permiso materializado» — no había botones que pulsar. La prueba
    // acusaba al producto de un fallo que estaba en los datos (evolith-ums#G-245).
    const filas = page.locator('[data-testid="entity-row"]');
    await expect(filas.first()).toBeVisible({ timeout: 15000 });

    const total = await filas.count();
    let encontrado = false;

    for (let i = 0; i < total && !encontrado; i += 1) {
      await filas.nth(i).click();
      await page.getByRole('button', { name: /permisos efectivos/i }).click();

      // El árbol tarda en pintarse; el panel vacío aparece de inmediato. Se espera a que gane uno
      // de los dos antes de decidir, o se descartarían perfiles buenos por ir demasiado rápido.
      const botonAllow = page.getByRole('button', { name: /^allow$/i }).first();
      const panelVacio = page.getByText(/ningún permiso materializado/i).first();
      await expect(botonAllow.or(panelVacio)).toBeVisible({ timeout: 15000 });

      encontrado = await botonAllow.isVisible();
    }

    expect(
      encontrado,
      'Ningún perfil del inquilino tiene permisos materializados: la siembra no permite ejercer ' +
        'la anulación de permisos, y sin eso esta prueba no puede afirmar nada.'
    ).toBe(true);

    // El efecto activo se distingue por su fondo sólido: allow=emerald, deny=rose, neutral=outline.
    const efectos = [
      { nombre: /^allow$/i, activo: /bg-emerald-500/ },
      { nombre: /^deny$/i, activo: /bg-rose-500/ },
      { nombre: /^neutral$/i, activo: /bg-m3-outline\/30/ },
    ];

    let vigente: (typeof efectos)[number] | null = null;
    for (const efecto of efectos) {
      const clases = (await page.getByRole('button', { name: efecto.nombre }).first().getAttribute('class')) ?? '';
      if (efecto.activo.test(clases)) {
        vigente = efecto;
        break;
      }
    }

    expect(
      vigente,
      'El primer nodo de permiso debe tener alguno de los tres efectos marcado como vigente.'
    ).not.toBeNull();

    // Se REAFIRMA el efecto vigente, no se cambia: el perfil está sembrado y es compartido, y
    // voltear el efecto dispararía la cascada que auto-permite los ancestros, dejando residuo que
    // no se puede restaurar de forma limpia. Reafirmar prueba lo mismo —que la anulación NUNCA
    // rechaza— sin ensuciar datos de los que dependen otras pruebas.
    await page.getByRole('button', { name: vigente!.nombre }).first().click();
    await expect(
      page.locator('[data-testid="toast"]:not([data-toast-type="error"])').first()
    ).toBeVisible({ timeout: 15000 });
    await expect(page.locator('[data-testid="toast"][data-toast-type="error"]')).toHaveCount(0);

    // El efecto NO cambió: sigue vigente el mismo. Refleja el estado REAL.
    await expect(page.getByRole('button', { name: vigente!.nombre }).first()).toHaveClass(
      vigente!.activo,
      { timeout: 15000 }
    );
  });
});
