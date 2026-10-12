import React, { useState, useCallback } from 'react';
import { EmptyState } from '../EmptyState';
import { M3SkeletonRow } from '../M3Skeleton';
import { ChevronLeft, ChevronRight, ChevronsUp, ChevronsDown } from 'lucide-react';
import { PageSizeSelector } from './PageSizeSelector';
import type { PageSizeOption } from '@app/shared/hooks/use-pagination-state';

export interface PaginationData {
  page: number;
  pageSize: PageSizeOption;
  totalItems: number;
  totalPages: number;
  onPageChange: (page: number) => void;
  onPageSizeChange?: (size: PageSizeOption) => void;
}

interface DataListProps {
  isLoading?: boolean;
  isEmpty?: boolean;
  emptyLabel?: string;
  emptyTitle?: string;
  emptyTooltip?: string;
  viewMode?: 'list' | 'thumbnail';
  renderList: () => React.ReactNode;
  renderThumbnail?: () => React.ReactNode;
  pagination?: PaginationData;
  telemetryInfo?: React.ReactNode;
}

export const DataList: React.FC<DataListProps> = ({
  isLoading = false,
  isEmpty = false,
  emptyLabel = 'No matching records found.',
  emptyTitle = 'No results found',
  emptyTooltip,
  viewMode = 'list',
  renderList,
  renderThumbnail,
  pagination,
  telemetryInfo,
}) => {
  const [isFooterCollapsed, setIsFooterCollapsed] = useState(false);
  const toggleFooter = useCallback(() => setIsFooterCollapsed(v => !v), []);

  return (
    <>
      <div className="flex-1 relative overflow-auto">
        {isLoading ? (
          <div className="flex flex-col space-y-4 px-2 py-4">
            <M3SkeletonRow columns={viewMode === 'list' ? 4 : 2} />
            <M3SkeletonRow columns={viewMode === 'list' ? 4 : 2} />
            <M3SkeletonRow columns={viewMode === 'list' ? 4 : 2} />
            <M3SkeletonRow columns={viewMode === 'list' ? 4 : 2} />
            <M3SkeletonRow columns={viewMode === 'list' ? 4 : 2} />
          </div>
        ) : isEmpty ? (
          <EmptyState
            variant="card"
            title={emptyTitle}
            message={emptyLabel}
            tooltip={emptyTooltip}
          />
        ) : (
          <div className="animate-fadeIn">
            {viewMode === 'list'
              ? renderList()
              : renderThumbnail
                ? renderThumbnail()
                : renderList()}
          </div>
        )}
      </div>

      {(pagination || telemetryInfo) && (
        <div className="flex-shrink-0 bg-m3-surface-container/20 border border-m3-outline/20 rounded-xl overflow-hidden mt-3">
          <div className="flex items-center justify-between px-3 h-9 gap-3">
            <div className="flex items-center gap-2 min-w-0 flex-1 text-xs font-medium text-m3-secondary">
              {telemetryInfo ??
                (pagination && (
                  <div className="flex items-center gap-1.5">
                    <span className="h-2 w-2 rounded-full bg-emerald-500 animate-pulse flex-shrink-0" />
                    <span className="text-xs text-m3-secondary/70 truncate">
                      {pagination.totalItems} records · {pagination.pageSize} per page
                    </span>
                  </div>
                ))}
            </div>

            {pagination && pagination.totalPages > 1 && (
              <button
                type="button"
                title={isFooterCollapsed ? 'Show pagination' : 'Hide pagination'}
                onClick={toggleFooter}
                className={[
                  'flex-shrink-0 p-1.5 rounded-lg border transition-all duration-150',
                  isFooterCollapsed
                    ? 'bg-m3-primary/10 border-m3-primary/40 text-m3-primary'
                    : 'bg-m3-surface-container/60 border-m3-outline/40 text-m3-secondary hover:bg-m3-primary/10 hover:border-m3-primary/40 hover:text-m3-primary',
                ].join(' ')}
              >
                {isFooterCollapsed ? (
                  <ChevronsUp className="w-3.5 h-3.5" />
                ) : (
                  <ChevronsDown className="w-3.5 h-3.5" />
                )}
              </button>
            )}
          </div>

          {pagination && pagination.totalPages > 1 && (
            <div
              style={{ maxHeight: isFooterCollapsed ? 0 : '3.5rem' }}
              className="overflow-hidden transition-[max-height] duration-200 ease-in-out"
            >
              <div className="flex justify-between items-center gap-2 px-3 pb-2.5">
                {pagination.onPageSizeChange && (
                  <PageSizeSelector
                    value={pagination.pageSize}
                    onChange={pagination.onPageSizeChange}
                  />
                )}

                <div className="flex items-center gap-2 ml-auto">
                  <button
                    type="button"
                    onClick={() => pagination.onPageChange(Math.max(1, pagination.page - 1))}
                    disabled={pagination.page === 1}
                    className="p-1.5 rounded-lg border border-m3-outline bg-m3-surface text-m3-secondary disabled:opacity-40 disabled:cursor-not-allowed hover:bg-m3-primary/10 hover:text-m3-primary transition-all"
                  >
                    <ChevronLeft className="w-4 h-4" />
                  </button>

                  <div className="flex items-center gap-1 select-none">
                    {Array.from({ length: pagination.totalPages }, (_, i) => i + 1).map(p => {
                      const isActive = p === pagination.page;
                      return (
                        <button
                          key={p}
                          type="button"
                          onClick={() => pagination.onPageChange(p)}
                          className={`h-7 min-w-7 px-2 text-xs font-medium rounded-lg flex items-center justify-center transition-all ${
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
                    onClick={() =>
                      pagination.onPageChange(Math.min(pagination.totalPages, pagination.page + 1))
                    }
                    disabled={pagination.page === pagination.totalPages}
                    className="p-1.5 rounded-lg border border-m3-outline bg-m3-surface text-m3-secondary disabled:opacity-40 disabled:cursor-not-allowed hover:bg-m3-primary/10 hover:text-m3-primary transition-all"
                  >
                    <ChevronRight className="w-4 h-4" />
                  </button>
                </div>
              </div>
            </div>
          )}
        </div>
      )}
    </>
  );
};
