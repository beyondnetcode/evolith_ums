import React from 'react';
import { ShieldAlert, AlertCircle, Info } from 'lucide-react';

type ErrorDisplayVariant = 'banner' | 'inline' | 'text';

interface ErrorDisplayProps {
  error: string;
  variant?: ErrorDisplayVariant;
  className?: string;
  icon?: React.ReactNode;
}

export const ErrorDisplay: React.FC<ErrorDisplayProps> = ({
  error,
  variant = 'inline',
  className = '',
  icon,
}) => {
  if (!error) return null;

  const defaultIcon =
    variant === 'banner' ? (
      <ShieldAlert className="w-4 h-4 flex-shrink-0" />
    ) : (
      <AlertCircle className="w-3 h-3" />
    );

  if (variant === 'banner') {
    return (
      <div
        className={`flex items-center gap-2 rounded-lg bg-m3-error-container/30 p-3 text-xs text-m3-error border border-m3-error/20 ${className}`}
      >
        {icon ?? defaultIcon}
        <span>{error}</span>
      </div>
    );
  }

  if (variant === 'text') {
    return (
      <span className={`text-[10px] text-rose-500 ${className}`}>
        {icon && <span className="inline mr-1">{icon}</span>}
        {error}
      </span>
    );
  }

  return (
    <div className={`flex items-center gap-1.5 ${className}`}>
      {icon ?? defaultIcon}
      <span className="text-[11px] text-m3-error">{error}</span>
    </div>
  );
};

interface InfoDisplayProps {
  message: string;
  className?: string;
}

export const InfoDisplay: React.FC<InfoDisplayProps> = ({ message, className = '' }) => (
  <div className={`flex items-center gap-1.5 ${className}`}>
    <Info className="w-3 h-3 text-m3-secondary" />
    <span className="text-[11px] text-m3-secondary">{message}</span>
  </div>
);
