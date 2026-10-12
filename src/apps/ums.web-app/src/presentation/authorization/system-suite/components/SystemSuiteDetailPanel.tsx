import React, { useState, useMemo } from 'react';
import {
  Box,
  Shield,
  Key,
  Users,
  Trash2,
  Flag,
  Database,
  Plus,
  ChevronsUpDown,
  ChevronsDownUp,
  Pencil,
  Check,
  X,
} from 'lucide-react';
import { SystemSuite } from '@domain/authorization/models/system-suite.model';
import { SystemSuiteProfileCard } from './SystemSuiteProfileCard';
import { DetailPanelShell, DetailTab } from '@shared/components/DetailPanelShell';
import { useI18n } from '@app/i18n/use-i18n';
import {
  useAddModule,
  useRemoveModule,
  useActivateModule,
  useDeactivateModule,
  useRegisterAction,
  useRenameAction,
  useRemoveAction,
} from '@app/authorization/hooks/use-system-suite';
import { M3TextField } from '@shared/components/M3TextField';
import { InlineAddForm } from '@shared/components/InlineAddForm';
import { IconButton } from '@shared/components/Tooltip';
import { CodeBadge } from '@shared/components/CodeBadge';
import { formatSystemCode } from '@app/utils/security';
import { SystemSuiteRolesPanel } from './SystemSuiteRolesPanel';
import { SystemSuiteDomainResourcesPanel } from './SystemSuiteDomainResourcesPanel';
import { SystemSuiteFeatureFlagsPanel } from './SystemSuiteFeatureFlagsPanel';
import { ListToolbar } from '@shared/components/ListToolbar';
import { SuiteNodeTree } from './hierarchy';
import type { SystemSuiteNode } from '@domain/authorization/schemas/system-suite.schema';

type SystemSuiteTab =
  | 'overview'
  | 'modules'
  | 'domain-resources'
  | 'actions'
  | 'roles'
  | 'feature-flags';

interface SystemSuiteDetailPanelProps {
  activeSystemSuite: SystemSuite | undefined;
  isLoading: boolean;
  onSystemSuiteUpdate: (systemSuiteId: string, patch: Partial<SystemSuite>) => void;
  onEditingChange: (isEditing: boolean) => void;
}

// ── Main panel ────────────────────────────────────────────────────────────────

export const SystemSuiteDetailPanel: React.FC<SystemSuiteDetailPanelProps> = ({
  activeSystemSuite,
  isLoading,
  onSystemSuiteUpdate,
  onEditingChange,
}) => {
  const t = useI18n();
  const [activeTab, setActiveTab] = useState<SystemSuiteTab>('overview');
  const [expandedNodes, setExpandedNodes] = useState<Record<string, boolean>>({});

  // ── Child entity view controls ─────────────────────────────────────────────
  const [modulesViewMode, setModulesViewMode] = useState<'list' | 'thumbnail'>('list');
  const [modulesFilter, setModulesFilter] = useState('all');
  const [modulesSortBy, setModulesSortBy] = useState('name');
  const [modulesSortOrder, setModulesSortOrder] = useState<'asc' | 'desc'>('asc');

  const [actionsViewMode, setActionsViewMode] = useState<'list' | 'thumbnail'>('thumbnail');
  const [actionsFilter, setActionsFilter] = useState('all');
  const [actionsSortBy, setActionsSortBy] = useState('name');
  const [actionsSortOrder, setActionsSortOrder] = useState<'asc' | 'desc'>('asc');
  const [editingActionCode, setEditingActionCode] = useState<string | null>(null);
  const [editingActionName, setEditingActionName] = useState('');

  const toggleNode = (nodeId: string) => {
    setExpandedNodes(prev => ({ ...prev, [nodeId]: !prev[nodeId] }));
  };

  const isExpanded = (nodeId: string) => expandedNodes[nodeId] !== false;

  // ── Collapse / expand all ──────────────────────────────────────────────────
  const allModuleNodeIds = useMemo(() => {
    if (!activeSystemSuite?.modules) return [];
    const ids: string[] = [];
    const walk = (nodes: SystemSuiteNode[]) => {
      for (const n of nodes) {
        ids.push(`node-${n.id}`);
        if (n.children?.length) walk(n.children);
      }
    };
    for (const mod of activeSystemSuite.modules) {
      ids.push(`module-${mod.id}`);
      walk(mod.nodes ?? []);
    }
    return ids;
  }, [activeSystemSuite?.modules]);

  const anyCollapsed = allModuleNodeIds.some(id => expandedNodes[id] === false);

  const handleToggleAll = () => {
    if (anyCollapsed) {
      // Expand all: reset to default (all nodes expanded by default)
      setExpandedNodes({});
    } else {
      // Collapse all: set every known node to false
      const collapsed: Record<string, boolean> = {};
      allModuleNodeIds.forEach(id => {
        collapsed[id] = false;
      });
      setExpandedNodes(collapsed);
    }
  };

  const tabs: DetailTab<SystemSuiteTab>[] = [
    { key: 'overview', label: t.overview, icon: <Box className="w-3.5 h-3.5" /> },
    { key: 'modules', label: t.modules, icon: <Shield className="w-3.5 h-3.5" /> },
    {
      key: 'domain-resources',
      label: 'Recursos de Dominio',
      icon: <Database className="w-3.5 h-3.5" />,
    },
    { key: 'actions', label: t.actions, icon: <Key className="w-3.5 h-3.5" /> },
    { key: 'roles', label: t.roles, icon: <Users className="w-3.5 h-3.5" /> },
    { key: 'feature-flags', label: 'Feature Flags', icon: <Flag className="w-3.5 h-3.5" /> },
  ];

  const suiteId = activeSystemSuite?.systemSuiteId ?? '';

  // Module mutations
  const addModuleMutation = useAddModule(suiteId);
  const removeModuleMutation = useRemoveModule(suiteId);
  const activateModuleMutation = useActivateModule(suiteId);
  const deactivateModuleMutation = useDeactivateModule(suiteId);
  const registerActionMutation = useRegisterAction(suiteId);
  const renameActionMutation = useRenameAction(suiteId);
  const removeActionMutation = useRemoveAction(suiteId);

  // Module add form state
  const [isAddingModule, setIsAddingModule] = useState(false);
  const [modCode, setModCode] = useState('');
  const [modName, setModName] = useState('');
  const [modDesc, setModDesc] = useState('');
  const [modSort, setModSort] = useState('1');
  const [modError, setModError] = useState('');

  // Action add form state
  const [isAddingAction, setIsAddingAction] = useState(false);
  const [actCode, setActCode] = useState('');
  const [actName, setActName] = useState('');
  const [actError, setActError] = useState('');

  const handleAddModule = async (e: React.FormEvent) => {
    e.preventDefault();
    setModError('');
    if (!modCode.trim()) {
      setModError('Código requerido');
      return;
    }
    if (!modName.trim()) {
      setModError('Nombre requerido');
      return;
    }
    try {
      await addModuleMutation.mutateAsync({
        code: formatSystemCode(modCode),
        name: modName.trim(),
        description: modDesc.trim(),
        sortOrder: parseInt(modSort) || 1,
      });
      setModCode('');
      setModName('');
      setModDesc('');
      setModSort('1');
      setIsAddingModule(false);
    } catch {
      /* handled by hook */
    }
  };

  const handleRegisterAction = async (e: React.FormEvent) => {
    e.preventDefault();
    setActError('');
    if (!actCode.trim()) {
      setActError('Código requerido');
      return;
    }
    if (!actName.trim()) {
      setActError('Nombre requerido');
      return;
    }
    try {
      await registerActionMutation.mutateAsync({
        code: formatSystemCode(actCode),
        name: actName.trim(),
      });
      setActCode('');
      setActName('');
      setIsAddingAction(false);
    } catch {
      /* handled by hook */
    }
  };

  if (isLoading || !activeSystemSuite) {
    return (
      <DetailPanelShell
        isLoading={isLoading}
        isEmpty={!activeSystemSuite}
        tabs={[]}
        activeTab={activeTab}
        onTabChange={setActiveTab}
        header={
          <div className="flex items-center justify-center h-32">
            <p className="text-[12px] text-m3-secondary">
              {t.selectSystemSuiteToView || 'Select system suite'}
            </p>
          </div>
        }
      >
        <div />
      </DetailPanelShell>
    );
  }

  return (
    <DetailPanelShell
      isLoading={false}
      isEmpty={false}
      tabs={tabs}
      activeTab={activeTab}
      onTabChange={setActiveTab}
      entityKey={activeSystemSuite.systemSuiteId}
      headerCollapsible
      header={
        <SystemSuiteProfileCard
          systemSuite={activeSystemSuite}
          onSystemSuiteUpdate={onSystemSuiteUpdate}
          onEditingChange={onEditingChange}
        />
      }
    >
      <div className="px-1.5 pt-1 pb-2 space-y-2">
        {/* ── Overview ── */}
        {activeTab === 'overview' && (
          <div>
            <h3 className="text-[12px] font-medium text-m3-on-surface mb-2">
              {t.systemSuiteDetails}
            </h3>
            <dl className="space-y-2 text-[11px]">
              <div className="flex justify-between">
                <dt className="text-m3-secondary">{t.code}</dt>
                <dd className="text-m3-on-surface font-mono">{activeSystemSuite.code}</dd>
              </div>
              <div className="flex justify-between">
                <dt className="text-m3-secondary">{t.name}</dt>
                <dd className="text-m3-on-surface">{activeSystemSuite.name}</dd>
              </div>
              <div className="flex justify-between">
                <dt className="text-m3-secondary">{t.description}</dt>
                <dd className="text-m3-on-surface">{activeSystemSuite.description || '-'}</dd>
              </div>
              <div className="flex justify-between">
                <dt className="text-m3-secondary">{t.status}</dt>
                <dd className="text-m3-on-surface">{activeSystemSuite.status}</dd>
              </div>
            </dl>
          </div>
        )}

        {/* ── Modules ── */}
        {activeTab === 'modules' && (
          <div className="space-y-1.5">
            <ListToolbar
              viewMode={modulesViewMode}
              onViewModeChange={setModulesViewMode}
              filterOptions={[
                { label: 'Todos', value: 'all' },
                { label: 'Activos', value: 'Active' },
                { label: 'Inactivos', value: 'Inactive' },
              ]}
              activeFilter={modulesFilter}
              onFilterChange={setModulesFilter}
              sortOptions={[
                { label: 'Nombre', value: 'name' },
                { label: 'Código', value: 'code' },
                { label: 'Orden', value: 'sortOrder' },
              ]}
              sortBy={modulesSortBy}
              onSortByChange={setModulesSortBy}
              sortOrder={modulesSortOrder}
              onSortOrderToggle={() => setModulesSortOrder(o => (o === 'asc' ? 'desc' : 'asc'))}
              itemCount={activeSystemSuite.modules?.length ?? 0}
              itemLabel="Módulo"
              onAdd={() => {
                setIsAddingModule(true);
                setModError('');
              }}
              addLabel="Nuevo Módulo Estructural"
              secondaryActions={
                allModuleNodeIds.length > 0 ? (
                  <button
                    type="button"
                    onClick={handleToggleAll}
                    className="inline-flex items-center justify-center h-8 w-8 rounded-md border border-m3-outline/30 bg-m3-surface text-m3-secondary hover:text-m3-primary hover:bg-m3-primary/10 transition-colors select-none"
                    title={anyCollapsed ? 'Expandir todos los nodos' : 'Colapsar todos los nodos'}
                  >
                    {anyCollapsed ? (
                      <ChevronsUpDown className="w-3.5 h-3.5" />
                    ) : (
                      <ChevronsDownUp className="w-3.5 h-3.5" />
                    )}
                  </button>
                ) : undefined
              }
            />

            {/* Formulario inline de nuevo módulo — el trigger vive en el toolbar (onAdd) */}
            <InlineAddForm
              isOpen={isAddingModule}
              onToggle={open => {
                setIsAddingModule(open);
                if (!open) setModError('');
              }}
              onSubmit={handleAddModule}
              addLabel="Módulo"
              title="Nuevo Módulo Estructural"
              cancelLabel={t.cancelEdit}
              submitLabel="Guardar Módulo"
              isLoading={addModuleMutation.isPending}
              triggerEmphasis="none"
              error={modError || undefined}
            >
              <M3TextField
                label="Código del Módulo"
                required
                value={modCode}
                onChange={e => setModCode(e.target.value)}
                placeholder="e.g. SEC"
              />
              <M3TextField
                label="Nombre del Módulo"
                required
                value={modName}
                onChange={e => setModName(e.target.value)}
                placeholder="e.g. Seguridad"
              />
              <M3TextField
                label="Descripción"
                value={modDesc}
                onChange={e => setModDesc(e.target.value)}
                placeholder="e.g. Módulo de administración de seguridad"
              />
              <M3TextField
                label="Orden"
                type="number"
                value={modSort}
                onChange={e => setModSort(e.target.value)}
                placeholder="1"
              />
            </InlineAddForm>

            {(() => {
              let filteredModules = activeSystemSuite.modules ?? [];
              if (modulesFilter !== 'all') {
                filteredModules = filteredModules.filter(m => m.status === modulesFilter);
              }
              filteredModules = [...filteredModules].sort((a, b) => {
                let cmp = 0;
                if (modulesSortBy === 'name') cmp = a.name.localeCompare(b.name);
                else if (modulesSortBy === 'code') cmp = a.code.localeCompare(b.code);
                else if (modulesSortBy === 'sortOrder')
                  cmp = (a.sortOrder ?? 0) - (b.sortOrder ?? 0);
                return modulesSortOrder === 'asc' ? cmp : -cmp;
              });

              if (!activeSystemSuite.modules || activeSystemSuite.modules.length === 0) {
                return (
                  <div className="flex flex-col items-center justify-center p-8 text-center border border-dashed border-m3-outline/25 rounded-xl bg-m3-surface-container/10 animate-fadeIn">
                    <Shield className="w-8 h-8 text-m3-secondary/50 mb-2" />
                    <p className="text-[12px] font-medium text-m3-on-surface">
                      {t.noModulesConfigured}
                    </p>
                  </div>
                );
              }

              if (filteredModules.length === 0) {
                return (
                  <div className="flex flex-col items-center justify-center p-6 text-center border border-dashed border-m3-outline/25 rounded-xl bg-m3-surface-container/10 animate-fadeIn">
                    <p className="text-[12px] font-medium text-m3-on-surface">
                      No hay módulos que coincidan con el filtro
                    </p>
                  </div>
                );
              }

              const containerClass =
                modulesViewMode === 'thumbnail'
                  ? 'grid grid-cols-1 sm:grid-cols-2 gap-3 animate-fadeIn'
                  : 'space-y-2 animate-fadeIn';

              return (
                <div className={containerClass}>
                  {filteredModules.map(module => {
                    const moduleNodeId = `module-${module.id}`;
                    const moduleExpanded = isExpanded(moduleNodeId);
                    return (
                      <SuiteNodeTree
                        key={module.id}
                        suiteId={suiteId}
                        module={module}
                        availableActions={activeSystemSuite.actions ?? []}
                        isExpanded={moduleExpanded}
                        isNodeExpanded={isExpanded}
                        onToggle={() => toggleNode(moduleNodeId)}
                        onToggleNode={toggleNode}
                        onDeactivate={() => deactivateModuleMutation.mutate(module.id)}
                        onActivate={() => activateModuleMutation.mutate(module.id)}
                        onRemove={() => removeModuleMutation.mutate(module.id)}
                        isDeactivating={deactivateModuleMutation.isPending}
                        isActivating={activateModuleMutation.isPending}
                        isRemoving={removeModuleMutation.isPending}
                      />
                    );
                  })}
                </div>
              );
            })()}
          </div>
        )}

        {/* ── Domain Resources ── */}
        {activeTab === 'domain-resources' && (
          <SystemSuiteDomainResourcesPanel systemSuite={activeSystemSuite} />
        )}

        {/* ── Actions ── */}
        {activeTab === 'actions' && (
          <div className="space-y-1.5">
            <ListToolbar
              viewMode={actionsViewMode}
              onViewModeChange={setActionsViewMode}
              filterOptions={[{ label: 'Todas', value: 'all' }]}
              activeFilter={actionsFilter}
              onFilterChange={setActionsFilter}
              sortOptions={[
                { label: 'Nombre', value: 'name' },
                { label: 'Código', value: 'code' },
              ]}
              sortBy={actionsSortBy}
              onSortByChange={setActionsSortBy}
              sortOrder={actionsSortOrder}
              onSortOrderToggle={() => setActionsSortOrder(o => (o === 'asc' ? 'desc' : 'asc'))}
              itemCount={activeSystemSuite.actions?.length ?? 0}
              itemLabel="Acción"
              onAdd={() => {
                setIsAddingAction(true);
                setActError('');
              }}
              addLabel="Nueva Acción del Sistema"
            />

            <InlineAddForm
              isOpen={isAddingAction}
              onToggle={open => {
                setIsAddingAction(open);
                if (!open) setActError('');
              }}
              onSubmit={handleRegisterAction}
              addLabel="Acción"
              title="Nueva Acción del Sistema"
              cancelLabel={t.cancelEdit}
              submitLabel="Guardar Acción"
              isLoading={registerActionMutation.isPending}
              triggerEmphasis="none"
              error={actError || undefined}
            >
              <M3TextField
                label="Código de Acción"
                required
                value={actCode}
                onChange={e => setActCode(e.target.value)}
                placeholder="e.g. INVENTORY_DELETE"
              />
              <M3TextField
                label="Nombre de la Acción"
                required
                value={actName}
                onChange={e => setActName(e.target.value)}
                placeholder="e.g. Eliminar Inventario"
              />
            </InlineAddForm>

            {(() => {
              let filteredActions = activeSystemSuite.actions ?? [];
              if (actionsSortBy === 'name') {
                filteredActions = [...filteredActions].sort((a, b) => {
                  const cmp = a.name.localeCompare(b.name);
                  return actionsSortOrder === 'asc' ? cmp : -cmp;
                });
              } else if (actionsSortBy === 'code') {
                filteredActions = [...filteredActions].sort((a, b) => {
                  const cmp = a.code.localeCompare(b.code);
                  return actionsSortOrder === 'asc' ? cmp : -cmp;
                });
              }

              if (!activeSystemSuite.actions || activeSystemSuite.actions.length === 0) {
                return (
                  <div className="flex flex-col items-center justify-center p-8 text-center border border-dashed border-m3-outline/25 rounded-xl bg-m3-surface-container/10 animate-fadeIn">
                    <Key className="w-8 h-8 text-m3-secondary/50 mb-2" />
                    <p className="text-[12px] font-medium text-m3-on-surface">
                      {t.noActionsConfigured}
                    </p>
                  </div>
                );
              }

              if (actionsViewMode === 'list') {
                return (
                  <div className="flex flex-col gap-1 animate-fadeIn">
                    {filteredActions.map(action => (
                      <div
                        key={action.id}
                        className="group/action flex items-center justify-between p-2.5 rounded-lg border border-m3-outline/10 bg-m3-surface-container/5 hover:bg-m3-surface-container/10 hover:border-m3-outline/25 transition-all duration-150"
                      >
                        <div className="flex items-center gap-3 flex-1 min-w-0">
                          <div className="p-1.5 rounded bg-m3-primary/10 text-m3-primary flex-shrink-0">
                            <Key className="w-3.5 h-3.5" />
                          </div>
                          <div className="flex-1 min-w-0">
                            {editingActionCode === action.code ? (
                              <input
                                autoFocus
                                value={editingActionName}
                                onChange={e => setEditingActionName(e.target.value)}
                                onKeyDown={e => {
                                  if (e.key === 'Enter') {
                                    renameActionMutation.mutate({
                                      code: action.code,
                                      name: editingActionName,
                                    });
                                    setEditingActionCode(null);
                                  }
                                  if (e.key === 'Escape') setEditingActionCode(null);
                                }}
                                className="w-full text-[12px] font-medium bg-m3-surface border border-m3-primary/40 rounded px-2 py-0.5 focus:outline-none focus:border-m3-primary"
                              />
                            ) : (
                              <p
                                className="text-[12px] font-medium text-m3-on-surface truncate"
                                title={action.name}
                              >
                                {action.name}
                              </p>
                            )}
                            <p className="text-[10px] font-mono text-m3-secondary truncate">
                              {action.code}
                            </p>
                          </div>
                        </div>
                        <div className="flex items-center gap-0.5 opacity-0 group-hover/action:opacity-100 transition-opacity">
                          {editingActionCode === action.code ? (
                            <>
                              <IconButton
                                tooltip="Guardar"
                                onClick={() => {
                                  renameActionMutation.mutate({
                                    code: action.code,
                                    name: editingActionName,
                                  });
                                  setEditingActionCode(null);
                                }}
                                disabled={renameActionMutation.isPending}
                                className="hover:text-emerald-500 hover:bg-emerald-500/10"
                              >
                                <Check className="w-3.5 h-3.5" />
                              </IconButton>
                              <IconButton
                                tooltip="Cancelar"
                                onClick={() => setEditingActionCode(null)}
                              >
                                <X className="w-3.5 h-3.5" />
                              </IconButton>
                            </>
                          ) : (
                            <IconButton
                              tooltip="Editar nombre"
                              onClick={() => {
                                setEditingActionCode(action.code);
                                setEditingActionName(action.name);
                              }}
                              className="hover:text-m3-primary hover:bg-m3-primary/10"
                            >
                              <Pencil className="w-3.5 h-3.5" />
                            </IconButton>
                          )}
                          <IconButton
                            tooltip="Remover Acción"
                            onClick={() => removeActionMutation.mutate(action.code)}
                            disabled={removeActionMutation.isPending}
                            className="hover:text-m3-error hover:bg-m3-error/10"
                          >
                            <Trash2 className="w-3.5 h-3.5" />
                          </IconButton>
                        </div>
                      </div>
                    ))}
                  </div>
                );
              }

              return (
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 animate-fadeIn">
                  {filteredActions.map(action => (
                    <div
                      key={action.id}
                      className="group/action flex items-center justify-between p-3 rounded-lg border border-m3-outline/15 bg-m3-surface-container/5 hover:bg-m3-surface-container/10 hover:border-m3-outline/30 hover:-translate-y-[1px] transition-all duration-200 shadow-sm"
                    >
                      <div className="flex items-center gap-3 flex-1 min-w-0">
                        <div className="p-2 rounded bg-m3-primary/10 text-m3-primary">
                          <Key className="w-3.5 h-3.5" />
                        </div>
                        <div className="flex-1 min-w-0">
                          <p
                            className="text-[12px] font-medium text-m3-on-surface truncate"
                            title={action.name}
                          >
                            {action.name}
                          </p>
                          <p className="text-[10px] font-mono text-m3-secondary truncate">
                            {action.code}
                          </p>
                        </div>
                      </div>
                      <IconButton
                        tooltip="Remover Acción"
                        onClick={() => removeActionMutation.mutate(action.code)}
                        disabled={removeActionMutation.isPending}
                        className="opacity-0 group-hover/action:opacity-100 transition-opacity hover:text-m3-error hover:bg-m3-error/10"
                      >
                        <Trash2 className="w-3.5 h-3.5" />
                      </IconButton>
                    </div>
                  ))}
                </div>
              );
            })()}
          </div>
        )}

        {activeTab === 'roles' && <SystemSuiteRolesPanel systemSuiteId={suiteId} />}

        {activeTab === 'feature-flags' && <SystemSuiteFeatureFlagsPanel systemSuiteId={suiteId} />}
      </div>
    </DetailPanelShell>
  );
};
