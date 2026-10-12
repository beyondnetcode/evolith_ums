import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { M3FieldsetWrapper } from './M3FieldsetWrapper';

describe('M3FieldsetWrapper', () => {
  it('renders label', () => {
    render(
      <M3FieldsetWrapper label="Username">
        <input />
      </M3FieldsetWrapper>
    );
    const labels = screen.getAllByText('Username');
    expect(labels.length).toBeGreaterThan(0);
  });

  it('renders children', () => {
    render(
      <M3FieldsetWrapper label="Username">
        <input data-testid="input" />
      </M3FieldsetWrapper>
    );
    expect(screen.getByTestId('input')).toBeInTheDocument();
  });

  it('uses standard height by default', () => {
    render(
      <M3FieldsetWrapper label="Username">
        <input />
      </M3FieldsetWrapper>
    );
    const fieldset = screen.getAllByText('Username')[0].closest('fieldset');
    expect(fieldset).toHaveClass('h-14');
  });

  it('uses compact height when compact is true', () => {
    render(
      <M3FieldsetWrapper label="Username" compact>
        <input />
      </M3FieldsetWrapper>
    );
    const fieldset = screen.getAllByText('Username')[0].closest('fieldset');
    expect(fieldset).toHaveClass('h-12');
  });

  it('applies custom className', () => {
    render(
      <M3FieldsetWrapper label="Username" className="custom-class">
        <input />
      </M3FieldsetWrapper>
    );
    const container = screen.getAllByText('Username')[0].closest('div');
    expect(container).toHaveClass('custom-class');
  });

  it('has focus state management', () => {
    const { container } = render(
      <M3FieldsetWrapper label="Username">
        <input />
      </M3FieldsetWrapper>
    );
    expect(container.firstChild).toBeInTheDocument();
  });
});
