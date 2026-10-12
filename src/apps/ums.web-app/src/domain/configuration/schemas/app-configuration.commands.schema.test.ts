import { describe, expect, it } from 'vitest';
import {
  CreateAppConfigurationPayloadSchema,
  UpdateAppConfigurationPayloadSchema,
} from './app-configuration.commands.schema';

describe('CreateAppConfigurationPayloadSchema', () => {
  it('acepta un payload mínimo y aplica valores por defecto', () => {
    const parsed = CreateAppConfigurationPayloadSchema.parse({
      code: 'FEATURE.LIMIT',
      value: '42',
    });
    expect(parsed.description).toBe('');
    expect(parsed.isInheritable).toBe(false);
    expect(parsed.isEncrypted).toBe(false);
  });

  it('acepta identificadores opcionales como UUID', () => {
    const parsed = CreateAppConfigurationPayloadSchema.parse({
      tenantId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
      systemSuiteId: '3fa85f64-5717-4562-b3fc-2c963f66afa7',
      moduleId: '3fa85f64-5717-4562-b3fc-2c963f66afa8',
      code: 'FEATURE.LIMIT',
      value: '42',
      description: 'desc',
      isInheritable: true,
      isEncrypted: true,
    });
    expect(parsed.tenantId).toBe('3fa85f64-5717-4562-b3fc-2c963f66afa6');
    expect(parsed.isInheritable).toBe(true);
  });

  it('acepta tenantId nulo', () => {
    const parsed = CreateAppConfigurationPayloadSchema.parse({
      tenantId: null,
      code: 'FEATURE.LIMIT',
      value: '42',
    });
    expect(parsed.tenantId).toBeNull();
  });

  it('rechaza code vacío', () => {
    expect(() => CreateAppConfigurationPayloadSchema.parse({ code: '', value: '42' })).toThrow();
  });

  it('rechaza tenantId con formato no-UUID', () => {
    expect(() =>
      CreateAppConfigurationPayloadSchema.parse({
        tenantId: 'no-uuid',
        code: 'FEATURE.LIMIT',
        value: '42',
      })
    ).toThrow();
  });

  it('rechaza description que excede 500 caracteres', () => {
    expect(() =>
      CreateAppConfigurationPayloadSchema.parse({
        code: 'FEATURE.LIMIT',
        value: '42',
        description: 'x'.repeat(501),
      })
    ).toThrow();
  });
});

describe('UpdateAppConfigurationPayloadSchema', () => {
  it('acepta un payload de actualización válido', () => {
    const parsed = UpdateAppConfigurationPayloadSchema.parse({
      value: '99',
      description: 'nueva descripción',
    });
    expect(parsed.value).toBe('99');
  });

  it('acepta description omitida', () => {
    const parsed = UpdateAppConfigurationPayloadSchema.parse({ value: '99' });
    expect(parsed.description).toBeUndefined();
  });

  it('rechaza value ausente', () => {
    expect(() => UpdateAppConfigurationPayloadSchema.parse({ description: 'x' })).toThrow();
  });

  it('rechaza description que excede 500 caracteres', () => {
    expect(() =>
      UpdateAppConfigurationPayloadSchema.parse({ value: '1', description: 'x'.repeat(501) })
    ).toThrow();
  });
});
