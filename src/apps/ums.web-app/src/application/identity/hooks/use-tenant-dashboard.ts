/**
 * useTenantDashboard.ts — Orchestrates state and API calls for TenantDashboardScreen.
 *
 * H-2: Extracted control state, selection logic, and API wiring from the screen component.
 */
import React, { useState, useEffect, useCallback } from 'react';
import { useGetAllTenants } from '@app/identity/hooks/use-tenant';
import { useLocalOverrides } from '@app/hooks/use-local-overrides';
import { Tenant } from '@domain/identity/models/tenant.model';
import { useQueryState } from '@app/shared/hooks/use-query-state';
import { usePaginationState } from '@app/shared/hooks/use-pagination-state';

export interface TenantDashboardState {
  selectedId: string;
  showDiscardDialog: boolean;
  pendingNavigationId: string | null;
  activeConsoleTab: 'branches' | 'providers' | 'configurations' | 'audit';
  isTenantEditing: boolean;
  isCreateOpen: boolean;
  viewMode: 'list' | 'thumbnail';
  queryState: ReturnType<typeof useQueryState>;
  paginationState: ReturnType<typeof usePaginationState>;
}

export interface TenantDashboardActions {
  setSelectedId: React.Dispatch<React.SetStateAction<string>>;
  setShowDiscardDialog: React.Dispatch<React.SetStateAction<boolean>>;
  setPendingNavigationId: React.Dispatch<React.SetStateAction<string | null>>;
  setActiveConsoleTab: React.Dispatch<
    React.SetStateAction<'branches' | 'providers' | 'configurations' | 'audit'>
  >;
  setIsTenantEditing: React.Dispatch<React.SetStateAction<boolean>>;
  setIsCreateOpen: React.Dispatch<React.SetStateAction<boolean>>;
  setViewMode: React.Dispatch<React.SetStateAction<'list' | 'thumbnail'>>;
  handleSelectTenant: (id: string) => void;
  handleSelectOwnTenant: (tenantId: string, tenantCode: string) => void;
  confirmDiscard: () => void;
  patchTenant: (tenantId: string, patch: Partial<Tenant>) => void;
  handleCreateSuccess: (newTenantId: string) => void;
}

export function useTenantDashboard(): TenantDashboardState &
  TenantDashboardActions & {
    knownTenants: Tenant[];
    isLoadingList: boolean;
    listError: Error | null;
    activeTenant: Tenant | undefined;
    parentTenant: Tenant | null;
    isRootTenant: boolean;
    consoleTabs: Array<'branches' | 'providers' | 'configurations' | 'audit'>;
    totalItems: number;
    totalPages: number;
    startIndex: number;
    requiresFilter: boolean;
  } {
  const [selectedId, setSelectedId] = useState('');
  const [showDiscardDialog, setShowDiscardDialog] = useState(false);
  const [pendingNavigationId, setPendingNavigationId] = useState<string | null>(null);
  const [activeConsoleTab, setActiveConsoleTab] = useState<
    'branches' | 'providers' | 'configurations' | 'audit'
  >('branches');
  const [isTenantEditing, setIsTenantEditing] = useState(false);
  const [isCreateOpen, setIsCreateOpen] = useState(false);
  const [viewMode, setViewMode] = useState<'list' | 'thumbnail'>('list');

  const queryState = useQueryState({
    criteria: 'name',
    filter: 'all',
    sortBy: 'name',
    // Patrón estándar de lista de administración: la lista se carga al entrar
    // (todos los tenants) en vez de exigir aplicar un filtro primero. Búsqueda,
    // «Mi Organización» y «Limpiar» son refinamientos sobre esa carga.
    appliedFilter: true,
  });

  const paginationState = usePaginationState({
    initialPageSize: 10,
  });

  const shouldFetch = queryState.appliedQuery.filterApplied;

  const {
    data: tenantPage,
    isLoading: isLoadingList,
    error: listError,
  } = useGetAllTenants(
    shouldFetch
      ? {
          page: paginationState.page,
          pageSize: paginationState.pageSize,
          search: queryState.appliedQuery.term,
          criteria: queryState.appliedQuery.criteria,
          status: queryState.activeFilter,
          sortBy: queryState.sortBy,
          sortOrder: queryState.sortOrder,
        }
      : null
  );

  const { items: knownTenants, patchItem: patchLocalTenant } = useLocalOverrides<Tenant>(
    tenantPage?.items,
    'tenantId'
  );

  const activeTenant = knownTenants.find(tenant => tenant.tenantId === selectedId);
  const isRootTenant = activeTenant?.parentTenantId === null;

  const consoleTabs: Array<'branches' | 'providers' | 'configurations' | 'audit'> = [
    'branches',
    'providers',
    'configurations',
    'audit',
  ];

  const parentTenant = activeTenant?.parentTenantId
    ? (knownTenants.find(t => t.tenantId === activeTenant.parentTenantId) ?? null)
    : null;

  const hasPendingChanges = isTenantEditing;

  const applyTenantSelection = useCallback((id: string) => {
    setIsTenantEditing(false);
    setActiveConsoleTab('branches');
    setSelectedId(id);
  }, []);

  const handleSelectTenant = useCallback(
    (id: string) => {
      if (id === selectedId) return;
      if (hasPendingChanges) {
        setPendingNavigationId(id);
        setShowDiscardDialog(true);
        return;
      }
      applyTenantSelection(id);
    },
    [selectedId, hasPendingChanges, applyTenantSelection]
  );

  // FS-26 «Mi Organización»: carga la lista filtrada por el código del propio tenant
  // (así el registro aparece seleccionado en la lista) y lo abre en el detalle.
  // Al pulsar «Buscar» de nuevo se aplica una consulta nueva y la carga se reinicia
  // (una sola fuente de datos: la lista).
  const handleSelectOwnTenant = useCallback(
    (tenantId: string, tenantCode: string) => {
      queryState.applyQuery('code', tenantCode);
      applyTenantSelection(tenantId);
    },
    [queryState, applyTenantSelection]
  );

  const confirmDiscard = useCallback(() => {
    if (pendingNavigationId) applyTenantSelection(pendingNavigationId);
    setPendingNavigationId(null);
    setShowDiscardDialog(false);
  }, [pendingNavigationId, applyTenantSelection]);

  useEffect(() => {
    if (!selectedId && knownTenants.length > 0) {
      const first = knownTenants.find(t => t.parentTenantId === null) ?? knownTenants[0];
      applyTenantSelection(first.tenantId);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [knownTenants]);

  const patchTenant = useCallback(
    (tenantId: string, patch: Partial<Tenant>) => {
      patchLocalTenant(tenantId, patch);
    },
    [patchLocalTenant]
  );

  const handleCreateSuccess = useCallback(
    (newTenantId: string) => {
      paginationState.setPage(1);
      queryState.handleResetQuery();
      applyTenantSelection(newTenantId);
    },
    [applyTenantSelection, paginationState, queryState]
  );

  // If search criteria is 'id', it triggers handleSelectTenant
  useEffect(() => {
    if (queryState.appliedQuery.criteria === 'id' && queryState.appliedQuery.term) {
      handleSelectTenant(queryState.appliedQuery.term);
    }
  }, [queryState.appliedQuery, handleSelectTenant]);

  const totalItems = tenantPage?.totalItems ?? 0;
  const totalPages = tenantPage?.totalPages ?? 0;

  return {
    selectedId,
    setSelectedId,
    showDiscardDialog,
    setShowDiscardDialog,
    pendingNavigationId,
    setPendingNavigationId,
    activeConsoleTab,
    setActiveConsoleTab,
    isTenantEditing,
    setIsTenantEditing,
    isCreateOpen,
    setIsCreateOpen,
    viewMode,
    setViewMode,
    queryState,
    paginationState,
    handleSelectTenant,
    handleSelectOwnTenant,
    confirmDiscard,
    patchTenant,
    handleCreateSuccess,
    knownTenants,
    isLoadingList,
    listError,
    activeTenant,
    parentTenant,
    isRootTenant,
    consoleTabs,
    totalItems,
    totalPages,
    startIndex: paginationState.startIndex,
    requiresFilter: !shouldFetch,
  };
}
