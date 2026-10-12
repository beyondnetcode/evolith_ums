import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { CodeBadge } from './CodeBadge';

describe('CodeBadge', () => {
  it('renders the code text', () => {
    render(<CodeBadge code="TEN-001" />);
    expect(screen.getByText('TEN-001')).toBeInTheDocument();
  });

  it('renders with default sm size', () => {
    render(<CodeBadge code="CODE" />);
    const badge = screen.getByText('CODE');
    expect(badge).toHaveClass('text-[11px]');
  });

  it('renders with xs size', () => {
    render(<CodeBadge code="CODE" size="xs" />);
    const badge = screen.getByText('CODE');
    expect(badge).toHaveClass('text-[10px]');
  });

  it('applies custom className', () => {
    render(<CodeBadge code="CODE" className="custom-class" />);
    expect(screen.getByText('CODE')).toHaveClass('custom-class');
  });

  it('has monospace font', () => {
    render(<CodeBadge code="CODE" />);
    expect(screen.getByText('CODE')).toHaveClass('font-mono');
  });

  it('has rounded background', () => {
    render(<CodeBadge code="CODE" />);
    expect(screen.getByText('CODE')).toHaveClass('rounded');
  });
});
