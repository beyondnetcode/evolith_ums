import type {
  AuthGraphAccessor,
  AuthorizationValidator,
  AuthorizationLogger,
  OpcionesDeJwks
} from '@evolith-ums/ums-sdk-authorization';
import type { ModuleMetadata, Type } from '@nestjs/common';

export type UmsAuthorizationMode = 'enforce' | 'audit-only';

/**
 * Configuración de `UmsSdkModule.forRoot()`. Hay que declarar material de verificación —`umsBaseUrl`
 * o `signingSecret`—; el resto tiene un valor por defecto razonable: un AuthorizationValidator
 * nuevo, sin accessor (lo pones tú), modo `enforce` y sin registro de auditoría.
 */
export interface UmsSdkModuleOptions {
  /**
   * Secreto con el que UMS firma sus portadores en HS256.
   *
   * Ya no es obligatorio: desde ADR-0157 UMS firma en RS256 y publica su clave pública, así que lo
   * normal es declarar `umsBaseUrl` y no custodiar secreto ninguno. Sigue existiendo para los
   * despliegues que todavía no han cruzado.
   *
   * **Hay que declarar al menos uno**: este o `umsBaseUrl`/`jwksUri`. Sin ninguno, el módulo falla
   * al construirse, porque sin material de verificación se cree el grafo que le llegue (G-217).
   */
  readonly signingSecret?: string;

  /** URI del JWKS, si no cuelga de `umsBaseUrl` en la ruta estándar. */
  readonly jwksUri?: string;

  /** Ajustes finos de la caché del JWKS (TTL, cadencia de refresco, tiempo de espera). */
  readonly jwks?: Partial<OpcionesDeJwks>;

  /** Claim del cuerpo del portador que transporta el grafo. Por defecto `'graph'`. */
  readonly jwtBodyClaim?: string;

  /** Compatibility range advertised by the SDK to consumers. Defaults to `">=1.0.0 <2.0.0"`. */
  readonly schemaCompatibility?: string;

  /** Enforcement mode. Defaults to `'enforce'`. */
  readonly mode?: UmsAuthorizationMode;

  /** Authorization graph accessor. Defaults to AsyncLocalAuthGraphAccessor (Node). */
  readonly accessor?: AuthGraphAccessor;

  /** Validator instance. Defaults to a fresh AuthorizationValidator. */
  readonly validator?: AuthorizationValidator;

  /** Optional structured logger for denial events. */
  readonly logger?: AuthorizationLogger;

  /**
   * Base de UMS. De ella sale su JWKS (`{base}/.well-known/jwks.json`), y declararla habilita la
   * verificación RS256.
   *
   * Declarar solo esto es lo deseable: el satélite verifica sin custodiar material de firma, y por
   * tanto no puede emitir portadores aunque quisiera.
   */
  readonly umsBaseUrl?: string;
}

/** Async factory form, for ConfigService-driven setups. */
export interface UmsSdkModuleAsyncOptions extends Pick<ModuleMetadata, 'imports'> {
  readonly inject?: Array<Type<unknown> | string | symbol>;
  readonly useFactory: (...args: unknown[]) => UmsSdkModuleOptions | Promise<UmsSdkModuleOptions>;
}
