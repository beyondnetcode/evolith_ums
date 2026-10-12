import React from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { PermissionTemplateListPanel } from './PermissionTemplateListPanel';
import type { PermissionTemplate } from '@domain/authorization/models/permission-template.model';

const mockTemplate: PermissionTemplate = {
  templateId: '11111111-1111-1111-1111-111111111111',
  tenantId: '22222222-2222-2222-2222-222222222222',
  roleId: '33333333-3333-3333-3333-333333333333',
  roleName: 'Admin',
  systemSuiteId: '44444444-4444-4444-4444-444444444444',
  systemSuiteName: 'UMS',
  version: '1.0.0',
  status: 'Draft',
};

const queryState = {
  searchCriteria: 'version',
  setSearchCriteria: vi.fn(),
  searchValue: '',
  setSearchValue: vi.fn(),
  handleQuerySubmit: (event: React.FormEvent) => event.preventDefault(),
  activeFilter: 'all',
  setActiveFilter: vi.fn(),
  sortBy: 'version',
  setSortBy: vi.fn(),
  sortOrder: 'asc' as const,
  toggleSortOrder: vi.fn(),
  appliedQuery: { term: '', filterApplied: true },
  handleResetQuery: vi.fn(),
};

const paginationState = {
  page: 1,
  pageSize: 20,
  totalItems: 1,
  totalPages: 1,
  startIndex: 0,
  handlePageChange: vi.fn(),
  handlePageSizeChange: vi.fn(),
  setPage: vi.fn(),
};

describe('PermissionTemplateListPanel', () => {
  it('renders the list panel without crashing and shows the available template', () => {
    render(
      <PermissionTemplateListPanel
        templates={[mockTemplate]}
        selectedId={mockTemplate.templateId}
        isLoading={false}
        error={null}
        viewMode="list"
        onViewModeChange={vi.fn()}
        queryState={queryState}
        paginationState={paginationState}
        onRegisterNew={vi.fn()}
        onSelectTemplate={vi.fn()}
        requiresFilter={false}
      />
    );

    expect(screen.getByText('Plantillas')).toBeInTheDocument();
    expect(screen.getByText('v1.0.0')).toBeInTheDocument();
    expect(screen.getByText('Admin')).toBeInTheDocument();
    expect(screen.getByText('UMS')).toBeInTheDocument();
  });
});
