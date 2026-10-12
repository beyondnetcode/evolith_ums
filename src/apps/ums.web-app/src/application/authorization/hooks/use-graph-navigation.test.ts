import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook } from '@testing-library/react';
import { useGraphNavigation, itemsNavegables } from './use-graph-navigation';

let menuAccess: unknown;

vi.mock('@app/stores/auth.store', () => ({
  useAuthStore: (selector: (s: unknown) => unknown) =>
    selector({ user: { authorizationGraph: { menuAccess } } }),
}));

const nodo = (
  code: string,
  kind: string,
  extra: Partial<{
    icon: string | null;
    route: string | null;
    actions: Array<{ actionCode: string; effect: string; source: string }>;
    children: unknown[];
  }> = {}
) => ({
  code,
  value: `Etiqueta ${code}`,
  kind,
  sortOrder: 1,
  icon: extra.icon ?? null,
  route: extra.route ?? null,
  actions: extra.actions ?? [],
  children: extra.children ?? [],
});

describe('useGraphNavigation', () => {
  beforeEach(() => {
    menuAccess = undefined;
  });

  it('devuelve vacío sin grafo', () => {
    const { result } = renderHook(() => useGraphNavigation());
    expect(result.current).toEqual([]);
  });

  it('conserva la jerarquía y el icono y la ruta de cada nodo', () => {
    menuAccess = [
      {
        code: 'PORT',
        value: 'Portafolio',
        sortOrder: 2,
        status: 'Active',
        icon: 'layout-dashboard',
        nodes: [
          nodo('DASHBOARDS', 'Menu', {
            icon: 'layout-dashboard',
            children: [
              nodo('DASH_EJEC', 'SubMenu', {
                children: [
                  nodo('PORT_DASHBOARD', 'Option', {
                    route: '/portafolio',
                    actions: [{ actionCode: 'VIEW', effect: 'Allow', source: 'Template' }],
                  }),
                ],
              }),
            ],
          }),
        ],
      },
    ];

    const { result } = renderHook(() => useGraphNavigation());
    const menu = result.current[0].items[0];

    // Desde el contrato 2.3.0 el módulo trae su propio icono, no solo sus nodos.
    expect(result.current[0].icon).toBe('layout-dashboard');
    expect(menu.icon).toBe('layout-dashboard');
    // Un menú agrupa: no navega.
    expect(menu.route).toBeNull();

    const hoja = menu.children[0].children[0];
    expect(hoja.route).toBe('/portafolio');
    expect(hoja.allowed.has('VIEW')).toBe(true);
  });

  it('separa lo concedido de lo denegado explícitamente', () => {
    menuAccess = [
      {
        code: 'PRD',
        value: 'Iniciativas',
        sortOrder: 1,
        status: 'Active',
        nodes: [
          nodo('ART_CERT', 'Option', {
            route: '/prds/artefactos/certificacion',
            actions: [
              { actionCode: 'VIEW', effect: 'Allow', source: 'Template' },
              { actionCode: 'CERTIFY', effect: 'Deny', source: 'Override' },
            ],
          }),
        ],
      },
    ];

    const { result } = renderHook(() => useGraphNavigation());
    const item = result.current[0].items[0];

    // `Deny` no es ausencia: la interfaz debe poder bloquear en vez de ocultar.
    expect(item.allowed.has('VIEW')).toBe(true);
    expect(item.denied.has('CERTIFY')).toBe(true);
    expect(item.allowed.has('CERTIFY')).toBe(false);
  });

  it('ordena los módulos y los nodos por su sortOrder', () => {
    menuAccess = [
      { code: 'B', value: 'B', sortOrder: 2, status: 'Active', icon: null, nodes: [] },
      { code: 'A', value: 'A', sortOrder: 1, status: 'Active', icon: null, nodes: [] },
    ];

    const { result } = renderHook(() => useGraphNavigation());
    expect(result.current.map(m => m.code)).toEqual(['A', 'B']);
    // Un módulo sin icono configurado llega como null, no como un icono inventado.
    expect(result.current[0].icon).toBeNull();
  });
});

describe('itemsNavegables', () => {
  it('devuelve solo lo que tiene ruta, a cualquier profundidad', () => {
    menuAccess = [
      {
        code: 'PRD',
        value: 'Iniciativas',
        sortOrder: 1,
        status: 'Active',
        nodes: [
          nodo('MENU', 'Menu', {
            children: [
              nodo('SUB', 'SubMenu', {
                children: [nodo('HOJA', 'Option', { route: '/prds' })],
              }),
            ],
          }),
        ],
      },
    ];

    const { result } = renderHook(() => useGraphNavigation());
    const navegables = itemsNavegables(result.current);

    // Menú y submenú agrupan; solo la hoja navega.
    expect(navegables.map(i => i.code)).toEqual(['HOJA']);
  });
});
