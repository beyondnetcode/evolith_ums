import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { M3TreeAccordion } from './M3TreeAccordion';

describe('M3TreeAccordion', () => {
  it('renders nothing when items is empty', () => {
    const { container } = render(<M3TreeAccordion items={[]} />);
    expect(container.firstChild).toBeNull();
  });

  it('renders nothing when items is null', () => {
    const { container } = render(<M3TreeAccordion items={null as unknown as []} />);
    expect(container.firstChild).toBeNull();
  });

  it('renders items with labels', () => {
    const items = [
      { id: '1', label: 'Item 1' },
      { id: '2', label: 'Item 2' },
    ];
    render(<M3TreeAccordion items={items} />);
    expect(screen.getByText('Item 1')).toBeInTheDocument();
    expect(screen.getByText('Item 2')).toBeInTheDocument();
  });

  it('renders items with icons', () => {
    const items = [{ id: '1', label: 'Item 1', icon: <span data-testid="icon-1">X</span> }];
    render(<M3TreeAccordion items={items} />);
    expect(screen.getByTestId('icon-1')).toBeInTheDocument();
  });

  it('renders nested children structure', () => {
    const items = [
      {
        id: '1',
        label: 'Parent',
        defaultExpanded: true,
        children: [{ id: '1.1', label: 'Child 1' }],
      },
    ];
    render(<M3TreeAccordion items={items} />);
    expect(screen.getByText('Parent')).toBeInTheDocument();
    expect(screen.getByText('Child 1')).toBeInTheDocument();
  });

  it('respects defaultExpanded prop', () => {
    const items = [
      {
        id: '1',
        label: 'Parent',
        defaultExpanded: true,
        children: [{ id: '1.1', label: 'Child' }],
      },
    ];
    render(<M3TreeAccordion items={items} />);
    expect(screen.getByText('Child')).toBeInTheDocument();
  });

  it('renders trailing content', () => {
    const items = [{ id: '1', label: 'Item', trailing: <span data-testid="trailing">End</span> }];
    render(<M3TreeAccordion items={items} />);
    expect(screen.getByTestId('trailing')).toBeInTheDocument();
  });

  it('applies custom className', () => {
    const items = [{ id: '1', label: 'Item' }];
    render(<M3TreeAccordion items={items} className="custom-class" />);
    expect(screen.getByText('Item').closest('ul')).toHaveClass('custom-class');
  });
});
