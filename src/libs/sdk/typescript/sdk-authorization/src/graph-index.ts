import type {
  AuthorizationGraph,
  DomainActionResolution,
  FeatureFlagState,
  NavigationNode,
  NodeAction,
} from '@evolith-ums/ums-sdk-contracts';
import { GraphSections, reportGraphSectionAbsent } from './graph-diagnostics.js';

/**
 * Índice de consulta O(1) sobre un `AuthorizationGraph`.
 *
 * El grafo es inmutable durante toda la sesión, pero el validador lo recorría entero en CADA
 * comprobación: cuatro bucles anidados para resolver una opción de menú. Con un catálogo de 500
 * opciones son cientos de comparaciones por llamada, y una vista normal hace varias comprobaciones
 * por render. Es coste por petición, no por sesión (R-7 del informe de arquitectura).
 *
 * Se memoiza por instancia de grafo en un `WeakMap`: no cambia la API pública, no hay que invalidar
 * nada a mano, y el índice se recolecta con su grafo.
 *
 * Paridad deliberada con el `GraphIndex` del SDK .NET: mismas estructuras y misma semántica, para
 * que un satélite Node y uno .NET no resuelvan permisos de forma distinta.
 */
export class GraphIndex {
  private static readonly cache = new WeakMap<AuthorizationGraph, GraphIndex>();

  /**
   * Primera fila por código de opción, en orden de recorrido. Se conserva a propósito la semántica
   * de «primera coincidencia» del validador original: una opción con VIEW=Allow y DELETE=Deny
   * resuelve por la primera que aparece. Cambiarlo a deny-wins sería alterar una decisión de
   * seguridad dentro de un cambio de rendimiento. Para preguntar por una acción concreta está
   * `optionAction`.
   */
  private readonly optionByCode = new Map<string, NodeAction>();
  private readonly optionByCodeAction = new Map<string, NodeAction>();
  private readonly domainByResourceAction = new Map<string, DomainActionResolution>();
  private readonly flagByCode = new Map<string, FeatureFlagState>();
  private readonly scopes = new Set<string>();

  /**
   * Secciones que llegaron AUSENTES en este grafo, por nombre (ver `GraphSections`).
   *
   * Existe para que ausente y vacía dejen de ser el mismo estado observable. Sin esta lista, quien
   * preguntara por un permiso de dominio sobre un grafo que llegó sin `domainPermissions`
   * recibiría un «no concedido» idéntico al de un grafo que sí traía la sección y de verdad no
   * concedía — y no habría forma de distinguirlos ni después.
   */
  readonly missingSections: ReadonlyArray<string>;

  private readonly ausentes: string[] = [];

  private *flatten(nodes: ReadonlyArray<NavigationNode>): Generator<NavigationNode> {
    for (const node of nodes) {
      yield node;
      yield* this.flatten(this.oEmpty(node.children, GraphSections.menuNodeChildren));
    }
  }

  /**
   * Cae a lista vacía ante una sección AUSENTE, y lo REGISTRA con el nombre de la sección.
   *
   * Desde 2.5.0 el grafo puede llegar PROYECTADO: los bloques no pedidos se omiten del JSON.
   * `AuthorizationGraph` es una interfaz —cero validación en ejecución—, así que una clave ausente
   * llega como `undefined` y el primer `.flatMap` o `.find` lanzaba `TypeError` en la PRIMERA
   * comprobación de permiso, no al recibir el grafo.
   *
   * Pero caer a `[]` y callar era cambiar un fallo ruidoso por uno mudo, y encima en el índice que
   * decide permisos: `undefined` y `[]` pasaban a ser el mismo estado, y «no me la mandaron»
   * dejaba de poder distinguirse de «está vacía». La tolerancia se conserva —fail-closed, sin
   * tumbar al satélite— pero deja rastro: el nombre va a `missingSections` y a
   * `reportGraphSectionAbsent`. Quien prefiera caerse antes que autorizar a ciegas enciende
   * `setStrictGraphSections(true)`.
   *
   * Una lista VACÍA no se reporta: vacía es un dato legítimo y frecuente —un grafo lobby las trae
   * todas así—. Lo que se reporta es la AUSENCIA, que es lo que no se podía ver.
   */
  private oEmpty<T>(
    list: ReadonlyArray<T> | null | undefined,
    section: string
  ): ReadonlyArray<T> {
    if (list !== null && list !== undefined) return list;

    this.ausentes.push(section);
    reportGraphSectionAbsent(section);
    return [];
  }

  private constructor(graph: AuthorizationGraph) {
    // El árbol es recursivo desde v2.0.0: se aplana una vez y se indexa por (código, acción).
    const modules = this.oEmpty(graph.menuAccess, GraphSections.menuAccess);
    const nodes = modules.flatMap(m => [...this.oEmpty(m.nodes, GraphSections.menuAccessNodes)]);

    for (const node of this.flatten(nodes)) {
      for (const action of this.oEmpty(node.actions, GraphSections.menuNodeActions)) {
        if (!this.optionByCode.has(node.code)) this.optionByCode.set(node.code, action);
        const key = GraphIndex.key(node.code, action.actionCode);
        if (!this.optionByCodeAction.has(key)) this.optionByCodeAction.set(key, action);
      }
    }

    for (const resource of this.oEmpty(graph.domainPermissions, GraphSections.domainPermissions)) {
      for (const action of this.oEmpty(resource.actions, GraphSections.domainResourceActions)) {
        const key = GraphIndex.key(resource.resourceCode, action.actionCode);
        if (!this.domainByResourceAction.has(key)) this.domainByResourceAction.set(key, action);
      }
    }

    // Los scopes viajan en minúsculas; se comparan sin distinguir mayúsculas (G-167).
    for (const scope of this.oEmpty(graph.scopes, GraphSections.scopes)) {
      this.scopes.add(scope.toLowerCase());
    }
    for (const flag of this.oEmpty(graph.featureFlags, GraphSections.featureFlags)) {
      if (!this.flagByCode.has(flag.flagCode)) this.flagByCode.set(flag.flagCode, flag);
    }

    this.missingSections = this.ausentes;
  }

  static for(graph: AuthorizationGraph): GraphIndex {
    let index = GraphIndex.cache.get(graph);
    if (index === undefined) {
      index = new GraphIndex(graph);
      GraphIndex.cache.set(graph, index);
    }
    return index;
  }

  /**
   * `\u0000` (NUL) como separador: no puede aparecer en un código, así que no hay colisiones.
   *
   * Va ESCAPADO, nunca como byte crudo. Un NUL literal en el fuente hace que git clasifique el
   * fichero como BINARIO y deje de mostrar su diff, y este fichero es el del cambio de seguridad
   * más sensible del lote: ilegible en revisión. El valor en ejecución es exactamente el mismo.
   */
  private static key(a: string, b: string): string {
    return `${a}\u0000${b}`;
  }

  option(code: string): NodeAction | undefined {
    return this.optionByCode.get(code);
  }

  optionAction(code: string, actionCode: string): NodeAction | undefined {
    return this.optionByCodeAction.get(GraphIndex.key(code, actionCode));
  }

  domain(resourceCode: string, actionCode: string): DomainActionResolution | undefined {
    return this.domainByResourceAction.get(GraphIndex.key(resourceCode, actionCode));
  }

  flag(flagCode: string): FeatureFlagState | undefined {
    return this.flagByCode.get(flagCode);
  }

  hasScope(scope: string): boolean {
    return this.scopes.has(scope.toLowerCase());
  }

  /** Denegación explícita en cualquiera de las dos superficies del grafo. */
  isDenied(code: string, actionCode: string): boolean {
    return (
      this.optionAction(code, actionCode)?.effect === 'Deny' ||
      this.domain(code, actionCode)?.effect === 'Deny'
    );
  }
}
