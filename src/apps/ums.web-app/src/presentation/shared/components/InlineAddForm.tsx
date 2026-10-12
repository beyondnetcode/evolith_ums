import React from 'react';
import { Plus, X } from 'lucide-react';
import { M3Card } from './M3Card';
import { M3Button } from './M3Button';
import { IconButton } from './Tooltip';

/**
 * InlineAddForm — collapsible card for adding new entities inline.
 *
 * When closed, renders a trigger button (or hidden if triggerEmphasis='none').
 * When open, renders an outlined card with header, children (form fields),
 * and a submit button aligned to the bottom right.
 */

export interface InlineAddFormProps {
  /** Whether the form is currently visible. */
  isOpen: boolean;
  /** Toggle visibility. */
  onToggle: (open: boolean) => void;
  /** Form submit handler (receives the form event). */
  onSubmit: (e: React.FormEvent) => void;
  /** Label shown on the closed-state button. Use "+" for icon-only. Use "" to hide trigger. */
  addLabel: string;
  /** Title shown in the open-state header. */
  title: string;
  /** Cancel tooltip label. */
  cancelLabel?: string;
  /** Submit button label. */
  submitLabel: string;
  /** Whether the submit is in progress. */
  isLoading?: boolean;
  /** Optional icon next to the title (defaults to Plus). */
  icon?: React.ReactNode;
  /** Optional error message displayed above the form fields. */
  error?: string;
  /** Form field children. */
  children: React.ReactNode;
  /** Visual emphasis for the closed-state trigger button.
   * 'normal' = full button, 'quiet' = smaller pill, 'none' = hidden (use external trigger) */
  triggerEmphasis?: 'normal' | 'quiet' | 'none';
  /** Whether the closed-state trigger should span the full width. */
  fullWidth?: boolean;
}

const isIconOnly = (label: string) => label.trim() === '+';

export const InlineAddForm: React.FC<InlineAddFormProps> = ({
  isOpen,
  onToggle,
  onSubmit,
  addLabel,
  title,
  cancelLabel = 'Cancelar',
  submitLabel,
  isLoading = false,
  icon,
  error,
  children,
  triggerEmphasis = 'normal',
  fullWidth = false,
}) => {
  const iconOnly = isIconOnly(addLabel);

  if (!isOpen) {
    // No trigger when triggerEmphasis='none' - rely on external button (e.g., ListToolbar)
    if (triggerEmphasis === 'none') {
      return null;
    }

    if (triggerEmphasis === 'quiet') {
      return (
        <div className="flex justify-start my-1 animate-fadeIn">
          <button
            type="button"
            onClick={() => onToggle(true)}
            className={`inline-flex items-center justify-center h-7 rounded-full
              text-m3-primary border border-m3-primary/20 bg-m3-primary/5
              hover:bg-m3-primary/10 hover:border-m3-primary/30
              transition-all select-none focus-visible:outline-none focus-visible:ring-2
              focus-visible:ring-m3-primary active:scale-[0.97] ${iconOnly ? 'w-7' : 'gap-1.5 px-3'}`}
            title={title}
          >
            <Plus className="w-3.5 h-3.5" />
            {!iconOnly && (
              <span className="text-[10px] font-bold uppercase tracking-wider">{addLabel}</span>
            )}
          </button>
        </div>
      );
    }

    return (
      <div className={`flex ${fullWidth ? 'w-full' : 'justify-start'} my-1 animate-fadeIn`}>
        <button
          type="button"
          onClick={() => onToggle(true)}
          className={`inline-flex items-center justify-center rounded-full h-8
            bg-m3-primary/10 border border-m3-primary/30 text-m3-primary
            hover:bg-m3-primary/20 hover:border-m3-primary/50 hover:shadow-sm
            transition-all focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-m3-primary
            active:scale-[0.97] ${iconOnly ? 'w-8' : 'px-3 gap-1.5'}`}
          title={title}
        >
          <Plus className="w-4 h-4" />
          {!iconOnly && <span className="text-[11px] font-semibold">{addLabel}</span>}
        </button>
      </div>
    );
  }

  return (
    <M3Card
      variant="outlined"
      className="p-4 rounded-xl bg-m3-surface-container/10 border-m3-primary/20 animate-fadeIn"
    >
      <div className="flex justify-between items-center pb-3 mb-3 border-b border-m3-outline/15">
        <h4 className="text-[11px] font-semibold uppercase tracking-wide text-m3-primary flex items-center gap-1.5">
          {icon ?? <Plus className="w-3.5 h-3.5" />}
          <span className="normal-case font-normal text-xs">{title}</span>
        </h4>
        <IconButton tooltip={cancelLabel} onClick={() => onToggle(false)}>
          <X className="w-3.5 h-3.5" />
        </IconButton>
      </div>

      {error && (
        <div className="p-2.5 mb-3 rounded-lg bg-m3-error/10 border border-m3-error/20 text-m3-error text-[10px] font-medium">
          {error}
        </div>
      )}

      <form onSubmit={onSubmit} className="space-y-3">
        {children}
        <div className="flex justify-end gap-2 pt-2 border-t border-m3-outline/10">
          <button
            type="button"
            onClick={() => onToggle(false)}
            className="h-8 px-4 rounded-md text-[11px] font-medium text-m3-secondary
              hover:bg-m3-surface-variant transition-colors"
          >
            {cancelLabel}
          </button>
          <button
            type="submit"
            disabled={isLoading}
            className="h-8 px-4 rounded-full bg-m3-primary text-white text-[11px] font-medium
              hover:bg-m3-primary/90 disabled:opacity-50 transition-colors flex items-center gap-1.5"
          >
            {isLoading ? (
              <>
                <div className="w-3 h-3 border-2 border-white/30 border-t-white rounded-full animate-spin" />
                <span>Guardando...</span>
              </>
            ) : (
              submitLabel
            )}
          </button>
        </div>
      </form>
    </M3Card>
  );
};
