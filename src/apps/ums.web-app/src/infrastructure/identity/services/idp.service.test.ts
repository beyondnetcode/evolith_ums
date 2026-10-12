import { describe, it, expect, vi, beforeEach } from 'vitest';
import { idpService } from './idp.service';
import * as httpClientModule from '@infra/http/httpClient';

vi.mock('@infra/http/httpClient', () => ({
  httpClient: {
    get: vi.fn(),
    post: vi.fn(),
    delete: vi.fn(),
  },
}));

describe('idpService', () => {
  beforeEach(() => {
    vi.mocked(httpClientModule.httpClient.get).mockClear();
    vi.mocked(httpClientModule.httpClient.post).mockClear();
    vi.mocked(httpClientModule.httpClient.delete).mockClear();
  });

  describe('getByIdentityProviders', () => {
    it('calls httpClient.get and parses response', async () => {
      const mockIdps = [
        {
          identityProviderId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
          code: 'AZURE',
          name: 'Azure AD',
          description: 'Azure Active Directory',
          strategy: 'OIDC',
          isActive: true,
        },
      ];
      vi.mocked(httpClientModule.httpClient.get).mockResolvedValue({
        data: mockIdps,
      });

      const result = await idpService.getByIdentityProviders(
        '3fa85f64-5717-4562-b3fc-2c963f66afa7'
      );

      expect(httpClientModule.httpClient.get).toHaveBeenCalledWith(
        '/tenants/3fa85f64-5717-4562-b3fc-2c963f66afa7/identity-providers'
      );
      expect(result).toHaveLength(1);
      expect(result[0].name).toBe('Azure AD');
    });

    it('throws on empty tenantId', async () => {
      await expect(idpService.getByIdentityProviders('')).rejects.toThrow(
        'Invalid tenantId parameter'
      );
    });
  });

  describe('registerIdentityProvider', () => {
    it('calls httpClient.post with correct payload', async () => {
      vi.mocked(httpClientModule.httpClient.post).mockResolvedValue({
        data: { identityProviderId: '3fa85f64-5717-4562-b3fc-2c963f66afa6' },
      });

      await idpService.registerIdentityProvider('3fa85f64-5717-4562-b3fc-2c963f66afa7', {
        name: 'Azure AD',
        code: 'AZURE',
        strategy: 'OIDC',
        description: 'Azure Active Directory',
      });

      expect(httpClientModule.httpClient.post).toHaveBeenCalledWith(
        '/tenants/3fa85f64-5717-4562-b3fc-2c963f66afa7/identity-providers',
        expect.objectContaining({
          name: 'Azure AD',
          code: 'AZURE',
        })
      );
    });

    it('returns identityProviderId', async () => {
      vi.mocked(httpClientModule.httpClient.post).mockResolvedValue({
        data: { identityProviderId: '3fa85f64-5717-4562-b3fc-2c963f66afa6' },
      });

      const result = await idpService.registerIdentityProvider(
        '3fa85f64-5717-4562-b3fc-2c963f66afa7',
        {
          name: 'Azure AD',
          code: 'AZURE',
          strategy: 'OIDC',
          description: 'Azure Active Directory',
        }
      );

      expect(result.identityProviderId).toBe('3fa85f64-5717-4562-b3fc-2c963f66afa6');
    });
  });

  describe('activateIdentityProvider', () => {
    it('calls activate endpoint', async () => {
      vi.mocked(httpClientModule.httpClient.post).mockResolvedValue({});

      await idpService.activateIdentityProvider(
        '3fa85f64-5717-4562-b3fc-2c963f66afa7',
        '3fa85f64-5717-4562-b3fc-2c963f66afa8'
      );

      expect(httpClientModule.httpClient.post).toHaveBeenCalledWith(
        '/tenants/3fa85f64-5717-4562-b3fc-2c963f66afa7/identity-providers/3fa85f64-5717-4562-b3fc-2c963f66afa8/activate'
      );
    });
  });

  describe('deactivateIdentityProvider', () => {
    it('calls deactivate endpoint', async () => {
      vi.mocked(httpClientModule.httpClient.post).mockResolvedValue({});

      await idpService.deactivateIdentityProvider(
        '3fa85f64-5717-4562-b3fc-2c963f66afa7',
        '3fa85f64-5717-4562-b3fc-2c963f66afa8'
      );

      expect(httpClientModule.httpClient.post).toHaveBeenCalledWith(
        '/tenants/3fa85f64-5717-4562-b3fc-2c963f66afa7/identity-providers/3fa85f64-5717-4562-b3fc-2c963f66afa8/deactivate'
      );
    });
  });

  describe('removeIdentityProvider', () => {
    it('calls delete endpoint', async () => {
      vi.mocked(httpClientModule.httpClient.delete).mockResolvedValue({});

      await idpService.removeIdentityProvider(
        '3fa85f64-5717-4562-b3fc-2c963f66afa7',
        '3fa85f64-5717-4562-b3fc-2c963f66afa8'
      );

      expect(httpClientModule.httpClient.delete).toHaveBeenCalledWith(
        '/tenants/3fa85f64-5717-4562-b3fc-2c963f66afa7/identity-providers/3fa85f64-5717-4562-b3fc-2c963f66afa8'
      );
    });
  });
});
