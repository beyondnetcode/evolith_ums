import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import DataGrid, { type ColumnDef } from './DataGrid';

// Modo tarjeta vs. tabla depende de `useIsMobile`; se controla en las pruebas.
let mockIsMobile = false;
vi.mock('../hooks/use-breakpoint', () => ({
  useIsMobile: () => mockIsMobile,
}));

beforeEach(() => {
  mockIsMobile = false;
});

interface TestData {
  id: string;
  name: string;
  status: string;
}

describe('DataGrid', () => {
  const columns: ColumnDef<TestData>[] = [
    { header: 'ID', accessor: 'id' },
    { header: 'Name', accessor: 'name' },
    { header: 'Status', accessor: 'status' },
  ];

  const data: TestData[] = [
    { id: '1', name: 'Item 1', status: 'Active' },
    { id: '2', name: 'Item 2', status: 'Inactive' },
  ];

  it('renders loading state', () => {
    render(<DataGrid columns={columns} data={[]} idKey="id" isLoading />);
    expect(screen.getByText('Loading...')).toBeInTheDocument();
  });

  it('renders empty state with default label', () => {
    render(<DataGrid columns={columns} data={[]} idKey="id" />);
    expect(screen.getByText('No records found.')).toBeInTheDocument();
  });

  it('renders empty state with custom label', () => {
    render(<DataGrid columns={columns} data={[]} idKey="id" emptyLabel="No items" />);
    expect(screen.getByText('No items')).toBeInTheDocument();
  });

  it('renders data rows', () => {
    render(<DataGrid columns={columns} data={data} idKey="id" />);
    expect(screen.getByText('Item 1')).toBeInTheDocument();
    expect(screen.getByText('Item 2')).toBeInTheDocument();
  });

  it('renders column headers', () => {
    render(<DataGrid columns={columns} data={data} idKey="id" />);
    expect(screen.getByText('ID')).toBeInTheDocument();
    expect(screen.getByText('Name')).toBeInTheDocument();
    expect(screen.getByText('Status')).toBeInTheDocument();
  });

  it('applies custom className', () => {
    render(<DataGrid columns={columns} data={data} idKey="id" className="custom-class" />);
    const container = screen.getByText('Item 1').closest('div');
    expect(container).toHaveClass('custom-class');
  });

  it('calls onSelect when row is clicked', () => {
    const onSelect = vi.fn();
    render(<DataGrid columns={columns} data={data} idKey="id" onSelect={onSelect} />);
    screen.getByText('Item 1').closest('tr')?.click();
    expect(onSelect).toHaveBeenCalledWith('1');
  });

  it('highlights selected row', () => {
    render(<DataGrid columns={columns} data={data} idKey="id" selectedId="1" />);
    const row = screen.getByText('Item 1').closest('tr');
    expect(row).toHaveClass('bg-m3-primary-container/30');
  });

  it('renders with cell renderer', () => {
    const customColumns: ColumnDef<TestData>[] = [
      {
        header: 'Name',
        cell: item => <span data-testid={`name-${item.id}`}>{item.name.toUpperCase()}</span>,
      },
    ];
    render(<DataGrid columns={customColumns} data={data} idKey="id" />);
    expect(screen.getByTestId('name-1')).toHaveTextContent('ITEM 1');
  });

  // ─── Modo tarjeta (móvil) ───────────────────────────────────────────────────
  describe('modo tarjeta (móvil)', () => {
    beforeEach(() => {
      mockIsMobile = true;
    });

    it('no renderiza tabla en móvil', () => {
      render(<DataGrid columns={columns} data={data} idKey="id" />);
      expect(document.querySelector('table')).toBeNull();
    });

    it('renderiza una tarjeta por registro con titular y datos', () => {
      render(<DataGrid columns={columns} data={data} idKey="id" />);
      expect(screen.getByText('Item 1')).toBeInTheDocument();
      expect(screen.getByText('Item 2')).toBeInTheDocument();
      // Cabeceras secundarias como etiquetas (aparecen una vez por tarjeta).
      expect(screen.getAllByText('Status')).toHaveLength(2);
      expect(screen.getByText('Active')).toBeInTheDocument();
      expect(screen.getByText('Inactive')).toBeInTheDocument();
    });

    it('selecciona al tocar la tarjeta', () => {
      const onSelect = vi.fn();
      render(<DataGrid columns={columns} data={data} idKey="id" onSelect={onSelect} />);
      screen.getByText('Item 1').closest('div')?.click();
      expect(onSelect).toHaveBeenCalledWith('1');
    });

    it('renderiza estado vacío en móvil', () => {
      render(<DataGrid columns={columns} data={[]} idKey="id" emptyLabel="Sin datos" />);
      expect(screen.getByText('Sin datos')).toBeInTheDocument();
    });
  });
});
