import React from 'react';

interface PaginationFooterProps {
  totalItems: number;
  startIndex: number;
  pageSize: number;
  itemLabel?: string;
  onClear?: () => void;
  searchTerm?: string;
}

export const PaginationFooter: React.FC<PaginationFooterProps> = ({
  totalItems,
  startIndex,
  pageSize,
  itemLabel = 'items',
  onClear,
  searchTerm,
}) => (
  <div className="flex items-center gap-3">
    <div className="flex items-center gap-1.5">
      <span className="h-2 w-2 rounded-full bg-m3-primary animate-pulse" />
      <span className="text-[12px] font-medium text-m3-secondary/80">
        Mostrando {totalItems === 0 ? 0 : startIndex + 1}-
        {Math.min(startIndex + pageSize, totalItems)} de {totalItems} {itemLabel}
      </span>
    </div>
    {searchTerm && searchTerm.trim() && onClear && (
      <button
        onClick={onClear}
        className="text-[12px] font-medium text-rose-500 hover:underline flex items-center gap-1"
      >
        <span className="w-3 h-3">Limpiar</span>
      </button>
    )}
  </div>
);
