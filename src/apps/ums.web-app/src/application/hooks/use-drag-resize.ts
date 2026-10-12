/**
 * use-drag-resize.ts — Hook for draggable panel resize with collapse/expand.
 *
 * M-10: Extracted from M3DataView to enforce Single Responsibility Principle.
 */
import { useState, useRef, useCallback, useEffect } from 'react';

interface UseDragResizeOptions {
  minSize?: number;
  maxSizeRatio?: number;
  initialSize?: number | null;
}

interface UseDragResizeResult {
  size: number | null;
  isCollapsed: boolean;
  isDragging: boolean;
  containerRef: React.RefObject<HTMLDivElement | null>;
  resizableRef: React.RefObject<HTMLDivElement | null>;
  handleMouseDown: (e: React.MouseEvent) => void;
  handleTouchStart: (e: React.TouchEvent) => void;
  handleKeyDown: (e: React.KeyboardEvent<HTMLDivElement>) => void;
  toggleCollapse: () => void;
}

export function useDragResize({
  minSize = 0,
  maxSizeRatio = 0.55,
  initialSize = null,
}: UseDragResizeOptions = {}): UseDragResizeResult {
  const [size, setSize] = useState<number | null>(initialSize);
  const [isCollapsed, setIsCollapsed] = useState(false);
  const [isDragging, setIsDragging] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);
  const resizableRef = useRef<HTMLDivElement>(null);
  const isDraggingRef = useRef(false);
  const prevSizeRef = useRef<number | null>(null);
  const cleanupDragRef = useRef<(() => void) | null>(null);

  useEffect(() => {
    return () => {
      cleanupDragRef.current?.();
    };
  }, []);

  // Aplica el movimiento a partir de una coordenada Y (mouse o touch).
  const applyMove = useCallback(
    (clientY: number) => {
      if (!isDraggingRef.current || !containerRef.current) return;
      const titleBarH = (containerRef.current.firstElementChild as HTMLElement)?.offsetHeight ?? 60;
      const rect = containerRef.current.getBoundingClientRect();
      const fromTop = clientY - rect.top - titleBarH;
      const clamped = Math.min(rect.height * maxSizeRatio, Math.max(minSize, fromTop));
      setSize(clamped);
      prevSizeRef.current = clamped;
      if (clamped > 4) setIsCollapsed(false);
    },
    [minSize, maxSizeRatio]
  );

  // Núcleo de arrastre común a puntero fino (mouse) y grueso (touch).
  const beginDrag = useCallback(() => {
    const measured = resizableRef.current?.offsetHeight ?? 200;
    setSize(prev => prev ?? measured);
    isDraggingRef.current = true;
    setIsDragging(true);
  }, [setSize]);

  const handleMouseDown = useCallback(
    (e: React.MouseEvent) => {
      e.preventDefault();
      beginDrag();

      const onMouseMove = (ev: MouseEvent) => applyMove(ev.clientY);

      const onMouseUp = () => {
        isDraggingRef.current = false;
        setIsDragging(false);
        cleanupDragRef.current = null;
        window.removeEventListener('mousemove', onMouseMove);
        window.removeEventListener('mouseup', onMouseUp);
      };

      window.addEventListener('mousemove', onMouseMove);
      window.addEventListener('mouseup', onMouseUp);

      cleanupDragRef.current = onMouseUp;
    },
    [beginDrag, applyMove]
  );

  const handleTouchStart = useCallback(
    (e: React.TouchEvent) => {
      if (e.touches.length !== 1) return;
      beginDrag();

      const onTouchMove = (ev: TouchEvent) => {
        if (ev.cancelable) ev.preventDefault(); // evita el scroll mientras se arrastra
        const touch = ev.touches[0];
        if (touch) applyMove(touch.clientY);
      };

      const onTouchEnd = () => {
        isDraggingRef.current = false;
        setIsDragging(false);
        cleanupDragRef.current = null;
        window.removeEventListener('touchmove', onTouchMove);
        window.removeEventListener('touchend', onTouchEnd);
        window.removeEventListener('touchcancel', onTouchEnd);
      };

      window.addEventListener('touchmove', onTouchMove, { passive: false });
      window.addEventListener('touchend', onTouchEnd);
      window.addEventListener('touchcancel', onTouchEnd);

      cleanupDragRef.current = onTouchEnd;
    },
    [beginDrag, applyMove]
  );

  const toggleCollapse = useCallback(() => {
    if (isCollapsed) {
      const restore = prevSizeRef.current ?? size ?? resizableRef.current?.offsetHeight ?? 200;
      setSize(restore);
      setIsCollapsed(false);
    } else {
      const current = size ?? resizableRef.current?.offsetHeight ?? 200;
      prevSizeRef.current = current;
      setSize(0);
      setIsCollapsed(true);
    }
  }, [isCollapsed, size]);

  const handleKeyDown = useCallback(
    (e: React.KeyboardEvent<HTMLDivElement>) => {
      if (e.key === 'Enter' || e.key === ' ') {
        e.preventDefault();
        toggleCollapse();
        return;
      }

      if (e.key !== 'ArrowUp' && e.key !== 'ArrowDown') return;
      e.preventDefault();
      const measured = resizableRef.current?.offsetHeight ?? 200;
      const current = size ?? measured;
      const containerHeight = containerRef.current?.getBoundingClientRect().height ?? 600;
      const delta = e.key === 'ArrowDown' ? 24 : -24;
      const next = Math.min(containerHeight * maxSizeRatio, Math.max(minSize, current + delta));
      setSize(next);
      prevSizeRef.current = next;
      setIsCollapsed(next <= 4);
    },
    [toggleCollapse, size, minSize, maxSizeRatio]
  );

  return {
    size,
    isCollapsed,
    isDragging,
    containerRef,
    resizableRef,
    handleMouseDown,
    handleTouchStart,
    handleKeyDown,
    toggleCollapse,
  };
}
