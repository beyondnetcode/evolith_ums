import React from 'react';
import { X } from 'lucide-react';
import { useFocusTrap } from '@app/hooks/use-focus-trap';

export interface M3DrawerProps {
  open: boolean;
  onClose: () => void;
  title: string;
  subtitle?: React.ReactNode;
  actions?: React.ReactNode;
  maxWidth?: string;
  children: React.ReactNode;
}

export const M3Drawer: React.FC<M3DrawerProps> = React.memo(
  ({ open, onClose, title, subtitle, actions, maxWidth = 'max-w-md', children }) => {
    const { containerRef: focusTrapRef } = useFocusTrap({
      active: open,
      onEscape: onClose,
    });

    if (!open) return null;

    return (
      <div
        ref={focusTrapRef}
        className="fixed inset-0 z-50 overflow-hidden select-none"
        role="dialog"
        aria-modal="true"
        aria-label={title}
      >
        <div
          className="absolute inset-0 bg-black/40 backdrop-blur-sm transition-opacity"
          onClick={onClose}
          aria-hidden="true"
        />

        <div className="pointer-events-none fixed inset-y-0 right-0 flex max-w-full pl-10">
          <div className={`pointer-events-auto w-screen ${maxWidth}`}>
            <div className="flex h-full flex-col bg-m3-surface border-l border-m3-outline/25 shadow-2xl transition-all duration-300">
              <div className="flex items-center justify-between px-6 py-5 border-b border-m3-outline/30">
                <div>
                  <h2 className="text-base font-extrabold tracking-tight text-m3-on-surface">
                    {title}
                  </h2>
                  {subtitle && (
                    <div className="text-[10px] text-m3-primary font-bold tracking-wide uppercase mt-1">
                      {subtitle}
                    </div>
                  )}
                </div>
                <button
                  onClick={onClose}
                  className="p-2 rounded-full hover:bg-m3-primary/10 text-m3-secondary transition-colors"
                  aria-label="Close drawer"
                >
                  <X className="w-5 h-5" aria-hidden="true" />
                </button>
              </div>

              {actions && (
                <div className="flex gap-2 px-6 py-3 bg-m3-surface-container/30 border-b border-m3-outline/10 text-xs">
                  {actions}
                </div>
              )}

              <div className="flex-1 overflow-y-auto p-6 space-y-4">{children}</div>
            </div>
          </div>
        </div>
      </div>
    );
  }
);
