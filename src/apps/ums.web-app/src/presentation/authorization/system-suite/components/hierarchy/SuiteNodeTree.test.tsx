import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { SuiteNodeTree } from './SuiteNodeTree';

// UX G-107: un módulo nace `Inactive` y el backend rechaza `AddNode` con 400 sobre
// un módulo inactivo. La UI debe deshabilitar el alta de nodos y guiar a activar el
// módulo primero, en lugar de fallar con un 400 críptico.

const mutation = () => ({ mutate: vi.fn(), isPending: false });

vi.mock('@app/authorization/hooks/use-system-suite', () => ({
  useAddNode: () => mutation(),
  useUpdateNode: () => mutation(),
  useRemoveNode: () => mutation(),
  useSetNodeStatus: () => mutation(),
  useLinkNodeAction: () => mutation(),
  useUnlinkNodeAction: () => mutation(),
  useSetNodeMetadata: () => mutation(),
}));

const baseProps = {
  suiteId: 'suite-1',
  availableActions: [],
  isExpanded: true,
  isNodeExpanded: () => true,
  onToggle: vi.fn(),
  onToggleNode: vi.fn(),
  onDeactivate: vi.fn(),
  onActivate: vi.fn(),
  onRemove: vi.fn(),
  isDeactivating: false,
  isActivating: false,
  isRemoving: false,
};

const makeModule = (status: string) => ({
  id: 'm1',
  code: 'MOD',
  name: 'Módulo 1',
  description: '',
  status,
  sortOrder: 1,
  nodes: [],
});

describe('SuiteNodeTree — alta de nodo raíz según estado del módulo', () => {
  it('deshabilita el alta y guía a activar cuando el módulo está inactivo', () => {
    render(<SuiteNodeTree {...baseProps} module={makeModule('Inactive')} />);

    const guide = screen.getByText('Activa el módulo para agregar nodos');
    expect(guide).toBeDisabled();

    // No debe existir ninguna acción habilitada de «Agregar nodo raíz».
    expect(screen.queryByText('Agregar nodo raíz')).toBeNull();
    expect(screen.queryByRole('button', { name: 'Agregar nodo raíz' })).toBeNull();
  });

  it('permite agregar un nodo raíz cuando el módulo está activo', () => {
    render(<SuiteNodeTree {...baseProps} module={makeModule('Active')} />);

    const addBtn = screen.getByText('Agregar nodo raíz');
    expect(addBtn).toBeEnabled();

    fireEvent.click(addBtn);

    // Se abre el formulario inline de alta de nodo raíz.
    expect(screen.getByText('Nuevo Nodo Raíz')).toBeInTheDocument();
  });
});
