import { describe, it, expect } from 'vitest';
import {
  SchemaVersion,
  isSchemaVersionSupported,
  isMinorAhead,
  isMinorBehind,
  isMajorMatch
} from '../src/index.js';

describe('SchemaVersion compatibility', () => {
  it.each([
    ['2.0.0', true],
    ['2.0.5', true],
    ['2.99.0', true],
    ['3.0.0', false],
    ['3.5.3', false],
    ['0.9.0', false],
    ['', false],
    [null, false],
    [undefined, false],
    ['not-a-version', false]
  ])('isSchemaVersionSupported(%s) === %s', (input, expected) => {
    expect(isSchemaVersionSupported(input)).toBe(expected);
  });

  it('Current and bounds match expected compatibility window', () => {
    expect(isSchemaVersionSupported(SchemaVersion.Current)).toBe(true);
    expect(isSchemaVersionSupported(SchemaVersion.Projected)).toBe(true);
    expect(isSchemaVersionSupported(SchemaVersion.CompatibilityMinInclusive)).toBe(true);
    expect(isSchemaVersionSupported(SchemaVersion.CompatibilityMaxExclusive)).toBe(false);
  });

  it('isMinorAhead detects server newer than SDK', () => {
    // Los casos se DERIVAN de la constante en vez de repetirla. Cuando la lista copiaba el
    // número, cada versión nueva rompía la prueba y arreglarla consistía en volver a copiarlo: el
    // pin acababa afirmando la versión de ayer. Lo que se verifica es la RELACIÓN.
    //
    // El techo es `Latest`, no `Current`: `Current` es lo que se EMITE en una respuesta completa
    // (2.4.0) y `Latest` lo MÁS ALTO que el SDK entiende (2.5.0, la respuesta proyectada). Comparar
    // contra `Current` haría que un grafo proyectado contara como «servidor por delante».
    const [major, minor] = SchemaVersion.Latest.split('.').map(Number);

    expect(isMinorAhead(`${major}.${minor + 1}.0`)).toBe(true);
    expect(isMinorAhead(`${major}.${minor}.0`)).toBe(false);
    for (let anterior = minor - 1; anterior >= 0; anterior--) {
      expect(isMinorAhead(`${major}.${anterior}.0`)).toBe(false);
    }
    // Un MAJOR distinto no es «por delante»: es incompatible.
    expect(isMinorAhead(`${major + 1}.0.0`)).toBe(false);
    if (major > 0) expect(isMinorAhead(`${major - 1}.99.0`)).toBe(false);
  });

  it('isMinorBehind detects SDK newer than server', () => {
    const [major, minor] = SchemaVersion.Latest.split('.').map(Number);

    // Un MAJOR distinto no es «por detrás»: es incompatible.
    expect(isMinorBehind(`${major - 1}.0.0`)).toBe(false);
    expect(isMinorBehind('0.9.0')).toBe(false);
    // El SDK va por delante de un servidor con MINOR anterior.
    if (minor > 0) expect(isMinorBehind(`${major}.${minor - 1}.0`)).toBe(true);
  });

  it('isMajorMatch checks major equality', () => {
    expect(isMajorMatch('2.999.999')).toBe(true);
    expect(isMajorMatch('3.0.0')).toBe(false);
  });
});
