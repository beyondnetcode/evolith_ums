import React, { useState } from 'react';
import {
  Shield,
  ShieldAlert,
  ShieldCheck,
  ToggleLeft,
  ToggleRight,
  User,
  Key,
  Globe,
  Info,
  GitCompare,
  ChevronDown,
  ChevronRight,
} from 'lucide-react';
import {
  type Profile,
  type ProfilePermission as ProfilePermissionType,
} from '@domain/authorization/schemas/profile.schema';
import { M3SegmentedButton } from '@shared/components/M3SegmentedButton';
import { M3Button, M3Skeleton, M3SkeletonRow, M3Tabs } from '@shared/components';
import { DetailPanelShell } from '@shared/components';
import { LayoutGrid, Database, Zap } from 'lucide-react';
import {
  useOverrideProfilePermission,
  useActivateProfilePermission,
  useDeactivateProfilePermission,
  useActivateProfile,
  useDeactivateProfile,
} from '@app/authorization/hooks/use-profile';
import { useGetSystemSuite } from '@app/authorization/hooks/use-system-suite';
import { ProfileModulePermissionsPanel } from './tree/ProfileModulePermissionsPanel';
import { ProfileDomainResourcesPanel } from './tree/ProfileDomainResourcesPanel';
import { ProfileSystemActionsPanel } from './tree/ProfileSystemActionsPanel';
import {
  getAscendantIds,
  getSiblingViewOptions,
} from '../../../../application/authorization/utils/permission-cascade';
import { useNotificationStore } from '@app/stores/notification.store';

interface Props {
  profile: Profile | null;
  isLoading: boolean;
}

type DetailTab = 'overview' | 'permissions';

const getEffectLabel = (isAllowed: boolean, isDenied: boolean): string => {
  if (isAllowed) return 'Allow';
  if (isDenied) return 'Deny';
  return 'Neutral';
};

const getEffectColor = (isAllowed: boolean, isDenied: boolean): string => {
  if (isAllowed) return 'text-emerald-500 bg-emerald-500/10 border-emerald-500/20';
  if (isDenied) return 'text-rose-500 bg-rose-500/10 border-rose-500/20';
  return 'text-m3-outline bg-m3-outline/10 border-m3-outline/20';
};

export const ProfileDetailPanel: React.FC<Props> = ({ profile, isLoading }) => {
  const [activeTab, setActiveTab] = useState<DetailTab>('overview');
  const [expandedPermissions, setExpandedPermissions] = useState<Set<string>>(new Set());

  const overrideMutation = useOverrideProfilePermission();
  const activatePermMutation = useActivateProfilePermission();
  const deactivatePermMutation = useDeactivateProfilePermission();
  const activateProfileMutation = useActivateProfile();
  const deactivateProfileMutation = useDeactivateProfile();
  const { data: suite, isLoading: loadingSuite } = useGetSystemSuite(
    profile?.systemSuiteId ?? null
  );
  const addNotification = useNotificationStore(s => s.addNotification);

  const renderInlineActions = (node: { id: string }) => {
    if (!profile) return null;
    const p = profile.permissions.find(perm => perm.targetId === node.id);
    if (!p) return null;
    return renderPermissionActions(p);
  };

  const renderPermissionActions = (p: ProfilePermissionType | undefined) => {
    if (!p) return null;
    return (
      <div className="flex items-center gap-4 ml-4">
        <div className="flex items-center bg-m3-surface-container/60 rounded-lg p-0.5 border border-m3-outline/20">
          <button
            type="button"
            onClick={e => {
              e.stopPropagation();
              handleChangePermissionEffect(p.permissionId, 'allow');
            }}
            disabled={overrideMutation.isPending}
            className={`px-2 py-1 rounded-md text-[10px] font-semibold transition-all ${p.isAllowed && !p.isDenied ? 'bg-emerald-500 text-white shadow-sm' : 'text-m3-on-surface/60 hover:text-m3-on-surface hover:bg-m3-surface/50'}`}
          >
            <ShieldCheck className="w-3 h-3 inline mr-0.5" />
            Allow
          </button>
          <button
            type="button"
            onClick={e => {
              e.stopPropagation();
              handleChangePermissionEffect(p.permissionId, 'neutral');
            }}
            disabled={overrideMutation.isPending}
            className={`px-2 py-1 rounded-md text-[10px] font-semibold transition-all ${!p.isAllowed && !p.isDenied ? 'bg-m3-outline/30 text-m3-on-surface shadow-sm' : 'text-m3-on-surface/60 hover:text-m3-on-surface hover:bg-m3-surface/50'}`}
          >
            <Shield className="w-3 h-3 inline mr-0.5" />
            Neutral
          </button>
          <button
            type="button"
            onClick={e => {
              e.stopPropagation();
              handleChangePermissionEffect(p.permissionId, 'deny');
            }}
            disabled={overrideMutation.isPending}
            className={`px-2 py-1 rounded-md text-[10px] font-semibold transition-all ${!p.isAllowed && p.isDenied ? 'bg-rose-500 text-white shadow-sm' : 'text-m3-on-surface/60 hover:text-m3-on-surface hover:bg-m3-surface/50'}`}
          >
            <ShieldAlert className="w-3 h-3 inline mr-0.5" />
            Deny
          </button>
        </div>

        <div className="flex items-center gap-1.5 border-l border-m3-outline/20 pl-4">
          <button
            type="button"
            onClick={e => {
              e.stopPropagation();
              handleTogglePermissionActive(p.permissionId, p.isActive);
            }}
            disabled={activatePermMutation.isPending || deactivatePermMutation.isPending}
            className="text-m3-primary hover:opacity-85 transition-opacity focus:outline-none"
          >
            {p.isActive ? (
              <ToggleRight className="w-6 h-6 text-emerald-500 fill-emerald-500/20" />
            ) : (
              <ToggleLeft className="w-6 h-6 text-m3-outline" />
            )}
          </button>
        </div>
      </div>
    );
  };

  if (isLoading) {
    return (
      <DetailPanelShell title="Cargando perfil..." subtitle="Recuperando estado efectivo">
        <div className="flex flex-col space-y-6 p-4">
          <div className="flex justify-center mb-2">
            <M3Skeleton variant="rectangular" width={240} height={32} className="rounded-full" />
          </div>
          <div className="space-y-4">
            <M3SkeletonRow />
            <M3SkeletonRow />
            <M3SkeletonRow />
            <M3SkeletonRow />
          </div>
        </div>
      </DetailPanelShell>
    );
  }

  if (!profile) {
    return (
      <div className="h-full flex flex-col items-center justify-center p-6 text-center text-m3-on-surface/40">
        <User className="w-12 h-12 mb-3 stroke-[1.5] text-m3-primary/30" />
        <h3 className="text-[12px] font-medium text-m3-on-surface/70">
          Ningún perfil seleccionado
        </h3>
        <p className="text-[12px] max-w-xs mt-1">
          Seleccione un perfil del panel izquierdo para visualizar su configuración y grafo de
          autorización.
        </p>
      </div>
    );
  }

  const handleToggleProfileStatus = async () => {
    if (profile.isActive) {
      await deactivateProfileMutation.mutateAsync(profile.profileId);
    } else {
      await activateProfileMutation.mutateAsync(profile.profileId);
    }
  };

  const handleTogglePermissionActive = async (permissionId: string, currentActive: boolean) => {
    if (currentActive) {
      await deactivatePermMutation.mutateAsync({ profileId: profile.profileId, permissionId });
    } else {
      await activatePermMutation.mutateAsync({ profileId: profile.profileId, permissionId });
    }
  };

  const handleChangePermissionEffect = async (
    permissionId: string,
    newEffect: 'allow' | 'deny' | 'neutral'
  ) => {
    if (!profile) return;
    const targetPerm = profile.permissions.find(p => p.permissionId === permissionId);

    await overrideMutation.mutateAsync({
      profileId: profile.profileId,
      permissionId,
      effect: newEffect,
    });

    if (newEffect === 'allow' && targetPerm && suite) {
      const ascendantIds = getAscendantIds(suite, targetPerm.targetId);
      const siblingViews = getSiblingViewOptions(suite, targetPerm.targetId);
      let changedParents = false;
      const idsToProcess = [...ascendantIds, ...siblingViews.map(s => s.id)];
      for (const ascendantId of idsToProcess) {
        const parentPerm = profile.permissions.find(p => p.targetId === ascendantId);
        if (parentPerm && !parentPerm.isAllowed) {
          await overrideMutation.mutateAsync({
            profileId: profile.profileId,
            permissionId: parentPerm.permissionId,
            effect: 'allow',
          });
          changedParents = true;
        }
      }
      if (changedParents) {
        addNotification({
          title: 'Jerarquía actualizada',
          message:
            'Se habilitaron automáticamente los niveles superiores por consistencia jerárquica.',
          type: 'info',
        });
      }
    }
  };

  const toggleExpanded = (permissionId: string) => {
    setExpandedPermissions(prev => {
      const next = new Set(prev);
      if (next.has(permissionId)) {
        next.delete(permissionId);
      } else {
        next.add(permissionId);
      }
      return next;
    });
  };

  const hasChanges = (p: ProfilePermissionType): boolean => {
    if (!p.originalFromTemplate) return false;
    return (
      p.isOverride &&
      (p.originalFromTemplate.isAllowed !== p.isAllowed ||
        p.originalFromTemplate.isDenied !== p.isDenied ||
        p.originalFromTemplate.isActive !== p.isActive)
    );
  };

  const renderPermissionRow = (p: ProfilePermissionType) => {
    const isExpanded = expandedPermissions.has(p.permissionId);
    const showDiff = !!p.originalFromTemplate;
    const hasChangesFlag = hasChanges(p);

    return (
      <div key={p.permissionId} className="divide-y divide-m3-outline/5">
        <div
          className={`flex flex-col md:flex-row md:items-center justify-between gap-3 p-3 text-[11px] transition-colors ${hasChangesFlag ? 'bg-amber-500/5 hover:bg-amber-500/10' : 'hover:bg-m3-surface-container/20'}`}
        >
          <div className="flex items-start gap-2">
            {showDiff && (
              <button
                type="button"
                onClick={() => toggleExpanded(p.permissionId)}
                className="mt-0.5 text-m3-secondary hover:text-m3-primary transition-colors"
              >
                {isExpanded ? (
                  <ChevronDown className="w-4 h-4" />
                ) : (
                  <ChevronRight className="w-4 h-4" />
                )}
              </button>
            )}
            <div className="space-y-0.5">
              <div className="flex items-center gap-1.5 flex-wrap">
                <span className="font-bold text-m3-on-surface">
                  {p.targetName && p.targetName !== '-' && p.targetName !== '—'
                    ? p.targetName
                    : p.targetType === 'SystemSuite'
                      ? suite?.name
                      : `(No definido)`}
                </span>
                <span className="text-[10px] uppercase font-semibold text-m3-primary bg-m3-primary/10 px-2 py-0.5 rounded-full border border-m3-primary/20">
                  {p.targetType}
                </span>
                {p.isOverride && (
                  <span className="text-[10px] uppercase font-semibold text-amber-500 bg-amber-500/10 px-2 py-0.5 rounded-full border border-amber-500/20">
                    Override
                  </span>
                )}
                {showDiff && hasChangesFlag && (
                  <span className="text-[10px] uppercase font-semibold text-cyan-600 bg-cyan-600/10 px-2 py-0.5 rounded-full border border-cyan-600/20 flex items-center gap-0.5">
                    <GitCompare className="w-3 h-3" />
                    Diff
                  </span>
                )}
              </div>
              {p.actionName && p.actionName !== '—' && p.actionName !== '-' && (
                <div className="text-[11px] text-m3-on-surface/60">
                  Acción: <span className="font-medium text-m3-secondary">{p.actionName}</span>
                </div>
              )}
            </div>
          </div>
        </div>
        {isExpanded && p.originalFromTemplate && (
          <div className="bg-m3-surface-container/5 p-3 pl-8 space-y-2">
            <div className="text-[10px] uppercase font-bold text-m3-secondary/60 mb-2">
              Comparación: Original vs Actual
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
              <div
                className={`rounded-lg p-2.5 border ${getEffectColor(p.originalFromTemplate.isAllowed, p.originalFromTemplate.isDenied)}`}
              >
                <div className="text-[10px] uppercase tracking-wider font-medium mb-1 opacity-70">
                  Original (Plantilla)
                </div>
                <div className="text-[12px] font-medium">
                  {getEffectLabel(
                    p.originalFromTemplate.isAllowed,
                    p.originalFromTemplate.isDenied
                  )}
                </div>
                <div className="text-[11px] mt-1 opacity-70">
                  Activo: {p.originalFromTemplate.isActive ? 'Sí' : 'No'}
                </div>
              </div>

              <div className={`rounded-lg p-2.5 border ${getEffectColor(p.isAllowed, p.isDenied)}`}>
                <div className="text-[10px] uppercase tracking-wider font-medium mb-1 opacity-70">
                  Actual (Perfil)
                </div>
                <div className="text-[12px] font-medium">
                  {getEffectLabel(p.isAllowed, p.isDenied)}
                </div>
                <div className="text-[11px] mt-1 opacity-70">
                  Activo: {p.isActive ? 'Sí' : 'No'}
                </div>
              </div>
            </div>

            {(p.originalFromTemplate.isAllowed !== p.isAllowed ||
              p.originalFromTemplate.isDenied !== p.isDenied ||
              p.originalFromTemplate.isActive !== p.isActive) && (
              <div className="text-[10px] text-amber-600 bg-amber-500/10 rounded px-2 py-1">
                Este permiso fue modificado respecto a su configuración original en la plantilla.
              </div>
            )}
          </div>
        )}
      </div>
    );
  };

  return (
    <DetailPanelShell
      title={`Perfil Efectivo`}
      subtitle={`${profile.systemSuiteCode} · ${profile.tenantCode} · ${profile.roleCode} · ${profile.scope}`}
      headerActions={
        <M3Button
          type="button"
          variant={profile.isActive ? 'tonal' : 'filled'}
          onClick={handleToggleProfileStatus}
          disabled={activateProfileMutation.isPending || deactivateProfileMutation.isPending}
        >
          {profile.isActive ? 'Desactivar Perfil' : 'Activar Perfil'}
        </M3Button>
      }
    >
      <div className="h-full flex flex-col">
        {/* Navigation Tabs using Segmented Buttons */}
        <div className="flex justify-center my-3 px-4">
          <M3SegmentedButton
            options={[
              { value: 'overview', label: 'Resumen' },
              { value: 'permissions', label: 'Permisos Efectivos' },
            ]}
            value={activeTab}
            onChange={id => setActiveTab(id as DetailTab)}
          />
        </div>

        <div className="flex-1 overflow-y-auto px-4 pb-6">
          {activeTab === 'overview' && (
            <div className="space-y-4">
              {/* Profile Overview Card */}
              <div className="rounded-xl border border-m3-outline/10 bg-m3-surface-container/20 p-4 space-y-3">
                <div className="flex items-center gap-3">
                  <div className="p-2 rounded-lg bg-m3-primary/10 text-m3-primary">
                    <User className="w-5 h-5" />
                  </div>
                  <div className="flex-1">
                    <span className="text-[11px] uppercase tracking-wider font-medium text-m3-secondary/50">
                      Usuario
                    </span>
                    <p className="text-[12px] font-medium text-m3-on-surface">
                      {profile.userEmail}
                    </p>
                  </div>
                </div>

                <div className="flex items-center gap-3">
                  <div className="p-2 rounded-lg bg-m3-primary/10 text-m3-primary">
                    <Key className="w-5 h-5" />
                  </div>
                  <div className="flex-1">
                    <span className="text-[11px] uppercase tracking-wider font-medium text-m3-secondary/50">
                      Rol Asociado
                    </span>
                    <p className="text-[12px] font-medium text-m3-on-surface">
                      {profile.roleCode} — {profile.roleName}
                    </p>
                  </div>
                </div>

                <div className="flex items-center gap-3">
                  <div className="p-2 rounded-lg bg-m3-primary/10 text-m3-primary">
                    <LayoutGrid className="w-5 h-5" />
                  </div>
                  <div className="flex-1">
                    <span className="text-[11px] uppercase tracking-wider font-medium text-m3-secondary/50">
                      Sistema
                    </span>
                    <p className="text-[12px] font-medium text-m3-on-surface">
                      {profile.systemSuiteCode} — {profile.systemSuiteName}
                    </p>
                  </div>
                </div>

                <div className="flex items-center gap-3">
                  <div className="p-2 rounded-lg bg-m3-primary/10 text-m3-primary">
                    <Globe className="w-5 h-5" />
                  </div>
                  <div className="flex-1">
                    <span className="text-[11px] uppercase tracking-wider font-medium text-m3-secondary/50">
                      Inquilino
                    </span>
                    <p className="text-[12px] font-medium text-m3-on-surface">
                      {profile.tenantCode} — {profile.tenantName}
                    </p>
                  </div>
                </div>

                <div className="flex items-center gap-3">
                  <div className="p-2 rounded-lg bg-m3-primary/10 text-m3-primary">
                    <Info className="w-5 h-5" />
                  </div>
                  <div className="flex-1">
                    <span className="text-[11px] uppercase tracking-wider font-medium text-m3-secondary/50">
                      Estado / Alcance
                    </span>
                    <p className="text-[12px] font-medium text-m3-on-surface">
                      {profile.isActive ? 'Activo' : 'Inactivo'} ·{' '}
                      <span className="font-medium text-m3-primary">{profile.scope}</span>
                    </p>
                  </div>
                </div>

                <div className="flex items-center gap-3">
                  <div className="p-2 rounded-lg bg-m3-primary/10 text-m3-primary">
                    <Shield className="w-5 h-5" />
                  </div>
                  <div className="flex-1">
                    <span className="text-[11px] uppercase tracking-wider font-medium text-m3-secondary/50">
                      Permisos Materializados
                    </span>
                    <p className="text-[12px] font-medium text-m3-on-surface">
                      {profile.permissionCount} permisos efectivos
                    </p>
                  </div>
                </div>
              </div>

              <div className="rounded-lg border border-amber-500/10 bg-amber-500/5 px-3 py-2 flex items-start gap-2">
                <span className="text-[10px] font-semibold text-amber-600 uppercase tracking-wide shrink-0 mt-0.5">
                  Auditor
                </span>
                <p className="text-[10px] text-amber-700/80 leading-relaxed">
                  Permisos materializados definitivos — fusión de plantilla base con overrides
                  manuales.
                </p>
              </div>
            </div>
          )}

          {activeTab === 'permissions' && (
            <div className="p-4 space-y-4">
              {profile.permissions.length === 0 ? (
                <div className="text-center py-10 text-[12px] text-m3-on-surface/50">
                  Ningún permiso materializado. Asocie una plantilla primero.
                </div>
              ) : (
                <div className="flex flex-col h-full min-h-[400px]">
                  <M3Tabs
                    tabs={[
                      {
                        id: 'modules',
                        label: 'Navegación',
                        icon: <LayoutGrid className="w-4 h-4" />,
                        content: (
                          <ProfileModulePermissionsPanel
                            suite={suite}
                            items={profile.permissions}
                            renderActions={renderInlineActions}
                            renderExpandedContent={node => {
                              const perm = profile.permissions.find(p => p.targetId === node.id);
                              // Solo renderizar el row expandido si hay diferencias (originalFromTemplate) o es override
                              if (perm && (perm.originalFromTemplate || perm.isOverride)) {
                                return (
                                  <div className="pl-6 border-l-2 border-m3-primary/20 ml-5 my-1">
                                    {renderPermissionRow(perm)}
                                  </div>
                                );
                              }
                              return null;
                            }}
                          />
                        ),
                      },
                      {
                        id: 'domain',
                        label: 'Recursos',
                        icon: <Database className="w-4 h-4" />,
                        content: (
                          <div className="h-[400px]">
                            <ProfileDomainResourcesPanel
                              suite={suite}
                              permissions={profile.permissions}
                              renderInlineActions={node =>
                                renderPermissionActions(node.permissions[0])
                              }
                            />
                          </div>
                        ),
                      },
                      {
                        id: 'system',
                        label: 'Acciones del Sistema',
                        icon: <Zap className="w-4 h-4" />,
                        content: (
                          <div className="h-[400px]">
                            <ProfileSystemActionsPanel
                              suite={suite}
                              permissions={profile.permissions}
                              renderInlineActions={p => renderPermissionActions(p)}
                            />
                          </div>
                        ),
                      },
                    ]}
                  />
                </div>
              )}
            </div>
          )}
        </div>
      </div>
    </DetailPanelShell>
  );
};
