import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { KeyValueRow } from './KeyValueRow';

describe('KeyValueRow', () => {
  it('renders label and value', () => {
    render(<KeyValueRow label="Status" value="Active" />);
    expect(screen.getByText('Status')).toBeInTheDocument();
    expect(screen.getByText('Active')).toBeInTheDocument();
  });

  it('shows bottom border by default', () => {
    render(<KeyValueRow label="Status" value="Active" />);
    const container = screen.getByText('Status').closest('div');
    expect(container).toHaveClass('border-b');
  });

  it('hides bottom border when bordered is false', () => {
    render(<KeyValueRow label="Status" value="Active" bordered={false} />);
    const container = screen.getByText('Status').closest('div');
    expect(container).not.toHaveClass('border-b');
  });

  it('applies custom className', () => {
    render(<KeyValueRow label="Status" value="Active" className="custom-class" />);
    const container = screen.getByText('Status').closest('div');
    expect(container).toHaveClass('custom-class');
  });

  it('renders icon when provided', () => {
    render(<KeyValueRow label="Status" value="Active" icon={<span data-testid="icon">X</span>} />);
    expect(screen.getByTestId('icon')).toBeInTheDocument();
  });

  it('renders ReactNode as value', () => {
    render(<KeyValueRow label="Status" value={<span data-testid="value-badge">Badge</span>} />);
    expect(screen.getByTestId('value-badge')).toHaveTextContent('Badge');
  });
});
