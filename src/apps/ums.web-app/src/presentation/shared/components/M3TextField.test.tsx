import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';
import { M3TextField } from './M3TextField';

describe('M3TextField', () => {
  it('renders with label', () => {
    render(<M3TextField label="Username" />);
    expect(screen.getByLabelText('Username')).toBeInTheDocument();
  });

  it('renders input element', () => {
    render(<M3TextField label="Email" type="email" />);
    const input = screen.getByLabelText('Email');
    expect(input).toHaveAttribute('type', 'email');
  });

  it('displays error message', () => {
    render(<M3TextField label="Email" error="Invalid email" />);
    expect(screen.getByText('Invalid email')).toBeInTheDocument();
  });

  it('displays helper text', () => {
    render(<M3TextField label="Username" helperText="Must be 3-20 characters" />);
    expect(screen.getByText('Must be 3-20 characters')).toBeInTheDocument();
  });

  it('shows error over helper text', () => {
    render(<M3TextField label="Username" error="Required" helperText="Enter username" />);
    expect(screen.getByText('Required')).toBeInTheDocument();
    expect(screen.queryByText('Enter username')).not.toBeInTheDocument();
  });

  it('applies required indicator', () => {
    render(<M3TextField label="Email" required />);
    const input = screen.getByLabelText(/Email/);
    expect(input).toBeRequired();
  });

  it('applies custom className', () => {
    render(<M3TextField label="Test" className="custom-class" />);
    const container = screen.getByLabelText('Test').closest('div');
    expect(container).toHaveClass('custom-class');
  });

  it('uses compact height', () => {
    render(<M3TextField label="Compact" compact />);
    const fieldset = document.querySelector('fieldset');
    expect(fieldset).toHaveClass('h-12');
  });

  it('uses dense height', () => {
    render(<M3TextField label="Dense" dense />);
    const fieldset = document.querySelector('fieldset');
    expect(fieldset).toHaveClass('h-10');
  });

  it('renders start icon', () => {
    render(
      <M3TextField label="Search" icon={<span data-testid="icon">🔍</span>} iconPosition="start" />
    );
    expect(screen.getByTestId('icon')).toBeInTheDocument();
  });

  it('renders end icon', () => {
    render(
      <M3TextField label="Search" icon={<span data-testid="icon">🔍</span>} iconPosition="end" />
    );
    expect(screen.getByTestId('icon')).toBeInTheDocument();
  });

  it('handles focus and blur', async () => {
    const user = userEvent.setup();
    render(<M3TextField label="Input" />);
    const input = screen.getByLabelText('Input');

    await user.click(input);
    expect(input).toHaveFocus();
  });

  it('passes through input props', () => {
    render(<M3TextField label="Name" maxLength={50} />);
    const input = screen.getByLabelText('Name');
    expect(input).toHaveAttribute('maxLength', '50');
  });

  it('floats label for date type', () => {
    render(<M3TextField label="Date" type="date" />);
    const label = document.querySelector('label');
    expect(label).toHaveClass('text-xs');
  });

  it('uses provided id', () => {
    render(<M3TextField label="Test" id="custom-id" />);
    const input = screen.getByLabelText('Test');
    expect(input).toHaveAttribute('id', 'custom-id');
  });
});
