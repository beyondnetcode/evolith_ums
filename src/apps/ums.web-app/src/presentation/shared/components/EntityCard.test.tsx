import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { EntityCard, EntityCardGeneric } from './EntityCard';
import * as M3CardModule from './M3Card';

vi.mock('./M3Card', () => ({
  M3Card: ({ children, onClick, variant, className }: any) => (
    <div data-testid="m3-card" data-variant={variant} className={className} onClick={onClick}>
      {children}
    </div>
  ),
}));

describe('EntityCard', () => {
  it('renders title', () => {
    render(<EntityCard title="Test Entity" />);
    expect(screen.getByText('Test Entity')).toBeInTheDocument();
  });

  it('renders subtitle', () => {
    render(<EntityCard title="Test Entity" subtitle="Subtitle text" />);
    expect(screen.getByText('Subtitle text')).toBeInTheDocument();
  });

  it('renders icon', () => {
    render(<EntityCard title="Test Entity" icon={<span data-testid="icon">I</span>} />);
    expect(screen.getByTestId('icon')).toBeInTheDocument();
  });

  it('renders badges', () => {
    render(<EntityCard title="Test Entity" badges={<span data-testid="badge">Badge</span>} />);
    expect(screen.getByTestId('badge')).toBeInTheDocument();
  });

  it('renders footer', () => {
    render(<EntityCard title="Test Entity" footer={<span data-testid="footer">Footer</span>} />);
    expect(screen.getByTestId('footer')).toBeInTheDocument();
  });

  it('calls onClick when clicked', () => {
    const onClick = vi.fn();
    render(<EntityCard title="Test Entity" onClick={onClick} />);
    fireEvent.click(screen.getByTestId('m3-card'));
    expect(onClick).toHaveBeenCalled();
  });

  it('applies selected styling when selected is true', () => {
    render(<EntityCard title="Test Entity" selected={true} />);
    expect(screen.getByTestId('m3-card')).toHaveAttribute('data-variant', 'elevated');
  });

  it('applies filled variant when not selected', () => {
    render(<EntityCard title="Test Entity" selected={false} />);
    expect(screen.getByTestId('m3-card')).toHaveAttribute('data-variant', 'filled');
  });

  it('applies custom className', () => {
    render(<EntityCard title="Test Entity" className="custom-class" />);
    expect(screen.getByTestId('m3-card')).toHaveClass('custom-class');
  });
});

describe('EntityCardGeneric', () => {
  const mockItem = { id: '1', name: 'Test Item', code: 'T001', status: 'Active' };

  it('renders title from function', () => {
    render(
      <EntityCardGeneric item={mockItem} idKey="id" icon="I" title={item => item.name as string} />
    );
    expect(screen.getByText('Test Item')).toBeInTheDocument();
  });

  it('renders subtitle from function', () => {
    render(
      <EntityCardGeneric
        item={mockItem}
        idKey="id"
        icon="I"
        title={item => item.name as string}
        subtitle={item => item.code as string}
      />
    );
    expect(screen.getByText('T001')).toBeInTheDocument();
  });

  it('renders fields', () => {
    render(
      <EntityCardGeneric
        item={mockItem}
        idKey="id"
        icon="I"
        title={item => item.name as string}
        fields={[
          { label: 'Status', accessor: 'status' },
          { label: 'Code', accessor: 'code' },
        ]}
      />
    );
    expect(screen.getByText('Status')).toBeInTheDocument();
    expect(screen.getByText('Active')).toBeInTheDocument();
  });

  it('renders badge', () => {
    render(
      <EntityCardGeneric
        item={mockItem}
        idKey="id"
        icon="I"
        title={item => item.name as string}
        badge={<span data-testid="badge">Badge</span>}
      />
    );
    expect(screen.getByTestId('badge')).toBeInTheDocument();
  });

  it('renders footer', () => {
    render(
      <EntityCardGeneric
        item={mockItem}
        idKey="id"
        icon="I"
        title={item => item.name as string}
        footer={<span data-testid="footer">Footer</span>}
      />
    );
    expect(screen.getByTestId('footer')).toBeInTheDocument();
  });

  it('calls onClick when clicked', () => {
    const onClick = vi.fn();
    render(
      <EntityCardGeneric
        item={mockItem}
        idKey="id"
        icon="I"
        title={item => item.name as string}
        onClick={onClick}
      />
    );
    fireEvent.click(screen.getByTestId('m3-card'));
    expect(onClick).toHaveBeenCalled();
  });

  it('applies selected styling when isSelected is true', () => {
    render(
      <EntityCardGeneric
        item={mockItem}
        idKey="id"
        icon="I"
        title={item => item.name as string}
        isSelected={true}
      />
    );
    expect(screen.getByTestId('m3-card')).toHaveAttribute('data-variant', 'elevated');
  });

  it('uses formatter for field values', () => {
    render(
      <EntityCardGeneric
        item={mockItem}
        idKey="id"
        icon="I"
        title={item => item.name as string}
        fields={[{ label: 'Status', accessor: 'status', formatter: v => `Status: ${v}` }]}
      />
    );
    expect(screen.getByText('Status: Active')).toBeInTheDocument();
  });

  it('shows dash for null field values', () => {
    const itemWithNull = { id: '1', name: 'Test', value: null };
    render(
      <EntityCardGeneric
        item={itemWithNull}
        idKey="id"
        icon="I"
        title={item => item.name as string}
        fields={[{ label: 'Value', accessor: 'value' }]}
      />
    );
    expect(screen.getByText('-')).toBeInTheDocument();
  });
});
