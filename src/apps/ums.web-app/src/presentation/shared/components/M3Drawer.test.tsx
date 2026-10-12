import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { M3Drawer } from './M3Drawer';

describe('M3Drawer', () => {
  const defaultProps = {
    open: true,
    onClose: vi.fn(),
    title: 'Drawer Title',
    children: <div>Drawer content</div>,
  };

  it('renders when open is true', () => {
    render(<M3Drawer {...defaultProps} />);
    expect(screen.getByText('Drawer Title')).toBeInTheDocument();
    expect(screen.getByText('Drawer content')).toBeInTheDocument();
  });

  it('does not render when open is false', () => {
    render(<M3Drawer {...defaultProps} open={false} />);
    expect(screen.queryByText('Drawer Title')).not.toBeInTheDocument();
  });

  it('calls onClose when close button is clicked', async () => {
    const user = userEvent.setup();
    const onClose = vi.fn();

    render(<M3Drawer {...defaultProps} onClose={onClose} />);

    await user.click(screen.getByLabelText('Close drawer'));
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('renders subtitle when provided', () => {
    render(<M3Drawer {...defaultProps} subtitle="Drawer subtitle" />);
    expect(screen.getByText('Drawer subtitle')).toBeInTheDocument();
  });

  it('renders actions when provided', () => {
    render(<M3Drawer {...defaultProps} actions={<button>Action</button>} />);
    expect(screen.getByText('Action')).toBeInTheDocument();
  });

  it('has correct ARIA attributes', () => {
    render(<M3Drawer {...defaultProps} />);
    const dialog = screen.getByRole('dialog');
    expect(dialog).toHaveAttribute('aria-modal', 'true');
    expect(dialog).toHaveAttribute('aria-label', 'Drawer Title');
  });

  it('renders children content', () => {
    render(
      <M3Drawer {...defaultProps}>
        <div data-testid="child-content">Nested child</div>
      </M3Drawer>
    );
    expect(screen.getByTestId('child-content')).toBeInTheDocument();
  });
});
