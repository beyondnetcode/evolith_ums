import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { TopAppBar } from './TopAppBar';
import * as authStoreModule from '@app/stores/auth.store';
import * as themeStoreModule from '@app/stores/theme.store';
import * as devToolsStoreModule from '@app/stores/devTools.store';
import * as i18nStoreModule from '@app/stores/i18n.store';
import * as notificationStoreModule from '@app/stores/notification.store';
import * as useI18nModule from '@app/i18n/use-i18n';

vi.mock('@app/stores/auth.store');
vi.mock('@app/stores/theme.store');
vi.mock('@app/stores/devTools.store');
vi.mock('@app/stores/i18n.store');
vi.mock('@app/stores/notification.store');
vi.mock('@app/i18n/use-i18n');
vi.mock('../components/NotificationCenter', () => ({
  NotificationCenter: () => <div data-testid="notification-center" />,
}));
vi.mock('../components/ToastQueue', () => ({
  ToastQueue: () => <div data-testid="toast-queue" />,
}));
vi.mock('../components/Tooltip', () => ({ Tooltip: ({ children }: any) => <>{children}</> }));

/** Marca publicada por el sistema en el grafo (G-178). Vacía = se ve el producto por defecto. */
let brand: {
  displayName?: string;
  tagline?: string;
  logoUrl?: string;
  iconUrl?: string;
} = {};

vi.mock('@app/authorization/hooks/use-system-settings', () => ({
  useSystemSettings: () => ({ brand, theme: {}, ui: {}, locale: {}, raw: {} }),
}));

const mockI18nStore = (parcial: { language: string; setLanguage: () => void }) => {
  const state = { ...parcial, applySystemLanguage: vi.fn(), chosenByUser: false };
  vi.mocked(i18nStoreModule.useI18nStore).mockImplementation((selector?: any) =>
    selector ? selector(state) : state
  );
};

describe('TopAppBar', () => {
  const mockOnToggleNav = vi.fn();

  beforeEach(() => {
    vi.restoreAllMocks();
    brand = {};

    vi.mocked(authStoreModule.useAuthStore).mockReturnValue({
      user: { id: 'u-1', username: 'testuser' },
      logout: vi.fn(),
    } as any);

    vi.mocked(themeStoreModule.useThemeStore).mockReturnValue({
      isDarkMode: false,
      toggleDarkMode: vi.fn(),
    } as any);

    vi.mocked(devToolsStoreModule.useDevToolsStore).mockReturnValue({} as any);

    // Con selector o sin él: la barra lee el store entero y SystemThemeApplier —que la barra
    // monta— lee `applySystemLanguage` con selector.
    mockI18nStore({ language: 'en', setLanguage: vi.fn() });

    vi.mocked(notificationStoreModule.useNotificationStore).mockImplementation((selector: any) => {
      const state = {
        notifications: [],
        setIsOpen: vi.fn(),
        isOpen: false,
      };
      return selector ? selector(state) : state;
    });

    vi.mocked(useI18nModule.useI18n).mockReturnValue({
      appName: 'UMS',
      appSubtitle: 'User Management',
      devUser: 'Dev:',
      toggleLanguage: 'Toggle Language',
      toggleTheme: 'Toggle Theme',
      openNotifications: 'Notifications',
      logoutBtn: 'Logout',
    } as any);
  });

  it('renders header element', () => {
    render(<TopAppBar onToggleNav={mockOnToggleNav} />);
    expect(screen.getByRole('banner')).toBeInTheDocument();
  });

  it('renders app name', () => {
    render(<TopAppBar onToggleNav={mockOnToggleNav} />);
    // Nombre corto y largo son el mismo texto cuando el producto no publica uno corto: el
    // encabezado lleva las dos formas y el ancho decide cuál se ve.
    expect(screen.getAllByText('UMS')).toHaveLength(2);
  });

  it('renders app subtitle', () => {
    render(<TopAppBar onToggleNav={mockOnToggleNav} />);
    expect(screen.getByText('User Management')).toBeInTheDocument();
  });

  it('renders NotificationCenter', () => {
    render(<TopAppBar onToggleNav={mockOnToggleNav} />);
    expect(screen.getByTestId('notification-center')).toBeInTheDocument();
  });

  it('renders ToastQueue', () => {
    render(<TopAppBar onToggleNav={mockOnToggleNav} />);
    expect(screen.getByTestId('toast-queue')).toBeInTheDocument();
  });

  it('calls onToggleNav when menu button is clicked', () => {
    render(<TopAppBar onToggleNav={mockOnToggleNav} />);
    const menuButton = screen.getByLabelText('Toggle navigation');
    fireEvent.click(menuButton);
    expect(mockOnToggleNav).toHaveBeenCalled();
  });

  it('shows user info when user exists', () => {
    render(<TopAppBar onToggleNav={mockOnToggleNav} />);
    expect(screen.getByText(/Dev:/)).toBeInTheDocument();
  });

  it('shows user initials', () => {
    render(<TopAppBar onToggleNav={mockOnToggleNav} />);
    expect(screen.getByText('TE')).toBeInTheDocument();
  });

  it('calls logout when logout button is clicked', () => {
    const logout = vi.fn();
    vi.mocked(authStoreModule.useAuthStore).mockReturnValue({
      user: { id: 'u-1', username: 'testuser' },
      logout,
    } as any);

    render(<TopAppBar onToggleNav={mockOnToggleNav} />);
    const logoutButton = screen.getByLabelText('Logout');
    fireEvent.click(logoutButton);
    expect(logout).toHaveBeenCalled();
  });

  it('calls toggleDarkMode when theme button is clicked', () => {
    const toggleDarkMode = vi.fn();
    vi.mocked(themeStoreModule.useThemeStore).mockReturnValue({
      isDarkMode: false,
      toggleDarkMode,
    } as any);

    render(<TopAppBar onToggleNav={mockOnToggleNav} />);
    const themeButton = screen.getByLabelText('Switch to dark mode');
    fireEvent.click(themeButton);
    expect(toggleDarkMode).toHaveBeenCalled();
  });

  it('calls setLanguage when language button is clicked', () => {
    const setLanguage = vi.fn();
    mockI18nStore({ language: 'en', setLanguage });

    render(<TopAppBar onToggleNav={mockOnToggleNav} />);
    const langButton = screen.getByLabelText('Switch to Spanish');
    fireEvent.click(langButton);
    expect(setLanguage).toHaveBeenCalledWith('es');
  });

  it('shows unread notification count when there are unread notifications', () => {
    vi.mocked(notificationStoreModule.useNotificationStore).mockImplementation((selector: any) => {
      const state = {
        notifications: [
          { id: '1', read: false, title: 'Test', message: 'Test', type: 'info' as const },
        ],
        setIsOpen: vi.fn(),
        isOpen: false,
      };
      return selector ? selector(state) : state;
    });

    render(<TopAppBar onToggleNav={mockOnToggleNav} />);
    expect(screen.getByText('1')).toBeInTheDocument();
  });

  it('calls setIsOpen when notification button is clicked', () => {
    const setIsOpen = vi.fn();
    vi.mocked(notificationStoreModule.useNotificationStore).mockImplementation((selector: any) => {
      const state = {
        notifications: [],
        setIsOpen,
        isOpen: false,
      };
      return selector ? selector(state) : state;
    });

    render(<TopAppBar onToggleNav={mockOnToggleNav} />);
    const notifButton = screen.getByLabelText('Notifications');
    fireEvent.click(notifButton);
    expect(setIsOpen).toHaveBeenCalledWith(true);
  });

  it('does not show user info when no user', () => {
    vi.mocked(authStoreModule.useAuthStore).mockReturnValue({
      user: null,
      logout: vi.fn(),
    } as any);

    render(<TopAppBar onToggleNav={mockOnToggleNav} />);
    expect(screen.queryByText(/Dev:/)).not.toBeInTheDocument();
  });

  it('does not show logout button when no user', () => {
    vi.mocked(authStoreModule.useAuthStore).mockReturnValue({
      user: null,
      logout: vi.fn(),
    } as any);

    render(<TopAppBar onToggleNav={mockOnToggleNav} />);
    expect(screen.queryByLabelText('Log out')).not.toBeInTheDocument();
  });

  describe('marca del sistema', () => {
    it('sin marca publicada se ve la del producto', () => {
      render(<TopAppBar onToggleNav={mockOnToggleNav} />);

      const marcas = screen.getAllByAltText('EVOLITH');
      expect(marcas).toHaveLength(2); // claro y oscuro
      expect(screen.getAllByText('UMS')).not.toHaveLength(0);
    });

    it('usa el icono en móvil y el logotipo desde sm', () => {
      brand = {
        displayName: 'Tablero de Gobierno SDLC',
        logoUrl: '/branding/sdlc/logo.svg',
        iconUrl: '/branding/sdlc/icon.svg',
      };

      render(<TopAppBar onToggleNav={mockOnToggleNav} />);
      const [compacta, ancha] = screen.getAllByAltText('Tablero de Gobierno SDLC');

      expect(compacta).toHaveAttribute('src', '/branding/sdlc/icon.svg');
      expect(compacta.className).toContain('sm:hidden');
      expect(ancha).toHaveAttribute('src', '/branding/sdlc/logo.svg');
      expect(ancha.className).toContain('hidden sm:block');

      // La marca del sistema sustituye a la del producto, no se suma a ella.
      expect(screen.queryByAltText('EVOLITH')).not.toBeInTheDocument();
    });

    it('si solo hay logotipo, también sirve de marca compacta', () => {
      // Un sistema a medio configurar debe verse completo en ambos tamaños, no dejar un hueco.
      brand = { displayName: 'WMS', logoUrl: '/branding/wms/logo.svg' };

      render(<TopAppBar onToggleNav={mockOnToggleNav} />);
      const marcas = screen.getAllByAltText('WMS');

      expect(marcas).toHaveLength(2);
      marcas.forEach(m => expect(m).toHaveAttribute('src', '/branding/wms/logo.svg'));
    });

    it('el nombre y el descriptor del sistema sustituyen a los del producto', () => {
      brand = {
        displayName: 'Tablero de Gobierno SDLC',
        tagline: 'Gobierno del ciclo de vida de los sistemas satélite',
      };

      render(<TopAppBar onToggleNav={mockOnToggleNav} />);

      expect(screen.getAllByText('Tablero de Gobierno SDLC')).not.toHaveLength(0);
      expect(
        screen.getByText('Gobierno del ciclo de vida de los sistemas satélite')
      ).toBeInTheDocument();
      expect(screen.queryByText('UMS')).not.toBeInTheDocument();
      expect(screen.queryByText('User Management')).not.toBeInTheDocument();
    });

    it('un sistema con nombre y sin descriptor no hereda el subtítulo del producto', () => {
      // Mezclar el nombre de un sistema con el subtítulo de otro confunde sobre dónde se está.
      brand = { displayName: 'Tablero de Gobierno SDLC' };

      render(<TopAppBar onToggleNav={mockOnToggleNav} />);

      expect(screen.getAllByText('Tablero de Gobierno SDLC')).not.toHaveLength(0);
      expect(screen.queryByText('User Management')).not.toBeInTheDocument();
    });

    it('en móvil el encabezado usa el nombre corto', () => {
      brand = { displayName: 'Tablero de Gobierno SDLC', shortName: 'SDLC' };

      render(<TopAppBar onToggleNav={mockOnToggleNav} />);
      const corto = screen.getByText('SDLC');
      const largo = screen.getByText('Tablero de Gobierno SDLC');

      // El ancho decide cuál se ve: el corto desaparece desde `sm`, el largo hasta `sm`.
      expect(corto.className).toContain('sm:hidden');
      expect(largo.className).toContain('hidden sm:inline');
    });

    it('sin nombre corto se recorta el largo, nunca se cae al del producto', () => {
      // Caer a «UMS» anunciaría un sistema distinto del que el usuario tiene abierto.
      brand = { displayName: 'Tablero de Gobierno SDLC' };

      render(<TopAppBar onToggleNav={mockOnToggleNav} />);

      expect(screen.getAllByText('Tablero de Gobierno SDLC')).toHaveLength(2);
      expect(screen.queryByText('UMS')).not.toBeInTheDocument();
    });

    it('si solo hay icono, también ocupa el sitio del logotipo', () => {
      brand = { displayName: 'WMS', iconUrl: '/branding/wms/icon.svg' };

      render(<TopAppBar onToggleNav={mockOnToggleNav} />);
      const marcas = screen.getAllByAltText('WMS');

      expect(marcas).toHaveLength(2);
      marcas.forEach(m => expect(m).toHaveAttribute('src', '/branding/wms/icon.svg'));
    });
  });
});
