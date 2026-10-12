import { describe, expect, it } from 'vitest';
import {
  CreateFeatureFlagPayloadSchema,
  UpdateFeatureFlagPayloadSchema,
  AddFeatureFlagCriteriaPayloadSchema,
} from './feature-flag.commands.schema';

describe('CreateFeatureFlagPayloadSchema', () => {
  const validPayload = {
    systemSuiteId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
    tenantId: '3fa85f64-5717-4562-b3fc-2c963f66afa7',
    flagCode: 'FEATURE_FLAG_001',
    flagType: 'Boolean' as const,
    flagTargets: '*',
    rolloutPercentage: 50,
  };

  it('accepts a valid payload', () => {
    const result = CreateFeatureFlagPayloadSchema.parse(validPayload);
    expect(result.flagCode).toBe('FEATURE_FLAG_001');
  });

  it('accepts nullable tenantId', () => {
    const result = CreateFeatureFlagPayloadSchema.parse({ ...validPayload, tenantId: null });
    expect(result.tenantId).toBeNull();
  });

  it('accepts undefined tenantId', () => {
    const result = CreateFeatureFlagPayloadSchema.parse({ ...validPayload, tenantId: undefined });
    expect(result.tenantId).toBeUndefined();
  });

  it('defaults flagTargets to asterisk', () => {
    const { flagTargets } = CreateFeatureFlagPayloadSchema.parse({
      systemSuiteId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
      flagCode: 'TEST_FLAG',
      flagType: 'Boolean',
    });
    expect(flagTargets).toBe('*');
  });

  it('accepts null rolloutPercentage', () => {
    const result = CreateFeatureFlagPayloadSchema.parse({
      ...validPayload,
      rolloutPercentage: null,
    });
    expect(result.rolloutPercentage).toBeNull();
  });

  it('rejects invalid systemSuiteId', () => {
    expect(() =>
      CreateFeatureFlagPayloadSchema.parse({ ...validPayload, systemSuiteId: 'not-a-uuid' })
    ).toThrow();
  });

  it('rejects empty flagCode', () => {
    expect(() => CreateFeatureFlagPayloadSchema.parse({ ...validPayload, flagCode: '' })).toThrow();
  });

  it('rejects flagCode with lowercase letters', () => {
    expect(() =>
      CreateFeatureFlagPayloadSchema.parse({ ...validPayload, flagCode: 'feature_flag' })
    ).toThrow();
  });

  it('rejects flagCode exceeding max length', () => {
    expect(() =>
      CreateFeatureFlagPayloadSchema.parse({ ...validPayload, flagCode: 'A'.repeat(101) })
    ).toThrow();
  });

  it('rejects negative rolloutPercentage', () => {
    expect(() =>
      CreateFeatureFlagPayloadSchema.parse({ ...validPayload, rolloutPercentage: -1 })
    ).toThrow();
  });

  it('rejects rolloutPercentage over 100', () => {
    expect(() =>
      CreateFeatureFlagPayloadSchema.parse({ ...validPayload, rolloutPercentage: 101 })
    ).toThrow();
  });
});

describe('UpdateFeatureFlagPayloadSchema', () => {
  const validPayload = {
    flagTargets: 'target-group',
    rolloutPercentage: 75,
  };

  it('accepts a valid payload', () => {
    const result = UpdateFeatureFlagPayloadSchema.parse(validPayload);
    expect(result.flagTargets).toBe('target-group');
  });

  it('accepts null rolloutPercentage', () => {
    const result = UpdateFeatureFlagPayloadSchema.parse({
      ...validPayload,
      rolloutPercentage: null,
    });
    expect(result.rolloutPercentage).toBeNull();
  });

  it('rejects empty flagTargets', () => {
    expect(() => UpdateFeatureFlagPayloadSchema.parse({ flagTargets: '' })).toThrow();
  });
});

describe('AddFeatureFlagCriteriaPayloadSchema', () => {
  const validPayload = {
    criteriaType: 'TenantId' as const,
    operator: 'Equals' as const,
    value: 'test-value',
  };

  it('accepts a valid payload', () => {
    const result = AddFeatureFlagCriteriaPayloadSchema.parse(validPayload);
    expect(result.value).toBe('test-value');
  });

  it('rejects empty value', () => {
    expect(() =>
      AddFeatureFlagCriteriaPayloadSchema.parse({ ...validPayload, value: '' })
    ).toThrow();
  });

  it('rejects invalid criteriaType', () => {
    expect(() =>
      AddFeatureFlagCriteriaPayloadSchema.parse({ ...validPayload, criteriaType: 'Invalid' })
    ).toThrow();
  });

  it('rejects invalid operator', () => {
    expect(() =>
      AddFeatureFlagCriteriaPayloadSchema.parse({ ...validPayload, operator: 'Invalid' })
    ).toThrow();
  });
});
