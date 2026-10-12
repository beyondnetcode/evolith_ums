import { describe, it, expect } from 'vitest';
import { IDP_STRATEGIES } from '@domain/identity/constants/idp.constants';

describe('idp.constants', () => {
  it('expone estrategias que coinciden con el enum IdpStrategy del backend (G-155)', () => {
    // Deben ser nombres EXACTOS del enum backend; OIDC/OAuth2 no existían y daban 400.
    expect(IDP_STRATEGIES).toContain('GenericOidc');
    expect(IDP_STRATEGIES).toContain('Saml2');
    expect(IDP_STRATEGIES).toContain('AzureAd');
    expect(IDP_STRATEGIES).not.toContain('OIDC');
    expect(IDP_STRATEGIES).not.toContain('OAuth2');
  });
});
