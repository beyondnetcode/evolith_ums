import React from 'react';
import { AlertTriangle, RefreshCw, ServerCrash } from 'lucide-react';
import { useI18nStore } from '@app/stores/i18n.store';
import translations from '@app/i18n/translations';
import { getSupportReferenceId } from '@app/errors/http-error';

interface ErrorBoundaryProps {
  children: React.ReactNode;
  fallback?: (error: Error, reset: () => void) => React.ReactNode;
  onRetry?: () => void;
}

interface ErrorBoundaryState {
  error: Error | null;
}

export class AppErrorBoundary extends React.Component<ErrorBoundaryProps, ErrorBoundaryState> {
  constructor(props: ErrorBoundaryProps) {
    super(props);
    this.state = { error: null };
  }

  static getDerivedStateFromError(error: Error): ErrorBoundaryState {
    return { error };
  }

  componentDidCatch(error: Error, info: React.ErrorInfo) {
    console.error('[UMS ErrorBoundary]', error, info.componentStack);
  }

  reset = () => {
    const { onRetry } = this.props;
    onRetry?.();
    this.setState({ error: null });
  };

  render() {
    const { error } = this.state;
    const { children, fallback } = this.props;

    if (!error) return children;

    if (fallback) return fallback(error, this.reset);

    const lang = useI18nStore.getState().language;
    const t = translations[lang];

    let isNetworkError = false;
    const supportReferenceId = getSupportReferenceId(error);

    if (error.message.includes('Network Error') || error.name === 'AxiosError') {
      isNetworkError = true;
    }

    const title = isNetworkError ? t.errorNetworkTitle : t.errorGenericTitle;
    const description = t.errorGenericMsg;

    return (
      <div className="flex-1 flex items-center justify-center p-8">
        <div className="max-w-md w-full rounded-2xl border border-m3-error/30 bg-m3-error-container/10 p-8 text-center space-y-4 shadow-lg">
          <div className="w-14 h-14 bg-m3-error/10 border border-m3-error/20 text-m3-error rounded-full flex items-center justify-center mx-auto">
            {isNetworkError ? (
              <ServerCrash className="w-7 h-7" />
            ) : (
              <AlertTriangle className="w-7 h-7" />
            )}
          </div>
          <div>
            <h2 className="text-sm font-semibold text-m3-on-surface">{title}</h2>
            <p className="mt-1 text-xs text-m3-secondary leading-relaxed">{description}</p>
          </div>
          {supportReferenceId && (
            <p className="text-xs text-m3-secondary">
              {t.errorSupportReference}: {supportReferenceId}
            </p>
          )}
          <button
            onClick={this.reset}
            className="inline-flex items-center gap-2 px-5 py-2 rounded-full bg-m3-primary text-m3-on-primary text-sm font-medium hover:bg-m3-primary/90 transition-colors"
          >
            <RefreshCw className="w-3.5 h-3.5" /> {t.errorRetry}
          </button>
        </div>
      </div>
    );
  }
}

export { AppErrorBoundary as ErrorBoundary };
export type { ErrorBoundaryProps };

export const RouteErrorBoundary: React.FC<{ children: React.ReactNode }> = ({ children }) => (
  <AppErrorBoundary>{children}</AppErrorBoundary>
);
