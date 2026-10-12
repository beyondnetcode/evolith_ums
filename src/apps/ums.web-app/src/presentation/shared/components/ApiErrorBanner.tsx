import React from 'react';
import { Info, WifiOff, AlertTriangle } from 'lucide-react';
import { useI18n } from '@app/i18n/use-i18n';
import { getSupportReferenceId } from '@app/errors/http-error';
import { getHttpStatus } from '@app/utils/error-utils';

export interface ApiErrorBannerProps {
  error: Error;
}

export const ApiErrorBanner: React.FC<ApiErrorBannerProps> = ({ error }) => {
  const t = useI18n();
  const status = getHttpStatus(error);
  const isUnavailable = status >= 500;
  const isValidation = status === 400 || status === 422;
  const supportReferenceId = getSupportReferenceId(error);

  const icon = isUnavailable ? (
    <WifiOff className="w-5 h-5 text-amber-500 mt-0.5 flex-shrink-0" />
  ) : isValidation ? (
    <AlertTriangle className="w-5 h-5 text-rose-500 mt-0.5 flex-shrink-0" />
  ) : (
    <Info className="w-5 h-5 text-rose-500 mt-0.5 flex-shrink-0" />
  );

  const title = isUnavailable
    ? t.errorBackendUnavailableTitle || 'Backend API Unavailable'
    : isValidation
      ? t.errorInvalidRequestTitle || 'Invalid Request'
      : t.errorGenericTitle || 'Error';

  const hint = isUnavailable
    ? t.errorBackendUnavailableHint || 'Start the backend API and refresh.'
    : isValidation
      ? t.errorInvalidRequestHint || 'Check the request parameters.'
      : t.errorGenericHint || 'Ensure the backend API is running.';

  return (
    <div className="mb-4 p-4 rounded-xl border border-rose-200 bg-rose-50">
      <div className="flex items-start gap-3">
        {icon}
        <div>
          <p className="text-sm font-medium text-rose-800">{title}</p>
          <p className="text-xs text-rose-700 mt-1">{hint}</p>
          {supportReferenceId && (
            <p className="text-xs text-rose-600 mt-2">
              {t.errorSupportReference(supportReferenceId)}
            </p>
          )}
        </div>
      </div>
    </div>
  );
};
