import type { AuthorizationDecision } from './authorization-decision.js';

/**
 * Thrown by the HOF, decorator, or guard when an authorization decision denies access and the
 * configured behavior is to throw. Carries the full {@link AuthorizationDecision} for diagnostics.
 */
export class AuthorizationDeniedError extends Error {
  readonly decision: AuthorizationDecision;

  constructor(decision: AuthorizationDecision) {
    super(decision.reason ?? 'Authorization denied.');
    this.name = 'AuthorizationDeniedError';
    this.decision = decision;
    Object.setPrototypeOf(this, AuthorizationDeniedError.prototype);
  }
}
