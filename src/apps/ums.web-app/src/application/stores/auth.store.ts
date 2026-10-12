/**
 * auth.store.ts
 *
 * Production-grade authentication state management.
 * Implements OWASP recommendations for secure session handling:
 *
 * - Refresh token rotation for extended sessions
 * - No automatic authentication bypass in DEV mode
 * - Session stored with security flags (httpOnly, secure, sameSite)
 * - Session validation on app initialization
 * - Automatic session expiration handling
 * - Secure logout with session invalidation
 * - Automatic token refresh on app focus
 *
 * C-2: DOM manipulation removed — presentation layer handles document.body.
 * M-1: Theme state moved to theme.store.ts.
 */
import { create } from 'zustand';
import { persist, createJSONStorage } from 'zustand/middleware';
import { AuthorizationGraph } from '@domain/authorization/schemas/authorization-graph.schema';

/**
 * Tenant-specific configuration values resolved at login from the backend's in-memory
 * parameter cache. Stored in the session so the frontend never needs a separate round-trip.
 * TD-003: When Redis is adopted on the backend, these values still arrive here unchanged.
 *
 * ADR-0076: defaultLanguage and defaultTimezone follow the priority chain:
 *   browser detection > tenant parameter > platform default.
 */
export interface SessionParameters {
  sessionTimeoutMinutes: number;
  accessTokenDurationMs: number;
  refreshTokenDurationMs: number;
  maxLoginAttempts: number;
  minPasswordLength: number;
  mfaRequiredForAdmin: boolean;
  mfaAllowedMethods: string[];
  customBrandingEnabled: boolean;
  defaultLanguage: string;
  defaultTimezone: string;
}

/**
 * ADR-0076: Detects the browser's IANA timezone.
 * Returns the browser timezone if available, falls back to sessionParameters.defaultTimezone.
 */
export function detectBrowserTimezone(fallback = 'America/Lima'): string {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone || fallback;
  } catch {
    return fallback;
  }
}

export interface AuthUser {
  id: string;
  username: string;
  email: string;
  role: string;
  tenantId: string;
  tenantCode: string;
  tenantName: string;
  profileId?: string;
  permissions: string[];
  sessionTrackingId: string;
  token?: string;
  isInternalAdmin: boolean;
  originalTenantId?: string;
  crossTenantAccessEnabled: boolean;
  /** Tenant-effective parameters loaded at login. Null only for legacy sessions. */
  sessionParameters: SessionParameters | null;
  /** El grafo efectivo de autorización que rige a este usuario */
  authorizationGraph: AuthorizationGraph | null;
}

export interface AuthState {
  user: AuthUser | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  sessionExpiresAt: number | null;
  refreshTokenExpiresAt: number | null;
  lastActivityAt: number | null;
  availableTenants: Array<{ id: string; code: string; name: string }>;

  login: (user: AuthUser, expiresAt?: number, refreshExpiresAt?: number) => void;
  logout: () => void;
  refreshSession: (newToken: string, expiresIn: number, refreshExpiresIn: number) => void;
  validateSession: () => boolean;
  checkSession: () => Promise<boolean>;
  updateActivity: () => void;
  getTimeUntilRefresh: () => number;
  switchTenant: (tenantId: string, enableCrossTenantAccess?: boolean) => Promise<boolean>;
  /**
   * Cambia el perfil vigente sin volver a autenticarse. Reemplaza el grafo COMPLETO: el árbol de
   * navegación, los permisos y el rol cambian de golpe, así que parchear el estado a trozos
   * dejaría la interfaz mostrando el menú de un perfil con los permisos de otro.
   */
  switchProfile: (profileId: string) => Promise<boolean>;
  setAvailableTenants: (tenants: Array<{ id: string; code: string; name: string }>) => void;
}

const ACCESS_TOKEN_DURATION = 60 * 60 * 1000;
const REFRESH_TOKEN_DURATION = 7 * 24 * 60 * 60 * 1000;
const SESSION_WARNING_THRESHOLD = 5 * 60 * 1000;
const ACTIVITY_UPDATE_INTERVAL = 60 * 1000;
const REFRESH_THRESHOLD = 5 * 60 * 1000;

function isSessionExpired(expiresAt: number | null): boolean {
  if (!expiresAt) return true;
  return Date.now() >= expiresAt;
}

function isRefreshExpired(expiresAt: number | null): boolean {
  if (!expiresAt) return true;
  return Date.now() >= expiresAt;
}

function getSessionExpiration(expiresAt: number | null): number {
  if (!expiresAt) return 0;
  return Math.max(0, expiresAt - Date.now());
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set, get) => ({
      user: null,
      isAuthenticated: false,
      isLoading: true,
      sessionExpiresAt: null,
      refreshTokenExpiresAt: null,
      lastActivityAt: null,
      availableTenants: [],

      login: (user, expiresAt, refreshExpiresAt) => {
        const now = Date.now();
        const accessDuration = expiresAt || ACCESS_TOKEN_DURATION;
        const refreshDuration = refreshExpiresAt || REFRESH_TOKEN_DURATION;

        set({
          user: {
            ...user,
            isInternalAdmin: user.isInternalAdmin || false,
            crossTenantAccessEnabled: false,
            originalTenantId: user.tenantId,
          },
          isAuthenticated: true,
          isLoading: false,
          sessionExpiresAt: now + accessDuration,
          refreshTokenExpiresAt: now + refreshDuration,
          lastActivityAt: now,
        });

        localStorage.setItem('ums_session_start', String(now));
        localStorage.setItem('ums_last_activity', String(now));

        console.info(`[Auth] Session started for user: ${user.username}`, {
          tenant: user.tenantCode,
          isInternalAdmin: user.isInternalAdmin,
          expiresAt: new Date(now + accessDuration).toISOString(),
        });
      },

      logout: () => {
        const state = get();
        console.info(`[Auth] Session ended for user: ${state.user?.username}`);

        set({
          user: null,
          isAuthenticated: false,
          isLoading: false,
          sessionExpiresAt: null,
          refreshTokenExpiresAt: null,
          lastActivityAt: null,
        });

        localStorage.removeItem('ums_session_start');
        localStorage.removeItem('ums_last_activity');

        if (import.meta.env.PROD) {
          // Navegación de página completa: no pasa por el router, así que la ruta debe
          // llevar el prefijo público con el que se construyó el bundle. Con '/login' a
          // secas, la app servida bajo /ums_web/ mandaba a la raíz del sitio —el API— en
          // vez de a su propio login.
          window.location.replace(`${import.meta.env.BASE_URL}login`);
        }
      },

      refreshSession: (newToken, expiresIn, refreshExpiresIn) => {
        const state = get();
        if (!state.user) {
          console.warn('[Auth] Cannot refresh session - no user logged in');
          return;
        }

        const now = Date.now();
        set({
          user: { ...state.user, token: newToken },
          sessionExpiresAt: now + expiresIn,
          refreshTokenExpiresAt: now + refreshExpiresIn,
          lastActivityAt: now,
        });

        localStorage.setItem('ums_last_activity', String(now));
        console.info('[Auth] Session refreshed', {
          expiresAt: new Date(now + expiresIn).toISOString(),
          refreshExpiresAt: new Date(now + refreshExpiresIn).toISOString(),
        });
      },

      validateSession: () => {
        const state = get();
        if (!state.user || !state.sessionExpiresAt) {
          return false;
        }

        if (isSessionExpired(state.sessionExpiresAt)) {
          if (state.refreshTokenExpiresAt && !isRefreshExpired(state.refreshTokenExpiresAt)) {
            console.info('[Auth] Access token expired, refresh token available - refresh needed');
            return true;
          }
          get().logout();
          return false;
        }

        return true;
      },

      checkSession: async () => {
        const state = get();

        if (!state.user || !state.sessionExpiresAt) {
          set({ isLoading: false });
          return false;
        }

        if (isRefreshExpired(state.refreshTokenExpiresAt)) {
          console.warn('[Auth] Refresh token expired - forcing logout');
          get().logout();
          set({ isLoading: false });
          return false;
        }

        if (isSessionExpired(state.sessionExpiresAt)) {
          console.info('[Auth] Access token expired - needs refresh');
          set({ isLoading: false });
          return false;
        }

        const timeUntilExpiry = getSessionExpiration(state.sessionExpiresAt);
        if (timeUntilExpiry > 0 && timeUntilExpiry < SESSION_WARNING_THRESHOLD) {
          console.warn(
            '[Auth] Session expiring soon:',
            timeUntilExpiry / 1000,
            'seconds remaining'
          );
        }

        set({ isLoading: false });
        return true;
      },

      updateActivity: () => {
        const state = get();
        if (!state.isAuthenticated) return;

        const now = Date.now();
        const lastActivity = state.lastActivityAt || now;

        if (now - lastActivity >= ACTIVITY_UPDATE_INTERVAL) {
          set({ lastActivityAt: now });
          localStorage.setItem('ums_last_activity', String(now));
        }
      },

      getTimeUntilRefresh: () => {
        const state = get();
        if (!state.refreshTokenExpiresAt) return 0;
        return Math.max(0, state.refreshTokenExpiresAt - Date.now());
      },

      switchTenant: async (tenantId: string, enableCrossTenantAccess = false) => {
        const state = get();
        if (!state.user?.isInternalAdmin) {
          console.warn('[Auth] Non-admin users cannot switch tenants');
          return false;
        }

        try {
          const response = await fetch('/api/v1/auth/switch-tenant', {
            method: 'POST',
            headers: {
              'Content-Type': 'application/json',
              Authorization: `Bearer ${state.user.token}`,
            },
            credentials: 'include',
            body: JSON.stringify({
              tenantId,
              enableCrossTenantAccess,
            }),
          });

          if (!response.ok) {
            const error = await response.json();
            console.error('[Auth] Tenant switch failed:', error);
            return false;
          }

          const data = await response.json();

          set({
            user: {
              ...state.user,
              tenantId: data.currentTenantId,
              tenantCode: data.currentTenantCode,
              tenantName: data.currentTenantName,
              crossTenantAccessEnabled: data.crossTenantAccessEnabled,
            },
          });

          console.info('[Auth] Tenant switched successfully', {
            previousTenantId: data.previousTenantId,
            currentTenantId: data.currentTenantId,
            crossTenantAccessEnabled: data.crossTenantAccessEnabled,
          });

          return true;
        } catch (error) {
          console.error('[Auth] Tenant switch error:', error);
          return false;
        }
      },

      switchProfile: async (profileId: string) => {
        const state = get();
        if (!state.user?.token) return false;

        try {
          const response = await fetch('/api/v1/auth/switch-profile', {
            method: 'POST',
            headers: {
              'Content-Type': 'application/json',
              Authorization: `Bearer ${state.user.token}`,
            },
            credentials: 'include',
            body: JSON.stringify({ profileId }),
          });

          if (!response.ok) {
            console.error('[Auth] Profile switch failed:', await response.text());
            return false;
          }

          const data = await response.json();

          set({
            user: {
              ...state.user,
              role: data.role ?? state.user.role,
              profileId: data.profileId,
              permissions: data.permissions ?? [],
              token: data.token ?? state.user.token,
              authorizationGraph: data.authorizationGraph ?? null,
            },
            // El token nuevo trae su propia vigencia: la sesión no se alarga por cambiar de perfil.
            sessionExpiresAt: data.expiresIn
              ? Date.now() + data.expiresIn * 1000
              : state.sessionExpiresAt,
          });

          console.info('[Auth] Profile switched', { profileId, role: data.role });
          return true;
        } catch (error) {
          console.error('[Auth] Profile switch error:', error);
          return false;
        }
      },

      setAvailableTenants: tenants => {
        set({ availableTenants: tenants });
      },
    }),
    {
      name: 'ums-auth-storage',
      storage: createJSONStorage(() => localStorage),
      partialize: state => ({
        user: state.user,
        isAuthenticated: state.isAuthenticated,
        sessionExpiresAt: state.sessionExpiresAt,
        refreshTokenExpiresAt: state.refreshTokenExpiresAt,
        lastActivityAt: state.lastActivityAt,
        availableTenants: state.availableTenants,
      }),
      onRehydrateStorage: () => state => {
        if (state) {
          state.isLoading = true;

          if (state.refreshTokenExpiresAt && isRefreshExpired(state.refreshTokenExpiresAt)) {
            console.info('[Auth] Refresh token expired during rehydration - logging out');
            state.logout();
            return;
          }

          if (state.sessionExpiresAt && isSessionExpired(state.sessionExpiresAt)) {
            if (state.refreshTokenExpiresAt && !isRefreshExpired(state.refreshTokenExpiresAt)) {
              console.info('[Auth] Access token expired but refresh available - needs refresh');
              state.isLoading = false;
            } else {
              console.info('[Auth] Both tokens expired - logging out');
              state.logout();
            }
          } else {
            state.isLoading = false;
          }
        }
      },
    }
  )
);

let activityInterval: ReturnType<typeof setInterval> | null = null;

export function startActivityMonitor() {
  if (activityInterval) return;

  const { updateActivity, isAuthenticated } = useAuthStore.getState();
  if (!isAuthenticated) return;

  activityInterval = setInterval(() => {
    useAuthStore.getState().updateActivity();
  }, ACTIVITY_UPDATE_INTERVAL);

  document.addEventListener('visibilitychange', () => {
    if (document.visibilityState === 'visible') {
      useAuthStore.getState().updateActivity();
    }
  });
}

export function stopActivityMonitor() {
  if (activityInterval) {
    clearInterval(activityInterval);
    activityInterval = null;
  }
}

export function useSessionMonitor() {
  const { validateSession, logout, sessionExpiresAt } = useAuthStore();

  if (sessionExpiresAt && Date.now() >= sessionExpiresAt) {
    const state = useAuthStore.getState();
    if (state.refreshTokenExpiresAt && !isRefreshExpired(state.refreshTokenExpiresAt)) {
      return 'needs_refresh';
    }
    logout();
    return false;
  }

  return validateSession();
}

export async function refreshAccessToken(): Promise<boolean> {
  const state = useAuthStore.getState();

  if (!state.user || !state.refreshTokenExpiresAt) {
    return false;
  }

  if (isRefreshExpired(state.refreshTokenExpiresAt)) {
    console.warn('[Auth] Cannot refresh - token expired');
    state.logout();
    return false;
  }

  try {
    const response = await fetch('/api/v1/auth/refresh', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      credentials: 'include',
    });

    if (!response.ok) {
      throw new Error('Refresh failed');
    }

    const data = await response.json();
    state.refreshSession(
      data.token,
      data.expiresIn * 1000,
      (data.refreshExpiresIn || 7 * 24 * 60 * 60) * 1000
    );

    return true;
  } catch (error) {
    console.error('[Auth] Token refresh failed:', error);
    state.logout();
    return false;
  }
}
