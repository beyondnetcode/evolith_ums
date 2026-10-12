import React, { useState, useRef, useEffect } from 'react';

interface TooltipProps {
  content: string;
  children: React.ReactNode;
  placement?: 'top' | 'bottom' | 'left' | 'right';
  delay?: number;
}

export const Tooltip: React.FC<TooltipProps> = React.memo(
  ({ content, children, placement = 'top', delay = 400 }) => {
    const [visible, setVisible] = useState(false);
    const [coords, setCoords] = useState({ top: 0, left: 0 });
    const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
    const triggerRef = useRef<HTMLSpanElement | null>(null);
    const tooltipRef = useRef<HTMLDivElement | null>(null);

    const show = () => {
      timerRef.current = setTimeout(() => setVisible(true), delay);
    };

    const hide = () => {
      if (timerRef.current) clearTimeout(timerRef.current);
      setVisible(false);
    };

    useEffect(() => {
      if (!visible || !triggerRef.current || !tooltipRef.current) return;
      const rect = triggerRef.current.getBoundingClientRect();
      const tip = tooltipRef.current.getBoundingClientRect();
      const gap = 6;
      let top = 0;
      let left = 0;

      if (placement === 'top') {
        top = rect.top - tip.height - gap;
        left = rect.left + rect.width / 2 - tip.width / 2;
      } else if (placement === 'bottom') {
        top = rect.bottom + gap;
        left = rect.left + rect.width / 2 - tip.width / 2;
      } else if (placement === 'left') {
        top = rect.top + rect.height / 2 - tip.height / 2;
        left = rect.left - tip.width - gap;
      } else {
        top = rect.top + rect.height / 2 - tip.height / 2;
        left = rect.right + gap;
      }

      left = Math.max(6, Math.min(left, window.innerWidth - tip.width - 6));
      top = Math.max(6, Math.min(top, window.innerHeight - tip.height - 6));

      setCoords({ top, left });
    }, [visible, placement]);

    useEffect(
      () => () => {
        if (timerRef.current) clearTimeout(timerRef.current);
      },
      []
    );

    if (!content) return <>{children}</>;

    return (
      <>
        <span
          ref={triggerRef}
          onMouseEnter={show}
          onMouseLeave={hide}
          onFocus={show}
          onBlur={hide}
          className="inline-flex"
          aria-describedby={visible ? 'tooltip-content' : undefined}
        >
          {children}
        </span>
        {visible && typeof document !== 'undefined' && (
          <div
            ref={tooltipRef}
            id="tooltip-content"
            role="tooltip"
            style={{ position: 'fixed', top: coords.top, left: coords.left, zIndex: 9999 }}
            className="pointer-events-none px-2.5 py-1.5 rounded-lg bg-m3-on-surface/90 text-m3-surface text-[10px] font-semibold tracking-wide shadow-lg whitespace-nowrap backdrop-blur-sm animate-tooltipIn"
          >
            {content}
          </div>
        )}
      </>
    );
  }
);

interface IconButtonProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  tooltip: string;
  placement?: 'top' | 'bottom' | 'left' | 'right';
  children: React.ReactNode;
}

export const IconButton: React.FC<IconButtonProps> = React.memo(
  ({ tooltip, placement = 'top', children, className = '', ...props }) => {
    return (
      <Tooltip content={tooltip} placement={placement}>
        <button
          // El tooltip es la única etiqueta visible del botón-icono; por defecto se usa como
          // nombre accesible (a11y) y para poder seleccionarlo en E2E por su rol/nombre (G-137).
          aria-label={props['aria-label'] ?? tooltip}
          className={`p-1.5 rounded-full transition-all text-m3-secondary hover:text-m3-primary hover:bg-m3-primary/10 ${className}`}
          {...props}
        >
          {children}
        </button>
      </Tooltip>
    );
  }
);
