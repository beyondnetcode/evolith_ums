import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';
import { M3SegmentedButton } from './M3SegmentedButton';

describe('M3SegmentedButton', () => {
  const options = [
    { value: 'list', label: 'List' },
    { value: 'grid', label: 'Grid' },
    { value: 'table', label: 'Table' },
  ];

  it('renders all options', () => {
    render(<M3SegmentedButton options={options} value="list" onChange={() => {}} />);
    expect(screen.getByText('List')).toBeInTheDocument();
    expect(screen.getByText('Grid')).toBeInTheDocument();
    expect(screen.getByText('Table')).toBeInTheDocument();
  });

  it('highlights active option', () => {
    render(<M3SegmentedButton options={options} value="grid" onChange={() => {}} />);
    const activeButton = screen.getByText('Grid');
    expect(activeButton).toHaveClass('bg-m3-primary');
  });

  it('calls onChange when option is clicked', async () => {
    const handleChange = vi.fn();
    const user = userEvent.setup();
    render(<M3SegmentedButton options={options} value="list" onChange={handleChange} />);

    await user.click(screen.getByText('Grid'));
    expect(handleChange).toHaveBeenCalledWith('grid');
  });

  it('applies sm size', () => {
    render(<M3SegmentedButton options={options} value="list" onChange={() => {}} size="sm" />);
    const button = screen.getByText('List');
    expect(button).toHaveClass('text-[10px]');
  });

  it('applies md size by default', () => {
    render(<M3SegmentedButton options={options} value="list" onChange={() => {}} />);
    const button = screen.getByText('List');
    expect(button).toHaveClass('text-[11px]');
  });

  it('applies custom className', () => {
    const { container } = render(
      <M3SegmentedButton
        options={options}
        value="list"
        onChange={() => {}}
        className="custom-class"
      />
    );
    expect(container.innerHTML).toContain('custom-class');
  });

  it('renders with icon labels', () => {
    render(
      <M3SegmentedButton
        options={[
          { value: 'a', label: <span data-testid="icon-a">A</span> },
          { value: 'b', label: <span data-testid="icon-b">B</span> },
        ]}
        value="a"
        onChange={() => {}}
      />
    );
    expect(screen.getByTestId('icon-a')).toBeInTheDocument();
    expect(screen.getByTestId('icon-b')).toBeInTheDocument();
  });
});
