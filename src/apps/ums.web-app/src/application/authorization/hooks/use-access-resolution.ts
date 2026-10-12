import { useAuthStore } from '@app/stores/auth.store';
import { AuthorizationGraph } from '@domain/authorization/schemas/authorization-graph.schema';
import { concedeMenu, concedeOpcion } from '@domain/authorization/acceso-en-grafo';

/**
 * Resolución de acceso para la interfaz.
 *
 * G-262: esto recorría `mod.menus`, `menu.subMenus` y `sub.options` e importaba tres tipos que el
 * esquema ya no exporta. `hasMenuAccess` y `hasOptionAccess` devolvían `false` —o reventaban sobre
 * `undefined`— para todo el que no fuera administrador interno, y las pantallas protegidas caían
 * al respaldo de «no autorizado». Como el administrador sale por la rama de arriba y nunca llegaba
 * a la rota, el defecto era invisible para quien lo probaba.
 *
 * La resolución vive ahora en `@domain/authorization/acceso-en-grafo`, en un solo sitio.
 */
export const useAccessResolution = () => {
  const user = useAuthStore(state => state.user);
  const graph: AuthorizationGraph | null = user?.authorizationGraph ?? null;
  const isInternalAdmin = user?.isInternalAdmin ?? false;

  const hasModuleAccess = (moduleCode: string): boolean => {
    if (isInternalAdmin) return true;
    if (!graph) return false;
    const mod = graph.menuAccess.find(m => m.code === moduleCode);
    return !!mod && mod.status === 'Active';
  };

  const hasMenuAccess = (moduleCode: string, menuCode: string): boolean => {
    if (isInternalAdmin) return true;
    if (!graph) return false;
    return concedeMenu(graph, moduleCode, menuCode);
  };

  const hasOptionAccess = (menuCode: string, optionCode: string): boolean => {
    if (isInternalAdmin) return true;
    if (!graph) return false;
    return concedeOpcion(graph, menuCode, optionCode);
  };

  const hasFeatureFlag = (flagCode: string): boolean => {
    if (isInternalAdmin) return true;
    if (!graph) return false;
    return graph.featureFlags.find(f => f.flagCode === flagCode)?.isEnabled ?? false;
  };

  return { graph, hasModuleAccess, hasMenuAccess, hasOptionAccess, hasFeatureFlag };
};
