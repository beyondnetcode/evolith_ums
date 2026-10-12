import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { MainLayout } from './MainLayout';
import * as authStoreModule from '@app/stores/auth.store';
import * as notificationStoreModule from '@app/stores/notification.store';
import * as useI18nModule from '@app/i18n/use-i18n';
import * as useIdleTimeoutModule from '@app/hooks/use-idle-timeout';
import * as TopAppBarModule from './TopAppBar';
import * as NavRailModule from './NavRail';

vi.mock('@app/stores/auth.store');
vi.mock('@app/stores/notification.store');
vi.mock('@app/i18n/use-i18n');
vi.mock('@app/hooks/use-idle-timeout');
vi.mock('./TopAppBar');
vi.mock('./NavRail');

/** Disposición que publica el sistema (`settings.ui.layout`, G-178). */
let ui: { layout?: string } = {};

vi.mock('@app/authorization/hooks/use-system-settings', () => ({
  useSystemSettings: () => ({ brand: {}, theme: {}, ui, locale: {}, raw: {} }),
}));

describe('MainLayout', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    ui = {};

    vi.mocked(authStoreModule.useAuthStore).mockReturnValue({
      user: { id: 'u-1', email: 'test@test.com' },
      logout: vi.fn(),
      isAuthenticated: true,
    } as any);

    vi.mocked(notificationStoreModule.useNotificationStore).mockImplementation((selector: any) => {
      const state = { addNotification: vi.fn() };
      return selector ? selector(state) : state;
    });

    vi.mocked(useI18nModule.useI18n).mockReturnValue({
      sessionExpired: 'Session Expired',
      sessionExpiredMsg: 'You have been logged out due to inactivity.',
    } as any);

    vi.mocked(useIdleTimeoutModule.useIdleTimeout).mockReturnValue(undefined as any);

    vi.mocked(TopAppBarModule.TopAppBar).mockImplementation(({ onToggleNav }: any) => (
      <div data-testid="top-app-bar">
        <button onClick={onToggleNav} data-testid="toggle-btn">
          Toggle
        </button>
      </div>
    ));
    vi.mocked(NavRailModule.NavRail).mockImplementation(({ collapsed }: any) => (
      <div data-testid="nav-rail" data-collapsed={String(collapsed)} />
    ));
  });

  it('renders TopAppBar', () => {
    render(
      <MainLayout>
        <div>Content</div>
      </MainLayout>
    );
    expect(screen.getByTestId('top-app-bar')).toBeInTheDocument();
  });

  it('renders NavRail', () => {
    render(
      <MainLayout>
        <div>Content</div>
      </MainLayout>
    );
    expect(screen.getByTestId('nav-rail')).toBeInTheDocument();
  });

  it('renders children', () => {
    render(
      <MainLayout>
        <div data-testid="child-content">Hello</div>
      </MainLayout>
    );
    expect(screen.getByTestId('child-content')).toBeInTheDocument();
  });

  it('passes enabled=true to useIdleTimeout when user exists', () => {
    render(
      <MainLayout>
        <div>Content</div>
      </MainLayout>
    );
    expect(useIdleTimeoutModule.useIdleTimeout).toHaveBeenCalledWith(
      expect.objectContaining({ enabled: true })
    );
  });

  it('passes enabled=false to useIdleTimeout when no user', () => {
    vi.mocked(authStoreModule.useAuthStore).mockReturnValue({
      user: null,
      logout: vi.fn(),
      isAuthenticated: false,
    } as any);

    render(
      <MainLayout>
        <div>Content</div>
      </MainLayout>
    );
    expect(useIdleTimeoutModule.useIdleTimeout).toHaveBeenCalledWith(
      expect.objectContaining({ enabled: false })
    );
  });

  it('toggles navCollapsed when TopAppBar toggle is called', () => {
    render(
      <MainLayout>
        <div>Content</div>
      </MainLayout>
    );

    expect(screen.getByTestId('nav-rail')).toHaveAttribute('data-collapsed', 'false');

    fireEvent.click(screen.getByTestId('toggle-btn'));

    expect(screen.getByTestId('nav-rail')).toHaveAttribute('data-collapsed', 'true');
  });

  describe('disposición publicada por el sistema', () => {
    const render_ = () =>
      render(
        <MainLayout>
          <div>Content</div>
        </MainLayout>
      );

    it('sin disposición declarada el rail nace desplegado', () => {
      render_();
      expect(screen.getByTestId('nav-rail')).toHaveAttribute('data-collapsed', 'false');
    });

    it('`nav-rail-compact` arranca el rail en solo-iconos', () => {
      ui = { layout: 'nav-rail-compact' };
      render_();
      expect(screen.getByTestId('nav-rail')).toHaveAttribute('data-collapsed', 'true');
    });

    it('una disposición desconocida cae en la conocida', () => {
      // Preferible un rail desplegado a una pantalla sin navegación.
      ui = { layout: 'holograma' };
      render_();
      expect(screen.getByTestId('nav-rail')).toHaveAttribute('data-collapsed', 'false');
    });

    it('es el estado inicial, no un candado: la hamburguesa sigue mandando', () => {
      ui = { layout: 'nav-rail-compact' };
      render_();

      fireEvent.click(screen.getByTestId('toggle-btn'));
      expect(screen.getByTestId('nav-rail')).toHaveAttribute('data-collapsed', 'false');
    });
  });
});
