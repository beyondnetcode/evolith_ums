import type { AuthorizationGraph } from '@evolith-ums/ums-sdk-contracts';

/**
 * Port for retrieving the AuthorizationGraph applicable to the current execution scope.
 * Implementations:
 *   - `AsyncLocalAuthGraphAccessor` for Node 20+ request lifecycles
 *   - `MemoryAuthGraphAccessor` for browser/SPA single-session scenarios
 *   - any custom adapter integrated with a host framework's request scope
 */
export interface AuthGraphAccessor {
  /**
   * The graph for the current scope, or null when no authenticated session is bound.
   * Callers must not cache the return value across scope boundaries.
   */
  current(): AuthorizationGraph | null;
}
