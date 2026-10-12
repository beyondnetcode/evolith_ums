// Autenticación del arnés RoboSoft (carril B).
//
// En entorno Development el backend expone DOS caminos de identidad:
//  1. Login real: POST /api/v1/auth/login → devuelve `token` (JWT) y datos de sesión.
//  2. DevAuthMiddleware: acepta identidad por cabeceras `X-User-Id` / `X-Tenant-Id` /
//     `X-Is-Internal-Admin` para toda petición fuera de /api/v1/auth y /api/v1/client.
//     Es el patrón usado por los tests de integración y permite actuar como distintos
//     usuarios/inquilinos al verificar invariantes (p. ej. segregación de deberes).
//
// El carril B usa DevAuth por su determinismo (no depende de expiración de tokens) y por
// poder personificar a un solicitante distinto del aprobador sin aprovisionar credenciales.
import type { APIRequestContext } from '@playwright/test';

/** Inquilino raíz (propietario de gestión) sembrado: EVOLITH. Ver DevAuthMiddleware / ADR-0071. */
export const EVOLITH_TENANT_ID = '5f4e3d2c-1b0a-9f8e-7d6c-543210987654';

/** Usuario administrador interno de EVOLITH (categoría Internal), sembrado de forma determinista. */
export const EVOLITH_ADMIN_USER_ID = '5f4e3d14-1b0a-9f8e-7d6c-543210987654';

export const API = '/api/v1';

export interface DevIdentity {
  /** GUID a inyectar como X-User-Id. Si se omite, DevAuth usa su identidad por defecto (admin EVOLITH). */
  userId?: string;
  tenantId?: string;
  internalAdmin?: boolean;
}

/**
 * Construye las cabeceras DevAuth para personificar una identidad.
 * Sin `userId` devuelve un objeto vacío: DevAuth aplica su identidad por defecto
 * (administrador interno de EVOLITH, propietario de gestión), útil como aprobador.
 */
export function devAuthHeaders(identity: DevIdentity = {}): Record<string, string> {
  const headers: Record<string, string> = {};
  if (identity.userId) headers['X-User-Id'] = identity.userId;
  if (identity.userId) {
    headers['X-Tenant-Id'] = identity.tenantId ?? EVOLITH_TENANT_ID;
    headers['X-Is-Internal-Admin'] = String(identity.internalAdmin ?? true);
  }
  return headers;
}

// Credenciales SEMBRADAS de forma determinista (semilla del clúster). Los tenants CLIENT
// (COMEX_ANDINA, AGRONORTE) usan autenticación local BCrypt (authUseExternalIdp=off).
// Se usan SOLO con contraseña correcta en los positivos; los negativos usan usuarios
// INEXISTENTES para no arriesgar el lockout (FR-017) de cuentas reales del clúster compartido.
export const CRED_COMEX = {
  tenantCode: 'COMEX_ANDINA',
  username: 'usuario.impo@comexandina.com.pe',
  password: 'Evolith.Dev.2026',
} as const;

export const CRED_AGRONORTE = {
  tenantCode: 'AGRONORTE',
  username: 'usuario.expo@agronorte.com.pe',
  password: 'Evolith.Dev.2026',
} as const;

export interface Credenciales {
  tenantCode: string;
  username: string;
  password: string;
}

/** POST /api/v1/auth/login (camino de autenticación real). Devuelve el APIResponse crudo. */
export const postLogin = (request: APIRequestContext, creds: Partial<Credenciales>) =>
  request.post(`${API}/auth/login`, { data: creds });

/** POST /api/v1/client/authenticate (endpoint de integración para tenants CLIENT). */
export const postClientAuthenticate = (request: APIRequestContext, creds: Partial<Credenciales>) =>
  request.post(`${API}/client/authenticate`, { data: creds });

/**
 * Login real contra /api/v1/auth/login. Devuelve el JWT. Se ofrece para invariantes que
 * necesiten ejercer el camino de autenticación real; el resto del arnés usa DevAuth.
 */
export async function login(
  request: APIRequestContext,
  credentials = {
    tenantCode: 'EVOLITH',
    username: 'admin@evolith.example',
    password: 'Evolith.Dev.2026',
  },
): Promise<{ token: string; userId: string; tenantId: string }> {
  const res = await request.post(`${API}/auth/login`, { data: credentials });
  if (!res.ok()) {
    throw new Error(`Login falló (${res.status()}): ${await res.text()}`);
  }
  const body = await res.json();
  return { token: body.token, userId: body.userId, tenantId: body.tenantId };
}
