import React, { useState } from 'react';
import { useAuthStore } from '@app/stores/auth.store';
import { useGetAllProfiles, useGetProfile } from '@app/authorization/hooks/use-profile';
import { useQueryState } from '@app/shared/hooks/use-query-state';
import { usePaginationState } from '@app/shared/hooks/use-pagination-state';
import { ProfileListPanel } from '../components/ProfileListPanel';
import { ProfileDetailPanel } from '../components/ProfileDetailPanel';
import { ProfileForm } from '../components/ProfileForm';
import { ProfileRequestsInboxPanel } from '../components/ProfileRequestsInboxPanel';
import { M3AuthorizationGraphPanel } from '../components/M3AuthorizationGraphPanel';
import { PageShell } from '@shared/layouts/PageShell';
import { MasterDetailLayout } from '@shared/layouts/MasterDetailLayout';

export default function ProfileDashboardScreen(): React.JSX.Element {
  const [selectedId, setSelectedId] = useState('');
  const [viewMode, setViewMode] = useState<'list' | 'thumbnail'>('list');

  const sessionTenantId = useAuthStore(state => state.user?.tenantId);

  const queryState = useQueryState({
    criteria: 'user',
    filter: 'all',
    sortBy: 'user',
    // Patrón estándar: la lista carga al entrar (no exige aplicar un filtro primero).
    appliedFilter: true,
  });

  const paginationState = usePaginationState({
    initialPageSize: 10,
  });

  const [isCreateOpen, setIsCreateOpen] = useState(false);
  const [isGraphOpen, setIsGraphOpen] = useState(false);
  const [graphProfileId, setGraphProfileId] = useState('');

  const shouldFetch = queryState.appliedQuery.filterApplied;

  const {
    data: pageData,
    isLoading: loadingList,
    error: listError,
  } = useGetAllProfiles(
    shouldFetch
      ? {
          page: paginationState.page,
          pageSize: paginationState.pageSize,
          search: queryState.appliedQuery.term || undefined,
          criteria: queryState.appliedQuery.criteria,
          status: queryState.activeFilter,
          sortBy: queryState.sortBy,
          sortOrder: queryState.sortOrder,
          tenantId: sessionTenantId,
        }
      : null
  );

  // Fetch detail for selected profile
  const { data: activeProfile, isLoading: loadingDetail } = useGetProfile(selectedId || null);

  const handleOpenGraph = (profileId: string) => {
    setGraphProfileId(profileId);
    setIsGraphOpen(true);
  };

  return (
    <PageShell>
      <ProfileForm
        isOpen={isCreateOpen}
        onClose={() => setIsCreateOpen(false)}
        onSuccess={profileId => {
          setIsCreateOpen(false);
          setSelectedId(profileId);
        }}
      />

      <M3AuthorizationGraphPanel
        isOpen={isGraphOpen}
        onClose={() => {
          setIsGraphOpen(false);
          setGraphProfileId('');
        }}
        profileId={graphProfileId}
      />

      <div className="flex h-full min-h-0 flex-col gap-4">
        <ProfileRequestsInboxPanel />
        <MasterDetailLayout
          splitterLabel="Resize profile detail panel"
          master={
            <ProfileListPanel
              profiles={pageData?.items ?? []}
              selectedId={selectedId}
              isLoading={loadingList}
              error={listError}
              viewMode={viewMode}
              onViewModeChange={setViewMode}
              queryState={queryState}
              paginationState={{
                ...paginationState,
                totalItems: pageData?.totalItems ?? 0,
                totalPages: pageData?.totalPages ?? 0,
              }}
              onRegisterNew={() => setIsCreateOpen(true)}
              onSelectProfile={setSelectedId}
              onOpenGraph={handleOpenGraph}
              requiresFilter={!shouldFetch}
            />
          }
          detail={<ProfileDetailPanel profile={activeProfile} isLoading={loadingDetail} />}
        />
      </div>
    </PageShell>
  );
}
