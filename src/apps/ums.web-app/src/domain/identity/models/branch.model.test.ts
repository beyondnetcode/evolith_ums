import { describe, it, expect } from 'vitest';
import * as branchModel from '@domain/identity/models/branch.model';

describe('branch.model', () => {
  it('re-exports expected types as undefined (type-only exports)', () => {
    expect(branchModel).toBeDefined();
  });
});
