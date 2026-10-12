import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { NavRail } from './NavRail';
import * as useI18nModule from '@app/i18n/use-i18n';

vi.mock('react-router', () => ({
  useNavigate: () => vi.fn(),
  useLocation: () => ({ pathname: '/tenants' }),
}));

vi.mock('@app/i18n/use-i18n');
vi.mock('./navigation.config', () => ({
  NAV_ROUTES: { tenants: '/tenants', users: '/users' },
  isExternalItem: (m: { external?: boolean }) => m.external === true,
  NAV_MODULES: () => [
    {
      key: 'idm',
      nameKey: 'identity',
      icon: 'I',
      members: [
        { id: 'tenants', nameKey: 'tenants', icon: 'T' },
        { id: 'users', nameKey: 'users', icon: 'U' },
      ],
    },
  ],
}));

/** Grafo de autorización de la sesión; `undefined` deja al shell en su respaldo estático. */
let menuAccess: unknown;

vi.mock('@app/stores/auth.store', () => ({
  useAuthStore: (selector: (s: unknown) => unknown) =>
    selector({ user: { authorizationGraph: { menuAccess } } }),
}));

describe('NavRail', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    menuAccess = undefined;

    vi.mocked(useI18nModule.useI18n).mockReturnValue({
      identity: 'Identity',
      tenants: 'Tenants',
      users: 'Users',
      portalFooter: 'UMS Portal',
      archVersion: 'v1.0.0',
    } as unknown as ReturnType<typeof useI18nModule.useI18n>);
  });

  it('renders navigation element', () => {
    render(<NavRail collapsed={false} />);
    expect(screen.getByRole('navigation')).toBeInTheDocument();
  });

  it('renders module headers when not collapsed', () => {
    render(<NavRail collapsed={false} />);
    expect(screen.getByText('Identity')).toBeInTheDocument();
  });

  it('renders navigation items when module is expanded', () => {
    render(<NavRail collapsed={false} />);
    expect(screen.getByText('Tenants')).toBeInTheDocument();
    expect(screen.getByText('Users')).toBeInTheDocument();
  });

  it('renders footer text', () => {
    render(<NavRail collapsed={false} />);
    expect(screen.getByText('UMS Portal')).toBeInTheDocument();
  });

  it('renders version text', () => {
    render(<NavRail collapsed={false} />);
    expect(screen.getByText('v1.0.0')).toBeInTheDocument();
  });

  it('renders collapsed view when collapsed is true', () => {
    render(<NavRail collapsed={true} />);
    expect(screen.getByRole('navigation')).toBeInTheDocument();
  });

  it('toggles module expansion when clicked', () => {
    const { container } = render(<NavRail collapsed={false} />);

    // Los módulos nacen desplegados: el menú llega del grafo y sus códigos no se conocen de
    // antemano, así que el estado inicial no puede sembrarse por módulo.
    const moduleButton = container.querySelector('button[aria-expanded="true"]');
    if (!moduleButton) throw new Error('No se renderizó ninguna cabecera de módulo desplegada');

    fireEvent.click(moduleButton);
    expect(container.querySelector('button[aria-expanded="false"]')).toBeInTheDocument();
    expect(screen.queryByText('Tenants')).not.toBeInTheDocument();
  });

  it('highlights active tab', () => {
    render(<NavRail collapsed={false} />);
    const tenantsButton = screen.getByText('Tenants').closest('button');
    expect(tenantsButton).toHaveClass('bg-m3-primary-container');
  });

  it('pinta la ESTRUCTURA del grafo con los rótulos de la aplicación (ADR-UMS-104)', () => {
    // Con grafo, la barra deja de depender de navigation.config para SABER QUÉ EXISTE: dar de alta
    // un menú en UMS basta para que aparezca. El texto, en cambio, lo sigue poniendo el cliente
    // desde su i18n, o el conmutador de idioma cambiaría toda la interfaz menos el menú (G-283).
    menuAccess = [
      {
        code: 'IDM',
        value: 'Identidad y Accesos',
        sortOrder: 1,
        status: 'Active',
        nodes: [
          {
            code: 'TENANTS',
            value: 'Empresas del grupo',
            kind: 'Menu',
            sortOrder: 1,
            icon: 'building',
            route: '/tenants',
            actions: [],
            children: [],
          },
        ],
      },
    ];

    render(<NavRail collapsed={false} />);

    // El grafo dice «Identidad y Accesos» y «Empresas del grupo»; se pinta lo que esta aplicación
    // llama a ese módulo y a esa pantalla.
    expect(screen.getByText('Identity')).toBeInTheDocument();
    expect(screen.getByText('Tenants')).toBeInTheDocument();
    expect(screen.queryByText('Empresas del grupo')).not.toBeInTheDocument();
    // «Users» existe en el respaldo estático, pero el grafo no lo concede.
    expect(screen.queryByText('Users')).not.toBeInTheDocument();
    expect(screen.getByText('Tenants').closest('button')).toHaveClass(
      'bg-m3-primary-container'
    );
  });
});
