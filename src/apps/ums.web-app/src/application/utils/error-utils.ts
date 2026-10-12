export const HTTP_NON_RECOVERABLE_STATUSES = [400, 401, 403, 404, 422] as const;

export function getHttpStatus(error: unknown): number {
  if (typeof error === 'object' && error !== null) {
    if ('status' in error && typeof (error as { status?: unknown }).status === 'number') {
      return (error as { status: number }).status;
    }

    const response = (error as { response?: { status?: unknown } }).response;
    if (response && typeof response.status === 'number') {
      return response.status;
    }
  }
  return 0;
}

export function isHttpNonRecoverable(error: unknown): boolean {
  const status = getHttpStatus(error);
  return HTTP_NON_RECOVERABLE_STATUSES.includes(
    status as (typeof HTTP_NON_RECOVERABLE_STATUSES)[number]
  );
}

/**
 * A network error is one where no HTTP response was received (status 0):
 * the backend was unreachable, the connection failed, or the request was aborted.
 */
export function isNetworkError(error: unknown): boolean {
  return getHttpStatus(error) === 0;
}

export function isNonRecoverable(error: unknown): boolean {
  return isHttpNonRecoverable(error);
}

export interface RetryConfig {
  maxRetries?: number;
  networkErrorMaxRetries?: number;
}

export function getRetryOptions(config: RetryConfig = {}) {
  const { maxRetries = 1, networkErrorMaxRetries = 2 } = config;

  return {
    retry: (failureCount: number, error: unknown) => {
      if (isNonRecoverable(error)) return false;
      if (isNetworkError(error)) return failureCount < networkErrorMaxRetries;
      return failureCount < maxRetries;
    },
    retryDelay: (attempt: number) => Math.min(1000 * 2 ** attempt, 5000),
  };
}
