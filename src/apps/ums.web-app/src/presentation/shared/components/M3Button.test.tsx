import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';
import { M3Button } from './M3Button';

describe('M3Button', () => {
  it('renders button with children', () => {
    render(<M3Button>Click me</M3Button>);
    expect(screen.getByRole('button', { name: 'Click me' })).toBeInTheDocument();
  });

  it('uses filled variant by default', () => {
    render(<M3Button>Default</M3Button>);
    const button = screen.getByRole('button');
    expect(button).toHaveClass('bg-m3-primary');
  });

  it('applies tonal variant', () => {
    render(<M3Button variant="tonal">Tonal</M3Button>);
    const button = screen.getByRole('button');
    expect(button).toHaveClass('bg-m3-primary-container');
  });

  it('applies outlined variant', () => {
    render(<M3Button variant="outlined">Outlined</M3Button>);
    const button = screen.getByRole('button');
    expect(button).toHaveClass('border-m3-outline');
  });

  it('applies text variant', () => {
    render(<M3Button variant="text">Text</M3Button>);
    const button = screen.getByRole('button');
    expect(button).toHaveClass('text-m3-primary');
    expect(button).not.toHaveClass('bg-m3-primary');
  });

  it('applies fab variant', () => {
    render(<M3Button variant="fab">FAB</M3Button>);
    const button = screen.getByRole('button');
    expect(button).toHaveClass('rounded-2xl');
  });

  it('renders left icon', () => {
    render(
      <M3Button icon={<span data-testid="icon">★</span>} iconPosition="left">
        With Icon
      </M3Button>
    );
    expect(screen.getByTestId('icon')).toBeInTheDocument();
  });

  it('renders right icon', () => {
    render(
      <M3Button icon={<span data-testid="icon">★</span>} iconPosition="right">
        With Icon
      </M3Button>
    );
    expect(screen.getByTestId('icon')).toBeInTheDocument();
  });

  it('shows spinner when loading', () => {
    render(<M3Button loading>Loading</M3Button>);
    const button = screen.getByRole('button');
    expect(button).toBeDisabled();
    const spinner = document.querySelector('svg.animate-spin');
    expect(spinner).toBeInTheDocument();
  });

  it('hides icon when loading', () => {
    render(
      <M3Button icon={<span data-testid="icon">★</span>} loading>
        Loading
      </M3Button>
    );
    expect(screen.queryByTestId('icon')).not.toBeInTheDocument();
  });

  it('is disabled when disabled prop is true', () => {
    render(<M3Button disabled>Disabled</M3Button>);
    expect(screen.getByRole('button')).toBeDisabled();
  });

  it('handles click events', async () => {
    const handleClick = vi.fn();
    const user = userEvent.setup();
    render(<M3Button onClick={handleClick}>Click</M3Button>);

    await user.click(screen.getByRole('button'));
    expect(handleClick).toHaveBeenCalledTimes(1);
  });

  it('applies custom className', () => {
    render(<M3Button className="custom-class">Custom</M3Button>);
    expect(screen.getByRole('button')).toHaveClass('custom-class');
  });

  it('passes through button props', () => {
    render(<M3Button type="submit">Submit</M3Button>);
    expect(screen.getByRole('button')).toHaveAttribute('type', 'submit');
  });

  it('has focus-visible ring styles', () => {
    render(<M3Button>Focusable</M3Button>);
    const button = screen.getByRole('button');
    expect(button).toHaveClass('focus-visible:ring-2');
  });
});
