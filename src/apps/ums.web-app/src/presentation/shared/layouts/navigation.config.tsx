/**
 * Estructura estática de navegación — **respaldo**, no fuente de verdad (G-181).
 *
 * Desde que el shell construye el menú desde el grafo de autorización
 * (ver `use-shell-navigation`), esta declaración solo se usa cuando el grafo no trae navegación:
 * sesión sin sistema resuelto o entorno de desarrollo sin sembrar. Ahí el router sigue siendo la
 * barrera real de acceso.
 *
 * Sigue siendo la fuente de `NAV_ROUTES` (rutas del portal, que el prefetch y el respaldo usan) y
 * de los enlaces externos de observabilidad, que no pertenecen al grafo.
 */
import React from 'react';

export type NavItemId =
  | 'tenants'
  | 'users'
  | 'delegations'
  | 'systemSuites'
  | 'permissionTemplates'
  | 'featureFlags'
  | 'appConfigurations'
  | 'parameterCatalog'
  | 'profiles'
  | 'profile'
  | 'login';

export type UserRole = 'admin' | 'moderator' | 'user' | 'super_admin';

export interface NavItem {
  id: NavItemId;
  nameKey: string;
  icon: React.ReactNode;
}

/**
 * Enlace externo en la barra (p. ej. Grafana). A diferencia de {@link NavItem}
 * no navega por el router: abre `href` en una pestaña nueva. Se usa para exponer
 * la observabilidad técnica (métricas, trazas, logs) junto a la zona de
 * autorización sin acoplarla a una ruta de la SPA.
 */
export interface NavExternalItem {
  id: string;
  nameKey: string;
  icon: React.ReactNode;
  href: string;
  external: true;
}

export type NavMember = NavItem | NavExternalItem;

export const isExternalItem = (m: NavMember): m is NavExternalItem =>
  (m as NavExternalItem).external === true;

/**
 * Base de Grafana. Por omisión, el mismo origen en `/grafana/`: eso es cierto **solo** donde el
 * nginx del contenedor hace de proxy (ver `nginx.conf.template`), que es el despliegue en Kind.
 *
 * <p>SERVIDA POR IIS NO EXISTE ESA RUTA. En SRVTMS, Grafana es un servicio propio en su puerto, y
 * con el valor por omisión los dos enlaces de observabilidad llevaban a un 404 — se veían en la
 * barra, se podían pulsar y no llegaban a ninguna parte. Por eso el build de IIS declara
 * `VITE_GRAFANA_URL` (ver `scripts/iis/README.md`).</p>
 *
 * <p>Es una variable de BUILD, no de ejecución: cambiarla exige reconstruir la SPA. Se acepta
 * porque la dirección de Grafana cambia con el entorno, no con el uso.</p>
 *
 * Sin barra final: los enlaces la añaden.
 */
const GRAFANA_URL = (import.meta.env.VITE_GRAFANA_URL as string | undefined) ?? '/grafana';

/**
 * Camino del tablero al que apunta «Observabilidad».
 *
 * <p>Por omisión el tablero que el chart provisiona (`ums-overview.json`). Un despliegue cuyo
 * Grafana no lo tenga importado —el de SRVTMS, hoy— debe apuntar a la LISTA de tableros con
 * `VITE_GRAFANA_DASHBOARD=/dashboards`: una lista siempre existe, y es mejor que un enlace a un
 * identificador que ese Grafana no conoce y que responde «dashboard not found».</p>
 */
const GRAFANA_DASHBOARD =
  (import.meta.env.VITE_GRAFANA_DASHBOARD as string | undefined) ?? '/d/ums-overview';

export interface NavModule {
  key: string;
  nameKey: string;
  icon: React.ReactNode;
  members: NavMember[];
}

export const NAV_ROUTES: Record<NavItemId, string> = {
  tenants: '/tenants',
  users: '/users',
  delegations: '/delegations',
  systemSuites: '/system-suites',
  permissionTemplates: '/permission-templates',
  featureFlags: '/feature-flags',
  appConfigurations: '/app-configurations',
  parameterCatalog: '/parameter-catalog',
  profiles: '/profiles',
  profile: '/profile',
  login: '/login',
};

interface NavModulesFactoryDeps {
  ShieldCheck: React.ComponentType<{ className?: string }>;
  Building2: React.ComponentType<{ className?: string }>;
  Users: React.ComponentType<{ className?: string }>;
  Cpu: React.ComponentType<{ className?: string }>;
  Flag: React.ComponentType<{ className?: string }>;
  User: React.ComponentType<{ className?: string }>;
  Settings: React.ComponentType<{ className?: string }>;
  Activity: React.ComponentType<{ className?: string }>;
  ScrollText: React.ComponentType<{ className?: string }>;
  primaryColorClass: string;
  indigoColorClass: string;
  t: Record<string, string>;
}

export const NAV_MODULES = (deps: NavModulesFactoryDeps): NavModule[] => [
  {
    key: 'idm',
    nameKey: 'identityContext',
    icon: <deps.ShieldCheck className={`w-5 h-5 ${deps.primaryColorClass}`} />,
    members: [
      { id: 'tenants', nameKey: 'tenant', icon: <deps.Building2 className="w-4 h-4" /> },
      { id: 'users', nameKey: 'userAccounts', icon: <deps.Users className="w-4 h-4" /> },
    ],
  },
  {
    key: 'auth',
    nameKey: 'authorizationContext',
    icon: <deps.ShieldCheck className={`w-5 h-5 ${deps.primaryColorClass}`} />,
    members: [
      { id: 'systemSuites', nameKey: 'systemSuitesNav', icon: <deps.Cpu className="w-4 h-4" /> },
      {
        id: 'permissionTemplates',
        nameKey: 'permissionTemplates',
        icon: <deps.ShieldCheck className="w-4 h-4" />,
      },
      { id: 'profiles', nameKey: 'profilesHeader', icon: <deps.ShieldCheck className="w-4 h-4" /> },
      { id: 'featureFlags', nameKey: 'featureFlags', icon: <deps.Flag className="w-4 h-4" /> },
      // Observabilidad de la zona de autorización: datos técnicos (métricas,
      // trazas y logs) en Grafana, bajo el mismo origen en /grafana/.
      {
        id: 'grafana',
        nameKey: 'observabilityGrafana',
        icon: <deps.Activity className="w-4 h-4" />,
        href: `${GRAFANA_URL}${GRAFANA_DASHBOARD}`,
        external: true,
      },
      {
        id: 'logs',
        nameKey: 'observabilityLogs',
        icon: <deps.ScrollText className="w-4 h-4" />,
        href: `${GRAFANA_URL}/explore`,
        external: true,
      },
    ],
  },
  {
    key: 'sys',
    nameKey: 'systemDiagnostics',
    icon: <deps.Cpu className={`w-5 h-5 ${deps.indigoColorClass}`} />,
    members: [
      {
        id: 'appConfigurations',
        nameKey: 'systemParameters',
        icon: <deps.Settings className="w-4 h-4" />,
      },
      {
        id: 'parameterCatalog',
        nameKey: 'parameterCatalog',
        icon: <deps.Settings className="w-4 h-4" />,
      },
      { id: 'profile', nameKey: 'profileStats', icon: <deps.User className="w-4 h-4" /> },
    ],
  },
];
