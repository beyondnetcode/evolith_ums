import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { useDragResize } from './use-drag-resize';

describe('useDragResize', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('initializes with null size', () => {
    const { result } = renderHook(() => useDragResize());

    expect(result.current.size).toBeNull();
  });

  it('initializes with custom initial size', () => {
    const { result } = renderHook(() => useDragResize({ initialSize: 300 }));

    expect(result.current.size).toBe(300);
  });

  it('starts not collapsed', () => {
    const { result } = renderHook(() => useDragResize());

    expect(result.current.isCollapsed).toBe(false);
  });

  it('starts not dragging', () => {
    const { result } = renderHook(() => useDragResize());

    expect(result.current.isDragging).toBe(false);
  });

  it('provides refs', () => {
    const { result } = renderHook(() => useDragResize());

    expect(result.current.containerRef).toBeDefined();
    expect(result.current.resizableRef).toBeDefined();
  });

  it('toggles collapse to collapsed state', () => {
    const { result } = renderHook(() => useDragResize({ initialSize: 300 }));

    act(() => {
      result.current.toggleCollapse();
    });

    expect(result.current.isCollapsed).toBe(true);
    expect(result.current.size).toBe(0);
  });

  it('toggles collapse to restore previous size', () => {
    const { result } = renderHook(() => useDragResize({ initialSize: 300 }));

    act(() => {
      result.current.toggleCollapse();
    });
    expect(result.current.isCollapsed).toBe(true);

    act(() => {
      result.current.toggleCollapse();
    });

    expect(result.current.isCollapsed).toBe(false);
    expect(result.current.size).toBe(300);
  });

  it('handles mouse down to start dragging', () => {
    const { result } = renderHook(() => useDragResize({ initialSize: 200 }));

    const mockEvent = {
      preventDefault: vi.fn(),
    } as unknown as React.MouseEvent;

    act(() => {
      result.current.handleMouseDown(mockEvent);
    });

    expect(mockEvent.preventDefault).toHaveBeenCalled();
    expect(result.current.isDragging).toBe(true);
  });

  it('handles touch start to start dragging', () => {
    const { result } = renderHook(() => useDragResize({ initialSize: 200 }));

    const mockEvent = {
      touches: [{ clientY: 100 }],
    } as unknown as React.TouchEvent;

    act(() => {
      result.current.handleTouchStart(mockEvent);
    });

    expect(result.current.isDragging).toBe(true);
  });

  it('ignores multi-touch gestures (solo un dedo redimensiona)', () => {
    const { result } = renderHook(() => useDragResize({ initialSize: 200 }));

    const mockEvent = {
      touches: [{ clientY: 100 }, { clientY: 200 }],
    } as unknown as React.TouchEvent;

    act(() => {
      result.current.handleTouchStart(mockEvent);
    });

    expect(result.current.isDragging).toBe(false);
  });

  it('handles keyboard Enter to toggle collapse', () => {
    const { result } = renderHook(() => useDragResize({ initialSize: 300 }));

    const mockEvent = {
      key: 'Enter',
      preventDefault: vi.fn(),
    } as unknown as React.KeyboardEvent<HTMLDivElement>;

    act(() => {
      result.current.handleKeyDown(mockEvent);
    });

    expect(mockEvent.preventDefault).toHaveBeenCalled();
    expect(result.current.isCollapsed).toBe(true);
  });

  it('handles keyboard Space to toggle collapse', () => {
    const { result } = renderHook(() => useDragResize({ initialSize: 300 }));

    const mockEvent = {
      key: ' ',
      preventDefault: vi.fn(),
    } as unknown as React.KeyboardEvent<HTMLDivElement>;

    act(() => {
      result.current.handleKeyDown(mockEvent);
    });

    expect(result.current.isCollapsed).toBe(true);
  });

  it('ignores non-resize keys', () => {
    const { result } = renderHook(() => useDragResize({ initialSize: 300 }));

    const mockEvent = {
      key: 'Escape',
      preventDefault: vi.fn(),
    } as unknown as React.KeyboardEvent<HTMLDivElement>;

    act(() => {
      result.current.handleKeyDown(mockEvent);
    });

    expect(mockEvent.preventDefault).not.toHaveBeenCalled();
  });

  it('uses default minSize of 0', () => {
    const { result } = renderHook(() => useDragResize());

    expect(result.current.size).toBeNull();
  });

  it('uses default maxSizeRatio of 0.55', () => {
    const { result } = renderHook(() => useDragResize());

    expect(result.current.isDragging).toBe(false);
  });
});
