import { describe, it, expect } from 'vitest';
import * as userAccountModel from '@domain/identity/models/user-account.model';

describe('user-account.model', () => {
  it('re-exports expected types as undefined (type-only exports)', () => {
    expect(userAccountModel).toBeDefined();
  });
});
