import React from 'react';
import { Building2, ArrowRight, ChevronRight } from 'lucide-react';
import { Tenant } from '@domain/identity/models/tenant.model';
import { M3Card } from '@shared/components/M3Card';
import { StatusBadge } from '@shared/components/StatusBadge';
import { CodeBadge } from '@shared/components/CodeBadge';
import { EntityRow } from '@shared/components/EntityRow';
import type { TreeNode } from '@app/hooks/use-tree-nodes';
import { useGetAllAppConfigurations } from '@app/configuration/hooks/use-app-configuration';

export const AuthModeBadge: React.FC<{ tenantId: string; tenantCode?: string }> = ({
  tenantId,
  tenantCode,
}) => {
  const code = tenantCode?.toUpperCase();
  const id = tenantId.toLowerCase();

  const isExternalIdp =
    code === 'RANSA_PERU' ||
    code === 'NEPTUNIA' ||
    code === 'PAITA_PORT' ||
    code === 'INTRADEVCO' ||
    id === '3fa85f64-5717-4562-b3fc-2c963f66afa6' ||
    id === 'c9b736b4-6a84-48f8-b34d-176bc5a6d542' ||
    id === '9e8d7c6b-5a4f-3e2d-1c0b-9876543210fe' ||
    id === 'f3e2d1c0-b9a8-7f6e-5d4c-321098765432';

  if (isExternalIdp) {
    return (
      <span className="inline-flex items-center text-[9px] font-bold px-2 py-0.5 rounded-full bg-emerald-500/10 text-emerald-600 dark:bg-emerald-500/20 dark:text-emerald-400 border border-emerald-500/20">
        IDP Mode
      </span>
    );
  }

  return (
    <span className="inline-flex items-center text-[9px] font-bold px-2 py-0.5 rounded-full bg-slate-500/10 text-slate-600 dark:bg-slate-500/20 dark:text-slate-400 border border-slate-500/20">
      Modo Local
    </span>
  );
};

export const isTenantRoot = (tenant: Tenant): boolean =>
  tenant.isManagementOwner === true && tenant.parentTenantId === null;

export const TenantRootBadge: React.FC = () => (
  <span className="inline-flex items-center text-[9px] font-bold px-2 py-0.5 rounded-full bg-amber-500/10 text-amber-600 dark:bg-amber-500/20 dark:text-amber-400 border border-amber-500/20">
    Tenant Root · Admin Root
  </span>
);

export const renderTenantParentRow = (
  node: TreeNode<Tenant>,
  isSelected: boolean,
  isExpanded: boolean,
  onToggle: () => void,
  onSelectTenant: (id: string) => void,
  statusLabel: (status: string) => string
) => {
  const id = node.item.tenantId;
  const tenant = node.item;
  const hasChildren = node.children.length > 0;

  return (
    <EntityRow
      key={id}
      id={id}
      isActive={tenant.status === 'Active'}
      selected={isSelected}
      onClick={() => onSelectTenant(id)}
      leading={
        <div className="flex items-center gap-2">
          {hasChildren ? (
            <button
              onClick={e => {
                e.stopPropagation();
                onToggle();
              }}
              className={`p-0.5 rounded transition-transform duration-200 ${isExpanded ? 'rotate-90' : ''}`}
            >
              <ChevronRight className="w-3.5 h-3.5 text-m3-secondary" />
            </button>
          ) : (
            <span className="w-4" />
          )}
          <div
            className={`p-2 rounded-lg transition-colors ${isSelected ? 'bg-m3-primary/15' : 'bg-m3-surface-container/50'}`}
          >
            <Building2
              className={`w-4 h-4 ${isSelected ? 'text-m3-primary' : 'text-m3-secondary'}`}
            />
          </div>
        </div>
      }
      trailingColumns={[
        { content: <CodeBadge code={tenant.code} />, width: 'w-20' },
        { content: <CodeBadge code={tenant.type} />, width: 'w-20' },
        {
          content: <StatusBadge status={tenant.status} label={statusLabel(tenant.status)} />,
          width: 'w-20',
        },
        {
          content: (
            <ArrowRight
              className={`w-4 h-4 transition-transform ${isSelected ? 'text-m3-primary translate-x-0.5' : 'text-m3-outline/30'}`}
            />
          ),
          width: 'w-5',
        },
      ]}
    >
      <div className="flex items-center gap-2 flex-wrap">
        <span className="text-[12px] font-medium text-m3-on-surface line-clamp-1">
          {tenant.name}
        </span>
        <AuthModeBadge tenantId={tenant.tenantId} tenantCode={tenant.code} />
        {isTenantRoot(tenant) && <TenantRootBadge />}
      </div>
      {tenant.companyReference && (
        <span className="text-[11px] text-m3-secondary/60 line-clamp-1 mt-0.5">
          {tenant.companyReference}
        </span>
      )}
    </EntityRow>
  );
};

export const renderTenantChildRow = (
  child: Tenant,
  isChildSelected: boolean,
  onSelectTenant: (id: string) => void,
  statusLabel: (status: string) => string
) => {
  return (
    <EntityRow
      key={child.tenantId}
      id={child.tenantId}
      isActive={child.status === 'Active'}
      selected={isChildSelected}
      onClick={() => onSelectTenant(child.tenantId)}
      className="ml-6 border-l-2 border-m3-outline/15 rounded-l-none"
      leading={
        <div className="flex items-center gap-2">
          <span className="w-4" />
          <div className="p-2 rounded-lg bg-m3-surface-container/40 border border-m3-outline/15 text-m3-secondary/70">
            <Building2 className="w-4 h-4" />
          </div>
        </div>
      }
      trailingColumns={[
        { content: <CodeBadge code={child.code} />, width: 'w-20' },
        { content: <CodeBadge code={child.type} />, width: 'w-20' },
        {
          content: <StatusBadge status={child.status} label={statusLabel(child.status)} />,
          width: 'w-20',
        },
        {
          content: (
            <ArrowRight
              className={`w-4 h-4 transition-transform ${isChildSelected ? 'text-m3-primary translate-x-0.5' : 'text-m3-outline/30'}`}
            />
          ),
          width: 'w-5',
        },
      ]}
    >
      <div className="flex items-center gap-2 flex-wrap">
        <span className="text-[12px] font-medium text-m3-on-surface line-clamp-1">
          {child.name}
        </span>
        <AuthModeBadge tenantId={child.tenantId} tenantCode={child.code} />
      </div>
      {child.companyReference && (
        <span className="text-[11px] text-m3-secondary/60 line-clamp-1 mt-0.5">
          {child.companyReference}
        </span>
      )}
    </EntityRow>
  );
};

export const renderTenantParentCard = (
  node: TreeNode<Tenant>,
  isSelected: boolean,
  _isExpanded: boolean,
  onToggle: () => void,
  onSelectTenant: (id: string) => void,
  statusLabel: (status: string) => string,
  t: Record<string, string>
) => {
  const id = node.item.tenantId;
  const tenant = node.item;
  const children = node.children;
  const hasChildren = children.length > 0;

  return (
    <M3Card
      key={id}
      onClick={() => onSelectTenant(id)}
      variant={isSelected ? 'elevated' : 'filled'}
      className={`p-5 cursor-pointer border transition-all duration-150 hover:-translate-y-0.5 hover:shadow-md ${
        isSelected
          ? 'border-m3-primary bg-m3-primary-container/15'
          : 'border-m3-outline/25 hover:border-m3-primary/30'
      }`}
    >
      <div className="flex justify-between items-start gap-4">
        <div className="flex gap-3 flex-1">
          <div
            className={`p-2.5 rounded-lg border ${isSelected ? 'bg-m3-primary text-white border-m3-primary' : 'bg-m3-primary/10 text-m3-primary border-m3-primary/10'}`}
          >
            <Building2 className="w-5 h-5" />
          </div>
          <div className="flex-1 min-w-0">
            <div className="flex items-center gap-2 flex-wrap">
              <h4 className="text-[12px] font-medium text-m3-on-surface line-clamp-1">
                {tenant.name}
              </h4>
              <AuthModeBadge tenantId={tenant.tenantId} tenantCode={tenant.code} />
              {isTenantRoot(tenant) && <TenantRootBadge />}
            </div>
            <p className="font-mono text-[11px] text-m3-secondary/70 mt-0.5">{tenant.code}</p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          <StatusBadge status={tenant.status} label={statusLabel(tenant.status)} />
          {hasChildren && (
            <button
              onClick={e => {
                e.stopPropagation();
                onToggle();
              }}
              className="p-1 rounded transition-transform duration-200"
            >
              <ChevronRight className="w-4 h-4 text-m3-secondary" />
            </button>
          )}
        </div>
      </div>
      <div className="mt-4 pt-3 border-t border-m3-outline/10 grid grid-cols-1 sm:grid-cols-2 gap-2 text-[11px]">
        <div>
          <p className="text-m3-secondary font-medium">{t.colCategory}</p>
          <p className="font-medium text-m3-on-surface mt-0.5">{tenant.type}</p>
        </div>
        <div>
          <p className="text-m3-secondary font-medium">{t.ref}</p>
          <p className="font-medium text-m3-on-surface truncate mt-0.5">
            {tenant.companyReference || '-'}
          </p>
        </div>
      </div>
      {hasChildren && (
        <div className="mt-2 text-[11px] text-m3-secondary/60">
          {children.length} {children.length === 1 ? t.tenant : t.tenants}
        </div>
      )}
    </M3Card>
  );
};

export const renderTenantChildCard = (
  child: Tenant,
  isChildSelected: boolean,
  onSelectTenant: (id: string) => void,
  statusLabel: (status: string) => string,
  t: Record<string, string>
) => (
  <M3Card
    key={child.tenantId}
    onClick={() => onSelectTenant(child.tenantId)}
    variant={isChildSelected ? 'elevated' : 'outlined'}
    className={`p-4 cursor-pointer border transition-all duration-150 ${
      isChildSelected
        ? 'border-m3-primary bg-m3-primary-container/10'
        : 'border-m3-outline/15 hover:border-m3-primary/20'
    }`}
  >
    <div className="flex justify-between items-start gap-3">
      <div className="flex gap-2.5">
        <div
          className={`p-2 rounded-lg border ${isChildSelected ? 'bg-m3-primary/80 text-white border-m3-primary/80' : 'bg-m3-surface-container/40 border-m3-outline/15 text-m3-secondary/70'}`}
        >
          <Building2 className="w-4 h-4" />
        </div>
        <div>
          <div className="flex items-center gap-2 flex-wrap">
            <h4 className="text-[12px] font-medium text-m3-on-surface">{child.name}</h4>
            <AuthModeBadge tenantId={child.tenantId} tenantCode={child.code} />
          </div>
          <p className="font-mono text-[11px] text-m3-secondary/60 mt-0.5">{child.code}</p>
        </div>
      </div>
      <StatusBadge status={child.status} label={statusLabel(child.status)} />
    </div>
    <div className="mt-3 pt-2 border-t border-m3-outline/10 grid grid-cols-1 sm:grid-cols-2 gap-2 text-[11px]">
      <div>
        <p className="text-m3-secondary font-medium">{t.colCategory}</p>
        <p className="font-medium text-m3-on-surface mt-0.5">{child.type}</p>
      </div>
      <div>
        <p className="text-m3-secondary font-medium">{t.ref}</p>
        <p className="font-medium text-m3-on-surface truncate mt-0.5">
          {child.companyReference || '-'}
        </p>
      </div>
    </div>
  </M3Card>
);
