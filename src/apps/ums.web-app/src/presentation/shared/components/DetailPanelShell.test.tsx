import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { DetailPanelShell } from './DetailPanelShell';

describe('DetailPanelShell', () => {
  it('renders loading state', () => {
    render(
      <DetailPanelShell isLoading isEmpty={false} tabs={[]} activeTab="tab1" onTabChange={() => {}}>
        <div>Content</div>
      </DetailPanelShell>
    );
    expect(screen.getByText('Cargando...')).toBeInTheDocument();
  });

  it('renders custom loading label', () => {
    render(
      <DetailPanelShell
        isLoading
        isEmpty={false}
        loadingLabel="Cargando..."
        tabs={[]}
        activeTab="tab1"
        onTabChange={() => {}}
      >
        <div>Content</div>
      </DetailPanelShell>
    );
    expect(screen.getByText('Cargando...')).toBeInTheDocument();
  });

  it('renders empty state', () => {
    render(
      <DetailPanelShell isLoading={false} isEmpty tabs={[]} activeTab="tab1" onTabChange={() => {}}>
        <div>Content</div>
      </DetailPanelShell>
    );
    expect(screen.getByText('Seleccione un elemento para ver los detalles.')).toBeInTheDocument();
  });

  it('renders custom empty label', () => {
    render(
      <DetailPanelShell
        isLoading={false}
        isEmpty
        emptyLabel="No hay datos"
        tabs={[]}
        activeTab="tab1"
        onTabChange={() => {}}
      >
        <div>Content</div>
      </DetailPanelShell>
    );
    expect(screen.getByText('No hay datos')).toBeInTheDocument();
  });

  it('renders header when provided', () => {
    render(
      <DetailPanelShell
        isLoading={false}
        isEmpty={false}
        header={<div data-testid="header">Header</div>}
        tabs={[]}
        activeTab="tab1"
        onTabChange={() => {}}
      >
        <div>Content</div>
      </DetailPanelShell>
    );
    expect(screen.getByTestId('header')).toBeInTheDocument();
  });

  it('renders banner when provided', () => {
    render(
      <DetailPanelShell
        isLoading={false}
        isEmpty={false}
        banner={<div data-testid="banner">Banner</div>}
        tabs={[]}
        activeTab="tab1"
        onTabChange={() => {}}
      >
        <div>Content</div>
      </DetailPanelShell>
    );
    expect(screen.getByTestId('banner')).toBeInTheDocument();
  });

  it('renders tabs', () => {
    render(
      <DetailPanelShell
        isLoading={false}
        isEmpty={false}
        tabs={[
          { key: 'tab1', label: 'Tab 1' },
          { key: 'tab2', label: 'Tab 2' },
        ]}
        activeTab="tab1"
        onTabChange={() => {}}
      >
        <div>Content</div>
      </DetailPanelShell>
    );
    expect(screen.getByText('Tab 1')).toBeInTheDocument();
    expect(screen.getByText('Tab 2')).toBeInTheDocument();
  });

  it('renders children content', () => {
    render(
      <DetailPanelShell
        isLoading={false}
        isEmpty={false}
        tabs={[]}
        activeTab="tab1"
        onTabChange={() => {}}
      >
        <div data-testid="content">Content</div>
      </DetailPanelShell>
    );
    expect(screen.getByTestId('content')).toBeInTheDocument();
  });

  it('uses entityKey for re-rendering', () => {
    const { rerender } = render(
      <DetailPanelShell
        isLoading={false}
        isEmpty={false}
        tabs={[]}
        activeTab="tab1"
        onTabChange={() => {}}
        entityKey="entity-1"
      >
        <div data-testid="content">Content</div>
      </DetailPanelShell>
    );
    expect(screen.getByTestId('content')).toBeInTheDocument();
    rerender(
      <DetailPanelShell
        isLoading={false}
        isEmpty={false}
        tabs={[]}
        activeTab="tab1"
        onTabChange={() => {}}
        entityKey="entity-2"
      >
        <div data-testid="content">New Content</div>
      </DetailPanelShell>
    );
    expect(screen.getByText('New Content')).toBeInTheDocument();
  });
});
