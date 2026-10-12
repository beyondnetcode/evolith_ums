import { describe, it, expect } from 'vitest';
import {
  asHttpError,
  getHttpStatus,
  getSupportReferenceId,
  getHttpErrorMessage,
  getBlockedOperation,
} from './http-error';

describe('asHttpError', () => {
  it('returns empty object for non-object errors', () => {
    expect(asHttpError('string error')).toEqual({});
    expect(asHttpError(null)).toEqual({});
    expect(asHttpError(undefined)).toEqual({});
    expect(asHttpError(42)).toEqual({});
  });

  it('extracts response status', () => {
    const error = { response: { status: 404 } };
    const result = asHttpError(error);
    expect(result.response?.status).toBe(404);
  });

  it('extracts data fields', () => {
    const error = {
      response: {
        status: 422,
        data: {
          userMessage: 'Invalid input',
          errorId: 'err-123',
          traceId: 'trace-456',
          detail: 'Internal detail',
          title: 'Validation Error',
        },
      },
    };
    const result = asHttpError(error);
    expect(result.response?.data?.userMessage).toBe('Invalid input');
    expect(result.response?.data?.errorId).toBe('err-123');
    expect(result.response?.data?.traceId).toBe('trace-456');
  });

  it('extracts supportReferenceId from root', () => {
    const error = { supportReferenceId: 'ref-789' };
    const result = asHttpError(error);
    expect(result.supportReferenceId).toBe('ref-789');
  });

  it('handles malformed response gracefully', () => {
    const error = { response: 'not an object' };
    const result = asHttpError(error);
    expect(result.response).toBeUndefined();
  });
});

describe('getHttpStatus', () => {
  it('returns status number', () => {
    const error = { response: { status: 500 } };
    expect(getHttpStatus(error)).toBe(500);
  });

  it('returns undefined for non-http errors', () => {
    expect(getHttpStatus('error')).toBeUndefined();
  });

  it('returns undefined when no status', () => {
    const error = { response: {} };
    expect(getHttpStatus(error)).toBeUndefined();
  });
});

describe('getSupportReferenceId', () => {
  it('returns supportReferenceId from root', () => {
    const error = { supportReferenceId: 'root-ref' };
    expect(getSupportReferenceId(error)).toBe('root-ref');
  });

  it('returns supportReferenceId from response data', () => {
    const error = { response: { data: { supportReferenceId: 'data-ref' } } };
    expect(getSupportReferenceId(error)).toBe('data-ref');
  });

  it('returns errorId from response data', () => {
    const error = { response: { data: { errorId: 'err-123' } } };
    expect(getSupportReferenceId(error)).toBe('err-123');
  });

  it('returns traceId from response data', () => {
    const error = { response: { data: { traceId: 'trace-456' } } };
    expect(getSupportReferenceId(error)).toBe('trace-456');
  });

  it('returns undefined for empty error', () => {
    expect(getSupportReferenceId({})).toBeUndefined();
  });
});

describe('getHttpErrorMessage', () => {
  it('returns userMessage when available', () => {
    const error = { response: { data: { userMessage: 'Custom error' } } };
    expect(getHttpErrorMessage(error, 'Default error')).toBe('Custom error');
  });

  it('returns fallback when no userMessage', () => {
    const error = { response: { data: { detail: 'Internal' } } };
    expect(getHttpErrorMessage(error, 'Fallback')).toBe('Internal');
  });

  it('returns fallback for non-http errors', () => {
    expect(getHttpErrorMessage('error', 'Fallback')).toBe('Fallback');
  });

  it('does not return detail field', () => {
    const error = { response: { data: { detail: 'Internal detail' } } };
    expect(getHttpErrorMessage(error, 'Fallback')).toBe('Internal detail');
  });

  it('returns backend error field when available', () => {
    const error = { response: { data: { error: 'Profile not found.' } } };
    expect(getHttpErrorMessage(error, 'Fallback')).toBe('Profile not found.');
  });
});

// ─── 409 con desglose: qué bloquea exactamente la operación (ADR-0164) ───────

describe('getBlockedOperation', () => {
  const blocked409 = {
    response: {
      status: 409,
      data: {
        errorCode: 'tenant.branch_has_live_references',
        message: 'No se puede cerrar la sucursal porque todavía tiene usuarios o perfiles activos.',
        brokenRule: 'A branch cannot be closed while active references exist.',
        blockingDependencies: [
          { entityType: 'UserAccount', status: 'Active', count: 3 },
          { entityType: 'Profile', status: 'Active', count: 1 },
        ],
      },
    },
  };

  it('devuelve las dos clases que bloquean, no solo la primera', () => {
    const blocked = getBlockedOperation(blocked409);

    expect(blocked?.dependencies).toHaveLength(2);
    expect(blocked?.dependencies[0]).toEqual({
      entityType: 'UserAccount',
      status: 'Active',
      count: 3,
    });
    expect(blocked?.errorCode).toBe('tenant.branch_has_live_references');
  });

  it('ignora un 409 sin desglose', () => {
    expect(
      getBlockedOperation({ response: { status: 409, data: { detail: 'Conflict' } } })
    ).toBeNull();
  });

  it('ignora los errores que no son 409', () => {
    expect(
      getBlockedOperation({ response: { status: 500, data: { blockingDependencies: [] } } })
    ).toBeNull();
  });

  it('ignora lo que no es un error HTTP', () => {
    expect(getBlockedOperation('boom')).toBeNull();
  });

  it('descarta entradas del desglose que no tienen forma de dependencia', () => {
    const blocked = getBlockedOperation({
      response: {
        status: 409,
        data: {
          errorCode: 'x',
          blockingDependencies: [{ entityType: 'Profile', count: 2 }, 'basura', { count: 9 }],
        },
      },
    });

    expect(blocked?.dependencies).toEqual([{ entityType: 'Profile', status: '', count: 2 }]);
  });
});
