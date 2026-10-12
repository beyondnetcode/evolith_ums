import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';
import { DataList } from './DataList';

describe('DataList', () => {
  it('renders list content when not loading and not empty', () => {
    render(
      <DataList
        isLoading={false}
        isEmpty={false}
        renderList={() => <div data-testid="list-content">List items</div>}
      />
    );
    expect(screen.getByTestId('list-content')).toBeInTheDocument();
  });

  it('renders skeleton rows when loading', () => {
    render(<DataList isLoading isEmpty={false} renderList={() => <div>Content</div>} />);
    const skeletons = document.querySelectorAll('[aria-hidden="true"]');
    expect(skeletons.length).toBeGreaterThan(0);
  });

  it('renders empty state when empty', () => {
    render(<DataList isLoading={false} isEmpty renderList={() => <div>Content</div>} />);
    expect(screen.getByText('No results found')).toBeInTheDocument();
    expect(screen.getByText('No matching records found.')).toBeInTheDocument();
  });

  it('renders custom empty labels', () => {
    render(
      <DataList
        isLoading={false}
        isEmpty
        emptyLabel="Custom empty message"
        emptyTitle="Custom title"
        renderList={() => <div>Content</div>}
      />
    );
    expect(screen.getByText('Custom title')).toBeInTheDocument();
    expect(screen.getByText('Custom empty message')).toBeInTheDocument();
  });

  it('renders thumbnail view when viewMode is thumbnail', () => {
    render(
      <DataList
        isLoading={false}
        isEmpty={false}
        viewMode="thumbnail"
        renderList={() => <div>List</div>}
        renderThumbnail={() => <div data-testid="thumbnail">Thumbnail</div>}
      />
    );
    expect(screen.getByTestId('thumbnail')).toBeInTheDocument();
    expect(screen.queryByText('List')).not.toBeInTheDocument();
  });

  it('falls back to list when thumbnail not provided', () => {
    render(
      <DataList
        isLoading={false}
        isEmpty={false}
        viewMode="thumbnail"
        renderList={() => <div data-testid="list">List</div>}
      />
    );
    expect(screen.getByTestId('list')).toBeInTheDocument();
  });

  it('renders pagination when provided', () => {
    render(
      <DataList
        isLoading={false}
        isEmpty={false}
        renderList={() => <div>Content</div>}
        pagination={{
          page: 1,
          pageSize: 10,
          totalItems: 50,
          totalPages: 5,
          onPageChange: () => {},
        }}
      />
    );
    expect(screen.getByText('50 records · 10 per page')).toBeInTheDocument();
  });

  it('renders page numbers', () => {
    render(
      <DataList
        isLoading={false}
        isEmpty={false}
        renderList={() => <div>Content</div>}
        pagination={{
          page: 1,
          pageSize: 10,
          totalItems: 30,
          totalPages: 3,
          onPageChange: () => {},
        }}
      />
    );
    expect(screen.getByText('1')).toBeInTheDocument();
    expect(screen.getByText('2')).toBeInTheDocument();
    expect(screen.getByText('3')).toBeInTheDocument();
  });

  it('calls onPageChange when page button is clicked', async () => {
    const handlePageChange = vi.fn();
    const user = userEvent.setup();
    render(
      <DataList
        isLoading={false}
        isEmpty={false}
        renderList={() => <div>Content</div>}
        pagination={{
          page: 1,
          pageSize: 10,
          totalItems: 30,
          totalPages: 3,
          onPageChange: handlePageChange,
        }}
      />
    );

    await user.click(screen.getByText('2'));
    expect(handlePageChange).toHaveBeenCalledWith(2);
  });

  it('disables previous button on first page', () => {
    render(
      <DataList
        isLoading={false}
        isEmpty={false}
        renderList={() => <div>Content</div>}
        pagination={{
          page: 1,
          pageSize: 10,
          totalItems: 30,
          totalPages: 3,
          onPageChange: () => {},
        }}
      />
    );
    const buttons = document.querySelectorAll('button');
    const prevChevron = buttons[1];
    expect(prevChevron).toBeDisabled();
  });

  it('disables next button on last page', () => {
    render(
      <DataList
        isLoading={false}
        isEmpty={false}
        renderList={() => <div>Content</div>}
        pagination={{
          page: 3,
          pageSize: 10,
          totalItems: 30,
          totalPages: 3,
          onPageChange: () => {},
        }}
      />
    );
    const chevronButtons = document.querySelectorAll('button');
    const nextChevron = chevronButtons[chevronButtons.length - 1];
    expect(nextChevron).toBeDisabled();
  });

  it('renders telemetry info instead of pagination info', () => {
    render(
      <DataList
        isLoading={false}
        isEmpty={false}
        renderList={() => <div>Content</div>}
        telemetryInfo={<span data-testid="telemetry">Custom telemetry</span>}
        pagination={{
          page: 1,
          pageSize: 10,
          totalItems: 50,
          totalPages: 5,
          onPageChange: () => {},
        }}
      />
    );
    expect(screen.getByTestId('telemetry')).toBeInTheDocument();
    expect(screen.queryByText('50 records · 10 per page')).not.toBeInTheDocument();
  });

  it('does not render pagination footer when totalPages is 1', () => {
    render(
      <DataList
        isLoading={false}
        isEmpty={false}
        renderList={() => <div>Content</div>}
        pagination={{
          page: 1,
          pageSize: 10,
          totalItems: 5,
          totalPages: 1,
          onPageChange: () => {},
        }}
      />
    );
    expect(screen.getByText('5 records · 10 per page')).toBeInTheDocument();
    expect(screen.queryByText('1')).not.toBeInTheDocument();
  });
});
