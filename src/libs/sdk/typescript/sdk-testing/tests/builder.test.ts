import { describe, it, expect, beforeEach } from 'vitest';
import {
  AuthorizationValidator,
  configureAuthorization,
  resetAuthorizationConfigForTesting,
  requireScope,
  AuthorizationDeniedError,
  isGranted,
  MemoryAuthGraphAccessor
} from '@evolith-ums/ums-sdk-authorization';
import { SchemaVersion } from '@evolith-ums/ums-sdk-contracts';
import { AuthGraphBuilder, TestAuthGraphAccessor } from '../src/index.js';

describe('AuthGraphBuilder', () => {
  beforeEach(() => {
    resetAuthorizationConfigForTesting();
  });

  it('produces a valid graph with provided scopes', () => {
    const graph = AuthGraphBuilder.forTenant('LOGISTICS_CORE')
      .withUser('ana.flores@example.com')
      .withScope('PURCHASE_ORDER.VIEW')
      .withScope('PURCHASE_ORDER.APPROVE')
      .build();

    expect(graph.schemaVersion).toBe(SchemaVersion.Current);
    expect(graph.context.tenant.code).toBe('LOGISTICS_CORE');
    expect(graph.scopes).toContain('PURCHASE_ORDER.APPROVE');

    const validator = new AuthorizationValidator();
    expect(isGranted(validator.requireScope(graph, 'PURCHASE_ORDER.APPROVE'))).toBe(true);
  });

  it('withDeny adds an override that beats scope allowance', () => {
    const graph = AuthGraphBuilder.forTenant('LOGISTICS_CORE')
      .withScope('STOCK.DELETE')
      .withDeny('STOCK.DELETE')
      .build();

    const validator = new AuthorizationValidator();
    const decision = validator.requireScope(graph, 'STOCK.DELETE');
    expect(isGranted(decision)).toBe(false);
    expect(decision.errorCode).toBe('AUTH_102');
  });

  it('buildExpired produces a graph with past validUntil', () => {
    const graph = AuthGraphBuilder.forTenant('LOGISTICS_CORE').withScope('ANY.VIEW').buildExpired();
    expect(new Date(graph.validUntil).getTime()).toBeLessThan(Date.now());

    const validator = new AuthorizationValidator();
    expect(validator.requireScope(graph, 'ANY.VIEW').errorCode).toBe('AUTH_201');
  });

  it('withBranchScopedProfile populates branch and profile scope', () => {
    const graph = AuthGraphBuilder.forTenant('LOGISTICS_CORE').withBranchScopedProfile('CALLAO_DC').build();
    expect(graph.context.branch).not.toBeNull();
    expect(graph.context.branch?.code).toBe('CALLAO_DC');
    expect(graph.context.profile.scope).toBe('BranchScoped');
  });

  it('TestAuthGraphAccessor returns the configured graph', () => {
    const graph = AuthGraphBuilder.forTenant('LOGISTICS_CORE').build();
    const accessor = new TestAuthGraphAccessor(graph);
    expect(accessor.current()).toBe(graph);
  });

  it('requireScope HOF throws AuthorizationDeniedError without scope', () => {
    const graph = AuthGraphBuilder.forTenant('LOGISTICS_CORE').withScope('OTHER.VIEW').build();
    const accessor = new MemoryAuthGraphAccessor();
    accessor.set(graph);
    configureAuthorization({ accessor, validator: new AuthorizationValidator() });

    const protectedFn = requireScope('PURCHASE_ORDER.APPROVE', (id: string) => ({ id, ok: true }));
    expect(() => protectedFn('order-1')).toThrow(AuthorizationDeniedError);
  });

  it('requireScope HOF returns Result.failure when onDenied=returnFailure', () => {
    const graph = AuthGraphBuilder.forTenant('LOGISTICS_CORE').build();
    const accessor = new MemoryAuthGraphAccessor();
    accessor.set(graph);
    configureAuthorization({ accessor, validator: new AuthorizationValidator() });

    const protectedFn = requireScope<[string], { ok: boolean }>(
      'PURCHASE_ORDER.APPROVE',
      (_id) => ({ ok: true }),
      { onDenied: 'returnFailure' }
    );
    const result = protectedFn('order-1') as unknown as { ok: false; error: { code: string } };
    expect(result.ok).toBe(false);
    expect(result.error.code).toBe('AUTH_101');
  });

  it('audit-only mode lets execution through but logs', () => {
    const graph = AuthGraphBuilder.forTenant('LOGISTICS_CORE').build();
    const accessor = new MemoryAuthGraphAccessor();
    accessor.set(graph);
    const captured: unknown[] = [];
    configureAuthorization({
      accessor,
      validator: new AuthorizationValidator(),
      mode: 'audit-only',
      logger: { warn: (p) => captured.push(p) }
    });

    const protectedFn = requireScope('PURCHASE_ORDER.APPROVE', () => 'ok');
    expect(protectedFn()).toBe('ok');
    expect(captured.length).toBe(1);
  });
});
