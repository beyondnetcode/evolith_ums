import React from 'react';
import { Shield, MapPin, LayoutGrid, Info } from 'lucide-react';
import { useGetAllProfiles } from '@app/authorization/hooks/use-profile';
import { EmptyState } from '@shared/components/EmptyState';
import { StatusBadge } from '@shared/components';
import { CodeBadge } from '@shared/components/CodeBadge';

interface UserAccountAccessViewProps {
  userId: string;
  tenantId: string;
}

export const UserAccountAccessView: React.FC<UserAccountAccessViewProps> = ({
  userId,
  tenantId,
}) => {
  const { data: profilePage, isLoading } = useGetAllProfiles({
    page: 1,
    pageSize: 50,
    userId,
    tenantId,
  });

  const profiles = profilePage?.items ?? [];

  if (isLoading) {
    return <div className="py-8 text-center text-sm text-m3-secondary">Cargando perfiles...</div>;
  }

  if (profiles.length === 0) {
    return (
      <EmptyState
        icon={<Shield className="w-5 h-5 text-m3-outline" />}
        message="Sin perfiles de acceso asignados"
      />
    );
  }

  return (
    <div className="space-y-3 animate-fadeIn">
      <div className="flex items-start gap-2 px-1 pb-1">
        <Info className="w-3.5 h-3.5 text-m3-secondary/60 shrink-0 mt-0.5" />
        <span className="text-[11px] text-m3-secondary/70 leading-relaxed">
          Vista de consulta. Los permisos se administran desde el perfil de autorización
          correspondiente.
        </span>
      </div>

      {profiles.map(profile => (
        <div
          key={profile.profileId}
          className="p-3.5 rounded-xl border border-m3-outline/20 bg-m3-surface-container/5 space-y-2.5 hover:border-m3-outline/30 transition-colors"
        >
          {/* Rol + Estado */}
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2 flex-wrap">
              <Shield className="w-3.5 h-3.5 text-m3-primary shrink-0" />
              <span className="text-[12px] font-semibold text-m3-on-surface">
                {profile.roleName}
              </span>
              <CodeBadge code={profile.roleCode} size="xs" />
            </div>
            <StatusBadge status={profile.isActive ? 'Active' : 'Inactive'} />
          </div>

          {/* Sistema */}
          <div className="flex items-center gap-2 flex-wrap">
            <LayoutGrid className="w-3 h-3 text-m3-secondary shrink-0" />
            <span className="text-[11px] text-m3-secondary">Sistema:</span>
            <span className="text-[11px] font-medium text-m3-on-surface">
              {profile.systemSuiteName}
            </span>
            <span className="text-[10px] font-mono px-1.5 py-0.5 rounded bg-m3-primary/10 text-m3-primary">
              {profile.systemSuiteCode}
            </span>
          </div>

          {/* Sucursal + Alcance */}
          <div className="flex items-center gap-2 flex-wrap">
            <MapPin className="w-3 h-3 text-m3-secondary shrink-0" />
            <span className="text-[11px] text-m3-secondary">Sucursal:</span>
            <span className="text-[11px] font-medium text-m3-on-surface">
              {profile.branchName ?? 'Corporativo'}
            </span>
            <span className="text-[10px] text-m3-secondary/50">· {profile.scope}</span>
          </div>

          {/* Conteo de permisos */}
          {profile.permissionCount > 0 && (
            <div className="pt-1 border-t border-m3-outline/10">
              <span className="text-[10px] text-m3-secondary/60">
                {profile.permissionCount} permiso{profile.permissionCount !== 1 ? 's' : ''}{' '}
                configurados
              </span>
            </div>
          )}
        </div>
      ))}
    </div>
  );
};
