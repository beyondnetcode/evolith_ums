import { AsyncLocalStorage } from 'node:async_hooks';
import type { AuthorizationGraph } from '@evolith-ums/ums-sdk-contracts';
import type { AuthGraphAccessor } from '../auth-graph-accessor.js';

/**
 * Node accessor backed by `node:async_hooks.AsyncLocalStorage`. Each call to `run(graph, fn)`
 * binds the graph to the current async execution context — any async code awaited within `fn`
 * sees the same graph via `current()`. Same primitive used by Express, Fastify and NestJS.
 *
 * Not available in browsers — use {@link MemoryAuthGraphAccessor} there.
 */
export class AsyncLocalAuthGraphAccessor implements AuthGraphAccessor {
  private readonly storage = new AsyncLocalStorage<AuthorizationGraph | null>();

  current(): AuthorizationGraph | null {
    return this.storage.getStore() ?? null;
  }

  /** Runs `fn` with `graph` bound to the current async scope. */
  run<R>(graph: AuthorizationGraph | null, fn: () => R): R {
    return this.storage.run(graph, fn);
  }

  /** Async variant: awaits `fn()` inside the bound scope. */
  async runAsync<R>(graph: AuthorizationGraph | null, fn: () => Promise<R>): Promise<R> {
    return this.storage.run(graph, fn);
  }
}
