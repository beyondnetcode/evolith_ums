import type { AuthorizationOptions } from './primitives.js';
import {
  requireScope,
  requireMenuOption,
  requireDomainAccess,
  requireFeatureFlag
} from './primitives.js';

type AnyFn = (...args: unknown[]) => unknown;

/**
 * TC39 Stage-3 method decorators. Equivalent to the HOFs but applied at class-definition time.
 * Requires TypeScript 5.0+ with native decorators (not `experimentalDecorators`).
 *
 * Example:
 * ```ts
 * class OrderService {
 *   @RequiresScope('PURCHASE_ORDER.APPROVE')
 *   async approveOrder(id: string): Promise<void> { ... }
 * }
 * ```
 */
export function RequiresScope(scope: string, options?: AuthorizationOptions) {
  return function (target: AnyFn, _ctx: ClassMethodDecoratorContext): AnyFn {
    return function (this: unknown, ...args: unknown[]): unknown {
      const bound = (...inner: unknown[]) => target.apply(this, inner);
      return requireScope(scope, bound, options)(...args);
    };
  };
}

export function RequiresMenuOption(optionCode: string, options?: AuthorizationOptions) {
  return function (target: AnyFn, _ctx: ClassMethodDecoratorContext): AnyFn {
    return function (this: unknown, ...args: unknown[]): unknown {
      const bound = (...inner: unknown[]) => target.apply(this, inner);
      return requireMenuOption(optionCode, bound, options)(...args);
    };
  };
}

export function RequiresDomainAccess(resourceCode: string, actionCode: string, options?: AuthorizationOptions) {
  return function (target: AnyFn, _ctx: ClassMethodDecoratorContext): AnyFn {
    return function (this: unknown, ...args: unknown[]): unknown {
      const bound = (...inner: unknown[]) => target.apply(this, inner);
      return requireDomainAccess(resourceCode, actionCode, bound, options)(...args);
    };
  };
}

export function RequiresFeatureFlag(flagCode: string, options?: AuthorizationOptions) {
  return function (target: AnyFn, _ctx: ClassMethodDecoratorContext): AnyFn {
    return function (this: unknown, ...args: unknown[]): unknown {
      const bound = (...inner: unknown[]) => target.apply(this, inner);
      return requireFeatureFlag(flagCode, bound, options)(...args);
    };
  };
}
