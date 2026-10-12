export type IdpStrategy = 'OIDC' | 'SAML2' | 'OAuth2';

export interface AuthProvider {
  id: string;
  code: string;
  name: string;
  description: string;
  strategy: IdpStrategy;
  isActive: boolean;
}

export interface CreateAuthProviderPayload {
  code: string;
  name: string;
  description: string;
  strategy: IdpStrategy;
}

export interface UpdateAuthProviderPayload {
  id: string;
  code: string;
  name: string;
  description: string;
  strategy: IdpStrategy;
}
