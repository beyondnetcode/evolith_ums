import React, { useState, useCallback } from 'react';
import { Search, LayoutList, LayoutGrid, Plus, Database } from 'lucide-react';
import { M3Card } from './M3Card';
import { M3TextField } from './M3TextField';
import { EmptyState } from './EmptyState';
import { M3SegmentedButton } from './M3SegmentedButton';
import type { SegmentOption } from './M3SegmentedButton';
import { M3SkeletonRow } from './M3Skeleton';
import { useDragResize } from '@app/hooks/use-drag-resize';
import { FilterBar } from './data-view/FilterBar';
import { SortDropdown } from './data-view/SortDropdown';
import { PaginationFooter } from './data-view/PaginationControls';

export interface SortOption {
  label: string;
  value: string;
}
export interface FilterOption {
  label: string;
  value: string;
}
export interface QueryCriteriaOption {
  label: string;
  value: string;
}
export interface PaginationData {
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
  onPageChange: (page: number) => void;
}

const VIEW_MODE_OPTIONS: SegmentOption<'list' | 'thumbnail'>[] = [
  { value: 'list', label: <LayoutList className="w-4 h-4" /> },
  { value: 'thumbnail', label: <LayoutGrid className="w-4 h-4" /> },
];

interface M3DataViewProps {
  title: string;
  subtitle?: string;

  searchPlaceholder?: string;
  searchCriteria?: QueryCriteriaOption[];
  activeCriteria?: string;
  onCriteriaChange?: (criteria: string) => void;
  searchValue: string;
  onSearchValueChange: (value: string) => void;
  onSearchSubmit: (e: React.FormEvent) => void;

  onRegisterNew?: () => void;
  registerLabel?: string;

  viewMode: 'list' | 'thumbnail';
  onViewModeChange: (mode: 'list' | 'thumbnail') => void;

  sortOptions?: SortOption[];
  sortBy?: string;
  onSortByChange?: (val: string) => void;
  sortOrder?: 'asc' | 'desc';
  onSortOrderToggle?: () => void;

  filterOptions?: FilterOption[];
  activeFilter?: string;
  onFilterChange?: (val: string) => void;

  isLoading?: boolean;
  isEmpty?: boolean;
  emptyLabel?: string;
  emptyTitle?: string;
  emptyTooltip?: string;
  loadingLabel?: string;
  criteriaLabel?: string;
  searchTermLabel?: string;
  searchButtonLabel?: string;

  renderList: () => React.ReactNode;
  renderThumbnail: () => React.ReactNode;

  pagination?: PaginationData;
  telemetryInfo?: React.ReactNode;
}

export const M3DataView: React.FC<M3DataViewProps> = ({
  title,
  subtitle,
  searchPlaceholder = 'Search...',
  searchCriteria,
  activeCriteria,
  onCriteriaChange,
  searchValue,
  onSearchValueChange,
  onSearchSubmit,
  onRegisterNew,
  registerLabel = 'New',
  viewMode,
  onViewModeChange,
  sortOptions,
  sortBy,
  onSortByChange,
  sortOrder,
  onSortOrderToggle,
  filterOptions,
  activeFilter,
  onFilterChange,
  isLoading = false,
  isEmpty = false,
  emptyLabel = 'No matching records found.',
  emptyTitle = 'No results found',
  emptyTooltip,
  criteriaLabel = 'Criteria',
  searchTermLabel = 'Search term',
  searchButtonLabel = 'Search',
  renderList,
  renderThumbnail,
  pagination,
  telemetryInfo,
}) => {
  const {
    size: topPx,
    isCollapsed: isHeaderCollapsed,
    isDragging: isDraggingH,
    containerRef: dvContainerRef,
    resizableRef: searchZoneRef,
    handleMouseDown: handleHSplitterMouseDown,
    handleTouchStart: handleHSplitterTouchStart,
    handleKeyDown: handleHSplitterKeyDown,
    toggleCollapse: toggleHeader,
  } = useDragResize();

  return (
    <div
      ref={dvContainerRef}
      className={`flex flex-col h-full${isDraggingH ? ' select-none cursor-row-resize' : ''}`}
    >
      {/*
        ══════════════════════════════════════════════════════════════
        ZONE A — Title bar   (ALWAYS VISIBLE — never collapses)
        Contains: title, subtitle, + New button, and the header toggle.
        Lives outside the collapsible searchZoneRef so the toggle
        button is always reachable regardless of collapsed state.
        ══════════════════════════════════════════════════════════════
      */}
      <M3Card
        variant="elevated"
        className="flex-shrink-0 border border-m3-outline/25 bg-m3-surface-container/20 shadow-sm"
      >
        <div className="px-5 py-4 flex items-center justify-between gap-3">
          {/* Left: title + subtitle */}
          <div className="min-w-0 flex-1 space-y-0.5">
            <h2 className="text-base font-semibold text-m3-on-surface flex items-center gap-2">
              <Database className="w-5 h-5 text-m3-primary flex-shrink-0" />
              <span className="truncate">{title}</span>
            </h2>
            {subtitle && (
              <p className="text-xs text-m3-secondary font-normal pl-7 truncate">{subtitle}</p>
            )}
          </div>

          {/* Right: + New  +  header collapse toggle */}
          <div className="flex items-center gap-2 flex-shrink-0">
            {onRegisterNew && (
              <button
                type="button"
                onClick={onRegisterNew}
                className="inline-flex h-8 items-center gap-1.5 rounded-lg border border-m3-primary/25 bg-m3-primary/10 px-3 text-xs font-medium text-m3-primary transition-colors hover:border-m3-primary/45 hover:bg-m3-primary/15 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-m3-primary focus-visible:ring-offset-1"
              >
                <Plus className="h-3.5 w-3.5" /> {registerLabel}
              </button>
            )}

            {/* Header collapse / expand button — always reachable */}
            <button
              type="button"
              title={isHeaderCollapsed ? 'Show search & filters' : 'Hide search & filters'}
              onClick={toggleHeader}
              className={[
                'p-1.5 rounded-lg border transition-all duration-150',
                isHeaderCollapsed
                  ? 'bg-m3-primary/10 border-m3-primary/40 text-m3-primary'
                  : 'bg-m3-surface-container/60 border-m3-outline/40 text-m3-secondary hover:bg-m3-primary/10 hover:border-m3-primary/40 hover:text-m3-primary',
              ].join(' ')}
            >
              {isHeaderCollapsed ? (
                <ChevronsDown className="w-3.5 h-3.5" />
              ) : (
                <ChevronsUp className="w-3.5 h-3.5" />
              )}
            </button>
          </div>
        </div>
      </M3Card>

      {/*
        ══════════════════════════════════════════════════════════════
        ZONE B — Search form + control row   (collapsible + draggable)
        searchZoneRef tracks the rendered height for the drag handler.
        When isHeaderCollapsed: height → 0 (overflow hidden).
        ══════════════════════════════════════════════════════════════
      */}
      <div
        ref={searchZoneRef}
        style={topPx !== null ? { height: topPx } : undefined}
        className={[
          'flex flex-col gap-3 flex-shrink-0',
          topPx !== null ? 'overflow-hidden' : 'overflow-visible',
          topPx !== null && !isDraggingH ? 'transition-[height] duration-200 ease-in-out' : '',
        ].join(' ')}
      >
        {/* Search form card */}
        <M3Card
          variant="elevated"
          className="mt-3 p-3 border border-m3-outline/25 bg-m3-surface-container/20 shadow-sm flex-shrink-0"
        >
          <form onSubmit={onSearchSubmit} className="flex flex-col gap-2 sm:flex-row sm:items-end">
            {searchCriteria && onCriteriaChange && activeCriteria && (
              <div className="relative flex w-full flex-col sm:w-40 sm:flex-shrink-0">
                <label className="block text-[11px] font-medium text-m3-secondary mb-1 ml-1">
                  {criteriaLabel}
                </label>
                <select
                  value={activeCriteria}
                  onChange={e => onCriteriaChange(e.target.value)}
                  aria-label={criteriaLabel}
                  className="w-full h-10 px-3 text-xs rounded-lg border border-m3-outline bg-m3-surface-container/30 dark:bg-m3-surface-container/20 text-m3-on-surface focus:outline-none focus:border-m3-primary transition-colors cursor-pointer"
                >
                  {searchCriteria.map(c => (
                    <option key={c.value} value={c.value}>
                      {c.label}
                    </option>
                  ))}
                </select>
              </div>
            )}

            <div className="w-full sm:min-w-0 sm:flex-1">
              <M3TextField
                dense
                label={searchTermLabel}
                value={searchValue}
                onChange={e => onSearchValueChange(e.target.value)}
                placeholder={searchPlaceholder}
                className="mb-0"
              />
            </div>

            <div className="flex items-center">
              <button
                type="submit"
                className="inline-flex h-10 items-center justify-center gap-1.5 rounded-lg border border-m3-primary/25 bg-m3-primary/10 px-3 text-xs font-medium text-m3-primary transition-colors hover:border-m3-primary/45 hover:bg-m3-primary/15 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-m3-primary focus-visible:ring-offset-1"
              >
                <Search className="h-3.5 w-3.5" /> {searchButtonLabel}
              </button>
            </div>
          </form>
        </M3Card>

        {/* Control row: filter + sort + view mode */}
        <div className="flex flex-col sm:flex-row justify-between items-stretch sm:items-center gap-2 bg-m3-surface-container/30 border border-m3-outline/20 rounded-xl p-2 flex-shrink-0">
          <div className="flex flex-wrap items-center gap-2">
            <FilterBar
              filterOptions={filterOptions}
              activeFilter={activeFilter}
              onFilterChange={onFilterChange}
            />

            <SortDropdown
              sortOptions={sortOptions}
              sortBy={sortBy}
              onSortByChange={onSortByChange}
              sortOrder={sortOrder}
              onSortOrderToggle={onSortOrderToggle}
            />
          </div>

          {/* View mode segmented control */}
          <M3SegmentedButton
            options={VIEW_MODE_OPTIONS}
            value={viewMode}
            onChange={onViewModeChange}
            size="sm"
            className="self-end sm:self-auto"
          />
        </div>
      </div>
      {/* end search zone */}

      {/*
        ══════════════════════════════════════════════════════════════
        SPLITTER — drag handle between search zone and list
        ══════════════════════════════════════════════════════════════
      */}
      <div
        onMouseDown={handleHSplitterMouseDown}
        onTouchStart={handleHSplitterTouchStart}
        onKeyDown={handleHSplitterKeyDown}
        role="separator"
        tabIndex={0}
        aria-orientation="horizontal"
        aria-label="Resize tenant search panel"
        aria-valuemin={0}
        aria-valuemax={100}
        aria-valuenow={topPx ?? undefined}
        className={[
          // `touch-none` + zona táctil de 32px (`before`) sin engrosar la línea visible (densidad).
          'relative flex-shrink-0 h-1.5 w-full flex items-center justify-center group cursor-row-resize z-10 mt-1 touch-none focus:outline-none focus-visible:ring-2 focus-visible:ring-m3-primary',
          'before:absolute before:inset-x-0 before:top-1/2 before:-translate-y-1/2 before:h-8 before:content-[""]',
          isDraggingH
            ? 'bg-m3-primary/20'
            : 'hover:bg-m3-primary/10 transition-colors duration-150',
        ].join(' ')}
      >
        <div
          className={[
            'h-px w-full transition-colors duration-150',
            isDraggingH ? 'bg-m3-primary/60' : 'bg-m3-outline/30 group-hover:bg-m3-primary/40',
          ].join(' ')}
        />
        {/* Pill — always visible on the handle */}
        <button
          type="button"
          title={isHeaderCollapsed ? 'Expand search panel' : 'Collapse search panel'}
          onClick={e => {
            e.stopPropagation();
            toggleHeader();
          }}
          onMouseDown={e => e.stopPropagation()}
          onTouchStart={e => e.stopPropagation()}
          className={[
            'absolute left-1/2 -translate-x-1/2 -translate-y-px',
            'h-4 w-12 rounded-full flex items-center justify-center gap-0.5',
            // Zona táctil de 32px de alto sin cambiar el tamaño visible de la píldora.
            'before:absolute before:inset-x-0 before:top-1/2 before:-translate-y-1/2 before:h-8 before:content-[""]',
            'border shadow-sm transition-all duration-150 text-[10px] font-medium',
            isDraggingH
              ? 'bg-m3-primary text-white border-m3-primary'
              : isHeaderCollapsed
                ? 'bg-m3-primary/15 border-m3-primary/50 text-m3-primary hover:bg-m3-primary/25'
                : 'bg-m3-surface-container border-m3-outline/50 text-m3-secondary hover:bg-m3-primary/10 hover:border-m3-primary/40 hover:text-m3-primary',
          ].join(' ')}
        >
          {isHeaderCollapsed ? (
            <ChevronsDown className="w-3 h-3" />
          ) : (
            <ChevronsUp className="w-3 h-3" />
          )}
        </button>
      </div>

      {/*
        ══════════════════════════════════════════════════════════════
        ZONE C — Content + Footer   (flex-1, takes remaining height)
        ══════════════════════════════════════════════════════════════
      */}
      <div className="flex-1 flex flex-col gap-3 min-h-0 overflow-hidden pt-1">
        {/* List content */}
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
              {viewMode === 'list' ? renderList() : renderThumbnail()}
            </div>
          )}
        </div>

        {pagination && (
          <PaginationFooter
            page={pagination.page}
            pageSize={pagination.pageSize}
            totalItems={pagination.totalItems}
            totalPages={pagination.totalPages}
            onPageChange={pagination.onPageChange}
            telemetryInfo={telemetryInfo}
          />
        )}
      </div>
      {/* end bottom zone */}
    </div>
  );
};
