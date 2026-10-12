import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';
import { SearchBar } from './SearchBar';

describe('SearchBar', () => {
  it('renders search input', () => {
    render(<SearchBar searchValue="" onSearchValueChange={() => {}} onSubmit={() => {}} />);
    expect(screen.getByLabelText('Término de búsqueda')).toBeInTheDocument();
  });

  it('renders search button', () => {
    render(<SearchBar searchValue="" onSearchValueChange={() => {}} onSubmit={() => {}} />);
    expect(screen.getByText('Buscar')).toBeInTheDocument();
  });

  it('calls onSearchValueChange when input changes', async () => {
    const handleChange = vi.fn();
    const user = userEvent.setup();
    render(<SearchBar searchValue="" onSearchValueChange={handleChange} onSubmit={() => {}} />);

    const input = screen.getByLabelText('Término de búsqueda');
    await user.type(input, 'test');

    expect(handleChange).toHaveBeenCalled();
  });

  it('calls onSubmit when form is submitted', async () => {
    const handleSubmit = vi.fn();
    const user = userEvent.setup();
    render(<SearchBar searchValue="" onSearchValueChange={() => {}} onSubmit={handleSubmit} />);

    const button = screen.getByText('Buscar');
    await user.click(button);

    expect(handleSubmit).toHaveBeenCalled();
  });

  it('renders criteria dropdown when options provided', () => {
    render(
      <SearchBar
        searchValue=""
        onSearchValueChange={() => {}}
        onSubmit={() => {}}
        criteriaOptions={[
          { label: 'Name', value: 'name' },
          { label: 'Email', value: 'email' },
        ]}
        activeCriteria="name"
        onCriteriaChange={() => {}}
      />
    );
    expect(screen.getByLabelText('Criterio')).toBeInTheDocument();
  });

  it('uses custom placeholder', () => {
    const { container } = render(
      <SearchBar
        searchValue=""
        onSearchValueChange={() => {}}
        onSubmit={() => {}}
        searchPlaceholder="Find users..."
      />
    );
    expect(container.innerHTML).toContain('Find users...');
  });

  it('uses custom labels', () => {
    render(
      <SearchBar
        searchValue=""
        onSearchValueChange={() => {}}
        onSubmit={() => {}}
        searchTermLabel="Query"
        searchButtonLabel="Go"
      />
    );
    expect(screen.getByLabelText('Query')).toBeInTheDocument();
    expect(screen.getByText('Go')).toBeInTheDocument();
  });
});
