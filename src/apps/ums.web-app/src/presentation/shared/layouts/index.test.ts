import { describe, it, expect } from 'vitest';
import * as layouts from './index';

describe('layouts index', () => {
  it('exports MainLayout', () => {
    expect(layouts.MainLayout).toBeDefined();
  });

  it('exports MasterDetailLayout', () => {
    expect(layouts.MasterDetailLayout).toBeDefined();
  });

  it('exports PageShell', () => {
    expect(layouts.PageShell).toBeDefined();
  });
});
