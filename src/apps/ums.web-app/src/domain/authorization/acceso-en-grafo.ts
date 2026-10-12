import {
  AccessEffect,
  AuthorizationGraph,
  GraphNavigationNode,
} from './schemas/authorization-graph.schema';

/**
 * Resolución de acceso sobre el grafo. Funciones puras, sin store ni React.
 *
 * VIVE AQUÍ Y NO EN CADA CONSUMIDOR porque G-262 fue exactamente eso: la misma lógica escrita dos
 * veces —en `use-access-resolution.ts` y en `RequireAccess.ts`— y las dos recorriendo una forma del
 * grafo que el productor había dejado de emitir. Un defecto duplicado se arregla dos veces o no se
 * arregla; con un solo sitio, la próxima vez que el contrato cambie hay un solo sitio que cambiar.
 *
 * LA FORMA CANÓNICA es `menuAccess[].nodes[].children[]`: un árbol de PROFUNDIDAD LIBRE desde
 * ADR-0156. La anterior, de tres niveles fijos, perdía los nodos que no encajaban.
 */

/** Recorre el árbol en profundidad, a cualquier nivel. */
export function* recorrerNodos(nodos: readonly GraphNavigationNode[]): Generator<GraphNavigationNode> {
  for (const nodo of nodos ?? []) {
    yield nodo;
    yield* recorrerNodos(nodo.children ?? []);
  }
}

/** Solo `Allow` concede. `Deny` gana sobre todo y `NotGranted` no abre nada. */
export const concede = (effect: AccessEffect): boolean => effect === AccessEffect.Allow;

/** ¿Alguna acción de este nodo concede? */
export const nodoConcede = (nodo: GraphNavigationNode): boolean =>
  (nodo.actions ?? []).some(a => concede(a.effect));

/** Busca un nodo por código en todos los módulos, a cualquier profundidad. */
export function buscarNodo(
  graph: AuthorizationGraph,
  code: string,
  moduleCode?: string,
): GraphNavigationNode | null {
  for (const mod of graph.menuAccess ?? []) {
    if (moduleCode && mod.code !== moduleCode) continue;
    for (const nodo of recorrerNodos(mod.nodes ?? [])) {
      if (nodo.code === code) return nodo;
    }
  }
  return null;
}

/**
 * ¿Concede el menú? Existe Y concede — él o alguno de sus descendientes.
 *
 * La existencia NO basta, y la versión anterior se conformaba con ella: el grafo trae también los
 * nodos `NotGranted` porque el cliente decide si los oculta o los deshabilita (ADR-0156), así que
 * con la sola presencia como criterio un menú explícitamente no concedido pasaba por accesible.
 *
 * Y no se le exigen acciones propias: un nodo que solo agrupa no las lleva, y exigírselas lo
 * dejaría inaccesible con todas sus opciones concedidas.
 */
export function concedeMenu(graph: AuthorizationGraph, moduleCode: string, menuCode: string): boolean {
  const nodo = buscarNodo(graph, menuCode, moduleCode);
  if (!nodo) return false;
  if (nodoConcede(nodo)) return true;
  for (const hijo of recorrerNodos(nodo.children ?? [])) if (nodoConcede(hijo)) return true;
  return false;
}

/**
 * ¿Concede la opción dentro del menú? `optionCode` admite las dos formas que los consumidores
 * usan: el código de un nodo descendiente, o el de una acción del propio nodo.
 */
export function concedeOpcion(graph: AuthorizationGraph, menuCode: string, optionCode: string): boolean {
  const nodo = buscarNodo(graph, menuCode);
  if (!nodo) return false;

  const accionPropia = (nodo.actions ?? []).find(a => a.actionCode === optionCode);
  if (accionPropia) return concede(accionPropia.effect);

  for (const hijo of recorrerNodos(nodo.children ?? [])) {
    if (hijo.code === optionCode) return nodoConcede(hijo);
  }
  return false;
}
