import { describe, it, expect, beforeEach } from 'vitest';
import { useAuthStore } from './auth.store';

describe('auth.store', () => {
  beforeEach(() => {
    useAuthStore.setState({
      user: null,
      isAuthenticated: false,
    });
  });

  it('initializes with correct state shape', () => {
    const state = useAuthStore.getState();
    expect(state).toHaveProperty('user');
    expect(state).toHaveProperty('isAuthenticated');
    expect(state).toHaveProperty('login');
    expect(state).toHaveProperty('logout');
  });

  it('logs in a user', () => {
    const testUser = {
      id: 'test-123',
      username: 'testuser',
      email: 'test@example.com',
      role: 'admin',
      tenantId: 'tenant-123',
      tenantCode: 'TEST_TENANT',
      tenantName: 'Test Tenant',
      permissions: [],
      sessionTrackingId: 'session-123',
      isInternalAdmin: false,
      crossTenantAccessEnabled: false,
      sessionParameters: {
        sessionTimeoutMinutes: 30,
        accessTokenDurationMs: 3600000,
        refreshTokenDurationMs: 604800000,
        maxLoginAttempts: 5,
        minPasswordLength: 12,
        mfaRequiredForAdmin: false,
        mfaAllowedMethods: ['Totp'],
        customBrandingEnabled: false,
        defaultLanguage: 'es',
        defaultTimezone: 'America/Lima',
      },
    };

    useAuthStore.getState().login(testUser);

    const state = useAuthStore.getState();
    expect(state.isAuthenticated).toBe(true);
    expect(state.user).toMatchObject({
      id: 'test-123',
      username: 'testuser',
      email: 'test@example.com',
      role: 'admin',
      tenantId: 'tenant-123',
      tenantCode: 'TEST_TENANT',
      tenantName: 'Test Tenant',
      isInternalAdmin: false,
      crossTenantAccessEnabled: false,
      originalTenantId: 'tenant-123',
    });
  });

  it('logs out a user', () => {
    const testUser = {
      id: 'test-123',
      username: 'testuser',
      email: 'test@example.com',
      role: 'admin',
      tenantId: 'tenant-123',
      tenantCode: 'TEST_TENANT',
      tenantName: 'Test Tenant',
      permissions: [],
      sessionTrackingId: 'session-123',
      isInternalAdmin: false,
      crossTenantAccessEnabled: false,
      sessionParameters: null,
    };

    useAuthStore.getState().login(testUser);
    expect(useAuthStore.getState().isAuthenticated).toBe(true);

    useAuthStore.getState().logout();

    const state = useAuthStore.getState();
    expect(state.isAuthenticated).toBe(false);
    expect(state.user).toBeNull();
  });

  it('selects user username via selector', () => {
    const testUser = {
      id: 'test-123',
      username: 'selector-test',
      email: 'test@example.com',
      role: 'user',
      tenantId: 'tenant-123',
      tenantCode: 'TEST_TENANT',
      tenantName: 'Test Tenant',
      permissions: [],
      sessionTrackingId: 'session-123',
      isInternalAdmin: false,
      crossTenantAccessEnabled: false,
      sessionParameters: null,
    };

    useAuthStore.getState().login(testUser);

    const username = useAuthStore.getState().user?.username;
    expect(username).toBe('selector-test');
  });

  it('overwrites previous user on subsequent login', () => {
    const user1 = {
      id: 'u1',
      username: 'first',
      email: 'a@b.com',
      role: 'user',
      tenantId: 't1',
      tenantCode: 'TENANT_1',
      tenantName: 'Tenant 1',
      permissions: [],
      sessionTrackingId: 'session-1',
      isInternalAdmin: false,
      crossTenantAccessEnabled: false,
      sessionParameters: null,
    };
    const user2 = {
      id: 'u2',
      username: 'second',
      email: 'c@d.com',
      role: 'admin',
      tenantId: 't2',
      tenantCode: 'TENANT_2',
      tenantName: 'Tenant 2',
      permissions: [],
      sessionTrackingId: 'session-2',
      isInternalAdmin: true,
      crossTenantAccessEnabled: false,
      sessionParameters: null,
    };

    useAuthStore.getState().login(user1);
    expect(useAuthStore.getState().user?.username).toBe('first');

    useAuthStore.getState().login(user2);
    expect(useAuthStore.getState().user?.username).toBe('second');
    expect(useAuthStore.getState().isAuthenticated).toBe(true);
  });
});
