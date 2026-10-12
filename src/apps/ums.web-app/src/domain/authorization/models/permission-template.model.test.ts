import { describe, it, expect } from 'vitest';
import { itemEffect } from '@domain/authorization/schemas/permission-template.schema';

describe('permission-template.model', () => {
  it('re-exports itemEffect function', () => {
    expect(itemEffect).toBeDefined();
    expect(typeof itemEffect).toBe('function');
  });

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
});
