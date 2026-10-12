#!/usr/bin/env node
/**
 * render-auth-graph.mjs — proyecta la spec del Tablero SDLC en la respuesta que
 * UMS devuelve al autenticar: `LoginSuccessResponse` con su `authorizationGraph`.
 *
 * Existe para revisar la spec en la forma en que se consume, no en la forma en
 * que se declara: un `grants` no se puede leer a ojo, un `menuAccess` si.
 *
 * FORMA DEL PAYLOAD — la define `AuthGraphPayload`, el unico mapeador del grafo
 * (D-025), y la describe `libs/sdk/contracts/auth-graph.schema.json`. El
 * envoltorio es el `LoginSuccessResponse` de `AuthEndpoints.cs`. Tres rasgos que
 * conviene recordar al leer los archivos generados:
 *   1. Los ids son opcionales y se omiten salvo que el inquilino active
 *      AUTH_GRAPH_INCLUDE_TECHNICAL_METADATA; aqui se renderiza el caso por
 *      defecto (sin ids). Los que si ve el cliente son los del envoltorio
 *      (`userId`, `tenantId`, `profileId`).
 *   2. Cada nombre o etiqueta se serializa como `value`, no como `name` ni `label`.
 *   3. `menuAccess` es una lista PLANA de modulos (sin envoltorio `module`) y
 *      `domainPermissions` expone `resourceType`/`resourceCode`/`value` sin
 *      envoltorio `resource`.
 *
 * Y tres reglas de construccion que cambian lo que se ve:
 *   4. NINGUN modulo se filtra: viajan los 6 aunque no tengan una sola opcion
 *      concedida. Las opciones `NotGranted` tambien viajan — el cliente decide
 *      si las oculta o las deshabilita.
 *   5. `domainPermissions` enumera TODAS las acciones de la suite por recurso
 *      (13 filas por recurso, ordenadas por codigo), con el efecto resuelto.
 *      Fail-closed: sin item de plantilla, `NotGranted` (G-039).
 *   6. `scopes` va en minusculas, deduplicado y ordenado alfabeticamente.
 *
 * Uso:
 *   node render-auth-graph.mjs                 # todos los roles -> ./auth-graph/
 *   node render-auth-graph.mjs PRODUCT_OWNER   # un rol por stdout
 *
 * Resolucion de permisos (misma intencion que PermissionTemplate + grafo):
 *   a. Un `grant` que apunta explicitamente a una opcion define su conjunto de
 *      acciones y anula los grants mas amplios (opcion > submenu > menu >
 *      modulo > suite). Sin esa regla, un grant de modulo con `UPDATE` haria
 *      inutil cualquier recorte posterior a nivel de opcion.
 *   b. Un `effect: "deny"` gana sobre cualquier allow, en cualquier nivel.
 *   c. Sin grant que case, el efecto es `NotGranted` (no `Deny`): el grafo
 *      distingue «no concedido» de «prohibido».
 *   d. Los objetos de dominio se resuelven en su propio eje
 *      (`resource` > `module` > `suite`). Un grant de modulo o de suite alcanza
 *      a los recursos de ese ambito: el cargador materializara un item de
 *      plantilla por cada par (recurso, accion), porque UMS no hereda permisos
 *      de dominio desde el menu.
 */

import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';

// La spec se recibe por `--spec`, igual que en el cargador: este renderizador es el ORÁCULO de
// cualquier sistema, no solo del Tablero. La salida acompaña a su spec —`<carpeta>/auth-graph/`—
// para que el grafo esperado se revise en el mismo commit que lo cambia.
function leerArgumento(nombre) {
  const i = process.argv.indexOf(`--${nombre}`);
  return i !== -1 && process.argv[i + 1] ? process.argv[i + 1] : null;
}

const RUTA_SPEC = leerArgumento('spec') || process.env.UMS_SPEC;
if (!RUTA_SPEC) {
  console.error('Falta la especificación. Usa: --spec ruta/al/sistema-suite.json (o UMS_SPEC).');
  process.exit(2);
}

const RUTA_ABSOLUTA = resolve(process.cwd(), RUTA_SPEC);
const AQUI = dirname(RUTA_ABSOLUTA);
const spec = JSON.parse(readFileSync(RUTA_ABSOLUTA, 'utf8'));

const NIVEL_MENU = { option: 4, submenu: 3, menu: 2, module: 1, suite: 0 };
const NIVEL_RECURSO = { resource: 2, module: 1, suite: 0 };

const opciones = [];
for (const m of spec.modules) {
  for (const me of m.menus) {
    for (const sm of me.submenus) {
      for (const o of sm.options) opciones.push({ option: o, submenu: sm, menu: me, module: m });
    }
  }
}

const acciones = (grant, disponibles) => (grant.actions === '*' ? disponibles : grant.actions);
const nivelDe = (grant, tabla) =>
  tabla[Object.keys(tabla).find((k) => grant[k] !== undefined)] ?? -1;

/** Efecto comun a menu y dominio: deny gana, y solo el nivel mas especifico decide. */
function efecto(aplicables, actionCode, disponibles, tabla) {
  if (aplicables.some((g) => g.effect === 'deny' && acciones(g, disponibles).includes(actionCode)))
    return 'Deny';

  const permisos = aplicables.filter((g) => g.effect !== 'deny');
  if (permisos.length === 0) return 'NotGranted';

  const top = Math.max(...permisos.map((g) => nivelDe(g, tabla)));
  const concede = permisos
    .filter((g) => nivelDe(g, tabla) === top)
    .some((g) => acciones(g, disponibles).includes(actionCode));

  return concede ? 'Allow' : 'NotGranted';
}

const casaMenu = (g, ctx) =>
  (g.suite !== undefined && (g.suite === '*' || g.suite === spec.suite.code)) ||
  g.module === ctx.module.code ||
  g.menu === ctx.menu.code ||
  g.submenu === ctx.submenu.code ||
  g.option === ctx.option.code;

const casaRecurso = (g, recurso, module) =>
  (g.suite !== undefined && (g.suite === '*' || g.suite === spec.suite.code)) ||
  (g.module !== undefined && g.module === module.code) ||
  (g.resource !== undefined && (g.resource === '*' || g.resource === recurso.code));

const efectoOpcion = (plantilla, ctx, actionCode) =>
  efecto(plantilla.grants.filter((g) => casaMenu(g, ctx)), actionCode, ctx.option.actions, NIVEL_MENU);

const todasLasAcciones = spec.actions.map((a) => a.code);

const efectoRecurso = (plantilla, recurso, module, actionCode) =>
  efecto(
    plantilla.grants.filter((g) => casaRecurso(g, recurso, module)),
    actionCode,
    todasLasAcciones,
    NIVEL_RECURSO,
  );

/** Aplana el arbol de navegacion, sin presuponer profundidad. */
const aplanar = (modulos) => {
  const salida = [];
  const visitar = (nodos) => nodos.forEach((n) => { salida.push(n); visitar(n.children ?? []); });
  modulos.forEach((m) => visitar(m.nodes ?? []));
  return salida;
};

const AHORA = '2026-08-01T09:00:00Z';
const EXPIRA = '2026-08-01T10:00:00Z';
const nombreAccion = (code) => spec.actions.find((a) => a.code === code).value ?? spec.actions.find((a) => a.code === code).name;

function construir(plantilla) {
  const rol = spec.roles.find((r) => r.code === plantilla.role);

  // menuAccess: lista plana de modulos, ninguno filtrado (BuildMenuAccess).
  const menuAccess = spec.modules
    .slice()
    .sort((a, b) => a.sortOrder - b.sortOrder)
    .map((m) => ({
      code: m.code,
      value: m.name,
      sortOrder: m.sortOrder,
      status: 'Active',
      icon: m.icon ?? null,
      nodes: m.menus
        .slice()
        .sort((a, b) => a.sortOrder - b.sortOrder)
        .map((me) => ({
          code: me.code,
          value: me.name,
          kind: 'Menu',
          sortOrder: me.sortOrder,
          icon: me.icon ?? null,
          route: null,
          actions: [],
          children: me.submenus
            .slice()
            .sort((a, b) => a.sortOrder - b.sortOrder)
            .map((sm) => ({
              code: sm.code,
              value: sm.name,
              kind: 'SubMenu',
              sortOrder: sm.sortOrder,
              // Un submenú solo agrupa: ni icono ni ruta.
              icon: null,
              route: null,
              actions: [],
              children: sm.options
                .slice()
                .sort((a, b) => a.sortOrder - b.sortOrder)
                .map((o) => ({
                  code: o.code,
                  value: o.name,
                  kind: 'Option',
                  sortOrder: o.sortOrder,
                  icon: null,
                  route: o.route ?? null,
                  // v2.0.0: solo viajan las acciones resueltas. `NotGranted` es la ausencia.
                  actions: o.actions
                    .map((a) => ({
                      actionCode: a,
                      effect: efectoOpcion(plantilla, { option: o, submenu: sm, menu: me, module: m }, a),
                      source: 'Template',
                    }))
                    .filter((a) => a.effect !== 'NotGranted'),
                  children: [],
                }))
                .filter((o) => o.actions.length > 0),
            }))
            .filter((sub) => sub.children.length > 0),
        }))
        .filter((menu) => menu.children.length > 0),
    }))
    .filter((mod) => mod.nodes.length > 0);

  // domainPermissions: recursos de toda la suite ordenados por codigo; por cada
  // uno, TODAS las acciones de la suite ordenadas por codigo (BuildDomainPermissions).
  const recursos = spec.modules
    .flatMap((m) => (m.domainResources || []).map((r) => ({ r, m })))
    .sort((a, b) => a.r.code.localeCompare(b.r.code));

  const domainPermissions = recursos.map(({ r, m }) => ({
    resourceType: r.type,
    resourceCode: r.code,
    value: r.name,
    actions: todasLasAcciones
      .slice()
      .sort((a, b) => a.localeCompare(b))
      .map((a) => ({
        actionCode: a,
        value: nombreAccion(a),
        effect: efectoRecurso(plantilla, r, m, a),
        source: 'Template',
      }))
      .filter((a) => a.effect !== 'NotGranted'),
  })).filter((r) => r.actions.length > 0);

  const scopes = [
    ...new Set([
      ...aplanar(menuAccess).flatMap((n) =>
        n.actions
          .filter((a) => a.effect === 'Allow')
          .map((a) => `${n.code.toLowerCase()}.${a.actionCode.toLowerCase()}`),
      ),
      ...domainPermissions.flatMap((d) =>
        d.actions
          .filter((a) => a.effect === 'Allow')
          .map((a) => `${d.resourceCode.toLowerCase()}.${a.actionCode.toLowerCase()}`),
      ),
    ]),
  ].sort();

  // permissions (envoltorio): "CODE:ACTION" solo de las opciones Allow.
  const permissions = aplanar(menuAccess).flatMap((n) =>
    n.actions.filter((a) => a.effect === 'Allow').map((a) => `${n.code}:${a.actionCode}`),
  );

  const authorizationGraph = {
    schemaVersion: '2.3.0',
    context: {
      user: {
        email: `${plantilla.role.toLowerCase()}@evolith.example`,
        username: plantilla.role.toLowerCase(),
        value: rol.name,
        status: 'Active',
      },
      tenant: { code: spec.tenant.code, value: 'Evolith UMS', status: 'Active', isManagementOwner: false },
      systemSuite: { code: spec.suite.code, value: spec.suite.name, status: 'Active' },
      role: { code: rol.code, value: rol.name, hierarchyLevel: rol.hierarchyLevel },
      profile: { scope: 'OrgWide', isActive: true },
      branch: null,
    },
    onboardingPending: false,
    authentication: {
      method: 'Local',
      provider: null,
      mfaRequired: false,
      issuedAt: AHORA,
      sessionExpiresAt: EXPIRA,
    },
    // UMS ordena el catalogo de acciones por codigo (BuildActions).
    actions: spec.actions
      .map((a) => ({ code: a.code, value: a.name }))
      .sort((x, y) => x.code.localeCompare(y.code)),
    menuAccess,
    domainPermissions,
    // Solo los ajustes operativos actúan como banderas; los de presentación van en `settings`.
    featureFlags: spec.appSettings
      .filter((s) => !s.isClientVisible)
      .map((s) => ({ flagCode: s.key, isEnabled: s.value === 'true', matchedCriteriaType: null })),
    effectiveConfig: {
      sessionTimeoutMinutes: 60,
      maxLoginAttempts: 5,
      minPasswordLength: 12,
      mfaRequiredForAdmin: true,
      mfaAllowedMethods: ['Totp'],
      accessTokenDurationMs: 3600000,
      authUseExternalIdp: false,
    },
    // v2.1.0: solo lo marcado como visible, agrupado por espacio de nombres (G-178).
    settings: spec.appSettings
      .filter((s) => s.isClientVisible)
      .reduce((acc, s) => {
        const corte = s.key.indexOf('_');
        const espacio = corte <= 0 ? 'general' : s.key.slice(0, corte).toLowerCase();
        const resto = corte <= 0 ? s.key.toLowerCase() : s.key.slice(corte + 1).toLowerCase();
        (acc[espacio] ??= {})[resto] = s.value;
        return acc;
      }, {}),
    scopes,
    generatedAt: AHORA,
    validUntil: EXPIRA,
  };

  return {
    sessionId: '00000000-0000-0000-0000-000000000000',
    sessionTrackingId: '00000000-0000-0000-0000-000000000000-session',
    userId: '<guid del usuario>',
    username: plantilla.role.toLowerCase(),
    email: `${plantilla.role.toLowerCase()}@evolith.example`,
    tenantId: '<guid del tenant EVOLITH>',
    tenantCode: spec.tenant.code,
    tenantName: 'Evolith UMS',
    role: rol.code,
    roleName: rol.name,
    profileId: '<guid del perfil>',
    permissions,
    language: 'es',
    token: '<jwt de grafo>',
    tokenType: 'Bearer',
    expiresIn: 3600,
    refreshExpiresIn: 86400,
    isInternalAdmin: false,
    sessionParameters: {
      sessionTimeoutMinutes: 60,
      accessTokenDurationMs: 3600000,
      refreshTokenDurationMs: 86400000,
      maxLoginAttempts: 5,
      minPasswordLength: 12,
      mfaRequiredForAdmin: true,
      mfaAllowedMethods: ['Totp'],
      customBrandingEnabled: false,
      defaultLanguage: 'es',
      defaultTimezone: 'America/Lima',
    },
    authorizationGraph,
    graphFormat: 'json',
    refreshToken: null,
  };
}

// El rol es el primer argumento POSICIONAL. Se descartan las banderas y el valor de `--spec`,
// que antes se colaba como nombre de rol y hacía fallar la ejecución completa.
const posicionales = process.argv.slice(2).filter((arg, i, todos) => {
  if (arg.startsWith('--')) return false;
  return !(i > 0 && todos[i - 1] === '--spec');
});
const pedido = posicionales[0];
if (pedido) {
  const plantilla = spec.permissionTemplates.find((t) => t.role === pedido);
  if (!plantilla) {
    console.error(`Rol desconocido: ${pedido}. Roles: ${spec.roles.map((r) => r.code).join(', ')}`);
    process.exit(1);
  }
  console.log(JSON.stringify(construir(plantilla), null, 2));
} else {
  const destino = join(AQUI, 'auth-graph');
  mkdirSync(destino, { recursive: true });
  for (const plantilla of spec.permissionTemplates) {
    const login = construir(plantilla);
    writeFileSync(join(destino, `${plantilla.role.toLowerCase()}.json`), `${JSON.stringify(login, null, 2)}\n`);
    const acciones = aplanar(login.authorizationGraph.menuAccess).flatMap((n) => n.actions);
    const dom = login.authorizationGraph.domainPermissions.flatMap((d) => d.actions);
    const bytes = Buffer.byteLength(JSON.stringify(login));
    console.log(
      `${plantilla.role.padEnd(15)} nav ${String(acciones.filter((a) => a.effect === 'Allow').length).padStart(2)} Allow / ` +
        `${String(acciones.filter((a) => a.effect === 'Deny').length).padStart(2)} Deny · ` +
        `dominio ${String(dom.length).padStart(3)} · scopes ${String(login.authorizationGraph.scopes.length).padStart(3)} · ` +
        `${(bytes / 1024).toFixed(1)} KB`,
    );
  }
}
