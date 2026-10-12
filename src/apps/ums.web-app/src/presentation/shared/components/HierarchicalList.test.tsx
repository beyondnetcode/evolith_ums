import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';
import { HierarchicalList, HierarchicalRow, HierarchicalExpandButton } from './HierarchicalList';

interface TestItem {
  id: string;
  name: string;
  parentId: string | null;
}

describe('HierarchicalList', () => {
  const items: TestItem[] = [
    { id: '1', name: 'Parent 1', parentId: null },
    { id: '2', name: 'Parent 2', parentId: null },
    { id: '1a', name: 'Child 1a', parentId: '1' },
    { id: '1b', name: 'Child 1b', parentId: '1' },
  ];

  it('renders parent rows in list mode', () => {
    render(
      <HierarchicalList<TestItem>
        items={items}
        idKey="id"
        parentIdKey="parentId"
        selectedId=""
        onSelect={() => {}}
        viewMode="list"
        renderParentRow={(node, isSelected, isExpanded, onToggle) => (
          <div data-testid={`parent-${String(node.item.id)}`} onClick={onToggle}>
            {String(node.item.name)}
          </div>
        )}
        renderChildRow={item => <div data-testid={`child-${item.id}`}>{item.name}</div>}
        renderParentCard={() => null}
        renderChildCard={() => null}
      />
    );

    expect(screen.getByTestId('parent-1')).toHaveTextContent('Parent 1');
    expect(screen.getByTestId('parent-2')).toHaveTextContent('Parent 2');
  });

  it('renders child rows when expanded', async () => {
    const user = userEvent.setup();
    render(
      <HierarchicalList<TestItem>
        items={items}
        idKey="id"
        parentIdKey="parentId"
        selectedId=""
        onSelect={() => {}}
        viewMode="list"
        renderParentRow={(node, isSelected, isExpanded, onToggle) => (
          <div data-testid={`parent-${String(node.item.id)}`} onClick={onToggle}>
            {String(node.item.name)}
          </div>
        )}
        renderChildRow={item => <div data-testid={`child-${item.id}`}>{item.name}</div>}
        renderParentCard={() => null}
        renderChildCard={() => null}
      />
    );

    expect(screen.queryByTestId('child-1a')).not.toBeInTheDocument();

    await user.click(screen.getByTestId('parent-1'));

    expect(screen.getByTestId('child-1a')).toBeInTheDocument();
    expect(screen.getByTestId('child-1b')).toBeInTheDocument();
  });

  it('renders cards in thumbnail mode', () => {
    render(
      <HierarchicalList<TestItem>
        items={items}
        idKey="id"
        parentIdKey="parentId"
        selectedId=""
        onSelect={() => {}}
        viewMode="thumbnail"
        renderParentRow={() => null}
        renderChildRow={() => null}
        renderParentCard={(node, isSelected, isExpanded, onToggle) => (
          <div data-testid={`card-${String(node.item.id)}`} onClick={onToggle}>
            {String(node.item.name)}
          </div>
        )}
        renderChildCard={item => <div data-testid={`child-card-${item.id}`}>{item.name}</div>}
      />
    );

    expect(screen.getByTestId('card-1')).toHaveTextContent('Parent 1');
    expect(screen.getByTestId('card-2')).toHaveTextContent('Parent 2');
  });

  it('renders nothing for empty items', () => {
    const { container } = render(
      <HierarchicalList<TestItem>
        items={[]}
        idKey="id"
        parentIdKey="parentId"
        selectedId=""
        onSelect={() => {}}
        viewMode="list"
        renderParentRow={() => <div>Row</div>}
        renderChildRow={() => <div>Child</div>}
        renderParentCard={() => null}
        renderChildCard={() => null}
      />
    );

    expect(container.textContent).toBe('');
  });
});

describe('HierarchicalRow', () => {
  it('renders children', () => {
    render(
      <table>
        <tbody>
          <HierarchicalRow
            hasChildren={false}
            isExpanded={false}
            isChild={false}
            onToggleExpand={() => {}}
            onClick={() => {}}
            isSelected={false}
          >
            <td>Cell</td>
          </HierarchicalRow>
        </tbody>
      </table>
    );

    expect(screen.getByText('Cell')).toBeInTheDocument();
  });

  it('applies selected styling', () => {
    render(
      <table>
        <tbody>
          <HierarchicalRow
            hasChildren={false}
            isExpanded={false}
            isChild={false}
            onToggleExpand={() => {}}
            onClick={() => {}}
            isSelected
          >
            <td>Selected</td>
          </HierarchicalRow>
        </tbody>
      </table>
    );

    const row = screen.getByText('Selected').closest('tr');
    expect(row).toHaveAttribute('aria-selected', 'true');
  });

  it('applies child styling', () => {
    render(
      <table>
        <tbody>
          <HierarchicalRow
            hasChildren={false}
            isExpanded={false}
            isChild
            onToggleExpand={() => {}}
            onClick={() => {}}
            isSelected={false}
          >
            <td>Child</td>
          </HierarchicalRow>
        </tbody>
      </table>
    );

    const row = screen.getByText('Child').closest('tr');
    expect(row).toHaveClass('bg-m3-surface-container/10');
  });

  it('is keyboard accessible', () => {
    render(
      <table>
        <tbody>
          <HierarchicalRow
            hasChildren={false}
            isExpanded={false}
            isChild={false}
            onToggleExpand={() => {}}
            onClick={() => {}}
            isSelected={false}
          >
            <td>Row</td>
          </HierarchicalRow>
        </tbody>
      </table>
    );

    const row = screen.getByText('Row').closest('tr');
    expect(row).toHaveAttribute('tabIndex', '0');
    expect(row).toHaveAttribute('role', 'row');
  });
});

describe('HierarchicalExpandButton', () => {
  it('renders chevron when hasChildren is true', () => {
    render(<HierarchicalExpandButton hasChildren isExpanded={false} onClick={() => {}} />);

    expect(screen.getByRole('button')).toBeInTheDocument();
  });

  it('renders spacer when hasChildren is false', () => {
    render(<HierarchicalExpandButton hasChildren={false} isExpanded={false} onClick={() => {}} />);

    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });

  it('applies rotation when expanded', () => {
    render(<HierarchicalExpandButton hasChildren isExpanded onClick={() => {}} />);

    const button = screen.getByRole('button');
    expect(button).toHaveClass('rotate-90');
  });

  it('calls onClick when clicked', async () => {
    const handleClick = vi.fn();
    const user = userEvent.setup();
    render(<HierarchicalExpandButton hasChildren isExpanded={false} onClick={handleClick} />);

    await user.click(screen.getByRole('button'));
    expect(handleClick).toHaveBeenCalled();
  });
});
