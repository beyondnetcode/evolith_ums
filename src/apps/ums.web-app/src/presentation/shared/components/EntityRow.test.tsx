import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';
import { EntityRow } from './EntityRow';

describe('EntityRow', () => {
  it('renders children', () => {
    render(<EntityRow>Entity Name</EntityRow>);
    expect(screen.getByText('Entity Name')).toBeInTheDocument();
  });

  it('renders leading content', () => {
    render(<EntityRow leading={<span data-testid="leading">★</span>}>Entity Name</EntityRow>);
    expect(screen.getByTestId('leading')).toBeInTheDocument();
  });

  it('renders trailing columns', () => {
    render(
      <EntityRow
        trailingColumns={[
          { content: <span>Col 1</span>, width: 'w-20' },
          { content: <span>Col 2</span>, width: 'w-24' },
        ]}
      >
        Entity Name
      </EntityRow>
    );
    expect(screen.getByText('Col 1')).toBeInTheDocument();
    expect(screen.getByText('Col 2')).toBeInTheDocument();
  });

  it('renders trailing content', () => {
    render(
      <EntityRow trailing={<span data-testid="trailing">Action</span>}>Entity Name</EntityRow>
    );
    expect(screen.getByTestId('trailing')).toBeInTheDocument();
  });

  it('renders actions (legacy alias)', () => {
    render(<EntityRow actions={<span data-testid="actions">Legacy</span>}>Entity Name</EntityRow>);
    expect(screen.getByTestId('actions')).toBeInTheDocument();
  });

  it('applies selected styling', () => {
    const { container } = render(<EntityRow selected>Selected Entity</EntityRow>);
    expect(container.innerHTML).toContain('bg-m3-primary/10');
  });

  it('applies inactive styling', () => {
    const { container } = render(<EntityRow isActive={false}>Inactive Entity</EntityRow>);
    expect(container.innerHTML).toContain('opacity-65');
  });

  it('handles click', async () => {
    const handleClick = vi.fn();
    const user = userEvent.setup();
    render(<EntityRow onClick={handleClick}>Clickable Entity</EntityRow>);

    await user.click(screen.getByText('Clickable Entity'));
    expect(handleClick).toHaveBeenCalled();
  });

  it('handles double click', async () => {
    const handleDoubleClick = vi.fn();
    const user = userEvent.setup();
    render(<EntityRow onDoubleClick={handleDoubleClick}>Double Click Entity</EntityRow>);

    await user.dblClick(screen.getByText('Double Click Entity'));
    expect(handleDoubleClick).toHaveBeenCalled();
  });

  it('applies custom className', () => {
    const { container } = render(<EntityRow className="custom-class">Custom Entity</EntityRow>);
    expect(container.innerHTML).toContain('custom-class');
  });

  it('aligns trailing column to start', () => {
    render(
      <EntityRow trailingColumns={[{ content: <span>Start</span>, align: 'start' }]}>
        Entity
      </EntityRow>
    );
    const col = screen.getByText('Start').closest('div');
    expect(col).toHaveClass('justify-start');
  });

  it('aligns trailing column to center', () => {
    render(
      <EntityRow trailingColumns={[{ content: <span>Center</span>, align: 'center' }]}>
        Entity
      </EntityRow>
    );
    const col = screen.getByText('Center').closest('div');
    expect(col).toHaveClass('justify-center');
  });
});
