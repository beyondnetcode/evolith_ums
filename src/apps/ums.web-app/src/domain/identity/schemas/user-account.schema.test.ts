import { describe, expect, it } from 'vitest';
import { UserAccountSchema, CreateUserAccountPayloadSchema } from './user-account.schema';

describe('UserAccountSchema', () => {
  it('accepts a valid user account payload', () => {
    const userAccount = UserAccountSchema.parse({
      userAccountId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
      tenantId: 'c9b736b4-6a84-48f8-b34d-176bc5a6d542',
      branchId: null,
      email: 'admin@ransa.pe',
      category: 'Internal',
      status: 'Active',
      identityReference: 'EMP-123',
      identityReferenceType: 'HrId',
    });

    expect(userAccount.email).toBe('admin@ransa.pe');
    expect(userAccount.category).toBe('Internal');
    expect(userAccount.status).toBe('Active');
  });

  it('rejects invalid email or UUIDs', () => {
    expect(() =>
      UserAccountSchema.parse({
        userAccountId: 'invalid-uuid',
        tenantId: 'c9b736b4-6a84-48f8-b34d-176bc5a6d542',
        email: 'invalid-email',
        category: 'Internal',
        status: 'Active',
      })
    ).toThrow();
  });
});

describe('CreateUserAccountPayloadSchema', () => {
  it('accepts a valid payload to create user', () => {
    const payload = CreateUserAccountPayloadSchema.parse({
      tenantId: 'c9b736b4-6a84-48f8-b34d-176bc5a6d542',
      email: 'user@ransa.pe',
      category: 'External',
      identityReference: 'REF-456',
      identityReferenceType: 'VendorCode',
    });

    expect(payload.email).toBe('user@ransa.pe');
    expect(payload.category).toBe('External');
  });
});
