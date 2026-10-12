/**
 * BranchManager · que la pantalla diga lo que de verdad hace (ADR-0164).
 *
 * El error caro de esta pantalla es cerrar una sucursal creyendo que era un borrado reversible.
 * Estas pruebas fijan que: el rótulo dice «cerrar», el cierre pasa por una confirmación que avisa
 * de que es irreversible, y una sucursal cerrada no ofrece reactivar ni ninguna otra transición.
 */
import React from 'react';
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { BranchManager } from './BranchManager';
import type { Branch } from '@domain/identity/models/branch.model';

const closeBranch = vi.fn().mockResolvedValue(undefined);
const deactivateBranch = vi.fn();
const reactivateBranch = vi.fn();
let branchesEnVista: Branch[] = [];

vi.mock('@app/identity/hooks/use-branch', () => ({
  useGetBranches: () => ({
    data: branchesEnVista,
    isLoading: false,
    isFetching: false,
    refetch: vi.fn(),
  }),
  useGetBranchLifecycle: () => ({ data: [], isLoading: false }),
  useAddBranch: () => ({ mutate: vi.fn(), mutateAsync: vi.fn(), isPending: false }),
  useUpdateBranch: () => ({ mutate: vi.fn(), isPending: false }),
  useCloseBranch: () => ({ mutate: vi.fn(), mutateAsync: closeBranch, isPending: false }),
  useDeactivateBranch: () => ({ mutate: deactivateBranch, isPending: false }),
  useReactivateBranch: () => ({ mutate: reactivateBranch, isPending: false }),
}));

const branch = (overrides: Partial<Branch>): Branch => ({
  branchId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
  code: 'SUC_CALLAO',
  name: 'Sucursal Callao',
  isActive: true,
  geofencingMetadata: null,
  isClosed: false,
  closedAtUtc: null,
  ...overrides,
});

const renderManager = (branches: Branch[]) => {
  branchesEnVista = branches;
  return render(<BranchManager tenantId="3fa85f64-5717-4562-b3fc-2c963f66afa7" />);
};

describe('BranchManager · cerrar no es eliminar', () => {
  beforeEach(() => {
    closeBranch.mockClear();
    deactivateBranch.mockClear();
    reactivateBranch.mockClear();
  });

  it('el rótulo de la acción terminal habla de cerrar, no de eliminar', () => {
    renderManager([branch({})]);

    expect(screen.getByLabelText('Cerrar sucursal (definitivo)')).toBeInTheDocument();
    expect(screen.queryByLabelText('Eliminar ubicación')).not.toBeInTheDocument();
  });

  it('cerrar no se dispara al pulsar: primero confirma y avisa de que es irreversible', async () => {
    renderManager([branch({})]);

    await userEvent.click(screen.getByLabelText('Cerrar sucursal (definitivo)'));

    expect(screen.getByText('¿Cerrar la sucursal definitivamente?')).toBeInTheDocument();
    expect(screen.getByText(/No podrá reactivarse/)).toBeInTheDocument();
    expect(screen.getByText(/quedará ocupado para siempre/)).toBeInTheDocument();
    expect(closeBranch).not.toHaveBeenCalled();
  });

  it('confirmar cierra la sucursal y arrastra el motivo escrito', async () => {
    renderManager([branch({})]);

    await userEvent.click(screen.getByLabelText('Cerrar sucursal (definitivo)'));
    await userEvent.type(
      screen.getByLabelText(/Motivo del cierre/),
      'Cese de operaciones en Callao'
    );
    await userEvent.click(screen.getByRole('button', { name: 'Sí, cerrar definitivamente' }));

    await waitFor(() =>
      expect(closeBranch).toHaveBeenCalledWith({
        branchId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
        reason: 'Cese de operaciones en Callao',
      })
    );
  });

  it('una sucursal activa ofrece desactivar, que es la pausa reversible', () => {
    renderManager([branch({})]);

    expect(screen.getByLabelText('Desactivar')).toBeInTheDocument();
    expect(screen.queryByLabelText('Reactivar')).not.toBeInTheDocument();
  });

  it('una sucursal pausada ofrece reactivar', () => {
    renderManager([branch({ isActive: false })]);

    expect(screen.getByLabelText('Reactivar')).toBeInTheDocument();
    expect(screen.queryByLabelText('Desactivar')).not.toBeInTheDocument();
  });

  it('una sucursal CERRADA no ofrece reactivar, ni desactivar, ni volver a cerrar, ni editar', () => {
    renderManager([
      branch({ isActive: false, isClosed: true, closedAtUtc: '2026-07-31T15:04:05Z' }),
    ]);

    expect(screen.queryByLabelText('Reactivar')).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Desactivar')).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Cerrar sucursal (definitivo)')).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Editar')).not.toBeInTheDocument();
  });

  it('la sucursal cerrada se distingue de la meramente pausada', () => {
    renderManager([branch({ isActive: false, isClosed: true })]);

    const badge = screen.getByTestId('status-badge');
    expect(badge).toHaveTextContent('Cerrada');
    expect(screen.getByText(/su ciclo de vida terminó/)).toBeInTheDocument();
  });

  it('la bitácora está a mano incluso en una sucursal cerrada', async () => {
    renderManager([branch({ isActive: false, isClosed: true, name: 'Sucursal Callao' })]);

    await userEvent.click(screen.getByLabelText('Bitácora'));

    expect(screen.getByText('Bitácora de «Sucursal Callao»')).toBeInTheDocument();
  });
});
