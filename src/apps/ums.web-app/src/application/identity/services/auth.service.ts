/**
 * auth.service.ts
 *
 * Production-grade authentication service.
 * Implements OWASP recommendations for secure authentication:
 *
 * - Input validation and sanitization
 * - Secure credential handling (no logging of passwords)
 * - Rate limiting awareness
 * - Secure session management
 * - CSRF protection via SameSite cookies
 * - Proper error messages that don't leak information
 */
import { AuthorizationGraph } from '@domain/authorization/schemas/authorization-graph.schema';
import { randomUuid } from '@app/utils/security';

export interface LoginCredentials {
  tenantCode: string;
  username: string;
  password: string;
  rememberMe?: boolean;
}

export interface AuthSessionParameters {
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

export interface AuthResponse {
  sessionId: string;
  sessionTrackingId: string;
  userId: string;
  username: string;
  email: string;
  tenantId: string;
  tenantCode: string;
  tenantName: string;
  role: string | null;
  roleName: string | null;
  profileId: string | null;
  permissions: string[];
  language: string;
  token: string;
  tokenType: string;
  expiresIn: number;
  refreshExpiresIn: number;
  isInternalAdmin: boolean;
  sessionParameters: AuthSessionParameters | null;
  authorizationGraph: AuthorizationGraph | null;
}

export interface AuthError {
  code: string;
  message: string;
  supportReferenceId: string | null;
}

type ErrorWithSupportReference = Error & { supportReferenceId?: string };

// In dev the Vite proxy handles /api → backend, so no absolute URL is needed.
// VITE_API_URL should only be set in production (e.g. https://api.ums.example.com).
const API_BASE_URL = import.meta.env.VITE_API_URL || '';

/**
 * El sistema que ESTA aplicacion es (G-184).
 *
 * El portal pide su propio grafo por codigo en vez de dejar que UMS lo resuelva por desempate: sin
 * esto, un usuario con perfiles en varios sistemas entraba aqui y recibia el grafo de OTRO producto
 * —`admin@evolith.example` resolvia `SIL`—, y el shell, que se construye del grafo desde G-181,
 * pintaba menus que este router no sirve. Es configurable porque el mismo bundle sirve para un
 * despliegue que registre la suite con otro codigo, no porque se espere cambiarlo.
 */
const SYSTEM_CODE = import.meta.env.VITE_UMS_SYSTEM_CODE || 'UMS';

const AUTH_ERROR_CODES = {
  VALIDATION_ERROR: 'AUTH_001',
  TENANT_NOT_FOUND: 'AUTH_002',
  TENANT_INACTIVE: 'AUTH_003',
  USER_NOT_FOUND: 'AUTH_004',
  USER_NOT_ACTIVE: 'AUTH_005',
  INVALID_CREDENTIALS: 'AUTH_006',
  SESSION_EXPIRED: 'AUTH_007',
} as const;

class AuthService {
  private baseUrl: string;
  private requestTimeout: number;

  constructor() {
    // Visual portal authentication must stay on the session-oriented API.
    // External systems use /api/v1/client/authenticate and receive the graph.
    this.baseUrl = `${API_BASE_URL}/api/v1/auth`;
    this.requestTimeout = 30000;
  }

  private sanitizeInput(input: string): string {
    return input.trim().replace(/[<>\"'&]/g, '');
  }

  private validateCredentials(credentials: LoginCredentials): string[] {
    const errors: string[] = [];

    if (!credentials.tenantCode || credentials.tenantCode.length < 1) {
      errors.push('Tenant code is required');
    }

    if (!credentials.username || credentials.username.length < 1) {
      errors.push('Username is required');
    }

    if (!credentials.password || credentials.password.length < 1) {
      errors.push('Password is required');
    }

    if (credentials.username && credentials.username.length > 100) {
      errors.push('Username exceeds maximum length');
    }

    return errors;
  }

  private getAuthHeaders(): HeadersInit {
    return {
      'Content-Type': 'application/json',
      Accept: 'application/json',
      'X-Request-ID': randomUuid(),
    };
  }

  async login(credentials: LoginCredentials): Promise<AuthResponse> {
    const sanitizedCredentials = {
      tenantCode: this.sanitizeInput(credentials.tenantCode).toUpperCase(),
      username: this.sanitizeInput(credentials.username),
      password: credentials.password.trim(),
      rememberMe: credentials.rememberMe || false,
      systemCode: SYSTEM_CODE,
    };

    const validationErrors = this.validateCredentials(sanitizedCredentials);
    if (validationErrors.length > 0) {
      throw new Error(validationErrors.join(', '));
    }

    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), this.requestTimeout);

    try {
      const response = await fetch(`${this.baseUrl}/login`, {
        method: 'POST',
        headers: this.getAuthHeaders(),
        body: JSON.stringify(sanitizedCredentials),
        credentials: 'include',
        signal: controller.signal,
      });

      clearTimeout(timeoutId);

      if (!response.ok) {
        const errorData = (await response.json().catch(() => null)) as AuthError | null;
        const supportReferenceId =
          errorData?.supportReferenceId ??
          response.headers.get('x-correlation-id') ??
          response.headers.get('x-error-id');

        const fail = (message: string): never => {
          const error = new Error(message) as ErrorWithSupportReference;
          error.supportReferenceId = supportReferenceId ?? undefined;
          throw error;
        };

        if (errorData) {
          switch (errorData.code) {
            case AUTH_ERROR_CODES.INVALID_CREDENTIALS:
              fail('No pudimos iniciar sesión. Verifique su usuario y contraseña.');
            case AUTH_ERROR_CODES.TENANT_NOT_FOUND:
              fail('No pudimos iniciar sesión. Verifique el código del tenant.');
            case AUTH_ERROR_CODES.TENANT_INACTIVE:
              fail('El tenant no está activo. Contacte al administrador.');
            case AUTH_ERROR_CODES.USER_NOT_ACTIVE:
              fail('Su cuenta no está activa. Contacte al administrador.');
            case AUTH_ERROR_CODES.SESSION_EXPIRED:
              fail('La sesión expiró. Vuelva a iniciar sesión.');
            default:
              fail(errorData.message || 'No pudimos iniciar sesión. Intente nuevamente.');
          }
        }

        if (response.status === 401) {
          fail('No pudimos iniciar sesión. Verifique sus credenciales.');
        }

        if (response.status === 429) {
          fail('Demasiados intentos fallidos. Espere unos minutos.');
        }

        fail('No pudimos iniciar sesión. Intente nuevamente.');
      }

      const data = await response.json();

      const authResponse: AuthResponse = {
        sessionId: data.sessionId,
        sessionTrackingId: data.sessionTrackingId || randomUuid(),
        userId: data.userId,
        username: data.username,
        email: data.email,
        tenantId: data.tenantId,
        tenantCode: data.tenantCode,
        tenantName: data.tenantName,
        role: data.role,
        roleName: data.roleName,
        profileId: data.profileId,
        permissions: data.permissions || [],
        language: data.language || 'es',
        token: data.token,
        tokenType: data.tokenType || 'Bearer',
        expiresIn: data.expiresIn || 3600,
        refreshExpiresIn: data.refreshExpiresIn || 86400,
        isInternalAdmin: data.isInternalAdmin || false,
        sessionParameters: data.sessionParameters ?? null,
        authorizationGraph: data.authorizationGraph ?? null,
      };

      return authResponse;
    } catch (error) {
      clearTimeout(timeoutId);

      if (error instanceof Error) {
        if (error.name === 'AbortError') {
          throw new Error('Tiempo de espera agotado. Verifique su conexión');
        }
        throw error;
      }

      throw new Error('Error de conexión. Verifique su red');
    }
  }

  async logout(): Promise<void> {
    try {
      await fetch(`${this.baseUrl}/logout`, {
        method: 'POST',
        headers: this.getAuthHeaders(),
        credentials: 'include',
      });
    } catch {}
  }

  async getSession(): Promise<AuthResponse | null> {
    try {
      const response = await fetch(`${this.baseUrl}/session`, {
        method: 'GET',
        headers: {
          ...this.getAuthHeaders(),
          Authorization: `Bearer ${this.getStoredToken()}`,
        },
        credentials: 'include',
      });

      if (!response.ok) {
        return null;
      }

      return await response.json();
    } catch {
      return null;
    }
  }

  private getStoredToken(): string | null {
    try {
      const stored = localStorage.getItem('ums-auth-storage');
      if (stored) {
        const parsed = JSON.parse(stored);
        return parsed.state?.user?.token || null;
      }
    } catch {}
    return null;
  }

  isAuthenticated(): boolean {
    try {
      const stored = localStorage.getItem('ums-auth-storage');
      if (!stored) return false;

      const parsed = JSON.parse(stored);
      const state = parsed.state;

      if (!state?.isAuthenticated || !state?.user) {
        return false;
      }

      if (state.sessionExpiresAt && Date.now() >= state.sessionExpiresAt) {
        return false;
      }

      return true;
    } catch {
      return false;
    }
  }
}

export const authService = new AuthService();
