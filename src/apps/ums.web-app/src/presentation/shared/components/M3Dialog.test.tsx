import React, { useState } from 'react';
import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { M3Dialog } from './M3Dialog';
import { M3TextField } from './M3TextField';

describe('M3Dialog', () => {
  const defaultProps = {
    open: true,
    title: 'Confirm Action',
    message: 'Are you sure you want to proceed?',
    actions: [
      { label: 'Cancel', variant: 'text' as const, onClick: vi.fn() },
      { label: 'Confirm', variant: 'filled' as const, onClick: vi.fn() },
    ],
  };

  it('renders when open is true', () => {
    render(<M3Dialog {...defaultProps} />);
    expect(screen.getByText('Confirm Action')).toBeInTheDocument();
    expect(screen.getByText('Are you sure you want to proceed?')).toBeInTheDocument();
  });

  it('does not render when open is false', () => {
    render(<M3Dialog {...defaultProps} open={false} />);
    expect(screen.queryByText('Confirm Action')).not.toBeInTheDocument();
  });

  it('renders action buttons', () => {
    render(<M3Dialog {...defaultProps} />);
    expect(screen.getByText('Cancel')).toBeInTheDocument();
    expect(screen.getByText('Confirm')).toBeInTheDocument();
  });

  it('calls action onClick when button is clicked', async () => {
    const user = userEvent.setup();
    const onConfirm = vi.fn();

    render(
      <M3Dialog
        {...defaultProps}
        actions={[
          { label: 'Cancel', variant: 'text', onClick: vi.fn() },
          { label: 'Confirm', variant: 'filled', onClick: onConfirm },
        ]}
      />
    );

    await user.click(screen.getByText('Confirm'));
    expect(onConfirm).toHaveBeenCalledTimes(1);
  });

  it('has correct ARIA attributes', () => {
    render(<M3Dialog {...defaultProps} />);
    const dialog = screen.getByRole('dialog');
    expect(dialog).toHaveAttribute('aria-modal', 'true');
    expect(dialog).toHaveAttribute('aria-labelledby');
    expect(dialog).toHaveAttribute('aria-describedby');
  });

  it('renders without message when message is omitted', () => {
    render(<M3Dialog {...defaultProps} message={undefined} />);
    expect(screen.getByText('Confirm Action')).toBeInTheDocument();
  });

  it('renders custom icon when provided', () => {
    render(<M3Dialog {...defaultProps} icon={<span data-testid="custom-icon">Custom</span>} />);
    expect(screen.getByTestId('custom-icon')).toBeInTheDocument();
  });

  it('hides icon when icon is null', () => {
    render(<M3Dialog {...defaultProps} icon={null} />);
    expect(screen.queryByRole('img')).not.toBeInTheDocument();
  });

  it('renders actions group with aria-label', () => {
    render(<M3Dialog {...defaultProps} />);
    expect(screen.getByRole('group', { name: 'Dialog actions' })).toBeInTheDocument();
  });

  // Regresión: un diálogo con campos tiene que dejar escribir. La trampa de foco se comía la
  // primera tecla y devolvía el foco al primer botón; con la barra espaciadora, ese botón se
  // pulsaba y el diálogo se cerraba solo.
  it('mantiene el foco en el campo mientras se escribe', async () => {
    const AnfitrionConCampo: React.FC = () => {
      const [texto, setTexto] = useState('');
      return (
        <M3Dialog open title="Cierre" actions={[{ label: 'Ok', onClick: vi.fn() }]}>
          <M3TextField
            label="Motivo del cierre"
            value={texto}
            onChange={e => setTexto(e.target.value)}
          />
        </M3Dialog>
      );
    };

    render(<AnfitrionConCampo />);
    const campo = screen.getByLabelText('Motivo del cierre');

    await userEvent.type(campo, 'Cese de operaciones');

    expect(campo).toHaveValue('Cese de operaciones');
    expect(document.activeElement).toBe(campo);
  });
});
