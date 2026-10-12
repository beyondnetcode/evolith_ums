import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { PageShell } from './PageShell';

describe('PageShell', () => {
  it('renders children', () => {
    render(
      <PageShell>
        <div>Page Content</div>
      </PageShell>
    );
    expect(screen.getByText('Page Content')).toBeInTheDocument();
  });

  it('applies flex layout classes', () => {
    render(
      <PageShell>
        <div>Content</div>
      </PageShell>
    );
    const container = screen.getByText('Content').parentElement;
    expect(container).toHaveClass('flex', 'flex-col', 'flex-1');
  });

  it('applies custom className', () => {
    render(
      <PageShell className="p-4">
        <div>Padded Content</div>
      </PageShell>
    );
    const container = screen.getByText('Padded Content').parentElement;
    expect(container).toHaveClass('p-4');
  });
});
