// Plantilla de acceso TOTAL para un rol, construida desde el catálogo de la API.
//
// Da de alta una plantilla de permisos con TODA la navegación y TODOS los objetos de dominio de una
// suite, y la publica. Es el complemento de `cargar-en-ums.mjs`: aquél aplica una spec declarativa
// completa; éste resuelve el caso puntual de «necesito una plantilla de administrador ya».
//
//   node src/provisioning/plantilla-total.mjs --suite SDLC --role ADMIN_SDLC
//   node src/provisioning/plantilla-total.mjs --suite SDLC --role ADMIN_SDLC --dry-run
//   node src/provisioning/plantilla-total.mjs --suite SDLC --limpiar --dry-run
//   node src/provisioning/plantilla-total.mjs --suite SDLC --limpiar --si-estoy-seguro
//
// Variables: UMS_URL (http://localhost:5000), UMS_USER, UMS_PASS, UMS_TENANT, UMS_PROTEGER.
//
// NO TOCA CUENTAS DE USUARIO. Su superficie son plantillas y perfiles; no invoca ningún endpoint de
// `/user-accounts`. Y nunca desactiva un perfil del usuario con el que se autenticó — ver `LIMPIAR`.

const leerArg = (nombre) => {
  const i = process.argv.indexOf(`--${nombre}`);
  return i !== -1 && process.argv[i + 1] && !process.argv[i + 1].startsWith('--') ? process.argv[i + 1] : null;
};
const bandera = (nombre) => process.argv.includes(`--${nombre}`);

const SUITE = leerArg('suite') || process.env.UMS_SUITE;
const ROL = leerArg('role') || process.env.UMS_ROLE;
const SIMULACRO = bandera('dry-run');
const LIMPIAR = bandera('limpiar');
const CONFIRMADO = bandera('si-estoy-seguro');

if (!SUITE) {
  console.error('Falta --suite CODIGO (o UMS_SUITE).');
  process.exit(2);
}
if (!LIMPIAR && !ROL) {
  console.error('Falta --role CODIGO (o UMS_ROLE). Para limpiar en su lugar, usa --limpiar.');
  process.exit(2);
}

const URL_BASE = (process.env.UMS_URL || 'http://localhost:5000').replace(/\/+$/, '');
const USUARIO = process.env.UMS_USER || 'admin@evolith.example';
const CLAVE = process.env.UMS_PASS || 'Evolith.Dev.2026';
const INQUILINO = process.env.UMS_TENANT || 'EVOLITH';

// Correos cuyos perfiles NO se tocan, además del propio usuario autenticado.
const PROTEGIDOS = new Set(
  (process.env.UMS_PROTEGER || 'admin@evolith.example')
    .split(',').map((s) => s.trim().toLowerCase()).filter(Boolean),
);

// El límite de peticiones es 1000/min en Development, 250 en UAT y 100 en Production, y la plantilla
// de un administrador de SDLC son ~410 escrituras. Sin freno, contra cualquier cosa que no sea
// Development el guion choca con 429 en el primer minuto y agota los reintentos de 61 s. Se espera
// entre peticiones en vez de reaccionar al 429: es más lento y mucho más predecible.
const PAUSA_MS = Number(process.env.UMS_PAUSA_MS ?? 250);

const cuenta = { creado: 0, existente: 0, fallo: 0 };
const log = (icono, texto) => console.log(`${icono} ${texto}`);
// En simulacro no se envía nada, así que esperar solo alargaría el diagnóstico: 407 ítems a 250 ms
// son casi dos minutos de nada. El freno existe para el límite de peticiones, y en simulacro no hay
// peticiones que limitar.
const esperar = (ms) => (SIMULACRO ? Promise.resolve() : new Promise((r) => setTimeout(r, ms)));

let cookie = '';

async function api(metodo, ruta, cuerpo, intento = 0) {
  // El corte del simulacro va aquí, el único punto por el que pasan todas las peticiones. El login
  // se exceptúa: sin sesión no se puede leer el catálogo, y leer no cambia nada.
  if (SIMULACRO && metodo !== 'GET' && !ruta.startsWith('/api/v1/auth/')) {
    return { ok: true, status: 0, datos: { simulado: true } };
  }

  const cabeceras = { 'Content-Type': 'application/json' };
  if (cookie) cabeceras.Cookie = cookie;

  const r = await fetch(`${URL_BASE}${ruta}`, {
    method: metodo,
    headers: cabeceras,
    body: cuerpo === undefined ? undefined : JSON.stringify(cuerpo),
  });

  const galletas = r.headers.getSetCookie ? r.headers.getSetCookie() : [];
  if (galletas.length) cookie = galletas.map((c) => c.split(';')[0]).join('; ');

  // 429: el servidor dice cuánto esperar; si no lo dice, 61 s (la ventana es de 60).
  if (r.status === 429 && intento < 3) {
    const espera = Number(r.headers.get('retry-after')) || 61;
    log('⏳', `429 en ${ruta}; reintento en ${espera}s`);
    await esperar(espera * 1000);
    return api(metodo, ruta, cuerpo, intento + 1);
  }

  const texto = await r.text();
  let datos = null;
  try { datos = texto ? JSON.parse(texto) : null; } catch { datos = texto; }
  return { ok: r.ok, status: r.status, datos };
}

async function listarTodo(rutaBase) {
  const todo = [];
  for (let pagina = 1; pagina <= 50; pagina += 1) {
    const sep = rutaBase.includes('?') ? '&' : '?';
    const { ok, datos } = await api('GET', `${rutaBase}${sep}page=${pagina}&pageSize=100`);
    if (!ok || !datos) break;
    const lote = Array.isArray(datos) ? datos : (datos.items ?? datos.data ?? []);
    todo.push(...lote);
    if (lote.length < 100) break;
  }
  return todo;
}

// Todo lo que toca la red vive dentro de `main()` y el codigo de salida se fija al FINAL.
// `process.exit()` con sockets keep-alive de fetch todavia abiertos aborta en Windows con
// «Assertion failed: !(handle->flags & UV_HANDLE_CLOSING)» y el codigo de salida se PIERDE:
// medido el 2026-08-15, una guarda que rechazaba la operacion devolvia 0 en vez de 2. Un guion
// cuyo codigo de salida miente no sirve para ninguna puerta automatica.
async function main() {
  // ── 1. Sesión ────────────────────────────────────────────────────────────────
  const sesion = await api('POST', '/api/v1/auth/login',
    { tenantCode: INQUILINO, username: USUARIO, password: CLAVE, rememberMe: false });

  if (!sesion.ok) {
    log('✖', `login: ${sesion.status}`);
    return 1;
  }
  const YO = sesion.datos.userId;
  const tenantId = sesion.datos.tenantId;
  log('✔', `sesión de ${USUARIO} en ${INQUILINO}${SIMULACRO ? '  [SIMULACRO: no se escribe nada]' : ''}`);

  // ── 2. Catálogo de la suite ──────────────────────────────────────────────────
  const suites = await listarTodo('/api/v1/system-suites');
  const cabecera = suites.find((s) => s.code === SUITE);
  if (!cabecera) {
    log('✖', `la suite ${SUITE} no existe. Hay: ${suites.map((s) => s.code).join(', ')}`);
    return 1;
  }
  const suiteId = cabecera.systemSuiteId ?? cabecera.id;
  const base = `/api/v1/system-suites/${suiteId}`;

  const detalle = (await api('GET', base)).datos ?? {};
  const idAccion = new Map((detalle.actions ?? []).map((a) => [a.code, a.id]));
  const recursos = detalle.domainResources ?? [];
  log('✔', `suite ${SUITE}: ${idAccion.size} acciones · ${(detalle.modules ?? []).length} módulos · ${recursos.length} objetos de dominio`);

  // ═════════════════════════════════════════════════════════════════════════════
  // LIMPIAR — retira el modelo anterior de esta suite
  // ═════════════════════════════════════════════════════════════════════════════
  if (LIMPIAR) {
    if (!SIMULACRO && !CONFIRMADO) {
      log('✖', 'La limpieza es destructiva. Ejecútala primero con --dry-run y luego con --si-estoy-seguro.');
      return 2;
    }

    const todosLosPerfiles = await listarTodo('/api/v1/profiles');

    // Se declara ANTES de tocar nada con qué cuenta el usuario que ejecuta esto, y en qué suites. Un
    // «0 protegidos» es ambiguo —¿no había ninguno, o no se miró?—, y la diferencia importa cuando lo
    // que está en juego es quedarse fuera del sistema.
    const mios = todosLosPerfiles.filter((p) => p.userId === YO);
    log('🛡', `perfiles de ${USUARIO} (el usuario que ejecuta esto): ${mios.length}`);
    for (const p of mios) {
      const dentro = p.systemSuiteCode === SUITE;
      log(' ', `    ${p.systemSuiteCode}/${p.roleCode} activo=${p.isActive} — ${dentro ? 'EN el alcance: se protege explícitamente' : `fuera del alcance (esta ejecución solo toca ${SUITE})`}`);
    }
    if (mios.length === 0) {
      log(' ', `    ninguno. Opera por ser inquilino propietario de gestión, no por perfil.`);
    }

    // Solo los perfiles DE ESTA SUITE. Las dos guardas de abajo son defensa en profundidad sobre este
    // filtro, no un sustituto.
    const perfiles = todosLosPerfiles.filter((p) => p.systemSuiteCode === SUITE);

    const protegidos = [];
    const aDesactivar = [];
    for (const p of perfiles) {
      const correo = (p.userEmail || '').toLowerCase();
      // (a) NUNCA un perfil del usuario que está ejecutando esto. No es configurable ni se puede
      //     desactivar con una bandera: es la garantía de no quedarse fuera del sistema uno mismo.
      // (b) Ningún correo de la lista de protección.
      // El DTO devuelve «—» cuando no resuelve el correo, así que (a) no puede depender de (b).
      if (p.userId === YO) { protegidos.push({ ...p, motivo: 'es el usuario que ejecuta el guion' }); continue; }
      if (correo && PROTEGIDOS.has(correo)) { protegidos.push({ ...p, motivo: 'está en la lista de protección' }); continue; }
      if (p.isActive === false) continue;
      aDesactivar.push(p);
    }

    for (const p of protegidos) log('🛡', `perfil PROTEGIDO ${p.roleCode} (${p.userEmail}): ${p.motivo}`);
    log('•', `perfiles de ${SUITE}: ${perfiles.length} · a desactivar ${aDesactivar.length} · protegidos ${protegidos.length}`);

    for (const p of aDesactivar) {
      const r = await api('POST', `/api/v1/profiles/${p.profileId}/deactivate`, {});
      if (r.ok) { cuenta.creado += 1; log('✔', `perfil ${p.roleCode} desactivado`); }
      else { cuenta.fallo += 1; log('✖', `perfil ${p.roleCode}: ${r.status} ${JSON.stringify(r.datos).slice(0, 120)}`); }
      await esperar(PAUSA_MS);
    }

    const plantillas = (await listarTodo('/api/v1/permission-templates'))
      .filter((t) => t.systemSuiteId === suiteId);
    log('•', `plantillas de ${SUITE}: ${plantillas.length}`);

    for (const t of plantillas) {
      // No se puede borrar desde Published: primero se depreca. Y `activeProfileCount > 0` bloquea el
      // borrado, que es justo lo que pasará si un perfil PROTEGIDO la usa — en ese caso se dice y se
      // sigue, sin forzar nada y sin desproteger a nadie para desatascarse.
      if (t.status === 'Published') {
        const dep = await api('POST', `/api/v1/permission-templates/${t.templateId}/deprecate`, {});
        if (!dep.ok) { cuenta.fallo += 1; log('✖', `deprecar ${t.roleName}: ${dep.status}`); continue; }
        await esperar(PAUSA_MS);
      }
      const del = await api('DELETE', `/api/v1/permission-templates/${t.templateId}`);
      if (del.ok) { cuenta.creado += 1; log('✔', `plantilla ${t.roleName} v${t.version} retirada`); }
      else {
        cuenta.fallo += 1;
        log('✖', `plantilla ${t.roleName} v${t.version}: ${del.status} ${JSON.stringify(del.datos).slice(0, 160)}`);
        log(' ', '  si es por perfiles activos, revisa la lista de PROTEGIDOS de arriba: no se desprotegen solos.');
      }
      await esperar(PAUSA_MS);
    }

    log(SIMULACRO ? '•' : '✔', `${SIMULACRO ? '[simulacro] ' : ''}limpieza de ${SUITE}: ${cuenta.creado} operaciones, ${cuenta.fallo} fallos`);
    log('•', 'El borrado es LÓGICO: las filas retiradas conservan su número de versión, así que las');
    log(' ', 'plantillas nuevas nacerán en 0.2.0, no en 0.1.0.');
    return cuenta.fallo > 0 ? 1 : 0;
  }

  // ═════════════════════════════════════════════════════════════════════════════
  // CONSTRUIR — plantilla de acceso total para --role
  // ═════════════════════════════════════════════════════════════════════════════
  const roles = await listarTodo(`${base}/roles`);
  const rol = roles.find((r) => r.code === ROL);
  if (!rol) {
    log('✖', `el rol ${ROL} no existe en ${SUITE}. Hay: ${roles.map((r) => r.code).join(', ')}`);
    return 1;
  }

  // ── Ítems ────────────────────────────────────────────────────────────────────
  // El mapeo de destino NO es 1:1 con `NodeKind`: un nodo `Menu` se registra como `Submodule`, y tanto
  // `SubMenu` como `Option` van como `Option`. Verificado contra el sembrador canónico
  // (AuthorizationDevDataSeeder) y contra `ExclusiveArcTarget`.
  const items = [];
  const visto = new Set();
  const anota = (targetType, targetId, actionId) => {
    const clave = `${targetType}|${targetId}|${actionId}`;
    if (visto.has(clave)) return;
    visto.add(clave);
    items.push({ targetType, targetId, actionId });
  };

  const ACCION_NAV = idAccion.get('VIEW') ?? [...idAccion.values()][0];
  if (!idAccion.has('VIEW')) {
    log('•', `la suite no declara la acción VIEW; las filas de navegación usarán ${[...idAccion.keys()][0]}.`);
  }

  anota('SystemSuite', suiteId, ACCION_NAV);

  const recorrer = (nodo) => {
    if (nodo.kind === 'Option') {
      // Un `actionId` inventado no concede nada: el grafo casa por el par (destino, acción) y una
      // acción que no existe resuelve `NotGranted` sin error visible (G-038). Solo se emiten las
      // acciones REALMENTE vinculadas al nodo.
      for (const codigo of nodo.actionCodes ?? []) {
        const actionId = idAccion.get(codigo);
        if (!actionId) { log('•', `acción ${codigo} de ${nodo.code} no existe en la suite; se omite`); continue; }
        anota('Option', nodo.id, actionId);
      }
    } else {
      anota(nodo.kind === 'Menu' ? 'Submodule' : 'Option', nodo.id, ACCION_NAV);
    }
    for (const hijo of nodo.children ?? []) recorrer(hijo);
  };

  for (const modulo of detalle.modules ?? []) {
    anota('Module', modulo.id, ACCION_NAV);
    for (const nodo of modulo.nodes ?? []) recorrer(nodo);
  }

  // `DomainResourceType` tiene tres valores y `ExclusiveArcTarget` solo dos: un recurso `DomainMethod`
  // no tiene destino válido. Se avisa en vez de mandarlo como otra cosa — el grafo casa solo
  // (TargetId, ActionId), así que un tipo mentiroso colaría y dejaría la tabla afirmando algo falso.
  let dominio = 0;
  for (const r of recursos) {
    if (r.type !== 'Aggregate' && r.type !== 'Entity') {
      log('•', `objeto de dominio ${r.code} es de tipo ${r.type}: sin destino en ExclusiveArcTarget, se omite`);
      continue;
    }
    for (const actionId of idAccion.values()) { anota(r.type, r.id, actionId); dominio += 1; }
  }

  const navegacion = items.length - dominio;
  log('•', `plantilla de ${ROL}: ${items.length} ítems (${navegacion} de navegación y acción, ${dominio} de dominio)`);

  const minutos = ((items.length + 3) * PAUSA_MS) / 60000;
  log('•', `a ${PAUSA_MS} ms por petición son ~${minutos.toFixed(1)} min. Ajusta con UMS_PAUSA_MS.`);

  // ── Alta ─────────────────────────────────────────────────────────────────────
  // `POST /permission-templates` SIEMPRE crea la versión siguiente para la terna (inquilino, rol,
  // suite): no hace falta un endpoint especial, y por eso una plantilla ya publicada no bloquea —
  // se crea una v+1 en Draft, se llena y se publica.
  const alta = await api('POST', '/api/v1/permission-templates',
    { tenantId, roleId: rol.roleId, systemSuiteId: suiteId });

  if (!alta.ok) {
    log('✖', `crear plantilla: ${alta.status} ${JSON.stringify(alta.datos).slice(0, 200)}`);
    return 1;
  }
  const templateId = SIMULACRO ? '(simulado)' : alta.datos.templateId;
  log('✔', `plantilla creada para ${ROL}: ${templateId}`);

  let puestos = 0;
  for (const [i, it] of items.entries()) {
    const r = await api('POST', `/api/v1/permission-templates/${templateId}/items`,
      { templateId, targetType: it.targetType, targetId: it.targetId, actionId: it.actionId, isAllowed: true, isDenied: false });

    if (r.ok) { puestos += 1; cuenta.creado += 1; }
    else if (r.status === 409 || /duplicad|already|existe/i.test(JSON.stringify(r.datos))) cuenta.existente += 1;
    else { cuenta.fallo += 1; log('✖', `ítem ${it.targetType}: ${r.status} ${JSON.stringify(r.datos).slice(0, 140)}`); }

    if ((i + 1) % 50 === 0) log('•', `  ${i + 1}/${items.length}`);
    await esperar(PAUSA_MS);
  }
  log('✔', `${puestos} ítems añadidos (${cuenta.existente} ya estaban, ${cuenta.fallo} fallos)`);

  // Publicar exige al menos un ítem VIGENTE; si todos fallaron, publicar daría un error opaco y es
  // preferible decir por qué no se publica.
  if (cuenta.fallo > 0 && puestos === 0) {
    log('✖', 'no se añadió ningún ítem: no se publica. Revisa los errores de arriba.');
    return 1;
  }

  const pub = await api('POST', `/api/v1/permission-templates/${templateId}/publish`, {});
  if (!pub.ok) { log('✖', `publicar: ${pub.status} ${JSON.stringify(pub.datos).slice(0, 200)}`); return 1; }

  log('✔', `plantilla de ${ROL} publicada${SIMULACRO ? ' [simulacro]' : ''}`);
  log('•', 'Queda CONGELADA: para cambiarla hay que crear una versión nueva. Asigna el perfil con');
  log(' ', `  POST /api/v1/profiles/{perfilId}/templates/${templateId}`);
  return cuenta.fallo > 0 ? 1 : 0;
}

process.exitCode = await main();
