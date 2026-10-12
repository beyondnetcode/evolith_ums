import React from 'react';
import { M3Card } from '../M3Card';
import { Database, ChevronsUp, ChevronsDown } from 'lucide-react';
import { useDragResize } from '@app/hooks/use-drag-resize';

interface DataViewShellProps {
  title: string;
  subtitle?: string;
  headerIcon?: React.ReactNode;
  controls: React.ReactNode;
  content: React.ReactNode;
}

export const DataViewShell: React.FC<DataViewShellProps> = ({
  title,
  subtitle,
  headerIcon = <Database className="w-4 h-4 text-m3-primary flex-shrink-0" />,
  controls,
  content,
}) => {
  const {
    size: topPx,
    isCollapsed: isHeaderCollapsed,
    isDragging: isDraggingH,
    containerRef: dvContainerRef,
    resizableRef: searchZoneRef,
    handleMouseDown: handleHSplitterMouseDown,
    handleKeyDown: handleHSplitterKeyDown,
    toggleCollapse: toggleHeader,
  } = useDragResize();

  return (
    <div
      ref={dvContainerRef}
      className={`flex flex-col h-full${isDraggingH ? ' select-none cursor-row-resize' : ''}`}
    >
      {/* ZONE A — Minimal title bar */}
      <div className="px-4 py-2 border-b border-m3-outline/15 flex items-center justify-between gap-3 bg-m3-surface/50">
        <div className="flex items-center gap-2 min-w-0">
          {headerIcon}
          <div className="min-w-0">
            <h2 className="text-[12px] font-semibold text-m3-on-surface truncate">{title}</h2>
            {subtitle && <p className="text-[11px] text-m3-secondary truncate">{subtitle}</p>}
          </div>
        </div>

        <button
          type="button"
          title={isHeaderCollapsed ? 'Mostrar filtros' : 'Ocultar filtros'}
          onClick={toggleHeader}
          className="p-1 rounded text-m3-secondary/60 hover:text-m3-primary hover:bg-m3-primary/10 transition-colors"
        >
          {isHeaderCollapsed ? (
            <ChevronsDown className="w-4 h-4" />
          ) : (
            <ChevronsUp className="w-4 h-4" />
          )}
        </button>
      </div>

      {/* ZONE B — Collapsible controls */}
      <div
        ref={searchZoneRef}
        style={topPx !== null ? { height: topPx } : undefined}
        className={[
          'flex flex-col flex-shrink-0 bg-m3-surface-container/5',
          topPx !== null ? 'overflow-hidden' : 'overflow-visible',
          topPx !== null && !isDraggingH ? 'transition-[height] duration-200 ease-in-out' : '',
        ].join(' ')}
      >
        {controls}
      </div>

      {/* ZONE C — Content */}
      <div className="flex-1 flex flex-col min-h-0 overflow-hidden">{content}</div>
    </div>
  );
};
