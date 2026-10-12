import React from 'react';

interface M3CardProps extends React.HTMLAttributes<HTMLDivElement> {
  variant?: 'elevated' | 'filled' | 'outlined';
  hoverable?: boolean;
}

export const M3Card = React.forwardRef<HTMLDivElement, M3CardProps>(
  ({ children, variant = 'elevated', hoverable = false, className = '', ...props }, ref) => {
    const baseStyle =
      'rounded-xl p-6 bg-m3-surface-container transition-all duration-300 relative overflow-hidden';

    const variants = {
      elevated: 'elevation-1 dark:border dark:border-m3-outline/20',
      filled: 'bg-m3-surface-container/50 border border-transparent',
      outlined: 'border border-m3-outline bg-transparent',
    };

    const hoverStyle = hoverable
      ? 'hover:-translate-y-1 hover:shadow-xl hover:shadow-m3-primary/5 cursor-pointer hover:border-m3-primary/30'
      : '';

    return (
      <div
        ref={ref}
        className={`${baseStyle} ${variants[variant]} ${hoverStyle} ${className}`}
        {...props}
      >
        {children}
      </div>
    );
  }
);

M3Card.displayName = 'M3Card';
