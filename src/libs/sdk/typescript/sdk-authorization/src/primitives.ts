import { Decisions, type AuthorizationDecision, isGranted } from './authorization-decision.js';
import { AuthorizationDeniedError } from './authorization-denied-error.js';
import { getAuthorizationConfig } from './configure.js';
import { Results, type Result } from './result.js';

export type DenialBehavior = 'throw' | 'returnFailure' | 'ignore';

export interface AuthorizationOptions {
  readonly onDenied?: DenialBehavior;
  readonly auditOnly?: boolean;
}

type Handler<TArgs extends unknown[], TResult> = (...args: TArgs) => TResult;

/**
 * Wraps `handler` with an authorization check evaluated against the configured accessor.
 * On denial:
 *   - `onDenied: 'throw'` (default) → throws {@link AuthorizationDeniedError}
 *   - `onDenied: 'returnFailure'`   → returns a `Result.failure(...)` (handler must return Result)
 *   - `onDenied: 'ignore'`          → handler runs anyway (per-call audit-only override)
 */
export function requireScope<TArgs extends unknown[], TResult>(
  scope: string,
  handler: Handler<TArgs, TResult>,
  options?: AuthorizationOptions
): Handler<TArgs, TResult> {
  return wrap(handler, options, () => {
    const config = getAuthorizationConfig();
    return config.validator.requireScope(config.accessor.current(), scope);
  });
}

export function requireMenuOption<TArgs extends unknown[], TResult>(
  optionCode: string,
  handler: Handler<TArgs, TResult>,
  options?: AuthorizationOptions
): Handler<TArgs, TResult> {
  return wrap(handler, options, () => {
    const config = getAuthorizationConfig();
    return config.validator.requireMenuOption(config.accessor.current(), optionCode);
  });
}

export function requireDomainAccess<TArgs extends unknown[], TResult>(
  resourceCode: string,
  actionCode: string,
  handler: Handler<TArgs, TResult>,
  options?: AuthorizationOptions
): Handler<TArgs, TResult> {
  return wrap(handler, options, () => {
    const config = getAuthorizationConfig();
    return config.validator.requireDomainAccess(config.accessor.current(), resourceCode, actionCode);
  });
}

export function requireFeatureFlag<TArgs extends unknown[], TResult>(
  flagCode: string,
  handler: Handler<TArgs, TResult>,
  options?: AuthorizationOptions
): Handler<TArgs, TResult> {
  return wrap(handler, options, () => {
    const config = getAuthorizationConfig();
    return config.validator.requireFeatureFlag(config.accessor.current(), flagCode);
  });
}

/**
 * Imperative API: evaluate a primitive without wrapping a handler. Useful for fine-grained
 * checks (e.g., conditionally rendering a UI element).
 */
export function evaluateScope(scope: string): AuthorizationDecision {
  const config = getAuthorizationConfig();
  return config.validator.requireScope(config.accessor.current(), scope);
}

export function evaluateMenuOption(optionCode: string): AuthorizationDecision {
  const config = getAuthorizationConfig();
  return config.validator.requireMenuOption(config.accessor.current(), optionCode);
}

export function evaluateDomainAccess(resourceCode: string, actionCode: string): AuthorizationDecision {
  const config = getAuthorizationConfig();
  return config.validator.requireDomainAccess(config.accessor.current(), resourceCode, actionCode);
}

export function evaluateFeatureFlag(flagCode: string): AuthorizationDecision {
  const config = getAuthorizationConfig();
  return config.validator.requireFeatureFlag(config.accessor.current(), flagCode);
}

function wrap<TArgs extends unknown[], TResult>(
  handler: Handler<TArgs, TResult>,
  options: AuthorizationOptions | undefined,
  evaluate: () => AuthorizationDecision
): Handler<TArgs, TResult> {
  const behavior: DenialBehavior = options?.onDenied ?? 'throw';
  const localAuditOnly = options?.auditOnly === true;

  return (...args: TArgs): TResult => {
    const decision = evaluate();
    if (isGranted(decision)) return handler(...args);

    const config = getAuthorizationConfig();
    const auditOnly = localAuditOnly || config.mode === 'audit-only';
    logDenial(decision, auditOnly);

    if (behavior === 'ignore' || auditOnly) {
      return handler(...args);
    }

    if (behavior === 'returnFailure') {
      return Results.failure<unknown>(
        decision.errorCode ?? 'AUTH_UNKNOWN',
        decision.reason ?? 'Denied.',
        decision.primitive,
        decision.target
      ) as unknown as TResult;
    }

    // Note: 'audit-only' precedence: when no explicit behavior was provided and we reach here,
    // throwing is the safe default — same semantics as .NET SDK.
    Decisions; // type-only side-effect import (kept to ensure tree-shake stability)
    throw new AuthorizationDeniedError(decision);
  };
}

function logDenial(decision: AuthorizationDecision, auditOnly: boolean): void {
  const config = getAuthorizationConfig();
  if (config.logger === null) return;
  config.logger.warn({
    event: auditOnly ? 'AuthorizationDeniedEvent (audit-only)' : 'AuthorizationDeniedEvent',
    primitive: decision.primitive,
    target: decision.target,
    code: decision.errorCode,
    reason: decision.reason
  });
}

/**
 * Type guard: narrows a Result returned by `onDenied: 'returnFailure'` wrapped handlers.
 */
export function isAuthorizationFailure<T>(result: unknown): result is Result<T> {
  return typeof result === 'object' && result !== null && 'ok' in result;
}
