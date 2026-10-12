import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import React from 'react';
import { ProfileRequestsInboxPanel } from './ProfileRequestsInboxPanel';

const approveMutation = vi.fn();
const rejectMutation = vi.fn();

vi.mock('@app/identity/hooks/use-inbox', () => ({
  useGetProfileRequests: () => ({
    data: [
      {
        approvalRequestId: '11111111-1111-1111-1111-111111111111',
        targetUserId: '22222222-2222-2222-2222-222222222222',
        requestedSystemId: '33333333-3333-3333-3333-333333333333',
        requestedBranchId: null,
        requestedRoleId: '44444444-4444-4444-4444-444444444444',
        justification: 'Necesita acceso operativo',
        requestedAt: '2026-06-01T10:00:00Z',
      },
    ],
    isLoading: false,
  }),
  useApproveProfileRequest: () => ({ mutate: approveMutation, isPending: false }),
  useRejectProfileRequest: () => ({ mutate: rejectMutation, isPending: false }),
}));

vi.mock('@app/formatting/use-date-format', () => ({
  useDateFormat: () => ({
    formatDateTime: () => '2026-06-01 10:00',
  }),
}));

vi.mock('@shared/components/StatusBadge', () => ({
  StatusBadge: ({ label }: { label: string }) => <span>{label}</span>,
}));

vi.mock('@shared/components/ListToolbar', () => ({
  ListToolbar: () => <div data-testid="list-toolbar" />,
}));

vi.mock('@shared/components/data-display/DataList', () => ({
  DataList: ({ renderThumbnail }: any) => <div data-testid="data-list">{renderThumbnail()}</div>,
}));

describe('ProfileRequestsInboxPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders a pending profile request', () => {
    render(<ProfileRequestsInboxPanel />);

    expect(screen.getByText('Solicitudes de Perfil')).toBeInTheDocument();
    expect(screen.getByText('Usuario 22222222')).toBeInTheDocument();
  });

  it('approves the request with the selected role', () => {
    render(<ProfileRequestsInboxPanel />);

    fireEvent.click(screen.getByText('Solicitud de perfil').closest('button')!);
    fireEvent.click(screen.getByRole('button', { name: 'Aprobar' }));

    expect(approveMutation).toHaveBeenCalledWith({
      id: '11111111-1111-1111-1111-111111111111',
      roleId: '44444444-4444-4444-4444-444444444444',
    });
  });

  it('rejects the request by id', () => {
    render(<ProfileRequestsInboxPanel />);

    fireEvent.click(screen.getByText('Solicitud de perfil').closest('button')!);
    fireEvent.click(screen.getByRole('button', { name: 'Rechazar' }));

    expect(rejectMutation).toHaveBeenCalledWith({
      id: '11111111-1111-1111-1111-111111111111',
    });
  });
});
