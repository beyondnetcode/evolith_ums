import { describe, expect, it } from 'vitest';
import {
  AppConfigurationSchema,
  AppConfigurationPageSchema,
  CreateAppConfigurationResponseSchema,
} from './app-configuration.schema';

const validConfig = {
  appConfigurationId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
  tenantId: null,
  systemSuiteId: null,
  moduleId: null,
  code: 'FEATURE.LIMIT',
  value: '42',
  description: 'Límite máximo permitido',
  scope: 'Global',
  isInheritable: true,
  isEncrypted: false,
  version: '1.0.0',
  status: 'Published',
  rowVersion: null,
};

describe('AppConfigurationSchema', () => {
  it('acepta una configuración válida', () => {
    const parsed = AppConfigurationSchema.parse(validConfig);
    expect(parsed.code).toBe('FEATURE.LIMIT');
    expect(parsed.scope).toBe('Global');
    expect(parsed.status).toBe('Published');
  });

  it('acepta rowVersion omitido (opcional)', () => {
    const withoutRowVersion: Record<string, unknown> = { ...validConfig };
    delete withoutRowVersion.rowVersion;
    const parsed = AppConfigurationSchema.parse(withoutRowVersion);
    expect(parsed.rowVersion).toBeUndefined();
  });

  it('rechaza appConfigurationId no-UUID', () => {
    expect(() =>
      AppConfigurationSchema.parse({ ...validConfig, appConfigurationId: 'no-uuid' })
    ).toThrow();
  });

  it('rechaza code vacío', () => {
    expect(() => AppConfigurationSchema.parse({ ...validConfig, code: '' })).toThrow();
  });

  it('rechaza code que excede 100 caracteres', () => {
    expect(() => AppConfigurationSchema.parse({ ...validConfig, code: 'x'.repeat(101) })).toThrow();
  });

  it('rechaza scope fuera del enum', () => {
    expect(() => AppConfigurationSchema.parse({ ...validConfig, scope: 'Regional' })).toThrow();
  });

  it('rechaza status fuera del enum', () => {
    expect(() => AppConfigurationSchema.parse({ ...validConfig, status: 'Deleted' })).toThrow();
  });

  it('rechaza version que no cumple el patrón semver X.Y.Z', () => {
    expect(() => AppConfigurationSchema.parse({ ...validConfig, version: '1.0' })).toThrow();
  });

  it('rechaza description que excede 500 caracteres', () => {
    expect(() =>
      AppConfigurationSchema.parse({ ...validConfig, description: 'x'.repeat(501) })
    ).toThrow();
  });
});

describe('AppConfigurationPageSchema', () => {
  const validPage = {
    items: [validConfig],
    page: 1,
    pageSize: 20,
    totalItems: 1,
    totalPages: 1,
  };

  it('acepta una página válida', () => {
    const parsed = AppConfigurationPageSchema.parse(validPage);
    expect(parsed.items).toHaveLength(1);
    expect(parsed.page).toBe(1);
  });

  it('acepta totalItems y totalPages en cero', () => {
    const parsed = AppConfigurationPageSchema.parse({
      ...validPage,
      items: [],
      totalItems: 0,
      totalPages: 0,
    });
    expect(parsed.totalItems).toBe(0);
    expect(parsed.totalPages).toBe(0);
  });

  it('rechaza page no positivo', () => {
    expect(() => AppConfigurationPageSchema.parse({ ...validPage, page: 0 })).toThrow();
  });

  it('rechaza totalItems negativo', () => {
    expect(() => AppConfigurationPageSchema.parse({ ...validPage, totalItems: -1 })).toThrow();
  });
});

describe('CreateAppConfigurationResponseSchema', () => {
  it('acepta una respuesta de creación válida', () => {
    const parsed = CreateAppConfigurationResponseSchema.parse({
      appConfigurationId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
      code: 'FEATURE.LIMIT',
    });
    expect(parsed.code).toBe('FEATURE.LIMIT');
  });

  it('rechaza appConfigurationId no-UUID', () => {
    expect(() =>
      CreateAppConfigurationResponseSchema.parse({
        appConfigurationId: 'x',
        code: 'FEATURE.LIMIT',
      })
    ).toThrow();
  });
});
