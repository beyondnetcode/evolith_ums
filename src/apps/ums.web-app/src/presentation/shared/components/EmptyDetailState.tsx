import React from 'react';
import type { LucideIcon } from 'lucide-react';

interface EmptyDetailStateProps {
  icon: LucideIcon;
  title: string;
  description?: string;
  className?: string;
}

export function EmptyDetailState({
  icon: Icon,
  title,
  description,
  className = '',
}: EmptyDetailStateProps): React.JSX.Element {
  return (
    <div
      className={`flex flex-col items-center justify-center h-full p-6 text-center ${className}`}
    >
      <Icon className="w-10 h-10 text-m3-outline mb-3" strokeWidth={1.5} />
      <p className="text-sm font-medium text-m3-on-surface-variant">{title}</p>
      {description && <p className="text-xs text-m3-secondary mt-1 max-w-[280px]">{description}</p>}
    </div>
  );
}
