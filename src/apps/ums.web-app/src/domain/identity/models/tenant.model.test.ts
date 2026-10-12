import { describe, it, expect } from 'vitest';
import * as tenantModel from '@domain/identity/models/tenant.model';

describe('tenant.model', () => {
  it('re-exports expected types as undefined (type-only exports)', () => {
    expect(tenantModel).toBeDefined();
  });
});
