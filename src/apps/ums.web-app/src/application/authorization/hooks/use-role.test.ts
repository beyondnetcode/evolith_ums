import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook } from '@testing-library/react';
import { useRolesBySystemSuite, useCreateRole, useUpdateRole, useSetRoleActive } from './use-role';
import * as reactQueryModule from '@tanstack/react-query';
import * as useI18nModule from '@app/i18n/use-i18n';

vi.mock('@tanstack/react-query');
vi.mock('@app/i18n/use-i18n');
vi.mock('@app/hooks/use-notified-mutation', () => ({
  useNotifiedMutation: () => ({ mutate: vi.fn(), mutateAsync: vi.fn(), isPending: false }),
}));
vi.mock('@infra/authorization/services/role.service', () => ({
  default: {
    getBySystemSuite: vi.fn().mockResolvedValue([]),
    create: vi.fn(),
    update: vi.fn(),
    setActive: vi.fn(),
  },
}));

describe('use-role hooks', () => {
  beforeEach(() => {
    vi.restoreAllMocks();

    vi.mocked(reactQueryModule.useQuery).mockReturnValue({
      data: [],
      isLoading: false,
      error: null,
    } as any);

    vi.mocked(useI18nModule.useI18n).mockReturnValue({
      notifRoleCreated: 'Role Created',
      notifRoleCreatedMsg: 'Role has been created',
      notifRoleCreateFailed: 'Create Failed',
      notifRoleCreateFailedMsg: 'Failed to create role',
      notifRoleUpdated: 'Role Updated',
      notifRoleUpdatedMsg: 'Role has been updated',
      notifRoleUpdateFailed: 'Update Failed',
      notifRoleUpdateFailedMsg: 'Failed to update role',
      notifRoleStatusChanged: 'Status Changed',
      notifRoleStatusChangedMsg: 'Role status changed',
      notifRoleStatusFailed: 'Status Failed',
      notifRoleStatusFailedMsg: 'Failed to change status',
    } as any);
  });

  describe('useRolesBySystemSuite', () => {
    it('calls useQuery with correct queryKey when systemSuiteId provided', () => {
      renderHook(() => useRolesBySystemSuite('ss-1'));

      expect(reactQueryModule.useQuery).toHaveBeenCalledWith(
        expect.objectContaining({
          queryKey: ['system-suite-roles', 'ss-1'],
          enabled: true,
        })
      );
    });

    it('disables query when systemSuiteId is empty', () => {
      renderHook(() => useRolesBySystemSuite(''));

      expect(reactQueryModule.useQuery).toHaveBeenCalledWith(
        expect.objectContaining({
          enabled: false,
        })
      );
    });
  });

  describe('useCreateRole', () => {
    it('returns mutation object', () => {
      const { result } = renderHook(() => useCreateRole('ss-1'));
      expect(result.current.mutate).toBeDefined();
    });
  });

  describe('useUpdateRole', () => {
    it('returns mutation object', () => {
      const { result } = renderHook(() => useUpdateRole('ss-1', 'r-1'));
      expect(result.current.mutate).toBeDefined();
    });
  });

  describe('useSetRoleActive', () => {
    it('returns mutation object', () => {
      const { result } = renderHook(() => useSetRoleActive('ss-1'));
      expect(result.current.mutate).toBeDefined();
    });
  });
});
