import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';
import { M3Tabs } from './M3Tabs';

describe('M3Tabs', () => {
  const tabs = [
    { id: 'tab1', label: 'Tab 1', content: <div>Content 1</div> },
    { id: 'tab2', label: 'Tab 2', content: <div>Content 2</div> },
    { id: 'tab3', label: 'Tab 3', content: <div>Content 3</div> },
  ];

  it('renders all tab labels', () => {
    render(<M3Tabs tabs={tabs} />);
    expect(screen.getByText('Tab 1')).toBeInTheDocument();
    expect(screen.getByText('Tab 2')).toBeInTheDocument();
    expect(screen.getByText('Tab 3')).toBeInTheDocument();
  });

  it('selects first tab by default', () => {
    render(<M3Tabs tabs={tabs} />);
    const tab1 = screen.getByRole('tab', { name: 'Tab 1' });
    expect(tab1).toHaveAttribute('aria-selected', 'true');
  });

  it('shows content of active tab', () => {
    render(<M3Tabs tabs={tabs} />);
    expect(screen.getByText('Content 1')).toBeInTheDocument();
    expect(screen.queryByText('Content 2')).not.toBeInTheDocument();
  });

  it('switches tabs when clicked', async () => {
    const user = userEvent.setup();
    render(<M3Tabs tabs={tabs} />);

    await user.click(screen.getByRole('tab', { name: 'Tab 2' }));

    expect(screen.getByRole('tab', { name: 'Tab 2' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByText('Content 2')).toBeInTheDocument();
    expect(screen.queryByText('Content 1')).not.toBeInTheDocument();
  });

  it('calls onChange when tab is clicked', async () => {
    const handleChange = vi.fn();
    const user = userEvent.setup();
    render(<M3Tabs tabs={tabs} onChange={handleChange} />);

    await user.click(screen.getByRole('tab', { name: 'Tab 2' }));

    expect(handleChange).toHaveBeenCalledWith('tab2');
  });

  it('selects defaultTabId when provided', () => {
    render(<M3Tabs tabs={tabs} defaultTabId="tab3" />);
    expect(screen.getByRole('tab', { name: 'Tab 3' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByText('Content 3')).toBeInTheDocument();
  });

  it('applies custom className', () => {
    render(<M3Tabs tabs={tabs} className="custom-class" />);
    const container = screen.getByRole('tab', { name: 'Tab 1' }).closest('.flex.flex-col');
    expect(container).toHaveClass('custom-class');
  });

  it('has role=tab on tab buttons', () => {
    render(<M3Tabs tabs={tabs} />);
    const tab = screen.getByRole('tab', { name: 'Tab 1' });
    expect(tab).toHaveAttribute('role', 'tab');
  });
});
