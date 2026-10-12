import React from 'react';
import { ChevronLeft, ChevronRight, ChevronsUp, ChevronsDown } from 'lucide-react';
import { useState, useCallback } from 'react';

interface PaginationControlsProps {
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
  onPageChange: (page: number) => void;
}

export const PaginationControls: React.FC<PaginationControlsProps> = ({
  page,
  totalPages,
  onPageChange,
}) => {
  const [isCollapsed, setIsCollapsed] = useState(false);
  const toggle = useCallback(() => setIsCollapsed(v => !v), []);

  return (
    <div className="flex justify-end items-center gap-2 px-3 pb-2.5">
      <button
        type="button"
        onClick={() => onPageChange(Math.max(1, page - 1))}
        disabled={page === 1}
        className="p-1.5 rounded-lg border border-m3-outline bg-m3-surface text-m3-secondary disabled:opacity-40 disabled:cursor-not-allowed hover:bg-m3-primary/10 hover:text-m3-primary transition-all"
      >
        <ChevronLeft className="w-4 h-4" />
      </button>

      <div className="flex items-center gap-1 select-none">
        {Array.from({ length: totalPages }, (_, i) => i + 1).map(p => {
          const isActive = p === page;
          return (
            <button
              key={p}
              type="button"
              onClick={() => onPageChange(p)}
              className={`h-7 min-w-7 px-2 text-[12px] font-medium rounded-lg flex items-center justify-center transition-all ${
                isActive
                  ? 'bg-m3-primary text-m3-on-primary shadow-sm'
                  : 'border border-m3-outline bg-m3-surface hover:bg-m3-primary/10 text-m3-secondary'
              }`}
            >
              {p}
            </button>
          );
        })}
      </div>

      <button
        type="button"
        onClick={() => onPageChange(Math.min(totalPages, page + 1))}
        disabled={page === totalPages}
        className="p-1.5 rounded-lg border border-m3-outline bg-m3-surface text-m3-secondary disabled:opacity-40 disabled:cursor-not-allowed hover:bg-m3-primary/10 hover:text-m3-primary transition-all"
      >
        <ChevronRight className="w-4 h-4" />
      </button>
    </div>
  );
};

interface PaginationFooterProps extends PaginationControlsProps {
  telemetryInfo?: React.ReactNode;
}

export const PaginationFooter: React.FC<PaginationFooterProps> = ({
  page,
  pageSize,
  totalItems,
  totalPages,
  onPageChange,
  telemetryInfo,
}) => {
  const [isCollapsed, setIsCollapsed] = useState(false);
  const toggle = useCallback(() => setIsCollapsed(v => !v), []);

  return (
    <div className="flex-shrink-0 bg-m3-surface-container/20 border border-m3-outline/20 rounded-xl overflow-hidden">
      <div className="flex items-center justify-between px-3 h-9 gap-3">
        <div className="flex items-center gap-2 min-w-0 flex-1 text-xs font-medium text-m3-secondary">
          {telemetryInfo ?? (
            <div className="flex items-center gap-1.5">
              <span className="h-2 w-2 rounded-full bg-emerald-500 animate-pulse flex-shrink-0" />
              <span className="text-xs text-m3-secondary/70 truncate">
                {totalItems} records · {pageSize} per page
              </span>
            </div>
          )}
        </div>

        {totalPages > 1 && (
          <button
            type="button"
            title={isCollapsed ? 'Show pagination' : 'Hide pagination'}
            onClick={toggle}
            className={[
              'flex-shrink-0 p-1.5 rounded-lg border transition-all duration-150',
              isCollapsed
                ? 'bg-m3-primary/10 border-m3-primary/40 text-m3-primary'
                : 'bg-m3-surface-container/60 border-m3-outline/40 text-m3-secondary hover:bg-m3-primary/10 hover:border-m3-primary/40 hover:text-m3-primary',
            ].join(' ')}
          >
            {isCollapsed ? (
              <ChevronsUp className="w-3.5 h-3.5" />
            ) : (
              <ChevronsDown className="w-3.5 h-3.5" />
            )}
          </button>
        )}
      </div>

      {totalPages > 1 && (
        <div
          style={{ maxHeight: isCollapsed ? 0 : '3.5rem' }}
          className="overflow-hidden transition-[max-height] duration-200 ease-in-out"
        >
          <PaginationControls
            page={page}
            pageSize={pageSize}
            totalItems={totalItems}
            totalPages={totalPages}
            onPageChange={onPageChange}
          />
        </div>
      )}
    </div>
  );
};
