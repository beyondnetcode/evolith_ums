/**
 * Registro de las secciones que llegaron AUSENTES en un grafo de autorización.
 *
 * Existe porque la guarda de nulos del índice, sola, era un arreglo peor que el fallo que cerraba.
 * Colapsar una sección ausente a `[]` evita el `TypeError`, sí, pero a cambio vuelve
 * indistinguibles «no me la mandaron» y «está vacía» justo en la estructura que decide permisos:
 * cierra, y cierra en silencio. Un satélite que pidiera `?include=navigation` por error y luego
 * preguntara por un permiso de dominio recibiría un «no concedido» perfectamente plausible y
 * ninguna pista de que la pregunta no se llegó a hacer.
 *
 * Paridad deliberada con `UmsGraphDiagnostics` del SDK .NET: mismos nombres de sección y mismas
 * tres salidas, para que un satélite Node y uno .NET no diagnostiquen distinto el mismo grafo.
 */

/** Nombres de sección tal como viajan en el JSON. Constantes, para que el registro sea correlacionable. */
export const GraphSections = {
  menuAccess: 'menuAccess',
  domainPermissions: 'domainPermissions',
  scopes: 'scopes',
  featureFlags: 'featureFlags',
  /**
   * Sublistas anidadas. La proyección NO las puede omitir —viajan dentro de su sección— así que
   * verlas ausentes significa otra cosa: un productor que no es este UMS, o un grafo hecho a mano.
   */
  menuAccessNodes: 'menuAccess[].nodes',
  menuNodeActions: 'menuAccess[].nodes[].actions',
  menuNodeChildren: 'menuAccess[].nodes[].children',
  domainResourceActions: 'domainPermissions[].actions'
} as const;

export type GraphSectionName = (typeof GraphSections)[keyof typeof GraphSections];

/**
 * Una sección del grafo llegó AUSENTE donde el consumidor la necesitaba.
 *
 * Ausente no es vacía, y esa es toda la razón de que este tipo exista. `domainPermissions: []`
 * significa «no hay ninguna denegación explícita»; la clave ausente significa «no sé si las hay».
 * El evaluador de deny-wins concluye lo mismo ante las dos —CONCEDE— y solo una de las dos
 * conclusiones está justificada.
 *
 * Solo se lanza si el consumidor lo pide con `setStrictGraphSections(true)`.
 */
export class GraphSectionMissingError extends Error {
  readonly sectionName: string;

  constructor(sectionName: string) {
    super(
      `La sección '${sectionName}' del grafo de autorización llegó AUSENTE, no vacía. Una sección ` +
        'ausente y una vacía llevan al evaluador a la misma conclusión —no hay denegaciones— y solo ' +
        'una de las dos es cierta. Si el grafo se pidió con `?include=`, comprueba que ' +
        `'${sectionName}' estuviera en la proyección que se solicitó.`
    );
    this.name = 'GraphSectionMissingError';
    this.sectionName = sectionName;
  }
}

export type GraphSectionAbsentListener = (sectionName: string) => void;

const oyentes = new Set<GraphSectionAbsentListener>();
const yaAvisadas = new Set<string>();
let estricto = false;

/**
 * Registra un oyente para las ausencias. Devuelve la función que lo da de baja.
 *
 * Es la salida que un anfitrión usa para llevar el aviso a SU registro (pino, winston, lo que sea),
 * que es donde alguien lo va a leer de verdad.
 */
export function onGraphSectionAbsent(listener: GraphSectionAbsentListener): () => void {
  oyentes.add(listener);
  return () => {
    oyentes.delete(listener);
  };
}

/**
 * Convierte la ausencia en `GraphSectionMissingError` en vez de tolerarla.
 *
 * Apagado por defecto, y a propósito: el orden de despliegue no se puede garantizar entre
 * organizaciones, y un SDK que reviente ante un grafo recortado convierte un `curl` con `?include=`
 * contra un entorno compartido en una caída ajena. Esto es para el satélite que NECESITA una
 * sección y prefiere caerse a autorizar a ciegas.
 */
export function setStrictGraphSections(activo: boolean): void {
  estricto = activo;
}

/** Solo para pruebas: olvida qué secciones ya se avisaron. */
export function resetGraphSectionDiagnostics(): void {
  yaAvisadas.clear();
  oyentes.clear();
  estricto = false;
}

/**
 * Reporta una sección ausente. La llama el índice, una sola vez por sección y grafo — el índice se
 * memoiza por grafo, así que esto no puede inundar nada.
 *
 * Si nadie ha registrado un oyente cae a `console.warn`, UNA vez por nombre de sección y proceso.
 * Es la mitad que hace que la tolerancia no sea muda: un aviso que solo existe cuando alguien lo
 * cableó de antemano seguiría siendo silencio para quien no sabe que hay algo que cablear.
 */
export function reportGraphSectionAbsent(sectionName: string): void {
  for (const oyente of oyentes) oyente(sectionName);

  if (oyentes.size === 0 && !yaAvisadas.has(sectionName)) {
    yaAvisadas.add(sectionName);
    console.warn(
      `[ums-sdk] El grafo de autorización llegó SIN la sección '${sectionName}'. Ausente no es ` +
        'vacía: se trata como vacía para no tumbar el proceso, así que toda comprobación que ' +
        'dependa de esa sección va a resolver NO CONCEDIDO. Si se pidió con `?include=`, revisa ' +
        `que '${sectionName}' estuviera en la proyección.`
    );
  }

  if (estricto) throw new GraphSectionMissingError(sectionName);
}
