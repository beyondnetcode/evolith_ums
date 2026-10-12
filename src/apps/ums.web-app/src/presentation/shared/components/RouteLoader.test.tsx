import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { RouteLoader } from './RouteLoader';

describe('RouteLoader', () => {
  it('renders loading spinner', () => {
    render(<RouteLoader />);
    const spinner = document.querySelector('.animate-spin');
    expect(spinner).toBeInTheDocument();
  });

  it('has aria-live region for accessibility', () => {
    render(<RouteLoader />);
    const status = screen.getByRole('status');
    expect(status).toHaveAttribute('aria-live', 'polite');
  });

  it('displays loading text', () => {
    render(<RouteLoader />);
    expect(screen.getByText('Loading...')).toBeInTheDocument();
  });
});
