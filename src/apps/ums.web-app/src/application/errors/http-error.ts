interface HttpErrorLike {
  supportReferenceId?: string;
  response?: {
    status?: number;
    headers?: Record<string, unknown>;
    data?: {
      // RFC 7807 Problem Details fields
      detail?: string; // RFC 7807 user-safe detail provided by the API
      title?: string;
      error?: string;
      // Approved, localized user-facing content
      userMessage?: string;
      // Correlation
      errorId?: string; // handle the user gives to support (look up in Loki)
      traceId?: string;
      supportReferenceId?: string;
    };
  };
}

const isRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === 'object' && value !== null;

export const asHttpError = (error: unknown): HttpErrorLike => {
  if (!isRecord(error)) return {};
  const response = isRecord(error.response) ? error.response : undefined;
  const data = response && isRecord(response.data) ? response.data : undefined;
  return {
    supportReferenceId:
      typeof error.supportReferenceId === 'string' ? error.supportReferenceId : undefined,
    response: response
      ? {
          status: typeof response.status === 'number' ? response.status : undefined,
          headers: isRecord(response.headers) ? response.headers : undefined,
          data: data
            ? {
                detail: typeof data.detail === 'string' ? data.detail : undefined,
                title: typeof data.title === 'string' ? data.title : undefined,
                error: typeof data.error === 'string' ? data.error : undefined,
                errorId: typeof data.errorId === 'string' ? data.errorId : undefined,
                traceId: typeof data.traceId === 'string' ? data.traceId : undefined,
                supportReferenceId:
                  typeof data.supportReferenceId === 'string' ? data.supportReferenceId : undefined,
                userMessage: typeof data.userMessage === 'string' ? data.userMessage : undefined,
              }
            : undefined,
        }
      : undefined,
  };
};

export const getHttpStatus = (error: unknown): number | undefined =>
  asHttpError(error).response?.status;

export const getSupportReferenceId = (error: unknown): string | undefined => {
  const httpError = asHttpError(error);
  const headerErrorId =
    httpError.response?.headers?.['x-error-id'] ?? httpError.response?.headers?.['X-Error-Id'];
  const headerTraceId =
    httpError.response?.headers?.['x-correlation-id'] ??
    httpError.response?.headers?.['X-Correlation-Id'];

  return (
    httpError.supportReferenceId ??
    httpError.response?.data?.supportReferenceId ??
    httpError.response?.data?.errorId ??
    (typeof headerErrorId === 'string' ? headerErrorId : undefined) ??
    httpError.response?.data?.traceId ??
    (typeof headerTraceId === 'string' ? headerTraceId : undefined)
  );
};

/**
 * Extracts the user-visible error message from an API error.
 *
 * Priority chain (OBS-01 contract):
 *  1. `userMessage` — localized and approved by the backend
 *  2. Problem Details `detail` — user-safe API message for RFC 7807 responses
 *  3. Caller-supplied fallback string
 *
 * Stack traces and internal identifiers never appear here; they stay in Grafana Loki,
 * keyed by the `errorId` returned by `getSupportReferenceId()`.
 */
export const getHttpErrorMessage = (error: unknown, fallback: string): string => {
  const data = asHttpError(error).response?.data;
  return data?.userMessage ?? data?.detail ?? data?.error ?? fallback;
};

// ─── Operaciones bloqueadas por dependencias vivas (409) ─────────────────────

/**
 * Una clase de dependencia que impide la operación: qué tipo de entidad, en qué estado y cuántas.
 * El backend devuelve TODAS las clases que bloquean, no solo la primera.
 */
export interface BlockingDependency {
  entityType: string;
  status: string;
  count: number;
}

export interface BlockedOperation {
  errorCode: string;
  message: string;
  dependencies: BlockingDependency[];
}

/**
 * Extrae el desglose de un 409 «operación bloqueada».
 *
 * Existe porque un «no se pudo» a secas obliga a adivinar: la respuesta trae cuántas cuentas y
 * cuántos perfiles siguen vivos, y esa es justo la información que necesita quien va a resolverlo.
 * Devuelve `null` si el error no trae ese cuerpo (otro 409, un 500, un fallo de red).
 */
export const getBlockedOperation = (error: unknown): BlockedOperation | null => {
  if (!isRecord(error)) return null;
  const response = isRecord(error.response) ? error.response : undefined;
  if (response?.status !== 409) return null;

  const data = isRecord(response.data) ? response.data : undefined;
  if (!data || !Array.isArray(data.blockingDependencies)) return null;

  const dependencies = data.blockingDependencies
    .filter(isRecord)
    .filter(
      (dep): dep is Record<string, unknown> & { entityType: string; count: number } =>
        typeof dep.entityType === 'string' && typeof dep.count === 'number'
    )
    .map(dep => ({
      entityType: dep.entityType,
      status: typeof dep.status === 'string' ? dep.status : '',
      count: dep.count,
    }));

  if (dependencies.length === 0) return null;

  return {
    errorCode: typeof data.errorCode === 'string' ? data.errorCode : '',
    message: typeof data.message === 'string' ? data.message : '',
    dependencies,
  };
};
