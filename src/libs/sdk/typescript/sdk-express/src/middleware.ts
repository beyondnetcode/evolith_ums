import type { AuthorizationGraph } from '@evolith-ums/ums-sdk-contracts';
import { isSchemaVersionSupported } from '@evolith-ums/ums-sdk-contracts';
import {
  type AuthGraphAccessor,
  type OpcionesDeVerificacion,
  type OpcionesDeJwks,
  AsyncLocalAuthGraphAccessor,
  MemoryAuthGraphAccessor,
  opcionesDeVerificacion,
  verificarFirma
} from '@evolith-ums/ums-sdk-authorization';

export interface UmsAuthGraphOptions {
  readonly accessor: AuthGraphAccessor;
  /**
   * Secreto con el que UMS firma sus portadores en HS256.
   *
   * Ya no es obligatorio: desde ADR-0157 UMS firma en RS256 y publica su clave pública, así que lo
   * normal es configurar `umsBaseUrl` y no tener secreto ninguno. Sigue existiendo para los
   * despliegues que todavía no han cruzado.
   *
   * **Hay que declarar al menos uno de los dos** (este o `umsBaseUrl`/`jwksUri`). Si no se declara
   * ninguno, el middleware falla al construirse: sin material de verificación no puede distinguir
   * un portador legítimo de uno fabricado, que es el agujero de G-217.
   */
  readonly signingSecret?: string;
  /**
   * Base de UMS, de la que sale su JWKS (`{base}/.well-known/jwks.json`). Habilita RS256.
   *
   * Declarar solo esto es lo deseable: el satélite verifica sin custodiar material de firma, y por
   * tanto no puede emitir portadores aunque quisiera.
   */
  readonly umsBaseUrl?: string;
  /** URI del JWKS, si no cuelga de `umsBaseUrl` en la ruta estándar. */
  readonly jwksUri?: string;
  /** Ajustes finos de la caché del JWKS (TTL, cadencia de refresco, tiempo de espera). */
  readonly jwks?: Partial<OpcionesDeJwks>;
  /** JWT body claim that carries the serialized graph. Default: 'graph'. */
  readonly jwtBodyClaim?: string;
  /** When true, reject with 401 + AUTH_201 if validUntil is in the past. */
  readonly rejectExpired?: boolean;
  /** When true (default), reject with 401 + AUTH_204/AUTH_205 on missing or incompatible schemaVersion. */
  readonly rejectIncompatible?: boolean;
}

/**
 * Express middleware. Reads the bearer JWT, decodes its body, extracts the `graph` claim,
 * validates schema compatibility (and optionally expiry), and binds the graph to the configured
 * accessor for the request lifetime. Downstream handlers protected by `requireScope`/decorators
 * see the graph through `accessor.current()`.
 */
export function umsAuthGraph(options: UmsAuthGraphOptions) {
  const accessor = options.accessor;
  if (!options.signingSecret && !options.umsBaseUrl && !options.jwksUri) {
    // Al construir, no al recibir la primera petición: un satélite mal configurado debe romperse
    // al arrancar, no servir peticiones sin verificar hasta que alguien lo note.
    throw new Error(
      'umsAuthGraph exige material con el que verificar la firma: `umsBaseUrl` (o `jwksUri`) para ' +
      'los portadores RS256 que emite UMS desde ADR-0157, o `signingSecret` para los HS256 que ' +
      'queden. Sin ninguno de los dos no puede distinguir un token legítimo de uno fabricado (G-217).'
    );
  }
  // Se construye UNA vez, no por petición: la caché del JWKS solo sirve de algo si sobrevive entre
  // peticiones. Crearla dentro del middleware haría un GET al emisor por cada token.
  const verificacion: OpcionesDeVerificacion = opcionesDeVerificacion(options);
  const claim = options.jwtBodyClaim ?? 'graph';
  const rejectExpired = options.rejectExpired === true;
  const rejectIncompatible = options.rejectIncompatible !== false;

  return function umsAuthGraphMiddleware(
    req: { headers: Record<string, string | string[] | undefined> },
    res: ExpressResponse,
    next: (err?: unknown) => void
  ): void {
    const token = extractBearer(req);
    if (token === null) return runWith(null, next);

    // Asíncrono porque RS256 puede tener que consultar el JWKS. El `void` es deliberado: Express no
    // espera la promesa, y el flujo continúa por `next()` o por `reject()` dentro de ella.
    void (async () => {
      // La firma PRIMERO. Sin esto, todo lo que viene después —el grafo, la versión de esquema, la
      // caducidad— se comprueba sobre un contenido que cualquiera pudo escribir.
      const resultado = await verificarFirma(token, verificacion);
      if (!resultado.valido) {
        return reject(res, 'AUTH_206', `El portador no supera la verificación de firma (${resultado.motivo}).`);
      }

      let graph: AuthorizationGraph | null = null;
      try {
        graph = extraerGrafo(resultado.cuerpo, claim);
      } catch {
        return runWith(null, next);
      }
      if (graph === null) return runWith(null, next);

      if (!graph.schemaVersion) {
        if (rejectIncompatible) return reject(res, 'AUTH_204', 'AuthorizationGraph payload does not declare a schemaVersion.');
        return runWith(null, next);
      }
      if (!isSchemaVersionSupported(graph.schemaVersion)) {
        if (rejectIncompatible)
          return reject(res, 'AUTH_205', `schemaVersion '${graph.schemaVersion}' is outside SDK compatibility range.`);
        return runWith(null, next);
      }
      if (rejectExpired && new Date(graph.validUntil).getTime() <= Date.now()) {
        return reject(res, 'AUTH_201', 'AuthorizationGraph has expired.');
      }

      return runWith(graph, next);
    })().catch((err: unknown) => next(err));
  };

  function runWith(graph: AuthorizationGraph | null, next: (err?: unknown) => void): void {
    if (accessor instanceof AsyncLocalAuthGraphAccessor) {
      accessor.run(graph, () => next());
      return;
    }
    if (accessor instanceof MemoryAuthGraphAccessor) {
      accessor.set(graph);
      next();
      return;
    }
    // Custom accessor — caller is responsible for plumbing.
    next();
  }
}

interface ExpressResponse {
  status: (code: number) => { json: (body: unknown) => void };
}

function extractBearer(req: { headers: Record<string, string | string[] | undefined> }): string | null {
  const raw = req.headers['authorization'];
  const header = Array.isArray(raw) ? raw[0] : raw;
  if (!header) return null;
  const m = /^Bearer\s+(.+)$/i.exec(header);
  return m?.[1]?.trim() ?? null;
}

/**
 * Extrae el grafo de un cuerpo YA VERIFICADO. Antes esta función partía el token y descodificaba
 * ella misma, y ahí estaba el defecto: quien decodifica sin verificar acaba confiando en lo que
 * lee. Ahora solo recibe lo que la verificación de firma dio por bueno.
 */
function extraerGrafo(payload: Record<string, unknown>, claim: string): AuthorizationGraph | null {
  const value = payload?.[claim];
  if (value && typeof value === 'object') return value as AuthorizationGraph;
  if (typeof value === 'string') return JSON.parse(value) as AuthorizationGraph;
  return null;
}


function reject(res: ExpressResponse, code: string, message: string): void {
  res.status(401).json({ code, message });
}
