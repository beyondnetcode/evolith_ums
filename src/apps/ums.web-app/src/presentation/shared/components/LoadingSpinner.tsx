import React from 'react';
import { Spinner } from './Spinner';

interface LoadingSpinnerProps {
  className?: string;
}

export const LoadingSpinner: React.FC<LoadingSpinnerProps> = ({ className = 'w-8 h-8' }) => {
  return (
    <div className="flex items-center justify-center p-8">
      <Spinner className={className} />
    </div>
  );
};
