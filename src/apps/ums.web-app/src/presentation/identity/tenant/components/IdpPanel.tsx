import React, { useEffect, useCallback, useState } from 'react';
import { useI18n } from '@app/i18n/use-i18n';
import { useInlineEdit } from '@app/hooks/use-inline-edit';
import { useResetOnChange } from '@app/hooks/use-reset-on-change';
import { useNotificationStore } from '@app/stores/notification.store';
import {
  useGetAllAppConfigurations,
  useUpdateAppConfiguration,
} from '@app/configuration/hooks/use-app-configuration';
import { formatSystemCode } from '@app/utils/security';
import { idpService } from '@infra/identity/services/idp.service';
import type { IdentityProvider } from '@domain/identity/schemas/identity-provider.schema';
import { M3Button } from '@shared/components/M3Button';
import { M3TextField } from '@shared/components/M3TextField';
import { M3Select } from '@shared/components/M3Select';
import { M3Switch } from '@shared/components/M3Switch';
import { EntityRow } from '@shared/components/EntityRow';
import { InlineAddForm } from '@shared/components/InlineAddForm';
import { EmptyState } from '@shared/components/EmptyState';
import { SectionHeader } from '@shared/components/SectionHeader';
import { CodeBadge } from '@shared/components/CodeBadge';
import { IconButton, Tooltip } from '@shared/components/Tooltip';
import { Key, Pencil, Save, Check, Trash2, X, ShieldAlert, Cpu, AlertTriangle } from 'lucide-react';
import { M3Dialog } from '@shared/components/M3Dialog';
import { ListToolbar } from '@shared/components/ListToolbar';
import { IDP_STRATEGIES } from '@domain/identity/constants/idp.constants';
import type { IdpStrategy } from '@domain/identity/constants/idp.constants';

interface IdpDraft {
  name: string;
  code: string;
  description: string;
  strategy: string;
}

interface IdpPanelProps {
  tenantId: string;
}

export const IdpPanel: React.FC<IdpPanelProps> = ({ tenantId }) => {
  const t = useI18n();
  const addNotification = useNotificationStore(s => s.addNotification);

  const [providers, setProviders] = useState<IdentityProvider[]>([]);
  const [isLoading, setIsLoading] = useState(false);

  const edit = useInlineEdit<IdpDraft>(['name', 'code', 'description', 'strategy']);

  const [isAddingProvider, setIsAddingProvider] = useState(false);
  const [provName, setProvName] = useState('');
  const [provCode, setProvCode] = useState('');
  const [provDescription, setProvDescription] = useState('');
  const [provStrategy, setProvStrategy] = useState<IdpStrategy>('OIDC');
  const [viewMode, setViewMode] = useState<'list' | 'thumbnail'>('list');
  const [activeFilter, setActiveFilter] = useState('all');
  const [sortBy, setSortBy] = useState('name');
  const [sortOrder, setSortOrder] = useState<'asc' | 'desc'>('asc');
  const [showAuthModeConfirm, setShowAuthModeConfirm] = useState(false);

  // Load tenant parameters/configs
  const { data: configsPage } = useGetAllAppConfigurations({
    page: 1,
    pageSize: 50,
    tenantId,
  });

  const updateConfigMutation = useUpdateAppConfiguration();

  const configItem = configsPage?.items?.find(c => c.code === 'AUTH_USE_EXTERNAL_IDP');
  const useExternalIdp = configItem?.value?.toLowerCase() === 'true';

  useResetOnChange(tenantId, () => {
    setIsAddingProvider(false);
    setProvName('');
    setProvCode('');
    setProvDescription('');
    setViewMode('list');
    setActiveFilter('all');
    edit.cancelEdit();
  });

  const loadProviders = useCallback(async () => {
    if (!tenantId) return;
    setIsLoading(true);
    try {
      const data = await idpService.getByIdentityProviders(tenantId);
      setProviders(data);
    } catch {
      addNotification({ title: t.error, message: t.notifIdpLoadFailed, type: 'error' });
    } finally {
      setIsLoading(false);
    }
  }, [tenantId, addNotification, t]);

  useEffect(() => {
    loadProviders();
  }, [loadProviders]);

  const strategyLabels: Record<string, string> = {
    OIDC: t.strategyOIDC,
    SAML2: t.strategySAML2,
    OAuth2: t.strategyOAuth2,
  };

  const handleToggleAuthMode = () => {
    if (!configItem) return;
    // Show confirmation before committing — toggling auth mode affects all users of this tenant.
    setShowAuthModeConfirm(true);
  };

  const confirmToggleAuthMode = async () => {
    setShowAuthModeConfirm(false);
    if (!configItem) return;

    // Switching TO IdP mode: require at least one active IdP
    if (!useExternalIdp) {
      const hasActiveIdp = providers.some(p => p.isActive);
      if (!hasActiveIdp) {
        addNotification({
          title: 'Requisito no cumplido',
          message:
            'Debe haber al menos un proveedor de identidad activo antes de cambiar al modo IDPs.',
          type: 'error',
        });
        return;
      }
    }

    try {
      await updateConfigMutation.mutateAsync({
        id: configItem.appConfigurationId,
        payload: {
          value: useExternalIdp ? 'false' : 'true',
          description: configItem.description,
          status: 'Active',
        },
        rowVersion: configItem.rowVersion,
      });
      addNotification({
        title: 'Modo de Autenticación Actualizado',
        message: `El inquilino ahora utiliza el modo ${!useExternalIdp ? 'Proveedores de Identidad (IDPs)' : 'Esquema Local (BCrypt)'}.`,
        type: 'success',
      });
    } catch {
      addNotification({
        title: t.error,
        message: 'No se pudo cambiar el modo de autenticación.',
        type: 'error',
      });
    }
  };

  const saveProviderEdit = async () => {
    const result = edit.commitEdit();
    if (!result || !result.draft.name?.trim()) return;
    const name = result.draft.name?.trim();
    if (!name) return;
    addNotification({
      title: t.notifProviderUpdated,
      message: t.notifProviderUpdatedMsg(name),
      type: 'success',
    });
    await loadProviders();
  };

  const handleAddProvider = async (e: React.FormEvent) => {
    e.preventDefault();
    // El backend exige nombre Y descripción no vacíos (G-156): validar antes del submit para no
    // enviar un 400 seguro y dar feedback en vez de un fallo silencioso.
    if (!provName.trim() || !provDescription.trim()) {
      addNotification({
        title: 'Datos incompletos',
        message: 'El nombre y la descripción del proveedor son obligatorios.',
        type: 'warning',
      });
      return;
    }
    try {
      await idpService.registerIdentityProvider(tenantId, {
        name: provName.trim(),
        code: formatSystemCode(provCode) || formatSystemCode(provName).substring(0, 20),
        description: provDescription.trim(),
        strategy: provStrategy,
      });
      setProvName('');
      setProvCode('');
      setProvDescription('');
      setIsAddingProvider(false);
      addNotification({
        title: t.notifProviderAdded,
        message: t.notifProviderAddedMsg(provName.trim(), provStrategy),
        type: 'success',
      });
      await loadProviders();
    } catch {
      addNotification({ title: t.error, message: t.notifIdpRegisterFailed, type: 'error' });
    }
  };

  const handleToggleProvider = async (provider: IdentityProvider) => {
    // In Local mode, IdPs must remain inactive
    if (!provider.isActive && !useExternalIdp) {
      addNotification({
        title: 'Activación no permitida',
        message: 'Los IDPs no pueden activarse mientras el modo de autenticación sea Local.',
        type: 'warning',
      });
      return;
    }

    try {
      if (provider.isActive) {
        await idpService.deactivateIdentityProvider(tenantId, provider.identityProviderId);
      } else {
        // Enforce the rule: "pudiendo ser varios pero uno solo activo"
        // Deactivate all other currently active IDPs first
        const activeProviders = providers.filter(
          p => p.isActive && p.identityProviderId !== provider.identityProviderId
        );
        for (const activeP of activeProviders) {
          await idpService.deactivateIdentityProvider(tenantId, activeP.identityProviderId);
        }
        await idpService.activateIdentityProvider(tenantId, provider.identityProviderId);
      }
      addNotification({
        title: t.notifProviderModified,
        message: t.notifProviderModifiedMsg,
        type: 'info',
      });
      await loadProviders();
    } catch {
      addNotification({ title: t.error, message: t.notifIdpToggleFailed, type: 'error' });
    }
  };

  const handleRemoveProvider = async (providerId: string) => {
    try {
      await idpService.removeIdentityProvider(tenantId, providerId);
      addNotification({
        title: t.notifProviderRemoved,
        message: t.notifProviderRemovedMsg,
        type: 'warning',
      });
      await loadProviders();
    } catch {
      addNotification({ title: t.error, message: t.notifIdpRemoveFailed, type: 'error' });
    }
  };

  const filteredProviders = [...providers]
    .filter(p =>
      activeFilter === 'active' ? p.isActive : activeFilter === 'inactive' ? !p.isActive : true
    )
    .sort((a, b) => {
      const cmp =
        sortBy === 'name'
          ? a.name.localeCompare(b.name)
          : sortBy === 'strategy'
            ? a.strategy.localeCompare(b.strategy)
            : a.code.localeCompare(b.code);
      return sortOrder === 'asc' ? cmp : -cmp;
    });

  const nextMode = useExternalIdp ? 'Esquema Local (BCrypt)' : 'Proveedores de Identidad (IDPs)';
  const currentMode = useExternalIdp ? 'Proveedores de Identidad (IDPs)' : 'Esquema Local (BCrypt)';

  return (
    <div className="space-y-4">
      <M3Dialog
        open={showAuthModeConfirm}
        title="Cambiar Modo de Autenticación"
        icon={<AlertTriangle className="w-5 h-5" />}
        iconColor="bg-amber-500/15 text-amber-500"
        onScrimClick={() => setShowAuthModeConfirm(false)}
        actions={[
          {
            label: 'Cancelar',
            variant: 'outlined',
            onClick: () => setShowAuthModeConfirm(false),
          },
          {
            label: 'Confirmar Cambio',
            variant: 'filled',
            className: 'bg-m3-primary hover:bg-m3-primary/90 border-0',
            onClick: confirmToggleAuthMode,
          },
        ]}
      >
        <div className="space-y-3 text-sm text-m3-on-surface">
          <p>
            Está por cambiar el modo de autenticación de este inquilino de{' '}
            <span className="font-semibold">{currentMode}</span> a{' '}
            <span className="font-semibold text-m3-primary">{nextMode}</span>.
          </p>
          <p className="text-m3-secondary text-xs leading-relaxed">
            {useExternalIdp
              ? 'Al cambiar a modo local, los usuarios deberán autenticarse con sus credenciales internas (contraseña BCrypt). Los proveedores de identidad externos configurados quedarán inactivos.'
              : 'Al activar el modo IDP, los usuarios se autenticarán a través de los proveedores de identidad externos configurados (OIDC, SAML2, OAuth2). Se requiere al menos un proveedor de identidad activo para completar este cambio.'}
          </p>
          {!useExternalIdp && !providers.some(p => p.isActive) && (
            <p className="text-xs text-m3-error font-medium flex items-center gap-1.5">
              <ShieldAlert className="w-3.5 h-3.5 shrink-0" />
              No hay ningún IDP activo. Active al menos uno antes de cambiar el modo.
            </p>
          )}
          <p className="text-xs text-amber-600 dark:text-amber-400 font-medium">
            Este cambio afecta inmediatamente a todos los usuarios del inquilino.
          </p>
        </div>
      </M3Dialog>

      <SectionHeader title={t.identityProviders} subtitle={t.idpSubtitle} />

      {/* Selector de Modo de Autenticación */}
      <div className="p-4 rounded-2xl border border-m3-outline/20 bg-m3-surface-container-low flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4 transition-all duration-300">
        <div className="space-y-1">
          <h3 className="text-sm font-semibold text-m3-on-surface">Modo de Autenticación</h3>
          <p className="text-xs text-m3-secondary">
            {useExternalIdp
              ? 'El inquilino utiliza Proveedores de Identidad (IDPs) externos para la autenticación.'
              : 'El inquilino utiliza el esquema de autenticación local (BCrypt interna).'}
          </p>
        </div>
        <div className="flex items-center gap-3">
          <span
            className={`text-xs font-bold uppercase tracking-wider ${!useExternalIdp ? 'text-m3-primary' : 'text-m3-secondary/60'}`}
          >
            Local
          </span>
          <M3Switch
            checked={useExternalIdp}
            onChange={handleToggleAuthMode}
            id="auth-mode-toggle"
          />
          <span
            className={`text-xs font-bold uppercase tracking-wider ${useExternalIdp ? 'text-m3-primary' : 'text-m3-secondary/60'}`}
          >
            IDPs
          </span>
        </div>
      </div>

      {/* Informational banner when in local mode — IDPs can still be pre-configured */}
      {!useExternalIdp && (
        <div className="flex items-start gap-3 px-4 py-3 rounded-xl border border-amber-500/20 bg-amber-500/5 text-xs text-amber-700 dark:text-amber-400">
          <Cpu className="w-4 h-4 shrink-0 mt-0.5 text-amber-500" />
          <span>
            <span className="font-semibold">Modo Local activo.</span> Los IDPs configurados aquí no
            se usan para autenticación hasta activar el modo IDPs. Puede pre-configurarlos con
            anticipación.
          </span>
        </div>
      )}

      <>
        <ListToolbar
          viewMode={viewMode}
          onViewModeChange={setViewMode}
          filterOptions={[
            { label: 'Todos', value: 'all' },
            { label: 'Activos', value: 'active' },
            { label: 'Inactivos', value: 'inactive' },
          ]}
          activeFilter={activeFilter}
          onFilterChange={setActiveFilter}
          sortOptions={[
            { label: 'Nombre', value: 'name' },
            { label: 'Código', value: 'code' },
            { label: 'Protocolo', value: 'strategy' },
          ]}
          sortBy={sortBy}
          onSortByChange={setSortBy}
          sortOrder={sortOrder}
          onSortOrderToggle={() => setSortOrder(o => (o === 'asc' ? 'desc' : 'asc'))}
          itemCount={providers.length}
          itemLabel="proveedor"
          onAdd={() => setIsAddingProvider(true)}
        />

        <InlineAddForm
          isOpen={isAddingProvider}
          onToggle={setIsAddingProvider}
          onSubmit={handleAddProvider}
          addLabel="+"
          title={t.newProvider}
          cancelLabel={t.cancelEdit}
          submitLabel={t.saveProvider}
          triggerEmphasis="none"
        >
          <M3TextField
            label={t.providerName}
            required
            value={provName}
            onChange={e => setProvName(e.target.value)}
            placeholder="e.g. Okta SSO"
          />
          <M3TextField
            label={t.providerCode}
            value={provCode}
            onChange={e => setProvCode(e.target.value.toUpperCase())}
            placeholder="e.g. OKTA_SSO"
          />
          <M3Select
            label={t.protocolType}
            value={provStrategy}
            onChange={(e: React.ChangeEvent<HTMLSelectElement>) =>
              setProvStrategy(e.target.value as IdpStrategy)
            }
          >
            {IDP_STRATEGIES.map(s => (
              <option key={s} value={s}>
                {strategyLabels[s]}
              </option>
            ))}
          </M3Select>
          <M3TextField
            label={t.providerDescription}
            value={provDescription}
            onChange={e => setProvDescription(e.target.value)}
            placeholder="e.g. https://login.microsoftonline.com/tenant-id"
          />
        </InlineAddForm>

        <div className="space-y-2.5">
          {isLoading ? (
            <div className="py-8 text-center text-sm text-m3-secondary">{t.loading}</div>
          ) : filteredProviders.length === 0 ? (
            <EmptyState icon={<Key className="w-5 h-5 text-m3-outline" />} message={t.noIdps} />
          ) : (
            filteredProviders.map(p =>
              edit.isEditing(p.identityProviderId) ? (
                <div
                  key={p.identityProviderId}
                  className="p-3.5 rounded-xl border border-m3-primary/30 bg-m3-surface-container/50 animate-fadeIn"
                >
                  <div className="flex items-center justify-between mb-2.5 pb-1.5 border-b border-m3-outline/10">
                    <span className="text-[10px] font-semibold uppercase tracking-wider text-m3-primary flex items-center gap-1">
                      <Pencil className="w-2.5 h-2.5" /> {t.editProvider}
                    </span>
                    <button
                      type="button"
                      onClick={edit.cancelEdit}
                      className="p-0.5 rounded text-m3-secondary/60 hover:text-m3-primary hover:bg-m3-primary/10 transition-colors"
                    >
                      <X className="w-3 h-3" />
                    </button>
                  </div>

                  <div className="space-y-3">
                    <M3TextField
                      label={t.providerName}
                      required
                      value={edit.draft.name ?? ''}
                      onChange={e => edit.setField('name', e.target.value)}
                      compact
                    />
                    <M3TextField
                      label={t.providerCode}
                      value={edit.draft.code ?? ''}
                      onChange={e => edit.setField('code', e.target.value.toUpperCase())}
                      compact
                    />
                    <M3Select
                      label={t.protocolType}
                      value={edit.draft.strategy ?? 'OIDC'}
                      onChange={(e: React.ChangeEvent<HTMLSelectElement>) =>
                        edit.setField('strategy', e.target.value)
                      }
                      compact
                    >
                      <option value="OIDC">{t.strategyOIDC}</option>
                      <option value="SAML2">{t.strategySAML2}</option>
                      <option value="OAuth2">{t.strategyOAuth2}</option>
                    </M3Select>
                    <M3TextField
                      label={t.providerDescription}
                      value={edit.draft.description ?? ''}
                      onChange={e => edit.setField('description', e.target.value)}
                      compact
                    />
                  </div>

                  <div className="flex justify-end gap-2 mt-2.5 pt-2 border-t border-m3-outline/10">
                    <button
                      type="button"
                      onClick={saveProviderEdit}
                      className="h-7 px-4 rounded-full bg-m3-primary text-white text-[10px] font-medium flex items-center justify-center gap-1.5 hover:bg-m3-primary/90 transition-colors"
                    >
                      <Save className="w-2.5 h-2.5" /> {t.saveBtn}
                    </button>
                    <button
                      type="button"
                      onClick={edit.cancelEdit}
                      className="h-7 px-3 rounded-full border border-m3-outline/30 text-m3-secondary text-[10px] font-medium hover:bg-m3-surface-variant transition-colors"
                    >
                      {t.cancelEdit}
                    </button>
                  </div>
                </div>
              ) : (
                <EntityRow
                  key={p.identityProviderId}
                  id={p.identityProviderId}
                  isActive={p.isActive}
                  trailing={
                    <>
                      {/* Edición de IdP retirada (G-157): no había endpoint/servicio de update →
                          «guardar» era un falso éxito. La edición real queda diferida a la ola de
                          federación. Se conservan crear / activar / desactivar / eliminar. */}
                      <Tooltip
                        content={
                          !p.isActive && !useExternalIdp
                            ? 'Activación bloqueada en modo Local'
                            : p.isActive
                              ? t.deactivate
                              : t.reactivate
                        }
                      >
                        <button
                          onClick={() => handleToggleProvider(p)}
                          disabled={!p.isActive && !useExternalIdp}
                          className={`p-1.5 rounded-full transition-all border ${
                            !p.isActive && !useExternalIdp
                              ? 'bg-m3-surface-variant border-m3-outline/20 text-m3-outline cursor-not-allowed opacity-50'
                              : p.isActive
                                ? 'bg-emerald-500/10 border-emerald-500/20 text-emerald-500 hover:bg-emerald-500/20'
                                : 'bg-rose-500/10 border-rose-500/20 text-rose-500 hover:bg-rose-500/20'
                          }`}
                        >
                          <Check className="w-3.5 h-3.5" />
                        </button>
                      </Tooltip>
                      <IconButton
                        tooltip={t.removeLocation}
                        onClick={() => handleRemoveProvider(p.identityProviderId)}
                        className="hover:text-m3-error hover:bg-m3-error/10"
                      >
                        <Trash2 className="w-3.5 h-3.5" />
                      </IconButton>
                    </>
                  }
                >
                  <div className="flex items-center gap-2">
                    <span className="font-medium text-sm text-m3-on-surface flex-1 min-w-0 truncate">
                      {p.name}
                    </span>
                    <div className="flex items-center gap-1.5 flex-shrink-0">
                      {p.code && <CodeBadge code={p.code} size="xs" />}
                      <span className="text-[10px] font-medium px-1.5 py-0.5 rounded bg-m3-primary/10 text-m3-primary font-mono">
                        {p.strategy}
                      </span>
                    </div>
                  </div>
                  {p.description && (
                    <p className="text-xs font-mono text-m3-secondary truncate">{p.description}</p>
                  )}
                </EntityRow>
              )
            )
          )}
        </div>
      </>
    </div>
  );
};
