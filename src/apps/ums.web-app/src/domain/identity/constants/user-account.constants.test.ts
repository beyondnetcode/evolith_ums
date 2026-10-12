import { describe, it, expect } from 'vitest';
import {
  USER_CATEGORIES,
  USER_STATUSES,
  IDENTITY_REFERENCE_TYPES,
  USER_ACCOUNT_PAGE_SIZE,
} from '@domain/identity/constants/user-account.constants';

describe('user-account.constants', () => {
  it('exports correct user categories', () => {
    expect(USER_CATEGORIES).toContain('Internal');
    expect(USER_CATEGORIES).toContain('External');
    expect(USER_CATEGORIES).toContain('B2B');
    expect(USER_CATEGORIES).toContain('Partner');
    expect(USER_CATEGORIES).toContain('ServiceAccount');
    expect(USER_CATEGORIES).toHaveLength(5);
  });

  it('exports correct user statuses', () => {
    expect(USER_STATUSES).toContain('Pending');
    expect(USER_STATUSES).toContain('Active');
    expect(USER_STATUSES).toContain('Blocked');
    expect(USER_STATUSES).toHaveLength(3);
  });

  it('exports correct identity reference types', () => {
    expect(IDENTITY_REFERENCE_TYPES).toContain('HrId');
    expect(IDENTITY_REFERENCE_TYPES).toContain('VendorCode');
    expect(IDENTITY_REFERENCE_TYPES).toContain('GovernmentId');
    expect(IDENTITY_REFERENCE_TYPES).toContain('PartnerRef');
    expect(IDENTITY_REFERENCE_TYPES).toHaveLength(4);
  });

  it('exports correct page size', () => {
    expect(USER_ACCOUNT_PAGE_SIZE).toBe(20);
  });
});
