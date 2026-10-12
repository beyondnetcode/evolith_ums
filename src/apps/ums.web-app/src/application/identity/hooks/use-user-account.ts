/**
 * use-user-account.ts — TanStack Query hooks for UserAccount bounded context
 *
 * Queries use useQuery directly (REST reads).
 * Mutations use useNotifiedMutation factory (REST writes).
 */
import { useQuery } from '@tanstack/react-query';
import userAccountService from '@infra/identity/services/user-account.service';
import { useNotifiedMutation } from '@app/hooks/use-notified-mutation';
import { useI18n } from '@app/i18n/use-i18n';
import {
  CreateUserAccountPayload,
  UserAccount,
  UserAccountPage,
} from '@domain/identity/models/user-account.model';
import { CONTEXT_QUERY_CONFIG } from '@app/shared/config/query.config';
import {
  getHttpStatus,
  isNonRecoverable,
  isNetworkError,
  getRetryOptions,
} from '@app/utils/error-utils';

export interface UserAccountQueryParams {
  page: number;
  pageSize: number;
  search?: string;
  criteria?: string;
  status?: string;
  sortBy?: string;
  sortOrder?: 'asc' | 'desc';
  tenantId?: string;
}

// ─── Queries ────────────────────────────────────────────────────────────────

export const useGetAllUserAccounts = (params: UserAccountQueryParams | null) => {
  return useQuery<UserAccountPage>({
    queryKey: [
      'user-accounts',
      params?.page,
      params?.pageSize,
      params?.search,
      params?.criteria,
      params?.status,
      params?.sortBy,
      params?.sortOrder,
      params?.tenantId,
    ],
    queryFn: () => userAccountService.getAll(params!),
    enabled: !!params,
    ...CONTEXT_QUERY_CONFIG.USER_ACCOUNT,
    ...getRetryOptions({ maxRetries: 1, networkErrorMaxRetries: 2 }),
  });
};

export const useGetUserAccount = (userAccountId: string | null) => {
  return useQuery<UserAccount | null>({
    queryKey: ['user-accounts', userAccountId],
    queryFn: async () => {
      if (!userAccountId) throw new Error('User account ID required');
      try {
        return await userAccountService.getById(userAccountId);
      } catch (err: unknown) {
        if (getHttpStatus(err) === 404) return null;
        throw err;
      }
    },
    enabled: !!userAccountId,
    ...CONTEXT_QUERY_CONFIG.USER_ACCOUNT,
    ...getRetryOptions({ maxRetries: 1, networkErrorMaxRetries: 2 }),
  });
};

// ─── Mutations ──────────────────────────────────────────────────────────────

export const useCreateUserAccount = () => {
  const t = useI18n();
  return useNotifiedMutation({
    mutationFn: (payload: CreateUserAccountPayload) =>
      userAccountService.createUserAccount(payload),
    invalidateKeys: [['user-accounts']],
    successNotif: () => ({
      title: t.notifUserCreated,
      message: t.notifUserCreatedMsg,
    }),
    errorNotif: () => ({
      title: t.notifUserCreateFailed,
      message: t.notifUserCreateFailedMsg,
    }),
  });
};

export const useActivateUserAccount = (userAccountId: string) => {
  const t = useI18n();
  return useNotifiedMutation({
    mutationFn: () => userAccountService.activateUserAccount(userAccountId),
    invalidateKeys: [['user-accounts'], ['user-accounts', userAccountId]],
    successNotif: () => ({
      title: t.notifActivated,
      message: t.notifUserActivatedMsg,
    }),
    errorNotif: () => ({
      title: t.notifActivateFailed,
      message: t.notifUserActivateFailedMsg,
    }),
  });
};

export const useBlockUserAccount = (userAccountId: string) => {
  const t = useI18n();
  return useNotifiedMutation({
    mutationFn: (reason: string) => userAccountService.blockUserAccount(userAccountId, reason),
    invalidateKeys: [['user-accounts'], ['user-accounts', userAccountId]],
    successNotif: () => ({
      title: t.notifBlocked,
      message: t.notifUserBlockedMsg,
      type: 'warning',
    }),
    errorNotif: () => ({
      title: t.notifBlockFailed,
      message: t.notifUserBlockFailedMsg,
    }),
  });
};

export const useRestoreUserAccount = (userAccountId: string) => {
  const t = useI18n();
  return useNotifiedMutation({
    mutationFn: () => userAccountService.restoreUserAccount(userAccountId),
    invalidateKeys: [['user-accounts'], ['user-accounts', userAccountId]],
    successNotif: () => ({
      title: t.notifRestored,
      message: t.notifUserRestoredMsg,
    }),
    errorNotif: () => ({
      title: t.notifRestoreFailed,
      message: t.notifUserRestoreFailedMsg,
    }),
  });
};

export const useSetUserAccountPassword = (userAccountId: string) => {
  const t = useI18n();
  return useNotifiedMutation({
    mutationFn: (password: string) =>
      userAccountService.setUserAccountPassword({
        userAccountId,
        password,
      }),
    invalidateKeys: [['user-accounts'], ['user-accounts', userAccountId]],
    successNotif: () => ({
      title: t.notifPasswordUpdated,
      message: t.notifPasswordUpdatedMsg,
    }),
    errorNotif: () => ({
      title: t.notifPasswordUpdateFailed,
      message: t.notifPasswordUpdateFailedMsg,
    }),
  });
};

export const useForcePasswordReset = (userAccountId: string) =>
  useNotifiedMutation({
    mutationFn: () => userAccountService.forcePasswordReset(userAccountId),
    invalidateKeys: [['user-accounts'], ['user-accounts', userAccountId]],
    successNotif: () => ({
      title: 'Reseteo Forzado',
      message: 'Se generó una contraseña temporal. Compártala de forma segura con el usuario.',
    }),
    errorNotif: () => ({
      title: 'Error al resetear',
      message: 'No se pudo forzar el reseteo de contraseña.',
    }),
  });
