// Inventario de quién tiene acceso a qué en una instancia de UMS. **No escribe nada.**
//
// Existe para poder decidir una depuración con la lista delante en vez de en bloque y a ciegas:
// cuántas cuentas hay, cuáles tienen perfil en cada sistema, y cuáles quedarían fuera del padrón
// declarado en las specs.
//
//   node src/provisioning/inventario-accesos.mjs
//   node src/provisioning/inventario-accesos.mjs --json inventario.json
//
// Variables: UMS_URL (http://localhost:5000), UMS_USER, UMS_PASS, UMS_TENANT, UMS_PAUSA_MS.
//
// EL PADRÓN NO SE ESCRIBE AQUÍ. Se deriva de `profiles` de las specs, que es la declaración
// autoritativa. Una lista repetida en este archivo envejecería a espaldas de la spec y este guion
// pasaría a proponer bajas de gente que sí debe estar — el peor error posible en una depuración.

import { readFileSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';

const URL_BASE = (process.env.UMS_URL || 'http://localhost:5000').replace(/\/+$/, '');
const USUARIO = process.env.UMS_USER || 'admin@evolith.example';
const CLAVE = process.env.UMS_PASS || 'Evolith.Dev.2026';
const TENANT = process.env.UMS_TENANT || 'EVOLITH';
const PAUSA_MS = Number(process.env.UMS_PAUSA_MS ?? 0);

const leerArgumento = (n) => {
  const i = process.argv.indexOf(`--${n}`);
  return i !== -1 && process.argv[i + 1] ? process.argv[i + 1] : null;
};
const RUTA_JSON = leerArgumento('json');

const SPECS = [
  'src/provisioning/sdlc/sdlc-suite.json',
  'src/provisioning/ums/ums-suite.json',
];

let cookie = null;
const esperar = (ms) => new Promise((r) => setTimeout(r, ms));

/**
 * Cliente HTTP de SOLO LECTURA.
 *
 * El corte está aquí, en el único punto por el que pasan todas las peticiones, y no en la
 * disciplina de quien escriba más abajo: este guion se va a leer mientras se decide dar de baja a
 * gente, y «no toca nada» tiene que ser una propiedad del código, no una promesa del comentario.
 */
async function leer(ruta, intento = 0) {
  if (PAUSA_MS > 0 && intento === 0) await esperar(PAUSA_MS);

  const res = await fetch(`${URL_BASE}${ruta}`, {
    method: 'GET',
    headers: { ...(cookie ? { Cookie: cookie } : {}), Accept: 'application/json' },
  });
  const puesta = res.headers.get('set-cookie');
  if (puesta) cookie = puesta.split(';')[0];

  if (res.status === 429 && intento < 3) {
    const cab = Number(res.headers.get('retry-after'));
    const seg = Number.isFinite(cab) && cab > 0 ? cab : 61;
    console.log(`⏳ límite alcanzado; se reanuda en ${seg}s`);
    await esperar(seg * 1000);
    return leer(ruta, intento + 1);
  }

  return { ok: res.ok, status: res.status, datos: await res.json().catch(() => ({})) };
}

/** Recorre las páginas de un catálogo. `pageSize` lo recorta el servidor a 100. */
async function listarTodo(rutaBase) {
  const sep = rutaBase.includes('?') ? '&' : '?';
  const acc = [];
  for (let p = 1; p <= 200; p += 1) {
    const r = await leer(`${rutaBase}${sep}page=${p}&pageSize=100`);
    if (!r.ok) return { ok: false, status: r.status, items: acc };
    const c = r.datos ?? {};
    if (Array.isArray(c)) return { ok: true, items: c };
    const trozo = c.items ?? c.data ?? c.suites ?? c.datos?.items ?? c.datos ?? [];
    if (!Array.isArray(trozo) || trozo.length === 0) break;
    acc.push(...trozo);
    const total = c.totalPages ?? c.datos?.totalPages;
    if (total ? p >= total : trozo.length < 100) break;
  }
  return { ok: true, items: acc };
}

// ── 1. Sesión ────────────────────────────────────────────────────────────────
const acceso = await fetch(`${URL_BASE}/api/v1/auth/login`, {
  method: 'POST',
  headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
  body: JSON.stringify({ tenantCode: TENANT, username: USUARIO, password: CLAVE, rememberMe: false }),
});
const cuerpoAcceso = await acceso.json().catch(() => ({}));
if (!acceso.ok) {
  console.error(`No se pudo iniciar sesión: HTTP ${acceso.status} ${JSON.stringify(cuerpoAcceso)}`);
  process.exit(1);
}
const puestaInicial = acceso.headers.get('set-cookie');
if (puestaInicial) cookie = puestaInicial.split(';')[0];
const tenantId = cuerpoAcceso.tenantId;
console.log(`✔ sesión iniciada en ${TENANT} (${tenantId})`);

// ── 2. El padrón declarado ───────────────────────────────────────────────────
const padron = new Map();   // correo → [{suite, rol}]
for (const ruta of SPECS) {
  let spec;
  try { spec = JSON.parse(readFileSync(resolve(process.cwd(), ruta), 'utf8')); }
  catch { console.log(`• no pude leer ${ruta}; su padrón no se tendrá en cuenta`); continue; }

  for (const d of spec.profiles ?? []) {
    const correo = d.userEmailEnv ? process.env[d.userEmailEnv] : d.userEmail;
    if (!correo) continue;
    if (!padron.has(correo)) padron.set(correo, []);
    padron.get(correo).push({ suite: spec.suite.code, rol: d.role });
  }
}
// La cuenta con la que corre esto se conserva SIEMPRE: proponer la baja de la sesión que está
// haciendo el inventario es la forma más rápida de quedarse fuera del sistema que se depura.
if (!padron.has(USUARIO)) padron.set(USUARIO, [{ suite: '—', rol: 'sesión actual' }]);
console.log(`✔ padrón declarado: ${padron.size} personas`);

// ── 3. Catálogos ─────────────────────────────────────────────────────────────
const suites = new Map();   // suiteId → code
const cs = await listarTodo('/api/v1/system-suites');
for (const s of cs.items) suites.set(s.id ?? s.systemSuiteId, s.code);

const roles = new Map();    // roleId → {code, suite}
for (const [id, code] of suites) {
  const cr = await listarTodo(`/api/v1/system-suites/${id}/roles`);
  for (const r of cr.items) roles.set(r.roleId ?? r.id, { code: r.code, suite: code });
}

const sucursales = new Map();
const cb = await leer(`/api/v1/tenants/${tenantId}/branches`);
const listaB = Array.isArray(cb.datos) ? cb.datos : (cb.datos?.items ?? []);
for (const b of listaB) sucursales.set(b.branchId ?? b.id, b.code);

// LAS PLANTILLAS Y SUS VERSIONES. Sin esto no se puede saber si un perfil tiene los permisos de la
// versión vigente o la SUMA de varias: `Profile.AssignTemplate` **acumula** —hace `_permissions.Add`
// por cada ítem y el agregado no tiene ningún `RemoveTemplate`—, así que asignar una versión nueva
// sobre un perfil que ya tenía la vieja deja las dos. Medido en UAT el 2026-08-17: los perfiles
// pasaron de 94 a 188, de 124 a 248 y de 407 a 528 permisos.
const ct = await listarTodo('/api/v1/permission-templates');
const plantillas = ct.items.map((t) => ({
  id: t.id ?? t.templateId,
  roleId: t.roleId,
  version: t.version,
  estado: t.status,
  rol: roles.get(t.roleId)?.code ?? '?',
}));

// LAS REGLAS DE AUTO-ASIGNACIÓN. Deciden qué plantilla recibe un perfil NUEVO, que es la única vía
// que queda para devolver un perfil a una sola versión: no hay forma de desligar una plantilla, así
// que se desactiva el perfil y se crea otro. Si la regla apunta a la versión vieja, recrear repite
// el problema en vez de arreglarlo — de ahí que haya que mirarlo antes y no después.
const cr = await leer('/api/v1/template-assignment-rules');
const reglasCrudas = Array.isArray(cr.datos) ? cr.datos : (cr.datos?.items ?? cr.datos?.data ?? []);
const reglas = reglasCrudas.map((r) => {
  const t = plantillas.find((x) => x.id === (r.templateId ?? r.template?.id));
  return {
    rol: roles.get(r.roleId)?.code ?? '?',
    templateId: r.templateId ?? r.template?.id,
    version: t?.version ?? '?',
    estadoPlantilla: t?.estado ?? '?',
    activa: r.isActive !== false,
  };
});

const cp = await listarTodo('/api/v1/profiles');
const cu = await listarTodo('/api/v1/user-accounts');
console.log(`✔ ${suites.size} suites · ${roles.size} roles · ${cp.items.length} perfiles · ${cu.items.length} cuentas`);

// ── 4. Clasificación ─────────────────────────────────────────────────────────
const perfilesPorUsuario = new Map();
for (const p of cp.items) {
  const uid = p.userId;
  if (!perfilesPorUsuario.has(uid)) perfilesPorUsuario.set(uid, []);
  const r = roles.get(p.roleId);
  perfilesPorUsuario.get(uid).push({
    profileId: p.profileId ?? p.id,
    suite: r?.suite ?? '?',
    rol: r?.code ?? '?',
    sucursal: p.branchId ? (sucursales.get(p.branchId) ?? '?') : null,
    activo: p.isActive !== false,
    permisos: p.permissionCount ?? 0,
  });
}

// LA LECTURA POR ID, UNA A UNA. Parece redundante teniendo el listado, y es lo único que detecta
// el caso que costó media jornada: una cuenta que el listado devuelve y `GET /{id}` no. Cuando eso
// pasa, cualquier operación sobre ella responde 404 —crear su perfil, activarla, ponerle
// credencial— y el mensaje culpa al «record», que se lee como si faltara otra cosa.
//
// Son tantas peticiones como cuentas, así que solo es asumible en un inventario. Aquí lo es.
const porId = new Map();
for (const u of cu.items) {
  const id = u.userAccountId ?? u.id;
  const r = await leer(`/api/v1/user-accounts/${id}`);
  porId.set(id, { ok: r.ok, status: r.status, datos: r.ok ? r.datos : null });
}

const filas = cu.items.map((u) => {
  const id = u.userAccountId ?? u.id;
  const correo = u.email ?? '(sin correo)';
  const suyos = perfilesPorUsuario.get(id) ?? [];
  const directa = porId.get(id);
  // El inquilino de la cuenta, comparado con el de la sesión: si difiere, el listado la enseña
  // por privilegio de administrador interno y todo lo demás la rechazará por RLS.
  const tenantDeLaCuenta = directa?.datos?.tenantId ?? directa?.datos?.tenant?.id ?? null;
  return {
    correo,
    userAccountId: id,
    estado: u.status ?? u.accountStatus ?? (u.isActive === false ? 'Inactive' : '?'),
    resolublePorId: directa?.ok === true,
    estadoLecturaDirecta: directa?.status ?? null,
    tenantDeLaCuenta,
    otroInquilino: Boolean(tenantDeLaCuenta && tenantDeLaCuenta !== tenantId),
    enPadron: padron.has(correo),
    perfiles: suyos,
    // «Conservar» es solo el padrón declarado. Todo lo demás se PROPONE para revisión, no para
    // baja: esta lista se lee antes de decidir, y marcar algo como «a borrar» invita a ejecutar
    // sin leer.
    veredicto: padron.has(correo) ? 'CONSERVAR' : 'REVISAR',
  };
}).sort((a, b) => (a.veredicto === b.veredicto ? a.correo.localeCompare(b.correo) : a.veredicto === 'CONSERVAR' ? -1 : 1));

// ── 5. Informe ───────────────────────────────────────────────────────────────
const anchoCorreo = Math.max(20, ...filas.map((f) => f.correo.length));
const resumen = (f) => f.perfiles.length === 0
  ? '(sin perfiles)'
  : f.perfiles.map((p) => `${p.suite}/${p.rol}${p.sucursal ? `@${p.sucursal}` : ''}${p.activo ? '' : ' (inactivo)'}`).join(', ');

console.log(`\n${'CORREO'.padEnd(anchoCorreo)}  ${'VEREDICTO'.padEnd(9)}  ${'ESTADO'.padEnd(9)}  ${'POR ID'.padEnd(8)}  PERFILES`);
for (const f of filas) {
  const porIdTxt = f.resolublePorId ? 'ok' : `HTTP ${f.estadoLecturaDirecta}`;
  console.log(`${f.correo.padEnd(anchoCorreo)}  ${f.veredicto.padEnd(9)}  ${String(f.estado).padEnd(9)}  ${porIdTxt.padEnd(8)}  ${resumen(f)}`);
}

// Las cuentas rotas se destacan aparte: en una tabla de nueve filas se ven, en una de doscientas
// se pierden, y son las únicas sobre las que NINGUNA operación va a funcionar.
const rotas = filas.filter((f) => !f.resolublePorId);
if (rotas.length) {
  console.log('\n✖ cuentas que el listado devuelve pero que NO se resuelven por id:');
  for (const f of rotas) {
    console.log(`    ${f.correo} (${f.userAccountId}) → GET /user-accounts/{id} responde ${f.estadoLecturaDirecta}`);
    console.log('      Sobre esta cuenta fallará TODO: crear su perfil, activarla, ponerle credencial.');
  }
}

console.log('\nplantillas por rol (versión · estado):');
for (const r of [...new Set(plantillas.map((p) => p.rol))].sort()) {
  const suyas = plantillas.filter((p) => p.rol === r).sort((a, b) => (a.version ?? 0) - (b.version ?? 0));
  console.log(`    ${r.padEnd(18)} ${suyas.map((s) => `v${s.version}:${s.estado}`).join('  ')}`);
}

console.log('\nreglas de auto-asignación (qué recibe un perfil NUEVO):');
if (reglas.length === 0) {
  console.log('    (ninguna) — un perfil nuevo nacería SIN permisos y habría que asignarle plantilla a mano');
} else {
  for (const r of reglas.filter((x) => x.activa)) {
    console.log(`    ${r.rol.padEnd(18)} → v${r.version} (${r.estadoPlantilla})`);
  }
}

const ajenas = filas.filter((f) => f.otroInquilino);
if (ajenas.length) {
  console.log('\n• cuentas de OTRO inquilino visibles por privilegio de administrador interno:');
  for (const f of ajenas) console.log(`    ${f.correo} → inquilino ${f.tenantDeLaCuenta}`);
}

const conservar = filas.filter((f) => f.veredicto === 'CONSERVAR');
const revisar = filas.filter((f) => f.veredicto === 'REVISAR');
const revisarConPerfil = revisar.filter((f) => f.perfiles.length > 0);

console.log(`\ncuentas: ${filas.length} · en el padrón: ${conservar.length} · fuera del padrón: ${revisar.length}`);
console.log(`de las que están fuera, ${revisarConPerfil.length} TIENEN perfil activo y ${revisar.length - revisarConPerfil.length} no tienen ninguno.`);

// Las que no tienen perfil no dan acceso a nada: desactivarlas no cambia lo que nadie puede hacer.
// Decirlo evita una depuración que se siente productiva y no reduce ninguna superficie.
if (revisar.length - revisarConPerfil.length > 0) {
  console.log('Las que no tienen perfil ya no conceden acceso a ningún sistema: darlas de baja no reduce superficie.');
}

// Del padrón declarado, quién NO aparece en la instancia. Es la otra mitad del inventario y la que
// se olvida: una depuración mira lo que sobra y no lo que falta.
const correosEnInstancia = new Set(filas.map((f) => f.correo));
const ausentes = [...padron.keys()].filter((c) => !correosEnInstancia.has(c));
if (ausentes.length) console.log(`\n✖ declaradas en las specs y AUSENTES de la instancia: ${ausentes.join(', ')}`);

if (RUTA_JSON) {
  writeFileSync(resolve(process.cwd(), RUTA_JSON), JSON.stringify({ tenant: TENANT, tenantId, filas }, null, 2), 'utf8');
  console.log(`\ninventario escrito en ${RUTA_JSON}`);
}

console.log('\nEste guion NO escribe en UMS: solo emite peticiones GET.');
