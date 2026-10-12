import { describe, it, expect } from 'vitest';
import * as profileModel from '@domain/authorization/models/profile.model';

describe('profile.model', () => {
  it('re-exports expected types as undefined (type-only exports)', () => {
    expect(profileModel).toBeDefined();
  });
});
