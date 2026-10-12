/**
 * SystemActionsPanel · lo que la pantalla dice frente a lo que el backend guarda (ADR-0164).
 *
 * Desde que el borrado es lógico, un ítem retirado (`isActive: false`) sobrevive con sus booleanos
 * intactos. Estas pruebas fijan que la pantalla no mienta: lo retirado se pinta Neutral, «Neutral»
 * no borra, y reactivar no vuelve a dar de alta la clave ya ocupada (que respondería 409).
 */
import React from 'react';
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SystemActionsPanel } from './SystemActionsPanel';
import type { PermissionTemplateItem } from '@domain/authorization/models/permission-template.model';
import type { SystemSuite } from '@domain/authorization/models/system-suite.model';

const applyEffect = vi.fn().mockResolvedValue(true);

vi.mock('@app/authorization/hooks/use-permission-template', () => ({
  useApplyTemplateItemEffect: () => ({ applyEffect, isPending: false }),
}));

const ACTION_ID = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
const SUITE_ID = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb';

const suite = {
  systemSuiteId: SUITE_ID,
  actions: [{ id: ACTION_ID, code: 'SYS.EXPORT', name: 'Exportar manifiesto' }],
} as unknown as SystemSuite;

const item = (overrides: Partial<PermissionTemplateItem>): PermissionTemplateItem => ({
  itemId: 'cccccccc-cccc-cccc-cccc-cccccccccccc',
  targetType: 'SystemSuite',
  targetId: SUITE_ID,
  targetName: 'Suite',
  actionId: ACTION_ID,
  actionName: 'Exportar manifiesto',
  isAllowed: false,
  isDenied: false,
  isActive: true,
  ...overrides,
});

const renderPanel = (items: PermissionTemplateItem[]) =>
  render(<SystemActionsPanel suite={suite} items={items} templateId="t1" isDraft={true} />);

describe('SystemActionsPanel · estado efectivo de la acción', () => {
  beforeEach(() => {
    applyEffect.mockClear();
  });

  it('pinta Permitido cuando el ítem está vigente y permite', () => {
    renderPanel([item({ isAllowed: true })]);

    expect(screen.getByText('Permitido')).toBeInTheDocument();
  });

  it('NO pinta Permitido un ítem retirado: ya no concede nada', () => {
    renderPanel([item({ isAllowed: true, isActive: false })]);

    expect(screen.queryByText('Permitido')).not.toBeInTheDocument();
    expect(screen.getByText('Neutral')).toBeInTheDocument();
  });

  it('NO pinta Denegado un ítem retirado', () => {
    renderPanel([item({ isDenied: true, isActive: false })]);

    expect(screen.queryByText('Denegado')).not.toBeInTheDocument();
    expect(screen.getByText('Neutral')).toBeInTheDocument();
  });

  it('poner Neutral delega en el adaptador con el ítem vigente, sin borrarlo', async () => {
    const vigente = item({ isAllowed: true });
    renderPanel([vigente]);

    await userEvent.click(screen.getByTitle('Neutral'));

    await waitFor(() =>
      expect(applyEffect).toHaveBeenCalledWith({
        effect: 'Neutral',
        item: vigente,
        target: { targetType: 'SystemSuite', targetId: SUITE_ID, actionId: ACTION_ID },
      })
    );
  });

  it('volver a marcar Allow pasa el ítem retirado al adaptador, que lo reactivará', async () => {
    const retirado = item({ isAllowed: true, isActive: false });
    renderPanel([retirado]);

    await userEvent.click(screen.getByTitle('Permitir'));

    await waitFor(() =>
      expect(applyEffect).toHaveBeenCalledWith(
        expect.objectContaining({ effect: 'Allow', item: retirado })
      )
    );
  });
});
