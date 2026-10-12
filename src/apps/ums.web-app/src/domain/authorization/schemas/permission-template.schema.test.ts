import { describe, expect, it } from 'vitest';
import {
  TemplateStatusSchema,
  ExclusiveArcTargetSchema,
  PermissionEffectSchema,
  PermissionTemplateItemSchema,
  PermissionTemplateSchema,
  PermissionTemplateDetailSchema,
  PermissionTemplatePageSchema,
  CreatePermissionTemplateResponseSchema,
  itemEffect,
  itemStoredEffect,
  isRetiredItem,
  primaryItem,
} from './permission-template.schema';

describe('Permission template enum schemas', () => {
  it('accepts valid TemplateStatus values', () => {
    expect(TemplateStatusSchema.parse('Draft')).toBe('Draft');
    expect(TemplateStatusSchema.parse('Published')).toBe('Published');
    expect(TemplateStatusSchema.parse('Deprecated')).toBe('Deprecated');
  });

  it('accepts valid ExclusiveArcTarget values', () => {
    const targets = ['SystemSuite', 'Module', 'Submodule', 'Option', 'Aggregate', 'Entity'];
    targets.forEach(target => {
      expect(ExclusiveArcTargetSchema.parse(target)).toBe(target);
    });
  });

  it('accepts valid PermissionEffect values', () => {
    expect(PermissionEffectSchema.parse('Allow')).toBe('Allow');
    expect(PermissionEffectSchema.parse('Deny')).toBe('Deny');
    expect(PermissionEffectSchema.parse('Neutral')).toBe('Neutral');
  });
});

describe('PermissionTemplateItemSchema', () => {
  it('accepts a valid template item', () => {
    const item = PermissionTemplateItemSchema.parse({
      itemId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
      targetType: 'SystemSuite',
      targetId: '3fa85f64-5717-4562-b3fc-2c963f66afa7',
      targetName: 'Main Suite',
      actionId: '3fa85f64-5717-4562-b3fc-2c963f66afa8',
      actionName: 'Read',
      isAllowed: true,
      isDenied: false,
      isActive: true,
    });

    expect(item.targetType).toBe('SystemSuite');
    expect(item.isAllowed).toBe(true);
  });
});

describe('PermissionTemplateSchema', () => {
  it('accepts a valid template', () => {
    const template = PermissionTemplateSchema.parse({
      templateId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
      tenantId: '3fa85f64-5717-4562-b3fc-2c963f66afa7',
      roleId: '3fa85f64-5717-4562-b3fc-2c963f66afa8',
      roleName: 'Admin',
      systemSuiteId: '3fa85f64-5717-4562-b3fc-2c963f66afa9',
      systemSuiteName: 'UMS',
      version: '1.0.0',
      status: 'Published',
    });

    expect(template.roleName).toBe('Admin');
    expect(template.status).toBe('Published');
  });
});

describe('PermissionTemplateDetailSchema', () => {
  it('accepts a template with items', () => {
    const detail = PermissionTemplateDetailSchema.parse({
      templateId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
      tenantId: '3fa85f64-5717-4562-b3fc-2c963f66afa7',
      roleId: '3fa85f64-5717-4562-b3fc-2c963f66afa8',
      roleName: 'Admin',
      systemSuiteId: '3fa85f64-5717-4562-b3fc-2c963f66afa9',
      systemSuiteName: 'UMS',
      version: '1.0.0',
      status: 'Published',
      items: [
        {
          itemId: '3fa85f64-5717-4562-b3fc-2c963f66afaa',
          targetType: 'Module',
          targetId: '3fa85f64-5717-4562-b3fc-2c963f66afab',
          targetName: 'Users',
          actionId: '3fa85f64-5717-4562-b3fc-2c963f66afac',
          actionName: 'Create',
          isAllowed: true,
          isDenied: false,
          isActive: true,
        },
      ],
    });

    expect(detail.items).toHaveLength(1);
  });
});

describe('PermissionTemplatePageSchema', () => {
  it('accepts a valid page', () => {
    const page = PermissionTemplatePageSchema.parse({
      items: [],
      page: 1,
      pageSize: 10,
      totalItems: 0,
      totalPages: 0,
    });
    expect(page.page).toBe(1);
  });
});

describe('CreatePermissionTemplateResponseSchema', () => {
  it('accepts a valid response', () => {
    const response = CreatePermissionTemplateResponseSchema.parse({
      templateId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
    });
    expect(response.templateId).toBe('3fa85f64-5717-4562-b3fc-2c963f66afa6');
  });
});

describe('itemEffect', () => {
  it('returns Allow when isAllowed is true', () => {
    expect(itemEffect({ isAllowed: true, isDenied: false })).toBe('Allow');
  });

  it('returns Deny when isDenied is true', () => {
    expect(itemEffect({ isAllowed: false, isDenied: true })).toBe('Deny');
  });

  it('returns Neutral when both are false', () => {
    expect(itemEffect({ isAllowed: false, isDenied: false })).toBe('Neutral');
  });

  it('prioritizes Allow over Deny', () => {
    expect(itemEffect({ isAllowed: true, isDenied: true })).toBe('Allow');
  });

  // ADR-0164: la fila retirada sobrevive con sus booleanos intactos, pero ya no concede nada.
  it('deriva a Neutral el ítem retirado aunque conserve isAllowed', () => {
    expect(itemEffect({ isAllowed: true, isDenied: false, isActive: false })).toBe('Neutral');
  });

  it('deriva a Neutral el ítem retirado aunque conserve isDenied', () => {
    expect(itemEffect({ isAllowed: false, isDenied: true, isActive: false })).toBe('Neutral');
  });

  it('respeta el efecto del ítem vigente', () => {
    expect(itemEffect({ isAllowed: true, isDenied: false, isActive: true })).toBe('Allow');
  });
});

describe('itemStoredEffect', () => {
  it('ignora el retiro y devuelve el efecto guardado en la fila', () => {
    expect(itemStoredEffect({ isAllowed: true, isDenied: false })).toBe('Allow');
    expect(itemStoredEffect({ isAllowed: false, isDenied: true })).toBe('Deny');
    expect(itemStoredEffect({ isAllowed: false, isDenied: false })).toBe('Neutral');
  });
});

describe('isRetiredItem', () => {
  it('solo considera retirado el isActive false explícito', () => {
    expect(isRetiredItem({ isActive: false })).toBe(true);
    expect(isRetiredItem({ isActive: true })).toBe(false);
    expect(isRetiredItem({})).toBe(false);
  });
});

describe('primaryItem', () => {
  it('devuelve undefined cuando no hay filas', () => {
    expect(primaryItem([])).toBeUndefined();
  });

  it('prefiere la fila vigente sobre la retirada', () => {
    const retirada = { itemId: 'retirada', isActive: false };
    const vigente = { itemId: 'vigente', isActive: true };
    expect(primaryItem([retirada, vigente])?.itemId).toBe('vigente');
  });

  it('devuelve la primera retirada cuando ninguna sigue vigente, porque es la que ocupa la clave', () => {
    const primera = { itemId: 'primera', isActive: false };
    const segunda = { itemId: 'segunda', isActive: false };
    expect(primaryItem([primera, segunda])?.itemId).toBe('primera');
  });
});
