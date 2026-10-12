/**
 * Reflector metadata keys used by the Nest decorators and the `UmsAuthGuard`.
 * Same handler can carry several entries (decorator stacking).
 */
export const UMS_AUTH_METADATA = 'ums:auth:requirements';

export type UmsAuthPrimitiveType =
  | 'RequiresScope'
  | 'RequiresMenuOption'
  | 'RequiresDomainAccess'
  | 'RequiresFeatureFlag';

export type UmsDenialBehavior = 'throw' | 'returnFailure' | 'ignore';

interface BaseRequirement {
  readonly type: UmsAuthPrimitiveType;
  readonly onDenied?: UmsDenialBehavior;
  readonly auditOnly?: boolean;
}

export interface ScopeRequirement extends BaseRequirement {
  readonly type: 'RequiresScope';
  readonly scope: string;
}

export interface MenuOptionRequirement extends BaseRequirement {
  readonly type: 'RequiresMenuOption';
  readonly optionCode: string;
}

export interface DomainAccessRequirement extends BaseRequirement {
  readonly type: 'RequiresDomainAccess';
  readonly resourceCode: string;
  readonly actionCode: string;
}

export interface FeatureFlagRequirement extends BaseRequirement {
  readonly type: 'RequiresFeatureFlag';
  readonly flagCode: string;
}

export type UmsAuthRequirement =
  | ScopeRequirement
  | MenuOptionRequirement
  | DomainAccessRequirement
  | FeatureFlagRequirement;
