/**
 * idp.constants.ts — Domain constants for Identity Providers.
 *
 * Centralises valid IdP strategy values referenced by forms and
 * panels so changes propagate from one place.
 */

/**
 * Estrategias de proveedor de identidad EXTERNO que acepta el backend (enum `IdpStrategy`,
 * DomainEnums.cs). Los valores deben coincidir EXACTO con los nombres del enum o el alta da 400
 * («Invalid identity provider strategy»). Se omite `InternalBcrypt` (auth local, no es un proveedor
 * externo registrable). (G-155)
 */
export const IDP_STRATEGIES = [
  'GenericOidc',
  'Saml2',
  'AzureAd',
  'Okta',
  'Keycloak',
  'Auth0',
  'Google',
  'Zitadel',
  'Ldap',
] as const;
export type IdpStrategy = (typeof IDP_STRATEGIES)[number];
