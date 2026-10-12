import React, { useEffect, useRef } from 'react';
import { ShieldAlert } from 'lucide-react';
import { M3Card } from './M3Card';
import { M3Button } from './M3Button';
import { useFocusTrap } from '@app/hooks/use-focus-trap';

export interface M3DialogAction {
  label: string;
  variant?: 'filled' | 'tonal' | 'outlined' | 'text';
  className?: string;
  onClick: () => void;
}

export interface M3DialogProps {
  open: boolean;
  title: string;
  message?: string;
  icon?: React.ReactNode | null;
  iconColor?: string;
  actions?: M3DialogAction[];
  onScrimClick?: () => void;
  children?: React.ReactNode;
}

export const M3Dialog: React.FC<M3DialogProps> = React.memo(
  ({
    open,
    title,
    message,
    icon,
    iconColor = 'bg-amber-500/15 text-amber-500',
    actions,
    onScrimClick,
    children,
  }) => {
    const dialogRef = useRef<HTMLDivElement | null>(null);
    const { containerRef: focusTrapRef } = useFocusTrap({
      active: open,
      onEscape: onScrimClick,
    });

    useEffect(() => {
      if (open && dialogRef.current) {
        dialogRef.current.focus();
      }
    }, [open]);

    if (!open) return null;

    const iconNode =
      icon === null
        ? null
        : (icon ?? (
            <div className={`p-2 rounded-lg flex-shrink-0 ${iconColor}`}>
              <ShieldAlert className="w-5 h-5" aria-hidden="true" />
            </div>
          ));

    return (
      <div
        ref={focusTrapRef}
        className="fixed inset-0 z-[60] flex items-center justify-center bg-black/50 backdrop-blur-sm"
        onClick={e => {
          if (e.target === e.currentTarget) onScrimClick?.();
        }}
        role="dialog"
        aria-modal="true"
        aria-labelledby="m3-dialog-title"
        aria-describedby={message ? 'm3-dialog-message' : undefined}
      >
        <M3Card
          ref={dialogRef}
          variant="elevated"
          className={`p-6 w-full mx-4 border border-m3-outline/30 shadow-2xl animate-fadeIn outline-none ${children ? 'max-w-md' : 'max-w-sm space-y-4'}`}
          tabIndex={-1}
        >
          {children ? (
            <>
              <div className="flex items-center justify-between mb-4">
                <div className="flex items-center gap-2">
                  {iconNode}
                  <h3 id="m3-dialog-title" className="text-sm font-semibold text-m3-on-surface">
                    {title}
                  </h3>
                </div>
                {onScrimClick && (
                  <button
                    onClick={onScrimClick}
                    className="p-1 rounded hover:bg-m3-surface-variant text-m3-secondary hover:text-m3-on-surface transition-colors"
                    aria-label="Close"
                  >
                    <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path
                        strokeLinecap="round"
                        strokeLinejoin="round"
                        strokeWidth={2}
                        d="M6 18L18 6M6 6l12 12"
                      />
                    </svg>
                  </button>
                )}
              </div>
              {children}
              {actions && actions.length > 0 && (
                <div className="flex gap-2.5 pt-4" role="group" aria-label="Dialog actions">
                  {actions.map(action => (
                    <M3Button
                      key={action.label}
                      variant={action.variant ?? 'outlined'}
                      onClick={action.onClick}
                      className={`flex-1 ${action.className ?? ''}`}
                    >
                      {action.label}
                    </M3Button>
                  ))}
                </div>
              )}
            </>
          ) : (
            <>
              <div className="flex items-start gap-3">
                {iconNode}
                <div className="flex-1">
                  <div className="flex items-center justify-between">
                    <h3 id="m3-dialog-title" className="text-sm font-semibold text-m3-on-surface">
                      {title}
                    </h3>
                    {onScrimClick && (
                      <button
                        onClick={onScrimClick}
                        className="p-1 rounded hover:bg-m3-surface-variant text-m3-secondary hover:text-m3-on-surface transition-colors"
                        aria-label="Close"
                      >
                        <svg
                          className="w-4 h-4"
                          fill="none"
                          viewBox="0 0 24 24"
                          stroke="currentColor"
                        >
                          <path
                            strokeLinecap="round"
                            strokeLinejoin="round"
                            strokeWidth={2}
                            d="M6 18L18 6M6 6l12 12"
                          />
                        </svg>
                      </button>
                    )}
                  </div>
                  {message && (
                    <p
                      id="m3-dialog-message"
                      className="text-xs text-m3-secondary mt-1 leading-relaxed"
                    >
                      {message}
                    </p>
                  )}
                </div>
              </div>

              {actions && actions.length > 0 && (
                <div className="flex gap-2.5 pt-1" role="group" aria-label="Dialog actions">
                  {actions.map(action => (
                    <M3Button
                      key={action.label}
                      variant={action.variant ?? 'outlined'}
                      onClick={action.onClick}
                      className={`flex-1 ${action.className ?? ''}`}
                    >
                      {action.label}
                    </M3Button>
                  ))}
                </div>
              )}
            </>
          )}
        </M3Card>
      </div>
    );
  }
);
