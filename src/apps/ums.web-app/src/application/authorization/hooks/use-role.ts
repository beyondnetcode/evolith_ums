import { useQuery } from '@tanstack/react-query';
import roleService from '@infra/authorization/services/role.service';
import { useNotifiedMutation } from '@app/hooks/use-notified-mutation';
import { useI18n } from '@app/i18n/use-i18n';
import type {
  CreateRolePayload,
  Role,
  UpdateRolePayload,
} from '@domain/authorization/schemas/role.schema';

export const useRolesBySystemSuite = (systemSuiteId: string) =>
  useQuery<Role[]>({
    queryKey: ['system-suite-roles', systemSuiteId],
    queryFn: () => roleService.getBySystemSuite(systemSuiteId),
    enabled: !!systemSuiteId,
  });

export const useCreateRole = (systemSuiteId: string) => {
  const t = useI18n();
  return useNotifiedMutation({
    mutationFn: (payload: CreateRolePayload) => roleService.create(systemSuiteId, payload),
    invalidateKeys: [['system-suite-roles', systemSuiteId]],
    successNotif: () => ({ title: t.notifRoleCreated, message: t.notifRoleCreatedMsg }),
    errorNotif: () => ({ title: t.notifRoleCreateFailed, message: t.notifRoleCreateFailedMsg }),
  });
};

export const useUpdateRole = (systemSuiteId: string, roleId: string) => {
  const t = useI18n();
  return useNotifiedMutation({
    mutationFn: (payload: UpdateRolePayload) => roleService.update(systemSuiteId, roleId, payload),
    invalidateKeys: [['system-suite-roles', systemSuiteId]],
    successNotif: () => ({ title: t.notifRoleUpdated, message: t.notifRoleUpdatedMsg }),
    errorNotif: () => ({ title: t.notifRoleUpdateFailed, message: t.notifRoleUpdateFailedMsg }),
  });
};

export const useSetRoleActive = (systemSuiteId: string) => {
  const t = useI18n();
  return useNotifiedMutation({
    mutationFn: ({ roleId, isActive }: { roleId: string; isActive: boolean }) =>
      roleService.setActive(systemSuiteId, roleId, isActive),
    invalidateKeys: [['system-suite-roles', systemSuiteId]],
    successNotif: () => ({ title: t.notifRoleStatusChanged, message: t.notifRoleStatusChangedMsg }),
    errorNotif: () => ({ title: t.notifRoleStatusFailed, message: t.notifRoleStatusFailedMsg }),
  });
};
