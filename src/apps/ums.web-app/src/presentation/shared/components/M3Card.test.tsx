import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { M3Card } from './M3Card';

describe('M3Card', () => {
  it('renders children', () => {
    render(<M3Card>Card Content</M3Card>);
    expect(screen.getByText('Card Content')).toBeInTheDocument();
  });

  it('applies elevated variant by default', () => {
    const { container } = render(<M3Card>Content</M3Card>);
    expect(container.firstChild).toHaveClass('elevation-1');
  });

  it('applies filled variant', () => {
    const { container } = render(<M3Card variant="filled">Content</M3Card>);
    expect(container.firstChild).toHaveClass('bg-m3-surface-container/50');
  });

  it('applies outlined variant', () => {
    const { container } = render(<M3Card variant="outlined">Content</M3Card>);
    expect(container.firstChild).toHaveClass('border-m3-outline');
  });

  it('applies hover styles when hoverable is true', () => {
    const { container } = render(<M3Card hoverable>Content</M3Card>);
    expect(container.firstChild).toHaveClass('hover:-translate-y-1');
  });

  it('does not apply hover styles when hoverable is false', () => {
    const { container } = render(<M3Card hoverable={false}>Content</M3Card>);
    expect(container.firstChild).not.toHaveClass('hover:-translate-y-1');
  });

  it('applies custom className', () => {
    const { container } = render(<M3Card className="custom-class">Content</M3Card>);
    expect(container.firstChild).toHaveClass('custom-class');
  });

  it('forwards ref', () => {
    const ref = { current: null };
    render(<M3Card ref={ref as any}>Content</M3Card>);
    expect(ref.current).not.toBeNull();
  });

  it('spreads additional props', () => {
    const { container } = render(<M3Card data-testid="card">Content</M3Card>);
    expect(container.firstChild).toHaveAttribute('data-testid', 'card');
  });
});
