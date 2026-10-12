import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { SystemSuiteForm } from './SystemSuiteForm';

// Contrato G-107: la descripción es requerida (el backend exige
// `Description.NotEmpty()`). Estos tests fijan que el formulario alinea la UI al
// contrato: indicador visual de requerido, mensaje de error y bloqueo del envío.

const mutateAsync = vi.fn();

vi.mock('@app/authorization/hooks/use-system-suite', () => ({
  useCreateSystemSuite: () => ({ mutateAsync, isPending: false }),
}));

vi.mock('@app/shared/hooks/use-effective-tenant', () => ({
  useEffectiveTenant: () => '5f4e3d2c-1b2a-3c4d-5e6f-7a8b9c0d1e2f',
}));

const renderForm = () => render(<SystemSuiteForm isOpen onClose={vi.fn()} onSuccess={vi.fn()} />);

const fill = (placeholder: string, value: string) => {
  fireEvent.change(screen.getByPlaceholderText(placeholder), { target: { value } });
};

describe('SystemSuiteForm', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mutateAsync.mockResolvedValue({ systemSuiteId: 'new-id' });
  });

  it('marca la descripción como campo requerido (asterisco visual)', () => {
    renderForm();
    const descLabel = screen.getByText('Descripción');
    expect(descLabel).toHaveTextContent('*');
  });

  it('no envía y muestra el error requerido cuando la descripción está vacía', async () => {
    renderForm();

    fill('SUITE_CRM', 'CRM');
    fill('CRM System Suite', 'CRM System');
    // Descripción se deja vacía a propósito.

    fireEvent.click(screen.getByRole('button', { name: /Registrar Suite/i }));

    expect(await screen.findByText('Descripción requerida')).toBeInTheDocument();
    expect(mutateAsync).not.toHaveBeenCalled();
  });

  it('no envía cuando la descripción es solo espacios en blanco', async () => {
    renderForm();

    fill('SUITE_CRM', 'CRM');
    fill('CRM System Suite', 'CRM System');
    fill('Customer relationship management module', '   ');

    fireEvent.click(screen.getByRole('button', { name: /Registrar Suite/i }));

    expect(await screen.findByText('Descripción requerida')).toBeInTheDocument();
    expect(mutateAsync).not.toHaveBeenCalled();
  });

  it('envía el payload con la descripción cuando el formulario es válido', async () => {
    renderForm();

    fill('SUITE_CRM', 'CRM');
    fill('CRM System Suite', 'CRM System');
    fill('Customer relationship management module', 'Gestión CRM');

    fireEvent.click(screen.getByRole('button', { name: /Registrar Suite/i }));

    await waitFor(() => expect(mutateAsync).toHaveBeenCalledTimes(1));
    expect(mutateAsync).toHaveBeenCalledWith(
      expect.objectContaining({
        tenantId: '5f4e3d2c-1b2a-3c4d-5e6f-7a8b9c0d1e2f',
        code: 'CRM',
        name: 'CRM System',
        description: 'Gestión CRM',
      })
    );
  });
});
