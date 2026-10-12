// Compara los DOS modelos de RiskScore de promoción de rol sobre todos los pares de una spec.
//
//   node src/provisioning/comparar-riesgo-de-promocion.mjs
//   node src/provisioning/comparar-riesgo-de-promocion.mjs --spec src/provisioning/ums/ums-suite.json
//   node src/provisioning/comparar-riesgo-de-promocion.mjs --umbral 70 --md tabla.md
//
// No toca ninguna instancia: lee la spec, expande las concesiones con las MISMAS reglas que el
// cargador y aplica las dos fórmulas. Existe porque cambiar de modelo de riesgo sin ver los pares
// antes y después es cambiar a ciegas cuántas promociones exigen revisión reforzada —y ese número
// es la única consecuencia observable del cambio.
//
//   iga-risk-heuristic-v1  HeuristicRiskScoreCalculator     salto en el organigrama
//   iga-risk-graph-v2      RiesgoPorDiferencialDePermisos   escrituras ganadas + editar/aprobar
//
// Las dos fórmulas se transcriben aquí, no se importan: son C#. Si alguna cambia allí y no aquí,
// la tabla miente; las constantes van juntas y nombradas para que la divergencia se vea.

import { readFileSync, writeFileSync } from 'node:fs';

const leerArgumento = (n) => {
  const i = process.argv.indexOf(`--${n}`);
  return i !== -1 && process.argv[i + 1] ? process.argv[i + 1] : null;
};

const RUTA = leerArgumento('spec') ?? 'src/provisioning/sdlc/sdlc-suite.json';
const UMBRAL = Number(leerArgumento('umbral') ?? 70);
const RUTA_MD = leerArgumento('md');

const spec = JSON.parse(readFileSync(RUTA, 'utf8'));

// ── Catálogo: opciones (hojas) con sus acciones vinculadas ───────────────────
const opciones = new Map(); // code → { code, moduleCode, acciones: [], ancestros: [codes] }

for (const modulo of spec.modules ?? []) {
  const registrar = (nodo, ancestros) => {
    const hijos = nodo.options ?? nodo.submenus ?? nodo.children ?? [];
    if (nodo.actions) {
      opciones.set(nodo.code, {
        code: nodo.code,
        moduleCode: modulo.code,
        acciones: nodo.actions,
        ancestros,
      });
    }
    for (const h of hijos) registrar(h, [...ancestros, nodo.code]);
  };
  for (const menu of modulo.menus ?? []) registrar(menu, []);
}

const LECTURA = new Set(['VIEW', 'SEARCH', 'EXPORT']);
const esEscritura = (accion) => !LECTURA.has(accion.toUpperCase());

/** Superficie de escritura de la suite: pares (opción, acción) vinculados que no leen. */
const SUPERFICIE = [...opciones.values()]
  .flatMap((o) => o.acciones.filter(esEscritura).map((a) => `${o.code}.${a}`))
  .length;

// ── Expansión de concesiones, con las reglas del cargador ────────────────────
// Contenedor (suite/module/menu/submenu): la lista ACOTA y solo concede lo que la opción tiene
// vinculado. Opción concreta: la lista es literal. Deny gana sobre allow.
function escriturasDe(plantilla) {
  const allow = new Set();
  const deny = new Set();

  const conceder = (opcion, acciones, esDeny, literal) => {
    const codigos = acciones === '*'
      ? opcion.acciones
      : (literal ? acciones : acciones.filter((c) => opcion.acciones.includes(c)));
    for (const c of codigos) {
      if (!esEscritura(c)) continue;
      (esDeny ? deny : allow).add(`${opcion.code}.${c}`.toLowerCase());
    }
  };

  const bajo = (codigo) => [...opciones.values()].filter(
    (o) => o.code === codigo || o.ancestros.includes(codigo));

  for (const g of plantilla.grants ?? []) {
    const esDeny = g.effect === 'deny';
    const acciones = g.actions === '*' ? '*' : (Array.isArray(g.actions) ? g.actions : [g.actions]);

    if (g.suite !== undefined) {
      for (const o of opciones.values()) conceder(o, acciones, esDeny, false);
    } else if (g.module !== undefined) {
      for (const o of opciones.values()) if (o.moduleCode === g.module) conceder(o, acciones, esDeny, false);
    } else if (g.menu !== undefined || g.submenu !== undefined) {
      for (const o of bajo(g.menu ?? g.submenu)) conceder(o, acciones, esDeny, false);
    } else if (g.option !== undefined) {
      const o = opciones.get(g.option);
      if (o) conceder(o, acciones, esDeny, true);
    }
    // `resource:` concede sobre objetos de dominio, no sobre nodos: el cálculo v2 mide escrituras
    // de navegación, así que aquí no entra (igual que en el calculador).
  }

  for (const d of deny) allow.delete(d);
  return allow;
}

// ── Las dos fórmulas ─────────────────────────────────────────────────────────
const V1 = { base: 10, porNivel: 15, porPaso: 10, maxPasos: 3 };
const sensibilidad = (nivel) => (nivel >= 4 ? 30 : nivel === 3 ? 20 : nivel === 2 ? 10 : 5);

const v1 = (origen, destino) =>
  Math.min(100, V1.base
    + Math.max(0, destino.hierarchyLevel - origen.hierarchyLevel) * V1.porNivel
    + Math.min(V1.maxPasos, Math.max(0, destino.promotionOrder - origen.promotionOrder)) * V1.porPaso
    + sensibilidad(destino.hierarchyLevel));

// `toxica` vale lo que el umbral por defecto menos la base: romper la separación entre editar y
// aprobar basta por sí solo para exigir revisión reforzada, gane la promoción mucho o poco.
const V2 = { base: 10, maxEscrituras: 60, toxica: 60 };
const EDITA = ['art_editor.update', 'art_editor.upload'];
const APRUEBA = ['art_cert.certify', 'art_revision.approve'];

const rompe = (esc) => EDITA.some((e) => esc.has(e)) && APRUEBA.some((a) => esc.has(a));

const v2 = (escOrigen, escDestino) => {
  const nuevas = [...escDestino].filter((e) => !escOrigen.has(e));
  const peso = SUPERFICIE === 0 ? 0
    : Math.min(V2.maxEscrituras, Math.ceil((nuevas.length * V2.maxEscrituras) / SUPERFICIE));
  // Diferencial, igual que el calculador: pesa que la promoción ROMPA la separación, no que el
  // destino la tenga rota.
  const toxica = rompe(escDestino) && !rompe(escOrigen);
  return {
    score: Math.min(100, V2.base + peso + (toxica ? V2.toxica : 0)),
    nuevas: nuevas.length,
    toxica,
  };
};

// ── Tabla ────────────────────────────────────────────────────────────────────
const roles = spec.roles ?? [];
const plantillaDe = new Map((spec.permissionTemplates ?? []).map((t) => [t.role, t]));
const escrituras = new Map(roles.map((r) => [
  r.code,
  plantillaDe.has(r.code) ? escriturasDe(plantillaDe.get(r.code)) : new Set(),
]));

const filas = [];
for (const origen of roles) {
  for (const destino of roles) {
    if (origen.code === destino.code) continue;
    const r = v2(escrituras.get(origen.code), escrituras.get(destino.code));
    filas.push({
      origen: origen.code,
      destino: destino.code,
      v1: v1(origen, destino),
      v2: r.score,
      nuevas: r.nuevas,
      toxica: r.toxica,
    });
  }
}

filas.sort((a, b) => b.v2 - a.v2 || b.v1 - a.v1 || a.origen.localeCompare(b.origen));

const cruzanV1 = filas.filter((f) => f.v1 >= UMBRAL);
const cruzanV2 = filas.filter((f) => f.v2 >= UMBRAL);

const md = [
  `# RiskScore de promoción: v1 contra v2 — ${spec.suite?.code ?? RUTA}`,
  '',
  `Superficie de escritura de la suite: **${SUPERFICIE}** pares (opción, acción).`,
  `Umbral de revisión reforzada: **${UMBRAL}**.`,
  '',
  `| Origen | Destino | v1 | v2 | Escrituras nuevas | Editar+aprobar | Cruza |`,
  `| --- | --- | ---: | ---: | ---: | :-: | :-: |`,
  ...filas.map((f) => {
    const cruza = f.v2 >= UMBRAL ? (f.v1 >= UMBRAL ? 'ambas' : '**v2**') : (f.v1 >= UMBRAL ? 'solo v1' : '—');
    return `| ${f.origen} | ${f.destino} | ${f.v1} | ${f.v2} | ${f.nuevas} | ${f.toxica ? 'sí' : '—'} | ${cruza} |`;
  }),
  '',
  `Pares: **${filas.length}**. Cruzan el umbral: **${cruzanV1.length}** con v1, **${cruzanV2.length}** con v2.`,
  '',
  `Destinos que cruzan con v1: ${[...new Set(cruzanV1.map((f) => f.destino))].join(', ') || '—'}.`,
  `Destinos que cruzan con v2: ${[...new Set(cruzanV2.map((f) => f.destino))].join(', ') || '—'}.`,
  '',
].join('\n');

if (RUTA_MD) {
  writeFileSync(RUTA_MD, md);
  console.log(`Tabla escrita en ${RUTA_MD}`);
} else {
  console.log(md);
}
