import { describe, expect, it } from 'vitest';
import { RoleSchema, RoleListSchema, CreateRoleResponseSchema } from './role.schema';

describe('RoleSchema', () => {
  const validRole = {
    roleId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
    tenantId: '3fa85f64-5717-4562-b3fc-2c963f66afa7',
    systemSuiteId: '3fa85f64-5717-4562-b3fc-2c963f66afa8',
    parentRoleId: null,
    code: 'ADMIN',
    value: 'Administrator',
    description: 'Full admin access',
    hierarchyLevel: 0,
    promotionOrder: 1,
    isActive: true,
  };

  it('accepts a valid role payload', () => {
    const role = RoleSchema.parse(validRole);
    expect(role.code).toBe('ADMIN');
    expect(role.value).toBe('Administrator');
    expect(role.isActive).toBe(true);
  });

  it('accepts nullable parentRoleId', () => {
    const role = RoleSchema.parse(validRole);
    expect(role.parentRoleId).toBeNull();
  });

  it('accepts role with parent', () => {
    const role = RoleSchema.parse({
      ...validRole,
      parentRoleId: '3fa85f64-5717-4562-b3fc-2c963f66afa9',
    });
    expect(role.parentRoleId).toBe('3fa85f64-5717-4562-b3fc-2c963f66afa9');
  });

  it('rejects empty code', () => {
    expect(() => RoleSchema.parse({ ...validRole, code: '' })).toThrow();
  });

  it('rejects empty value', () => {
    expect(() => RoleSchema.parse({ ...validRole, value: '' })).toThrow();
  });

  it('rejects negative hierarchyLevel', () => {
    expect(() => RoleSchema.parse({ ...validRole, hierarchyLevel: -1 })).toThrow();
  });

  it('rejects invalid GUID', () => {
    expect(() => RoleSchema.parse({ ...validRole, roleId: 'not-a-guid' })).toThrow();
  });
});

describe('RoleListSchema', () => {
  it('accepts an array of valid roles', () => {
    const roles = RoleListSchema.parse([
      {
        roleId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
        tenantId: '3fa85f64-5717-4562-b3fc-2c963f66afa7',
        systemSuiteId: '3fa85f64-5717-4562-b3fc-2c963f66afa8',
        parentRoleId: null,
        code: 'ADMIN',
        value: 'Admin',
        description: 'Admin role',
        hierarchyLevel: 0,
        promotionOrder: 1,
        isActive: true,
      },
      {
        roleId: '3fa85f64-5717-4562-b3fc-2c963f66afa9',
        tenantId: '3fa85f64-5717-4562-b3fc-2c963f66afa7',
        systemSuiteId: '3fa85f64-5717-4562-b3fc-2c963f66afa8',
        parentRoleId: null,
        code: 'USER',
        value: 'User',
        description: 'User role',
        hierarchyLevel: 1,
        promotionOrder: 2,
        isActive: true,
      },
    ]);

    expect(roles).toHaveLength(2);
    expect(roles[0].code).toBe('ADMIN');
    expect(roles[1].code).toBe('USER');
  });
});

describe('CreateRoleResponseSchema', () => {
  it('accepts a valid response', () => {
    const response = CreateRoleResponseSchema.parse({
      roleId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
    });

    expect(response.roleId).toBe('3fa85f64-5717-4562-b3fc-2c963f66afa6');
  });

  it('rejects non-UUID roleId', () => {
    expect(() => CreateRoleResponseSchema.parse({ roleId: 'invalid' })).toThrow();
  });
});
