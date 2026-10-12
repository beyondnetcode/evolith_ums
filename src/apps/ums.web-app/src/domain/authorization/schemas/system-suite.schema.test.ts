import { describe, expect, it } from 'vitest';
import {
  SystemStatusSchema,
  SystemSuiteSchema,
  SystemSuitePageSchema,
  CreateSystemSuitePayloadSchema,
  CreateSystemSuiteResponseSchema,
  SystemSuiteActionSchema,
  SystemSuiteNodeSchema,
  SystemSuiteModuleSchema,
  SystemSuiteDomainResourceSchema,
} from './system-suite.schema';

describe('SystemStatusSchema', () => {
  it('accepts valid status values', () => {
    expect(SystemStatusSchema.parse('Active')).toBe('Active');
    expect(SystemStatusSchema.parse('Maintenance')).toBe('Maintenance');
    expect(SystemStatusSchema.parse('Deprecated')).toBe('Deprecated');
  });

  it('rejects invalid status', () => {
    expect(() => SystemStatusSchema.parse('Unknown')).toThrow();
  });
});

describe('SystemSuiteActionSchema', () => {
  it('accepts a valid action', () => {
    const action = SystemSuiteActionSchema.parse({
      id: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
      code: 'CREATE',
      name: 'Create',
    });
    expect(action.code).toBe('CREATE');
  });
});

describe('SystemSuiteNodeSchema', () => {
  it('accepts a valid leaf node with N:M action codes', () => {
    const node = SystemSuiteNodeSchema.parse({
      id: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
      parentNodeId: null,
      kind: 'Option',
      code: 'OPT1',
      label: 'Option 1',
      description: 'Test option',
      status: 'Active',
      sortOrder: 1,
      actionCodes: ['CREATE', 'VIEW'],
      metadata: null,
      children: [],
    });
    expect(node.label).toBe('Option 1');
    expect(node.actionCodes).toEqual(['CREATE', 'VIEW']);
  });

  it('accepts a recursive branch node with children and SDLC metadata', () => {
    const node = SystemSuiteNodeSchema.parse({
      id: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
      parentNodeId: null,
      kind: 'Menu',
      code: 'MENU1',
      label: 'Menu 1',
      description: 'Branch',
      status: 'Active',
      sortOrder: 1,
      actionCodes: [],
      metadata: { responsable: 'Equipo A', criticidad: 'Alta' },
      children: [
        {
          id: '3fa85f64-5717-4562-b3fc-2c963f66afa7',
          parentNodeId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
          kind: 'Option',
          code: 'CHILD',
          label: 'Child',
          description: '',
          status: 'Active',
          sortOrder: 1,
          actionCodes: [],
          metadata: null,
          children: [],
        },
      ],
    });
    expect(node.children).toHaveLength(1);
    expect(node.metadata?.responsable).toBe('Equipo A');
  });
});

describe('SystemSuiteModuleSchema', () => {
  it('accepts a valid module with a node tree', () => {
    const module = SystemSuiteModuleSchema.parse({
      id: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
      code: 'MOD1',
      name: 'Module 1',
      description: 'Test module',
      status: 'Active',
      sortOrder: 1,
      nodes: [],
    });
    expect(module.name).toBe('Module 1');
    expect(module.nodes).toEqual([]);
  });
});

describe('SystemSuiteDomainResourceSchema', () => {
  it('accepts a valid domain resource', () => {
    const resource = SystemSuiteDomainResourceSchema.parse({
      id: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
      type: 'Aggregate',
      code: 'USER',
      name: 'User',
      description: 'User aggregate',
    });
    expect(resource.type).toBe('Aggregate');
    expect(resource.crudOperations).toEqual([]);
  });

  it('defaults crudOperations to empty array', () => {
    const resource = SystemSuiteDomainResourceSchema.parse({
      id: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
      type: 'Entity',
      code: 'ROLE',
      name: 'Role',
      description: 'Role entity',
    });
    expect(resource.crudOperations).toEqual([]);
  });

  it('accepts Entity type', () => {
    const resource = SystemSuiteDomainResourceSchema.parse({
      id: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
      type: 'Entity',
      code: 'PERM',
      name: 'Permission',
      description: 'Permission entity',
    });
    expect(resource.type).toBe('Entity');
  });
});

describe('SystemSuiteSchema', () => {
  const validSuite = {
    systemSuiteId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
    tenantId: '3fa85f64-5717-4562-b3fc-2c963f66afa7',
    code: 'UMS',
    name: 'User Management System',
    description: 'Main system suite',
    status: 'Active',
  };

  it('accepts a valid system suite', () => {
    const suite = SystemSuiteSchema.parse(validSuite);
    expect(suite.code).toBe('UMS');
    expect(suite.status).toBe('Active');
  });

  it('defaults modules to empty array', () => {
    const suite = SystemSuiteSchema.parse(validSuite);
    expect(suite.modules).toEqual([]);
  });

  it('defaults actions to empty array', () => {
    const suite = SystemSuiteSchema.parse(validSuite);
    expect(suite.actions).toEqual([]);
  });

  it('defaults domainResources to empty array', () => {
    const suite = SystemSuiteSchema.parse(validSuite);
    expect(suite.domainResources).toEqual([]);
  });

  it('rejects empty code', () => {
    expect(() => SystemSuiteSchema.parse({ ...validSuite, code: '' })).toThrow();
  });

  it('rejects empty name', () => {
    expect(() => SystemSuiteSchema.parse({ ...validSuite, name: '' })).toThrow();
  });
});

describe('SystemSuitePageSchema', () => {
  it('accepts a valid page', () => {
    const page = SystemSuitePageSchema.parse({
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
      SystemSuitePageSchema.parse({
        items: [],
        page: 0,
        pageSize: 10,
        totalItems: 0,
        totalPages: 0,
      })
    ).toThrow();
  });
});

describe('CreateSystemSuitePayloadSchema', () => {
  it('accepts a valid payload with description', () => {
    const payload = CreateSystemSuitePayloadSchema.parse({
      tenantId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
      code: 'NEW',
      name: 'New System',
      description: 'A new system suite',
    });
    expect(payload.code).toBe('NEW');
    expect(payload.description).toBe('A new system suite');
  });

  // Contrato: el backend exige `Description.NotEmpty()` (G-107). La descripción es
  // requerida en el payload del frontend para no llegar a un 400.
  it('rejects a missing description', () => {
    const result = CreateSystemSuitePayloadSchema.safeParse({
      tenantId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
      code: 'NEW',
      name: 'New System',
    });
    expect(result.success).toBe(false);
  });

  it('rejects an empty description with the required message', () => {
    const result = CreateSystemSuitePayloadSchema.safeParse({
      tenantId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
      code: 'NEW',
      name: 'New System',
      description: '',
    });
    expect(result.success).toBe(false);
    if (!result.success) {
      const descIssue = result.error.issues.find(issue => issue.path[0] === 'description');
      expect(descIssue?.message).toBe('Descripción requerida');
    }
  });

  it('rejects code exceeding max length', () => {
    expect(() =>
      CreateSystemSuitePayloadSchema.parse({
        tenantId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
        code: 'A'.repeat(51),
        name: 'New System',
        description: 'A new system suite',
      })
    ).toThrow();
  });
});

describe('CreateSystemSuiteResponseSchema', () => {
  it('accepts a valid response', () => {
    const response = CreateSystemSuiteResponseSchema.parse({
      systemSuiteId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
    });
    expect(response.systemSuiteId).toBe('3fa85f64-5717-4562-b3fc-2c963f66afa6');
  });
});
