import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { PermissionSectionToolbar } from './PermissionSectionToolbar';

describe('PermissionSectionToolbar', () => {
  it('renders item count and label', () => {
    render(
      <PermissionSectionToolbar
        viewMode="list"
        onViewModeChange={() => {}}
        itemCount={3}
        itemLabel="permission"
        itemLabelPlural="permissions"
      />
    );
    expect(screen.getByText('3 permissions')).toBeInTheDocument();
  });

  it('renders singular label when count is 1', () => {
    render(
      <PermissionSectionToolbar
        viewMode="list"
        onViewModeChange={() => {}}
        itemCount={1}
        itemLabel="permission"
        itemLabelPlural="permissions"
      />
    );
    expect(screen.getByText('1 permission')).toBeInTheDocument();
  });

  it('renders filter dropdown when options provided', () => {
    render(
      <PermissionSectionToolbar
        viewMode="list"
        onViewModeChange={() => {}}
        itemCount={3}
        itemLabel="permission"
        itemLabelPlural="permissions"
        filterOptions={[{ label: 'Allowed', value: 'allowed' }]}
        activeFilter="allowed"
        onFilterChange={() => {}}
      />
    );
    expect(screen.getByText('Allowed')).toBeInTheDocument();
  });

  it('renders sort dropdown when options provided', () => {
    render(
      <PermissionSectionToolbar
        viewMode="list"
        onViewModeChange={() => {}}
        itemCount={3}
        itemLabel="permission"
        itemLabelPlural="permissions"
        sortOptions={[{ label: 'Name', value: 'name' }]}
        sortBy="name"
        onSortByChange={() => {}}
      />
    );
    expect(screen.getByText('Name')).toBeInTheDocument();
  });

  it('renders expand/collapse button when showExpandCollapse is true', () => {
    render(
      <PermissionSectionToolbar
        viewMode="list"
        onViewModeChange={() => {}}
        itemCount={3}
        itemLabel="permission"
        itemLabelPlural="permissions"
        showExpandCollapse
        allExpanded={false}
        onToggleExpandAll={() => {}}
      />
    );
    expect(screen.getByText(/Expandir/)).toBeInTheDocument();
  });

  it('shows collapse text when all expanded', () => {
    render(
      <PermissionSectionToolbar
        viewMode="list"
        onViewModeChange={() => {}}
        itemCount={3}
        itemLabel="permission"
        itemLabelPlural="permissions"
        showExpandCollapse
        allExpanded
        onToggleExpandAll={() => {}}
      />
    );
    expect(screen.getByText(/Colapsar/)).toBeInTheDocument();
  });

  it('uses tree view mode options when viewMode is tree', () => {
    render(
      <PermissionSectionToolbar
        viewMode="tree"
        onViewModeChange={() => {}}
        itemCount={3}
        itemLabel="permission"
        itemLabelPlural="permissions"
      />
    );
    expect(screen.getByText('3 permissions')).toBeInTheDocument();
  });

  it('uses custom viewModeOptions when provided', () => {
    render(
      <PermissionSectionToolbar
        viewMode="list"
        onViewModeChange={() => {}}
        itemCount={3}
        itemLabel="permission"
        itemLabelPlural="permissions"
        viewModeOptions={[{ value: 'custom', label: 'Custom' }]}
      />
    );
    expect(screen.getByText('3 permissions')).toBeInTheDocument();
  });
});
