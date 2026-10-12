// Carga la especificación de UN SISTEMA en UMS **por API**, no por seeder.
//
// Va por la API a propósito: es el mismo camino que recorrería cualquier satélite al darse de alta,
// así que lo que aquí funcione demuestra que la capacidad existe de verdad y no solo dentro del
// proceso de siembra. Es el guion artesanal que G-166 quiere convertir en una capacidad de UMS.
//
// Idempotente por construcción: si la suite ya existe, no la duplica; si un módulo o un nodo ya está,
// lo salta. Se puede volver a ejecutar sin dejar el sistema a medias.
//
//   node src/provisioning/cargar-en-ums.mjs --spec src/provisioning/ums/ums-suite.json
//   node src/provisioning/cargar-en-ums.mjs --spec src/provisioning/sdlc/sdlc-suite.json --dry-run
//
// Para que una CORRECCIÓN de la spec llegue a una instancia donde las plantillas ya están
// publicadas, hace falta una versión nueva: este cargador solo añade concesiones, nunca las quita.
//
//   node src/provisioning/cargar-en-ums.mjs --spec .../sdlc-suite.json --nueva-version
//
// El PADRÓN —las personas que entran, con su rol y su sucursal— se declara en `profiles` de la spec.
// Dos banderas lo gobiernan, y son distintas porque el riesgo lo es:
//
//   --crear-cuentas   da de alta a quien falte, la activa y le pone su PRIMERA contraseña.
//   --fijar-claves    ROTA la contraseña de quien YA existía. Eso sí pisa la que la persona tenga.
//
//   node src/provisioning/cargar-en-ums.mjs --spec .../sdlc-suite.json --crear-cuentas --fijar-claves
//
// Al terminar imprime una tabla con usuario, contraseña y si la cuenta era nueva o ya estaba.
//
// Variables: UMS_URL (http://localhost:5000), UMS_USER, UMS_PASS, UMS_TENANT, UMS_SPEC,
//            UMS_PAUSA_MS (freno entre peticiones; 0 por omisión),
//            UMS_PADRON_CLAVE_PATRON (patrón de contraseña del padrón; `Evolith.{Nombre}.2026!`).
//
// SOBRE `UMS_PAUSA_MS`, con un número y no con una intuición: UMS limita a 100 peticiones por
// minuto (`ApiSettings:RateLimiting`), así que el freno tiene que ser de al menos **600 ms** para
// no rebasarlo — 700 deja margen. Con 250 ms se envían 240 por minuto, y el resultado no es un
// error limpio: el guion reintenta tres veces por petición, agota los reintentos, DA POR FALLIDA
// esa carga y sigue. Termina dejando plantillas con los ítems a medias y sin publicar. Ocurrió el
// 2026-08-16 con la suite SDLC: 85 de 186 ítems en una versión y 33 en otra.

import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';

/** Versión MAYOR del contrato de spec que este cargador sabe aplicar. */
const SPEC_MAJOR_SOPORTADA = 1;

function leerArgumento(nombre) {
  const i = process.argv.indexOf(`--${nombre}`);
  return i !== -1 && process.argv[i + 1] ? process.argv[i + 1] : null;
}

const RUTA_SPEC = leerArgumento('spec') || process.env.UMS_SPEC;
if (!RUTA_SPEC) {
  console.error('Falta la especificación. Usa: --spec ruta/al/sistema-suite.json (o UMS_SPEC).');
  process.exit(2);
}

const SPEC = JSON.parse(readFileSync(resolve(process.cwd(), RUTA_SPEC), 'utf8'));

// Se comprueba la MAYOR y se aborta si no coincide. Una spec 2.x aplicada por un cargador 1.x
// no fallaría al principio sino a la mitad, dejando el sistema con parte de su navegación puesta
// y parte no — el estado más caro de diagnosticar.
const MAYOR_SPEC = Number.parseInt(String(SPEC.specVersion ?? '0').split('.')[0], 10);
if (MAYOR_SPEC !== SPEC_MAJOR_SOPORTADA) {
  console.error(
    `specVersion ${SPEC.specVersion ?? '(ausente)'} incompatible: este cargador aplica la mayor ${SPEC_MAJOR_SOPORTADA}.x`,
  );
  process.exit(2);
}

// `--dry-run` no escribe nada: sirve para contestar «¿en qué difiere esta instancia de la spec?».
// Es además el verificador de la transcripción del sembrador — contra una base ya sembrada debe
// reportar CERO creaciones.
const SIMULACRO = process.argv.includes('--dry-run');

// Las dos banderas del PADRÓN se declaran aquí arriba, con el resto, y no junto al paso que las
// usa: el simulacro necesita saber si la carga real crearía las cuentas para poder decirlo, y
// declararlas más abajo las dejaba fuera de su alcance.
//
// Son distintas porque el riesgo lo es. `--crear-cuentas` da de alta a quien falte, la activa y le
// pone su PRIMERA contraseña: no pisa nada. `--fijar-claves` ROTA la de quien ya existía, y esa sí
// deja fuera de su cuenta a una persona que no lo pidió.
const CREAR_CUENTAS = process.argv.includes('--crear-cuentas');
const FIJAR_CLAVES = process.argv.includes('--fijar-claves');

// `--nueva-version` fabrica una VERSIÓN NUEVA de cada plantilla en vez de reutilizar la que ya
// existe. Es la única forma de que una CORRECCIÓN de la spec llegue al sistema vivo.
//
// POR QUÉ HACE FALTA UNA BANDERA Y NO ES EL COMPORTAMIENTO NORMAL. Este cargador solo AÑADE
// concesiones: los ítems únicamente se admiten en `Draft` y la reentrada se apoya en el rechazo por
// duplicado. Así que quitar un `grant` de la spec y volver a correrlo no quita nada — la plantilla
// publicada sigue con lo que tenía, y la ejecución termina en verde diciendo «ya existía». Es un
// silencio caro: quien corrige la spec cree que corrigió el sistema.
//
// Y no puede ser el comportamiento por omisión porque `POST /permission-templates` asigna siempre
// la versión siguiente (CreateNextVersion, G-140): sin la bandera, cada reejecución rutinaria
// fabricaría una versión más. Ese fue exactamente el defecto que dejó 17 plantillas donde iban 8.
//
// QUÉ HACE, EN ORDEN: crea la versión nueva en `Draft`, le carga las concesiones de la spec, la
// publica, la asigna a los perfiles y solo entonces DEPRECA la anterior. La anterior NO se borra:
// puede estar asignada, y borrarla rompería el perfil que la use.
//
// SOBRE LA REMATERIALIZACIÓN, que durante un tiempo fue una promesa falsa (G-272). Asignar la
// versión nueva SUMABA sus permisos a los de la vieja —`AssignTemplate` hacía `_permissions.Add(...)`
// y no existía operación inversa—, así que esta bandera corrompía el modelo en vez de corregirlo.
// Medido en UAT el 2026-08-17 sobre perfiles que ya tenían la v1: 94→188, 124→248, 407→528.
//
// Corregido por ADR-UMS-106: asignar una plantilla SUPERSEDE a las de su misma línea —misma terna
// (inquilino, rol, suite)—, desligándolas primero; las de otras líneas se conservan. El perfil queda
// con una sola versión viva por línea, que es lo que esta bandera siempre dijo hacer. Y desligar es
// ahora una operación del modelo: `DELETE /api/v1/profiles/{profileId}/templates/{templateId}`.
//
// Exige un UMS con ese cambio desplegado. Contra uno anterior, el comportamiento vuelve a ser el
// aditivo descrito arriba.
const NUEVA_VERSION = process.argv.includes('--nueva-version');

const URL_BASE = (process.env.UMS_URL || 'http://localhost:5000').replace(/\/+$/, '');
const USUARIO = process.env.UMS_USER || 'admin@evolith.example';
const CLAVE = process.env.UMS_PASS || 'Evolith.Dev.2026';
const TENANT = process.env.UMS_TENANT || SPEC.tenant.code;

let cookie = null;
const cuenta = { creado: 0, existente: 0, fallo: 0 };

const log = (icono, texto) => console.log(`${icono} ${texto}`);

const esperar = (ms) => new Promise((r) => setTimeout(r, ms));

/**
 * Freno PROACTIVO entre peticiones, en milisegundos (`UMS_PAUSA_MS`).
 *
 * El reintento ante 429 de más abajo es REACTIVO: llega tarde y cuesta 61 s cada vez. Contra un
 * entorno con cuota estrecha eso no es una molestia sino un fallo — la carga completa ronda las
 * 1100 peticiones y UAT concede 250 por minuto, así que el guion dispara la cuota entera en
 * segundos, se come un 429 tras otro y puede agotar los 3 reintentos antes de terminar.
 *
 * Por omisión NO frena: en Development la cuota es 1000/min y la carga cabe holgada, y añadir
 * medio segundo por petición allí solo alargaría el ciclo de trabajo. Contra UAT o producción se
 * fija a mano —250 ms es un ritmo que cabe en cualquiera de las dos cuotas— y entonces la carga
 * tarda lo que tiene que tardar en vez de pelearse con el limitador.
 */
const PAUSA_MS = Number(process.env.UMS_PAUSA_MS ?? 0);

/**
 * Cliente HTTP con reintento ante 429.
 *
 * La carga completa ronda las 1100 peticiones y el limitador de UMS concede 1000 por ventana de un
 * minuto (ApiSettings:RateLimiting en Development, 250 en UAT, 100 en el resto). Una segunda pasada
 * dentro de la misma ventana agota la cuota y devuelve 429 en masa: sin esto, el guion los contaba
 * como fallos de carga y mentía sobre qué se había cargado. Se espera lo que la propia respuesta
 * pide, y con `UMS_PAUSA_MS` se evita llegar a ese punto.
 */
async function api(metodo, ruta, cuerpo, intento = 0) {
  // En simulacro solo se leen catálogos: cualquier verbo mutante se contesta con un éxito
  // sintético. El corte va aquí, en el único punto por el que pasan TODAS las peticiones, para
  // que no exista ningún camino de escritura que se salte la simulación.
  //
  // `/auth/` queda FUERA del corte: es un POST, pero no aprovisiona nada — establece la sesión y
  // devuelve el `tenantId` del que cuelga todo lo demás. Simularlo dejaba al guion sin cookie y
  // sin inquilino, y abortaba en el paso 2 diciendo «sin suite no hay nada más que cargar».
  const esAutenticacion = ruta.startsWith('/api/v1/auth/');
  if (SIMULACRO && metodo !== 'GET' && !esAutenticacion) {
    return { ok: true, status: 200, datos: {}, simulado: true };
  }

  // Antes de CADA petición, incluidos los GET: el limitador cuenta todas. En simulacro no se frena
  // porque no se envía casi nada, y esperar solo alargaría el diagnóstico.
  if (PAUSA_MS > 0 && !SIMULACRO && intento === 0) await esperar(PAUSA_MS);

  const res = await fetch(`${URL_BASE}${ruta}`, {
    method: metodo,
    headers: {
      ...(cuerpo ? { 'Content-Type': 'application/json' } : {}),
      ...(cookie ? { Cookie: cookie } : {}),
      Accept: 'application/json',
    },
    body: cuerpo ? JSON.stringify(cuerpo) : undefined,
  });

  const puesta = res.headers.get('set-cookie');
  if (puesta) cookie = puesta.split(';')[0];

  const datos = await res.json().catch(() => ({}));

  if (res.status === 429 && intento < 3) {
    const cabecera = Number(res.headers.get('retry-after'));
    const segundos = Number.isFinite(cabecera) && cabecera > 0 ? cabecera : 61;
    log('⏳', `límite de peticiones alcanzado; se reanuda en ${segundos}s (intento ${intento + 1}/3)`);
    await esperar(segundos * 1000);
    return api(metodo, ruta, cuerpo, intento + 1);
  }

  return { ok: res.ok, status: res.status, datos };
}

/**
 * Lee un catálogo COMPLETO, recorriendo sus páginas.
 *
 * `pageSize` es una petición, no una orden: el endpoint la recorta a 100. Pedir 200 o 500 devuelve
 * 100 y el guion se creía que eso era todo. Con 254 suites en la instancia, `SDLC` caía en la
 * página 3 y NO aparecía: el guion la daba por inexistente, intentaba crearla, recibía el conflicto
 * y abortaba con «sin suite no hay nada más que cargar» sobre una instancia que SÍ la tenía.
 *
 * Es el mismo error que ya se había corregido una vez aquí —faltaba `page=1`— resurgido un nivel
 * más abajo: entonces la consulta no devolvía nada, ahora devuelve una parte. Una lista parcial es
 * peor que una vacía, porque parece una respuesta.
 */
async function listarTodo(rutaBase) {
  const separador = rutaBase.includes('?') ? '&' : '?';
  const acumulado = [];

  for (let pagina = 1; pagina <= 100; pagina += 1) {
    const r = await api('GET', `${rutaBase}${separador}page=${pagina}&pageSize=100`);
    if (!r.ok) return { ok: false, status: r.status, datos: r.datos, items: acumulado };

    const cuerpo = r.datos ?? {};

    // No todos los catálogos paginan. `/system-suites/{id}/roles` devuelve un ARRAY pelado e
    // ignora `page`, así que pedirle la página 2 devuelve lo mismo otra vez: hay que tomarlo entero
    // y parar. Tratarlo como si paginara duplicaría cada rol cien veces.
    if (Array.isArray(cuerpo)) return { ok: true, status: 200, items: cuerpo };

    const trozo = cuerpo.items ?? cuerpo.data ?? cuerpo.suites
               ?? cuerpo.datos?.items ?? cuerpo.datos?.data ?? cuerpo.datos ?? [];
    if (!Array.isArray(trozo) || trozo.length === 0) break;

    acumulado.push(...trozo);

    const total = cuerpo.totalPages ?? cuerpo.datos?.totalPages;
    if (total ? pagina >= total : trozo.length < 100) break;
  }

  return { ok: true, status: 200, items: acumulado };
}

/**
 * Alta idempotente: un 409 (o un mensaje de duplicado) no es un fallo, es «ya estaba».
 * Sin esto, reejecutar el guion tras un corte obligaría a limpiar la base a mano.
 */
async function crear(descripcion, ruta, cuerpo, extraerId, yaEstaba = null) {
  const r = await api('POST', ruta, cuerpo);

  if (r.ok) {
    cuenta.creado += 1;
    return extraerId ? extraerId(r.datos) : true;
  }

  const mensaje = JSON.stringify(r.datos);

  // `yaEstaba` es para las operaciones cuyo «ya estaba» NO se llama duplicado. Activar una cuenta
  // ya activa responde 400 `user_account.cannot_activate`, que no encaja con ningún patrón de
  // duplicado y se contaba como fallo: la segunda ejecución del guion salía con ocho errores y
  // exit 1 sobre un sistema perfectamente cargado. Un guion que solo pasa la primera vez no es
  // idempotente, es de un solo uso.
  if (yaEstaba?.test(mensaje)) {
    cuenta.existente += 1;
    return null;
  }

  if (r.status === 409 || /duplicad|already|existe|Duplicate/i.test(mensaje)) {
    cuenta.existente += 1;
    return null; // el llamador resuelve el id por consulta
  }

  cuenta.fallo += 1;
  log('✖', `${descripcion} → HTTP ${r.status} ${mensaje.slice(0, 160)}`);
  return null;
}

// ── 1. Sesión ────────────────────────────────────────────────────────────────
const acceso = await api('POST', '/api/v1/auth/login', {
  tenantCode: TENANT, username: USUARIO, password: CLAVE, rememberMe: false,
});
if (!acceso.ok) {
  console.error(`No se pudo iniciar sesión en UMS: HTTP ${acceso.status} ${JSON.stringify(acceso.datos)}`);
  process.exit(1);
}
const tenantId = acceso.datos.tenantId;
log('✔', `sesión iniciada en ${TENANT} (tenant ${tenantId})`);

// ── 1.b Sucursales del inquilino ─────────────────────────────────────────────
//
// Van ANTES de la suite porque el padrón (paso 9.b) ancla cada perfil a una sucursal, y un perfil
// no se puede crear apuntando a una sucursal que todavía no existe.
//
// LA SPEC DECLARA LAS QUE NECESITA, y este paso las resuelve o las da de alta. No inventa códigos:
// solo aplica los que están escritos. La cautela no es celo — **crear una sucursal es
// irreversible**. Su baja es un cierre lógico (ADR-0164, `CloseBranch`): la fila permanece y el
// código queda ocupado para siempre. Un código equivocado no se corrige, se arrastra.
//
// De ahí que el catálogo se lea SIEMPRE antes de crear nada y que un fallo al leerlo aborte: crear
// a ciegas sobre un catálogo que no se pudo consultar es exactamente la forma de ocupar un código
// que ya estaba puesto.
const sucursales = new Map();   // code → branchId
const SUCURSALES_DECL = SPEC.tenant?.branches ?? [];

if (SUCURSALES_DECL.length > 0) {
  const r = await api('GET', `/api/v1/tenants/${tenantId}/branches`);
  if (!r.ok) {
    console.error(`El catálogo de sucursales respondió ${r.status}: ${JSON.stringify(r.datos).slice(0, 200)}`);
    console.error('Sin catálogo no se puede decidir si una sucursal ya existe, y crearla a ciegas ocupa un código que no se libera.');
    process.exit(1);
  }

  // `GET /tenants/{id}/branches` devuelve un ARRAY pelado (`IReadOnlyList<BranchDto>`), no una
  // página; se contemplan igual las envolturas por si el endpoint cambia de forma.
  const vivas = Array.isArray(r.datos) ? r.datos : (r.datos?.items ?? r.datos?.data ?? []);

  for (const decl of SUCURSALES_DECL) {
    const ya = vivas.find((b) => b.code === decl.code);
    if (ya) {
      sucursales.set(decl.code, ya.branchId ?? ya.id);
      log('•', `sucursal ${decl.code} ya existía («${ya.name}»)`);
      continue;
    }

    // En simulacro se ANUNCIA y no se crea. Es el uso principal de `--dry-run` en este paso:
    // enterarse de que la carga iba a dar de alta una sucursal —y poder pararla— antes de que el
    // código quede ocupado.
    if (SIMULACRO) {
      log('•', `sucursal ${decl.code}: NO existe, se crearía («${decl.name}») — simulacro, no se escribe`);
      continue;
    }

    const id = await crear(`sucursal ${decl.code}`, `/api/v1/tenants/${tenantId}/branches`, {
      code: decl.code, name: decl.name, geofencingMetadata: decl.geofencingMetadata ?? null,
    }, (d) => d.branchId ?? d.id);

    if (id) {
      sucursales.set(decl.code, id);
      log('✔', `sucursal ${decl.code} creada («${decl.name}»)`);
    } else {
      log('✖', `sucursal ${decl.code}: no se pudo dar de alta; los perfiles que la usen quedarán sin crear`);
    }
  }
}

// ── 2. Suite ─────────────────────────────────────────────────────────────────
// La forma del catálogo varía entre versiones del endpoint, así que se buscan las envolturas
// conocidas y, si aun así no aparece, se admite el id por variable: reejecutar tras un corte no
// debería exigir limpiar la base.
//
// `page=1` NO es decorativo: sin él, el endpoint responde **400** —la paginación exige las dos
// partes— y el guion se quedaba con una lista vacía. Efecto medido el 2026-08-02: no encontraba la
// suite existente, intentaba crearla, recibía el conflicto, se quedaba sin id y abortaba con «sin
// suite no hay nada más que cargar» sobre una instancia que SÍ tenía la suite. Es decir: el guion
// se declaraba idempotente y no lo era, y el síntoma acusaba al dato en vez de a la consulta.
const catalogo = await listarTodo('/api/v1/system-suites');
if (!catalogo.ok) {
  console.error(`El catálogo de suites respondió ${catalogo.status}: ${JSON.stringify(catalogo.datos).slice(0, 200)}`);
  console.error('Sin catálogo no se puede decidir si la suite ya existe, y crearla a ciegas duplica.');
  process.exit(1);
}
const existente = catalogo.items.find((s) => s.code === SPEC.suite.code) ?? null;

let suiteId = process.env.UMS_SUITE_ID || existente?.id || existente?.systemSuiteId || null;
if (suiteId) {
  log('•', `suite ${SPEC.suite.code} ya existía (${suiteId})`);
} else {
  suiteId = await crear(`suite ${SPEC.suite.code}`, '/api/v1/system-suites', {
    tenantId, code: SPEC.suite.code, name: SPEC.suite.name, description: SPEC.suite.description,
  }, (d) => d.id ?? d.systemSuiteId);
  if (!suiteId) {
    // En simulacro esto NO es un fallo: la suite no existe y no se ha creado porque no se
    // escribe nada, así que no hay GUID del que colgar módulos, nodos ni plantillas. Se informa
    // de lo único que se puede afirmar —que habría que crearla entera— y se sale en verde.
    // El simulacro es exhaustivo sobre una suite que YA existe, que es su caso de uso: contestar
    // «¿en qué difiere esta instancia de la spec?».
    if (SIMULACRO) {
      log('•', `[simulacro] la suite ${SPEC.suite.code} NO existe en este inquilino: se crearía completa`);
      log('•', '[simulacro] sin la suite creada no se puede diferenciar su contenido; ejecuta sin --dry-run');
      process.exit(0);
    }
    console.error('Sin suite no hay nada más que cargar.');
    process.exit(1);
  }
  log('✔', `suite ${SPEC.suite.code} creada (${suiteId})`);
}

const base = `/api/v1/system-suites/${suiteId}`;

// ── 2.b Simulacro: diferencia real contra el estado de la instancia ──────────
//
// El simulacro NO se implementa cortocircuitando las escrituras y contándolas: sin el 409 que
// devuelve el intento de crear, el cargador no puede saber qué existía ya, y todo saldría como
// «creado». Un informe que dice 33 cambios sobre una instancia idéntica a la spec es peor que no
// tenerlo, porque nadie volvería a mirarlo.
//
// Se compara contra `GET {base}`, que devuelve el árbol completo —módulos, nodos recursivos con
// sus `actionCodes`, acciones de suite y objetos de dominio—, más los catálogos de roles y
// plantillas. Es el estado real, no una inferencia.
//
// Límite declarado: los `appSettings` NO viajan en el DTO de la suite, así que no se pueden
// diferenciar. Se informa como desconocido en vez de darlos por buenos.
if (SIMULACRO) {
  const d = (await api('GET', base)).datos ?? {};
  const faltan = [];

  const accionesSuite = new Set((d.actions ?? []).map((a) => a.code));
  for (const a of SPEC.actions ?? []) {
    if (!accionesSuite.has(a.code)) faltan.push(`acción ${a.code}`);
  }

  const modulosPorCodigo = new Map((d.modules ?? []).map((m) => [m.code, m]));
  const recursos = new Set((d.domainResources ?? []).map((r) => r.code));

  // Índice plano de nodos por código: el árbol de la spec y el de la instancia se recorren por
  // código de negocio, que es lo único estable entre instancias.
  const nodosPorCodigo = new Map();
  const recorrer = (lista) => {
    for (const n of lista ?? []) {
      nodosPorCodigo.set(n.code, n);
      recorrer(n.children);
    }
  };
  for (const m of d.modules ?? []) recorrer(m.nodes);

  for (const m of SPEC.modules ?? []) {
    if (!modulosPorCodigo.has(m.code)) { faltan.push(`módulo ${m.code} (y todo su árbol)`); continue; }

    const comprobarNodo = (n, tipo) => {
      const real = nodosPorCodigo.get(n.code);
      if (!real) { faltan.push(`${tipo} ${m.code}/${n.code}`); return; }
      for (const codigo of n.actions ?? []) {
        if (!(real.actionCodes ?? []).includes(codigo)) {
          faltan.push(`acción ${codigo} en ${n.code}`);
        }
      }
    };

    for (const me of m.menus ?? []) {
      comprobarNodo(me, 'menú');
      for (const sm of me.submenus ?? []) {
        comprobarNodo(sm, 'submenú');
        for (const op of sm.options ?? []) comprobarNodo(op, 'opción');
      }
    }

    for (const dr of m.domainResources ?? []) {
      if (!recursos.has(dr.code)) faltan.push(`objeto de dominio ${dr.code}`);
    }
  }

  const catRoles = await listarTodo(`${base}/roles`);
  const rolesReales = new Set(catRoles.items.map((r) => r.code));
  for (const r of SPEC.roles ?? []) {
    if (!rolesReales.has(r.code)) faltan.push(`rol ${r.code}`);
  }

  const catPlantillas = await listarTodo(`/api/v1/permission-templates?systemSuiteId=${suiteId}`);
  const rolIdPorCodigo = new Map(catRoles.items.map((r) => [r.code, r.roleId ?? r.id]));
  for (const t of SPEC.permissionTemplates ?? []) {
    const rolId = rolIdPorCodigo.get(t.role);
    const hay = catPlantillas.items.some((x) => (x.roleId ?? null) === rolId);
    if (!hay) faltan.push(`plantilla ${t.code} (rol ${t.role})`);
  }

  // ── El PADRÓN, en el simulacro ─────────────────────────────────────────────
  //
  // FALTABA, y era el silencio más caro que tenía este guion: `--dry-run` salía justo antes de
  // mirar el padrón, así que la única pasada pensada para contestar «¿qué me falta antes de
  // escribir?» no contestaba nada sobre las personas. Se revisaba el árbol entero y se callaba lo
  // que hace fallar la carga real seis veces seguidas: que la cuenta no existe.
  //
  // Las sucursales no se comprueban aquí porque el paso 1.b ya las anunció una por una.
  const perfilesReales = (await listarTodo('/api/v1/profiles')).items ?? [];

  for (const decl of SPEC.profiles ?? []) {
    const correo = decl.userEmailEnv ? process.env[decl.userEmailEnv] : decl.userEmail;
    if (!correo) {
      faltan.push(`padrón: la fila de ${decl.role} no tiene correo (${decl.userEmailEnv} sin definir)`);
      continue;
    }
    if (decl.tenant && decl.tenant !== TENANT) {
      faltan.push(`padrón: ${correo} declara el inquilino ${decl.tenant} y la sesión es de ${TENANT}`);
      continue;
    }

    const b = await api('GET', `/api/v1/user-accounts?page=1&search=${encodeURIComponent(correo)}&pageSize=20`);
    const lista = b.datos?.items ?? b.datos?.data ?? b.datos ?? [];
    const u = Array.isArray(lista) ? lista.find((x) => x.email === correo) : null;
    const userId = u?.userAccountId ?? u?.id ?? null;

    if (!userId) {
      // El matiz importa: con la bandera es un aviso de lo que va a pasar; sin ella es la razón
      // por la que la carga real va a fallar, y conviene leerlo antes y no después.
      faltan.push(CREAR_CUENTAS
        ? `cuenta ${correo} — se CREARÍA (--crear-cuentas)`
        : `cuenta ${correo} — NO existe y NO se crearía; añade --crear-cuentas o dala de alta`);
      continue;
    }

    const roleId = rolIdPorCodigo.get(decl.role);
    for (const codigo of decl.branches ?? (decl.branch ? [decl.branch] : [null])) {
      if (codigo && !sucursales.has(codigo)) {
        faltan.push(`perfil ${decl.role} de ${correo}: su sucursal ${codigo} todavía no existe`);
        continue;
      }
      const branchId = codigo ? sucursales.get(codigo) : null;
      const hay = perfilesReales.some((p) => p.userId === userId
        && p.roleId === roleId
        && (p.branchId ?? null) === branchId);
      if (!hay) faltan.push(`perfil ${decl.role} de ${correo}${codigo ? ` en ${codigo}` : ''}`);
    }
  }

  console.log('');
  if (faltan.length === 0) {
    log('✔', `[simulacro] sin diferencias: la instancia ya refleja ${RUTA_SPEC}`);
  } else {
    log('•', `[simulacro] ${faltan.length} objeto(s) de la spec no están en la instancia:`);
    for (const f of faltan) console.log(`    ✖ ${f}`);
  }
  log('•', '[simulacro] los appSettings no se pueden diferenciar: el DTO de la suite no los devuelve');
  console.log('');

  process.exitCode = faltan.length === 0 ? 0 : 1;
  process.exit(process.exitCode);
}

// ── 3. Catálogo de acciones ──────────────────────────────────────────────────
for (const a of SPEC.actions) {
  await crear(`acción ${a.code}`, `${base}/actions`, {
    systemSuiteId: suiteId, code: a.code, name: a.name, description: a.name,
  });
}
log('✔', `${SPEC.actions.length} acciones procesadas`);

// ── 4. Módulos, navegación y objetos de dominio ──────────────────────────────
// El árbol se recorre igual que lo declara la spec: menú → submenús → opciones. Las opciones son
// hojas y son las únicas que vinculan acciones (regla del dominio, ADR-0090).
async function nodo(moduleId, parentNodeId, kind, n, orden) {
  const id = await crear(`nodo ${n.code}`, `${base}/modules/${moduleId}/nodes`, {
    systemSuiteId: suiteId,
    moduleId,
    parentNodeId,
    kind,
    code: n.code,
    label: n.name,
    description: n.descripcion ?? n.name,
    sortOrder: orden,
    icon: n.icon ?? null,
    route: n.route ?? null,
  }, (d) => d.nodeId ?? d.id);

  // Si ya existía, el alta devuelve `null` y hasta hoy el guion se encogía de hombros: la spec podía
  // corregir un rótulo y la instancia se quedaba con el viejo para siempre. Medido: la spec decía
  // «Gobierno de Satélites» y el menú de UMS seguía diciendo «Gobierno de Satelites», porque los
  // textos de la spec ACABAN siendo el nombre del nodo. Aplicar una spec no es solo crear lo que
  // falta; es dejar la instancia como la spec dice.
  if (id) return id;

  const existente = nodosDeLaInstancia.get(n.code);
  if (!existente) return null;

  if (existente.label !== n.name) {
    const r = await api('PUT', `${base}/modules/${moduleId}/nodes/${existente.id}`, {
      systemSuiteId: suiteId,
      moduleId,
      nodeId: existente.id,
      label: n.name,
      description: n.descripcion ?? n.name,
      sortOrder: orden,
      icon: n.icon ?? null,
      route: n.route ?? null,
    });
    if (r.ok) log('•', `nodo ${n.code}: rótulo actualizado a «${n.name}»`);
    else log('•', `nodo ${n.code}: no se pudo actualizar el rótulo (${r.status})`);
  }
  return existente.id;
}

/**
 * Los nodos que la instancia YA tiene, por código, con su rótulo y sus acciones vinculadas.
 *
 * Es lo que permite reconciliar en vez de solo añadir: sin este índice, el guion no puede saber si
 * un rótulo cambió ni si sobra un vínculo de acción.
 */
const nodosDeLaInstancia = new Map();
async function indexarNodosDeLaInstancia() {
  const d = await api('GET', base);
  const recorrer = (lista) => {
    for (const n of lista ?? []) {
      nodosDeLaInstancia.set(n.code, {
        id: n.id ?? n.nodeId,
        label: n.label ?? n.name,
        actionCodes: n.actionCodes ?? [],
      });
      recorrer(n.children);
    }
  };
  for (const m of d.datos?.modules ?? d.datos?.modulos ?? []) recorrer(m.nodes ?? m.nodos);
}

// El detalle de la suite es la fuente para reentrar: si un módulo ya existe, se recupera su id en
// vez de saltárselo. Saltar el módulo entero dejaba fuera todo lo que colgaba de él —que fue
// exactamente lo que pasó con los objetos de dominio en la primera carga.
await indexarNodosDeLaInstancia();

const detalle = await api('GET', base);
const modulosExistentes = new Map(
  (detalle.datos?.modules ?? detalle.datos?.modulos ?? []).map((x) => [x.code, x.id ?? x.moduleId]),
);

for (const m of SPEC.modules) {
  let moduleId = await crear(`módulo ${m.code}`, `${base}/modules`, {
    systemSuiteId: suiteId, code: m.code, name: m.name, description: m.description, sortOrder: m.sortOrder,
  }, (d) => d.moduleId ?? d.id);

  if (!moduleId) moduleId = modulosExistentes.get(m.code) ?? null;
  if (!moduleId) { log('•', `módulo ${m.code}: no se pudo crear ni resolver; se omite su árbol`); continue; }

  // El módulo que ya existía también se pone al día. Los nodos ya se reconcilian más abajo, pero el
  // rótulo del MÓDULO es el que encabeza el menú: sin esto, la spec podía decir «Gobierno de
  // Satélites» y la barra lateral seguía diciendo «Gobierno de Satelites».
  const moduloReal = (detalle.datos?.modules ?? detalle.datos?.modulos ?? []).find((x) => x.code === m.code);
  if (moduloReal && moduloReal.name !== m.name) {
    const r = await api('PUT', `${base}/modules/${moduleId}`, {
      systemSuiteId: suiteId, moduleId, name: m.name, description: m.description, sortOrder: m.sortOrder,
    });
    if (r.ok) log('•', `módulo ${m.code}: rótulo actualizado a «${m.name}»`);
    else log('•', `módulo ${m.code}: no se pudo actualizar el rótulo (${r.status})`);
  }

  // Un módulo nace inactivo y un módulo inactivo no admite nodos: activar es parte del alta.
  await api('POST', `${base}/modules/${moduleId}/activate`);

  for (const me of m.menus ?? []) {
    const menuId = await nodo(moduleId, null, 'Menu', me, me.sortOrder);
    if (!menuId) continue;

    for (const sm of me.submenus ?? []) {
      const subId = await nodo(moduleId, menuId, 'SubMenu', sm, sm.sortOrder);
      if (!subId) continue;

      for (const op of sm.options ?? []) {
        const opId = await nodo(moduleId, subId, 'Option', op, op.sortOrder);
        if (!opId) continue;

        for (const codigo of op.actions ?? []) {
          await crear(`acción ${codigo} en ${op.code}`,
            `${base}/modules/${moduleId}/nodes/${opId}/actions`,
            { systemSuiteId: suiteId, moduleId, nodeId: opId, actionCode: codigo });
        }

        // Y se RETIRA lo que la spec ya no declara. Añadir sin retirar deja scopes vivos que la
        // spec dice haber quitado: la declaración y el inventario dejan de coincidir, y quien
        // reparta perfiles concede una capacidad que creía retirada. Medido: `fase_gate.approve` se
        // retiró de la spec y el administrador seguía recibiéndolo, 84 scopes donde debían ser 83.
        const declaradas = new Set(op.actions ?? []);
        for (const codigo of nodosDeLaInstancia.get(op.code)?.actionCodes ?? []) {
          if (declaradas.has(codigo)) continue;
          const r = await api('DELETE', `${base}/modules/${moduleId}/nodes/${opId}/actions/${codigo}`);
          if (r.ok) log('•', `acción ${codigo} RETIRADA de ${op.code} (ya no está en la spec)`);
          else log('•', `acción ${codigo} en ${op.code}: no se pudo retirar (${r.status})`);
        }
      }
    }
  }

  for (const dr of m.domainResources ?? []) {
    await crear(`objeto ${dr.code}`, `${base}/domain-resources`, {
      systemSuiteId: suiteId, moduleId, type: dr.type, code: dr.code,
      name: dr.name, description: dr.description,
    });
  }

  log('✔', `módulo ${m.code} cargado`);
}

// ── 5. Ajustes del sistema ───────────────────────────────────────────────────
for (const s of SPEC.appSettings ?? []) {
  await crear(`ajuste ${s.key}`, `${base}/app-settings`, {
    systemSuiteId: suiteId, key: s.key, value: s.value, scope: 'Global',
    isClientVisible: !!s.isClientVisible,
  });
}

// ── 6. Roles ─────────────────────────────────────────────────────────────────
// Un rol de nivel N exige colgar de uno de nivel N-1: la jerarquía es una cadena, no una lista
// (verificado — los roles de nivel 0 entran y los demás son rechazados sin padre). La spec declara
// el nivel pero no el padre, así que se deriva: el primero del nivel anterior por orden de
// promoción, que es el criterio con el que la propia spec los ordena.
const roles = new Map();
const porNivel = new Map();

// El catálogo se lee ANTES de crear, no después: en una reejecución los roles raíz ya existen, y si
// no se conocen aquí los de nivel 1 vuelven a pedirse sin padre —que es justo lo que UMS rechaza—.
// Este era el fallo: la primera pasada creaba los de nivel 0 y las siguientes no los recordaban.
{
  const cat = await listarTodo(`${base}/roles`);
  const lista = cat.items;
  if (Array.isArray(lista)) {
    for (const r of lista) {
      const id = r.id ?? r.roleId;
      if (!r.code || !id) continue;
      roles.set(r.code, id);
      const nivel = r.hierarchyLevel ?? r.hierarchy_level;
      if (typeof nivel === 'number' && !porNivel.has(nivel)) porNivel.set(nivel, id);
    }
  }
}

const ordenados = [...(SPEC.roles ?? [])].sort(
  (a, b) => a.hierarchyLevel - b.hierarchyLevel || a.promotionOrder - b.promotionOrder,
);

for (const rol of ordenados) {
  if (roles.has(rol.code)) { cuenta.existente += 1; continue; }
  const padre = rol.hierarchyLevel > 0 ? (porNivel.get(rol.hierarchyLevel - 1) ?? null) : null;
  const id = await crear(`rol ${rol.code}`, `${base}/roles`, {
    systemSuiteId: suiteId, code: rol.code, value: rol.name, description: rol.description,
    parentRoleId: padre, hierarchyLevel: rol.hierarchyLevel, promotionOrder: rol.promotionOrder,
  }, (d) => d.roleId ?? d.id);

  if (id) {
    roles.set(rol.code, id);
    if (!porNivel.has(rol.hierarchyLevel)) porNivel.set(rol.hierarchyLevel, id);
  }
}

// Reentrada: los roles que ya existían se resuelven del catálogo.
if (roles.size < (SPEC.roles ?? []).length) {
  const cat = await listarTodo(`${base}/roles`);
  const lista = cat.items;
  if (Array.isArray(lista)) for (const r of lista) if (r.code) roles.set(r.code, r.id ?? r.roleId);
}
log('✔', `${roles.size} roles disponibles`);

// ── 7. Plantillas de permisos ────────────────────────────────────────────────
// Una plantilla por rol, con sus concesiones expandidas desde los comodines de la spec.
//
// `POST /permission-templates` NUNCA devuelve 409: el dominio asigna la versión siguiente a la
// máxima existente (CreateNextVersion, G-140), así que cada reejecución fabricaba una versión más
// —17 plantillas donde debían ir 8—. Por eso aquí se lee el catálogo ANTES de crear, igual que en
// los roles, y las sobrantes se borran: `DELETE /permission-templates/{id}` acepta Draft y
// Deprecated (PermissionTemplate.Delete), y arrastra sus ítems.
const plantillas = new Map(); // rolCode → { id, version, status }
const sobrantes = [];
// Solo con `--nueva-version`: la versión que estaba viva antes de esta pasada, para deprecarla
// cuando la nueva ya esté publicada Y asignada. En ese orden — deprecar antes dejaría a los
// perfiles apuntando a una plantilla deprecada durante la ventana intermedia.
const anteriores = new Map(); // rolCode → { id, version, status }

{
  const cat = await listarTodo(`/api/v1/permission-templates?systemSuiteId=${suiteId}`);
  const lista = cat.items;
  const porRol = new Map();
  const rolPorId = new Map([...roles].map(([codigo, id]) => [id, codigo]));

  for (const p of Array.isArray(lista) ? lista : []) {
    const codigo = rolPorId.get(p.roleId);
    if (!codigo) continue;
    if (!porRol.has(codigo)) porRol.set(codigo, []);
    porRol.get(codigo).push(p);
  }

  // Se conserva la MAYOR versión publicada, y el criterio es el contrario del que hubo aquí: antes
  // se tomaba la menor «porque es la que se creó primero».
  //
  // Eso valía cuando las versiones eran copias unas de otras. Desde que `--nueva-version` existe
  // para que una CORRECCIÓN de la spec llegue al sistema vivo, la versión nueva es la que lleva los
  // permisos corregidos y la vieja es la equivocada. Con el criterio anterior, una reejecución sin
  // la bandera reasignaba la vieja y —desde que asignar SUPERSEDE a la de su línea (ADR-UMS-106)—
  // desasignaba activamente la buena. Medido el 2026-08-21 contra el stage: GERENTE y JEFE volvieron
  // de 49 y 41 scopes a 83, es decir a la superficie completa que el recorte les acababa de quitar.
  //
  // Un `Published` sigue ganando a un `Draft`: un borrador no puede estar asignado.
  const rango = (p) => (p.status === 'Published' ? 0 : p.status === 'Deprecated' ? 2 : 1);
  for (const [codigo, grupo] of porRol) {
    grupo.sort((a, b) => rango(a) - rango(b) || b.version.localeCompare(a.version, 'en', { numeric: true }));
    const [canonica, ...resto] = grupo;

    // Con `--nueva-version` la canónica NO entra en `plantillas`: dejarla fuera es lo que hace que
    // el bucle de creación fabrique la versión siguiente en vez de saltarla por «ya existente».
    // Se guarda aparte para deprecarla al final, cuando la nueva ya esté publicada y asignada.
    if (NUEVA_VERSION) {
      anteriores.set(codigo, { id: canonica.templateId, version: canonica.version, status: canonica.status });

      // Los BORRADORES sí se barren, y solo ellos. Un `Draft` no puede estar asignado --asignar
      // exige la plantilla publicada--, así que borrarlo no rompe ningún perfil; y son justamente
      // el residuo que deja una pasada que se quedó a medias, como la del 2026-08-16 que agotó la
      // cuota de peticiones y abandonó dos versiones con los ítems incompletos. Sin esto se
      // acumulan, y la siguiente ejecución ya no sabe cuál de todas era la buena.
      //
      // Lo publicado y lo deprecado NO se tocan: una versión vieja puede estar asignada, y el
      // objetivo aquí es sustituirla, no dejar huecos por el camino.
      sobrantes.push(...grupo.filter((p) => p.status === 'Draft').map((p) => ({ rol: codigo, ...p })));
      continue;
    }

    plantillas.set(codigo, { id: canonica.templateId, version: canonica.version, status: canonica.status });
    sobrantes.push(...resto.map((p) => ({ rol: codigo, ...p })));
  }
}

for (const p of sobrantes) {
  const r = await api('DELETE', `/api/v1/permission-templates/${p.templateId}`);
  if (r.ok) { log('•', `plantilla duplicada ${p.rol} v${p.version} eliminada`); }
  else { cuenta.fallo += 1; log('✖', `no se pudo eliminar la plantilla duplicada ${p.rol} v${p.version} → HTTP ${r.status} ${JSON.stringify(r.datos).slice(0, 160)}`); }
}

for (const t of SPEC.permissionTemplates ?? []) {
  const roleId = roles.get(t.role);
  if (!roleId) { log('•', `plantilla ${t.code}: sin rol ${t.role}`); continue; }
  if (plantillas.has(t.role)) { cuenta.existente += 1; continue; }

  const id = await crear(`plantilla ${t.code}`, '/api/v1/permission-templates', {
    tenantId, roleId, systemSuiteId: suiteId,
  }, (d) => d.templateId ?? d.id);
  // La versión la decide el dominio (CreateNextVersion) y la respuesta de creación no la devuelve.
  // `0.1.0` solo es cierto para una plantilla que nace; con `--nueva-version` sería una etiqueta
  // falsa en el registro, así que se dice lo que se sabe. Solo afecta a lo que se imprime: el
  // resto del guion se guía por `status`.
  if (id) plantillas.set(t.role, { id, version: NUEVA_VERSION ? '(siguiente)' : '0.1.0', status: 'Draft' });
}
log('✔', `${plantillas.size} plantillas disponibles`);

// ── 7.1 Índices código → id para traducir las concesiones ────────────────────
// La spec declara las concesiones por CÓDIGO y con comodines; la API las exige por GUID
// (AddTemplateItemCommand). El detalle de la suite es la única fuente para el puente: se relee
// aquí —y no el `detalle` del paso 4— porque en la primera pasada aún no había módulos ni nodos.
const suiteDet = (await api('GET', base)).datos ?? {};
const idAccion = new Map((suiteDet.actions ?? []).map((a) => [a.code, a.id]));
const idModulo = new Map((suiteDet.modules ?? []).map((m) => [m.code, m.id]));
const idRecurso = new Map((suiteDet.domainResources ?? []).map((r) => [r.code, { id: r.id, tipo: r.type }]));

// Cada nodo se indexa con su cadena de ancestros: conceder una opción sin conceder navegación a
// sus padres deja la opción inalcanzable —el menú no se pinta— y conceder los padres sin mirar
// las hojas abre menús vacíos. Con la cadena a mano se puede hacer lo justo.
const nodos = new Map(); // code → { id, kind, actionCodes, moduleId, ancestros: [{id,kind}], hojas: [] }
{
  const registrar = (n, moduleId, ancestros) => {
    const registro = {
      id: n.id, kind: n.kind, code: n.code,
      actionCodes: n.actionCodes ?? [], moduleId, ancestros,
    };
    nodos.set(n.code, registro);
    for (const h of n.children ?? []) registrar(h, moduleId, [...ancestros, { id: n.id, kind: n.kind }]);
  };
  for (const m of suiteDet.modules ?? []) for (const n of m.nodes ?? []) registrar(n, m.id, []);
}

const opcionesDe = (predicado) => [...nodos.values()].filter((n) => n.kind === 'Option' && predicado(n));
const bajo = (nodo) => opcionesDe((n) => n.id === nodo.id || n.ancestros.some((a) => a.id === nodo.id));

// El grafo no evalúa efecto en nodos estructurales (AuthorizationGraphBuilderService.ProyectarNodo
// solo casa (nodeId, actionId) en las hojas), así que el ActionId de una fila de navegación es
// indiferente: el seeder canónico usa uno aleatorio. Aquí se usa VIEW, que además de bastar deja
// la tabla legible cuando se audita a mano.
const ACCION_NAV = idAccion.get('VIEW');

// ── 7.2 Traductor de concesiones ─────────────────────────────────────────────
// Mapeo de destinos verificado contra el seeder canónico (AuthorizationDevDataSeeder,
// AddNodeNavigation) y contra `ExclusiveArcTarget`, de donde el validador de la API los deriva:
//   suite → SystemSuite · módulo → Module · nodo Menu → Submodule · nodo SubMenu y Option → Option
//   objeto de dominio → Aggregate o Entity, según su propio tipo
// El mapeo de nodos NO es 1:1 con `NodeKind`: un `SubMenu` se registra como `Option`.
function traducir(plantilla) {
  const deseado = new Map();
  const pendientes = [];

  // Deny gana sobre Allow, igual que el resolutor del grafo. Es una decisión de CONSTRUCCIÓN, no
  // de envío: la clave (tipo, destino, acción) es única en el agregado, así que si un módulo
  // concede en bloque lo que otra concesión deniega en detalle —PMO sobre ART_CERT/CERTIFY— la
  // segunda fila sería rechazada por duplicada y el deny se perdería en silencio.
  const anota = (tipo, targetId, actionId, deny) => {
    if (!targetId || !actionId) return;
    const clave = `${tipo}|${targetId}|${actionId}`;
    const previo = deseado.get(clave);
    if (previo) { if (deny) previo.deny = true; return; }
    deseado.set(clave, { targetType: tipo, targetId, actionId, deny });
  };

  const navegacion = (opcion) => {
    anota('SystemSuite', suiteId, ACCION_NAV, false);
    anota('Module', opcion.moduleId, ACCION_NAV, false);
    for (const a of opcion.ancestros) anota(a.kind === 'Menu' ? 'Submodule' : 'Option', a.id, ACCION_NAV, false);
  };

  // `literal` distingue las dos intenciones que conviven en la spec:
  //   contenedor (suite/module/menu/submenu) → la lista acota, pero solo puede concederse lo que
  //     la opción tiene vinculado; expandir el producto cartesiano concedería filas que el grafo
  //     nunca consulta y engordaría la plantilla sin significado.
  //   opción concreta → la lista es la voluntad explícita del autor y se respeta tal cual, que es
  //     lo que permite denegar una acción puntual.
  const conceder = (opcion, acciones, deny, literal) => {
    const codigos = acciones === '*'
      ? opcion.actionCodes
      : (literal ? acciones : acciones.filter((c) => opcion.actionCodes.includes(c)));

    let algunAllow = false;
    for (const codigo of codigos) {
      const actionId = idAccion.get(codigo);
      if (!actionId) { pendientes.push(`acción ${codigo} no existe en la suite`); continue; }
      anota('Option', opcion.id, actionId, deny);
      if (!deny) algunAllow = true;
    }
    // La navegación al subárbol solo se abre si algo quedó CONCEDIDO ahí: un grant que solo
    // deniega no debe hacer visible el menú que lo contiene.
    if (algunAllow) navegacion(opcion);
  };

  for (const g of plantilla.grants ?? []) {
    const deny = g.effect === 'deny';
    const acciones = g.actions === '*' ? '*' : (Array.isArray(g.actions) ? g.actions : [g.actions]);

    if (g.suite !== undefined) {
      for (const opcion of opcionesDe(() => true)) conceder(opcion, acciones, deny, false);
      continue;
    }

    if (g.module !== undefined) {
      const moduleId = idModulo.get(g.module);
      if (!moduleId) { pendientes.push(`módulo ${g.module} no existe`); continue; }
      for (const opcion of opcionesDe((n) => n.moduleId === moduleId)) conceder(opcion, acciones, deny, false);
      continue;
    }

    if (g.menu !== undefined || g.submenu !== undefined) {
      const codigo = g.menu ?? g.submenu;
      const nodo = nodos.get(codigo);
      const esperado = g.menu !== undefined ? 'Menu' : 'SubMenu';
      if (!nodo) { pendientes.push(`nodo ${codigo} no existe`); continue; }
      // El kind se verifica en vez de asumirse: un `menu:` que en realidad apunta a un submenú
      // expandiría un subárbol distinto del declarado, y eso concede permisos que nadie pidió.
      if (nodo.kind !== esperado) { pendientes.push(`nodo ${codigo} es ${nodo.kind}, la spec lo declara ${esperado}`); continue; }
      for (const opcion of bajo(nodo)) conceder(opcion, acciones, deny, false);
      continue;
    }

    if (g.option !== undefined) {
      const nodo = nodos.get(g.option);
      if (!nodo) { pendientes.push(`opción ${g.option} no existe`); continue; }
      if (nodo.kind !== 'Option') { pendientes.push(`nodo ${g.option} es ${nodo.kind}, no una opción`); continue; }
      conceder(nodo, acciones, deny, true);
      continue;
    }

    if (g.resource !== undefined) {
      // El `targetType` es el TIPO DEL PROPIO RECURSO —`Aggregate` o `Entity`—, no un cuarto valor
      // llamado «DomainResource»: ese no existe en `ExclusiveArcTarget` y la API lo rechaza con 400.
      //
      // Aquí hubo un rechazo en bloque mientras G-192 estuvo abierto, porque el validador llevaba
      // una copia a mano de los destinos admitidos que dejaba fuera los dos del dominio. G-192 se
      // cerró el 2026-08-02 —`AddTemplateItemCommandValidator` los deriva hoy de
      // `ExclusiveArcTarget`— y el rechazo se quedó puesto: durante ese tiempo TODA plantilla
      // cargada por spec nacía con `domainPermissions` vacío, en silencio y con exit 0.
      const destinos = g.resource === '*' ? [...idRecurso.keys()] : [g.resource];
      for (const codigo of destinos) {
        const recurso = idRecurso.get(codigo);
        if (!recurso) { pendientes.push(`objeto de dominio ${codigo} no existe en la suite`); continue; }

        // `DomainResourceType` tiene tres valores y `ExclusiveArcTarget` solo dos: un recurso
        // `DomainMethod` no tiene destino válido. Se dice en vez de mandarlo como otra cosa —el
        // grafo casa solo (TargetId, ActionId), así que un tipo mentiroso colaría y dejaría la
        // tabla afirmando algo que no es.
        if (recurso.tipo !== 'Aggregate' && recurso.tipo !== 'Entity') {
          pendientes.push(`objeto de dominio ${codigo} es de tipo ${recurso.tipo}, que no tiene destino en ExclusiveArcTarget`);
          continue;
        }

        // Los objetos de dominio NO tienen acciones vinculadas como las opciones: se concede lo que
        // pida la spec, y `*` son todas las de la suite. Es lo mismo que hace el sembrador canónico.
        const codigos = acciones === '*' ? [...idAccion.keys()] : acciones;
        for (const cod of codigos) {
          const actionId = idAccion.get(cod);
          if (!actionId) { pendientes.push(`acción ${cod} no existe en la suite`); continue; }
          anota(recurso.tipo, recurso.id, actionId, deny);
        }
      }
      continue;
    }

    pendientes.push(`forma de concesión no reconocida: ${JSON.stringify(g)}`);
  }

  return { items: [...deseado.values()], pendientes };
}

// ── 7.3 Carga de concesiones ─────────────────────────────────────────────────
// Los ítems solo se admiten en Draft (PermissionTemplate.AddItem), así que la reentrada se apoya en
// el rechazo por duplicado del propio agregado (`template_item_target_already_exists`), que `crear`
// ya interpreta como «ya estaba». Por lo mismo NO se publica aquí: publicar cerraría la plantilla y
// una corrección de la spec exigiría una versión nueva.
//
// (`GET /permission-templates/{id}` SÍ devuelve los ítems desde que se cerró G-193; el rechazo por
// duplicado se conserva porque sigue siendo más barato que releer la plantilla entera por ítem.)
const resumenPlantillas = [];

for (const t of SPEC.permissionTemplates ?? []) {
  const plantilla = plantillas.get(t.role);
  if (!plantilla) continue;

  const { items, pendientes } = traducir(t);

  if (plantilla.status !== 'Draft') {
    // Cuenta como FALLO, no como aviso. Antes solo se registraba y el proceso salía con 0: como el
    // paso 10 publica todas las plantillas al terminar, la segunda ejecución encontraba todo en
    // `Published` y descartaba en bloque sus concesiones —hasta 407 en la del administrador—
    // informando de éxito. Un guion que pierde permisos y devuelve 0 es peor que uno que falla.
    log('✖', `plantilla ${t.code}: en estado ${plantilla.status}; no admite ítems, se DESCARTAN ${items.length}`);
    log(' ', `  para corregirla hay que crear una versión nueva: POST /api/v1/permission-templates con el mismo rol`);
    cuenta.fallo += 1;
    resumenPlantillas.push({ codigo: t.code, items: 0, pendientes: [`plantilla en estado ${plantilla.status}: ${items.length} concesiones descartadas`] });
    continue;
  }

  let alta = 0;
  for (const it of items) {
    const r = await crear(`ítem ${it.targetType} de ${t.code}`,
      `/api/v1/permission-templates/${plantilla.id}/items`,
      {
        templateId: plantilla.id, targetType: it.targetType, targetId: it.targetId,
        actionId: it.actionId, isAllowed: !it.deny, isDenied: !!it.deny,
      }, (d) => d.itemId);
    if (r) alta += 1;
  }

  for (const p of pendientes) log('•', `plantilla ${t.code}: ${p}`);
  log('✔', `plantilla ${t.code}: ${items.length} concesiones (${alta} nuevas)`);
  resumenPlantillas.push({ codigo: t.code, items: items.length, pendientes });
}

// ── 8. Usuarios y perfiles de ejemplo, uno por rol ───────────────────────────
// El objetivo no es tener con qué entrar: es poder COMPARAR comportamientos entre roles —qué menús
// ve cada uno, qué acciones tiene, dónde hay un Deny explícito— que es lo que valida que la
// interfaz obedece al grafo y no a código.
//
// Es ANDAMIAJE DE PRUEBA, no configuración: solo corre si la spec declara `sampleUsers`. Un sistema
// como UMS, que se provisiona en un entorno real, no quiere ocho cuentas sintéticas.
//
// `--sin-usuarios-de-ejemplo` lo omite aunque la spec lo declare. Existe porque la misma spec sirve
// a dos entornos con necesidades opuestas: el de desarrollo NECESITA esas cuentas —el carril E2E
// compara qué ve cada rol y sin ellas no compara nada—, y un QA o una producción no las quiere
// dentro. Antes había que elegir entre las dos cosas editando el archivo, y editar la spec por
// entorno es justo lo que este mecanismo vino a evitar.
const OMITIR_MUESTRA = process.argv.includes('--sin-usuarios-de-ejemplo');
const MUESTRA = OMITIR_MUESTRA ? null : (SPEC.sampleUsers ?? null);

if (OMITIR_MUESTRA && SPEC.sampleUsers) {
  log('•', 'se omiten los usuarios de ejemplo de la spec (--sin-usuarios-de-ejemplo)');
}

// El patrón vive en la spec, no aquí. Debe ser LITERAL y no derivarse del código de suite: las
// pruebas E2E reconstruyen la contraseña por su cuenta desde el mismo patrón, y derivarla de otra
// forma rompería ese carril sin que nadie lo hubiera tocado.
const correoDe = (codigo) =>
  (MUESTRA?.emailPattern ?? '{rol}@invalido.local').replace('{rol}', codigo.toLowerCase().replace(/_/g, '.'));

const usuarios = new Map();
const perfiles = new Map();   // rolCode → profileId

for (const rol of (MUESTRA ? SPEC.roles ?? [] : [])) {
  const roleId = roles.get(rol.code);
  if (!roleId) continue;

  const email = correoDe(rol.code);
  const userId = await crear(`usuario ${email}`, '/api/v1/user-accounts', {
    tenantId, branchId: null, email, category: 'Internal',
    identityReference: null, identityReferenceType: null,
  }, (d) => d.userAccountId ?? d.id);

  // En reejecución el alta devuelve duplicado; el id se resuelve del catálogo para que la
  // credencial y el perfil sigan siendo alcanzables.
  let idFinal = userId;
  if (!idFinal) {
    const b = await api('GET', `/api/v1/user-accounts?page=1&search=${encodeURIComponent(email)}&pageSize=20`);
    const lista = b.datos?.items ?? b.datos?.data ?? b.datos ?? [];
    // `userAccountId` PRIMERO: es la clave que devuelve el catálogo. El alta ya la contemplaba
    // (`d.userAccountId ?? d.id`) y esta relectura no, así que en reejecución resolvía `undefined`
    // y el usuario se descartaba en silencio — sin credencial, sin perfil y sin activar, con el
    // guion informando «0 credenciales procesadas» sobre ocho cuentas que existían.
    if (Array.isArray(lista)) {
      const u = lista.find((x) => x.email === email);
      idFinal = u?.userAccountId ?? u?.id ?? null;
    }
  }
  if (!idFinal) { log('•', `usuario ${email}: no se pudo resolver su id`); continue; }
  usuarios.set(email, idFinal);

  const perfilId = await crear(`perfil ${rol.code}`, '/api/v1/profiles', {
    tenantId, userId: idFinal, roleId, branchId: null,
  }, (d) => d.profileId ?? d.id);

  perfiles.set(rol.code, perfilId);
}

// Los perfiles que ya existían no devuelven id al crearlos: se resuelven del catálogo, igual que
// los usuarios. Sin esto, una reejecución no puede asignar plantillas a lo que ya está.
if ([...perfiles.values()].some((v) => !v)) {
  const cat = await listarTodo('/api/v1/profiles');
  const todos = cat.items;
  if (Array.isArray(todos)) {
    for (const rol of SPEC.roles ?? []) {
      if (perfiles.get(rol.code)) continue;
      const roleId = roles.get(rol.code);
      const userId = usuarios.get(correoDe(rol.code));
      const hallado = todos.find((x) => x.roleId === roleId && x.userId === userId);
      if (hallado) perfiles.set(rol.code, hallado.profileId ?? hallado.id);
    }
  }
}

// ── 8.b El sujeto multi-perfil: un usuario con DOS perfiles de rol distinto ──
//
// Sin él, la Fase 2 no se puede probar: cualquier escenario que compare «lo que ve con un perfil»
// contra «lo que ve con otro» pasa en verde sin comparar nada, porque no hay a quién cambiarle el
// sombrero. Es lo que G-209 denunciaba, y lo que el carril E necesita para no ser decorativo.
//
// Se elige AUDITOR + EQUIPO a propósito: uno es de solo lectura y el otro no, así que sus grafos
// DEBEN diferir. Dos roles parecidos harían pasar la prueba sin demostrar nada.
//
// Va ANTES de reconciliar duplicados, y no después como estaba: creado más tarde, la reconciliación
// ya había pasado y no lo veía, así que cada ejecución dejaba OTRO perfil EQUIPO para el mismo
// usuario. El grafo llegaba con tres perfiles donde la prueba espera dos, y el carril E fallaba por
// culpa del cargador, no del sistema.
const SUJETO_MULTIPERFIL = MUESTRA?.multiProfileSubject
  ? { correo: correoDe(MUESTRA.multiProfileSubject.role), rolExtra: MUESTRA.multiProfileSubject.extraRole }
  : null;
let perfilExtraDelSujeto = null;
if (SUJETO_MULTIPERFIL) {
  const userId = usuarios.get(SUJETO_MULTIPERFIL.correo);
  const roleId = roles.get(SUJETO_MULTIPERFIL.rolExtra);
  if (!userId || !roleId) {
    log('•', `sujeto multi-perfil: falta ${!userId ? 'la cuenta' : 'el rol ' + SUJETO_MULTIPERFIL.rolExtra}`);
  } else {
    perfilExtraDelSujeto = await crear(
      `perfil extra ${SUJETO_MULTIPERFIL.rolExtra} para ${SUJETO_MULTIPERFIL.correo}`,
      '/api/v1/profiles', { tenantId, userId, roleId, branchId: null }, (d) => d.profileId ?? d.id);

    if (!perfilExtraDelSujeto) {
      const cat = await listarTodo('/api/v1/profiles');
      const h = cat.items.find((x) => x.roleId === roleId && x.userId === userId);
      perfilExtraDelSujeto = h?.profileId ?? h?.id ?? null;
    }
  }
}

// ── 8.c Un perfil por rol: reconciliación de duplicados ─────────────────────
//
// `POST /profiles` no rechazaba el duplicado, así que cada reejecución dejaba OTRO perfil del mismo
// usuario y rol. La plantilla se asigna a UNO, de modo que el resto quedaba con cero concesiones —y
// el selector de perfil los ofrece indistinguibles: elegir el vacío es entrar sin permisos—. La
// guarda ya vive en la API; esto limpia lo que quedó de antes, que la guarda no puede deshacer.
//
// Se CONSERVA el que tiene concesiones y se desactivan los demás. Desactivar y no borrar: un perfil
// es historia de acceso, y borrarlo se lleva por delante la trazabilidad de quién pudo qué.
//
// Va ANTES de publicar y asignar, no después. Puesto al final, la asignación ya había apuntado al
// perfil que esto desactiva, y la verificación pedía la vista previa de un perfil inactivo: HTTP
// 400 en dos roles, con todo lo demás correcto. Reconciliar y luego trabajar sobre lo reconciliado.
//
// Solo con `sampleUsers`: esta reconciliación desactiva perfiles duplicados de la suite, y contra
// un entorno ya poblado tocaría perfiles que este cargador no creó. Limpiar lo propio es correcto;
// desactivar lo ajeno, no.
if (MUESTRA) {
  const cat = await listarTodo('/api/v1/profiles');
  const todos = cat.items;
  const delaSuite = todos.filter((x) => x.systemSuiteId === suiteId && x.isActive !== false);

  const porUsuarioYRol = new Map();
  for (const p of delaSuite) {
    const clave = `${p.userId}|${p.roleId}`;
    if (!porUsuarioYRol.has(clave)) porUsuarioYRol.set(clave, []);
    porUsuarioYRol.get(clave).push(p);
  }

  let desactivados = 0;
  for (const [clave, grupo] of porUsuarioYRol) {
    if (grupo.length < 2) continue;
    // El de más concesiones es el bueno; a igualdad, el primero. Nunca se desactivan todos.
    const [conservado, ...sobrantes] = [...grupo].sort(
      (a, b) => (b.permissionCount ?? 0) - (a.permissionCount ?? 0));

    for (const s of sobrantes) {
      const r = await api('POST', `/api/v1/profiles/${s.profileId}/deactivate`, {});
      if (r.ok) { desactivados += 1; }
      else { cuenta.fallo += 1; log('✖', `no se pudo desactivar el perfil duplicado ${s.profileId} → HTTP ${r.status}`); }
    }
    // El mapa de perfiles se reapunta al superviviente: si se quedara con el desactivado, todo lo
    // que viene después —asignar la plantilla, pedir la vista previa— trabajaría sobre un perfil
    // que ya no existe para el sistema.
    for (const [rolCode, id] of perfiles) {
      if (sobrantes.some((x) => x.profileId === id)) perfiles.set(rolCode, conservado.profileId);
    }
    if (sobrantes.some((x) => x.profileId === perfilExtraDelSujeto)) {
      perfilExtraDelSujeto = conservado.profileId;
    }

    log('•', `${clave.split('|')[1].slice(0, 8)}: ${grupo.length} perfiles, se conserva ${conservado.profileId.slice(0, 8)} (${conservado.permissionCount ?? 0} concesiones)`);
  }
  if (desactivados) log('✔', `${desactivados} perfiles duplicados desactivados`);
}

// ── 8.b Activación de las cuentas ───────────────────────────────────────────
//
// Una cuenta recién creada nace `Pending`, y una cuenta `Pending` NO puede entrar: el login
// devuelve `AUTH_005` («su cuenta no está activa»). Sin este paso el guion deja ocho usuarios con
// contraseña y ninguno capaz de iniciar sesión, que es la forma más cara de fallar — todo responde
// 201 y nada funciona.
//
// Faltaba, y el efecto fue exactamente ese: el 2026-08-02 se activaron a mano contra la instancia
// local, así que la carga «funcionaba» allí y volvía a fallar en cualquier otra instancia. Un
// aprovisionamiento que depende de un arreglo manual no aprovisiona nada; es un guion a medias con
// una nota en la cabeza de alguien.
//
// Idempotente: activar una cuenta ya activa devuelve un error que se cuenta como «ya existía».
for (const [correo, id] of usuarios) {
  await crear(`activación de ${correo}`, `/api/v1/user-accounts/${id}/activate`, {}, null,
    /cannot_activate|already_active/i);
}
log('✔', `${usuarios.size} cuentas activadas`);

// ── 9. Credencial local de cada usuario ─────────────────────────────────────
// Sin contraseña no hay login, y sin login no se puede comprobar lo único que importa: que la
// interfaz del satélite cambie según el rol. La contraseña es DETERMINISTA por usuario para que
// la prueba automatizada la derive sin que nadie tenga que anotarla en ningún sitio.
// El patrón también viene de la spec, por la misma razón que el correo: las pruebas E2E lo
// reconstruyen por su cuenta. `{Nombre}` es la primera etiqueta del correo, capitalizada.
const CLAVE_DE = (correo) =>
  (MUESTRA?.passwordPattern ?? '{Nombre}.invalida')
    .replace('{Nombre}', correo.split('@')[0].split('.')[0].replace(/^./, (c) => c.toUpperCase()));

for (const [correo, id] of usuarios) {
  await crear(`credencial de ${correo}`, `/api/v1/user-accounts/${id}/passwords`, {
    userAccountId: id, password: CLAVE_DE(correo),
  });
}
log('✔', `${usuarios.size} credenciales locales procesadas`);

// ── 9.b Perfiles sobre usuarios YA existentes ────────────────────────────────
//
// Es lo que permite provisionar un sistema en un entorno real, donde las cuentas no las crea este
// guion. La spec declara `profiles: [{ userEmailEnv, role }]` y el correo llega por variable de
// entorno, no escrito en el archivo: quién administra un sistema es dato del entorno, no del
// contrato del sistema.
//
// Alimenta el MISMO mapa `perfiles` que el paso 8, porque el paso 10 itera ese mapa para asignar
// las plantillas. Sin esto, un sistema sin `sampleUsers` publicaría sus plantillas y no se las
// asignaría a nadie: todo 2xx y cero permisos, el fallo que este guion ya pagó dos veces.
// UN PERFIL POR (usuario, rol, SUCURSAL) —las tres, no dos—, que es la unicidad que impone el
// dominio (`CreateProfileCommandHandler`). De ahí que esto acumule en una LISTA y no en el mapa
// `perfiles`, que está indexado por código de rol.
//
// El mapa servía mientras el único padrón eran las cuentas de ejemplo, que son una por rol. Con un
// padrón de personas deja de servir en el primer caso real: dos jefes de proyecto comparten el rol
// `JEFE`, así que `perfiles.set('JEFE', …)` guarda al segundo y PIERDE al primero. El paso 10
// itera ese mapa para asignar plantillas, de modo que el perfil perdido se quedaba sin ninguna:
// creado, activo, ofrecido en el selector y con cero concesiones. Todo 2xx, y una persona que entra
// y no ve nada. Lo mismo, multiplicado, con los seis `ADMIN_UMS` del padrón.
const perfilesDelPadron = [];      // {rolCode, profileId, correo, sucursal}
const cuentasDelPadron = new Map(); // correo → userId

// El catálogo de perfiles se lee UNA VEZ y se reutiliza. Antes se releía dentro del bucle por cada
// perfil que ya existiera: con 6 personas × 2 sucursales son 12 lecturas de un catálogo paginado
// —cientos de peticiones— contra un limitador que en UAT concede 250 por minuto.
let catalogoPerfiles = null;
const perfilesExistentes = async () => {
  if (!catalogoPerfiles) catalogoPerfiles = (await listarTodo('/api/v1/profiles')).items ?? [];
  return catalogoPerfiles;
};

// Sobre `--crear-cuentas` (declarada arriba con el resto de banderas): crear implica DOS pasos más,
// y omitirlos deja el trabajo a medias de una forma que responde 2xx —la cuenta nace `Pending`, el
// login devuelve AUTH_005, y nace sin contraseña—. Por eso una cuenta recién creada se activa y
// recibe credencial SIEMPRE, sin depender de `--fijar-claves`: ahí no hay nada que pisar.
const cuentasCreadas = new Set();
const cuentasResueltas = new Map();   // correo → userId | null

/**
 * Resuelve la cuenta de una persona, creándola si se pidió. Cachea por correo: una persona puede
 * aparecer en varias filas del padrón, y sin caché cada fila repetiría la búsqueda —y, peor,
 * reintentaría el alta— sobre la misma cuenta.
 */
async function resolverCuenta(correo, homeBranch) {
  if (cuentasResueltas.has(correo)) return cuentasResueltas.get(correo);

  // BUSCAR NO BASTA: hay que comprobar que la cuenta hallada se puede USAR.
  //
  // El listado y la lectura por id no siempre coinciden. Medido en UAT el 2026-08-17 con
  // `mhernandez@evolith.example`: el catálogo la devolvía como `Active` y `GET /user-accounts/{id}`
  // respondía 404, así que su perfil, su activación y su credencial fallaban las tres con un
  // «record does not exist» que parecía hablar de otra cosa. Con `--crear-cuentas` puesto, además,
  // el guion NO la creaba —creía que ya existía— y la persona se quedaba fuera en silencio.
  //
  // Se comprueba con una lectura por id, que es exactamente lo que hará el manejador después.
  const buscar = async () => {
    const b = await api('GET', `/api/v1/user-accounts?page=1&search=${encodeURIComponent(correo)}&pageSize=20`);
    const lista = b.datos?.items ?? b.datos?.data ?? b.datos ?? [];
    const candidatos = Array.isArray(lista) ? lista.filter((x) => x.email === correo) : [];
    if (candidatos.length === 0) return null;

    // EL PRIMERO QUE SE PUEDA RESOLVER, no el primero que coincida.
    //
    // Un mismo correo puede devolver VARIOS registros: el roto que no se resuelve por id y el que
    // este guion creó después. Tomando el primero de la lista, si el roto venía delante, la cuenta
    // buena quedaba invisible: el alta respondía duplicado, la relectura volvía a traer el roto y
    // la persona se quedaba fuera. Ocurrió con `mhernandez@evolith.example` el 2026-08-17: quedó con
    // sus dos perfiles en SDLC y ninguno en UMS, en la misma ejecución.
    const rotos = [];
    for (const c of candidatos) {
      const id = c.userAccountId ?? c.id;
      if (!id) continue;
      const directa = await api('GET', `/api/v1/user-accounts/${id}`);
      if (directa.ok) return id;
      rotos.push(`${id} → HTTP ${directa.status}`);
    }

    log('✖', `cuenta ${correo}: el catálogo devuelve ${candidatos.length} registro(s) y NINGUNO se `
      + `resuelve por id (${rotos.join(', ')}). Es inutilizable: su perfil, su activación y su `
      + `credencial fallarán. Suele ser una cuenta de OTRO inquilino visible por privilegio de `
      + `administrador interno, o una baja lógica que el catálogo no filtra.`);
    return null;
  };

  let userId = await buscar();

  if (!userId && CREAR_CUENTAS && !SIMULACRO) {
    // `branchId` es la sede DE LA CUENTA, distinta del alcance de cada perfil. Va a `null` salvo
    // que la fila declare `homeBranch`: el padrón asigna dos sucursales a cada persona, y elegir
    // una de las dos como su sede sería inventar un dato que nadie declaró.
    const branchId = homeBranch ? (sucursales.get(homeBranch) ?? null) : null;

    userId = await crear(`cuenta ${correo}`, '/api/v1/user-accounts', {
      tenantId, branchId, email: correo, category: 'Internal',
      identityReference: null, identityReferenceType: null,
    }, (d) => d.userAccountId ?? d.id);

    // El alta responde duplicado si la cuenta existía y la búsqueda no la halló —correo con otra
    // caja, índice al día—. Se relee antes de darla por perdida.
    if (userId) cuentasCreadas.add(correo);
    else userId = await buscar();
  } else if (!userId && CREAR_CUENTAS && SIMULACRO) {
    log('•', `cuenta ${correo}: NO existe, se crearía — simulacro, no se escribe`);
  }

  cuentasResueltas.set(correo, userId);
  return userId;
}

for (const decl of SPEC.profiles ?? []) {
  const correo = decl.userEmailEnv ? process.env[decl.userEmailEnv] : decl.userEmail;
  const roleId = roles.get(decl.role);

  if (!correo) { log('•', `perfil ${decl.role}: sin correo (${decl.userEmailEnv} no definida)`); continue; }
  if (!roleId) { log('•', `perfil ${decl.role}: el rol no existe en la suite`); continue; }

  // El inquilino se declara por fila y se COMPRUEBA; no se usa para nada más. El cargador trabaja
  // siempre contra el inquilino de la sesión, así que una fila que nombrara otro no se aplicaría
  // donde dice: se aplicaría igual al de la sesión, sin avisar. Comprobarlo convierte un padrón mal
  // copiado en un fallo visible en vez de en seis perfiles creados en el inquilino equivocado.
  if (decl.tenant && decl.tenant !== TENANT) {
    log('✖', `perfil ${decl.role} de ${correo}: la fila declara el inquilino ${decl.tenant} y la sesión es de ${TENANT}`);
    cuenta.fallo += 1;
    continue;
  }

  const userId = await resolverCuenta(correo, decl.homeBranch);

  // La cuenta que falta NO es un aviso, es un fallo. Saltársela en silencio dejaba al padrón a
  // medias con exit 0: el guion terminaba en verde y la persona no podía entrar. Quien lea el
  // resumen tiene que ver que su padrón no se aplicó entero.
  if (!userId) {
    if (SIMULACRO && CREAR_CUENTAS) continue;   // ya se anunció el alta; no es un fallo del simulacro
    log('✖', CREAR_CUENTAS
      ? `perfil ${decl.role}: no se pudo crear la cuenta ${correo}`
      : `perfil ${decl.role}: no existe la cuenta ${correo} — usa --crear-cuentas o dala de alta en UMS`);
    cuenta.fallo += 1;
    continue;
  }
  cuentasDelPadron.set(correo, userId);

  // `branches` (varias) o `branch` (una); sin ninguna, `[null]` = perfil sin alcance por sucursal,
  // que es lo que hacía la versión anterior con su `branchId: null` fijo.
  const codigos = decl.branches ?? (decl.branch ? [decl.branch] : [null]);

  for (const codigo of codigos) {
    if (codigo && !sucursales.has(codigo)) {
      log('✖', `perfil ${decl.role} de ${correo}: la sucursal ${codigo} no está en tenant.branches de la spec`);
      cuenta.fallo += 1;
      continue;
    }
    const branchId = codigo ? sucursales.get(codigo) : null;
    const donde = codigo ? ` en ${codigo}` : '';

    let profileId = await crear(`perfil ${decl.role} de ${correo}${donde}`, '/api/v1/profiles',
      { tenantId, userId, roleId, branchId }, (d) => d.profileId ?? d.id);

    // En reejecución el alta devuelve duplicado; se resuelve del catálogo para que el paso 10
    // encuentre a quién asignarle la plantilla. El cotejo incluye la SUCURSAL: sin ella, los dos
    // perfiles de una misma persona y rol son indistinguibles y ambos resolvían al mismo id, de
    // modo que uno recibía la plantilla dos veces y el otro ninguna.
    if (!profileId) {
      const h = (await perfilesExistentes()).find((x) => x.roleId === roleId
        && x.userId === userId
        && (x.branchId ?? null) === branchId);
      profileId = h?.profileId ?? h?.id ?? null;
    }

    if (profileId) perfilesDelPadron.push({ rolCode: decl.role, profileId, correo, sucursal: codigo });
    else if (!SIMULACRO) log('•', `perfil ${decl.role} de ${correo}${donde}: no se pudo resolver su id`);
  }
}

if (perfilesDelPadron.length) log('✔', `${perfilesDelPadron.length} perfiles del padrón sobre ${cuentasDelPadron.size} cuentas`);

// ── 9.c Credencial y activación de las cuentas del padrón (`--fijar-claves`) ─
//
// DETRÁS DE UNA BANDERA, Y NUNCA POR OMISIÓN. Fijar la contraseña es una ROTACIÓN: el endpoint es
// «set or rotate», así que pisa la que la persona tuviera. Una reejecución rutinaria del cargador
// —que se hace a menudo, para aplicar una corrección de permisos— dejaría fuera de su propia cuenta
// a todo el padrón sin que nadie lo hubiera pedido. Se hace cuando se pide y se sabe por qué.
//
// La contraseña es DETERMINISTA para poder entregarla en una tabla al final. El patrón vive AQUÍ y
// no en la spec, al contrario que el de `sampleUsers`: una persona del padrón está en las DOS specs
// —perfil en SDLC y perfil en UMS— pero tiene una sola cuenta y una sola contraseña. Con el patrón
// duplicado en dos archivos, el día que difieran la segunda carga pisa la clave de la primera y la
// tabla que esa primera imprimió pasa a ser mentira. Un solo sitio no puede divergir de sí mismo.
const CLAVE_PADRON_PATRON = process.env.UMS_PADRON_CLAVE_PATRON || 'Evolith.{Nombre}.2026!';
const CLAVE_PADRON_DE = (correo) =>
  CLAVE_PADRON_PATRON.replace('{Nombre}', correo.split('@')[0].replace(/^./, (c) => c.toUpperCase()));

const credenciales = [];   // {correo, clave, nueva, fijada}

// A quién se le pone credencial, y por qué son dos criterios distintos y no uno:
//
//  · A las cuentas RECIÉN CREADAS, siempre. No hay nada que pisar —nacieron sin contraseña— y sin
//    esto el guion las dejaría inservibles: creadas, con perfil, con plantilla, y sin poder entrar.
//  · A las que YA EXISTÍAN, solo con `--fijar-claves`. Ahí sí se pisa la contraseña de una persona.
const aCredencial = new Map();
for (const [correo, id] of cuentasDelPadron) {
  if (cuentasCreadas.has(correo) || FIJAR_CLAVES) aCredencial.set(correo, id);
}

if (cuentasCreadas.size > 0) log('✔', `${cuentasCreadas.size} cuentas creadas: ${[...cuentasCreadas].join(', ')}`);

if (aCredencial.size > 0) {
  const rotadas = [...aCredencial.keys()].filter((c) => !cuentasCreadas.has(c)).length;
  if (rotadas > 0) log('•', `se van a ROTAR las contraseñas de ${rotadas} cuentas que ya existían (--fijar-claves)`);

  for (const [correo, id] of aCredencial) {
    // Activar primero: una cuenta `Pending` puede tener contraseña y aun así no entra —el login
    // responde AUTH_005—, y entregar una credencial que no deja pasar es peor que no entregarla.
    await crear(`activación de ${correo}`, `/api/v1/user-accounts/${id}/activate`, {}, null,
      /cannot_activate|already_active/i);

    const clave = CLAVE_PADRON_DE(correo);
    const puesta = await crear(`credencial de ${correo}`, `/api/v1/user-accounts/${id}/passwords`, {
      userAccountId: id, password: clave,
    });

    // `=== true` y no `!== false`: `crear()` devuelve `true` al conseguirlo y **null** en los dos
    // casos restantes —«ya existía» y fallo—, así que `!== false` daba siempre verdadero y la
    // tabla juraba haber fijado credenciales que habían respondido 404. Ocurrió el 2026-08-17:
    // informó de «6 contraseñas ROTADAS» cuando una de las seis no llegó a fijarse.
    credenciales.push({ correo, clave, nueva: cuentasCreadas.has(correo), fijada: puesta === true });
  }
  const puestas = credenciales.filter((c) => c.fijada).length;
  log('✔', `${puestas} de ${aCredencial.size} credenciales fijadas`);
}

// Las del padrón a las que NO se tocó la credencial entran igual en la tabla, con la columna
// vacía. Omitirlas dejaría un padrón de seis personas con una tabla de dos, y quien la lea
// concluiría que las otras cuatro no se cargaron.
for (const correo of cuentasDelPadron.keys()) {
  if (!aCredencial.has(correo)) credenciales.push({ correo, clave: null, nueva: false, fijada: false });
}

// ── 10. Publicación de plantillas y asignación a los perfiles ───────────────
//
// LAS DOS OPERACIONES SON IMPRESCINDIBLES, y ninguna estaba. Una plantilla en `Draft` no concede
// nada, y una plantilla publicada que no está ASIGNADA a un perfil tampoco: el grafo llega con
// `scopes: []` y la interfaz del satélite sale vacía para todos los roles.
//
// Es el mismo patrón que la activación de cuentas: se hizo a mano contra la instancia local el
// 2026-08-02 —«publicar las plantillas», «asignarlas a los perfiles»— y nunca entró al guion, de
// modo que cargar en una instancia nueva producía 893 objetos y cero permisos. El síntoma es
// especialmente traicionero porque TODO responde 2xx.

let publicadas = 0;
for (const [rolCode, plantilla] of plantillas) {
  if (plantilla.status === 'Published') { cuenta.existente += 1; continue; }
  const ok = await crear(`publicación de ${rolCode}`, `/api/v1/permission-templates/${plantilla.id}/publish`, {});
  if (ok) publicadas += 1;
}
log('✔', `${publicadas} plantillas publicadas`);

// Las cuentas de ejemplo (mapa, una por rol) y el padrón (lista, varias por rol) se asignan por el
// MISMO camino: la plantilla se asigna a un PERFIL, y a este paso le da igual de dónde salió. Se
// unifican en una lista para que no exista una segunda ruta de asignación que pueda quedarse atrás
// cuando se toque esta —que es como el padrón se quedó sin plantillas la primera vez—.
const asignaciones = [
  ...[...perfiles].filter(([, id]) => id).map(([rolCode, profileId]) => ({ rolCode, profileId, etiqueta: rolCode })),
  ...perfilesDelPadron.map((p) => ({ ...p, etiqueta: `${p.rolCode} de ${p.correo}${p.sucursal ? ` en ${p.sucursal}` : ''}` })),
];

let asignadas = 0;
for (const { rolCode, profileId, etiqueta } of asignaciones) {
  const plantilla = plantillas.get(rolCode);
  if (!profileId || !plantilla) {
    log('•', `sin asignar ${etiqueta}: ${!profileId ? 'perfil' : 'plantilla'} no resuelto`);
    continue;
  }
  const ok = await crear(`asignación ${etiqueta}`, `/api/v1/profiles/${profileId}/templates/${plantilla.id}`, {});
  if (ok) asignadas += 1;
}
log('✔', `${asignadas} plantillas asignadas a ${asignaciones.length} perfiles`);

// ── 7.5 Depreciación de la versión anterior (solo con `--nueva-version`) ─────
// VA AL FINAL Y NO ANTES, y el orden es la mitad del asunto: si se deprecara al crear la versión
// nueva, entre ese instante y la asignación los perfiles seguirían apuntando a una plantilla
// deprecada. Aquí ya están apuntando a la nueva.
//
// Se DEPRECA, no se borra. `DELETE` acepta Deprecated y arrastra sus ítems, pero esta plantilla
// estuvo asignada: sus filas en `ProfilePermissions` son el rastro de qué pudo hacer cada perfil
// mientras estuvo viva, y esa es justo la pregunta que hace una auditoría. Una plantilla deprecada
// no se ofrece en el catálogo y no concede nada nuevo; ya es un estado terminal correcto.
if (NUEVA_VERSION && anteriores.size > 0) {
  let deprecadas = 0;
  for (const [rolCode, vieja] of anteriores) {
    if (vieja.status === 'Deprecated') { cuenta.existente += 1; continue; }
    const ok = await crear(
      `depreciación de ${rolCode} v${vieja.version}`,
      `/api/v1/permission-templates/${vieja.id}/deprecate`,
      {},
    );
    if (ok) deprecadas += 1;
  }
  log('✔', `${deprecadas} plantillas anteriores deprecadas`);
}

// La del sujeto multi-perfil va aquí y no antes: asignar exige la plantilla PUBLICADA, y la
// publicación acaba de ocurrir unas líneas más arriba.
if (perfilExtraDelSujeto) {
  const plantillaExtra = plantillas.get(SUJETO_MULTIPERFIL.rolExtra);
  if (plantillaExtra) {
    await crear(`asignación extra ${SUJETO_MULTIPERFIL.rolExtra}`,
      `/api/v1/profiles/${perfilExtraDelSujeto}/templates/${plantillaExtra.id}`, {});
    log('✔', `sujeto multi-perfil listo: ${SUJETO_MULTIPERFIL.correo} con AUDITOR + ${SUJETO_MULTIPERFIL.rolExtra}`);
  } else {
    log('•', `sujeto multi-perfil: sin plantilla para ${SUJETO_MULTIPERFIL.rolExtra}`);
  }
}

// EL SUJETO MULTI-PERFIL. Un usuario con DOS perfiles de rol DISTINTO en el mismo sistema.
//
// Sin él, la Fase 2 no se puede probar: cualquier escenario que compare «lo que ve con un perfil»
// contra «lo que ve con otro» pasa en verde sin comparar nada, porque no hay a quién cambiarle el
// sombrero. Es lo que G-209 denunciaba, y lo que el carril E necesita para no ser decorativo.
//
// Se elige AUDITOR + EQUIPO a propósito: uno es de solo lectura y el otro no, así que sus grafos
// DEBEN diferir. Dos roles parecidos harían pasar la prueba sin demostrar nada.
console.log(`\nsuite ${SPEC.suite.code} = ${suiteId}`);

// El recuento por plantilla se imprime siempre: es la única forma de ver de un vistazo si un
// comodín se expandió de más —un rol restringido con tantas concesiones como el administrador es
// un error, aunque todos los HTTP hayan sido 201—.
console.log('\nconcesiones por plantilla:');
for (const r of resumenPlantillas.sort((a, b) => b.items - a.items)) {
  console.log(`  ${r.codigo.padEnd(20)} ${String(r.items).padStart(4)} ítems${r.pendientes.length ? `  · ${r.pendientes.length} pendiente(s)` : ''}`);
}
const sinCargar = resumenPlantillas.flatMap((r) => r.pendientes.map((p) => `${r.codigo}: ${p}`));
if (sinCargar.length) {
  console.log('\nno cargado (queda como deuda explícita, no como silencio):');
  for (const p of sinCargar) console.log(`  ✖ ${p}`);
}

// ── El padrón, en una tabla ─────────────────────────────────────────────────
//
// Una fila por PERSONA y no por perfil: la persona tiene una cuenta y una contraseña, y varios
// perfiles colgando de ella. Una tabla por perfil repetiría la credencial cuatro veces por persona
// —dos sucursales × dos sistemas— e invitaría a leer que son credenciales distintas.
//
// Se imprime por la SALIDA ESTÁNDAR y no se escribe en ningún archivo. Escribirlo dejaría las
// contraseñas del padrón en el disco del repositorio, a un `git add .` de distancia de quedar
// publicadas para siempre; quien las necesite en un archivo redirige la salida a donde deba estar,
// que es una decisión suya y no de este guion.
if (perfilesDelPadron.length > 0) {
  const porPersona = new Map();
  for (const p of perfilesDelPadron) {
    if (!porPersona.has(p.correo)) porPersona.set(p.correo, []);
    porPersona.get(p.correo).push(p);
  }

  const clave = new Map(credenciales.map((c) => [c.correo, c]));
  const anchoCorreo = Math.max(7, ...[...porPersona.keys()].map((c) => c.length));
  const anchoClave = Math.max(10, ...credenciales.map((c) => (c.clave ?? '(sin fijar)').length));

  console.log(`\npadrón de ${SPEC.suite.code} — ${porPersona.size} personas, ${perfilesDelPadron.length} perfiles:`);
  console.log(`  ${'USUARIO'.padEnd(anchoCorreo)}  ${'CONTRASEÑA'.padEnd(anchoClave)}  ${'CUENTA'.padEnd(8)}  TENANT   ROL            SUCURSALES`);

  for (const [correo, suyos] of porPersona) {
    const c = clave.get(correo);
    const texto = c?.clave ?? '(conserva la suya)';
    // Distinguir «nueva» de «rotada» no es cosmético: a quien la cuenta se le acaba de crear hay
    // que darle la bienvenida, y a quien ya la tenía hay que avisarle de que la suya dejó de valer.
    const origen = c?.nueva ? 'nueva' : 'existía';
    const porRol = new Map();
    for (const p of suyos) {
      if (!porRol.has(p.rolCode)) porRol.set(p.rolCode, []);
      porRol.get(p.rolCode).push(p.sucursal ?? '—');
    }
    let primera = true;
    for (const [rol, sedes] of porRol) {
      console.log(`  ${(primera ? correo : '').padEnd(anchoCorreo)}  ${(primera ? texto : '').padEnd(anchoClave)}  ${(primera ? origen : '').padEnd(8)}  ${(primera ? TENANT : '').padEnd(7)}  ${rol.padEnd(13)}  ${sedes.join(', ')}`);
      primera = false;
    }
  }

  // Solo cuenta lo que de verdad se fijó. Una tabla que enumera contraseñas que no llegaron a
  // ponerse es peor que no imprimir ninguna: se reparten y no dejan entrar.
  const conClave = credenciales.filter((c) => c.clave && c.fijada);
  const rotadas = conClave.filter((c) => !c.nueva);
  const fallidas = credenciales.filter((c) => c.clave && !c.fijada).map((c) => c.correo);

  if (conClave.length === 0) {
    console.log('\n  La columna de contraseña va vacía a propósito: sin `--fijar-claves` este guion no');
    console.log('  toca credenciales, y una contraseña ya puesta no se puede leer —está hasheada—.');
  } else {
    if (fallidas.length) console.log(`\n  ✖ sin credencial fijada: ${fallidas.join(', ')}`);
    if (rotadas.length) {
      console.log(`\n  ${rotadas.length} contraseña(s) ROTADAS: la anterior de esas cuentas ya no sirve.`);
    }
    console.log('  Entrega estas claves por un canal que no sea este registro, y no las pegues en el');
    console.log('  repositorio.');
  }

  // El padrón es de personas, así que su cobertura se declara junto a la tabla: quien la lea tiene
  // que poder ver que falta alguien sin ir a buscarlo en el registro de más arriba.
  const declaradas = new Set((SPEC.profiles ?? [])
    .map((d) => (d.userEmailEnv ? process.env[d.userEmailEnv] : d.userEmail))
    .filter(Boolean));
  const ausentes = [...declaradas].filter((c) => !porPersona.has(c));
  if (ausentes.length) console.log(`\n  ✖ declaradas en la spec y NO aplicadas: ${ausentes.join(', ')}`);
}

console.log(`\ncreado: ${cuenta.creado} · ya existía: ${cuenta.existente} · fallos: ${cuenta.fallo}`);
process.exit(cuenta.fallo > 0 ? 1 : 0);
