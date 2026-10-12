import { describe, it, expect } from 'vitest';
import {
  FEATURE_FLAG_PAGE_SIZE,
  FLAG_TYPE_LABELS,
  FLAG_STATUS_LABELS,
  CRITERIA_TYPE_LABELS,
  CRITERIA_OPERATOR_LABELS,
  CRITERIA_TYPE_OPERATORS,
} from '@domain/configuration/constants/feature-flag.constants';

describe('feature-flag.constants', () => {
  it('exports correct page size', () => {
    expect(FEATURE_FLAG_PAGE_SIZE).toBe(20);
  });

  it('exports flag type labels', () => {
    expect(FLAG_TYPE_LABELS).toHaveProperty('Boolean');
    expect(FLAG_TYPE_LABELS).toHaveProperty('Variant');
    expect(FLAG_TYPE_LABELS).toHaveProperty('Percentage');
    expect(Object.keys(FLAG_TYPE_LABELS)).toHaveLength(3);
  });

  it('exports flag status labels', () => {
    expect(FLAG_STATUS_LABELS).toHaveProperty('Inactive');
    expect(FLAG_STATUS_LABELS).toHaveProperty('Active');
    expect(FLAG_STATUS_LABELS).toHaveProperty('Archived');
    expect(Object.keys(FLAG_STATUS_LABELS)).toHaveLength(3);
  });

  it('exports criteria type labels', () => {
    expect(CRITERIA_TYPE_LABELS).toHaveProperty('TenantId');
    expect(CRITERIA_TYPE_LABELS).toHaveProperty('BranchId');
    expect(CRITERIA_TYPE_LABELS).toHaveProperty('UserProfileId');
    expect(CRITERIA_TYPE_LABELS).toHaveProperty('RoleCode');
    expect(CRITERIA_TYPE_LABELS).toHaveProperty('Environment');
    expect(CRITERIA_TYPE_LABELS).toHaveProperty('DateRange');
    expect(CRITERIA_TYPE_LABELS).toHaveProperty('PercentageHash');
    expect(CRITERIA_TYPE_LABELS).toHaveProperty('CustomRule');
    expect(Object.keys(CRITERIA_TYPE_LABELS)).toHaveLength(8);
  });

  it('exports criteria operator labels', () => {
    expect(CRITERIA_OPERATOR_LABELS).toHaveProperty('Equals');
    expect(CRITERIA_OPERATOR_LABELS).toHaveProperty('NotEquals');
    expect(CRITERIA_OPERATOR_LABELS).toHaveProperty('In');
    expect(CRITERIA_OPERATOR_LABELS).toHaveProperty('Between');
    expect(CRITERIA_OPERATOR_LABELS).toHaveProperty('LessThanOrEqual');
    expect(CRITERIA_OPERATOR_LABELS).toHaveProperty('Matches');
    expect(Object.keys(CRITERIA_OPERATOR_LABELS)).toHaveLength(6);
  });

  it('exports criteria type operators mapping', () => {
    expect(CRITERIA_TYPE_OPERATORS['TenantId']).toEqual(['Equals', 'NotEquals']);
    expect(CRITERIA_TYPE_OPERATORS['BranchId']).toEqual(['Equals', 'NotEquals']);
    expect(CRITERIA_TYPE_OPERATORS['RoleCode']).toContain('In');
    expect(CRITERIA_TYPE_OPERATORS['DateRange']).toEqual(['Between']);
    expect(CRITERIA_TYPE_OPERATORS['PercentageHash']).toEqual(['LessThanOrEqual']);
    expect(CRITERIA_TYPE_OPERATORS['CustomRule']).toEqual(['Matches']);
  });
});
