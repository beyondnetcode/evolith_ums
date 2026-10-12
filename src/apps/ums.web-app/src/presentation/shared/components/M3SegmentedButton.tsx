import React, { useRef, useState, useEffect } from 'react';
import { ChevronLeft, ChevronRight } from 'lucide-react';

/**
 * M3SegmentedButton — single-select segmented control.
 *
 * Renders a horizontal row of mutually-exclusive options following
 * Material Design 3 segmented button patterns. Replaces inline
 * button-group implementations across data views and forms.
 * Supports smooth, chevron-guided horizontal scroll navigation on overflow.
 */

export interface SegmentOption<T extends string = string> {
  /** The value used for selection tracking. */
  value: T;
  /** Display label (text or icon). */
  label: React.ReactNode;
}

export interface M3SegmentedButtonProps<T extends string = string> {
  /** Available options. */
  options: SegmentOption<T>[];
  /** Currently selected value. */
  value: T;
  /** Called when the user selects an option. */
  onChange: (value: T) => void;
  /** Size variant. @default "md" */
  size?: 'sm' | 'md';
  /** Extra Tailwind classes on the container. */
  className?: string;
}

const SIZE_CLASSES: Record<'sm' | 'md', string> = {
  sm: 'p-1.5 text-[10px]',
  md: 'p-2 text-[11px]',
} as const;

export function M3SegmentedButton<T extends string = string>({
  options,
  value,
  onChange,
  size = 'md',
  className = '',
}: M3SegmentedButtonProps<T>) {
  const containerRef = useRef<HTMLDivElement>(null);
  const [showLeftArrow, setShowLeftArrow] = useState(false);
  const [showRightArrow, setShowRightArrow] = useState(false);

  const checkScroll = () => {
    const container = containerRef.current;
    if (container) {
      const { scrollLeft, scrollWidth, clientWidth } = container;
      // Show left arrow if scrolled right at least 2px
      setShowLeftArrow(scrollLeft > 2);
      // Show right arrow if there is still space to scroll right
      setShowRightArrow(scrollLeft + clientWidth < scrollWidth - 2);
    }
  };

  useEffect(() => {
    const container = containerRef.current;
    if (!container) return;

    // Run initially
    checkScroll();

    // Set up ResizeObserver to handle screen resizing or option changes
    const resizeObserver = new ResizeObserver(() => {
      checkScroll();
    });
    resizeObserver.observe(container);

    // Set up scroll event listener
    container.addEventListener('scroll', checkScroll);

    return () => {
      resizeObserver.disconnect();
      container.removeEventListener('scroll', checkScroll);
    };
  }, [options]);

  const handleScroll = (direction: 'left' | 'right') => {
    const container = containerRef.current;
    if (container) {
      const scrollAmount = direction === 'left' ? -200 : 200;
      container.scrollBy({ left: scrollAmount, behavior: 'smooth' });
    }
  };

  return (
    <div className="relative w-full flex items-center group/seg-btn">
      {/* Left scroll overlay & button */}
      {showLeftArrow && (
        <div className="absolute left-0 top-0 bottom-0 z-10 flex items-center pr-6 bg-gradient-to-r from-m3-surface-container via-m3-surface-container/90 to-transparent rounded-l-lg pointer-events-none">
          <button
            type="button"
            onClick={() => handleScroll('left')}
            className="pointer-events-auto h-7 w-7 flex items-center justify-center rounded-full bg-m3-surface border border-m3-outline/25 text-m3-secondary hover:text-m3-primary hover:bg-m3-primary/10 shadow-sm transition-all focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-m3-primary ml-1"
            aria-label="Desplazar a la izquierda"
            title="Desplazar a la izquierda"
          >
            <ChevronLeft className="w-4 h-4" />
          </button>
        </div>
      )}

      {/* Main scrollable container */}
      <div
        ref={containerRef}
        className={`flex flex-nowrap items-center gap-1 bg-m3-surface-container rounded-lg p-1 border border-m3-outline/25 overflow-x-auto overflow-y-hidden no-scrollbar w-full scroll-smooth ${className}`}
      >
        {options.map(opt => {
          const isActive = opt.value === value;
          return (
            <button
              key={opt.value}
              type="button"
              onClick={() => onChange(opt.value)}
              className={[
                `${SIZE_CLASSES[size]} rounded-md transition-all duration-200 flex items-center justify-center whitespace-nowrap shrink-0`,
                isActive
                  ? 'bg-m3-primary text-m3-on-primary shadow-sm'
                  : 'text-m3-secondary hover:bg-m3-primary/10',
              ].join(' ')}
            >
              {opt.label}
            </button>
          );
        })}
      </div>

      {/* Right scroll overlay & button */}
      {showRightArrow && (
        <div className="absolute right-0 top-0 bottom-0 z-10 flex items-center pl-6 bg-gradient-to-l from-m3-surface-container via-m3-surface-container/90 to-transparent rounded-r-lg pointer-events-none">
          <button
            type="button"
            onClick={() => handleScroll('right')}
            className="pointer-events-auto h-7 w-7 flex items-center justify-center rounded-full bg-m3-surface border border-m3-outline/25 text-m3-secondary hover:text-m3-primary hover:bg-m3-primary/10 shadow-sm transition-all focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-m3-primary mr-1"
            aria-label="Desplazar a la derecha"
            title="Desplazar a la derecha"
          >
            <ChevronRight className="w-4 h-4" />
          </button>
        </div>
      )}
    </div>
  );
}
