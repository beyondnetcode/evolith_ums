import React from 'react';
import { Plus } from 'lucide-react';

/**
 * AddButton — minimalist circular "+" button for inline addition
 *
 * Use inside detail panels and sections where adding items inline.
 * - Size: 32x32px (w-8 h-8)
 * - Style: circular, outlined, minimal
 */
interface AddButtonProps {
  onClick: () => void;
  title?: string;
  className?: string;
}

export const AddButton: React.FC<AddButtonProps> = ({
  onClick,
  title = 'Agregar',
  className = '',
}) => (
  <button
    type="button"
    onClick={onClick}
    title={title}
    className={`inline-flex items-center justify-center w-8 h-8 rounded-full
      border border-m3-outline/30 text-m3-secondary
      hover:border-m3-primary/50 hover:text-m3-primary hover:bg-m3-primary/10
      transition-all focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-m3-primary
      active:scale-95 ${className}`}
  >
    <Plus className="w-4 h-4" />
  </button>
);

/**
 * AddButtonInline — for use inside inline forms or compact spaces
 * Smaller variant: 28x28px
 */
interface AddButtonInlineProps {
  onClick: () => void;
  title?: string;
  className?: string;
}

export const AddButtonInline: React.FC<AddButtonInlineProps> = ({
  onClick,
  title = 'Agregar',
  className = '',
}) => (
  <button
    type="button"
    onClick={onClick}
    title={title}
    className={`inline-flex items-center justify-center w-7 h-7 rounded-full
      border border-m3-outline/20 text-m3-secondary/60
      hover:border-m3-primary/40 hover:text-m3-primary hover:bg-m3-primary/10
      transition-all focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-m3-primary
      active:scale-95 ${className}`}
  >
    <Plus className="w-3.5 h-3.5" />
  </button>
);
