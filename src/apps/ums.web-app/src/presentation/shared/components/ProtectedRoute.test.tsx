import React from 'react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, renderHook } from '@testing-library/react';
import { ProtectedRoute, useRequireAuth } from './ProtectedRoute';

const mocks = vi.hoisted(() => ({
  authState: {
    isAuthenticated: false,
    isLoading: false,
    user: null as unknown,
    checkSession: vi.fn(),
  },
  location: { pathname: '/panel' },
}));

vi.mock('@app/stores/auth.store', () => ({
  useAuthStore: () => mocks.authState,
}));

vi.mock('react-router', () => ({
  useLocation: () => mocks.location,
  Navigate: ({ to, state }: { to: string; state?: unknown }) => (
    <div data-testid="navigate" data-to={to}>
      {JSON.stringify(state)}
    </div>
  ),
}));

vi.mock('@app/identity/services/auth.service', () => ({ authService: {} }));

vi.mock('@shared/components/Spinner', () => ({
  Spinner: () => <div data-testid="spinner" />,
}));

const Child = () => <div data-testid="child">contenido</div>;

describe('ProtectedRoute', () => {
  beforeEach(() => {
    mocks.authState.isAuthenticated = false;
    mocks.authState.isLoading = false;
    mocks.authState.user = null;
    mocks.authState.checkSession = vi.fn().mockResolvedValue(true);
    mocks.location = { pathname: '/panel' };
  });

  it('muestra el spinner mientras el store está cargando', () => {
    mocks.authState.isLoading = true;
    render(
      <ProtectedRoute>
        <Child />
      </ProtectedRoute>
    );
    expect(screen.getByTestId('spinner')).toBeInTheDocument();
    expect(screen.getByText('Validando sesión...')).toBeInTheDocument();
    expect(screen.queryByTestId('child')).not.toBeInTheDocument();
  });

  it('redirige a /login cuando no hay sesión autenticada', async () => {
    render(
      <ProtectedRoute>
        <Child />
      </ProtectedRoute>
    );
    await waitFor(() => expect(screen.getByTestId('navigate')).toBeInTheDocument());
    const nav = screen.getByTestId('navigate');
    expect(nav).toHaveAttribute('data-to', '/login');
    expect(nav.textContent).toContain('/panel');
    expect(nav.textContent).toContain('showSessionExpired');
    expect(screen.queryByTestId('child')).not.toBeInTheDocument();
  });

  it('renderiza los hijos cuando la sesión es válida', async () => {
    mocks.authState.isAuthenticated = true;
    mocks.authState.user = { username: 'ana' };
    render(
      <ProtectedRoute>
        <Child />
      </ProtectedRoute>
    );
    await waitFor(() => expect(screen.getByTestId('child')).toBeInTheDocument());
    expect(mocks.authState.checkSession).toHaveBeenCalledTimes(1);
    expect(screen.queryByTestId('navigate')).not.toBeInTheDocument();
  });

  it('no revalida sesión cuando falta el usuario aunque isAuthenticated sea verdadero', async () => {
    mocks.authState.isAuthenticated = true;
    mocks.authState.user = null;
    render(
      <ProtectedRoute>
        <Child />
      </ProtectedRoute>
    );
    await waitFor(() => expect(screen.getByTestId('navigate')).toBeInTheDocument());
    expect(mocks.authState.checkSession).not.toHaveBeenCalled();
  });
});

describe('useRequireAuth', () => {
  beforeEach(() => {
    mocks.authState.isAuthenticated = false;
    mocks.authState.isLoading = false;
    mocks.location = { pathname: '/reportes' };
  });

  it('devuelve autorización indeterminada mientras carga', () => {
    mocks.authState.isLoading = true;
    const { result } = renderHook(() => useRequireAuth());
    expect(result.current.isAuthorized).toBeNull();
    expect(result.current.redirectPath).toBeNull();
  });

  it('devuelve no autorizado y ruta de redirección cuando no hay sesión', () => {
    const { result } = renderHook(() => useRequireAuth());
    expect(result.current.isAuthorized).toBe(false);
    expect(result.current.redirectPath).toBe('/login?redirect=%2Freportes');
  });

  it('devuelve autorizado cuando hay sesión', () => {
    mocks.authState.isAuthenticated = true;
    const { result } = renderHook(() => useRequireAuth());
    expect(result.current.isAuthorized).toBe(true);
    expect(result.current.redirectPath).toBeNull();
  });
});
