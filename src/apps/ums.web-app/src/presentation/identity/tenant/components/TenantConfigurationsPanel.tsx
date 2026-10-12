/**
 * TenantConfigurationsPanel.tsx
 *
 * Tenant-specific configuration parameters panel.
 * Uses ParameterDefinitionPickerDialog for adding parameters (Tenant Only scope).
 */
import React, { useEffect, useCallback, useState } from 'react';
import { useI18n } from '@app/i18n/use-i18n';
import { useNotificationStore } from '@app/stores/notification.store';
import { Plus, Globe, Building2, Edit2, X, Check } from 'lucide-react';
import { appConfigurationService } from '@infra/configuration/services/app-configuration.service';
import { EmptyState } from '@presentation/shared/components/EmptyState';
import { SectionHeader } from '@presentation/shared/components/SectionHeader';
import { CodeBadge } from '@presentation/shared/components/CodeBadge';
import { IconButton } from '@presentation/shared/components/Tooltip';
import { SmartConfigInput } from '@presentation/shared/components/SmartConfigInput';
import { ConfigValueDisplay } from '@presentation/shared/components/ConfigValueDisplay';
import { ListToolbar } from '@shared/components/ListToolbar';
import { StatusBadge } from '@shared/components/StatusBadge';
import { M3Dialog } from '@shared/components/M3Dialog';
import { FormField, FormInput, FormButton } from '@shared/components/form';
import { parameterCatalogService } from '@infra/configuration/services/parameter-catalog/parameter-catalog.service';
import type { ParameterDefinition } from '@domain/configuration/schemas/parameter-catalog/parameter-definition.schema';
import { getHttpErrorMessage, getSupportReferenceId } from '@app/errors/http-error';

interface TenantConfigurationsPanelProps {
  tenantId: string;
  tenantName: string;
}

interface ConfigParameter {
  appConfigurationId: string;
  code: string;
  value: string;
  description: string;
  scope: string;
  status: string;
}

export const TenantConfigurationsPanel: React.FC<TenantConfigurationsPanelProps> = ({
  tenantId,
  tenantName,
}) => {
  const t = useI18n();
  const addNotification = useNotificationStore(s => s.addNotification);

  const [configs, setConfigs] = useState<ConfigParameter[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editValue, setEditValue] = useState('');
  const [searchValue, setSearchValue] = useState('');
  const [searchCriteria, setSearchCriteria] = useState('code');
  const [appliedSearchTerm, setAppliedSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [sortBy, setSortBy] = useState('code');
  const [sortOrder, setSortOrder] = useState<'asc' | 'desc'>('asc');
  const [viewMode, setViewMode] = useState<'list' | 'thumbnail'>('list');
  const [selectedId, setSelectedId] = useState<string | null>(null);

  // Picker dialog state
  const [isPickerOpen, setIsPickerOpen] = useState(false);
  const [pickerParams, setPickerParams] = useState<ParameterDefinition[]>([]);
  const [pickerLoading, setPickerLoading] = useState(false);
  const [pickerSelectedId, setPickerSelectedId] = useState<string | null>(null);
  const [pickerSearch, setPickerSearch] = useState('');

  // Delete dialog state
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const [pendingDeleteId, setPendingDeleteId] = useState<string | null>(null);

  const loadConfigs = useCallback(async () => {
    if (!tenantId) return;
    setIsLoading(true);
    setError(null);
    try {
      const page = await appConfigurationService.getAll({ page: 1, pageSize: 100, tenantId });
      const mapped: ConfigParameter[] = page.items.map(item => ({
        appConfigurationId: item.appConfigurationId,
        code: item.code,
        value: item.value,
        description: item.description,
        scope: item.scope,
        status: item.status,
      }));
      setConfigs(mapped);
    } catch (err) {
      const errorMessage = getHttpErrorMessage(
        err,
        'No se pudieron cargar las configuraciones del tenant. Intente nuevamente.'
      );
      const supportReferenceId = getSupportReferenceId(err);
      setError(
        supportReferenceId ? `${errorMessage} Referencia: ${supportReferenceId}` : errorMessage
      );
      addNotification({ title: t.error, message: t.notifConfigLoadFailed, type: 'error' });
    } finally {
      setIsLoading(false);
    }
  }, [tenantId, addNotification, t]);

  useEffect(() => {
    loadConfigs();
  }, [loadConfigs]);

  const handleSearchSubmit = useCallback(() => {
    setAppliedSearchTerm(searchValue);
  }, [searchValue]);

  const handleSearchClear = useCallback(() => {
    setSearchValue('');
    setAppliedSearchTerm('');
  }, []);

  const filteredConfigs = configs
    .filter(config => {
      const term = appliedSearchTerm.toLowerCase();
      const matchesSearch =
        !appliedSearchTerm ||
        (searchCriteria === 'code'
          ? config.code.toLowerCase().includes(term)
          : config.description.toLowerCase().includes(term));
      const matchesStatus = statusFilter === 'all' || config.status === statusFilter;
      return matchesSearch && matchesStatus;
    })
    .sort((a, b) => {
      const aVal = a[sortBy as keyof ConfigParameter] ?? '';
      const bVal = b[sortBy as keyof ConfigParameter] ?? '';
      const cmp = String(aVal).localeCompare(String(bVal));
      return sortOrder === 'asc' ? cmp : -cmp;
    });

  const existingCodes = configs.map(c => c.code);

  const handleEdit = (id: string, currentValue: string) => {
    setEditingId(id);
    setEditValue(currentValue);
  };

  const handleSaveEdit = async () => {
    if (!editingId) return;
    try {
      await appConfigurationService.updateAppConfiguration(editingId, {
        value: editValue,
        description: '',
      });
      addNotification({ title: t.success, message: t.notifConfigUpdated, type: 'success' });
      setEditingId(null);
      loadConfigs();
    } catch {
      addNotification({ title: t.error, message: t.notifConfigUpdateFailed, type: 'error' });
    }
  };

  const handleCancelEdit = () => {
    setEditingId(null);
    setEditValue('');
  };

  const handleDeleteRequest = (id: string) => {
    setPendingDeleteId(id);
    setDeleteDialogOpen(true);
  };

  const handleDeleteConfirm = async () => {
    if (!pendingDeleteId) return;
    try {
      await appConfigurationService.deleteAppConfiguration(pendingDeleteId);
      addNotification({ title: t.success, message: t.notifConfigDeleted, type: 'success' });
      setDeleteDialogOpen(false);
      setPendingDeleteId(null);
      loadConfigs();
    } catch {
      addNotification({ title: t.error, message: t.notifConfigDeleteFailed, type: 'error' });
    }
  };

  const openPicker = async () => {
    setPickerLoading(true);
    setPickerSearch('');
    setPickerSelectedId(null);
    setIsPickerOpen(true);
    try {
      const result = await parameterCatalogService.getAll({});
      // Filter: only Tenant Only (scopeId === 2) and not already linked
      const available = result.items.filter(
        p => p.scopeId === 2 && !existingCodes.includes(p.code)
      );
      setPickerParams(available);
    } catch {
      setPickerParams([]);
    } finally {
      setPickerLoading(false);
    }
  };

  const handlePickerSelect = async () => {
    if (!pickerSelectedId) return;
    const param = pickerParams.find(p => p.id === pickerSelectedId);
    if (!param) return;

    try {
      await appConfigurationService.createAppConfiguration({
        tenantId,
        code: param.code.toUpperCase(),
        value: param.defaultValue ?? '',
        description: param.description || '',
        isInheritable: false,
        isEncrypted: false,
      });
      addNotification({ title: t.success, message: t.notifConfigCreated, type: 'success' });
      setIsPickerOpen(false);
      loadConfigs();
    } catch {
      addNotification({ title: t.error, message: t.notifConfigCreateFailed, type: 'error' });
    }
  };

  const filterOptions = [
    { label: 'Todos', value: 'all' },
    { label: 'Published', value: 'Published' },
    { label: 'Draft', value: 'Draft' },
    { label: 'Archived', value: 'Archived' },
  ];

  const sortOptions = [
    { label: 'Código', value: 'code' },
    { label: 'Estado', value: 'status' },
  ];

  const searchOptions = [
    { label: 'Código', value: 'code' },
    { label: 'Descripción', value: 'description' },
  ];

  const scopeIcons: Record<string, React.ReactNode> = {
    Global: <Globe className="w-3.5 h-3.5 text-blue-500" />,
    Tenant: <Building2 className="w-3.5 h-3.5 text-green-500" />,
  };

  if (error) {
    return (
      <div className="flex-1 flex items-center justify-center p-4">
        <div className="text-center text-m3-error">{error}</div>
      </div>
    );
  }

  return (
    <div className="flex flex-col h-full">
      {/* Parameter Picker Dialog */}
      <M3Dialog
        open={isPickerOpen}
        onScrimClick={() => setIsPickerOpen(false)}
        title={t.selectTenantParameter ?? 'Select Tenant Parameter'}
        message={t.onlyTenantParameters ?? 'Only Tenant-scoped parameters can be added'}
        actions={[
          {
            label: t.cancelBtn ?? 'Cancel',
            variant: 'outlined',
            onClick: () => setIsPickerOpen(false),
          },
          {
            label: t.addParameter ?? 'Add',
            variant: 'filled',
            onClick: handlePickerSelect,
            disabled: !pickerSelectedId,
          },
        ]}
      >
        <div className="space-y-3">
          <FormInput
            placeholder={t.searchParameters ?? 'Search parameters...'}
            value={pickerSearch}
            onChange={e => setPickerSearch(e.target.value)}
          />
          <div className="border border-m3-outline/20 rounded-lg overflow-hidden max-h-[250px] overflow-y-auto">
            {pickerLoading ? (
              <div className="p-4 text-center text-[12px] text-m3-secondary">
                {t.loading ?? 'Cargando...'}
              </div>
            ) : pickerParams.length === 0 ? (
              <EmptyState
                icon={<Building2 className="w-5 h-5" />}
                message={t.noTenantParametersAvailable ?? 'No hay parámetros de tenant disponibles'}
                variant="dashed"
              />
            ) : (
              pickerParams
                .filter(
                  p =>
                    p.name.toLowerCase().includes(pickerSearch.toLowerCase()) ||
                    p.code.toLowerCase().includes(pickerSearch.toLowerCase())
                )
                .map(param => (
                  <button
                    key={param.id}
                    onClick={() => setPickerSelectedId(param.id)}
                    className={`w-full px-3 py-2 text-left flex items-center gap-2 hover:bg-m3-surface-variant transition-colors ${
                      pickerSelectedId === param.id ? 'bg-m3-primary/10' : ''
                    }`}
                  >
                    <div
                      className={`w-4 h-4 rounded border-2 flex items-center justify-center ${
                        pickerSelectedId === param.id
                          ? 'bg-m3-primary border-m3-primary'
                          : 'border-m3-outline'
                      }`}
                    >
                      {pickerSelectedId === param.id && (
                        <Check className="w-2.5 h-2.5 text-white" />
                      )}
                    </div>
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-1.5">
                        <span className="text-[12px] font-medium">{param.name}</span>
                        <CodeBadge code={param.code} size="xs" />
                      </div>
                      {param.description && (
                        <span className="text-[11px] text-m3-secondary truncate block">
                          {param.description}
                        </span>
                      )}
                    </div>
                  </button>
                ))
            )}
          </div>
        </div>
      </M3Dialog>

      {/* Delete Confirmation Dialog */}
      <M3Dialog
        open={deleteDialogOpen}
        onScrimClick={() => setDeleteDialogOpen(false)}
        title={t.deleteConfiguration ?? 'Delete Configuration'}
        message={
          t.deleteConfigurationConfirm ?? 'Are you sure you want to delete this configuration?'
        }
        actions={[
          {
            label: t.cancelBtn ?? 'Cancel',
            variant: 'outlined',
            onClick: () => setDeleteDialogOpen(false),
          },
          {
            label: t.deleteBtn ?? 'Delete',
            variant: 'filled',
            className: '!bg-rose-500 hover:!bg-rose-600',
            onClick: handleDeleteConfirm,
          },
        ]}
      />

      <SectionHeader
        title={
          t.configurationsForTenant?.replace('{0}', tenantName) ??
          `Configurations for ${tenantName}`
        }
        subtitle={`${configs.length} ${configs.length === 1 ? 'configuración' : 'configuraciones'}`}
      />

      <ListToolbar
        itemCount={configs.length}
        itemLabel="configuración"
        viewMode={viewMode}
        onViewModeChange={setViewMode}
        searchOptions={searchOptions}
        activeSearchCriteria={searchCriteria}
        onSearchCriteriaChange={setSearchCriteria}
        searchValue={searchValue}
        onSearchValueChange={setSearchValue}
        onSearchSubmit={handleSearchSubmit}
        onSearchClear={handleSearchClear}
        filterOptions={filterOptions}
        activeFilter={statusFilter}
        onFilterChange={setStatusFilter}
        sortOptions={sortOptions}
        sortBy={sortBy}
        onSortByChange={setSortBy}
        sortOrder={sortOrder}
        onSortOrderToggle={() => setSortOrder(o => (o === 'asc' ? 'desc' : 'asc'))}
        onAdd={openPicker}
        addLabel={t.addParameter ?? 'Add Parameter'}
      />

      <div className="flex-1 overflow-auto p-2">
        {isLoading ? (
          <div className="flex items-center justify-center h-32">
            <div className="animate-spin w-5 h-5 border-2 border-m3-primary border-t-transparent rounded-full" />
          </div>
        ) : filteredConfigs.length === 0 ? (
          <EmptyState
            icon={<Building2 className="w-5 h-5 text-m3-outline" />}
            message={t.noConfigsForTenant ?? 'No configurations for this tenant'}
          />
        ) : viewMode === 'list' ? (
          <div className="space-y-1">
            {filteredConfigs.map(config => (
              <div
                key={config.appConfigurationId}
                onClick={() =>
                  setSelectedId(
                    selectedId === config.appConfigurationId ? null : config.appConfigurationId
                  )
                }
                className={`p-2.5 rounded-lg border cursor-pointer transition-colors ${
                  selectedId === config.appConfigurationId
                    ? 'border-m3-primary/30 bg-m3-primary/5'
                    : 'border-transparent hover:bg-m3-surface-container/40'
                }`}
              >
                <div className="flex items-center gap-2">
                  <div className="flex items-center gap-1.5 flex-1 min-w-0">
                    {scopeIcons[config.scope] ?? (
                      <Building2 className="w-3.5 h-3.5 text-green-500" />
                    )}
                    <span className="text-[12px] font-medium text-m3-on-surface truncate">
                      {config.code}
                    </span>
                  </div>
                  <div className="flex items-center gap-1.5 flex-shrink-0">
                    <CodeBadge code={config.scope} size="xs" />
                    <StatusBadge status={config.status} label={config.status} size="xs" />
                  </div>
                  <div className="flex items-center gap-0.5 flex-shrink-0">
                    <IconButton
                      tooltip={t.editBtn ?? 'Edit'}
                      onClick={e => {
                        e.stopPropagation();
                        handleEdit(config.appConfigurationId, config.value);
                      }}
                      size="small"
                    >
                      <Edit2 className="w-3.5 h-3.5" />
                    </IconButton>
                    <IconButton
                      tooltip={t.deleteBtn ?? 'Delete'}
                      onClick={e => {
                        e.stopPropagation();
                        handleDeleteRequest(config.appConfigurationId);
                      }}
                      size="small"
                      className="hover:text-rose-500"
                    >
                      <X className="w-3.5 h-3.5" />
                    </IconButton>
                  </div>
                </div>
                {selectedId === config.appConfigurationId && (
                  <div className="mt-2 pt-2 border-t border-m3-outline/10">
                    {editingId === config.appConfigurationId ? (
                      <div className="flex items-center gap-1.5">
                        <div className="w-56">
                          <SmartConfigInput
                            code={config.code}
                            value={editValue}
                            onChange={setEditValue}
                          />
                        </div>
                        <button
                          onClick={handleSaveEdit}
                          className="h-6 px-2.5 text-[10px] font-medium bg-m3-primary text-white rounded hover:bg-m3-primary/90"
                        >
                          {t.save ?? 'Save'}
                        </button>
                        <button
                          onClick={handleCancelEdit}
                          className="h-6 px-2.5 text-[10px] font-medium text-m3-secondary hover:bg-m3-surface-variant rounded"
                        >
                          {t.cancel ?? 'Cancel'}
                        </button>
                      </div>
                    ) : (
                      <div className="flex items-center gap-1.5">
                        <span className="text-[11px] text-m3-secondary">{t.value ?? 'Value'}:</span>
                        <ConfigValueDisplay value={config.value} />
                      </div>
                    )}
                  </div>
                )}
              </div>
            ))}
          </div>
        ) : (
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
            {filteredConfigs.map(config => (
              <div
                key={config.appConfigurationId}
                onClick={() =>
                  setSelectedId(
                    selectedId === config.appConfigurationId ? null : config.appConfigurationId
                  )
                }
                className={`p-2.5 rounded-lg border cursor-pointer transition-colors ${
                  selectedId === config.appConfigurationId
                    ? 'border-m3-primary/30 bg-m3-primary/5'
                    : 'border-transparent hover:bg-m3-surface-container/40'
                }`}
              >
                <div className="flex items-center justify-between mb-1">
                  <div className="flex items-center gap-1.5">
                    {scopeIcons[config.scope] ?? (
                      <Building2 className="w-3.5 h-3.5 text-green-500" />
                    )}
                    <span className="text-[12px] font-medium truncate">{config.code}</span>
                  </div>
                  <div className="flex items-center gap-0.5 opacity-0 group-hover:opacity-100 transition-opacity">
                    <IconButton
                      tooltip={t.editBtn ?? 'Edit'}
                      onClick={e => {
                        e.stopPropagation();
                        handleEdit(config.appConfigurationId, config.value);
                      }}
                      size="small"
                    >
                      <Edit2 className="w-3 h-3" />
                    </IconButton>
                    <IconButton
                      tooltip={t.deleteBtn ?? 'Delete'}
                      onClick={e => {
                        e.stopPropagation();
                        handleDeleteRequest(config.appConfigurationId);
                      }}
                      size="small"
                      className="hover:text-rose-500"
                    >
                      <X className="w-3 h-3" />
                    </IconButton>
                  </div>
                </div>
                <div className="flex items-center gap-1 mb-1">
                  <CodeBadge code={config.scope} size="xs" />
                  <StatusBadge status={config.status} label={config.status} size="xs" />
                </div>
                <div className="mb-1">
                  <ConfigValueDisplay value={config.value} />
                </div>
                {config.description && (
                  <p className="text-[11px] text-m3-secondary truncate">{config.description}</p>
                )}
                {selectedId === config.appConfigurationId && (
                  <div className="mt-2 pt-2 border-t border-m3-outline/10">
                    <div className="flex items-center gap-1.5">
                      <div className="w-48">
                        <SmartConfigInput
                          code={config.code}
                          value={editingId === config.appConfigurationId ? editValue : config.value}
                          onChange={setEditValue}
                        />
                      </div>
                      {editingId === config.appConfigurationId ? (
                        <>
                          <button
                            onClick={e => {
                              e.stopPropagation();
                              handleSaveEdit();
                            }}
                            className="h-6 px-2 text-[10px] font-medium bg-m3-primary text-white rounded"
                          >
                            {t.save ?? 'Save'}
                          </button>
                          <button
                            onClick={e => {
                              e.stopPropagation();
                              handleCancelEdit();
                            }}
                            className="h-6 px-2 text-[10px] font-medium text-m3-secondary hover:bg-m3-surface-variant rounded"
                          >
                            {t.cancel ?? 'Cancel'}
                          </button>
                        </>
                      ) : (
                        <button
                          onClick={e => {
                            e.stopPropagation();
                            handleEdit(config.appConfigurationId, config.value);
                          }}
                          className="h-6 px-2 text-[10px] font-medium text-m3-secondary hover:bg-m3-surface-variant rounded"
                        >
                          {t.editBtn ?? 'Edit'}
                        </button>
                      )}
                    </div>
                  </div>
                )}
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
};
