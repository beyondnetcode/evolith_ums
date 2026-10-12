import { useMemo } from 'react';
import { useAuthStore } from '@app/stores/auth.store';
import type { GraphNavigationNode } from '@domain/authorization/schemas/authorization-graph.schema';

/**
 * La navegación que publica el sistema, lista para pintar.
 *
 * Desde el contrato v2.2.0 cada nodo trae `icon` y `route` —y desde v2.3.0 el módulo trae el
 * suyo—, que era lo que faltaba para que el
 * cliente construyera su menú sin conocer de antemano el sistema: con solo código y etiqueta sabía
 * qué texto poner, pero no qué icono ni a dónde llevar al usuario.
 *
 * Este hook NO decide el aspecto: aplana el árbol conservando la jerarquía y resuelve tres cosas
 * que, si cada pantalla las resolviera por su cuenta, acabarían divergiendo:
 *
 *  1. **Qué es navegable.** Un nodo con `route` lleva a algún sitio; uno sin ella solo agrupa.
 *  2. **Qué acciones concede.** Se exponen como conjunto para que un `disabled` sea una consulta
 *     y no un recorrido.
 *  3. **Qué está bloqueado explícitamente.** `Deny` no es lo mismo que ausencia, y la interfaz
 *     debería poder distinguir «no puedes» de «no existe».
 *
 * El árbol viaja ya podado: lo que llega es lo alcanzable. No hay que filtrar por permiso otra vez.
 */
export interface NavigationItem {
  code: string;
  label: string;
  kind: string;
  icon: string | null;
  route: string | null;
  /** Acciones concedidas sobre el nodo. */
  allowed: ReadonlySet<string>;
  /** Acciones denegadas explícitamente: bloquear, no ocultar. */
  denied: ReadonlySet<string>;
  children: NavigationItem[];
}

export interface NavigationModule {
  code: string;
  label: string;
  sortOrder: number;
  /** Icono del módulo publicado por el sistema; null si no se configuró. */
  icon: string | null;
  items: NavigationItem[];
}

const VACIO: NavigationModule[] = [];

function mapear(nodo: GraphNavigationNode): NavigationItem {
  const allowed = new Set<string>();
  const denied = new Set<string>();

  for (const accion of nodo.actions ?? []) {
    if (accion.effect === 'Allow') allowed.add(accion.actionCode);
    else if (accion.effect === 'Deny') denied.add(accion.actionCode);
  }

  return {
    code: nodo.code,
    label: nodo.value,
    kind: nodo.kind,
    icon: nodo.icon,
    route: nodo.route,
    allowed,
    denied,
    children: (nodo.children ?? []).map(mapear),
  };
}

export function useGraphNavigation(): NavigationModule[] {
  const menuAccess = useAuthStore(state => state.user?.authorizationGraph?.menuAccess);

  return useMemo(() => {
    if (!menuAccess?.length) return VACIO;

    return [...menuAccess]
      .sort((a, b) => a.sortOrder - b.sortOrder)
      .map(modulo => ({
        code: modulo.code,
        label: modulo.value,
        sortOrder: modulo.sortOrder,
        icon: modulo.icon ?? null,
        items: [...(modulo.nodes ?? [])].sort((a, b) => a.sortOrder - b.sortOrder).map(mapear),
      }));
  }, [menuAccess]);
}

/**
 * Aplana la navegación en las entradas que REALMENTE navegan: las que tienen ruta.
 *
 * Útil para resolver qué elemento marcar como activo a partir de la ruta actual, sin que cada
 * pantalla reimplemente el recorrido del árbol.
 */
export function itemsNavegables(modulos: NavigationModule[]): NavigationItem[] {
  const salida: NavigationItem[] = [];

  const visitar = (items: NavigationItem[]) => {
    for (const item of items) {
      if (item.route) salida.push(item);
      visitar(item.children);
    }
  };

  modulos.forEach(m => visitar(m.items));
  return salida;
}
