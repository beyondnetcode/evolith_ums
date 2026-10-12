// Depura una instancia de UMS que quedó con permisos acumulados. **Sin `--aplicar` no escribe.**
//
// Existe por G-272: asignar una plantilla a un perfil SUMA sus permisos a los que ya tenía, y el
// agregado no ofrece forma de desligar la anterior. Así que un perfil contaminado no se corrige —se
// desactiva y se crea otro—. Esto hace las dos mitades que preceden a esa recreación.
//
//   node src/provisioning/depurar-accesos.mjs             # lista lo que haría
//   node src/provisioning/depurar-accesos.mjs --aplicar   # lo hace
//
// QUÉ TOCA, Y QUÉ NO:
//   · DEPRECA las versiones de plantilla publicadas que ya no son la vigente de su rol. Deprecar no
//     borra: la plantilla y sus ítems siguen ahí, y las filas que materializó siguen siendo el
//     rastro de qué pudo hacer cada perfil mientras estuvo viva.
//   · DESACTIVA perfiles. No borra ninguno, por lo mismo: «un perfil es historia de acceso, y
//     borrarlo se lleva por delante la trazabilidad de quién pudo qué». `deactivate` es además
//     reversible con `activate`; el borrado no lo es.
//   · NO toca cuentas de usuario. Ni una. Dar de baja personas es otra decisión y otro guion.

import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';

const URL_BASE = (process.env.UMS_URL || 'http://localhost:5000').replace(/\/+$/, '');
const USUARIO = process.env.UMS_USER || 'admin@evolith.example';
const CLAVE = process.env.UMS_PASS || 'Evolith.Dev.2026';
const TENANT = process.env.UMS_TENANT || 'EVOLITH';
const PAUSA_MS = Number(process.env.UMS_PAUSA_MS ?? 0);

const APLICAR = process.argv.includes('--aplicar');

// `--recrear <SUITE>`: desactiva TODOS los perfiles del padrón en esa suite, estén como estén.
//
// No es una limpieza, es el preparativo obligado de un cambio de permisos. Por G-272 asignar una
// versión nueva de plantilla a un perfil vivo SUMA sus concesiones a las que ya tenía, así que la
// única forma de que un perfil acabe con exactamente el modelo nuevo es que NAZCA con él. Se
// desactiva, y el cargador crea otro y le asigna la versión vigente y nada más.
//
// Los perfiles desactivados NO se borran: siguen siendo el rastro de qué pudo hacer esa persona
// mientras el modelo anterior estuvo vigente, que es justo lo que pregunta una auditoría.
const RECREAR = (() => {
  const i = process.argv.indexOf('--recrear');
  return i !== -1 && process.argv[i + 1] ? process.argv[i + 1].toUpperCase() : null;
})();
const SPECS = ['src/provisioning/sdlc/sdlc-suite.json', 'src/provisioning/ums/ums-suite.json'];

let cookie = null;
const esperar = (ms) => new Promise((r) => setTimeout(r, ms));
const log = (i, t) => console.log(`${i} ${t}`);

async function api(metodo, ruta, cuerpo, intento = 0) {
  // El corte de simulacro va aquí, en el único punto por el que pasan todas las peticiones, para
  // que no exista ningún camino de escritura que se lo salte.
  if (!APLICAR && metodo !== 'GET' && !ruta.startsWith('/api/v1/auth/')) {
    return { ok: true, status: 200, datos: {}, simulado: true };
  }
  if (PAUSA_MS > 0 && intento === 0) await esperar(PAUSA_MS);

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

  if (res.status === 429 && intento < 3) {
    const cab = Number(res.headers.get('retry-after'));
    const seg = Number.isFinite(cab) && cab > 0 ? cab : 61;
    log('⏳', `límite alcanzado; se reanuda en ${seg}s`);
    await esperar(seg * 1000);
    return api(metodo, ruta, cuerpo, intento + 1);
  }
  return { ok: res.ok, status: res.status, datos: await res.json().catch(() => ({})) };
}

async function listarTodo(rutaBase) {
  const sep = rutaBase.includes('?') ? '&' : '?';
  const acc = [];
  for (let p = 1; p <= 200; p += 1) {
    const r = await api('GET', `${rutaBase}${sep}page=${p}&pageSize=100`);
    if (!r.ok) return { ok: false, items: acc };
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

// ── Sesión ───────────────────────────────────────────────────────────────────
const acceso = await api('POST', '/api/v1/auth/login', {
  tenantCode: TENANT, username: USUARIO, password: CLAVE, rememberMe: false,
});
if (!acceso.ok) {
  console.error(`No se pudo iniciar sesión: HTTP ${acceso.status} ${JSON.stringify(acceso.datos)}`);
  process.exit(1);
}
const tenantId = acceso.datos.tenantId;
log('✔', `sesión iniciada en ${TENANT}${APLICAR ? '' : '  (SIMULACRO: no se escribe nada)'}`);

// ── Catálogos ────────────────────────────────────────────────────────────────
const suites = new Map();
for (const s of (await listarTodo('/api/v1/system-suites')).items) suites.set(s.id ?? s.systemSuiteId, s.code);

const roles = new Map();
for (const [id, code] of suites) {
  for (const r of (await listarTodo(`/api/v1/system-suites/${id}/roles`)).items) {
    roles.set(r.roleId ?? r.id, { code: r.code, suite: code });
  }
}

const plantillas = (await listarTodo('/api/v1/permission-templates')).items.map((t) => ({
  id: t.id ?? t.templateId,
  roleId: t.roleId,
  version: String(t.version ?? '0'),
  estado: t.status,
  rol: roles.get(t.roleId)?.code ?? '?',
}));

const perfiles = (await listarTodo('/api/v1/profiles')).items;
const cuentas = (await listarTodo('/api/v1/user-accounts')).items;
const idsDeCuenta = new Set(cuentas.map((u) => u.userAccountId ?? u.id));

// ── 1. Plantillas viejas que siguen publicadas ───────────────────────────────
//
// «Vigente» es la de MAYOR versión entre las publicadas del rol. Se compara por segmentos
// numéricos y no como cadena: con orden alfabético, «v0.10.0» iría antes que «v0.6.0» y se
// deprecaría justo la buena.
const cmpVersion = (a, b) => {
  const pa = a.split('.').map(Number), pb = b.split('.').map(Number);
  for (let i = 0; i < Math.max(pa.length, pb.length); i += 1) {
    const d = (pa[i] ?? 0) - (pb[i] ?? 0);
    if (d !== 0) return d;
  }
  return 0;
};

const aDeprecar = [];
for (const rol of [...new Set(plantillas.map((p) => p.rol))]) {
  const publicadas = plantillas.filter((p) => p.rol === rol && p.estado === 'Published');
  if (publicadas.length < 2) continue;
  const vigente = publicadas.slice().sort((a, b) => cmpVersion(b.version, a.version))[0];
  for (const p of publicadas) if (p.id !== vigente.id) aDeprecar.push({ ...p, vigente: vigente.version });
}

// ── 2. Perfiles a desactivar ─────────────────────────────────────────────────
//
// Tres clases, y las tres se listan por separado porque se justifican distinto:
//   · CONTAMINADO: su rol tenía más de una versión publicada, así que sus permisos son la SUMA.
//   · HUÉRFANO: cuelga de un usuario que ya no está en el catálogo de cuentas.
//   · SIN DECLARAR: del padrón, pero con una combinación (rol, sucursal) que la spec no declara —el
//     caso de los perfiles sin sucursal que dejó el cargador anterior.
const padron = new Map();
for (const ruta of SPECS) {
  let spec;
  try { spec = JSON.parse(readFileSync(resolve(process.cwd(), ruta), 'utf8')); } catch { continue; }
  for (const d of spec.profiles ?? []) {
    const correo = d.userEmailEnv ? process.env[d.userEmailEnv] : d.userEmail;
    if (!correo) continue;
    for (const sede of d.branches ?? (d.branch ? [d.branch] : [null])) {
      padron.set(`${correo}|${d.role}|${sede ?? ''}`, true);
    }
  }
}
// CUENTAS PROTEGIDAS: nunca se propone tocar sus perfiles.
//
// No es una cortesía, es una guarda contra una forma concreta de quedarse fuera. El padrón se
// deriva de las specs, y la fila del administrador de UMS declara su correo por VARIABLE DE
// ENTORNO (`UMS_BOOTSTRAP_EMAIL`). Esa variable vive en el pool de IIS —la lee la API— y NO en el
// proceso que corre este guion, así que el padrón se construye sin él y su perfil aparecía como
// «sin declarar». Es decir: la primera versión de esto proponía desactivar el único perfil de
// administración de UMS que existe, por un detalle de dónde vive una variable.
//
// Se protegen tres cosas: la cuenta con la que corre la sesión —proponer su propia baja es
// absurdo—, el correo de arranque documentado en `BootstrapOptions.AdminEmail`, y lo que se
// añada a mano con `--conservar`.
const listaConservar = (() => {
  const i = process.argv.indexOf('--conservar');
  return i !== -1 && process.argv[i + 1] ? process.argv[i + 1].split(',').map((s) => s.trim()) : [];
})();
const PROTEGIDAS = new Set([
  USUARIO,
  'admin@evolith.example',
  process.env.UMS_BOOTSTRAP_EMAIL,
  ...listaConservar,
].filter(Boolean));

const correoDe = new Map(cuentas.map((u) => [u.userAccountId ?? u.id, u.email]));
const sucursales = new Map();
const cb = await api('GET', `/api/v1/tenants/${tenantId}/branches`);
for (const b of (Array.isArray(cb.datos) ? cb.datos : (cb.datos?.items ?? []))) sucursales.set(b.branchId ?? b.id, b.code);

const rolesContaminados = new Set(aDeprecar.map((p) => p.rol));
const aDesactivar = [];
const protegidosOmitidos = [];
for (const p of perfiles) {
  // Los ya inactivos se saltan: no hay nada que desactivar y listarlos infla la propuesta con
  // trabajo que no existe. Es donde caen los perfiles huérfanos que una limpieza anterior apagó.
  if (p.isActive === false) continue;
  const id = p.profileId ?? p.id;
  const rol = roles.get(p.roleId);
  const correo = correoDe.get(p.userId);
  const sede = p.branchId ? (sucursales.get(p.branchId) ?? '?') : null;

  if (correo && PROTEGIDAS.has(correo)) {
    protegidosOmitidos.push({ correo, rol: rol?.code ?? '?', suite: rol?.suite ?? '?' });
    continue;
  }

  let motivo = null;
  if (RECREAR && rol && rol.suite === RECREAR && correo && padron.has(`${correo}|${rol.code}|${sede ?? ''}`)) {
    motivo = `RECREAR: se desactiva para que el cargador lo cree con el modelo de permisos nuevo (--recrear ${RECREAR})`;
  }
  else if (!correo) motivo = 'HUÉRFANO: su usuario no está en el catálogo de cuentas';
  else if (rol && rolesContaminados.has(rol.code)) motivo = `CONTAMINADO: ${rol.code} tenía varias versiones publicadas; sus ${p.permissionCount ?? '?'} permisos son la suma`;
  else if (rol && !padron.has(`${correo}|${rol.code}|${sede ?? ''}`)) motivo = 'SIN DECLARAR: esta combinación (rol, sucursal) no está en ninguna spec';

  if (motivo) {
    aDesactivar.push({ id, correo: correo ?? '(sin cuenta)', rol: rol?.code ?? '?', suite: rol?.suite ?? '?', sede, permisos: p.permissionCount ?? 0, motivo });
  }
}

// ── Informe ──────────────────────────────────────────────────────────────────
console.log(`\nplantillas a DEPRECAR (${aDeprecar.length}) — dejan una sola vigente por rol:`);
for (const p of aDeprecar) console.log(`    ${p.rol.padEnd(16)} v${p.version}  →  se conserva v${p.vigente}`);
if (aDeprecar.length === 0) console.log('    (ninguna: cada rol ya tiene una sola versión publicada)');

console.log(`\nperfiles a DESACTIVAR (${aDesactivar.length}):`);
for (const d of aDesactivar.sort((a, b) => a.correo.localeCompare(b.correo))) {
  console.log(`    ${d.correo.padEnd(28)} ${(d.suite + '/' + d.rol).padEnd(20)} ${String(d.sede ?? 'sin sede').padEnd(11)} ${String(d.permisos).padStart(4)} permisos`);
  console.log(`      ${d.motivo}`);
}
if (aDesactivar.length === 0) console.log('    (ninguno)');

// Se imprime lo PROTEGIDO, no solo lo que se toca. Una propuesta que solo enseña sus víctimas no
// deja comprobar que lo que debía quedarse se queda —y aquí eso ya falló una vez.
if (protegidosOmitidos.length) {
  console.log(`\nperfiles PROTEGIDOS, no se tocan (${protegidosOmitidos.length}):`);
  for (const p of protegidosOmitidos) console.log(`    ${p.correo.padEnd(28)} ${p.suite}/${p.rol}`);
  console.log(`    (protegidas: ${[...PROTEGIDAS].join(', ')} · añade más con --conservar a@b,c@d)`);
}

if (!APLICAR) {
  console.log('\nSIMULACRO: no se ha escrito nada. Repite con --aplicar para ejecutarlo.');
  console.log('Después hay que RECREAR los perfiles del padrón — sin regla de auto-asignación nacen');
  console.log('sin permisos, así que la asignación la hace el cargador:');
  if (RECREAR === 'SDLC' || RECREAR === 'UMS') {
    const spec = RECREAR === 'SDLC' ? 'sdlc/sdlc-suite.json' : 'ums/ums-suite.json';
    console.log(`    node src/provisioning/cargar-en-ums.mjs --spec src/provisioning/${spec} --sin-usuarios-de-ejemplo --crear-cuentas --nueva-version`);
    console.log('    (--nueva-version porque la plantilla vigente está publicada y no admite ítems nuevos;');
    console.log('     con los perfiles ya desactivados NO puede acumular, que es lo que arruinó G-272)');
  } else {
    console.log('    node src/provisioning/cargar-en-ums.mjs --spec src/provisioning/sdlc/sdlc-suite.json --sin-usuarios-de-ejemplo --crear-cuentas');
  }
  process.exit(0);
}

// ── Ejecución ────────────────────────────────────────────────────────────────
//
// PRIMERO LOS PERFILES, DESPUÉS LAS PLANTILLAS. El orden no es preferencia: es un requisito de la
// API. `DeprecatePermissionTemplateCommandHandler` cuenta los perfiles ACTIVOS que usan la
// plantilla y, si hay alguno, rechaza con `TemplateHasActiveProfiles` → HTTP 409.
//
// La primera versión de esto lo hacía al revés, con un comentario que justificaba el orden
// equivocado. Resultado medido el 2026-08-17 sobre UAT: 12 de 12 perfiles desactivados y **0 de 5**
// plantillas deprecadas, o sea el peor estado intermedio posible —nadie con acceso y las versiones
// viejas todavía vigentes—. Es idempotente, así que se resolvió repitiendo; pero solo porque el
// daño era reversible.
let ok2 = 0;
for (const d of aDesactivar) {
  const r = await api('POST', `/api/v1/profiles/${d.id}/deactivate`, {});
  if (r.ok) ok2 += 1; else log('✖', `no se pudo desactivar ${d.correo} ${d.rol} → HTTP ${r.status}`);
}
log('✔', `${ok2} de ${aDesactivar.length} perfiles desactivados`);

let ok1 = 0;
for (const p of aDeprecar) {
  const r = await api('POST', `/api/v1/permission-templates/${p.id}/deprecate`, {});
  if (r.ok) { ok1 += 1; continue; }
  log('✖', `no se pudo deprecar ${p.rol} v${p.version} → HTTP ${r.status}`);
  if (r.status === 409) {
    log(' ', `  409 = todavía tiene perfiles ACTIVOS usándola. Mira si alguno quedó fuera de la lista`);
    log(' ', `  (protegido, de otro inquilino) y vuelve a ejecutar: este guion es idempotente.`);
  }
}
log('✔', `${ok1} de ${aDeprecar.length} plantillas deprecadas`);

console.log('\nAhora RECREA los perfiles del padrón; sin eso nadie tiene acceso a nada:');
console.log('    node src/provisioning/cargar-en-ums.mjs --spec src/provisioning/sdlc/sdlc-suite.json --sin-usuarios-de-ejemplo --crear-cuentas');
console.log('    node src/provisioning/cargar-en-ums.mjs --spec src/provisioning/ums/ums-suite.json --sin-usuarios-de-ejemplo --crear-cuentas');
process.exit(ok1 === aDeprecar.length && ok2 === aDesactivar.length ? 0 : 1);
