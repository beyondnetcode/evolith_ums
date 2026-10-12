import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';
import { InlineAddForm } from './InlineAddForm';

describe('InlineAddForm', () => {
  it('renders add button when closed', () => {
    render(
      <InlineAddForm
        isOpen={false}
        onToggle={() => {}}
        onSubmit={() => {}}
        addLabel="Add Item"
        title="New Item"
        submitLabel="Save"
      >
        <input />
      </InlineAddForm>
    );
    expect(screen.getByText('Add Item')).toBeInTheDocument();
  });

  it('renders form when open', () => {
    render(
      <InlineAddForm
        isOpen
        onToggle={() => {}}
        onSubmit={() => {}}
        addLabel="Add Item"
        title="New Item"
        submitLabel="Save"
      >
        <input data-testid="form-input" />
      </InlineAddForm>
    );
    expect(screen.getByText('New Item')).toBeInTheDocument();
    expect(screen.getByTestId('form-input')).toBeInTheDocument();
  });

  it('calls onToggle when add button is clicked', async () => {
    const handleToggle = vi.fn();
    const user = userEvent.setup();
    render(
      <InlineAddForm
        isOpen={false}
        onToggle={handleToggle}
        onSubmit={() => {}}
        addLabel="Add Item"
        title="New Item"
        submitLabel="Save"
      >
        <input />
      </InlineAddForm>
    );

    await user.click(screen.getByText('Add Item'));
    expect(handleToggle).toHaveBeenCalledWith(true);
  });

  it('calls onToggle when cancel is clicked', async () => {
    const handleToggle = vi.fn();
    const user = userEvent.setup();
    render(
      <InlineAddForm
        isOpen
        onToggle={handleToggle}
        onSubmit={() => {}}
        addLabel="Add Item"
        title="New Item"
        submitLabel="Save"
      >
        <input />
      </InlineAddForm>
    );

    const cancelBtn = screen.getByText('Cancelar');
    await user.click(cancelBtn);
    expect(handleToggle).toHaveBeenCalledWith(false);
  });

  it('calls onSubmit when form is submitted', async () => {
    const handleSubmit = vi.fn();
    const user = userEvent.setup();
    render(
      <InlineAddForm
        isOpen
        onToggle={() => {}}
        onSubmit={handleSubmit}
        addLabel="Add Item"
        title="New Item"
        submitLabel="Save"
      >
        <input />
      </InlineAddForm>
    );

    const submitBtn = screen.getByText('Save');
    await user.click(submitBtn);
    expect(handleSubmit).toHaveBeenCalled();
  });

  it('renders error message', () => {
    render(
      <InlineAddForm
        isOpen
        onToggle={() => {}}
        onSubmit={() => {}}
        addLabel="Add Item"
        title="New Item"
        submitLabel="Save"
        error="Something went wrong"
      >
        <input />
      </InlineAddForm>
    );
    expect(screen.getByText('Something went wrong')).toBeInTheDocument();
  });

  it('renders quiet trigger button', () => {
    render(
      <InlineAddForm
        isOpen={false}
        onToggle={() => {}}
        onSubmit={() => {}}
        addLabel="Add Item"
        title="New Item"
        submitLabel="Save"
        triggerEmphasis="quiet"
      >
        <input />
      </InlineAddForm>
    );
    expect(screen.getByText('Add Item')).toBeInTheDocument();
  });

  it('renders full width trigger', () => {
    const { container } = render(
      <InlineAddForm
        isOpen={false}
        onToggle={() => {}}
        onSubmit={() => {}}
        addLabel="Add Item"
        title="New Item"
        submitLabel="Save"
        fullWidth
      >
        <input />
      </InlineAddForm>
    );
    expect(container.innerHTML).toContain('w-full');
  });

  it('shows loading state on submit button', () => {
    render(
      <InlineAddForm
        isOpen
        onToggle={() => {}}
        onSubmit={() => {}}
        addLabel="Add Item"
        title="New Item"
        submitLabel="Save"
        isLoading
      >
        <input />
      </InlineAddForm>
    );
    const submitBtn = screen.getByRole('button', { name: /Guardando/i });
    expect(submitBtn).toBeDisabled();
  });

  it('renders custom icon', () => {
    render(
      <InlineAddForm
        isOpen
        onToggle={() => {}}
        onSubmit={() => {}}
        addLabel="Add Item"
        title="New Item"
        submitLabel="Save"
        icon={<span data-testid="custom-icon">★</span>}
      >
        <input />
      </InlineAddForm>
    );
    expect(screen.getByTestId('custom-icon')).toBeInTheDocument();
  });
});
