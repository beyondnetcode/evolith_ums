#!/usr/bin/env node
/**
 * probar-recuperacion.mjs — carril E2E del restablecimiento de contraseña y del alta de cuenta.
 *
 * POR QUE EXISTE. `ForgotPasswordCommandHandler` y `ResetPasswordCommandHandler` tienen 12 y 8
 * pruebas unitarias, y el Tablero SDLC tiene las suyas sobre `procesarRecuperacion`. Ninguna de
 * las tres cruza el limite: nadie habia ejercido nunca solicitud -> buzon -> canje -> login
 * contra los procesos vivos. Las propiedades que importan aqui —que la respuesta no delate si la
 * cuenta existe, que el token sirva UNA vez, que la contrasena vieja siga valiendo hasta el
 * canje— son propiedades del FLUJO, y una prueba unitaria de cada mitad no las ve.
 *
 * CUENTA DESECHABLE, Y NO ES UN DETALLE. Solicitar el restablecimiento de una cuenta sembrada le
 * cambia la contrasena en cuanto alguien canjea el token, y deja la base inservible para las demas
 * pruebas. Ya paso el 2026-08-02 con `admin@evolith.example` y hubo que resembrar. Este guion crea
 * su propia cuenta, con correo unico por corrida, y no toca ninguna otra.
 *
 * EL BUZON EN DESARROLLO. `SimulatedNotificationAdapter` escribe la notificacion en el log del
 * servidor en vez de enviarla. El token se lee de ahi, que es el equivalente exacto de abrir el
 * correo: NO se lee de la respuesta HTTP, porque el dia que se lea de ahi la propiedad que este
 * guion verifica habra dejado de ser cierta.
 *
 * Uso:
 *   node src/tests/e2e-functional/recuperacion/probar-recuperacion.mjs \
 *     --api http://localhost:5080 --log <ruta-del-log-del-servidor> [--tablero http://localhost:4317]
 *
 * Salida: 0 si todo se comporta; 1 al primer incumplimiento, con lo medido.
 */

import { readFileSync } from 'node:fs';
import { execFileSync } from 'node:child_process';

const arg = (nombre, porDefecto = null) => {
  const i = process.argv.indexOf(`--${nombre}`);
  return i !== -1 ? process.argv[i + 1] : porDefecto;
};

const API      = arg('api', 'http://localhost:5080').replace(/\/$/, '');
const TABLERO  = arg('tablero');
const LOG      = arg('log');
const TENANT   = arg('tenant', 'EVOLITH');
const ADMIN    = arg('admin', 'admin@evolith.example');
const ADMIN_PW = arg('admin-password', 'Evolith.Dev.2026');

/*
 * Contenedor de PostgreSQL, OPCIONAL. Sin el, el tramo de caducidad se declara omitido en vez de
 * darse por bueno: un carril que se salta en silencio una propiedad que no puede medir es peor que
 * uno que no la intenta, porque el verde miente.
 */
const PG = arg('pg-container');
const PG_DB = arg('pg-db', 'UmsDev');
const PG_USER = arg('pg-user', 'postgres');

/**
 * Primera linea util de la consulta. `psql -tAc` con un `RETURNING` imprime tambien la etiqueta de
 * comando —`UPDATE 1`— detras del valor, y compararla con el valor esperado hacia fallar una
 * asercion sobre una operacion que si habia ocurrido.
 */
const sql = (consulta) => execFileSync(
  'docker', ['exec', PG, 'psql', '-U', PG_USER, '-d', PG_DB, '-tAc', consulta],
  { encoding: 'utf-8' },
).trim().split('\n')[0].trim();

if (!LOG) {
  console.error('Falta --log: sin el buzon no hay token que canjear, y leerlo de la respuesta HTTP '
              + 'seria dar por buena justamente la propiedad que se quiere comprobar.');
  process.exit(2);
}

// ── Aserciones ──────────────────────────────────────────────────────────────

let fallos = 0;
let pasos = 0;

function comprobar(etiqueta, condicion, medido) {
  pasos++;
  const marca = condicion ? '  ✔' : '  ✘';
  console.log(`${marca} ${etiqueta}${medido ? `  ·  ${medido}` : ''}`);
  if (!condicion) fallos++;
  return condicion;
}

// ── HTTP ────────────────────────────────────────────────────────────────────

async function pedir(metodo, ruta, cuerpo, portador) {
  const r = await fetch(`${API}${ruta}`, {
    method: metodo,
    headers: {
      'Content-Type': 'application/json',
      ...(portador ? { Authorization: `Bearer ${portador}` } : {}),
    },
    body: cuerpo === undefined ? undefined : JSON.stringify(cuerpo),
  });
  const texto = await r.text();
  let datos = null;
  try { datos = texto ? JSON.parse(texto) : null; } catch { datos = texto; }
  return { status: r.status, datos, texto };
}

/**
 * Lee el token del buzon simulado. Se busca desde el final y se acota a las lineas escritas
 * DESPUES de la solicitud: el log acumula corridas anteriores y quedarse con el primer token que
 * aparezca haria pasar la prueba con un token viejo.
 *
 * El destinatario se busca por la PARTE LOCAL del correo, no por el correo entero: el log
 * enmascara el `To=` a `pr***@***.pe` —correctamente, es PII— y solo el saludo del cuerpo lleva el
 * nombre completo. Buscar el correo entero no encontraba nada y la prueba se declaraba sin token,
 * que es un falso negativo, no un fallo del servidor.
 */
function tokenDelBuzon(correo, desdeByte) {
  const parteLocal = correo.split('@')[0];
  const contenido = readFileSync(LOG, 'utf-8').slice(desdeByte);
  const bloques = contenido.split('[SIMULATED NOTIFICATION]');
  for (let i = bloques.length - 1; i >= 1; i--) {
    if (!bloques[i].includes(parteLocal)) continue;
    if (!bloques[i].includes('Restablecimiento')) continue;   // no el correo de alta aprobada
    const m = bloques[i].match(/^\s{2,}([A-Za-z0-9_-]{32,})\s*$/m);
    if (m) return m[1];
  }
  return null;
}

/**
 * Longitud del buzon en CARACTERES, no en bytes. `tokenDelBuzon` corta con `String.slice`, que
 * cuenta caracteres; devolver aqui el tamano en bytes hacia que el corte cayera mas adelante de lo
 * previsto —el log lleva acentos y reglas Unicode, asi que ambos numeros divergen y la diferencia
 * crece con el fichero— y la busqueda se saltaba la notificacion recien escrita. Sintoma: «el buzon
 * no recibio token» sobre un buzon que si lo tenia.
 */
const tamanoLog = () => { try { return readFileSync(LOG, 'utf-8').length; } catch { return 0; } };

/** Espera a que el buzon reciba el token: el log se escribe por su cuenta, no con la respuesta. */
async function esperarToken(correo, desde, intentos = 20) {
  for (let i = 0; i < intentos; i++) {
    const t = tokenDelBuzon(correo, desde);
    if (t) return t;
    await new Promise((r) => setTimeout(r, 250));
  }
  return null;
}

// ── Guion ───────────────────────────────────────────────────────────────────

const marca      = Date.now().toString(36);
const DESECHABLE = `prueba.recuperacion.${marca}@evolith.example`;
const PW_VIEJA   = `Vieja.${marca}.2026!`;
const PW_NUEVA   = `Nueva.${marca}.2026!`;

console.log(`\n━━━ Recuperacion de contrasena y alta — carril E2E ━━━`);
console.log(`API ${API} · inquilino ${TENANT} · cuenta desechable ${DESECHABLE}\n`);

// 0. Sesion de administrador, solo para dar de alta la cuenta desechable.
//
// Por el carril del PORTAL (`/auth/login`) y no por el de satelite: el identificador del inquilino
// hace falta para crear la cuenta, y el grafo del satelite lo OMITE a proposito —los ids tecnicos
// solo viajan con `AUTH_GRAPH_INCLUDE_TECHNICAL_METADATA`—. Pedirselo al carril equivocado es como
// se descubrio: el alta fallaba con un 400 opaco porque `tenantId` llegaba null.
const acceso = await pedir('POST', '/api/v1/auth/login', {
  tenantCode: TENANT, username: ADMIN, password: ADMIN_PW, rememberMe: false,
});
if (acceso.status !== 200) {
  console.error(`  ✘ no se pudo autenticar ${ADMIN} para preparar la cuenta desechable `
              + `(HTTP ${acceso.status}). Sin eso no hay nada que probar.`);
  process.exit(2);
}
const portador = acceso.datos.token;
const tenantId = acceso.datos.tenantId;

// 1. Alta de la cuenta desechable, con su credencial.
const alta = await pedir('POST', '/api/v1/user-accounts', {
  tenantId,
  branchId: null, email: DESECHABLE, category: 'Internal',
  identityReference: null, identityReferenceType: null,
}, portador);

const idDesechable = alta.datos?.datos?.userAccountId ?? alta.datos?.userAccountId
                  ?? alta.datos?.datos?.id ?? alta.datos?.id ?? null;

if (!idDesechable) {
  console.error(`  ✘ no se pudo crear la cuenta desechable (HTTP ${alta.status}): `
              + `${alta.texto.slice(0, 300)}`);
  console.error('    Sin cuenta propia NO se sigue: probar esto sobre una cuenta sembrada le '
              + 'cambia la contrasena y deja la base inservible.');
  process.exit(2);
}
console.log(`  · cuenta desechable creada: ${idDesechable}`);

await pedir('POST', `/api/v1/user-accounts/${idDesechable}/activate`, {}, portador);
await pedir('POST', `/api/v1/user-accounts/${idDesechable}/passwords`,
  { userAccountId: idDesechable, password: PW_VIEJA }, portador);

const accesoViejo = await pedir('POST', '/api/v1/client/authenticate', {
  tenantCode: TENANT, username: DESECHABLE, password: PW_VIEJA, format: 'JSON',
});
comprobar('la cuenta desechable entra con su contrasena inicial',
  accesoViejo.status === 200, `HTTP ${accesoViejo.status}`);

console.log('\n── Anti-enumeracion ──');

// 2. La respuesta de una cuenta que existe y una que no deben ser indistinguibles.
const antes = tamanoLog();
const existe   = await pedir('POST', '/api/v1/auth/forgot-password', { tenantCode: TENANT, email: DESECHABLE });
const noExiste = await pedir('POST', '/api/v1/auth/forgot-password',
  { tenantCode: TENANT, email: `no.existe.${marca}@evolith.example` });

comprobar('cuenta existente e inexistente devuelven el MISMO estado',
  existe.status === noExiste.status, `${existe.status} vs ${noExiste.status}`);
comprobar('...y el MISMO cuerpo',
  JSON.stringify(existe.datos) === JSON.stringify(noExiste.datos),
  JSON.stringify(existe.datos)?.slice(0, 120));

// 3. El secreto no viaja en la respuesta, en ningun entorno.
const cuerpoTexto = existe.texto ?? '';
comprobar('la respuesta NO lleva el secreto de restablecimiento',
  !/[A-Za-z0-9_-]{32,}/.test(cuerpoTexto),
  `${cuerpoTexto.length} bytes de cuerpo`);

console.log('\n── El token, y solo por el buzon ──');

const token = await esperarToken(DESECHABLE, antes);
comprobar('el buzon recibio un token para la cuenta que SI existe', Boolean(token),
  token ? `${token.slice(0, 12)}… (${token.length} car.)` : 'ninguno');

if (!token) {
  console.error('\n  Sin token no se puede seguir. Comprueba que --log apunta al log del servidor.');
  process.exit(1);
}

// 4. La contrasena anterior sigue valiendo: solicitar no es cambiar.
const trasSolicitar = await pedir('POST', '/api/v1/client/authenticate', {
  tenantCode: TENANT, username: DESECHABLE, password: PW_VIEJA, format: 'JSON',
});
comprobar('solicitar NO cambia la contrasena: la anterior sigue entrando',
  trasSolicitar.status === 200, `HTTP ${trasSolicitar.status}`);

console.log('\n── Canje ──');

// 5. Un token inventado no canjea.
const inventado = await pedir('POST', '/api/v1/auth/reset-password',
  { token: 'a'.repeat(token.length), newPassword: PW_NUEVA });
comprobar('un token inventado NO canjea', inventado.status >= 400, `HTTP ${inventado.status}`);

// 6. El token real canjea una vez.
const canje = await pedir('POST', '/api/v1/auth/reset-password',
  { token, newPassword: PW_NUEVA });
comprobar('el token real canjea', canje.status === 200, `HTTP ${canje.status}`);

// 7. Y SOLO una: el segundo canje falla.
const segundo = await pedir('POST', '/api/v1/auth/reset-password',
  { token, newPassword: `Otra.${marca}.2026!` });
comprobar('el mismo token NO canjea dos veces', segundo.status >= 400, `HTTP ${segundo.status}`);

console.log('\n── Efecto ──');

const conNueva = await pedir('POST', '/api/v1/client/authenticate', {
  tenantCode: TENANT, username: DESECHABLE, password: PW_NUEVA, format: 'JSON',
});
comprobar('la contrasena NUEVA entra', conNueva.status === 200, `HTTP ${conNueva.status}`);

const conVieja = await pedir('POST', '/api/v1/client/authenticate', {
  tenantCode: TENANT, username: DESECHABLE, password: PW_VIEJA, format: 'JSON',
});
comprobar('la contrasena VIEJA ya no entra', conVieja.status === 401, `HTTP ${conVieja.status}`);

// 8. Y el tercer canje, con la contrasena ya cambiada, tampoco revive el token.
const tras = await pedir('POST', '/api/v1/auth/reset-password',
  { token, newPassword: PW_VIEJA });
comprobar('el token gastado no revierte el cambio', tras.status >= 400, `HTTP ${tras.status}`);

// ── Caducidad ───────────────────────────────────────────────────────────────
//
// Se comprueban DOS cosas distintas, y hacen falta las dos. Que la vida persistida sean 15 minutos
// —eso lo dice la fila— y que el servidor RECHACE un token vencido —eso solo lo dice intentando
// canjearlo—. Con la primera sola, un servidor que nunca mirara `ExpiresAtUtc` saldria verde; con
// la segunda sola, uno que caducara a los 15 segundos tambien.
//
// No se espera un cuarto de hora ni se toca el reloj del proceso: se emite un token nuevo y se
// envejece SU fila. Es cirugia sobre el estado, no sobre la logica — la comprobacion que se ejerce
// sigue siendo la del servidor, sobre datos reales.

if (!PG) {
  console.log('\n── Caducidad ──');
  console.log('  ⃝ OMITIDO: sin --pg-container no se puede envejecer el token sin esperar 15 min.');
  console.log('    Ejecutalo con --pg-container ums_postgres para cubrir esta propiedad.');
} else {
  console.log('\n── Caducidad ──');

  const antesCad = tamanoLog();
  await pedir('POST', '/api/v1/auth/forgot-password', { tenantCode: TENANT, email: DESECHABLE });
  const tokenCad = await esperarToken(DESECHABLE, antesCad);

  if (!tokenCad) {
    comprobar('se emitio un token para probar la caducidad', false, 'ninguno');
  } else {
    const vida = sql(`SELECT EXTRACT(EPOCH FROM ("ExpiresAtUtc" - "IssuedAtUtc"))/60
                      FROM ums_identity."PasswordResetTokens"
                      WHERE "Status" = 'Active' ORDER BY "IssuedAtUtc" DESC LIMIT 1;`);
    comprobar('la vida persistida del token es de 15 minutos',
      Math.abs(Number(vida) - 15) < 0.01, `${Number(vida)} min`);

    // Antes de envejecerlo: el token vale. Sin esta linea, el rechazo posterior podria deberse a
    // cualquier otra cosa y la prueba no distinguiria «vencido» de «nunca sirvio».
    const antesDeVencer = await pedir('POST', '/api/v1/auth/reset-password',
      { token: tokenCad, newPassword: `Intermedia.${marca}.2026!` });
    comprobar('el token recien emitido SI canjearia', antesDeVencer.status === 200,
      `HTTP ${antesDeVencer.status}`);

    // Se emite otro y se envejece: el anterior acaba de gastarse al comprobar que servia.
    const antesCad2 = tamanoLog();
    await pedir('POST', '/api/v1/auth/forgot-password', { tenantCode: TENANT, email: DESECHABLE });
    const tokenVencer = await esperarToken(DESECHABLE, antesCad2);

    const movidas = sql(`UPDATE ums_identity."PasswordResetTokens"
                         SET "ExpiresAtUtc" = now() - interval '1 second'
                         WHERE "Status" = 'Active'
                           AND "Id" = (SELECT "Id" FROM ums_identity."PasswordResetTokens"
                                       WHERE "Status" = 'Active'
                                       ORDER BY "IssuedAtUtc" DESC LIMIT 1)
                         RETURNING 1;`);
    comprobar('se pudo envejecer la fila del token', movidas === '1', `filas: ${movidas || '0'}`);

    const vencido = await pedir('POST', '/api/v1/auth/reset-password',
      { token: tokenVencer, newPassword: `Vencida.${marca}.2026!` });
    comprobar('un token VENCIDO no canjea', vencido.status >= 400, `HTTP ${vencido.status}`);

    // Y no delata por que: el mensaje de «vencido» debe ser el mismo que el de «no existe», o el
    // endpoint vuelve a ser un oraculo — esta vez sobre si un token dado existio alguna vez.
    const inexistente = await pedir('POST', '/api/v1/auth/reset-password',
      { token: 'z'.repeat(tokenVencer.length), newPassword: `Vencida.${marca}.2026!` });
    comprobar('vencido e inexistente son INDISTINGUIBLES',
      vencido.status === inexistente.status
      && JSON.stringify(vencido.datos) === JSON.stringify(inexistente.datos),
      `${vencido.status} vs ${inexistente.status}`);

    // La contrasena vigente no cambio por intentar canjear un token vencido.
    const sigueLaBuena = await pedir('POST', '/api/v1/client/authenticate', {
      tenantCode: TENANT, username: DESECHABLE, password: `Intermedia.${marca}.2026!`, format: 'JSON',
    });
    comprobar('el intento con token vencido no cambio nada',
      sigueLaBuena.status === 200, `HTTP ${sigueLaBuena.status}`);
  }
}

// ── El Tablero, si esta levantado ───────────────────────────────────────────

if (TABLERO) {
  console.log('\n── A traves del Tablero SDLC ──');
  const base = TABLERO.replace(/\/$/, '');

  const rec = async (correo) => {
    const r = await fetch(`${base}/api/auth/recuperar`, {
      method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email: correo }),
    });
    return { status: r.status, texto: await r.text() };
  };

  const a = await rec(DESECHABLE);
  const b = await rec(`no.existe.${marca}@evolith.example`);
  comprobar('el Tablero tampoco delata si la cuenta existe',
    a.status === b.status && a.texto === b.texto, `${a.status} vs ${b.status}`);
  comprobar('la respuesta del Tablero NO lleva el secreto',
    !/[A-Za-z0-9_-]{32,}/.test(a.texto), `${a.texto.length} bytes · ${a.texto.slice(0, 90)}`);

  // Y la propiedad que de verdad importa del intermediario: que el token siga saliendo SOLO por
  // el buzon. Un Tablero que reenviara al navegador lo que UMS le da convertiria el formulario de
  // recuperacion en una toma de cuentas para cualquiera que sepa un correo.
  const antesTablero = tamanoLog();
  await rec(DESECHABLE);
  const tokenTablero = await esperarToken(DESECHABLE, antesTablero);
  comprobar('la solicitud por el Tablero SI emite token, y solo al buzon',
    Boolean(tokenTablero) && !a.texto.includes(tokenTablero ?? '\u0000'),
    tokenTablero ? `${tokenTablero.slice(0, 12)}… no aparece en la respuesta` : 'ninguno');

  const correoAlta = `alta.${marca}@evolith.example`;
  const signup = await fetch(`${base}/api/auth/signup`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      displayName: 'Alta de prueba', email: correoAlta, password: `Alta.${marca}.2026!`,
    }),
  });
  const cuerpoAlta = await signup.text();
  comprobar('el alta se ACEPTA como solicitud, no como acceso concedido',
    signup.status === 202, `HTTP ${signup.status} · ${cuerpoAlta.slice(0, 120)}`);

  // Solicitada no es concedida: la cuenta recien pedida NO debe poder entrar todavia.
  const entrar = await pedir('POST', '/api/v1/client/authenticate', {
    tenantCode: TENANT, username: correoAlta, password: `Alta.${marca}.2026!`, format: 'JSON',
  });
  comprobar('la cuenta recien solicitada NO entra hasta que la aprueben',
    entrar.status !== 200, `HTTP ${entrar.status}`);
}

// ── Cierre ──────────────────────────────────────────────────────────────────

console.log(`\n${fallos === 0 ? '✔' : '✘'} ${pasos - fallos}/${pasos} comprobaciones.`);
console.log(`\nLa cuenta desechable ${DESECHABLE} (${idDesechable}) queda en la base a proposito: `
          + `borrarla aqui exigiria un borrado duro que este carril no debe ejercer. Se limpia con `
          + `el resembrado, igual que el resto de datos de desarrollo.`);

process.exit(fallos === 0 ? 0 : 1);
