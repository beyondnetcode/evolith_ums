import type { AuthorizationGraph } from '@evolith-ums/ums-sdk-contracts';
import type { AuthGraphAccessor } from '../auth-graph-accessor.js';

/**
 * Single in-memory holder. Suitable for browser SPAs operating on one user session at a time,
 * or for any single-threaded synchronous test harness.
 *
 * Not suitable for Node servers handling concurrent requests — use
 * {@link AsyncLocalAuthGraphAccessor} there.
 */
export class MemoryAuthGraphAccessor implements AuthGraphAccessor {
  private graph: AuthorizationGraph | null = null;

  current(): AuthorizationGraph | null {
    return this.graph;
  }

  set(graph: AuthorizationGraph | null): void {
    this.graph = graph;
  }

  clear(): void {
    this.graph = null;
  }
}
