import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { ApiErrorBanner } from './ApiErrorBanner';
import * as useI18nModule from '@app/i18n/use-i18n';
import * as httpErrorModule from '@app/errors/http-error';

vi.mock('@app/i18n/use-i18n');
vi.mock('@app/errors/http-error');

/** Builds an Error carrying an HTTP status the way axios exposes it (error.response.status). */
function httpError(message: string, status: number): Error {
  return Object.assign(new Error(message), { response: { status } });
}

describe('ApiErrorBanner', () => {
  beforeEach(() => {
    vi.restoreAllMocks();

    vi.mocked(useI18nModule.useI18n).mockReturnValue({
      errorBackendUnavailableTitle: 'Backend Unavailable',
      errorBackendUnavailableHint: 'Start the backend',
      errorInvalidRequestTitle: 'Invalid Request',
      errorInvalidRequestHint: 'Check parameters',
      errorGenericTitle: 'Error',
      errorGenericHint: 'Ensure backend is running',
      errorSupportReference: (ref: string) => `Ref: ${ref}`,
    } as any);

    vi.mocked(httpErrorModule.getSupportReferenceId).mockReturnValue(null);
  });

  it('renders generic error message', () => {
    const error = new Error('Something went wrong');
    render(<ApiErrorBanner error={error} />);
    expect(screen.getByText('Error')).toBeInTheDocument();
  });

  it('renders hint message', () => {
    const error = new Error('Something went wrong');
    render(<ApiErrorBanner error={error} />);
    expect(screen.getByText('Ensure backend is running')).toBeInTheDocument();
  });

  it('renders backend unavailable message for a 5xx error', () => {
    render(<ApiErrorBanner error={httpError('Backend unavailable', 503)} />);
    expect(screen.getByText('Backend Unavailable')).toBeInTheDocument();
  });

  it('renders validation error message for a 400 error', () => {
    render(<ApiErrorBanner error={httpError('Validation failed', 400)} />);
    expect(screen.getByText('Invalid Request')).toBeInTheDocument();
  });

  it('renders support reference when available', () => {
    vi.mocked(httpErrorModule.getSupportReferenceId).mockReturnValue('REF-123');

    const error = new Error('Something went wrong');
    render(<ApiErrorBanner error={error} />);
    expect(screen.getByText('Ref: REF-123')).toBeInTheDocument();
  });
});
