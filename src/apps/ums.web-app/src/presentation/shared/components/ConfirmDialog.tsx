import React from 'react';
import { M3Dialog, M3DialogAction } from './M3Dialog';

export interface ConfirmDialogProps {
  /** True when the dialog should render. */
  open: boolean;
  /** Title of the dialog. */
  title: string;
  /** Description or warning message inside the dialog. */
  message: string;
  /** Label for the confirmation action. */
  confirmLabel: string;
  /** Label for the cancellation action. */
  cancelLabel: string;
  /** Callback fired when user confirms the dialog. */
  onConfirm: () => void;
  /** Callback fired when user cancels the dialog. */
  onCancel: () => void;
  /** Theme variation for actions. @default "primary" */
  variant?: 'primary' | 'danger' | 'warning';
  /** True if the confirmation action is currently performing work. */
  isLoading?: boolean;
}

export const ConfirmDialog: React.FC<ConfirmDialogProps> = React.memo(
  ({
    open,
    title,
    message,
    confirmLabel,
    cancelLabel,
    onConfirm,
    onCancel,
    variant = 'primary',
  }) => {
    const confirmClass =
      variant === 'danger'
        ? 'bg-m3-error hover:bg-m3-error/90 text-m3-on-error border-0'
        : variant === 'warning'
          ? 'bg-amber-500 hover:bg-amber-500/90 text-white border-0'
          : undefined;

    const actions: M3DialogAction[] = [
      {
        label: cancelLabel,
        variant: 'outlined',
        onClick: onCancel,
      },
      {
        label: confirmLabel,
        variant: 'filled',
        onClick: onConfirm,
        className: confirmClass,
      },
    ];

    const iconColor =
      variant === 'danger'
        ? 'bg-m3-error/15 text-m3-error'
        : variant === 'warning'
          ? 'bg-amber-500/15 text-amber-500'
          : 'bg-m3-primary/15 text-m3-primary';

    return (
      <M3Dialog
        open={open}
        title={title}
        message={message}
        iconColor={iconColor}
        actions={actions}
        onScrimClick={onCancel}
      />
    );
  }
);

ConfirmDialog.displayName = 'ConfirmDialog';
