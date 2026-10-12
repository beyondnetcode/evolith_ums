import { describe, it, expect } from 'vitest';
import type {
  IdpStrategy,
  AuthProvider,
  CreateAuthProviderPayload,
  UpdateAuthProviderPayload,
} from '@domain/identity/models/idp.model';

describe('idp.model', () => {
  it('defines IdpStrategy type values', () => {
    const strategies: IdpStrategy[] = ['OIDC', 'SAML2', 'OAuth2'];
    expect(strategies).toHaveLength(3);
    expect(strategies).toContain('OIDC');
    expect(strategies).toContain('SAML2');
    expect(strategies).toContain('OAuth2');
  });

  it('allows valid AuthProvider object shape', () => {
    const provider: AuthProvider = {
      id: '123e4567-e89b-12d3-a456-426614174000',
      code: 'google',
      name: 'Google',
      description: 'Google OAuth2 Provider',
      strategy: 'OIDC',
      isActive: true,
    };
    expect(provider.id).toBeDefined();
    expect(provider.strategy).toBe('OIDC');
    expect(provider.isActive).toBe(true);
  });

  it('allows valid CreateAuthProviderPayload object shape', () => {
    const payload: CreateAuthProviderPayload = {
      code: 'azure',
      name: 'Azure AD',
      description: 'Azure Active Directory',
      strategy: 'SAML2',
    };
    expect(payload.code).toBe('azure');
    expect(payload.strategy).toBe('SAML2');
  });

  it('allows valid UpdateAuthProviderPayload object shape', () => {
    const payload: UpdateAuthProviderPayload = {
      id: '123e4567-e89b-12d3-a456-426614174000',
      code: 'okta',
      name: 'Okta',
      description: 'Okta Identity Provider',
      strategy: 'OAuth2',
    };
    expect(payload.id).toBeDefined();
    expect(payload.strategy).toBe('OAuth2');
  });
});
