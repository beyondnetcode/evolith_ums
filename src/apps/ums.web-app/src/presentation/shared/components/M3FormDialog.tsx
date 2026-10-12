import { useFocusTrap } from '@app/hooks/use-focus-trap';
import React from 'react';
import { X } from 'lucide-react';

/**
 * M3FormDialog — centred modal optimised for form content.
 *
 * Unlike M3Dialog (which shows a title + message + action buttons),
 * M3FormDialog provides a header bar, a scrollable form body via `children`,
 * and an action footer. Designed for "Create / Edit entity" flows.
 */

export interface M3FormDialogProps {
  /** Controls visibility. */
  open: boolean;
  /** Called when the user dismisses the dialog. */
  onClose: () => void;
  /** Title shown in the header. */
  title: string;
  /** Optional icon rendered before the title. */
  icon?: React.ReactNode;
  /** Max width Tailwind class. @default "max-w-lg" */
  maxWidth?: string;
  /** Form body (fields). */
  children: React.ReactNode;
  /** Footer actions (buttons). */
  footer: React.ReactNode;
}

export const M3FormDialog: React.FC<M3FormDialogProps> = ({
  open,
  onClose,
  title,
  icon,
  maxWidth = 'max-w-lg',
  children,
  footer,
}) => {
  // No era un diálogo: sin `role`, sin `aria-modal`, sin nombre y sin gestión de foco, aunque lo
  // consumen catorce pantallas —todos los formularios de alta y edición—. Para un lector de pantalla
  // era un `div` más en medio del documento, y con el tabulador se podía salir de él sin cerrarlo.
  // El hook de foco ya existía; lo único que faltaba era enchufarlo (P-UX-08 de ADR-0110).
  const idTitulo = React.useId();
  const { containerRef } = useFocusTrap({ active: open, onEscape: onClose });

  if (!open) return null;

  return (
    <div className="fixed inset-0 z-50 overflow-hidden flex items-center justify-center p-4 select-none">
      {/* Scrim */}
      <div className="absolute inset-0 bg-m3-scrim/45 backdrop-blur-sm" onClick={onClose} />

      <div
        ref={containerRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby={idTitulo}
        className={`bg-m3-surface border border-m3-outline/25 w-full ${maxWidth} rounded-xl overflow-hidden shadow-2xl z-10`}
      >
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-m3-outline/20">
          <div className="flex items-center gap-2 text-m3-primary">
            {icon}
            <h2 id={idTitulo} className="md-title-medium text-m3-on-surface">
              {title}
            </h2>
          </div>
          <button
            type="button"
            onClick={onClose}
            // Un botón de solo icono sin nombre accesible se anuncia como «botón» y nada más. Y el
            // objetivo táctil sube a 48 dp con área de impacto, sin engordar el icono.
            aria-label="Cerrar"
            className="relative p-2 rounded-full hover:bg-m3-primary/10 text-m3-on-surface-variant transition-colors after:absolute after:left-1/2 after:top-1/2 after:h-12 after:w-12 after:-translate-x-1/2 after:-translate-y-1/2 after:content-['']"
          >
            <X className="w-4 h-4" />
          </button>
        </div>

        {/* Body */}
        <div className="p-6 space-y-0">{children}</div>

        {/* Footer */}
        <div className="flex justify-end gap-3 px-6 pb-6 pt-0 border-t border-m3-outline/10 mt-0">
          {footer}
        </div>
      </div>
    </div>
  );
};
