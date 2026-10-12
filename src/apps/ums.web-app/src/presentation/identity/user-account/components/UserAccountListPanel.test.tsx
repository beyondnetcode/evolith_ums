import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import React from 'react';
import { UserAccountListPanel } from './UserAccountListPanel';
import { Tenant } from '@domain/identity/models/tenant.model';
import { UserAccount } from '@domain/identity/models/user-account.model';

const mockTenants: Tenant[] = [
  {
    tenantId: 'tenant-1',
    code: 'RANSA',
    name: 'Ransa Comercial',
    type: 'INTERNAL',
    status: 'Active',
    parentTenantId: null,
    companyReference: null,
  },
  {
    tenantId: 'tenant-2',
    code: 'NEPTUNIA',
    name: 'Neptunia S.A.',
    type: 'CLIENT',
    status: 'Active',
    parentTenantId: null,
    companyReference: null,
  },
];

const mockAccounts: UserAccount[] = [
  {
    userAccountId: 'user-1',
    tenantId: 'tenant-1',
    branchId: null,
    displayName: 'Admin Ransa',
    email: 'admin@ransa.pe',
    category: 'Internal',
    status: 'Active',
    identityReference: null,
    identityReferenceType: null,
  },
];

const mockQueryState = {
  searchCriteria: 'email',
  setSearchCriteria: () => {},
  searchValue: '',
  setSearchValue: () => {},
  handleQuerySubmit: (e: React.FormEvent) => e.preventDefault(),
  activeFilter: 'all',
  setActiveFilter: () => {},
  sortBy: 'email',
  setSortBy: () => {},
  sortOrder: 'asc' as const,
  toggleSortOrder: () => {},
  appliedQuery: { term: '' },
};

const mockPaginationState = {
  page: 1,
  pageSize: 20,
  totalItems: 1,
  totalPages: 1,
  handlePageChange: () => {},
  setPage: () => {},
};

describe('UserAccountListPanel', () => {
  it('renders title, search input, and active tenant selector dropdown', () => {
    const onTenantChange = vi.fn();
    render(
      <UserAccountListPanel
        accounts={mockAccounts}
        selectedId="user-1"
        isLoading={false}
        error={null}
        viewMode="list"
        onViewModeChange={() => {}}
        queryState={mockQueryState}
        paginationState={mockPaginationState}
        onRegisterNew={() => {}}
        onSelectAccount={() => {}}
        criteriaOptions={[{ label: 'Email', value: 'email' }]}
        filterOptions={[{ label: 'All', value: 'all' }]}
        sortOptions={[{ label: 'Email', value: 'email' }]}
        tenants={mockTenants}
        selectedTenantId="tenant-1"
        onTenantChange={onTenantChange}
      />
    );

    // Verify title and subtitle are present
    expect(screen.getByText('Cuentas de Usuario')).toBeInTheDocument();

    // Verify active tenant selector is rendered
    expect(screen.getAllByText('Inquilino Activo').length).toBeGreaterThan(0);
    expect(screen.getByRole('button', { name: 'Ransa Comercial (RANSA)' })).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Ransa Comercial (RANSA)' }));
    expect(screen.getByText(/Neptunia S.A./)).toBeInTheDocument();

    // Verify account row renders
    expect(screen.getByText('admin@ransa.pe')).toBeInTheDocument();
  });

  it('triggers onTenantChange callback when changing selected tenant', () => {
    const onTenantChange = vi.fn();
    render(
      <UserAccountListPanel
        accounts={mockAccounts}
        selectedId="user-1"
        isLoading={false}
        error={null}
        viewMode="list"
        onViewModeChange={() => {}}
        queryState={mockQueryState}
        paginationState={mockPaginationState}
        onRegisterNew={() => {}}
        onSelectAccount={() => {}}
        criteriaOptions={[{ label: 'Email', value: 'email' }]}
        filterOptions={[{ label: 'All', value: 'all' }]}
        sortOptions={[{ label: 'Email', value: 'email' }]}
        tenants={mockTenants}
        selectedTenantId="tenant-1"
        onTenantChange={onTenantChange}
      />
    );

    fireEvent.click(screen.getByRole('button', { name: 'Ransa Comercial (RANSA)' }));
    fireEvent.click(screen.getByText(/Neptunia S.A./));

    expect(onTenantChange).toHaveBeenCalledWith('tenant-2');
  });
});
