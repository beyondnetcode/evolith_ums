/**
 * Minimal discriminated-union Result pattern. Compatible with common TypeScript Result libraries.
 * When `onDenied: 'returnFailure'`, the HOF/decorator returns this shape instead of throwing.
 */
export interface AuthorizationFailure {
  readonly code: string;
  readonly message: string;
  readonly primitive?: string;
  readonly target?: string;
}

export type Result<T = void> =
  | { readonly ok: true; readonly value: T }
  | { readonly ok: false; readonly error: AuthorizationFailure };

export const Results = {
  success<T = void>(value: T): Result<T> {
    return { ok: true, value };
  },
  successVoid(): Result<void> {
    return { ok: true, value: undefined };
  },
  failure<T = void>(code: string, message: string, primitive?: string, target?: string): Result<T> {
    return { ok: false, error: { code, message, primitive, target } };
  }
};
