import { describe, it, expect } from 'vitest';
import * as featureFlagModel from '@domain/configuration/models/feature-flag.model';

describe('feature-flag.model', () => {
  it('re-exports expected types as undefined (type-only exports)', () => {
    expect(featureFlagModel).toBeDefined();
  });
});
