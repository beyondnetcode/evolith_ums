import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { ChildEntityToolbar } from './ChildEntityToolbar';

describe('ChildEntityToolbar', () => {
  it('renders item count and label', () => {
    render(
      <ChildEntityToolbar
        viewMode="list"
        onViewModeChange={() => {}}
        itemCount={5}
        itemLabel="role"
      />
    );
    expect(screen.getByText('5 roles')).toBeInTheDocument();
  });

  it('renders singular label when count is 1', () => {
    render(
      <ChildEntityToolbar
        viewMode="list"
        onViewModeChange={() => {}}
        itemCount={1}
        itemLabel="role"
      />
    );
    expect(screen.getByText('1 role')).toBeInTheDocument();
  });

  it('renders filter dropdown when options provided', () => {
    render(
      <ChildEntityToolbar
        viewMode="list"
        onViewModeChange={() => {}}
        itemCount={5}
        itemLabel="role"
        filterOptions={[{ label: 'Active', value: 'active' }]}
        activeFilter="active"
        onFilterChange={() => {}}
      />
    );
    expect(screen.getByText('Active')).toBeInTheDocument();
  });

  it('renders sort dropdown when options provided', () => {
    render(
      <ChildEntityToolbar
        viewMode="list"
        onViewModeChange={() => {}}
        itemCount={5}
        itemLabel="role"
        sortOptions={[{ label: 'Name', value: 'name' }]}
        sortBy="name"
        onSortByChange={() => {}}
      />
    );
    expect(screen.getByText('Name')).toBeInTheDocument();
  });

  it('renders sort order toggle button', () => {
    render(
      <ChildEntityToolbar
        viewMode="list"
        onViewModeChange={() => {}}
        itemCount={5}
        itemLabel="role"
        sortOptions={[{ label: 'Name', value: 'name' }]}
        sortBy="name"
        onSortByChange={() => {}}
        sortOrder="asc"
        onSortOrderToggle={() => {}}
      />
    );
    const toggleBtn = screen.getByTitle('Ascending');
    expect(toggleBtn).toBeInTheDocument();
  });

  it('does not render filter when options not provided', () => {
    render(
      <ChildEntityToolbar
        viewMode="list"
        onViewModeChange={() => {}}
        itemCount={5}
        itemLabel="role"
      />
    );
    expect(screen.queryByRole('combobox')).not.toBeInTheDocument();
  });
});
