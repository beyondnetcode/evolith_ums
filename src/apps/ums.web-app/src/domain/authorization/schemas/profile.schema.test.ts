import { describe, expect, it } from 'vitest';
import {
  ProfileSchema,
  ProfilePermissionSchema,
  ProfilePageSchema,
  CreateProfileResponseSchema,
} from './profile.schema';

describe('ProfilePermissionSchema', () => {
  it('accepts a valid permission payload', () => {
    const permission = ProfilePermissionSchema.parse({
      permissionId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
      profileId: '3fa85f64-5717-4562-b3fc-2c963f66afa7',
      templateId: '3fa85f64-5717-4562-b3fc-2c963f66afa8',
      targetType: 'Tenant',
      targetId: '3fa85f64-5717-4562-b3fc-2c963f66afa9',
      targetName: 'Main Tenant',
      actionId: '3fa85f64-5717-4562-b3fc-2c963f66afaa',
      actionName: 'Read',
      isAllowed: true,
      isDenied: false,
      isActive: true,
      isOverride: false,
    });

    expect(permission.actionName).toBe('Read');
    expect(permission.isAllowed).toBe(true);
  });

  it('rejects invalid GUID', () => {
    expect(() =>
      ProfilePermissionSchema.parse({
        permissionId: 'invalid',
        profileId: '3fa85f64-5717-4562-b3fc-2c963f66afa7',
        templateId: '3fa85f64-5717-4562-b3fc-2c963f66afa8',
        targetType: 'Tenant',
        targetId: '3fa85f64-5717-4562-b3fc-2c963f66afa9',
        targetName: 'Main',
        actionId: '3fa85f64-5717-4562-b3fc-2c963f66afaa',
        actionName: 'Read',
        isAllowed: true,
        isDenied: false,
        isActive: true,
        isOverride: false,
      })
    ).toThrow();
  });
});

describe('ProfileSchema', () => {
  it('accepts a valid profile payload', () => {
    const profile = ProfileSchema.parse({
      profileId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
      tenantId: '3fa85f64-5717-4562-b3fc-2c963f66afa7',
      tenantCode: 'TENANT_1',
      tenantName: 'Tenant 1',
      userId: '3fa85f64-5717-4562-b3fc-2c963f66afa8',
      userEmail: 'user@example.com',
      roleId: '3fa85f64-5717-4562-b3fc-2c963f66afa9',
      roleCode: 'ADMIN',
      roleName: 'Administrator',
      branchId: '3fa85f64-5717-4562-b3fc-2c963f66afaa',
      branchName: 'Main Branch',
      systemSuiteId: '3fa85f64-5717-4562-b3fc-2c963f66afab',
      systemSuiteCode: 'CORE',
      systemSuiteName: 'Core Suite',
      scope: 'Tenant',
      isActive: true,
      permissionCount: 0,
      permissions: [],
    });

    expect(profile.scope).toBe('Tenant');
    expect(profile.isActive).toBe(true);
    expect(profile.permissions).toHaveLength(0);
  });

  it('accepts profile with permissions', () => {
    const profile = ProfileSchema.parse({
      profileId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
      tenantId: '3fa85f64-5717-4562-b3fc-2c963f66afa7',
      tenantCode: 'TENANT_1',
      tenantName: 'Tenant 1',
      userId: '3fa85f64-5717-4562-b3fc-2c963f66afa8',
      userEmail: 'user@example.com',
      roleId: '3fa85f64-5717-4562-b3fc-2c963f66afa9',
      roleCode: 'ADMIN',
      roleName: 'Administrator',
      systemSuiteId: '3fa85f64-5717-4562-b3fc-2c963f66afae',
      systemSuiteCode: 'CORE',
      systemSuiteName: 'Core Suite',
      scope: 'Global',
      isActive: true,
      permissionCount: 1,
      permissions: [
        {
          permissionId: '3fa85f64-5717-4562-b3fc-2c963f66afaa',
          profileId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
          templateId: '3fa85f64-5717-4562-b3fc-2c963f66afab',
          targetType: 'Tenant',
          targetId: '3fa85f64-5717-4562-b3fc-2c963f66afac',
          targetName: 'Target',
          actionId: '3fa85f64-5717-4562-b3fc-2c963f66afad',
          actionName: 'Write',
          isAllowed: true,
          isDenied: false,
          isActive: true,
          isOverride: false,
        },
      ],
    });

    expect(profile.permissions).toHaveLength(1);
    expect(profile.permissions[0].actionName).toBe('Write');
  });

  it('accepts nullable branchId', () => {
    const profile = ProfileSchema.parse({
      profileId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
      tenantId: '3fa85f64-5717-4562-b3fc-2c963f66afa7',
      tenantCode: 'TENANT_1',
      tenantName: 'Tenant 1',
      userId: '3fa85f64-5717-4562-b3fc-2c963f66afa8',
      userEmail: 'user@example.com',
      roleId: '3fa85f64-5717-4562-b3fc-2c963f66afa9',
      roleCode: 'ADMIN',
      roleName: 'Administrator',
      systemSuiteId: '3fa85f64-5717-4562-b3fc-2c963f66afae',
      systemSuiteCode: 'CORE',
      systemSuiteName: 'Core Suite',
      scope: 'Tenant',
      isActive: true,
      permissionCount: 0,
      permissions: [],
    });

    expect(profile.branchId).toBeUndefined();
  });
});

describe('ProfilePageSchema', () => {
  it('accepts a valid page payload', () => {
    const page = ProfilePageSchema.parse({
      items: [],
      page: 1,
      pageSize: 10,
      totalItems: 0,
      totalPages: 0,
    });

    expect(page.page).toBe(1);
    expect(page.pageSize).toBe(10);
  });

  it('rejects page less than 1', () => {
    expect(() =>
      ProfilePageSchema.parse({
        items: [],
        page: 0,
        pageSize: 10,
        totalItems: 0,
        totalPages: 0,
      })
    ).toThrow();
  });
});

describe('CreateProfileResponseSchema', () => {
  it('accepts a valid response', () => {
    const response = CreateProfileResponseSchema.parse({
      profileId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
    });

    expect(response.profileId).toBe('3fa85f64-5717-4562-b3fc-2c963f66afa6');
  });
});
