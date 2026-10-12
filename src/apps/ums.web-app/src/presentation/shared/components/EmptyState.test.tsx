import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { EmptyState } from './EmptyState';
import * as M3CardModule from './M3Card';
import * as TooltipModule from './Tooltip';

vi.mock('./M3Card', () => ({
  M3Card: ({ children, className }: any) => (
    <div data-testid="m3-card" className={className}>
      {children}
    </div>
  ),
}));
vi.mock('./Tooltip', () => ({
  Tooltip: ({ children, content }: any) => <div data-tooltip={content}>{children}</div>,
}));

describe('EmptyState', () => {
  it('renders message', () => {
    render(<EmptyState message="No items found" />);
    expect(screen.getByText('No items found')).toBeInTheDocument();
  });

  it('renders with dashed variant by default', () => {
    render(<EmptyState message="No items" />);
    expect(screen.getByText('No items')).toBeInTheDocument();
  });

  it('renders card variant with M3Card', () => {
    render(<EmptyState message="No items" variant="card" />);
    expect(screen.getByTestId('m3-card')).toBeInTheDocument();
  });

  it('renders title in card variant', () => {
    render(<EmptyState message="No items" variant="card" title="Empty List" />);
    expect(screen.getByText('Empty List')).toBeInTheDocument();
  });

  it('renders custom icon', () => {
    render(<EmptyState message="No items" icon={<span data-testid="custom-icon">X</span>} />);
    expect(screen.getByTestId('custom-icon')).toBeInTheDocument();
  });

  it('hides icon when icon is null', () => {
    const { container } = render(<EmptyState message="No items" icon={null} />);
    expect(container.querySelector('.w-12')).not.toBeInTheDocument();
  });

  it('renders tooltip when provided in card variant', () => {
    const { container } = render(
      <EmptyState message="No items" variant="card" title="Empty" tooltip="Help info" />
    );
    expect(container.querySelector('[data-tooltip="Help info"]')).toBeInTheDocument();
  });

  it('applies custom className', () => {
    const { container } = render(<EmptyState message="No items" className="custom-class" />);
    expect(container.firstChild).toHaveClass('custom-class');
  });

  it('renders default Info icon for dashed variant', () => {
    const { container } = render(<EmptyState message="No items" />);
    expect(container.querySelector('svg')).toBeInTheDocument();
  });

  it('renders default Info icon for card variant', () => {
    render(<EmptyState message="No items" variant="card" />);
    expect(screen.getByTestId('m3-card')).toBeInTheDocument();
  });
});
