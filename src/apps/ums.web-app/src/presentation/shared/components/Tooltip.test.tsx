import { render, screen, act, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { Tooltip, IconButton } from './Tooltip';

describe('Tooltip', () => {
  it('renders children', () => {
    render(
      <Tooltip content="Tooltip text">
        <button>Hover me</button>
      </Tooltip>
    );
    expect(screen.getByText('Hover me')).toBeInTheDocument();
  });

  it('does not render tooltip content initially', () => {
    render(
      <Tooltip content="Tooltip text" delay={0}>
        <button>Hover me</button>
      </Tooltip>
    );
    expect(screen.queryByText('Tooltip text')).not.toBeInTheDocument();
  });

  it('renders children without tooltip when content is empty', () => {
    render(
      <Tooltip content="">
        <button>No tooltip</button>
      </Tooltip>
    );
    expect(screen.getByText('No tooltip')).toBeInTheDocument();
    expect(screen.queryByRole('tooltip')).not.toBeInTheDocument();
  });

  it('shows tooltip on mouse enter after delay', async () => {
    render(
      <Tooltip content="Tooltip text" delay={0}>
        <button>Hover me</button>
      </Tooltip>
    );

    const trigger = screen.getByText('Hover me').closest('span');
    fireEvent.mouseEnter(trigger!);

    await act(async () => {
      await new Promise(r => setTimeout(r, 10));
    });

    expect(screen.getByRole('tooltip')).toHaveTextContent('Tooltip text');
  });

  it('hides tooltip on mouse leave', async () => {
    render(
      <Tooltip content="Tooltip text" delay={0}>
        <button>Hover me</button>
      </Tooltip>
    );

    const trigger = screen.getByText('Hover me').closest('span');
    fireEvent.mouseEnter(trigger!);

    await act(async () => {
      await new Promise(r => setTimeout(r, 10));
    });

    expect(screen.getByRole('tooltip')).toBeInTheDocument();

    fireEvent.mouseLeave(trigger!);

    expect(screen.queryByRole('tooltip')).not.toBeInTheDocument();
  });

  it('sets aria-describedby when visible', async () => {
    render(
      <Tooltip content="Tooltip text" delay={0}>
        <button>Hover me</button>
      </Tooltip>
    );

    const trigger = screen.getByText('Hover me').closest('span');
    fireEvent.mouseEnter(trigger!);

    await act(async () => {
      await new Promise(r => setTimeout(r, 10));
    });

    expect(trigger).toHaveAttribute('aria-describedby', 'tooltip-content');
  });
});

describe('IconButton', () => {
  it('renders button with children', () => {
    render(
      <IconButton tooltip="Click me">
        <span>Icon</span>
      </IconButton>
    );
    expect(screen.getByText('Icon')).toBeInTheDocument();
  });

  it('renders as a button element', () => {
    render(
      <IconButton tooltip="Click me">
        <span>Icon</span>
      </IconButton>
    );
    expect(screen.getByRole('button')).toBeInTheDocument();
  });

  it('passes through button props', () => {
    render(
      <IconButton tooltip="Click me" disabled>
        <span>Icon</span>
      </IconButton>
    );
    expect(screen.getByRole('button')).toBeDisabled();
  });

  it('applies custom className', () => {
    render(
      <IconButton tooltip="Click me" className="custom-class">
        <span>Icon</span>
      </IconButton>
    );
    expect(screen.getByRole('button')).toHaveClass('custom-class');
  });
});
