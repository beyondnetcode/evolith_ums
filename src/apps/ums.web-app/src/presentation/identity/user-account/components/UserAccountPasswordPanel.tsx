import React, { useState } from 'react';
import { KeyRound, ShieldCheck, RefreshCw, Copy, Check, ExternalLink, Info } from 'lucide-react';
import {
  useSetUserAccountPassword,
  useForcePasswordReset,
} from '@app/identity/hooks/use-user-account';
import { useGetAllAppConfigurations } from '@app/configuration/hooks/use-app-configuration';
import { useI18n } from '@app/i18n/use-i18n';
import { UserAccount } from '@domain/identity/models/user-account.model';
import { InlineAddForm } from '@shared/components/InlineAddForm';
import { M3TextField } from '@shared/components/M3TextField';
import { M3Button } from '@shared/components/M3Button';
import { Tooltip } from '@shared/components/Tooltip';

interface UserAccountPasswordPanelProps {
  account: UserAccount;
}

export function usesExternalIdentityProvider(configsPage?: {
  items?: Array<{ code: string; value: string }>;
}): boolean {
  return (
    configsPage?.items?.some(
      c => c.code === 'AUTH_USE_EXTERNAL_IDP' && c.value.toLowerCase() === 'true'
    ) ?? false
  );
}

const isLocalAdminUser = (account: UserAccount) => account.category === 'Internal';

const isExternalUser = (account: UserAccount) =>
  account.category === 'External' || account.category === 'Partner' || account.category === 'B2B';

export const UserAccountPasswordPanel: React.FC<UserAccountPasswordPanelProps> = ({ account }) => {
  const t = useI18n();
  const [isChangeOpen, setIsChangeOpen] = useState(false);
  const [password, setPassword] = useState('');
  const [confirmation, setConfirmation] = useState('');
  const [validationError, setValidationError] = useState<string>();
  const [tempPassword, setTempPassword] = useState<string | null>(null);
  const [copied, setCopied] = useState(false);

  const setPasswordMutation = useSetUserAccountPassword(account.userAccountId);
  const forceResetMutation = useForcePasswordReset(account.userAccountId);

  const { data: configsPage } = useGetAllAppConfigurations({
    page: 1,
    pageSize: 50,
    tenantId: account.tenantId,
  });

  const isFederated = usesExternalIdentityProvider(configsPage);
  const isInternal = isLocalAdminUser(account);
  const isExternal = isExternalUser(account);

  const closeChangeForm = () => {
    setIsChangeOpen(false);
    setPassword('');
    setConfirmation('');
    setValidationError(undefined);
  };

  const handleChangeSubmit = async (event: React.FormEvent) => {
    event.preventDefault();
    if (password.length < 12) {
      setValidationError(t.passwordMinLength);
      return;
    }
    if (password !== confirmation) {
      setValidationError(t.passwordMismatch);
      return;
    }
    try {
      await setPasswordMutation.mutateAsync(password);
      closeChangeForm();
    } catch {
      // Notification hook handles the error
    }
  };

  const handleForceReset = async () => {
    setTempPassword(null);
    try {
      const result = await forceResetMutation.mutateAsync();
      setTempPassword(result.temporaryPassword);
    } catch {
      // Notification hook handles the error
    }
  };

  const handleCopy = () => {
    if (!tempPassword) return;
    navigator.clipboard.writeText(tempPassword);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  // ── Federated mode: no local password management ─────────────────────────
  if (isFederated) {
    return (
      <div className="space-y-3 animate-fadeIn">
        <div className="p-3.5 rounded-xl border border-m3-outline/20 bg-m3-surface-container/5 space-y-3">
          <div className="flex items-center gap-2 border-b border-m3-outline/10 pb-2">
            <ShieldCheck className="w-4 h-4 text-m3-primary" />
            <span className="text-[12px] font-medium text-m3-on-surface">
              {t.passwordManagement}
            </span>
          </div>
          <p className="text-[12px] text-m3-secondary">{t.federatedPasswordManagedExternally}</p>
        </div>
      </div>
    );
  }

  // ── External/Partner users: no admin management ──────────────────────────
  if (isExternal) {
    return (
      <div className="space-y-3 animate-fadeIn">
        <div className="p-3.5 rounded-xl border border-amber-500/20 bg-amber-500/5 space-y-3">
          <div className="flex items-center gap-2 border-b border-m3-outline/10 pb-2">
            <Info className="w-4 h-4 text-amber-500" />
            <span className="text-[12px] font-medium text-m3-on-surface">
              Gestión de Contraseña
            </span>
          </div>
          <div className="flex items-start gap-2 text-[11px] text-amber-700 dark:text-amber-400 leading-relaxed">
            <ExternalLink className="w-3.5 h-3.5 shrink-0 mt-0.5" />
            <span>
              Los usuarios externos ({account.category}) gestionan su contraseña y recuperación
              directamente desde la{' '}
              <span className="font-semibold">pantalla de inicio de sesión</span>. El administrador
              no puede modificar sus credenciales desde este módulo.
            </span>
          </div>
        </div>
      </div>
    );
  }

  // ── Internal user (local auth): full management ───────────────────────────
  return (
    <div className="space-y-3 animate-fadeIn">
      {/* Password status */}
      <div className="p-3.5 rounded-xl border border-m3-outline/20 bg-m3-surface-container/5 space-y-3">
        <div className="flex items-center gap-2 border-b border-m3-outline/10 pb-2">
          <ShieldCheck className="w-4 h-4 text-m3-primary" />
          <span className="text-[12px] font-medium text-m3-on-surface">{t.passwordManagement}</span>
          <Tooltip content={t.passwordManagementInfo}>
            <Info className="w-3.5 h-3.5 text-m3-secondary/70 hover:text-m3-primary cursor-help" />
          </Tooltip>
        </div>
        <dl className="space-y-2 text-[11px]">
          <div className="flex items-center justify-between">
            <dt className="text-m3-secondary">{t.localPassword}</dt>
            <dd
              className={
                account.hasActivePassword ? 'text-emerald-500 font-semibold' : 'text-m3-secondary'
              }
            >
              {account.hasActivePassword ? t.active : t.notConfigured}
            </dd>
          </div>
          {account.passwordUpdatedAtUtc && (
            <div className="flex items-center justify-between">
              <dt className="text-m3-secondary">{t.lastPasswordRotation}</dt>
              <dd className="text-m3-on-surface">
                {new Date(account.passwordUpdatedAtUtc).toLocaleDateString()}
              </dd>
            </div>
          )}
        </dl>
      </div>

      {/* Force reset — available for all internal users */}
      {account.hasActivePassword && (
        <div className="p-3.5 rounded-xl border border-m3-outline/20 bg-m3-surface-container/5 space-y-3">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              <RefreshCw className="w-3.5 h-3.5 text-m3-secondary" />
              <span className="text-[12px] font-medium text-m3-on-surface">Reseteo Forzado</span>
            </div>
            <M3Button
              variant="outlined"
              onClick={handleForceReset}
              isLoading={forceResetMutation.isPending}
              className="text-[11px] h-7 px-3"
            >
              Generar contraseña temporal
            </M3Button>
          </div>
          <p className="text-[10px] text-m3-secondary/70 leading-relaxed">
            Genera una contraseña temporal e invalida la actual. El usuario deberá usar esta clave
            para ingresar y luego actualizar su contraseña.
          </p>

          {/* Temp password result */}
          {tempPassword && (
            <div className="mt-2 p-3 rounded-lg border border-emerald-500/30 bg-emerald-500/5 space-y-2">
              <p className="text-[10px] font-semibold text-emerald-600 dark:text-emerald-400 uppercase tracking-wider">
                Contraseña temporal generada
              </p>
              <div className="flex items-center gap-2">
                <code className="flex-1 text-[12px] font-mono text-m3-on-surface bg-m3-surface-container px-2 py-1 rounded select-all">
                  {tempPassword}
                </code>
                <button
                  onClick={handleCopy}
                  className="p-1.5 rounded border border-m3-outline/20 hover:bg-m3-surface-variant transition-colors"
                  title="Copiar al portapapeles"
                >
                  {copied ? (
                    <Check className="w-3.5 h-3.5 text-emerald-500" />
                  ) : (
                    <Copy className="w-3.5 h-3.5 text-m3-secondary" />
                  )}
                </button>
              </div>
              <p className="text-[10px] text-amber-600 dark:text-amber-400">
                Comparta esta clave de forma segura. Solo es válida hasta que el usuario inicie
                sesión y establezca una nueva.
              </p>
            </div>
          )}
        </div>
      )}

      {/* Direct password change — only for Internal users (USER ADMIN LOCAL) */}
      {isInternal && (
        <InlineAddForm
          isOpen={isChangeOpen}
          onToggle={open => (open ? setIsChangeOpen(true) : closeChangeForm())}
          onSubmit={handleChangeSubmit}
          addLabel={account.hasActivePassword ? t.rotatePassword : t.setTemporaryPassword}
          title={account.hasActivePassword ? t.rotatePassword : t.setTemporaryPassword}
          submitLabel={t.savePassword}
          cancelLabel={t.cancelBtn}
          isLoading={setPasswordMutation.isPending}
          error={validationError}
          triggerEmphasis="quiet"
          icon={<KeyRound className="w-3.5 h-3.5" />}
        >
          <M3TextField
            label={t.temporaryPassword}
            type="password"
            autoComplete="new-password"
            required
            compact
            value={password}
            onChange={event => {
              setPassword(event.target.value);
              setValidationError(undefined);
            }}
            helperText={t.passwordRule}
          />
          <M3TextField
            label={t.confirmPassword}
            type="password"
            autoComplete="new-password"
            required
            compact
            value={confirmation}
            onChange={event => {
              setConfirmation(event.target.value);
              setValidationError(undefined);
            }}
          />
        </InlineAddForm>
      )}
    </div>
  );
};
