import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { SuiteNode, type NodeHandlers } from './SuiteNode';
import type { SystemSuiteNode } from '@domain/authorization/schemas/system-suite.schema';

// UX G-107: el alta de nodo hijo también se apoya en `AddNode`, que el backend
// rechaza sobre un módulo inactivo. `NodeHandlers.canAdd` propaga el estado del
// módulo para deshabilitar la acción y guiar a activar primero.

const node: SystemSuiteNode = {
  id: 'n1',
  parentNodeId: null,
  kind: 'Menu',
  code: 'MENU',
  label: 'Menú 1',
  description: '',
  status: 'Active',
  sortOrder: 1,
  actionCodes: [],
  metadata: null,
  children: [],
};

const makeHandlers = (canAdd: boolean): NodeHandlers => ({
  onAddChild: vi.fn(),
  onUpdate: vi.fn(),
  onRemove: vi.fn(),
  onSetStatus: vi.fn(),
  onLinkAction: vi.fn(),
  onUnlinkAction: vi.fn(),
  onSetMetadata: vi.fn(),
  canAdd,
  pending: {
    add: false,
    update: false,
    remove: false,
    status: false,
    link: false,
    unlink: false,
    metadata: false,
  },
});

const renderNode = (canAdd: boolean) =>
  render(
    <SuiteNode
      node={node}
      level={0}
      availableActions={[]}
      handlers={makeHandlers(canAdd)}
      isNodeExpanded={() => true}
      onToggleNode={vi.fn()}
    />
  );

describe('SuiteNode — alta de nodo hijo según estado del módulo', () => {
  it('deshabilita el alta de nodo hijo cuando el módulo está inactivo', () => {
    renderNode(false);
    expect(
      screen.getByRole('button', { name: 'Activa el módulo para agregar nodos' })
    ).toBeDisabled();
  });

  it('permite agregar un nodo hijo cuando el módulo está activo', () => {
    renderNode(true);
    const addChild = screen.getByRole('button', { name: 'Agregar nodo hijo' });
    expect(addChild).toBeEnabled();

    fireEvent.click(addChild);
    expect(screen.getByText('Nuevo Nodo Hijo')).toBeInTheDocument();
  });
});
