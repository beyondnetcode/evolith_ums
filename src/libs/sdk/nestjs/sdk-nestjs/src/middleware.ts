import { Inject, Injectable, type NestMiddleware } from '@nestjs/common';
import type { AuthorizationGraph } from '@evolith-ums/ums-sdk-contracts';
import {
  AsyncLocalAuthGraphAccessor,
  MemoryAuthGraphAccessor,
  opcionesDeVerificacion,
  verificarFirma,
  type AuthGraphAccessor,
  type OpcionesDeVerificacion
} from '@evolith-ums/ums-sdk-authorization';
import { UMS_AUTH_GRAPH_ACCESSOR, UMS_SDK_OPTIONS } from './tokens.js';
import type { UmsSdkModuleOptions } from './options.js';

type ExpressRequest = {
  readonly headers: Record<string, string | string[] | undefined>;
};

type ExpressResponse = unknown;
type NextFn = (err?: unknown) => void;

/**
 * Toma el grafo de autorización del portador de la petición y lo deja disponible durante toda su
 * ejecución, para que `UmsAuthGuard` lo lea con `AuthGraphAccessor.current()`.
 *
 * Antes de G-217 este middleware leía `req.umsAuthGraph` —un campo que ponía «alguien más arriba»—
 * y lo daba por bueno. Ese campo lo puede escribir cualquier middleware anterior, y nadie
 * comprobaba de dónde salía: bastaba con que un middleware mal hecho copiase ahí algo del cuerpo de
 * la petición para que el guardia autorizara con un grafo escrito por quien llamaba. Delegar la
 * verificación en un tercero que no existe no es delegar: es no verificar.
 *
 * Ahora el grafo solo puede venir de un sitio: un portador que UMS firmó.
 */
@Injectable()
export class AuthGraphMiddleware implements NestMiddleware<ExpressRequest, ExpressResponse> {
  /**
   * Se arma UNA vez, en el constructor, porque el middleware es un singleton del módulo. Si la
   * caché del JWKS se creara por petición no cachearía nada: habría un GET al emisor por token.
   */
  private readonly verificacion: OpcionesDeVerificacion;

  constructor(
    @Inject(UMS_AUTH_GRAPH_ACCESSOR) private readonly accessor: AuthGraphAccessor,
    @Inject(UMS_SDK_OPTIONS) private readonly options: UmsSdkModuleOptions
  ) {
    this.verificacion = opcionesDeVerificacion(options);
  }

  use(req: ExpressRequest, _res: ExpressResponse, next: NextFn): void {
    // Asíncrono porque RS256 puede tener que consultar el JWKS del emisor. El grafo se enlaza
    // dentro de la promesa, siempre antes de llamar a `next()`, para que el guardia lo vea.
    void this.extraerGrafoVerificado(req)
      .then((grafo) => this.runWith(grafo, next))
      .catch(() => this.runWith(null, next));
  }

  private async extraerGrafoVerificado(req: ExpressRequest): Promise<AuthorizationGraph | null> {
    const cabecera = req.headers?.['authorization'] ?? req.headers?.['Authorization'];
    const valor = Array.isArray(cabecera) ? cabecera[0] : cabecera;
    if (!valor?.startsWith('Bearer ')) return null;

    const verificacion = await verificarFirma(valor.slice(7).trim(), this.verificacion);
    if (!verificacion.valido) return null;

    const claim = verificacion.cuerpo[this.options.jwtBodyClaim ?? 'graph'];
    try {
      if (claim && typeof claim === 'object') return claim as AuthorizationGraph;
      if (typeof claim === 'string') return JSON.parse(claim) as AuthorizationGraph;
    } catch {
      return null;
    }
    return null;
  }

  private runWith(graph: AuthorizationGraph | null, next: NextFn): void {
    if (this.accessor instanceof AsyncLocalAuthGraphAccessor) {
      this.accessor.run(graph, () => next());
      return;
    }
    if (this.accessor instanceof MemoryAuthGraphAccessor) {
      this.accessor.set(graph);
      next();
      return;
    }
    // Accessor desconocido — se sigue, para que quien use uno propio monte su propio enlace.
    next();
  }
}
