import { describe, it, expect } from 'vitest';
import { DEV_PROFILES } from '@domain/identity/constants/dev-profiles.constants';

describe('dev-profiles.constants', () => {
  it('exports three dev profiles', () => {
    expect(DEV_PROFILES).toHaveLength(3);
  });

  it('has admin profile', () => {
    const admin = DEV_PROFILES.find(p => p.role === 'admin');
    expect(admin).toBeDefined();
    expect(admin?.email).toBe('admin@ransa.pe');
    expect(admin?.username).toBe('admin_root');
  });

  it('has moderator profile', () => {
    const moderator = DEV_PROFILES.find(p => p.role === 'moderator');
    expect(moderator).toBeDefined();
    expect(moderator?.email).toBe('operaciones@ransa.pe');
    expect(moderator?.username).toBe('gerente_ops');
  });

  it('has user profile', () => {
    const user = DEV_PROFILES.find(p => p.role === 'user');
    expect(user).toBeDefined();
    expect(user?.email).toBe('auditoria@ransa.pe');
    expect(user?.username).toBe('auditor_est');
  });

  it('all profiles have required fields', () => {
    DEV_PROFILES.forEach(profile => {
      expect(profile).toHaveProperty('nameKey');
      expect(profile).toHaveProperty('id');
      expect(profile).toHaveProperty('role');
      expect(profile).toHaveProperty('email');
      expect(profile).toHaveProperty('username');
    });
  });
});
