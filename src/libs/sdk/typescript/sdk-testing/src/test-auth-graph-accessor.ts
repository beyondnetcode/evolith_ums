import type { AuthorizationGraph } from '@evolith-ums/ums-sdk-contracts';
import type { AuthGraphAccessor } from '@evolith-ums/ums-sdk-authorization';

/**
 * Trivial accessor backed by a single in-memory graph. Useful in unit tests that wire the
 * validator directly without any framework integration.
 */
export class TestAuthGraphAccessor implements AuthGraphAccessor {
  private graph: AuthorizationGraph | null;

  constructor(graph: AuthorizationGraph | null = null) {
    this.graph = graph;
  }

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
