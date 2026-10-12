import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import React from 'react';
import { UserAccountProfileCard } from './UserAccountProfileCard';
import { UserAccount } from '@domain/identity/models/user-account.model';

const mockAccount: UserAccount = {
  userAccountId: 'user-12345',
  tenantId: 'tenant-12345',
  branchId: null,
  email: 'operator@ransa.pe',
  category: 'Internal',
  status: 'Active',
  identityReference: 'EMP-99',
  identityReferenceType: 'HrId',
};

describe('UserAccountProfileCard', () => {
  it('renders user details correctly', () => {
    render(
      <UserAccountProfileCard
        account={mockAccount}
        onActivate={() => {}}
        onBlock={() => {}}
        onRestore={() => {}}
        onAccountUpdate={() => {}}
      />
    );

    expect(screen.getAllByText('operator@ransa.pe')[0]).toBeInTheDocument();
    expect(screen.getAllByText('Internal')[0]).toBeInTheDocument();
    expect(screen.getByText('Activo')).toBeInTheDocument(); // Active status label resolved by useStatusLabel
    expect(screen.getByText('EMP-99')).toBeInTheDocument();
  });

  it('triggers inline editing mode on double click', async () => {
    const onAccountUpdate = vi.fn();
    const user = userEvent.setup();
    render(
      <UserAccountProfileCard
        account={mockAccount}
        onActivate={() => {}}
        onBlock={() => {}}
        onRestore={() => {}}
        onAccountUpdate={onAccountUpdate}
      />
    );

    const emailElement = screen.getAllByText('operator@ransa.pe')[0];
    expect(emailElement).toBeInTheDocument();

    // Double click directly on the email element to trigger editing (event bubbles up)
    fireEvent.doubleClick(emailElement);

    // Should display Edit User Account form title
    expect(screen.getByText('Editar Cuenta de Usuario')).toBeInTheDocument();

    // Verify input email is rendered with initial value
    const emailInput = screen.getByLabelText(/Correo Electrónico/i) as HTMLInputElement;
    expect(emailInput.value).toBe('operator@ransa.pe');

    // Change email
    await user.clear(emailInput);
    await user.type(emailInput, 'supervisor@ransa.pe');

    // Click save
    const saveButton = screen.getByText(/Guardar/i);
    await user.click(saveButton);

    expect(onAccountUpdate).toHaveBeenCalledWith('user-12345', {
      email: 'supervisor@ransa.pe',
      category: 'Internal',
    });
  });
});
