import { describe, expect, it } from 'vitest';
import { usesExternalIdentityProvider } from './UserAccountPasswordPanel';

describe('usesExternalIdentityProvider', () => {
  it('returns false for local tenant authentication even when user accounts have business identity references', () => {
    expect(
      usesExternalIdentityProvider({
        items: [{ code: 'AUTH_USE_EXTERNAL_IDP', value: 'false' }],
      })
    ).toBe(false);
  });

  it('returns true only when tenant configuration enables external identity provider authentication', () => {
    expect(
      usesExternalIdentityProvider({
        items: [{ code: 'AUTH_USE_EXTERNAL_IDP', value: 'true' }],
      })
    ).toBe(true);
  });
});
