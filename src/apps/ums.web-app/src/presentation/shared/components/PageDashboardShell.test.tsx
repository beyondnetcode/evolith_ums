import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { PageDashboardShell } from './PageDashboardShell';

describe('PageDashboardShell', () => {
  it('renders master content', () => {
    render(
      <PageDashboardShell
        splitterLabel="Resize"
        master={<div data-testid="master">Master</div>}
        detail={<div data-testid="detail">Detail</div>}
      />
    );
    expect(screen.getByTestId('master')).toBeInTheDocument();
  });

  it('renders detail content', () => {
    render(
      <PageDashboardShell
        splitterLabel="Resize"
        master={<div>Master</div>}
        detail={<div data-testid="detail">Detail</div>}
      />
    );
    expect(screen.getByTestId('detail')).toBeInTheDocument();
  });

  it('renders overlay when provided', () => {
    render(
      <PageDashboardShell
        splitterLabel="Resize"
        master={<div>Master</div>}
        detail={<div>Detail</div>}
        overlay={<div data-testid="overlay">Overlay</div>}
      />
    );
    expect(screen.getByTestId('overlay')).toBeInTheDocument();
  });

  it('has displayName', () => {
    expect(PageDashboardShell.displayName).toBe('PageDashboardShell');
  });
});
