import { describe, it, expect, vi } from 'vitest';
import { renderHook } from '@testing-library/react';
import { useEffectiveTenant } from './use-effective-tenant';
import { useAuthStore } from '@app/stores/auth.store';

vi.mock('@app/stores/auth.store', () => ({
  useAuthStore: vi.fn(),
}));

describe('useEffectiveTenant', () => {
  it('returns session tenant when no override provided', () => {
    vi.mocked(useAuthStore).mockImplementation((selector?: never) =>
      selector({ user: { tenantId: 'session-tenant' } } as never)
    );

    const { result } = renderHook(() => useEffectiveTenant());

    expect(result.current).toBe('session-tenant');
  });

  it('returns override tenant when provided', () => {
    vi.mocked(useAuthStore).mockImplementation((selector?: never) =>
      selector({ user: { tenantId: 'session-tenant' } } as never)
    );

    const { result } = renderHook(() => useEffectiveTenant('override-tenant'));

    expect(result.current).toBe('override-tenant');
  });

  it('returns undefined when no tenant in session and no override', () => {
    vi.mocked(useAuthStore).mockImplementation((selector?: never) =>
      selector({ user: { tenantId: undefined } } as never)
    );

    const { result } = renderHook(() => useEffectiveTenant());

    expect(result.current).toBeUndefined();
  });

  it('prefers override even if session tenant exists', () => {
    vi.mocked(useAuthStore).mockImplementation((selector?: never) =>
      selector({ user: { tenantId: 'session-tenant' } } as never)
    );

    const { result } = renderHook(() => useEffectiveTenant('override-tenant'));

    expect(result.current).toBe('override-tenant');
  });
});
