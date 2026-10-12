import { describe, it, expect } from 'vitest';
import {
  CODIGO_INQUILINO_RAIZ,
  LONGITUD_MAXIMA_CODIGO_INQUILINO,
  TENANT_PAGE_SIZE,
  TENANT_TYPES,
  normalizarCodigoInquilino,
  parsearInquilinosDeclarados,
  resolverCodigoPorOmision,
} from './tenant.constants';

describe('tenant.constants', () => {
  it('exports correct tenant types', () => {
    expect(TENANT_TYPES).toContain('INTERNAL');
    expect(TENANT_TYPES).toContain('SUPPLIER');
    expect(TENANT_TYPES).toContain('CLIENT');
    expect(TENANT_TYPES).toHaveLength(3);
  });

  it('exports correct page size', () => {
    expect(TENANT_PAGE_SIZE).toBe(9);
  });
});

describe('normalizarCodigoInquilino', () => {
  it('recorta y pasa a mayúsculas, que es lo que el servidor compara', () => {
    expect(normalizarCodigoInquilino('  comex_andina ')).toBe('COMEX_ANDINA');
  });

  it('no excede el máximo que acepta el servidor', () => {
    const largo = 'A'.repeat(LONGITUD_MAXIMA_CODIGO_INQUILINO + 10);
    expect(normalizarCodigoInquilino(largo)).toHaveLength(LONGITUD_MAXIMA_CODIGO_INQUILINO);
  });
});

describe('parsearInquilinosDeclarados', () => {
  it('no declara nada cuando la variable no existe o no es texto', () => {
    // Frontera (P-FE-06): una variable de entorno es texto arbitrario, no un contrato.
    expect(parsearInquilinosDeclarados(undefined)).toEqual([]);
    expect(parsearInquilinosDeclarados('')).toEqual([]);
    expect(parsearInquilinosDeclarados('   ')).toEqual([]);
    expect(parsearInquilinosDeclarados(42)).toEqual([]);
    expect(parsearInquilinosDeclarados(['EVOLITH'])).toEqual([]);
  });

  it('lee pares código|nombre separados por comas', () => {
    expect(parsearInquilinosDeclarados('EVOLITH|Evolith UMS, comex_andina|Comex Andina')).toEqual([
      { code: 'EVOLITH', name: 'Evolith UMS' },
      { code: 'COMEX_ANDINA', name: 'Comex Andina' },
    ]);
  });

  it('acepta el código a secas y usa el código como nombre', () => {
    expect(parsearInquilinosDeclarados('EVOLITH')).toEqual([{ code: 'EVOLITH', name: 'EVOLITH' }]);
  });

  it('descarta entradas vacías y códigos repetidos sin romper el resto', () => {
    expect(parsearInquilinosDeclarados(',EVOLITH,,evolith|Duplicado, SIL |Sistema SIL,')).toEqual([
      { code: 'EVOLITH', name: 'EVOLITH' },
      { code: 'SIL', name: 'Sistema SIL' },
    ]);
  });
});

describe('resolverCodigoPorOmision', () => {
  it('manda lo que declara el despliegue', () => {
    expect(resolverCodigoPorOmision(' comex_andina ', [])).toBe('COMEX_ANDINA');
  });

  it('sin declaración, arranca en la primera sugerencia', () => {
    expect(resolverCodigoPorOmision(undefined, [{ code: 'SIL', name: 'Sistema SIL' }])).toBe('SIL');
  });

  it('sin declaración ni sugerencias, arranca en el inquilino raíz', () => {
    // Es el único código que este portal puede dar por existente: la organización que lo opera
    // (ADR-0071 / FS-26). Y es editable — no esconde ningún otro inquilino.
    expect(resolverCodigoPorOmision(null, [])).toBe(CODIGO_INQUILINO_RAIZ);
  });
});
