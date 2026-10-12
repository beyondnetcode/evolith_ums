import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import React from 'react';
import { TenantProfileCard } from './TenantProfileCard';

const activateMutation = vi.fn();
const suspendMutation = vi.fn();
const managementOwnerMutation = vi.fn();
const updateTenantMutation = vi.fn();
const addNotification = vi.fn();

vi.mock('@app/identity/hooks/use-tenant', () => ({
  useActivateTenant: () => ({ mutate: activateMutation, isPending: false }),
  useSuspendTenant: () => ({ mutate: suspendMutation, isPending: false }),
  useSetManagementOwner: () => ({ mutate: managementOwnerMutation, isPending: false }),
  useUpdateTenant: () => ({ mutate: updateTenantMutation, isPending: false }),
}));

vi.mock('@app/i18n/use-i18n', () => ({
  useI18n: () => ({
    notifTenantUpdated: 'Tenant updated',
    notifTenantUpdatedMsg: (name: string) => `Tenant ${name} updated`,
    notifActivated: 'Activated',
    notifSuspended: 'Suspended',
    notifStatusSetTo: (status: string) => `Status ${status}`,
    editTenant: 'Edit tenant',
    tenantName: 'Tenant name',
    tenantCode: 'Tenant code',
    companyReference: 'Company reference',
    tenantType: 'Tenant type',
    saveBtn: 'Save',
    cancelEdit: 'Cancel',
    unsavedChanges: 'Unsaved changes',
    unsavedChangesMsg: 'There are unsaved changes',
    discardChanges: 'Discard',
    doubleClickToEdit: 'Double click to edit',
    stateControls: 'State controls',
    activateBtn: 'Activate',
    suspendBtn: 'Suspend',
  }),
}));

vi.mock('@app/hooks/use-status-label', () => ({
  useStatusLabel: () => (status: string) => (status === 'Active' ? 'Active' : 'Suspended'),
}));

vi.mock('@app/hooks/use-inline-edit', () => ({
  useInlineEdit: () => ({
    hasEditing: false,
    draft: {
      name: 'Tenant 1',
      code: 'TEN-001',
      companyReference: '',
      type: 'INTERNAL',
    },
    openEdit: vi.fn(),
    cancelEdit: vi.fn(),
    commitEdit: vi.fn(() => ({
      draft: { name: 'Tenant 1', code: 'TEN-001', companyReference: '', type: 'INTERNAL' },
    })),
    setField: vi.fn(),
  }),
}));

vi.mock('@app/hooks/use-reset-on-change', () => ({
  useResetOnChange: vi.fn(),
}));

vi.mock('@app/stores/notification.store', () => ({
  useNotificationStore: (selector: any) => selector({ addNotification }),
}));

vi.mock('@shared/components/M3Card', () => ({
  M3Card: ({ children, ...props }: any) => <section {...props}>{children}</section>,
}));

vi.mock('@shared/components/M3Button', () => ({
  M3Button: ({ children, ...props }: any) => <button {...props}>{children}</button>,
}));

vi.mock('@shared/components/M3TextField', () => ({
  M3TextField: ({ label, value }: any) => (
    <label>
      {label}
      <input value={value} readOnly />
    </label>
  ),
}));

vi.mock('@shared/components/M3Select', () => ({
  M3Select: ({ label, value, children }: any) => (
    <label>
      {label}
      <select value={value} readOnly>
        {children}
      </select>
    </label>
  ),
}));

vi.mock('@shared/components/M3Dialog', () => ({
  M3Dialog: () => null,
}));

vi.mock('@shared/components/StatusBadge', () => ({
  StatusBadge: ({ label }: { label: string }) => <span>{label}</span>,
}));

vi.mock('@shared/components/CodeBadge', () => ({
  CodeBadge: ({ code }: { code: string }) => <span>{code}</span>,
}));

vi.mock('@shared/components/Tooltip', () => ({
  IconButton: ({ children, onClick, tooltip }: any) => (
    <button type="button" aria-label={tooltip} onClick={onClick}>
      {children}
    </button>
  ),
}));

describe('TenantProfileCard', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders the management owner state', () => {
    render(
      <TenantProfileCard
        tenant={{
          tenantId: 'tenant-1',
          code: 'TEN-001',
          name: 'Tenant 1',
          type: 'INTERNAL',
          status: 'Active',
          parentTenantId: null,
          companyReference: null,
          isManagementOwner: true,
        }}
        parentTenant={null}
        onTenantUpdate={vi.fn()}
      />
    );

    expect(screen.getByText('Owner')).toBeInTheDocument();
  });

  it('toggles management owner from true to false', () => {
    render(
      <TenantProfileCard
        tenant={{
          tenantId: 'tenant-1',
          code: 'TEN-001',
          name: 'Tenant 1',
          type: 'INTERNAL',
          status: 'Active',
          parentTenantId: null,
          companyReference: null,
          isManagementOwner: true,
        }}
        parentTenant={null}
        onTenantUpdate={vi.fn()}
      />
    );

    fireEvent.click(screen.getByRole('button', { name: 'Owner' }));

    expect(managementOwnerMutation).toHaveBeenCalledWith(false);
  });

  it('toggles management owner from false to true', () => {
    render(
      <TenantProfileCard
        tenant={{
          tenantId: 'tenant-1',
          code: 'TEN-001',
          name: 'Tenant 1',
          type: 'INTERNAL',
          status: 'Active',
          parentTenantId: null,
          companyReference: null,
          isManagementOwner: false,
        }}
        parentTenant={null}
        onTenantUpdate={vi.fn()}
      />
    );

    fireEvent.click(screen.getByRole('button', { name: 'Not owner' }));

    expect(managementOwnerMutation).toHaveBeenCalledWith(true);
  });
});
