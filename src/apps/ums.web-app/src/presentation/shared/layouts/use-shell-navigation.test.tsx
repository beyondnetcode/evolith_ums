import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook } from '@testing-library/react';
import { useShellNavigation, claveActiva } from './use-shell-navigation';
import type { ShellModule } from './use-shell-navigation';

let menuAccess: unknown;

vi.mock('@app/stores/auth.store', () => ({
  useAuthStore: (selector: (s: unknown) => unknown) =>
    selector({ user: { authorizationGraph: { menuAccess } } }),
}));

vi.mock('@app/i18n/use-i18n', () => ({
  useI18n: () => ({
    identityContext: 'Identidad',
    tenant: 'Empresas',
    observabilityGrafana: 'Grafana',
    observabilityLogs: 'Logs',
  }),
}));

const nodo = (
  code: string,
  kind: string,
  extra: Partial<{ icon: string | null; route: string | null; children: unknown[] }> = {}
) => ({
  code,
  value: `Etiqueta ${code}`,
  kind,
  sortOrder: 1,
  icon: extra.icon ?? null,
  route: extra.route ?? null,
  actions: [],
  children: extra.children ?? [],
});

describe('useShellNavigation', () => {
  beforeEach(() => {
    menuAccess = undefined;
  });

  it('sin grafo cae al respaldo estático', () => {
    const { result } = renderHook(() => useShellNavigation());

    // El respaldo son los módulos declarados en navigation.config, con sus etiquetas traducidas.
    expect(result.current.map(m => m.key)).toEqual(['idm', 'auth', 'sys']);
    expect(result.current[0].label).toBe('Identidad');
    expect(result.current[0].items[0].route).toBe('/tenants');
    expect(result.current[0].items[0].prefetchId).toBe('tenants');
  });

  it('con grafo, la ESTRUCTURA viene del grafo y el RÓTULO de esta aplicación (ADR-UMS-104)', () => {
    menuAccess = [
      {
        code: 'IDM',
        value: 'Identidad y Accesos',
        sortOrder: 1,
        status: 'Active',
        icon: 'shield-check',
        nodes: [
          nodo('TENANTS', 'Menu', {
            icon: 'building',
            route: '/tenants',
            children: [
              nodo('TENANTS_LIST', 'SubMenu', { children: [nodo('VIEW_TENANTS', 'Option')] }),
            ],
          }),
        ],
      },
    ];

    const { result } = renderHook(() => useShellNavigation());

    expect(result.current).toHaveLength(1);

    // El grafo dice `Identidad y Accesos`; se pinta `Identidad`, que es lo que esta aplicación
    // llama a ese módulo en el idioma activo. Si mandara el catálogo, el conmutador de idioma
    // cambiaría toda la interfaz menos el menú (G-283).
    expect(result.current[0].label).toBe('Identidad');

    // La pantalla es el menú con ruta; lo que cuelga de ella son permisos, no destinos.
    expect(result.current[0].items.map(i => i.key)).toEqual(['TENANTS']);
    expect(result.current[0].items[0].label).toBe('Empresas');
    expect(result.current[0].items[0].route).toBe('/tenants');
    expect(result.current[0].items[0].prefetchId).toBe('tenants');
  });

  it('un código que esta aplicación no conoce se pinta con el rótulo del grafo', () => {
    // El respaldo es lo que hace viable traducir en el cliente: una opción recién dada de alta en
    // UMS aparece con el nombre del catálogo, no en blanco, hasta que alguien la traduzca.
    menuAccess = [
      {
        code: 'DESCONOCIDO',
        value: 'Módulo Nuevo',
        sortOrder: 1,
        status: 'Active',
        nodes: [nodo('NUEVA_PANTALLA', 'Option', { route: '/pantalla-que-no-existe-aun' })],
      },
    ];

    const { result } = renderHook(() => useShellNavigation());

    expect(result.current[0].label).toBe('Módulo Nuevo');
    expect(result.current[0].items[0].label).toBe('Etiqueta NUEVA_PANTALLA');
  });

  it('desciende hasta la primera ruta cuando el menú solo agrupa', () => {
    menuAccess = [
      {
        code: 'PRD',
        value: 'Iniciativas',
        sortOrder: 1,
        status: 'Active',
        nodes: [
          nodo('DASHBOARDS', 'Menu', {
            icon: 'layout-dashboard',
            children: [
              nodo('DASH_EJEC', 'SubMenu', {
                children: [nodo('PORT_DASHBOARD', 'Option', { route: '/portafolio' })],
              }),
            ],
          }),
        ],
      },
    ];

    const { result } = renderHook(() => useShellNavigation());
    const items = result.current[0].items;

    expect(items.map(i => i.key)).toEqual(['PORT_DASHBOARD']);
    expect(items[0].route).toBe('/portafolio');
    // Ruta ajena al portal: no hay pantalla que precargar, y eso no debe romper el hover.
    expect(items[0].prefetchId).toBeUndefined();
  });

  it('la ficha del propio usuario se pinta aunque el grafo no la conceda', () => {
    // No es una excepción cómoda: el sujeto y el recurso son la misma persona, así que pedirle
    // permiso al catálogo no protege nada — y dejarla fuera la volvía inalcanzable en cuanto la
    // navegación pasó a construirse del grafo.
    menuAccess = [
      {
        code: 'SYS',
        value: 'Sistema',
        sortOrder: 1,
        status: 'Active',
        nodes: [nodo('SYSTEM_SUITES', 'Option', { route: '/system-suites' })],
      },
    ];

    const { result } = renderHook(() => useShellNavigation());

    const claves = result.current.flatMap(m => m.items.map(i => i.key));
    expect(claves).toContain('profile');
  });

  it('conserva los enlaces externos de observabilidad en su módulo', () => {
    menuAccess = [
      {
        code: 'AUTH',
        value: 'Autorización',
        sortOrder: 1,
        status: 'Active',
        nodes: [nodo('SYSTEM_SUITES', 'Menu', { route: '/system-suites' })],
      },
    ];

    const { result } = renderHook(() => useShellNavigation());
    const items = result.current[0].items;

    // Grafana no es una ruta del grafo —aplica su propia autenticación—, pero vive en ese módulo.
    expect(items.map(i => i.key)).toEqual(['SYSTEM_SUITES', 'grafana', 'logs']);
    expect(items[1].href).toContain('/grafana');
    expect(items[1].route).toBeUndefined();
  });

  it('descarta el módulo que no aporta ninguna fila navegable, y si no queda ninguna cae al respaldo', () => {
    menuAccess = [
      {
        code: 'VACIO',
        value: 'Sin pantallas',
        sortOrder: 1,
        status: 'Active',
        nodes: [nodo('SOLO_AGRUPA', 'Menu')],
      },
    ];

    const { result } = renderHook(() => useShellNavigation());

    // El módulo sin filas se descarta, pero el rail NO se queda vacío: un grafo cuyos nodos no
    // declaran ruta —así llega la suite `SIL`— dejaba al usuario dentro de la aplicación y sin un
    // solo destino. Sin nada que pintar manda el respaldo estático, igual que sin grafo.
    expect(result.current.map(m => m.key)).toEqual(['idm', 'auth', 'sys']);
    expect(result.current[0].items[0].route).toBe('/tenants');
  });

  it('el módulo sin filas se descarta cuando OTRO sí aporta: ahí no hay respaldo que valga', () => {
    menuAccess = [
      {
        code: 'VACIO',
        value: 'Sin pantallas',
        sortOrder: 1,
        status: 'Active',
        nodes: [nodo('SOLO_AGRUPA', 'Menu')],
      },
      {
        code: 'SYS',
        value: 'Sistema',
        sortOrder: 2,
        status: 'Active',
        nodes: [nodo('SYSTEM_SUITES', 'Option', { route: '/system-suites' })],
      },
    ];

    const { result } = renderHook(() => useShellNavigation());
    expect(result.current.map(m => m.key)).toEqual(['SYS']);
  });
});

describe('claveActiva', () => {
  const modulos = [
    {
      key: 'AUTH',
      label: 'Autorización',
      icon: null,
      items: [
        { key: 'SUITES', label: 'Sistemas', icon: null, route: '/system-suites' },
        { key: 'PERFIL', label: 'Perfil', icon: null, route: '/profile' },
        { key: 'PERFILES', label: 'Perfiles', icon: null, route: '/profiles' },
        { key: 'GRAFANA', label: 'Grafana', icon: null, href: '/grafana' },
      ],
    },
  ] as unknown as ShellModule[];

  it('marca la fila cuya ruta contiene a la actual', () => {
    expect(claveActiva(modulos, '/system-suites')).toBe('SUITES');
    expect(claveActiva(modulos, '/system-suites/abc')).toBe('SUITES');
  });

  it('no confunde rutas que comparten prefijo textual', () => {
    // `/profiles` no es un hijo de `/profile`: el corte debe ser por segmento, no por caracteres.
    expect(claveActiva(modulos, '/profiles')).toBe('PERFILES');
    expect(claveActiva(modulos, '/profile')).toBe('PERFIL');
  });

  it('devuelve null fuera de la navegación', () => {
    // Antes se devolvía «tenants» por defecto, y una ruta desconocida dejaba una fila marcada
    // como activa sin estarlo.
    expect(claveActiva(modulos, '/login')).toBeNull();
  });
});
