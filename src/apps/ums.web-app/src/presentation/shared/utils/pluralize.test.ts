import { describe, it, expect } from 'vitest';
import { pluralizeEs, formatItemCountLabel } from './pluralize';

describe('pluralizeEs', () => {
  it('agrega -s a palabras terminadas en vocal', () => {
    expect(pluralizeEs('módulo')).toBe('módulos');
    expect(pluralizeEs('recurso')).toBe('recursos');
    expect(pluralizeEs('cuenta')).toBe('cuentas');
    expect(pluralizeEs('suite')).toBe('suites');
  });

  it('convierte -ión en -iones (sin tilde)', () => {
    expect(pluralizeEs('acción')).toBe('acciones');
    expect(pluralizeEs('Acción')).toBe('Acciones');
    expect(pluralizeEs('configuración')).toBe('configuraciones');
    expect(pluralizeEs('delegación')).toBe('delegaciones');
  });

  it('convierte -z en -ces', () => {
    expect(pluralizeEs('voz')).toBe('voces');
  });

  it('agrega -es a otras consonantes', () => {
    expect(pluralizeEs('Rol')).toBe('Roles');
    expect(pluralizeEs('perfil')).toBe('perfiles');
    expect(pluralizeEs('sucursal')).toBe('sucursales');
    expect(pluralizeEs('proveedor')).toBe('proveedores');
    expect(pluralizeEs('solicitud')).toBe('solicitudes');
  });
});

describe('formatItemCountLabel', () => {
  it('usa el singular cuando el conteo es 1', () => {
    expect(formatItemCountLabel(1, 'Acción')).toBe('1 Acción');
    expect(formatItemCountLabel(1, 'Rol')).toBe('1 Rol');
  });

  it('deriva el plural en español cuando el conteo no es 1', () => {
    expect(formatItemCountLabel(0, 'Acción')).toBe('0 Acciones');
    expect(formatItemCountLabel(7, 'Acción')).toBe('7 Acciones');
    expect(formatItemCountLabel(4, 'Rol')).toBe('4 Roles');
  });

  it('respeta el plural explícito para préstamos/irregulares', () => {
    expect(formatItemCountLabel(3, 'tenant', 'tenants')).toBe('3 tenants');
    expect(formatItemCountLabel(2, 'flag', 'flags')).toBe('2 flags');
  });
});
