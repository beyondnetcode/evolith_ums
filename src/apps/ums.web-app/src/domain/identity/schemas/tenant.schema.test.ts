import { describe, expect, it } from 'vitest';
import { TenantSchema } from './tenant.schema';

describe('TenantSchema', () => {
  it('accepts a valid tenant payload', () => {
    const tenant = TenantSchema.parse({
      tenantId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
      code: 'RANSA',
      name: 'Ransa',
      type: 'INTERNAL',
      status: 'Active',
      parentTenantId: null,
      companyReference: null,
    });

    expect(tenant.code).toBe('RANSA');
  });

  it('rejects invalid runtime payloads', () => {
    expect(() =>
      TenantSchema.parse({
        tenantId: 'not-a-guid',
        code: '',
        name: 'Ransa',
        type: 'INTERNAL',
        status: 'Unknown',
        parentTenantId: null,
      })
    ).toThrow();
  });
});
