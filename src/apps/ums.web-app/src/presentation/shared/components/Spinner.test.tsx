import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { Spinner } from './Spinner';

describe('Spinner', () => {
  it('renders an SVG element', () => {
    render(<Spinner />);
    const svg = document.querySelector('svg');
    expect(svg).toBeInTheDocument();
  });

  it('has animate-spin class', () => {
    render(<Spinner />);
    const svg = document.querySelector('svg');
    expect(svg).toHaveClass('animate-spin');
  });

  it('uses default className', () => {
    render(<Spinner />);
    const svg = document.querySelector('svg');
    expect(svg).toHaveClass('w-4', 'h-4');
  });

  it('applies custom className', () => {
    render(<Spinner className="w-8 h-8 text-blue-500" />);
    const svg = document.querySelector('svg');
    expect(svg).toHaveClass('w-8', 'h-8', 'text-blue-500');
  });

  it('has correct viewBox', () => {
    render(<Spinner />);
    const svg = document.querySelector('svg');
    expect(svg).toHaveAttribute('viewBox', '0 0 24 24');
  });
});
