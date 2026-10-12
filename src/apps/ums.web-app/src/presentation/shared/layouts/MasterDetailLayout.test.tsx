import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { MasterDetailLayout } from './MasterDetailLayout';

describe('MasterDetailLayout', () => {
  it('renders master content', () => {
    render(
      <MasterDetailLayout
        master={<div data-testid="master">Master</div>}
        detail={<div data-testid="detail">Detail</div>}
      />
    );
    expect(screen.getByTestId('master')).toBeInTheDocument();
  });

  it('renders detail content', () => {
    render(
      <MasterDetailLayout
        master={<div>Master</div>}
        detail={<div data-testid="detail">Detail</div>}
      />
    );
    expect(screen.getByTestId('detail')).toBeInTheDocument();
  });

  it('renders overlay when provided', () => {
    render(
      <MasterDetailLayout
        master={<div>Master</div>}
        detail={<div>Detail</div>}
        overlay={<div data-testid="overlay">Overlay</div>}
      />
    );
    expect(screen.getByTestId('overlay')).toBeInTheDocument();
  });

  it('has splitter with accessible label', () => {
    render(
      <MasterDetailLayout
        master={<div>Master</div>}
        detail={<div>Detail</div>}
        splitterLabel="Custom label"
      />
    );
    const separator = screen.getByRole('separator');
    expect(separator).toHaveAttribute('aria-label', 'Custom label');
  });

  it('has default splitter label', () => {
    render(<MasterDetailLayout master={<div>Master</div>} detail={<div>Detail</div>} />);
    const separator = screen.getByRole('separator');
    expect(separator).toHaveAttribute('aria-label', 'Resize detail panel');
  });

  it('has separator role', () => {
    render(<MasterDetailLayout master={<div>Master</div>} detail={<div>Detail</div>} />);
    expect(screen.getByRole('separator')).toHaveAttribute('role', 'separator');
  });

  it('has keyboard focus support', () => {
    render(<MasterDetailLayout master={<div>Master</div>} detail={<div>Detail</div>} />);
    const separator = screen.getByRole('separator');
    expect(separator).toHaveAttribute('tabIndex', '0');
  });

  it('redimensiona el panel de detalle al arrastrar con el dedo (touch)', () => {
    render(<MasterDetailLayout master={<div>Master</div>} detail={<div>Detail</div>} />);
    const separator = screen.getByRole('separator');
    const containerEl = separator.parentElement as HTMLElement;
    vi.spyOn(containerEl, 'getBoundingClientRect').mockReturnValue({
      left: 0,
      right: 1000,
      width: 1000,
      top: 0,
      bottom: 600,
      height: 600,
      x: 0,
      y: 0,
      toJSON: () => ({}),
    } as DOMRect);

    const before = separator.getAttribute('aria-valuenow');
    fireEvent.touchStart(separator, { touches: [{ clientX: 600 }] });
    fireEvent.touchMove(window, { touches: [{ clientX: 300 }] }); // fromRight=700 → 70%
    fireEvent.touchEnd(window);

    expect(separator.getAttribute('aria-valuenow')).toBe('70');
    expect(separator.getAttribute('aria-valuenow')).not.toBe(before);
  });
});
