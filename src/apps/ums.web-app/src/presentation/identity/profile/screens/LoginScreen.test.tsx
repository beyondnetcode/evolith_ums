import React from 'react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import LoginScreen from './LoginScreen';

const navigateMock = vi.fn();
const loginMock = vi.fn();
const setLanguageMock = vi.fn();
const setDevUserIdMock = vi.fn();
const addNotificationMock = vi.fn();

vi.mock('react-router', () => ({
  useNavigate: () => navigateMock,
  useLocation: () => ({ search: '' }),
}));

vi.mock('@app/stores/auth.store', () => ({
  detectBrowserTimezone: vi.fn((timezone: string) => timezone),
  useAuthStore: () => ({
    login: loginMock,
    isAuthenticated: false,
  }),
}));

vi.mock('@app/stores/i18n.store', () => ({
  useI18nStore: () => ({
    setLanguage: setLanguageMock,
  }),
}));

vi.mock('@app/stores/devTools.store', () => ({
  useDevToolsStore: () => ({
    setDevUserId: setDevUserIdMock,
  }),
}));

vi.mock('@app/stores/notification.store', () => ({
  useNotificationStore: (
    selector: (state: { addNotification: typeof addNotificationMock }) => unknown
  ) => selector({ addNotification: addNotificationMock }),
}));

vi.mock('@app/i18n/use-i18n', () => ({
  useI18n: () => (key: string) => key,
}));

const loginServiceMock = vi.fn();

vi.mock('@app/identity/services/auth.service', () => ({
  authService: {
    login: (...args: unknown[]) => loginServiceMock(...args),
  },
}));

vi.mock('@shared/components/M3Card', () => ({
  M3Card: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
}));

vi.mock('@shared/components/M3Button', () => ({
  M3Button: ({
    children,
    ...props
  }: React.ButtonHTMLAttributes<HTMLButtonElement> & { children: React.ReactNode }) => (
    <button {...props}>{children}</button>
  ),
}));

vi.mock('@shared/components/M3TextField', () => ({
  M3TextField: ({
    label,
    value,
    onChange,
    type = 'text',
    placeholder,
    disabled,
    error,
  }: {
    label: string;
    value: string;
    onChange: React.ChangeEventHandler<HTMLInputElement>;
    type?: string;
    placeholder?: string;
    disabled?: boolean;
    error?: string;
  }) => (
    <label>
      <span>{label}</span>
      <input
        aria-label={label}
        value={value}
        onChange={onChange}
        type={type}
        placeholder={placeholder}
        disabled={disabled}
      />
      {error ? <span>{error}</span> : null}
    </label>
  ),
}));

vi.mock('@shared/components/TenantSelect', () => ({
  TenantSelect: ({
    label,
    value,
    onChange,
  }: {
    label: string;
    value: string;
    onChange: (codigo: string) => void;
  }) => (
    <label>
      <span>{label}</span>
      <input aria-label={label} value={value} onChange={e => onChange(e.target.value)} />
    </label>
  ),
}));

vi.mock('@domain/identity/constants/tenant.constants', () => ({
  CODIGO_INQUILINO_POR_OMISION: 'EVOLITH',
  INQUILINOS_SUGERIDOS: [{ code: 'EVOLITH', name: 'Evolith UMS' }],
  normalizarCodigoInquilino: (valor: string) => valor.trim().toUpperCase(),
}));

vi.mock('../components/ForgotPasswordForm', () => ({
  ForgotPasswordForm: ({ onBack }: { onBack: () => void }) => (
    <div>
      <h2>Recuperar Contraseña</h2>
      <button onClick={onBack}>Volver al inicio de sesión</button>
    </div>
  ),
}));

vi.mock('../components/SignupForm', () => ({
  SignupForm: () => <div>Signup</div>,
}));

vi.mock('../components/TenantSignupForm', () => ({
  TenantSignupForm: () => <div>Tenant Signup</div>,
}));

vi.mock('../components/ProfileRequestForm', () => ({
  ProfileRequestForm: () => <div>Profile Request</div>,
}));

beforeEach(() => {
  vi.clearAllMocks();
});

describe('LoginScreen', () => {
  it('shows only the login form in the default state', () => {
    render(<LoginScreen />);

    expect(screen.getByText('Iniciar Sesión')).toBeInTheDocument();
    expect(screen.queryByText('Recuperar Contraseña')).not.toBeInTheDocument();
  });

  it('envía el código de inquilino que se teclea, sin identificador de por medio', async () => {
    // G-263: el código sale del campo y viaja tal cual. Antes salía de una lista compilada y el
    // GUID que la acompañaba no correspondía a ninguna base que no fuera la del sembrador.
    loginServiceMock.mockRejectedValue(new Error('credenciales'));
    render(<LoginScreen />);

    fireEvent.change(screen.getByLabelText('Inquilino'), { target: { value: 'comex_andina' } });
    fireEvent.change(screen.getByLabelText('Correo electrónico'), {
      target: { value: 'usuario.impo@comexandina.com.pe' },
    });
    fireEvent.change(screen.getByLabelText('Contraseña'), { target: { value: 'Evolith.Dev.2026' } });
    fireEvent.click(screen.getByRole('button', { name: 'Ingresar' }));

    await waitFor(() => expect(loginServiceMock).toHaveBeenCalledTimes(1));
    expect(loginServiceMock.mock.calls[0][0]).toMatchObject({
      tenantCode: 'COMEX_ANDINA',
      username: 'usuario.impo@comexandina.com.pe',
    });
  });

  it('no intenta ingresar sin código de inquilino', async () => {
    render(<LoginScreen />);

    fireEvent.change(screen.getByLabelText('Inquilino'), { target: { value: '   ' } });
    fireEvent.change(screen.getByLabelText('Correo electrónico'), {
      target: { value: 'admin@evolith.example' },
    });
    fireEvent.change(screen.getByLabelText('Contraseña'), { target: { value: 'Evolith.Dev.2026' } });
    fireEvent.click(screen.getByRole('button', { name: 'Ingresar' }));

    expect(await screen.findByText('Ingrese el código de su inquilino')).toBeInTheDocument();
    expect(loginServiceMock).not.toHaveBeenCalled();
  });

  it('replaces the login form with the forgot password view', () => {
    render(<LoginScreen />);

    fireEvent.click(screen.getByRole('button', { name: '¿Olvidaste tu contraseña?' }));

    expect(screen.getByText('Recuperar Contraseña')).toBeInTheDocument();
    expect(screen.queryByText('Iniciar Sesión')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Ingresar' })).not.toBeInTheDocument();
  });
});
