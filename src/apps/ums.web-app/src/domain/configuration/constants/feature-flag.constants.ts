/**
 * feature-flag.constants.ts — Domain constants for Feature Flags.
 *
 * Following Configuration Catalog Standard (R-13):
 * All configurable entities must follow code/value/description pattern.
 */

export const FEATURE_FLAG_PAGE_SIZE = 20;

export const FLAG_TYPE_LABELS: Record<string, string> = {
  Boolean: 'Boolean',
  Variant: 'Variant',
  Percentage: 'Percentage',
};

export const FLAG_STATUS_LABELS: Record<string, string> = {
  Inactive: 'Inactive',
  Active: 'Active',
  Archived: 'Archived',
};

export const ROLLOUT_CONSTRAINTS = {
  MIN: 0,
  MAX: 100,
  STEP: 1,
} as const;

export const FLAG_TARGETS = {
  ALL: '*',
  NONE: 'none',
} as const;

export const CRITERIA_TYPE_LABELS: Record<string, string> = {
  TenantId: 'Tenant',
  BranchId: 'Branch',
  UserProfileId: 'User Profile',
  RoleCode: 'Role Code',
  Environment: 'Environment',
  DateRange: 'Date Range',
  PercentageHash: 'Percentage Hash',
  CustomRule: 'Custom Rule',
};

export const CRITERIA_OPERATOR_LABELS: Record<string, string> = {
  Equals: 'Equals',
  NotEquals: 'Not Equals',
  In: 'In (list)',
  Between: 'Between',
  LessThanOrEqual: 'Less Than or Equal',
  Matches: 'Matches',
};

/** Operators valid for each CriteriaType */
export const CRITERIA_TYPE_OPERATORS: Record<string, string[]> = {
  TenantId: ['Equals', 'NotEquals'],
  BranchId: ['Equals', 'NotEquals'],
  UserProfileId: ['Equals', 'NotEquals'],
  RoleCode: ['Equals', 'NotEquals', 'In'],
  Environment: ['Equals', 'NotEquals', 'In'],
  DateRange: ['Between'],
  PercentageHash: ['LessThanOrEqual'],
  CustomRule: ['Matches'],
};
