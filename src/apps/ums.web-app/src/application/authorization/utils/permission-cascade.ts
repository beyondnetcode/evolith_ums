import type { SystemSuite } from '@domain/authorization/models/system-suite.model';
import type { SystemSuiteNode } from '@domain/authorization/schemas/system-suite.schema';

/**
 * Utilidades de cascada de permisos sobre el árbol de nodos recursivo del
 * SystemSuite (ADR-0090). Reemplazan el recorrido rígido Menú→Submenú→Opción
 * por un recorrido recursivo de `module.nodes`.
 */

type AscendantType = 'Module' | 'Submodule' | 'Page' | 'Option';

// Mapea el rol del nodo (NodeKind) al tipo de destino usado por la cascada.
// Preserva la semántica previa: Menú→Submodule, Submenú→Page, Opción→Option.
function nodeKindToAscendantType(kind: string): AscendantType {
  if (kind === 'Menu') return 'Submodule';
  if (kind === 'SubMenu') return 'Page';
  return 'Option';
}

// Busca el camino (nodos ancestros, de raíz a target sin incluirlo) dentro de
// un subárbol. Devuelve null si el target no está en el subárbol.
function findNodePath(nodes: SystemSuiteNode[], targetId: string): SystemSuiteNode[] | null {
  for (const node of nodes) {
    if (node.id === targetId) return [];
    const childPath = findNodePath(node.children ?? [], targetId);
    if (childPath) return [node, ...childPath];
  }
  return null;
}

/**
 * Devuelve los IDs de los ancestros (Módulo y nodos) desde la raíz hasta (sin
 * incluir) el targetId. Vacío si no se encuentra.
 */
export function getAscendantIds(suite: SystemSuite, targetId: string): string[] {
  for (const module of suite.modules) {
    if (module.id === targetId) return [];
    const path = findNodePath(module.nodes ?? [], targetId);
    if (path) return [module.id, ...path.map(n => n.id)];
  }
  return [];
}

/**
 * Igual que {@link getAscendantIds} pero devuelve el tipo de cada ancestro.
 */
export function getAscendantsWithTypes(
  suite: SystemSuite,
  targetId: string
): { id: string; type: AscendantType }[] {
  for (const module of suite.modules) {
    if (module.id === targetId) return [];
    const path = findNodePath(module.nodes ?? [], targetId);
    if (path) {
      return [
        { id: module.id, type: 'Module' as const },
        ...path.map(n => ({ id: n.id, type: nodeKindToAscendantType(n.kind) })),
      ];
    }
  }
  return [];
}

/**
 * Devuelve true si el código de acción implica una operación de lectura/consulta.
 */
export function isReadAction(actionCode: string): boolean {
  const upper = actionCode.toUpperCase();
  return (
    upper === 'VIEW' ||
    upper === 'LIST' ||
    upper === 'READ' ||
    upper.endsWith('_VIEW') ||
    upper.endsWith('_LIST')
  );
}

// ¿Alguna de las funcionalidades vinculadas al nodo es de lectura?
function nodeIsReadOnly(node: SystemSuiteNode): boolean {
  return node.actionCodes.length > 0 && node.actionCodes.every(isReadAction);
}

function nodeHasReadAction(node: SystemSuiteNode): boolean {
  return node.actionCodes.some(isReadAction);
}

// Encuentra el nodo objetivo y su lista de hermanos (children de su padre).
function findNodeWithSiblings(
  nodes: SystemSuiteNode[],
  targetId: string,
  siblings: SystemSuiteNode[]
): { node: SystemSuiteNode; siblings: SystemSuiteNode[] } | null {
  for (const node of nodes) {
    if (node.id === targetId) return { node, siblings };
    const found = findNodeWithSiblings(node.children ?? [], targetId, node.children ?? []);
    if (found) return found;
  }
  return null;
}

/**
 * Dado un nodo hoja (Opción) de "escritura/gestión", devuelve los IDs de los
 * nodos hermanos de "lectura/consulta" en el mismo padre, para propagar que el
 * acceso de escritura implica el de lectura.
 */
export function getSiblingViewOptions(
  suite: SystemSuite,
  optionId: string
): { id: string; type: 'Option' }[] {
  for (const module of suite.modules) {
    const found = findNodeWithSiblings(module.nodes ?? [], optionId, module.nodes ?? []);
    if (found) {
      // Si el propio nodo ya es de lectura, no hay lectura implícita que resolver.
      if (nodeIsReadOnly(found.node)) return [];
      return found.siblings
        .filter(s => s.id !== optionId && s.kind === 'Option' && nodeHasReadAction(s))
        .map(s => ({ id: s.id, type: 'Option' as const }));
    }
  }
  return [];
}
