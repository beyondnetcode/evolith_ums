import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';
import { M3Select } from './M3Select';

describe('M3Select', () => {
  it('renders with label', () => {
    render(
      <M3Select label="Category">
        <option value="">Select...</option>
        <option value="1">Option 1</option>
        <option value="2">Option 2</option>
      </M3Select>
    );
    expect(screen.getByLabelText('Category')).toBeInTheDocument();
  });

  it('renders select element with children', () => {
    render(
      <M3Select label="Category">
        <option value="1">Option 1</option>
        <option value="2">Option 2</option>
      </M3Select>
    );
    const select = screen.getByLabelText('Category');
    expect(select).toBeInTheDocument();
  });

  it('displays error message', () => {
    render(
      <M3Select label="Category" error="Required">
        <option value="">Select...</option>
      </M3Select>
    );
    expect(screen.getByText('Required')).toBeInTheDocument();
  });

  it('displays helper text', () => {
    render(
      <M3Select label="Category" helperText="Choose a category">
        <option value="">Select...</option>
      </M3Select>
    );
    expect(screen.getByText('Choose a category')).toBeInTheDocument();
  });

  it('shows error over helper text', () => {
    render(
      <M3Select label="Category" error="Required" helperText="Choose one">
        <option value="">Select...</option>
      </M3Select>
    );
    expect(screen.getByText('Required')).toBeInTheDocument();
    expect(screen.queryByText('Choose one')).not.toBeInTheDocument();
  });

  it('applies compact height', () => {
    render(
      <M3Select label="Category" compact>
        <option value="">Select...</option>
      </M3Select>
    );
    const fieldset = document.querySelector('fieldset');
    expect(fieldset).toHaveClass('h-12');
  });

  it('uses standard height by default', () => {
    render(
      <M3Select label="Category">
        <option value="">Select...</option>
      </M3Select>
    );
    const fieldset = document.querySelector('fieldset');
    expect(fieldset).toHaveClass('h-14');
  });

  it('applies disabled state', () => {
    render(
      <M3Select label="Category" disabled>
        <option value="">Select...</option>
      </M3Select>
    );
    expect(screen.getByLabelText('Category')).toBeDisabled();
  });

  it('applies custom className', () => {
    render(
      <M3Select label="Category" className="custom-class">
        <option value="">Select...</option>
      </M3Select>
    );
    const container = screen.getByLabelText('Category').closest('div');
    expect(container).toHaveClass('custom-class');
  });

  it('uses provided id', () => {
    render(
      <M3Select label="Category" id="custom-id">
        <option value="">Select...</option>
      </M3Select>
    );
    expect(screen.getByLabelText('Category')).toHaveAttribute('id', 'custom-id');
  });

  it('handles focus and blur', async () => {
    const user = userEvent.setup();
    render(
      <M3Select label="Category">
        <option value="">Select...</option>
      </M3Select>
    );
    const select = screen.getByLabelText('Category');
    await user.click(select);
    expect(select).toHaveFocus();
  });

  it('passes through select props', () => {
    render(
      <M3Select label="Category" required>
        <option value="">Select...</option>
        <option value="1">Option 1</option>
      </M3Select>
    );
    expect(screen.getByLabelText('Category')).toBeRequired();
  });
});
