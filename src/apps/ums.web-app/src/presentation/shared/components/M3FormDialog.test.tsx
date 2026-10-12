import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';
import { M3FormDialog } from './M3FormDialog';

describe('M3FormDialog', () => {
  it('renders when open is true', () => {
    render(
      <M3FormDialog open onClose={() => {}} title="Create Item" footer={<button>Save</button>}>
        <input data-testid="input" />
      </M3FormDialog>
    );
    expect(screen.getByText('Create Item')).toBeInTheDocument();
    expect(screen.getByTestId('input')).toBeInTheDocument();
    expect(screen.getByText('Save')).toBeInTheDocument();
  });

  it('does not render when open is false', () => {
    render(
      <M3FormDialog
        open={false}
        onClose={() => {}}
        title="Create Item"
        footer={<button>Save</button>}
      >
        <input data-testid="input" />
      </M3FormDialog>
    );
    expect(screen.queryByText('Create Item')).not.toBeInTheDocument();
  });

  it('closes when scrim is clicked', async () => {
    const handleClose = vi.fn();
    const user = userEvent.setup();
    render(
      <M3FormDialog open onClose={handleClose} title="Dialog" footer={<button>Save</button>}>
        <p>Content</p>
      </M3FormDialog>
    );

    const scrim = screen.getByText('Content').parentElement?.parentElement?.previousElementSibling;
    if (scrim) {
      await user.click(scrim);
      expect(handleClose).toHaveBeenCalled();
    }
  });

  it('closes when close button is clicked', async () => {
    const handleClose = vi.fn();
    const user = userEvent.setup();
    render(
      <M3FormDialog open onClose={handleClose} title="Dialog" footer={<button>Save</button>}>
        <p>Content</p>
      </M3FormDialog>
    );

    // Buscaba el botón por nombre VACÍO, que era la forma de encontrarlo cuando no tenía ninguno:
    // la prueba fijaba el defecto en vez de detectarlo. Ahora se busca por su nombre accesible.
    const closeBtn = screen.getByRole('button', { name: 'Cerrar' });
    await user.click(closeBtn);
    expect(handleClose).toHaveBeenCalled();
  });

  it('renders icon when provided', () => {
    render(
      <M3FormDialog
        open
        onClose={() => {}}
        title="Dialog"
        icon={<span data-testid="icon">★</span>}
        footer={<button>Save</button>}
      >
        <p>Content</p>
      </M3FormDialog>
    );
    expect(screen.getByTestId('icon')).toBeInTheDocument();
  });

  it('applies custom maxWidth', () => {
    const { container } = render(
      <M3FormDialog
        open
        onClose={() => {}}
        title="Dialog"
        maxWidth="max-w-xl"
        footer={<button>Save</button>}
      >
        <p>Content</p>
      </M3FormDialog>
    );
    expect(container.innerHTML).toContain('max-w-xl');
  });

  it('uses default max-w-lg', () => {
    const { container } = render(
      <M3FormDialog open onClose={() => {}} title="Dialog" footer={<button>Save</button>}>
        <p>Content</p>
      </M3FormDialog>
    );
    expect(container.innerHTML).toContain('max-w-lg');
  });

  it('renders footer content', () => {
    render(
      <M3FormDialog
        open
        onClose={() => {}}
        title="Dialog"
        footer={
          <>
            <button>Cancel</button>
            <button>Save</button>
          </>
        }
      >
        <p>Content</p>
      </M3FormDialog>
    );
    expect(screen.getByText('Cancel')).toBeInTheDocument();
    expect(screen.getByText('Save')).toBeInTheDocument();
  });
});
