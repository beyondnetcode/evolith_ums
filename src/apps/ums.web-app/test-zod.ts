import { RoleListSchema } from './src/domain/authorization/schemas/role.schema';

const data = [
  {
    roleId: 'aaaa0001-0000-0000-0000-000000000001',
    tenantId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
    systemSuiteId: '44c0b33f-e626-4e70-84d1-6d820c94f20a',
    parentRoleId: null,
    code: 'ADMIN',
    value: 'System Administrator',
    description: 'Full administrative access',
    hierarchyLevel: 0,
    promotionOrder: 0,
    isActive: true,
  },
];

try {
  const result = RoleListSchema.parse(data);
  // eslint-disable-next-line no-console
  console.log('Success', result);
} catch (e) {
  console.error('Zod Error:', e.errors || e);
}
