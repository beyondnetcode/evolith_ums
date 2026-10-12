import React from 'react';
import { PAGE_SIZE_OPTIONS, type PageSizeOption } from '@app/shared/hooks/use-pagination-state';

interface PageSizeSelectorProps {
  value: PageSizeOption;
  onChange: (size: PageSizeOption) => void;
}

export const PageSizeSelector: React.FC<PageSizeSelectorProps> = ({ value, onChange }) => {
  return (
    <div className="flex items-center gap-1.5">
      <span className="text-[10px] font-medium text-m3-secondary/60 uppercase tracking-wide">
        Filas
      </span>
      <div className="flex items-center rounded-lg border border-m3-outline/20 bg-m3-surface-container/20 overflow-hidden">
        {PAGE_SIZE_OPTIONS.map(size => (
          <button
            key={size}
            type="button"
            onClick={() => onChange(size)}
            className={`px-2 py-1 text-[10px] font-medium transition-colors ${
              value === size
                ? 'bg-m3-primary/10 text-m3-primary'
                : 'text-m3-secondary/60 hover:text-m3-on-surface hover:bg-m3-outline/10'
            }`}
          >
            {size}
          </button>
        ))}
      </div>
    </div>
  );
};
