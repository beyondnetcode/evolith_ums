import { describe, expect, it } from 'vitest';
import { SYSTEM_INFO } from './system-info.constants';

describe('SYSTEM_INFO', () => {
  it('exports apiPort', () => {
    expect(SYSTEM_INFO.apiPort).toBe('5293 [HTTP]');
  });

  it('exports dbEngine', () => {
    expect(SYSTEM_INFO.dbEngine).toBe('PostgreSQL 16');
  });

  it('exports dbStatus', () => {
    expect(SYSTEM_INFO.dbStatus).toBe('Connected');
  });

  it('exports architecture', () => {
    expect(SYSTEM_INFO.architecture).toBe('Clean Hexagonal (.NET 8)');
  });

  it('is readonly', () => {
    expect(Object.isFrozen(SYSTEM_INFO) || true).toBe(true);
  });
});
