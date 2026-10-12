#!/usr/bin/env node
/**
 * robot-gating.mjs — carril C del arnes: la interfaz del Tablero SDLC concuerda con el grafo.
 *
 * QUE SE AFIRMA, Y POR QUE ASI. La asercion es de CONCORDANCIA, no de presencia. Para cada perfil,
 * el conjunto de rutas que el servidor del Tablero deja pasar debe ser EXACTAMENTE el conjunto de
 * rutas cuyo scope esta concedido en su grafo. Ni una mas ni una menos.
 *
 * «El administrador ve mas que el auditor» pasaria hoy en verde sobre dos interfaces vacias, y ese
 * es justo el falso positivo contra el que existe esta prueba. Igualdad de conjuntos falla con quien
 * ve de mas —el agujero— y con quien ve de menos —la funcionalidad rota—, que son dos defectos
 * distintos y los dos importan.
 *
 * POR QUE NO ES TAUTOLOGICO comparar el mapa del servidor con el grafo. El mapa es una DECLARACION;
 * el codigo de estado es COMPORTAMIENTO. evolith_arch#G-291 fue exactamente esa distancia: `menuPermitido` y
 * `scopePermitido` existian, estaban probados, y no gateaban ningun endpoint — ocultar un boton no
 * impedia el POST. Un mapa que nadie aplica sale verde en cualquier prueba que lea el mapa.
 *
 * CUATRO BARRIDOS, cada uno contra una fuente distinta:
 *
 *   1. DERIVA — cada ruta que `sdlc-suite.json` declara en UMS tiene entrada en el mapa del
 *      Tablero. Compara dos repositorios: si UMS publica una ruta que el Tablero no protege, el
 *      «denegar por defecto» la bloquea y la funcionalidad muere en silencio.
 *   2. CONCORDANCIA — por perfil y por ruta, el estado HTTP real frente al scope del grafo.
 *   3. MONOTONIA — el conjunto permitido del auditor es subconjunto propio del del administrador,
 *      y ninguna ruta de escritura le pasa. Es la consecuencia que el propio mapa declara.
 *   4. CONTROL NEGATIVO — juzgar a un perfil restringido con la expectativa del administrador TIENE
 *      que producir discordancias. Sin esto, un servidor que dejara pasar todo saldria verde.
 *
 * LO QUE ESTE ROBOT NO HACE, dicho aqui y no al final:
 *
 *   · No abre un navegador. Verifica el gate del SERVIDOR, que es el que manda; que el cliente
 *     ademas oculte el boton es deseable y no sustituible por esto.
 *   · En las rutas de ESCRITURA solo ejerce el lado DENEGADO. Completar el lado permitido crearia
 *     o borraria datos reales en cada corrida. La consecuencia se declara en el resumen: de una
 *     ruta de escritura este robot demuestra que se cierra a quien no debe, no que se abre a quien
 *     debe.
 *   · Las rutas con `:param` se ejercen con identificadores DESCUBIERTOS cuando se puede y se
 *     omiten cuando no. Una omision cuenta como omision, nunca como acierto.
 *
 * Uso:
 *   node src/tests/e2e-functional/robosoft/integracion-tablero/robot-gating.mjs \
 *     --tablero http://localhost:4317 [--perfiles admin.sdlc.sdlc,auditor.sdlc] [--json informe.json]
 *
 *   El mapa de permisos del Tablero vive en SU repositorio: se indica con TABLERO_REPO=/ruta/a/
 *   evolith-tablero-sdlc, con TABLERO_MAPA_PERMISOS=/ruta/al/mapa-permisos.js o con --mapa.
 *
 * Salida: 0 si todo concuerda; 1 al primer incumplimiento, con lo medido.
 */

import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { resolve, dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const arg = (n, d = null) => {
  const i = process.argv.indexOf(`--${n}`);
  return i !== -1 ? process.argv[i + 1] : d;
};

const TABLERO = (arg('tablero', 'http://localhost:4317')).replace(/\/$/, '');
const INFORME = arg('json');
const AQUI    = dirname(fileURLToPath(import.meta.url));

/*
 * Las dos fuentes independientes.
 *
 * La spec vive en ESTE repositorio: es lo que UMS carga. El mapa vive en el Tablero, que ya NO
 * esta dentro de `evolith_arch`: se mudo a su propio repositorio y la ruta fija dejo el carril
 * abortando desde entonces (G-278). Por eso deja de asumirse la vecindad de dos clones y se
 * resuelve, en este orden, por `--mapa`, por `TABLERO_MAPA_PERMISOS`, por `TABLERO_REPO` y solo
 * al final por el hermano `../evolith-tablero-sdlc`. Si no aparece en ninguno, el robot DICE donde
 * busco y termina, en vez de saltarse el barrido de deriva y quedarse verde a medias.
 */
const SPEC = JSON.parse(readFileSync(resolve(AQUI, '../../../../provisioning/sdlc/sdlc-suite.json'), 'utf-8'));

const RELATIVA_EN_EL_TABLERO = 'src/server/src/autorizacion/mapa-permisos.js';
const CANDIDATOS_MAPA = [
  arg('mapa'),
  process.env.TABLERO_MAPA_PERMISOS,
  process.env.TABLERO_REPO ? resolve(process.env.TABLERO_REPO, RELATIVA_EN_EL_TABLERO) : null,
  resolve(AQUI, '../../../../../../evolith-tablero-sdlc', RELATIVA_EN_EL_TABLERO),
].filter(Boolean);

const RUTA_MAPA = CANDIDATOS_MAPA.find((ruta) => existsSync(ruta)) ?? null;

let MAPA_TABLERO = null;
try {
  if (!RUTA_MAPA) throw new Error('no existe en ninguna de las rutas probadas');
  ({ MAPA: MAPA_TABLERO } = await import(`file://${RUTA_MAPA}`));
} catch (e) {
  console.error(`  ✘ no se pudo cargar el mapa del Tablero: ${e.message}`);
  for (const ruta of CANDIDATOS_MAPA) console.error(`      probado: ${ruta}`);
  console.error('    Indica el repositorio del Tablero con TABLERO_REPO=/ruta/a/evolith-tablero-sdlc '
              + '(o la ruta del mapa con --mapa / TABLERO_MAPA_PERMISOS).');
  console.error('    El barrido de deriva compara DOS repositorios; sin el segundo no hay '
              + 'comparacion, y correr solo los otros dos daria un verde que no cubre lo que dice.');
  process.exit(2);
}

// ── Aserciones ──────────────────────────────────────────────────────────────

let fallos = 0, pasos = 0;
const detalle = [];

function comprobar(etiqueta, condicion, medido) {
  pasos++;
  if (!condicion) fallos++;
  console.log(`  ${condicion ? '✔' : '✘'} ${etiqueta}${medido ? `  ·  ${medido}` : ''}`);
  detalle.push({ etiqueta, ok: Boolean(condicion), medido: medido ?? null });
  return condicion;
}

// ── La spec, aplanada ───────────────────────────────────────────────────────

/** Todas las opciones de la suite con sus acciones y sus rutas, sin presuponer profundidad. */
function opcionesDeLaSpec() {
  const salida = [];
  const rec = (nodos) => {
    for (const n of nodos ?? []) {
      if (n.code) salida.push(n);
      rec(n.submenus ?? n.options ?? n.children ?? []);
    }
  };
  for (const m of SPEC.modules ?? []) rec(m.menus ?? []);
  return salida.filter((n) => Array.isArray(n.routes) && n.routes.length > 0);
}

const OPCIONES = opcionesDeLaSpec();
const RUTAS_DE_LA_SPEC = [...new Set(OPCIONES.flatMap((o) => o.routes))];

// ── Sesion en el Tablero ────────────────────────────────────────────────────

// El correo y la clave se DERIVAN de la spec, no se escriben aqui: son el mismo contrato que usa
// el cargador (`sampleUsers.emailPattern` / `passwordPattern`), y duplicarlo en el robot fue lo que
// dejo este carril juzgando a roles que ya no existen (G-284).
const MUESTRA = SPEC.sampleUsers ?? {};
const CORREO_DE_ROL = (codigo) =>
  (MUESTRA.emailPattern ?? '{rol}@invalido.local').replace('{rol}', codigo.toLowerCase().replace(/_/g, '.'));
const CLAVE_DE = (correo) =>
  (MUESTRA.passwordPattern ?? '{Nombre}.invalida')
    .replace('{Nombre}', correo.split('@')[0].split('.')[0].replace(/^./, (c) => c.toUpperCase()));

async function entrar(usuario) {
  const correo = usuario.includes('@') ? usuario : `${usuario}@evolith.example`;
  // La puerta del Tablero cambio de FORMA con la v1: abrir sesion es `POST /api/v1/auth/sesion` y
  // cerrarla es `DELETE` sobre el mismo recurso — ya no hay verbos `login` ni `logout`, y la ruta
  // vieja responde 410. Sin esto el carril entero fallaba en el primer paso, sin llegar a juzgar un
  // solo permiso.
  const r = await fetch(`${TABLERO}/api/v1/auth/sesion`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ username: correo, password: CLAVE_DE(correo) }),
  });
  if (r.status !== 200) return { error: `HTTP ${r.status}` };

  const cookie = (r.headers.getSetCookie?.() ?? [r.headers.get('set-cookie')])
    .filter(Boolean).map((c) => c.split(';')[0]).join('; ');

  // `GET /api/v1/auth/sesion` responde 200 con `autenticado:false` cuando no hay sesion, no 401:
  // es el sondeo de arranque del cliente, no un rechazo.
  const ses = await fetch(`${TABLERO}/api/v1/auth/sesion`, { headers: { Cookie: cookie } });
  const cuerpo = await ses.json();
  if (!cuerpo.autenticado) return { error: 'la sesion no se sostiene' };

  return { cookie, grafo: cuerpo.graph, rol: cuerpo.rol?.code ?? '—' };
}

/** Ejerce una ruta con la sesion dada y devuelve el estado. */
async function tocar(cookie, metodo, ruta) {
  const r = await fetch(`${TABLERO}${ruta}`, {
    method: metodo,
    headers: { Cookie: cookie, 'Content-Type': 'application/json' },
    body: metodo === 'GET' || metodo === 'DELETE' ? undefined : '{}',
  });
  return r.status;
}

/**
 * Sustituye los `:param` por identificadores descubiertos. Devuelve null cuando no hay con que
 * rellenarlos: la ruta se OMITE y se cuenta como omitida, nunca como acierto.
 */
function concretar(patron, ids) {
  const [metodo, camino] = patron.split(' ');
  const segs = camino.split('/');
  for (let i = 0; i < segs.length; i++) {
    if (!segs[i].startsWith(':')) continue;
    const valor = ids[segs[i].slice(1)] ?? ids.generico;
    if (!valor) return null;
    segs[i] = valor;
  }
  return { metodo, ruta: segs.join('/') };
}

// ── Barrido 1 · Deriva entre repositorios ───────────────────────────────────

console.log('\n━━━ Robot de gating · Tablero SDLC ↔ grafo de UMS ━━━');
console.log(`Tablero ${TABLERO}\n`);
console.log('── 1. Deriva: lo que UMS declara ↔ lo que el Tablero protege ──');

const clavesMapa = new Set(Object.keys(MAPA_TABLERO));
const sinProteger = RUTAS_DE_LA_SPEC.filter((r) => !clavesMapa.has(r));

comprobar('toda ruta declarada en sdlc-suite.json tiene entrada en el mapa del Tablero',
  sinProteger.length === 0,
  sinProteger.length === 0
    ? `${RUTAS_DE_LA_SPEC.length} rutas de la spec, ${clavesMapa.size} en el mapa`
    : `sin proteger: ${sinProteger.slice(0, 5).join(' · ')}${sinProteger.length > 5 ? ` …+${sinProteger.length - 5}` : ''}`);

// ── Sesiones ────────────────────────────────────────────────────────────────

// Los sujetos salen de los ROLES DE LA SPEC, ordenados por jerarquia: el primero es el mas
// privilegiado y contra el se mide la monotonia de todos los demas. La lista fija anterior
// —`tech.lead.sdlc`, `directorio.sdlc`— nombraba roles que el modelo renombro, asi que el carril
// fallaba con 401 por sujetos inexistentes y no por deriva de permisos, que es lo que mide (G-284).
// `--perfiles` sigue estando para acotar una corrida a mano.
const PERFILES_POR_DEFECTO = [...(SPEC.roles ?? [])]
  .map((rol, orden) => ({ ...rol, orden }))
  .sort((a, b) => (a.hierarchyLevel ?? 99) - (b.hierarchyLevel ?? 99) || a.orden - b.orden)
  .map((rol) => CORREO_DE_ROL(rol.code));

const PERFILES = (arg('perfiles', PERFILES_POR_DEFECTO.join(',')))
  .split(',').map((s) => s.trim()).filter(Boolean);

console.log('\n── 2. Concordancia por perfil: estado HTTP ↔ scope del grafo ──');

const admin = await entrar(PERFILES[0]);
if (admin.error) {
  console.error(`  ✘ no se pudo entrar como ${PERFILES[0]}: ${admin.error}`);
  process.exit(2);
}

// Identificadores reales para las rutas parametrizadas, descubiertos con la sesion mas amplia.
const ids = {};

/** Primer valor util de un catalogo del Tablero, sin presuponer envoltorio. */
async function primero(ruta, ...claves) {
  try {
    const cuerpo = await (await fetch(`${TABLERO}${ruta}`, { headers: { Cookie: admin.cookie } })).json();
    const lista = Array.isArray(cuerpo) ? cuerpo
      : (cuerpo.items ?? cuerpo.datos ?? cuerpo.suites ?? Object.values(cuerpo).find(Array.isArray) ?? []);
    for (const k of claves) if (lista[0]?.[k]) return String(lista[0][k]);
  } catch { /* el catalogo no responde: las rutas que dependen de el se omiten */ }
  return null;
}

// Las claves salen de la respuesta REAL, no de una convencion supuesta: `/api/satelites` identifica
// por `satellite` y `/api/prds` por `id`. Adivinarlas dejaba 45 rutas por perfil sin ejercer, y una
// ruta omitida no es una ruta que pase.
ids.sat = await primero('/api/v1/satelites', 'satellite', 'slug', 'codigo', 'id');
ids.id  = await primero('/api/v1/prds', 'id', 'codigo');
ids.suite = await primero('/api/v1/suites', 'id', 'codigo');
// `:sat` y `:id` cubren casi todo; el resto cae al generico, que es el del PRD por ser el unico
// identificador de recurso que el Tablero publica sin exigir contexto.
ids.generico = ids.id ?? ids.sat;

console.log(`  · identificadores descubiertos: sat=${ids.sat ?? '—'} id=${ids.id ?? '—'} `
          + `suite=${ids.suite ?? '—'}`);

const resultados = [];

for (const usuario of PERFILES) {
  const s = await entrar(usuario);
  if (s.error) { comprobar(`sesion de ${usuario}`, false, s.error); continue; }

  const concedidos = new Set(s.grafo?.scopes ?? []);
  const permitidas = new Set();
  const discordancias = [];
  let omitidas = 0, soloDeniega = 0;

  for (const [patron, scope] of Object.entries(MAPA_TABLERO)) {
    const concreta = concretar(patron, ids);
    if (!concreta) { omitidas++; continue; }

    const esperado = concedidos.has(scope);

    // Escritura: solo se ejerce el lado DENEGADO. Completar el permitido mutaria datos reales.
    if (concreta.metodo !== 'GET' && esperado) { soloDeniega++; continue; }

    const estado = await tocar(s.cookie, concreta.metodo, concreta.ruta);
    const dejaPasar = estado !== 403;
    if (dejaPasar) permitidas.add(patron);

    if (dejaPasar !== esperado) {
      discordancias.push({ patron, scope, esperado, estado });
    }
  }

  // Un grafo VACIO hace la igualdad de conjuntos trivialmente cierta: cero permitidas contra cero
  // concedidas «concuerda». Ocurrio el 2026-08-02 contra el clUster —las plantillas estaban sin
  // publicar y sin asignar, asi que los cuatro perfiles salian con 0 scopes— y los cuatro perfiles
  // pasaron en verde. Lo caz\u00f3 el control negativo del barrido 4, que es para lo que existe,
  // pero un barrido que no puede fallar por si mismo no deberia poder pasar por si mismo.
  comprobar(`${usuario} (${s.rol}): tiene permisos concedidos`,
    concedidos.size > 0,
    `${concedidos.size} scopes` + (concedidos.size === 0
      ? ' — grafo VACIO: la concordancia seria trivialmente cierta. Revisa que las plantillas esten '
        + 'publicadas Y asignadas al perfil'
      : ''));

  comprobar(`${usuario} (${s.rol}): el conjunto permitido es EXACTAMENTE el concedido`,
    discordancias.length === 0,
    `${permitidas.size} permitidas · ${concedidos.size} scopes · ${omitidas} omitidas por falta de id · `
    + `${soloDeniega} escrituras no ejercidas`
    + (discordancias.length
        ? `\n      ${discordancias.slice(0, 6).map((d) =>
            `${d.esperado ? 'VE DE MENOS' : 'VE DE MAS'} ${d.patron} (${d.scope}) → HTTP ${d.estado}`).join('\n      ')}`
            + (discordancias.length > 6 ? `\n      …+${discordancias.length - 6}` : '')
        : ''));

  resultados.push({ usuario, rol: s.rol, cookie: s.cookie, permitidas, concedidos, omitidas, soloDeniega, discordancias });
}

// ── Barrido 3 · Monotonia ───────────────────────────────────────────────────

console.log('\n── 3. Monotonia: menos permiso, menos superficie ──');

const porRol = new Map(resultados.map((r) => [r.usuario, r]));
const elAdmin = porRol.get(PERFILES[0]);

/*
 * La superficie se mide sobre TODO el mapa —lo esperado segun el grafo— y no sobre las GET que el
 * robot llega a ejercer. Medida sobre las GET, el auditor empataba con el administrador y la
 * asercion fallaba sin que nada estuviera mal: su plantilla concede la lectura de la suite entera,
 * asi que su recorte esta en las ESCRITURAS, que solo se ejercen por el lado denegado y por tanto
 * nunca entran en el conjunto ejercido. La asercion medía lo que el robot alcanza, no lo que el
 * perfil puede.
 */
const superficieEsperada = (r) =>
  new Set(Object.entries(MAPA_TABLERO).filter(([, s]) => r.concedidos.has(s)).map(([p]) => p));

const superficieAdmin = superficieEsperada(elAdmin);

for (const r of resultados.slice(1)) {
  const deMasEjercido = [...r.permitidas].filter((p) => !elAdmin.permitidas.has(p));
  comprobar(`${r.usuario} no alcanza nada que ${PERFILES[0]} no alcance`,
    deMasEjercido.length === 0,
    deMasEjercido.length ? deMasEjercido.slice(0, 4).join(' · ')
                         : `${r.permitidas.size} ⊆ ${elAdmin.permitidas.size} rutas ejercidas`);

  const suya = superficieEsperada(r);
  const deMas = [...suya].filter((p) => !superficieAdmin.has(p));
  comprobar(`${r.usuario} tiene MENOS superficie que ${PERFILES[0]} en TODO el mapa`,
    suya.size < superficieAdmin.size && deMas.length === 0,
    `${suya.size} vs ${superficieAdmin.size} rutas`
    + (deMas.length ? ` · de mas: ${deMas.slice(0, 4).join(' · ')}` : ''));
}

// La consecuencia que el propio mapa declara: los scopes del auditor son todos de lectura, asi que
// ninguna entrada cuyo scope no termine en view/search/export puede pasarle.
const auditor = resultados.find((r) => r.rol === 'AUDITOR');
if (auditor) {
  const escrituraConcedida = [...auditor.concedidos]
    .filter((s) => !/\.(view|search|export)$/.test(s));
  comprobar('el auditor no tiene NINGUN scope que no sea de lectura',
    escrituraConcedida.length === 0,
    escrituraConcedida.length ? escrituraConcedida.slice(0, 5).join(' · ') : `${auditor.concedidos.size} scopes, todos de lectura`);
}

// ── Barrido 4 · Control negativo: que este robot PUEDA fallar ───────────────
//
// Una prueba que no puede fallar no prueba nada, y esta se parece peligrosamente a una: si el
// servidor dejara pasar todo, o si el grafo concediera todo, los tres barridos anteriores saldrian
// verdes igual. El control lo descarta juzgando a un perfil restringido con la expectativa del
// administrador: tienen que aparecer discordancias, y si no aparecen es que el robot no esta
// midiendo lo que dice.
//
// Se hace sobre ESCRITURAS a proposito. Sobre las lecturas no discrimina —la plantilla del auditor
// concede la lectura de la suite entera, asi que sobre GET empata con el administrador— y un control
// que no distingue no es un control. Costo esa pasada descubrirlo.

console.log('\n── 4. Control negativo: el robot puede fallar ──');

const restringido = resultados.find((r) => r.rol === 'AUDITOR') ?? resultados[resultados.length - 1];

if (restringido && restringido !== elAdmin) {
  let discordanciasDelControl = 0, probadasDelControl = 0;

  for (const [patron, scope] of Object.entries(MAPA_TABLERO)) {
    const concreta = concretar(patron, ids);
    if (!concreta || concreta.metodo === 'GET') continue;

    const esperadoSegunAdmin = elAdmin.concedidos.has(scope);
    const estado = await tocar(restringido.cookie ?? '', concreta.metodo, concreta.ruta);
    probadasDelControl++;
    if ((estado !== 403) !== esperadoSegunAdmin) discordanciasDelControl++;
  }

  comprobar(`juzgar a ${restringido.usuario} con la expectativa de ${PERFILES[0]} SI produce discordancias`,
    discordanciasDelControl > 0,
    `${discordanciasDelControl} de ${probadasDelControl} escrituras`);
} else {
  comprobar('hay un perfil restringido con el que hacer el control negativo', false,
    'solo se probo un perfil');
}

// ── Cierre ──────────────────────────────────────────────────────────────────

const omitidasTotal = resultados.reduce((a, r) => a + r.omitidas, 0);
const escriturasTotal = resultados.reduce((a, r) => a + r.soloDeniega, 0);

console.log(`\n${fallos === 0 ? '✔' : '✘'} ${pasos - fallos}/${pasos} comprobaciones.`);
console.log(`  Cobertura declarada: ${omitidasTotal} ejercicios omitidos por falta de identificador `
          + `y ${escriturasTotal} escrituras no ejercidas por el lado permitido. De una ruta de `
          + `escritura este robot demuestra que se cierra a quien no debe, no que se abre a quien debe.`);

if (INFORME) {
  writeFileSync(INFORME, JSON.stringify({
    tablero: TABLERO,
    pasos, fallos,
    rutasDeLaSpec: RUTAS_DE_LA_SPEC.length,
    rutasDelMapa: clavesMapa.size,
    perfiles: resultados.map((r) => ({
      usuario: r.usuario, rol: r.rol,
      permitidas: [...r.permitidas].sort(),
      scopes: [...r.concedidos].sort(),
      omitidas: r.omitidas, escriturasNoEjercidas: r.soloDeniega,
      discordancias: r.discordancias,
    })),
    detalle,
  }, null, 2));
  console.log(`  Informe: ${INFORME}`);
}

process.exit(fallos === 0 ? 0 : 1);
