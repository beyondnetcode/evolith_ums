export type AuthorizationDecisionStatus =
  | 'Granted'
  | 'Denied'
  | 'NotGranted'
  | 'Expired'
  | 'GraphMissing'
  | 'SchemaUnsupported'
  | 'SchemaMissing'
  | 'TenantMismatch';

/**
 * Outcome of an authorization probe. Pure value type — no I/O, no exceptions raised by
 * construction. Throwing happens at the HOF / decorator / guard layer when configured.
 */
export interface AuthorizationDecision {
  readonly status: AuthorizationDecisionStatus;
  readonly primitive: string;
  readonly target: string;
  readonly errorCode?: string;
  readonly reason?: string;
  readonly graphRequestId?: string;
  readonly validUntil?: string;
  readonly occurredAt: string;
}

export function isGranted(decision: AuthorizationDecision): boolean {
  return decision.status === 'Granted';
}

export function isDenied(decision: AuthorizationDecision): boolean {
  return decision.status !== 'Granted';
}

function now(): string {
  return new Date().toISOString();
}

export const Decisions = {
  granted(primitive: string, target: string, validUntil?: string): AuthorizationDecision {
    return { status: 'Granted', primitive, target, validUntil, occurredAt: now() };
  },
  deny(primitive: string, target: string, errorCode: string, reason: string, validUntil?: string): AuthorizationDecision {
    return { status: 'Denied', primitive, target, errorCode, reason, validUntil, occurredAt: now() };
  },
  notGranted(primitive: string, target: string, errorCode: string, reason: string, validUntil?: string): AuthorizationDecision {
    return { status: 'NotGranted', primitive, target, errorCode, reason, validUntil, occurredAt: now() };
  },
  expired(primitive: string, target: string, validUntil: string): AuthorizationDecision {
    return {
      status: 'Expired',
      primitive,
      target,
      errorCode: 'AUTH_201',
      reason: 'AuthorizationGraph.validUntil is in the past.',
      validUntil,
      occurredAt: now()
    };
  },
  graphMissing(primitive: string, target: string): AuthorizationDecision {
    return {
      status: 'GraphMissing',
      primitive,
      target,
      errorCode: 'AUTH_202',
      reason: 'No AuthorizationGraph is bound to the current scope.',
      occurredAt: now()
    };
  },
  schemaUnsupported(primitive: string, target: string, version: string): AuthorizationDecision {
    return {
      status: 'SchemaUnsupported',
      primitive,
      target,
      errorCode: 'AUTH_205',
      reason: `Schema version '${version}' is outside SDK compatibility range.`,
      occurredAt: now()
    };
  },
  schemaMissing(primitive: string, target: string): AuthorizationDecision {
    return {
      status: 'SchemaMissing',
      primitive,
      target,
      errorCode: 'AUTH_204',
      reason: 'AuthorizationGraph payload does not declare a schemaVersion.',
      occurredAt: now()
    };
  },
  tenantMismatch(expected: string, actual: string): AuthorizationDecision {
    return {
      status: 'TenantMismatch',
      primitive: 'AssertTenant',
      target: expected,
      errorCode: 'AUTH_109',
      reason: `Tenant mismatch: expected '${expected}', got '${actual}'.`,
      occurredAt: now()
    };
  }
};
