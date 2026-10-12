/**
 * La navegación que pinta el shell, con el grafo como fuente de verdad (G-181).
 *
 * Hasta ahora el menú se declaraba en `navigation.config` y el grafo solo se usaba para **filtrar**
 * esa lista, traduciendo a mano el id de cada pantalla a un código de menú. Eran dos fuentes: dar de
 * alta una opción en UMS no la hacía aparecer, y renombrar un menú en la base no cambiaba la barra.
 *
 * Aquí la relación se invierte: el grafo **construye** el menú —módulos, iconos, rutas y orden— y
 * la configuración estática queda como respaldo para cuando no hay **nada que pintar** (sesión sin
 * sistema resuelto, grafo cuyos nodos no declaran ruta, o entorno de desarrollo sin sembrar), donde
 * el router sigue siendo la última barrera.
 *
 * El RÓTULO es la excepción, y es deliberada (ADR-UMS-104): lo pone esta aplicación desde su i18n,
 * no el catálogo. Si viniera de la base, el conmutador de idioma cambiaría toda la interfaz menos
 * el menú —medido: el rail decía «Empresas» con la aplicación en inglés (G-283)—. El `value` del
 * grafo no se tira: es el respaldo para un código que esta aplicación aún no conoce, de modo que
 * una opción recién dada de alta en UMS aparece con el nombre del catálogo en vez de en blanco.
 *
 * No se vuelve a filtrar por permiso: el árbol llega podado desde el servidor y filtrarlo otra vez
 * en el cliente solo abriría la puerta a que ambas reglas discrepen.
 */
import React, { useMemo } from 'react';
import { useI18n } from '@app/i18n/use-i18n';
import {
  useGraphNavigation,
  type NavigationItem,
  type NavigationModule,
} from '@app/authorization/hooks/use-graph-navigation';
import {
  Activity,
  Building2,
  Cpu,
  Flag,
  ScrollText,
  Settings,
  ShieldCheck,
  User,
  Users,
} from 'lucide-react';
import { NAV_MODULES, NAV_ROUTES, isExternalItem } from './navigation.config';
import type { NavItemId, NavMember } from './navigation.config';
import { iconoDeNodo } from './navigation.icons';

export interface ShellItem {
  key: string;
  label: string;
  icon: React.ReactNode;
  /** Ruta interna del router. Excluyente con {@link href}. */
  route?: string;
  /** Enlace externo (se abre en pestaña nueva). Excluyente con {@link route}. */
  href?: string;
  /** Pantalla conocida para el prefetch en hover; ausente si la ruta no es del portal. */
  prefetchId?: NavItemId;
}

export interface ShellModule {
  key: string;
  label: string;
  icon: React.ReactNode;
  items: ShellItem[];
}

/**
 * Pantallas que esta aplicación muestra SIEMPRE, estén o no en el grafo.
 *
 * Solo entra aquí lo que no gobierna un permiso porque no hay nada que gobernar: la ficha del
 * propio usuario. Cualquier otra pantalla se declara en el catálogo de UMS y llega por el grafo,
 * que es lo que G-181 vino a arreglar; esta lista no es una puerta trasera para saltarse eso.
 */
const PANTALLAS_PROPIAS = new Set<NavItemId>(['profile']);

const RUTA_A_PANTALLA: Record<string, NavItemId> = Object.fromEntries(
  Object.entries(NAV_ROUTES).map(([id, ruta]) => [ruta, id as NavItemId])
) as Record<string, NavItemId>;

/**
 * Convierte el árbol de un módulo en las filas de la barra.
 *
 * Se detiene en el primer nodo con ruta: lo que cuelga de una pantalla son **permisos sobre ella**
 * —ver, crear, exportar—, no destinos distintos, y pintarlos como enlaces llenaría el menú de rutas
 * que no existen. Un nodo sin ruta solo agrupa, así que se desciende a sus hijos y su icono se
 * hereda, para que una opción sin icono propio no rompa la columna.
 */
function filasDe(
  items: readonly NavigationItem[],
  iconoHeredado: string | null,
  rotulo: (pantalla: NavItemId | undefined, delGrafo: string) => string
): ShellItem[] {
  const salida: ShellItem[] = [];

  for (const item of items) {
    const icono = item.icon ?? iconoHeredado;

    if (item.route) {
      salida.push({
        key: item.code,
        label: rotulo(RUTA_A_PANTALLA[item.route], item.label),
        icon: iconoDeNodo(icono),
        route: item.route,
        prefetchId: RUTA_A_PANTALLA[item.route],
      });
      continue;
    }

    salida.push(...filasDe(item.children, icono, rotulo));
  }

  return salida;
}

function desdeGrafo(
  modulos: NavigationModule[],
  externos: Map<string, ShellItem[]>,
  rotuloDeFila: (pantalla: NavItemId | undefined, delGrafo: string) => string,
  rotuloDeModulo: (codigo: string, delGrafo: string) => string
): ShellModule[] {
  return modulos
    .map(modulo => ({
      key: modulo.code,
      label: rotuloDeModulo(modulo.code, modulo.label),
      // Desde el contrato 2.3.0 el módulo publica su icono (G-182). Sin él, respaldo neutro:
      // resolverlo por código sería reintroducir la tabla estática que este cambio eliminó.
      icon: iconoDeNodo(modulo.icon, 'w-5 h-5 text-m3-primary'),
      // Los enlaces externos (observabilidad) no son rutas del grafo —Grafana aplica su propia
      // autenticación—, así que se anexan al módulo al que pertenecen por convención de código.
      items: [...filasDe(modulo.items, null, rotuloDeFila), ...(externos.get(modulo.code) ?? [])],
    }))
    .filter(modulo => modulo.items.length > 0);
}

const filaEstatica = (miembro: NavMember, t: Record<string, string>): ShellItem => ({
  key: miembro.id,
  label: t[miembro.nameKey] ?? miembro.nameKey,
  icon: miembro.icon,
  ...(isExternalItem(miembro)
    ? { href: miembro.href }
    : { route: NAV_ROUTES[miembro.id], prefetchId: miembro.id }),
});

export function useShellNavigation(): ShellModule[] {
  const t = useI18n() as unknown as Record<string, string>;
  const grafo = useGraphNavigation();

  const estaticos = useMemo(
    () =>
      NAV_MODULES({
        ShieldCheck,
        Building2,
        Users,
        Cpu,
        Flag,
        User,
        Settings,
        Activity,
        ScrollText,
        primaryColorClass: 'text-m3-primary',
        indigoColorClass: 'text-indigo-400',
        t,
      }),
    [t]
  );

  return useMemo(() => {
    // Filas que NO vienen del grafo y se anexan al módulo que les toca:
    //
    // * Los enlaces externos de observabilidad (Grafana, Logs): no son rutas de este router y
    //   aplican su propia autenticación, así que UMS no tiene por qué conocerlos.
    // * Las PANTALLAS PROPIAS: la ficha del propio usuario. Pedirle permiso al catálogo para ver
    //   los datos de uno mismo es una autorización que no protege nada —el sujeto y el recurso son
    //   la misma persona—, y dejarla fuera del menú la volvía inalcanzable en cuanto la navegación
    //   pasó a construirse del grafo.
    const externos = new Map<string, ShellItem[]>();
    for (const modulo of estaticos) {
      const propias = modulo.members.filter(
        m => isExternalItem(m) || PANTALLAS_PROPIAS.has(m.id as NavItemId)
      );
      const enlaces = propias.map(m => filaEstatica(m, t));
      if (enlaces.length) externos.set(modulo.key.toUpperCase(), enlaces);
    }

    // ADR-UMS-104: el rótulo lo pone esta aplicación. Para una fila, por la pantalla a la que
    // lleva —que es lo que `NAV_ROUTES` ya sabe—; para un módulo, por su código. Cuando no hay
    // traducción, manda el `value` del grafo: un alta nueva en UMS se ve con el nombre del
    // catálogo, no con un hueco.
    const claveDeModulo = new Map(estaticos.map(m => [m.key.toUpperCase(), m.nameKey]));

    const rotuloDeFila = (pantalla: NavItemId | undefined, delGrafo: string): string => {
      if (!pantalla) return delGrafo;
      const miembro = estaticos.flatMap(m => m.members).find(x => !isExternalItem(x) && x.id === pantalla);
      const traducido = miembro ? t[miembro.nameKey] : undefined;
      return traducido ?? delGrafo;
    };

    const rotuloDeModulo = (codigo: string, delGrafo: string): string =>
      t[claveDeModulo.get(codigo.toUpperCase()) ?? ''] ?? delGrafo;

    const delGrafo = grafo.length ? desdeGrafo(grafo, externos, rotuloDeFila, rotuloDeModulo) : [];
    if (delGrafo.length) return delGrafo;

    // Sin una sola fila que pintar, el respaldo estático. La condición mira lo PINTABLE y no si el
    // grafo trajo módulos, porque las dos cosas se separan en la práctica: un grafo puede traer
    // módulos cuyos nodos no declaran `route` —así llega hoy la suite `SIL`— y entonces `desdeGrafo`
    // los descarta todos y devuelve una lista vacía. Con la condición anterior eso no era «no hay
    // navegación» sino «hay grafo», así que no había respaldo y el usuario se quedaba con un rail de
    // cero filas: dentro de la aplicación y sin ningún destino, que es un callejón sin salida y no
    // una restricción de permisos. El router sigue siendo la última barrera, igual que en el caso
    // sin grafo. Esto NO sustituye a que el portal pida su propio sistema (G-184): lo que evita es
    // que resolver el sistema equivocado deje la ventana en blanco.
    return estaticos.map(modulo => ({
      key: modulo.key,
      label: t[modulo.nameKey] ?? modulo.nameKey,
      icon: modulo.icon,
      items: modulo.members.map(miembro => filaEstatica(miembro, t)),
    }));
  }, [grafo, estaticos, t]);
}

/**
 * Resuelve qué fila marcar como activa a partir de la ruta actual.
 *
 * Gana el prefijo más largo: con `/system-suites/abc` y `/system-suites` declarados, la fila activa
 * debe ser la más específica, no la primera que case.
 */
export function claveActiva(modulos: ShellModule[], pathname: string): string | null {
  let clave: string | null = null;
  let largo = -1;

  for (const modulo of modulos) {
    for (const item of modulo.items) {
      if (!item.route) continue;
      const casa = pathname === item.route || pathname.startsWith(`${item.route}/`);
      if (casa && item.route.length > largo) {
        clave = item.key;
        largo = item.route.length;
      }
    }
  }

  return clave;
}
