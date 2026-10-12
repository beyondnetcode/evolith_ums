import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import React from 'react';
import { UserSignupsInboxPanel } from './UserSignupsInboxPanel';

const activateMutation = vi.fn();
const denyMutation = vi.fn();

vi.mock('@app/identity/hooks/use-inbox', () => ({
  useGetUserSignups: () => ({
    data: [
      {
        userAccountId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
        tenantId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
        email: 'new.user@ums.local',
        displayName: 'Nuevo Usuario',
        category: 'External',
        requestedAt: '2026-06-01T10:00:00Z',
      },
    ],
    isLoading: false,
  }),
  useActivateUser: () => ({ mutate: activateMutation, isPending: false }),
  useDenyUserSignup: () => ({ mutate: denyMutation, isPending: false }),
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

describe('UserSignupsInboxPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders a pending signup request', () => {
    render(<UserSignupsInboxPanel />);

    expect(screen.getByText('Solicitudes de Acceso')).toBeInTheDocument();
    expect(screen.getByText('new.user@ums.local')).toBeInTheDocument();
    expect(screen.getByText('Nuevo Usuario')).toBeInTheDocument();
  });

  it('activates the signup request', () => {
    render(<UserSignupsInboxPanel />);

    fireEvent.click(screen.getByText('new.user@ums.local').closest('button')!);
    fireEvent.click(screen.getByRole('button', { name: 'Activar' }));

    expect(activateMutation).toHaveBeenCalledWith('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa');
  });

  it('denies the signup request', () => {
    render(<UserSignupsInboxPanel />);

    fireEvent.click(screen.getByText('new.user@ums.local').closest('button')!);
    fireEvent.click(screen.getByRole('button', { name: 'Rechazar' }));

    expect(denyMutation).toHaveBeenCalledWith({
      id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    });
  });
});
