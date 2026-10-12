import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';
import { M3Switch } from './M3Switch';

describe('M3Switch', () => {
  it('renders a switch button', () => {
    render(<M3Switch checked={false} onChange={() => {}} />);
    const switchEl = screen.getByRole('switch');
    expect(switchEl).toBeInTheDocument();
  });

  it('shows unchecked state', () => {
    render(<M3Switch checked={false} onChange={() => {}} />);
    const switchEl = screen.getByRole('switch');
    expect(switchEl).toHaveAttribute('aria-checked', 'false');
  });

  it('shows checked state', () => {
    render(<M3Switch checked onChange={() => {}} />);
    const switchEl = screen.getByRole('switch');
    expect(switchEl).toHaveAttribute('aria-checked', 'true');
  });

  it('calls onChange when clicked', async () => {
    const handleChange = vi.fn();
    const user = userEvent.setup();
    render(<M3Switch checked={false} onChange={handleChange} />);

    await user.click(screen.getByRole('switch'));
    expect(handleChange).toHaveBeenCalledWith(true);
  });

  it('calls onChange with false when currently checked', async () => {
    const handleChange = vi.fn();
    const user = userEvent.setup();
    render(<M3Switch checked onChange={handleChange} />);

    await user.click(screen.getByRole('switch'));
    expect(handleChange).toHaveBeenCalledWith(false);
  });

  it('does not call onChange when disabled', async () => {
    const handleChange = vi.fn();
    const user = userEvent.setup();
    render(<M3Switch checked={false} onChange={handleChange} disabled />);

    await user.click(screen.getByRole('switch'));
    expect(handleChange).not.toHaveBeenCalled();
  });

  it('is disabled when disabled prop is true', () => {
    render(<M3Switch checked={false} onChange={() => {}} disabled />);
    const switchEl = screen.getByRole('switch');
    expect(switchEl).toBeDisabled();
  });

  it('renders label when provided', () => {
    render(<M3Switch checked={false} onChange={() => {}} label="Enable Feature" />);
    expect(screen.getByText('Enable Feature')).toBeInTheDocument();
  });

  it('does not render label when not provided', () => {
    render(<M3Switch checked={false} onChange={() => {}} />);
    expect(screen.queryByRole('label')).not.toBeInTheDocument();
  });

  it('uses provided id', () => {
    render(<M3Switch checked={false} onChange={() => {}} id="custom-id" label="Test" />);
    const switchEl = screen.getByRole('switch');
    expect(switchEl).toHaveAttribute('id', 'custom-id');
  });

  it('links label to switch via htmlFor', () => {
    render(<M3Switch checked={false} onChange={() => {}} id="my-switch" label="Test" />);
    const label = screen.getByText('Test');
    expect(label).toHaveAttribute('for', 'my-switch');
  });
});
