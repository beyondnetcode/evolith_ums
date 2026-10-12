import type { AuthGraphAccessor } from './auth-graph-accessor.js';
import { AuthorizationValidator } from './authorization-validator.js';

export type AuthorizationMode = 'enforce' | 'audit-only';

export interface AuthorizationLogger {
  warn(payload: Record<string, unknown>): void;
}

export interface AuthorizationConfig {
  readonly accessor: AuthGraphAccessor;
  readonly validator: AuthorizationValidator;
  readonly mode?: AuthorizationMode;
  readonly logger?: AuthorizationLogger;
}

interface ResolvedConfig {
  readonly accessor: AuthGraphAccessor;
  readonly validator: AuthorizationValidator;
  readonly mode: AuthorizationMode;
  readonly logger: AuthorizationLogger | null;
}

let current: ResolvedConfig | null = null;

/**
 * Globally configures the HOF and decorator surfaces. Call once during application startup.
 * For libraries that prefer dependency injection, you can instead pass `accessor` and
 * `validator` explicitly to {@link requireScope}, etc., via the `options` argument.
 */
export function configureAuthorization(config: AuthorizationConfig): void {
  current = {
    accessor: config.accessor,
    validator: config.validator,
    mode: config.mode ?? 'enforce',
    logger: config.logger ?? null
  };
}

/** Returns the current configuration, or throws if {@link configureAuthorization} was not called. */
export function getAuthorizationConfig(): ResolvedConfig {
  if (current === null) {
    throw new Error(
      'UMS SDK is not configured. Call configureAuthorization({ accessor, validator }) before using authorization primitives.'
    );
  }
  return current;
}

/** Resets the global configuration. Intended for tests. */
export function resetAuthorizationConfigForTesting(): void {
  current = null;
}
