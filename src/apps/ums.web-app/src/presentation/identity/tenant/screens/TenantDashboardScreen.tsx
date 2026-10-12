import React from 'react';
import { useI18n } from '@app/i18n/use-i18n';
import { useTenantDashboard } from '@app/identity/hooks/use-tenant-dashboard';
import { useAuthStore } from '@app/stores/auth.store';
import { TenantForm } from '../components/TenantForm';
import { TenantDetailPanel } from '../components/TenantDetailPanel';
import { TenantListPanel } from '../components/TenantListPanel';
import { TenantSignupRequestsPanel } from '../components/TenantSignupRequestsPanel';
import { PageShell } from '@shared/layouts/PageShell';
import { MasterDetailLayout } from '@shared/layouts/MasterDetailLayout';
import { M3Dialog } from '@shared/components/M3Dialog';
import { SortOption, FilterOption, QueryCriteriaOption } from '@shared/components/M3DataView';
import { Building2 } from 'lucide-react';

import { useAccessResolution } from '@app/authorization/hooks/use-access-resolution';
export default function TenantDashboardScreen(): React.JSX.Element {
  const t = useI18n();
  const dashboard = useTenantDashboard();
  const { hasOptionAccess } = useAccessResolution();
  const canCreate = hasOptionAccess('TENANTS_LIST', 'MANAGE_TENANTS');
  const isInternalAdmin = useAuthStore(state => state.user?.isInternalAdmin);
  const ownTenantId = useAuthStore(state => state.user?.tenantId);
  const ownTenantCode = useAuthStore(state => state.user?.tenantCode);

  const criteriaOptions: QueryCriteriaOption[] = [
    { label: t.byName, value: 'name' },
    { label: t.byCode, value: 'code' },
    { label: t.byTenantId, value: 'id' },
  ];
  const filterOptions: FilterOption[] = [
    { label: t.allStatuses, value: 'all' },
    { label: t.active, value: 'Active' },
    { label: t.suspended, value: 'Suspended' },
  ];
  const sortOptions: SortOption[] = [
    { label: t.sortByName, value: 'name' },
    { label: t.sortByCode, value: 'code' },
    { label: t.sortByStatus, value: 'status' },
  ];

  return (
    <PageShell>
      <div className="flex h-full min-h-0 flex-col gap-4">
        {isInternalAdmin && <TenantSignupRequestsPanel />}

        {ownTenantId && ownTenantCode && (
          <div className="flex justify-end">
            {(() => {
              // «Mi Organización» como filtro rápido en toggle: un clic filtra la lista a
              // la organización propia y la selecciona; otro clic limpia el filtro y vuelve
              // a mostrar todos los tenants. Patrón estándar de filtro reversible con estado visible.
              const applied = dashboard.queryState.appliedQuery;
              const isOwnOrgActive = applied.criteria === 'code' && applied.term === ownTenantCode;
              return (
                <button
                  type="button"
                  aria-pressed={isOwnOrgActive}
                  onClick={() =>
                    isOwnOrgActive
                      ? dashboard.queryState.handleResetQuery()
                      : dashboard.handleSelectOwnTenant(ownTenantId, ownTenantCode)
                  }
                  className={`h-8 px-3.5 rounded-full border text-[11px] font-medium flex items-center gap-1.5 transition-colors ${
                    isOwnOrgActive
                      ? 'border-m3-primary bg-m3-primary text-white hover:bg-m3-primary/90'
                      : 'border-m3-primary/30 text-m3-primary hover:bg-m3-primary/10'
                  }`}
                >
                  <Building2 className="w-3.5 h-3.5" />
                  {isOwnOrgActive ? 'Mi Organización · ver todos' : 'Mi Organización'}
                </button>
              );
            })()}
          </div>
        )}

        <MasterDetailLayout
          splitterLabel="Resize tenant detail panel"
          overlay={
            <>
              <M3Dialog
                open={dashboard.showDiscardDialog}
                title={t.unsavedChanges}
                message={t.unsavedChangesMsg}
                onScrimClick={() => dashboard.setShowDiscardDialog(false)}
                actions={[
                  {
                    label: t.cancelEdit,
                    variant: 'outlined',
                    onClick: () => dashboard.setShowDiscardDialog(false),
                  },
                  {
                    label: t.discardChanges,
                    variant: 'filled',
                    className: 'bg-m3-error hover:bg-m3-error/90 border-0',
                    onClick: dashboard.confirmDiscard,
                  },
                ]}
              />
              <TenantForm
                isOpen={dashboard.isCreateOpen}
                onClose={() => dashboard.setIsCreateOpen(false)}
                onSuccess={dashboard.handleCreateSuccess}
              />
            </>
          }
          master={
            <TenantListPanel
              tenants={dashboard.knownTenants}
              selectedId={dashboard.selectedId}
              isLoading={dashboard.isLoadingList}
              error={dashboard.listError}
              viewMode={dashboard.viewMode}
              onViewModeChange={dashboard.setViewMode}
              queryState={dashboard.queryState}
              paginationState={{
                ...dashboard.paginationState,
                totalItems: dashboard.totalItems,
                totalPages: dashboard.totalPages,
              }}
              onRegisterNew={() => dashboard.setIsCreateOpen(true)}
              onAddDisabled={!canCreate}
              onSelectTenant={dashboard.handleSelectTenant}
              criteriaOptions={criteriaOptions}
              filterOptions={filterOptions}
              sortOptions={sortOptions}
              requiresFilter={dashboard.requiresFilter}
            />
          }
          detail={
            <TenantDetailPanel
              selectedId={dashboard.selectedId}
              activeTenant={dashboard.activeTenant}
              parentTenant={dashboard.parentTenant}
              isRootTenant={dashboard.isRootTenant}
              isLoading={dashboard.isLoadingList}
              activeConsoleTab={dashboard.activeConsoleTab}
              consoleTabs={dashboard.consoleTabs}
              onConsoleTabChange={dashboard.setActiveConsoleTab}
              onTenantUpdate={dashboard.patchTenant}
              onTenantEditingChange={dashboard.setIsTenantEditing}
            />
          }
        />
      </div>
    </PageShell>
  );
}
