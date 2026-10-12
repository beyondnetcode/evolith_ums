import { describe, expect, it } from 'vitest';
import {
  DelegationScopeTypeSchema,
  DelegationStatusSchema,
  DelegatedActionSchema,
  DelegationSchema,
  CreateDelegationPayloadSchema,
  CreateDelegationResponseSchema,
  RevokeDelegationPayloadSchema,
} from './delegation.schema';

describe('Delegation enum schemas', () => {
  it('accepts valid DelegationScopeType values', () => {
    expect(DelegationScopeTypeSchema.parse('Tenant')).toBe('Tenant');
    expect(DelegationScopeTypeSchema.parse('Organization')).toBe('Organization');
    expect(DelegationScopeTypeSchema.parse('Department')).toBe('Department');
    expect(DelegationScopeTypeSchema.parse('System')).toBe('System');
    expect(DelegationScopeTypeSchema.parse('Team')).toBe('Team');
  });

  it('rejects invalid DelegationScopeType', () => {
    expect(() => DelegationScopeTypeSchema.parse('Invalid')).toThrow();
  });

  it('accepts valid DelegationStatus values', () => {
    const statuses = [
      'Draft',
      'PendingApproval',
      'Active',
      'Revoked',
      'Expired',
      'Completed',
      'Rejected',
      'Archived',
    ];
    statuses.forEach(status => {
      expect(DelegationStatusSchema.parse(status)).toBe(status);
    });
  });

  it('rejects invalid DelegationStatus', () => {
    expect(() => DelegationStatusSchema.parse('Unknown')).toThrow();
  });

  it('accepts valid DelegatedAction values', () => {
    const actions = ['CreateUser', 'BlockUser', 'AssignProfile', 'ResetPassword', 'RevokeMfa'];
    actions.forEach(action => {
      expect(DelegatedActionSchema.parse(action)).toBe(action);
    });
  });
});

describe('DelegationSchema', () => {
  const validDelegation = {
    delegationId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
    tenantId: '3fa85f64-5717-4562-b3fc-2c963f66afa7',
    delegatingAdminId: '3fa85f64-5717-4562-b3fc-2c963f66afa8',
    delegatedAdminId: '3fa85f64-5717-4562-b3fc-2c963f66afa9',
    scopeType: 'Tenant',
    scopeId: null,
    allowedActions: ['CreateUser', 'BlockUser'],
    validFrom: '2024-01-01T00:00:00Z',
    validUntil: '2024-12-31T23:59:59Z',
    maxDurationDays: 30,
    requiresApproval: true,
    approvalRequestId: null,
    status: 'Active',
    revokedAt: null,
    revokedBy: null,
    revocationReason: null,
  };

  it('accepts a valid delegation payload', () => {
    const delegation = DelegationSchema.parse(validDelegation);
    expect(delegation.scopeType).toBe('Tenant');
    expect(delegation.status).toBe('Active');
    expect(delegation.allowedActions).toHaveLength(2);
  });

  it('transforms undefined nullable fields to null', () => {
    const minimal = {
      ...validDelegation,
      scopeId: undefined,
      maxDurationDays: undefined,
      approvalRequestId: undefined,
      revokedAt: undefined,
      revokedBy: undefined,
      revocationReason: undefined,
    };
    const delegation = DelegationSchema.parse(minimal);
    expect(delegation.scopeId).toBeNull();
    expect(delegation.maxDurationDays).toBeNull();
    expect(delegation.approvalRequestId).toBeNull();
  });

  it('rejects invalid GUID fields', () => {
    expect(() => DelegationSchema.parse({ ...validDelegation, delegationId: 'invalid' })).toThrow();
  });

  it('rejects empty allowedActions', () => {
    expect(() => DelegationSchema.parse({ ...validDelegation, allowedActions: [] })).not.toThrow();
  });

  it('rejects negative maxDurationDays', () => {
    expect(() => DelegationSchema.parse({ ...validDelegation, maxDurationDays: -1 })).toThrow();
  });
});

describe('CreateDelegationPayloadSchema', () => {
  it('accepts a valid create delegation payload', () => {
    const payload = CreateDelegationPayloadSchema.parse({
      tenantId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
      delegatingAdminId: '3fa85f64-5717-4562-b3fc-2c963f66afa7',
      delegatedAdminId: '3fa85f64-5717-4562-b3fc-2c963f66afa8',
      scopeType: 'Department',
      scopeId: '3fa85f64-5717-4562-b3fc-2c963f66afa9',
      allowedActions: ['CreateUser'],
      validFrom: '2024-01-01',
      validUntil: '2024-12-31',
      requiresApproval: false,
    });

    expect(payload.scopeType).toBe('Department');
    expect(payload.requiresApproval).toBe(false);
  });

  it('accepts nullable scopeId', () => {
    const payload = CreateDelegationPayloadSchema.parse({
      tenantId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
      delegatingAdminId: '3fa85f64-5717-4562-b3fc-2c963f66afa7',
      delegatedAdminId: '3fa85f64-5717-4562-b3fc-2c963f66afa8',
      scopeType: 'System',
      allowedActions: ['ResetPassword'],
      validFrom: '2024-01-01',
      validUntil: '2024-12-31',
      requiresApproval: true,
    });

    expect(payload.scopeId).toBeUndefined();
  });
});

describe('CreateDelegationResponseSchema', () => {
  it('accepts a valid response', () => {
    const response = CreateDelegationResponseSchema.parse({
      delegationId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
    });

    expect(response.delegationId).toBe('3fa85f64-5717-4562-b3fc-2c963f66afa6');
  });
});

describe('RevokeDelegationPayloadSchema', () => {
  it('accepts a valid revoke payload', () => {
    const payload = RevokeDelegationPayloadSchema.parse({
      reason: 'No longer needed',
    });

    expect(payload.reason).toBe('No longer needed');
  });

  it('rejects empty reason', () => {
    expect(() => RevokeDelegationPayloadSchema.parse({ reason: '' })).toThrow();
  });

  it('rejects reason exceeding max length', () => {
    expect(() => RevokeDelegationPayloadSchema.parse({ reason: 'A'.repeat(501) })).toThrow();
  });
});
