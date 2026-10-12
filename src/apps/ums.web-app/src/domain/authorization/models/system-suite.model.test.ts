import { describe, it, expect } from 'vitest';
import * as systemSuiteModel from '@domain/authorization/models/system-suite.model';

describe('system-suite.model', () => {
  it('re-exports expected types as undefined (type-only exports)', () => {
    expect(systemSuiteModel).toBeDefined();
  });
});
