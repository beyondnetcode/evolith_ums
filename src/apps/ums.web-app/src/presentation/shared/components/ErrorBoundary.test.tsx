import { describe, it, expect, vi } from 'vitest';
import { render, screen, act } from '@testing-library/react';
import { AppErrorBoundary } from './ErrorBoundary';

vi.mock('@app/stores/i18n.store', () => ({
  useI18nStore: {
    getState: () => ({ language: 'en' }),
  },
}));

vi.mock('@app/i18n/translations', () => ({
  default: {
    en: {
      errorGenericTitle: 'Something went wrong',
      errorNetworkTitle: 'Network error',
      errorGenericMsg: 'An unexpected error occurred',
      errorRetry: 'Try again',
      errorSupportReference: 'Support reference',
    },
  },
}));

vi.mock('@app/errors/http-error', () => ({
  getSupportReferenceId: () => null,
}));

describe('AppErrorBoundary', () => {
  it('renders children when no error', () => {
    render(
      <AppErrorBoundary>
        <div data-testid="child">Hello</div>
      </AppErrorBoundary>
    );
    expect(screen.getByTestId('child')).toHaveTextContent('Hello');
  });

  it('renders error UI when child throws', () => {
    const Bomb = () => {
      throw new Error('Test error');
    };

    vi.spyOn(console, 'error').mockImplementation(() => {});

    render(
      <AppErrorBoundary>
        <Bomb />
      </AppErrorBoundary>
    );

    expect(screen.getByText(/Something went wrong/i)).toBeInTheDocument();
  });

  it('resets error state when reset is called', () => {
    let shouldThrow = true;
    const Bomb = () => {
      if (shouldThrow) {
        throw new Error('Test error');
      }
      return <div data-testid="recovered">Recovered</div>;
    };

    vi.spyOn(console, 'error').mockImplementation(() => {});

    render(
      <AppErrorBoundary>
        <Bomb />
      </AppErrorBoundary>
    );

    expect(screen.getByText('Something went wrong')).toBeInTheDocument();

    shouldThrow = false;
    act(() => {
      screen.getByText('Try again').click();
    });

    expect(screen.getByTestId('recovered')).toBeInTheDocument();
  });

  it('uses custom fallback when provided', () => {
    const Bomb = () => {
      throw new Error('Test error');
    };

    vi.spyOn(console, 'error').mockImplementation(() => {});

    render(
      <AppErrorBoundary fallback={error => <div data-testid="custom">Custom: {error.message}</div>}>
        <Bomb />
      </AppErrorBoundary>
    );

    expect(screen.getByTestId('custom')).toHaveTextContent('Custom: Test error');
  });
});
