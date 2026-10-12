import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { M3Skeleton, M3SkeletonRow } from './M3Skeleton';

describe('M3Skeleton', () => {
  it('renders a div element', () => {
    render(<M3Skeleton />);
    const skeleton = document.querySelector('[aria-hidden="true"]');
    expect(skeleton).toBeInTheDocument();
  });

  it('has animate-pulse class', () => {
    render(<M3Skeleton />);
    const skeleton = document.querySelector('[aria-hidden="true"]');
    expect(skeleton).toHaveClass('animate-pulse');
  });

  it('uses text variant by default', () => {
    render(<M3Skeleton />);
    const skeleton = document.querySelector('[aria-hidden="true"]');
    expect(skeleton).toHaveClass('rounded-[4px]');
  });

  it('applies circular variant', () => {
    render(<M3Skeleton variant="circular" />);
    const skeleton = document.querySelector('[aria-hidden="true"]');
    expect(skeleton).toHaveClass('rounded-full');
  });

  it('applies rectangular variant', () => {
    render(<M3Skeleton variant="rectangular" />);
    const skeleton = document.querySelector('[aria-hidden="true"]');
    expect(skeleton).toHaveClass('rounded-none');
  });

  it('applies rounded variant', () => {
    render(<M3Skeleton variant="rounded" />);
    const skeleton = document.querySelector('[aria-hidden="true"]');
    expect(skeleton).toHaveClass('rounded-lg');
  });

  it('applies custom width', () => {
    render(<M3Skeleton width="200px" />);
    const skeleton = document.querySelector('[aria-hidden="true"]');
    expect(skeleton).toHaveStyle({ width: '200px' });
  });

  it('applies numeric width as px', () => {
    render(<M3Skeleton width={100} />);
    const skeleton = document.querySelector('[aria-hidden="true"]');
    expect(skeleton).toHaveStyle({ width: '100px' });
  });

  it('applies custom height', () => {
    render(<M3Skeleton height="50px" />);
    const skeleton = document.querySelector('[aria-hidden="true"]');
    expect(skeleton).toHaveStyle({ height: '50px' });
  });

  it('uses darker variant when specified', () => {
    render(<M3Skeleton darker />);
    const skeleton = document.querySelector('[aria-hidden="true"]');
    expect(skeleton.className).toContain('bg-m3-surface-container-high');
  });

  it('applies additional className', () => {
    render(<M3Skeleton className="custom-class" />);
    const skeleton = document.querySelector('[aria-hidden="true"]');
    expect(skeleton).toHaveClass('custom-class');
  });

  it('has aria-hidden attribute', () => {
    render(<M3Skeleton />);
    const skeleton = document.querySelector('[aria-hidden="true"]');
    expect(skeleton).toHaveAttribute('aria-hidden', 'true');
  });
});

describe('M3SkeletonRow', () => {
  it('renders a row', () => {
    render(<M3SkeletonRow />);
    const row = document.querySelector('.flex.items-center');
    expect(row).toBeInTheDocument();
  });

  it('renders multiple skeleton elements', () => {
    render(<M3SkeletonRow columns={4} />);
    const skeletons = document.querySelectorAll('[aria-hidden="true"]');
    expect(skeletons.length).toBeGreaterThan(1);
  });
});
