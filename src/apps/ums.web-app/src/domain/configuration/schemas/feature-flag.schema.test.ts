import { describe, expect, it } from 'vitest';
import {
  FlagTypeSchema,
  FlagStatusSchema,
  CriteriaTypeSchema,
  CriteriaOperatorSchema,
  FeatureFlagSchema,
  FeatureFlagCriteriaSchema,
  FeatureFlagPageSchema,
  CreateFeatureFlagResponseSchema,
} from './feature-flag.schema';

describe('Feature flag enum schemas', () => {
  it('accepts valid FlagType values', () => {
    expect(FlagTypeSchema.parse('Boolean')).toBe('Boolean');
    expect(FlagTypeSchema.parse('Variant')).toBe('Variant');
    expect(FlagTypeSchema.parse('Percentage')).toBe('Percentage');
  });

  it('rejects invalid FlagType', () => {
    expect(() => FlagTypeSchema.parse('Invalid')).toThrow();
  });

  it('accepts valid FlagStatus values', () => {
    expect(FlagStatusSchema.parse('Inactive')).toBe('Inactive');
    expect(FlagStatusSchema.parse('Active')).toBe('Active');
    expect(FlagStatusSchema.parse('Archived')).toBe('Archived');
  });

  it('accepts valid CriteriaType values', () => {
    const types = [
      'TenantId',
      'BranchId',
      'UserProfileId',
      'RoleCode',
      'Environment',
      'DateRange',
      'PercentageHash',
      'CustomRule',
    ];
    types.forEach(type => {
      expect(CriteriaTypeSchema.parse(type)).toBe(type);
    });
  });

  it('accepts valid CriteriaOperator values', () => {
    const operators = ['Equals', 'NotEquals', 'In', 'Between', 'LessThanOrEqual', 'Matches'];
    operators.forEach(op => {
      expect(CriteriaOperatorSchema.parse(op)).toBe(op);
    });
  });
});

describe('FeatureFlagCriteriaSchema', () => {
  it('accepts a valid criteria payload', () => {
    const criteria = FeatureFlagCriteriaSchema.parse({
      criteriaId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
      criteriaType: 'TenantId',
      operator: 'Equals',
      value: 'tenant-123',
      createdAtUtc: '2024-01-01T00:00:00Z',
    });

    expect(criteria.criteriaType).toBe('TenantId');
    expect(criteria.operator).toBe('Equals');
  });

  it('rejects invalid GUID', () => {
    expect(() =>
      FeatureFlagCriteriaSchema.parse({
        criteriaId: 'invalid',
        criteriaType: 'TenantId',
        operator: 'Equals',
        value: 'test',
        createdAtUtc: '2024-01-01',
      })
    ).toThrow();
  });
});

describe('FeatureFlagSchema', () => {
  const validFlag = {
    featureFlagId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
    systemSuiteId: '3fa85f64-5717-4562-b3fc-2c963f66afa7',
    systemSuiteCode: 'CORE',
    systemSuiteName: 'Core Suite',
    flagCode: 'DARK_MODE',
    flagType: 'Boolean',
    flagTargets: 'all',
    status: 'Active',
    criteria: [],
  };

  it('accepts a valid feature flag payload', () => {
    const flag = FeatureFlagSchema.parse(validFlag);
    expect(flag.flagCode).toBe('DARK_MODE');
    expect(flag.flagType).toBe('Boolean');
    expect(flag.status).toBe('Active');
  });

  it('defaults criteria to empty array', () => {
    const flag = FeatureFlagSchema.parse({
      ...validFlag,
    });
    expect(flag.criteria).toEqual([]);
  });

  it('accepts optional linkedResourceType', () => {
    const flag = FeatureFlagSchema.parse({
      ...validFlag,
      linkedResourceType: 'Tenant',
      linkedResourceId: '3fa85f64-5717-4562-b3fc-2c963f66afa8',
    });
    expect(flag.linkedResourceType).toBe('Tenant');
  });

  it('accepts valid rolloutPercentage', () => {
    const flag = FeatureFlagSchema.parse({
      ...validFlag,
      rolloutPercentage: 50,
    });
    expect(flag.rolloutPercentage).toBe(50);
  });

  it('rejects rolloutPercentage over 100', () => {
    expect(() => FeatureFlagSchema.parse({ ...validFlag, rolloutPercentage: 101 })).toThrow();
  });

  it('rejects negative rolloutPercentage', () => {
    expect(() => FeatureFlagSchema.parse({ ...validFlag, rolloutPercentage: -1 })).toThrow();
  });

  it('rejects empty flagCode', () => {
    expect(() => FeatureFlagSchema.parse({ ...validFlag, flagCode: '' })).toThrow();
  });
});

describe('FeatureFlagPageSchema', () => {
  it('accepts a valid page payload', () => {
    const page = FeatureFlagPageSchema.parse({
      items: [],
      page: 1,
      pageSize: 10,
      totalItems: 0,
      totalPages: 0,
    });

    expect(page.page).toBe(1);
  });

  it('rejects page less than 1', () => {
    expect(() =>
      FeatureFlagPageSchema.parse({
        items: [],
        page: 0,
        pageSize: 10,
        totalItems: 0,
        totalPages: 0,
      })
    ).toThrow();
  });
});

describe('CreateFeatureFlagResponseSchema', () => {
  it('accepts a valid response', () => {
    const response = CreateFeatureFlagResponseSchema.parse({
      featureFlagId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
    });

    expect(response.featureFlagId).toBe('3fa85f64-5717-4562-b3fc-2c963f66afa6');
  });
});
