// ¿Los grafos de `auth-graph/` siguen diciendo lo que dice la spec?
//
//   node src/provisioning/verificar-deriva-auth-graph.mjs            # AVISA
//   node src/provisioning/verificar-deriva-auth-graph.mjs --strict   # BLOQUEA
//   node src/provisioning/verificar-deriva-auth-graph.mjs --spec otra/spec.json
//
// EXISTE PORQUE LA DERIVA YA PASÓ, y en silencio. Los fixtures son el oráculo con el que se revisa
// un cambio de permisos —«un grants no se puede leer a ojo, un menuAccess sí»— pero nadie los
// regenera al tocar la spec, así que envejecen. El 2026-08-25 se midió: GERENTE y JEFE figuraban
// con los 84 scopes del ADMIN, y `aprobador.json` no existía. Durante ese tiempo, cualquiera que
// revisara un permiso mirando el fixture leía permisos que el cargador no concede.
//
// Un fixture que miente sobre quién puede qué es peor que no tener fixture: se consulta con la
// confianza de una fuente y responde con la autoridad de un archivo versionado.
//
// AVISA POR DEFECTO, NO BLOQUEA. Es deliberado y temporal: la deriva acumulada es anterior a este
// guion, y un gate que bloquea desde el primer día por deuda ajena obliga a `--no-verify`, que es
// como se pierden todos los gates a la vez. En cuanto los fixtures se resincronicen —decidiendo
// antes si manda la spec o lo desplegado en UAT— este guion pasa a `--strict` en el hook y la
// deriva vuelve a ser imposible en vez de solo visible.
import { execFileSync } from 'node:child_process';
import { readFileSync, readdirSync, existsSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const AQUI = dirname(fileURLToPath(import.meta.url));
const bandera = (n) => process.argv.includes(`--${n}`);
const valor = (n) => {
  const i = process.argv.indexOf(`--${n}`);
  return i !== -1 && process.argv[i + 1] ? process.argv[i + 1] : null;
};

const SPEC = resolve(valor('spec') || process.env.UMS_SPEC || join(AQUI, 'sdlc/sdlc-suite.json'));
const DIR = join(dirname(SPEC), 'auth-graph');
const ESTRICTO = bandera('strict');

if (!existsSync(SPEC)) { console.error(`No existe la spec: ${SPEC}`); process.exit(2); }

const spec = JSON.parse(readFileSync(SPEC, 'utf8'));
const roles = (spec.permissionTemplates ?? []).map((t) => t.role);
const archivoDe = (rol) => join(DIR, `${rol.toLowerCase()}.json`);

// Los scopes son la parte del grafo por la que se decide un permiso; el resto —tokens, ids de
// sesión— es envoltorio. Comparar el envoltorio haría ruidoso el guion sin decir nada de permisos.
const scopesDe = (g) => [...new Set(g?.permissions ?? [])].sort();

const derivas = [];
for (const rol of roles) {
  const f = archivoDe(rol);
  if (!existsSync(f)) { derivas.push({ rol, ausente: true }); continue; }

  let renderizado;
  try {
    renderizado = JSON.parse(execFileSync(
      process.execPath, [join(AQUI, 'render-auth-graph.mjs'), rol, '--spec', SPEC],
      { encoding: 'utf8', maxBuffer: 64 * 1024 * 1024 }));
  } catch (e) {
    derivas.push({ rol, error: e.message.split('\n')[0] });
    continue;
  }

  const enDisco = scopesDe(JSON.parse(readFileSync(f, 'utf8')));
  const segunSpec = scopesDe(renderizado);
  const sobran = enDisco.filter((s) => !segunSpec.includes(s));
  const faltan = segunSpec.filter((s) => !enDisco.includes(s));
  if (sobran.length || faltan.length) derivas.push({ rol, sobran, faltan });
}

if (derivas.length === 0) {
  console.log(`✔ auth-graph: los ${roles.length} grafos coinciden con la spec.`);
  process.exit(0);
}

const marca = ESTRICTO ? '✖' : '⚠';
console.log(`${marca} auth-graph: ${derivas.length} de ${roles.length} grafos NO coinciden con la spec.\n`);
for (const d of derivas) {
  if (d.ausente) { console.log(`  ${d.rol}: falta ${archivoDe(d.rol).replace(process.cwd() + '/', '')}`); continue; }
  if (d.error) { console.log(`  ${d.rol}: no se pudo renderizar — ${d.error}`); continue; }
  console.log(`  ${d.rol}:`);
  // Se nombran los primeros y se cuenta el resto: una lista de 44 scopes no se lee, y el número
  // sí dice si el fixture está algo desfasado o es de otro perfil entero.
  if (d.sobran.length) console.log(`     el fixture concede de más (${d.sobran.length}): ${d.sobran.slice(0, 4).join(', ')}${d.sobran.length > 4 ? ' …' : ''}`);
  if (d.faltan.length) console.log(`     el fixture no refleja (${d.faltan.length}): ${d.faltan.slice(0, 4).join(', ')}${d.faltan.length > 4 ? ' …' : ''}`);
}
console.log(`\n  Se regeneran con: node src/provisioning/render-auth-graph.mjs --spec ${SPEC.replace(process.cwd() + '/', '')}`);

if (!ESTRICTO) {
  console.log('  (aviso: no bloquea. Con --strict, sí.)');
  process.exit(0);
}
process.exit(1);
