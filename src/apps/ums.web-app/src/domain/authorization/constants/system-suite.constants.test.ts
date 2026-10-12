import { describe, it, expect } from 'vitest';
import {
  SYSTEM_SUITE_STATUSES,
  SYSTEM_SUITE_PAGE_SIZE,
} from '@domain/authorization/constants/system-suite.constants';

describe('system-suite.constants', () => {
  it('exports correct statuses', () => {
    expect(SYSTEM_SUITE_STATUSES).toContain('Active');
    expect(SYSTEM_SUITE_STATUSES).toContain('Maintenance');
    expect(SYSTEM_SUITE_STATUSES).toContain('Deprecated');
    expect(SYSTEM_SUITE_STATUSES).toHaveLength(3);
  });

  it('exports correct page size', () => {
    expect(SYSTEM_SUITE_PAGE_SIZE).toBe(20);
  });
});
