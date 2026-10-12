import { useAuthStore } from '@app/stores/auth.store';
import { concedeOpcion } from '@domain/authorization/acceso-en-grafo';

/**
 * Evalúa un permiso contra el grafo del store.
 *
 * G-262: esto tenía COPIADA la resolución del hook, y las dos copias recorrían una forma del grafo
 * que el productor ya no emite. Ahora delega en el único sitio que sabe recorrerlo.
 */
const checkOptionAccess = (menuCode: string, optionCode: string): boolean => {
  const graph = useAuthStore.getState().user?.authorizationGraph;
  return graph ? concedeOpcion(graph, menuCode, optionCode) : false;
};

/**
 * Decorador de Método: Valida si el usuario tiene acceso a una opción específica.
 * Convención sobre configuración: si no se provee `optionCode`, intenta inferirlo
 * del nombre del método (ej. 'createUsuario' -> 'create').
 */
export function RequireOption(menuCode: string, optionCode?: string) {
  return function (target: any, propertyKey: string, descriptor: PropertyDescriptor) {
    const originalMethod = descriptor.value;
    const inferredOptionCode =
      optionCode ||
      propertyKey
        .replace(/([A-Z])/g, '-$1')
        .toLowerCase()
        .split('-')[0];

    descriptor.value = function (...args: any[]) {
      const hasAccess = checkOptionAccess(menuCode, inferredOptionCode);

      if (!hasAccess) {
        console.warn(
          `[Security] Acceso denegado a la opción '${inferredOptionCode}' en el menú '${menuCode}'. Ejecución abortada.`
        );
        // Dependiendo del caso, se podría lanzar una excepción o simplemente retornar null
        // throw new Error(`Access Denied to ${inferredOptionCode}`);
        return null; // Silent abort
      }

      return originalMethod.apply(this, args);
    };

    return descriptor;
  };
}
