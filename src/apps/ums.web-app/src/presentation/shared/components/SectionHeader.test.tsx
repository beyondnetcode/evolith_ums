import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { SectionHeader } from './SectionHeader';

describe('SectionHeader', () => {
  it('renders title', () => {
    render(<SectionHeader title="Settings" />);
    expect(screen.getByText('Settings')).toBeInTheDocument();
  });

  it('renders subtitle when provided', () => {
    render(<SectionHeader title="Settings" subtitle="Configure your preferences" />);
    expect(screen.getByText('Configure your preferences')).toBeInTheDocument();
  });

  it('does not render subtitle when not provided', () => {
    render(<SectionHeader title="Settings" />);
    expect(screen.queryByText('Configure your preferences')).not.toBeInTheDocument();
  });

  it('renders actions when provided', () => {
    render(
      <SectionHeader title="Settings" actions={<button data-testid="action-btn">Edit</button>} />
    );
    expect(screen.getByTestId('action-btn')).toBeInTheDocument();
  });

  it('does not render actions when not provided', () => {
    render(<SectionHeader title="Settings" />);
    expect(screen.queryByTestId('action-btn')).not.toBeInTheDocument();
  });

  it('applies custom className', () => {
    const { container } = render(<SectionHeader title="Settings" className="custom-class" />);
    expect(container.firstChild).toHaveClass('custom-class');
  });

  it('has bottom border', () => {
    const { container } = render(<SectionHeader title="Settings" />);
    expect(container.firstChild).toHaveClass('border-b');
  });
});
