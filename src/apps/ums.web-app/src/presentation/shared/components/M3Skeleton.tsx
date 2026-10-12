import React from 'react';

interface M3SkeletonProps {
  /** Shape of the skeleton */
  variant?: 'text' | 'circular' | 'rectangular' | 'rounded';
  /** Width of the skeleton (e.g., '100%', '40px') */
  width?: string | number;
  /** Height of the skeleton (e.g., '20px', '40px') */
  height?: string | number;
  /** Additional Tailwind classes */
  className?: string;
  /** Use a darker surface variant for contrast when nested */
  darker?: boolean;
}

export const M3Skeleton: React.FC<M3SkeletonProps> = ({
  variant = 'text',
  width,
  height,
  className = '',
  darker = false,
}) => {
  const getVariantClass = () => {
    switch (variant) {
      case 'circular':
        return 'rounded-full';
      case 'rounded':
        return 'rounded-lg';
      case 'rectangular':
        return 'rounded-none';
      case 'text':
      default:
        return 'rounded-[4px]';
    }
  };

  const getHeight = () => {
    if (height) return typeof height === 'number' ? `${height}px` : height;
    if (variant === 'text') return '1.25em';
    return 'auto';
  };

  const getWidth = () => {
    if (width) return typeof width === 'number' ? `${width}px` : width;
    return '100%';
  };

  const baseColor = darker
    ? 'bg-m3-surface-container-high/60 dark:bg-m3-surface-container-high/40'
    : 'bg-m3-surface-variant/40 dark:bg-m3-surface-variant/20';

  return (
    <div
      className={`animate-pulse ${baseColor} ${getVariantClass()} ${className}`}
      style={{
        width: getWidth(),
        height: getHeight(),
      }}
      aria-hidden="true"
    />
  );
};

export const M3SkeletonRow: React.FC<{ columns?: number; className?: string }> = ({
  columns = 3,
  className = '',
}) => (
  <div className={`flex items-center gap-4 py-3 ${className}`}>
    <M3Skeleton variant="circular" width={40} height={40} className="flex-shrink-0" />
    <div className="flex-1 space-y-2">
      <M3Skeleton variant="text" width="40%" height={16} />
      <div className="flex gap-2">
        {Array.from({ length: columns - 1 }).map((_, i) => (
          <M3Skeleton key={i} variant="text" width={`${Math.max(15, 30 - i * 5)}%`} height={12} />
        ))}
      </div>
    </div>
  </div>
);
