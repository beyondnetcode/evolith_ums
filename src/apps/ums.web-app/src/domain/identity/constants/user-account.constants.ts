export const USER_CATEGORIES = [
  'Internal',
  'External',
  'B2B',
  'Partner',
  'ServiceAccount',
] as const;
export type UserCategoryType = (typeof USER_CATEGORIES)[number];

export const USER_STATUSES = ['Pending', 'Active', 'Blocked'] as const;
export type UserStatusType = (typeof USER_STATUSES)[number];

export const IDENTITY_REFERENCE_TYPES = [
  'HrId',
  'VendorCode',
  'GovernmentId',
  'PartnerRef',
] as const;
export type IdentityReferenceTypeType = (typeof IDENTITY_REFERENCE_TYPES)[number];

export const USER_ACCOUNT_PAGE_SIZE = 20 as const;
