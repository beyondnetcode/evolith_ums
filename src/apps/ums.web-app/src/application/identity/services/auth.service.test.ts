import { beforeEach, describe, expect, it, vi } from 'vitest';
import { authService } from './auth.service';

describe('authService routing', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn());
    vi.stubGlobal('crypto', {
      randomUUID: vi.fn(() => 'test-request-id'),
    } as unknown as Crypto);
  });

  it('keeps the visual login on the session auth API and not on the client graph API', async () => {
    vi.mocked(fetch).mockResolvedValue({
      ok: true,
      json: vi.fn().mockResolvedValue({
        sessionId: 'session-id',
        sessionTrackingId: 'tracking-id',
        userId: 'user-id',
        username: 'gerente.operaciones',
        email: 'gerente.operaciones@ransa.pe',
        tenantId: 'tenant-id',
        tenantCode: 'RANSA_PERU',
        tenantName: 'Ransa Comercial S.A.',
        role: 'ADMIN',
        roleName: 'Administrator',
        profileId: 'profile-id',
        permissions: [],
        language: 'es',
        token: 'jwt-token',
        tokenType: 'Bearer',
        expiresIn: 3600,
        refreshExpiresIn: 86400,
        isInternalAdmin: false,
        sessionParameters: null,
      }),
    } as unknown as Response);

    await authService.login({
      tenantCode: 'ransa_peru',
      username: ' gerente.operaciones@ransa.pe ',
      password: 'secret',
    });

    expect(fetch).toHaveBeenCalledTimes(1);
    expect(fetch).toHaveBeenCalledWith(
      '/api/v1/auth/login',
      expect.objectContaining({
        method: 'POST',
        credentials: 'include',
      })
    );

    const [requestUrl, requestInit] = vi.mocked(fetch).mock.calls[0];
    expect(requestUrl).not.toContain('/api/v1/client/authenticate');
    expect(requestInit?.body).toBeDefined();

    const parsedBody = JSON.parse(String(requestInit?.body));
    expect(parsedBody.tenantCode).toBe('RANSA_PERU');
    expect(parsedBody.username).toBe('gerente.operaciones@ransa.pe');

    // G-184: el portal pide SU sistema. Sin este campo, UMS resuelve el grafo por desempate y un
    // usuario con perfiles en varios sistemas recibe aqui el de otro producto —lo medido fue `SIL`
    // para el administrador—, con lo que el shell pinta menus que este router no sirve.
    expect(parsedBody.systemCode).toBe('UMS');
  });
});
