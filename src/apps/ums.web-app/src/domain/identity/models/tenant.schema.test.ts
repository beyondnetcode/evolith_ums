import { describe, expect, it } from 'vitest';
import {
  TenantSchema,
  CreateTenantResponseSchema,
  BranchSchema,
  AddBranchResponseSchema,
  validateTenants,
  validateTenant,
  validateCreateTenantResponse,
  validateBranches,
  validateAddBranchResponse,
} from './tenant.schema';

describe('TenantSchema', () => {
  const validTenant = {
    tenantId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
    code: 'TEST',
    name: 'Test Tenant',
    type: 'INTERNAL',
    status: 'Active',
  };

  it('accepts a valid tenant', () => {
    const tenant = TenantSchema.parse(validTenant);
    expect(tenant.code).toBe('TEST');
  });

  it('accepts optional companyReference', () => {
    const tenant = TenantSchema.parse({ ...validTenant, companyReference: 'REF-123' });
    expect(tenant.companyReference).toBe('REF-123');
  });

  it('accepts nullable companyReference', () => {
    const tenant = TenantSchema.parse({ ...validTenant, companyReference: null });
    expect(tenant.companyReference).toBeNull();
  });

  it('accepts optional createdAt', () => {
    const tenant = TenantSchema.parse({ ...validTenant, createdAt: '2024-01-01T00:00:00Z' });
    expect(tenant.createdAt).toBe('2024-01-01T00:00:00Z');
  });

  it('rejects invalid tenantId', () => {
    expect(() => TenantSchema.parse({ ...validTenant, tenantId: 'not-a-uuid' })).toThrow();
  });

  it('rejects empty code', () => {
    expect(() => TenantSchema.parse({ ...validTenant, code: '' })).toThrow();
  });

  it('rejects invalid type', () => {
    expect(() => TenantSchema.parse({ ...validTenant, type: 'INVALID' })).toThrow();
  });

  it('rejects invalid status', () => {
    expect(() => TenantSchema.parse({ ...validTenant, status: 'Unknown' })).toThrow();
  });
});

describe('CreateTenantResponseSchema', () => {
  it('accepts a valid response', () => {
    const response = CreateTenantResponseSchema.parse({
      tenantId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
      code: 'NEW',
      name: 'New Tenant',
    });
    expect(response.tenantId).toBe('3fa85f64-5717-4562-b3fc-2c963f66afa6');
  });

  it('rejects invalid tenantId', () => {
    expect(() =>
      CreateTenantResponseSchema.parse({ tenantId: 'invalid', code: 'X', name: 'Y' })
    ).toThrow();
  });
});

describe('BranchSchema', () => {
  const validBranch = {
    branchId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
    code: 'BR001',
    name: 'Main Branch',
    isActive: true,
  };

  it('accepts a valid branch', () => {
    const branch = BranchSchema.parse(validBranch);
    expect(branch.code).toBe('BR001');
  });

  it('accepts optional geofencingMetadata', () => {
    const branch = BranchSchema.parse({ ...validBranch, geofencingMetadata: '{"lat": 123}' });
    expect(branch.geofencingMetadata).toBe('{"lat": 123}');
  });

  it('rejects invalid branchId', () => {
    expect(() => BranchSchema.parse({ ...validBranch, branchId: 'invalid' })).toThrow();
  });
});

describe('AddBranchResponseSchema', () => {
  it('accepts a valid response', () => {
    const response = AddBranchResponseSchema.parse({
      branchId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
      code: 'BR001',
    });
    expect(response.branchId).toBe('3fa85f64-5717-4562-b3fc-2c963f66afa6');
  });
});

describe('validateTenants', () => {
  it('validates an array of tenants', () => {
    const tenants = validateTenants([
      {
        tenantId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
        code: 'T1',
        name: 'Tenant 1',
        type: 'INTERNAL',
        status: 'Active',
      },
    ]);
    expect(tenants).toHaveLength(1);
  });

  it('throws on invalid data', () => {
    expect(() => validateTenants('invalid')).toThrow();
  });
});

describe('validateTenant', () => {
  it('validates a single tenant', () => {
    const tenant = validateTenant({
      tenantId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
      code: 'T1',
      name: 'Tenant 1',
      type: 'INTERNAL',
      status: 'Active',
    });
    expect(tenant.code).toBe('T1');
  });

  it('throws on invalid data', () => {
    expect(() => validateTenant('invalid')).toThrow();
  });
});

describe('validateCreateTenantResponse', () => {
  it('validates a create response', () => {
    const response = validateCreateTenantResponse({
      tenantId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
      code: 'NEW',
      name: 'New Tenant',
    });
    expect(response.code).toBe('NEW');
  });

  it('throws on invalid data', () => {
    expect(() => validateCreateTenantResponse('invalid')).toThrow();
  });
});

describe('validateBranches', () => {
  it('validates an array of branches', () => {
    const branches = validateBranches([
      {
        branchId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
        code: 'BR001',
        name: 'Branch 1',
        isActive: true,
      },
    ]);
    expect(branches).toHaveLength(1);
  });

  it('throws on invalid data', () => {
    expect(() => validateBranches('invalid')).toThrow();
  });
});

describe('validateAddBranchResponse', () => {
  it('validates an add branch response', () => {
    const response = validateAddBranchResponse({
      branchId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
      code: 'BR001',
    });
    expect(response.code).toBe('BR001');
  });

  it('throws on invalid data', () => {
    expect(() => validateAddBranchResponse('invalid')).toThrow();
  });
});
