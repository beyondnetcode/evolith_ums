/**
 * Canonical UMS error code catalog. Mirror of `src/libs/sdk/contracts/error-codes.yaml`.
 * Codes are stable strings, never reused. See ADR-0073.
 */
export const UmsErrorCodes = {
  // Authentication — server-emitted (AUTH_001..AUTH_010)
  ValidationError: 'AUTH_001',
  TenantNotFound: 'AUTH_002',
  TenantNotActive: 'AUTH_003',
  IdpUserHasNoUmsAccount: 'AUTH_004',
  UserNotActive: 'AUTH_005',
  InvalidCredentials: 'AUTH_006',
  AccountLocked: 'AUTH_007',
  MfaChallengeRequired: 'AUTH_008',
  MfaChallengeFailed: 'AUTH_009',
  SessionExpired: 'AUTH_010',

  // IDP resolution — server-emitted (AUTH_011..AUTH_019)
  NoActiveIdpConfigured: 'AUTH_011',
  NoIdpAdapterRegistered: 'AUTH_012',
  IdpCallFailed: 'AUTH_013',
  IdpTokenValidationFailed: 'AUTH_014',

  // Authorization — SDK-emitted (AUTH_100..AUTH_199)
  ScopeNotGranted: 'AUTH_101',
  ScopeDenied: 'AUTH_102',
  MenuOptionNotGranted: 'AUTH_103',
  MenuOptionDenied: 'AUTH_104',
  DomainAccessNotGranted: 'AUTH_105',
  DomainAccessDenied: 'AUTH_106',
  FeatureFlagDisabled: 'AUTH_107',
  FeatureFlagNotFound: 'AUTH_108',
  TenantMismatch: 'AUTH_109',

  // Graph lifecycle — SDK-emitted (AUTH_200..AUTH_299)
  AuthGraphExpired: 'AUTH_201',
  AuthGraphMissing: 'AUTH_202',
  AuthGraphMalformed: 'AUTH_203',
  AuthGraphSchemaMissing: 'AUTH_204',
  AuthGraphSchemaUnsupported: 'AUTH_205',
  /**
   * El portador no supera la verificacion de firma. Antes no existia porque los SDK no la
   * hacian: descodificaban el token y se creian su contenido (G-217).
   */
  AuthGraphSignatureInvalid: 'AUTH_206'
} as const;

export type UmsErrorCode = (typeof UmsErrorCodes)[keyof typeof UmsErrorCodes];
