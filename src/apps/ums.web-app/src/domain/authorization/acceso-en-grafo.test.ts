import { describe, it, expect } from 'vitest';
import {
  AccessEffect,
  AuthorizationGraph,
  GraphNavigationNode,
  PermissionSource,
} from './schemas/authorization-graph.schema';
import { buscarNodo, concedeMenu, concedeOpcion, recorrerNodos } from './acceso-en-grafo';

/** Nodo mínimo con la forma CANÓNICA: `children`, no `subMenus` ni `options`. */
const nodo = (
  code: string,
  effect: AccessEffect | null,
  children: GraphNavigationNode[] = [],
): GraphNavigationNode => ({
  code,
  value: code,
  kind: children.length ? 'Menu' : 'Option',
  sortOrder: 1,
  icon: null,
  route: null,
  actions: effect === null ? [] : [{ actionCode: 'VIEW', effect, source: PermissionSource.Template }],
  children,
});

const grafo = (nodes: GraphNavigationNode[]): AuthorizationGraph =>
  ({ menuAccess: [{ code: 'IDM', value: 'Identidad', sortOrder: 1, status: 'Active', icon: null, nodes }], featureFlags: [] }) as unknown as AuthorizationGraph;

describe('acceso-en-grafo (G-262)', () => {
  it('recorre el árbol a cualquier profundidad', () => {
    // La forma anterior fijaba TRES niveles y perdía lo que no encajaba. El contrato no fija ninguno.
    const g = grafo([nodo('A', null, [nodo('B', null, [nodo('C', AccessEffect.Allow)])])]);
    const codigos = [...recorrerNodos(g.menuAccess[0].nodes)].map(n => n.code);
    expect(codigos).toEqual(['A', 'B', 'C']);
  });

  it('encuentra un nodo profundo por código', () => {
    const g = grafo([nodo('MENU', null, [nodo('SUB', null, [nodo('OPCION', AccessEffect.Allow)])])]);
    expect(buscarNodo(g, 'OPCION')?.code).toBe('OPCION');
  });

  it('un menú concedido a través de un descendiente es accesible', () => {
    // Un nodo que solo agrupa no lleva acciones propias: exigírselas lo dejaría inaccesible con
    // todas sus opciones concedidas.
    const g = grafo([nodo('USERS', null, [nodo('USERS_LIST', AccessEffect.Allow)])]);
    expect(concedeMenu(g, 'IDM', 'USERS')).toBe(true);
  });

  it('un menú presente pero NO concedido no es accesible', () => {
    // La versión anterior devolvía `!!menu`: bastaba con existir. Y el grafo trae también los
    // nodos NotGranted, porque el cliente decide si los oculta o los deshabilita.
    const g = grafo([nodo('USERS', null, [nodo('USERS_LIST', AccessEffect.NotGranted)])]);
    expect(concedeMenu(g, 'IDM', 'USERS')).toBe(false);
  });

  it('Deny no concede', () => {
    const g = grafo([nodo('USERS', null, [nodo('USERS_LIST', AccessEffect.Deny)])]);
    expect(concedeMenu(g, 'IDM', 'USERS')).toBe(false);
  });

  it('una opción se resuelve por código de nodo descendiente', () => {
    const g = grafo([nodo('USERS', null, [nodo('MANAGE_USERS', AccessEffect.Allow)])]);
    expect(concedeOpcion(g, 'USERS', 'MANAGE_USERS')).toBe(true);
  });

  it('una opción se resuelve también por código de acción del propio nodo', () => {
    // Las dos formas que los consumidores usaban, conservadas.
    const g = grafo([nodo('USERS', AccessEffect.Allow)]);
    expect(concedeOpcion(g, 'USERS', 'VIEW')).toBe(true);
  });

  it('un menú de otro módulo no se cuela', () => {
    const g = grafo([nodo('USERS', null, [nodo('USERS_LIST', AccessEffect.Allow)])]);
    expect(concedeMenu(g, 'AUTH', 'USERS')).toBe(false);
  });

  it('lo que no existe no concede', () => {
    const g = grafo([nodo('USERS', AccessEffect.Allow)]);
    expect(concedeMenu(g, 'IDM', 'NO_EXISTE')).toBe(false);
    expect(concedeOpcion(g, 'USERS', 'NO_EXISTE')).toBe(false);
  });
});
