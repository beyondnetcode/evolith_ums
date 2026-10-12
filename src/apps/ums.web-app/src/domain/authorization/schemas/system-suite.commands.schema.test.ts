import { describe, expect, it } from 'vitest';
import {
  CreateSystemSuiteCommandSchema,
  UpdateSystemSuiteCommandSchema,
  AddModuleCommandSchema,
  UpdateModuleCommandSchema,
  RegisterActionCommandSchema,
  AddDomainResourceCommandSchema,
  UpdateDomainResourceCommandSchema,
} from './system-suite.commands.schema';

describe('CreateSystemSuiteCommandSchema', () => {
  const validCommand = {
    tenantId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
    code: 'SUITE_001',
    name: 'Test Suite',
    description: 'A test system suite',
  };

  it('accepts a valid command', () => {
    const result = CreateSystemSuiteCommandSchema.parse(validCommand);
    expect(result.code).toBe('SUITE_001');
  });

  it('rejects invalid tenantId', () => {
    expect(() =>
      CreateSystemSuiteCommandSchema.parse({ ...validCommand, tenantId: 'not-a-uuid' })
    ).toThrow();
  });

  it('rejects empty code', () => {
    expect(() => CreateSystemSuiteCommandSchema.parse({ ...validCommand, code: '' })).toThrow();
  });

  it('rejects code with special characters', () => {
    expect(() =>
      CreateSystemSuiteCommandSchema.parse({ ...validCommand, code: 'SUITE-001!' })
    ).toThrow();
  });

  it('rejects code exceeding max length', () => {
    expect(() =>
      CreateSystemSuiteCommandSchema.parse({ ...validCommand, code: 'A'.repeat(51) })
    ).toThrow();
  });

  it('rejects empty name', () => {
    expect(() => CreateSystemSuiteCommandSchema.parse({ ...validCommand, name: '' })).toThrow();
  });

  it('rejects name exceeding max length', () => {
    expect(() =>
      CreateSystemSuiteCommandSchema.parse({ ...validCommand, name: 'A'.repeat(151) })
    ).toThrow();
  });

  it('rejects empty description', () => {
    expect(() =>
      CreateSystemSuiteCommandSchema.parse({ ...validCommand, description: '' })
    ).toThrow();
  });

  it('rejects description exceeding max length', () => {
    expect(() =>
      CreateSystemSuiteCommandSchema.parse({ ...validCommand, description: 'A'.repeat(501) })
    ).toThrow();
  });
});

describe('UpdateSystemSuiteCommandSchema', () => {
  const validCommand = {
    name: 'Updated Suite',
    description: 'Updated description',
  };

  it('accepts a valid update command', () => {
    const result = UpdateSystemSuiteCommandSchema.parse(validCommand);
    expect(result.name).toBe('Updated Suite');
  });

  it('defaults description to empty string when not provided', () => {
    const result = UpdateSystemSuiteCommandSchema.parse({ name: 'Test' });
    expect(result.description).toBe('');
  });

  it('rejects empty name', () => {
    expect(() => UpdateSystemSuiteCommandSchema.parse({ name: '' })).toThrow();
  });
});

describe('AddModuleCommandSchema', () => {
  const validCommand = {
    code: 'MODULE_001',
    name: 'Test Module',
    description: 'A test module',
    sortOrder: 1,
  };

  it('accepts a valid command', () => {
    const result = AddModuleCommandSchema.parse(validCommand);
    expect(result.code).toBe('MODULE_001');
  });

  it('rejects negative sortOrder', () => {
    expect(() => AddModuleCommandSchema.parse({ ...validCommand, sortOrder: -1 })).toThrow();
  });

  it('rejects zero sortOrder', () => {
    expect(() => AddModuleCommandSchema.parse({ ...validCommand, sortOrder: 0 })).toThrow();
  });

  it('rejects empty code', () => {
    expect(() => AddModuleCommandSchema.parse({ ...validCommand, code: '' })).toThrow();
  });
});

describe('UpdateModuleCommandSchema', () => {
  it('is the same as AddModuleCommandSchema', () => {
    expect(UpdateModuleCommandSchema).toBe(AddModuleCommandSchema);
  });
});

describe('RegisterActionCommandSchema', () => {
  const validCommand = {
    code: 'ACTION_001',
    name: 'Test Action',
  };

  it('accepts a valid command', () => {
    const result = RegisterActionCommandSchema.parse(validCommand);
    expect(result.code).toBe('ACTION_001');
  });

  it('rejects empty code', () => {
    expect(() => RegisterActionCommandSchema.parse({ ...validCommand, code: '' })).toThrow();
  });

  it('rejects empty name', () => {
    expect(() => RegisterActionCommandSchema.parse({ ...validCommand, name: '' })).toThrow();
  });
});

describe('AddDomainResourceCommandSchema', () => {
  const validCommand = {
    moduleId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
    type: 'Aggregate' as const,
    code: 'RESOURCE_001',
    name: 'Test Resource',
    description: 'A test domain resource',
  };

  it('accepts a valid command', () => {
    const result = AddDomainResourceCommandSchema.parse(validCommand);
    expect(result.code).toBe('RESOURCE_001');
  });

  it('accepts nullable moduleId', () => {
    const result = AddDomainResourceCommandSchema.parse({ ...validCommand, moduleId: null });
    expect(result.moduleId).toBeNull();
  });

  it('accepts undefined moduleId', () => {
    const result = AddDomainResourceCommandSchema.parse({ ...validCommand, moduleId: undefined });
    expect(result.moduleId).toBeUndefined();
  });

  it('accepts Entity type', () => {
    const result = AddDomainResourceCommandSchema.parse({ ...validCommand, type: 'Entity' });
    expect(result.type).toBe('Entity');
  });

  it('rejects invalid type', () => {
    expect(() =>
      AddDomainResourceCommandSchema.parse({ ...validCommand, type: 'Invalid' })
    ).toThrow();
  });

  it('rejects empty code', () => {
    expect(() => AddDomainResourceCommandSchema.parse({ ...validCommand, code: '' })).toThrow();
  });
});

describe('UpdateDomainResourceCommandSchema', () => {
  const validCommand = {
    name: 'Updated Resource',
    description: 'Updated description',
  };

  it('accepts a valid command', () => {
    const result = UpdateDomainResourceCommandSchema.parse(validCommand);
    expect(result.name).toBe('Updated Resource');
  });

  it('rejects empty name', () => {
    expect(() => UpdateDomainResourceCommandSchema.parse({ name: '' })).toThrow();
  });

  it('rejects empty description', () => {
    expect(() =>
      UpdateDomainResourceCommandSchema.parse({ name: 'Test', description: '' })
    ).toThrow();
  });
});
