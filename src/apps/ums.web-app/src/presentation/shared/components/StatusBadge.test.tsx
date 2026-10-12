import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { StatusBadge } from './StatusBadge';

describe('StatusBadge', () => {
  it('renders with default Active styling', () => {
    render(<StatusBadge status="Active" />);
    const badge = screen.getByText('Active');
    expect(badge).toBeInTheDocument();
    expect(badge).toHaveClass('text-emerald-500');
  });

  it('renders with Suspended styling', () => {
    render(<StatusBadge status="Suspended" />);
    const badge = screen.getByText('Suspended');
    expect(badge).toBeInTheDocument();
    expect(badge).toHaveClass('text-rose-500');
  });

  it('renders with custom label', () => {
    render(<StatusBadge status="Active" label="Activo" />);
    expect(screen.getByText('Activo')).toBeInTheDocument();
    expect(screen.queryByText('Active')).not.toBeInTheDocument();
  });

  it('renders with fallback styling for unknown status', () => {
    render(<StatusBadge status="Unknown" />);
    const badge = screen.getByText('Unknown');
    expect(badge).toBeInTheDocument();
    expect(badge).toHaveClass('text-m3-secondary');
  });

  it('applies custom className', () => {
    render(<StatusBadge status="Active" className="custom-class" />);
    expect(screen.getByText('Active')).toHaveClass('custom-class');
  });
});
