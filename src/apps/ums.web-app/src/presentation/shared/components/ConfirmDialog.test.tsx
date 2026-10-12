import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';
import { ConfirmDialog } from './ConfirmDialog';

describe('ConfirmDialog', () => {
  it('renders when open is true', () => {
    render(
      <ConfirmDialog
        open
        title="Confirm Action"
        message="Are you sure?"
        confirmLabel="Yes"
        cancelLabel="No"
        onConfirm={() => {}}
        onCancel={() => {}}
      />
    );
    expect(screen.getByText('Confirm Action')).toBeInTheDocument();
    expect(screen.getByText('Are you sure?')).toBeInTheDocument();
  });

  it('does not render when open is false', () => {
    render(
      <ConfirmDialog
        open={false}
        title="Confirm Action"
        message="Are you sure?"
        confirmLabel="Yes"
        cancelLabel="No"
        onConfirm={() => {}}
        onCancel={() => {}}
      />
    );
    expect(screen.queryByText('Confirm Action')).not.toBeInTheDocument();
  });

  it('renders confirm and cancel labels', () => {
    render(
      <ConfirmDialog
        open
        title="Delete"
        message="Delete this item?"
        confirmLabel="Delete"
        cancelLabel="Cancel"
        onConfirm={() => {}}
        onCancel={() => {}}
      />
    );
    expect(screen.getByRole('button', { name: 'Delete' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Cancel' })).toBeInTheDocument();
  });

  it('calls onConfirm when confirm button is clicked', async () => {
    const handleConfirm = vi.fn();
    const user = userEvent.setup();
    render(
      <ConfirmDialog
        open
        title="Confirm"
        message="Proceed?"
        confirmLabel="Yes"
        cancelLabel="No"
        onConfirm={handleConfirm}
        onCancel={() => {}}
      />
    );

    await user.click(screen.getByText('Yes'));
    expect(handleConfirm).toHaveBeenCalled();
  });

  it('calls onCancel when cancel button is clicked', async () => {
    const handleCancel = vi.fn();
    const user = userEvent.setup();
    render(
      <ConfirmDialog
        open
        title="Confirm"
        message="Proceed?"
        confirmLabel="Yes"
        cancelLabel="No"
        onConfirm={() => {}}
        onCancel={handleCancel}
      />
    );

    await user.click(screen.getByText('No'));
    expect(handleCancel).toHaveBeenCalled();
  });

  it('applies danger variant styling', () => {
    render(
      <ConfirmDialog
        open
        title="Delete"
        message="This cannot be undone"
        confirmLabel="Delete"
        cancelLabel="Cancel"
        onConfirm={() => {}}
        onCancel={() => {}}
        variant="danger"
      />
    );
    const confirmBtn = screen.getByRole('button', { name: 'Delete' });
    expect(confirmBtn.className).toContain('bg-m3-error');
  });

  it('applies warning variant styling', () => {
    render(
      <ConfirmDialog
        open
        title="Warning"
        message="Proceed with caution"
        confirmLabel="Continue"
        cancelLabel="Stop"
        onConfirm={() => {}}
        onCancel={() => {}}
        variant="warning"
      />
    );
    const confirmBtn = screen.getByText('Continue');
    expect(confirmBtn).toHaveClass('bg-amber-500');
  });

  it('uses primary variant by default', () => {
    render(
      <ConfirmDialog
        open
        title="Confirm"
        message="Proceed?"
        confirmLabel="Yes"
        cancelLabel="No"
        onConfirm={() => {}}
        onCancel={() => {}}
      />
    );
    const confirmBtn = screen.getByText('Yes');
    expect(confirmBtn).not.toHaveClass('bg-m3-error');
    expect(confirmBtn).not.toHaveClass('bg-amber-500');
  });
});
