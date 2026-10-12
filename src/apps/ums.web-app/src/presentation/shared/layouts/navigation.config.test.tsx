import { describe, it, expect, vi, afterEach } from 'vitest';
import { NAV_ROUTES, isExternalItem } from './navigation.config';
import type { NavExternalItem } from './navigation.config';

describe('navigation.config', () => {
  it('exports NAV_ROUTES with correct paths', () => {
    expect(NAV_ROUTES.tenants).toBe('/tenants');
    expect(NAV_ROUTES.users).toBe('/users');
    expect(NAV_ROUTES.delegations).toBe('/delegations');
    expect(NAV_ROUTES.systemSuites).toBe('/system-suites');
    expect(NAV_ROUTES.permissionTemplates).toBe('/permission-templates');
    expect(NAV_ROUTES.featureFlags).toBe('/feature-flags');
    expect(NAV_ROUTES.appConfigurations).toBe('/app-configurations');
    expect(NAV_ROUTES.parameterCatalog).toBe('/parameter-catalog');
    expect(NAV_ROUTES.profiles).toBe('/profiles');
    expect(NAV_ROUTES.profile).toBe('/profile');
    expect(NAV_ROUTES.login).toBe('/login');
  });

  it('las rutas del portal coinciden con las que siembra UMS en el grafo', () => {
    // El servidor publica estas mismas rutas en `MenuNodePresentation` (AuthorizationDevDataSeeder).
    // Si divergen, el menú construido desde el grafo llevaría a pantallas que el router no conoce.
    const sembradas = [
      '/tenants',
      '/users',
      '/delegations',
      '/system-suites',
      '/permission-templates',
      '/profiles',
      '/feature-flags',
      '/app-configurations',
      '/parameter-catalog',
    ];

    const declaradas = new Set(Object.values(NAV_ROUTES));
    expect(sembradas.filter(r => !declaradas.has(r))).toEqual([]);
  });

  describe('enlaces de observabilidad', () => {
    afterEach(() => {
      vi.unstubAllEnvs();
      vi.resetModules();
    });

    /** Los dos enlaces externos del módulo de autorización, con el entorno ya fijado. */
    async function enlaces(): Promise<Record<string, string>> {
      vi.resetModules();
      const modulo = await import('./navigation.config');
      const nulo = () => null;
      const deps = {
        ShieldCheck: nulo,
        Building2: nulo,
        Users: nulo,
        Cpu: nulo,
        Flag: nulo,
        User: nulo,
        Settings: nulo,
        Activity: nulo,
        ScrollText: nulo,
        primaryColorClass: '',
        indigoColorClass: '',
        t: {},
      };

      const externos = modulo
        .NAV_MODULES(deps)
        .flatMap(m => m.members)
        .filter(modulo.isExternalItem) as NavExternalItem[];

      return Object.fromEntries(externos.map(e => [e.id, e.href]));
    }

    it('por omision apuntan al proxy del contenedor', async () => {
      const href = await enlaces();

      expect(href.grafana).toBe('/grafana/d/ums-overview');
      expect(href.logs).toBe('/grafana/explore');
    });

    /* eslint-disable sonarjs/no-clear-text-protocols -- La URL es el dato bajo prueba: UAT se
       sirve HOY por HTTP sin cifrar (G-264, en curso) y la prueba tiene que reflejar el
       despliegue real. El dia que UAT tenga TLS, esta prueba cambia con el. */
    it('el despliegue puede mover la base y el tablero', async () => {
      // Es lo que hace falta en IIS: alli Grafana es un servicio propio en su puerto y `/grafana`
      // no existe, asi que con el valor por omision los dos enlaces llevaban a un 404. Y su
      // Grafana no tiene importado `ums-overview`, de modo que el tablero apunta a la lista.
      vi.stubEnv('VITE_GRAFANA_URL', 'http://srvtms:3000');
      vi.stubEnv('VITE_GRAFANA_DASHBOARD', '/dashboards');

      const href = await enlaces();

      expect(href.grafana).toBe('http://srvtms:3000/dashboards');
      expect(href.logs).toBe('http://srvtms:3000/explore');
    });
    /* eslint-enable sonarjs/no-clear-text-protocols */

    it('los dos son externos: el router del portal no los sirve', async () => {
      vi.resetModules();
      const modulo = await import('./navigation.config');
      const nulo = () => null;
      const miembros = modulo
        .NAV_MODULES({
          ShieldCheck: nulo,
          Building2: nulo,
          Users: nulo,
          Cpu: nulo,
          Flag: nulo,
          User: nulo,
          Settings: nulo,
          Activity: nulo,
          ScrollText: nulo,
          primaryColorClass: '',
          indigoColorClass: '',
          t: {},
        })
        .flatMap(m => m.members);

      const observabilidad = miembros.filter(
        m => isExternalItem(m) && (m.id === 'grafana' || m.id === 'logs')
      );

      expect(observabilidad).toHaveLength(2);
      expect(Object.values(NAV_ROUTES)).not.toContain('/explore');
    });
  });
});
