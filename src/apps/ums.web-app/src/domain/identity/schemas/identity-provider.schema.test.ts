import { describe, expect, it } from 'vitest';
import {
  IdentityProviderSchema,
  IdentityProviderListSchema,
  RegisterIdentityProviderPayloadSchema,
} from './identity-provider.schema';

describe('IdentityProviderSchema', () => {
  it('accepts a valid identity provider', () => {
    const provider = IdentityProviderSchema.parse({
      identityProviderId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
      code: 'AZURE_AD',
      name: 'Azure AD',
      description: 'Azure Active Directory',
      strategy: 'oidc',
      isActive: true,
    });

    expect(provider.code).toBe('AZURE_AD');
    expect(provider.isActive).toBe(true);
  });

  it('defaults description to empty string', () => {
    const provider = IdentityProviderSchema.parse({
      identityProviderId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
      code: 'GOOGLE',
      name: 'Google',
      strategy: 'oauth2',
      isActive: true,
    });

    expect(provider.description).toBe('');
  });

  it('rejects empty code', () => {
    expect(() =>
      IdentityProviderSchema.parse({
        identityProviderId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
        code: '',
        name: 'Test',
        strategy: 'oidc',
        isActive: true,
      })
    ).toThrow();
  });

  it('rejects empty name', () => {
    expect(() =>
      IdentityProviderSchema.parse({
        identityProviderId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
        code: 'TEST',
        name: '',
        strategy: 'oidc',
        isActive: true,
      })
    ).toThrow();
  });

  it('rejects non-UUID identityProviderId', () => {
    expect(() =>
      IdentityProviderSchema.parse({
        identityProviderId: 'invalid',
        code: 'TEST',
        name: 'Test',
        strategy: 'oidc',
        isActive: true,
      })
    ).toThrow();
  });
});

describe('IdentityProviderListSchema', () => {
  it('accepts an array of providers', () => {
    const providers = IdentityProviderListSchema.parse([
      {
        identityProviderId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
        code: 'AZURE_AD',
        name: 'Azure AD',
        strategy: 'oidc',
        isActive: true,
      },
      {
        identityProviderId: '3fa85f64-5717-4562-b3fc-2c963f66afa7',
        code: 'GOOGLE',
        name: 'Google',
        strategy: 'oauth2',
        isActive: false,
      },
    ]);

    expect(providers).toHaveLength(2);
    expect(providers[0].code).toBe('AZURE_AD');
  });
});

describe('RegisterIdentityProviderPayloadSchema', () => {
  it('accepts a valid payload', () => {
    const payload = RegisterIdentityProviderPayloadSchema.parse({
      code: 'OKTA',
      name: 'Okta',
      strategy: 'oidc',
    });

    expect(payload.code).toBe('OKTA');
  });

  it('accepts optional description', () => {
    const payload = RegisterIdentityProviderPayloadSchema.parse({
      code: 'OKTA',
      name: 'Okta',
      description: 'Okta Identity Provider',
      strategy: 'oidc',
    });

    expect(payload.description).toBe('Okta Identity Provider');
  });

  it('rejects empty code', () => {
    expect(() =>
      RegisterIdentityProviderPayloadSchema.parse({
        code: '',
        name: 'Test',
        strategy: 'oidc',
      })
    ).toThrow();
  });

  it('rejects empty strategy', () => {
    expect(() =>
      RegisterIdentityProviderPayloadSchema.parse({
        code: 'TEST',
        name: 'Test',
        strategy: '',
      })
    ).toThrow();
  });
});
