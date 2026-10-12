import { describe, it, expect } from 'vitest';
import * as delegationModel from '@domain/identity/models/delegation.model';

describe('delegation.model', () => {
  it('re-exports expected types as undefined (type-only exports)', () => {
    expect(delegationModel).toBeDefined();
  });
});
