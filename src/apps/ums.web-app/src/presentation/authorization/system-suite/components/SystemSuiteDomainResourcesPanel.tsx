import React, { useState } from 'react';
import { Database, Layers, Component, FunctionSquare, Trash2, Pencil } from 'lucide-react';
import { useI18n } from '@app/i18n/use-i18n';
import { SystemSuite } from '@domain/authorization/models/system-suite.model';
import { M3TextField } from '@shared/components/M3TextField';
import { InlineAddForm } from '@shared/components/InlineAddForm';
import { IconButton } from '@shared/components/Tooltip';
import { CodeBadge } from '@shared/components/CodeBadge';
import { formatSystemCode } from '@app/utils/security';
import { useInlineEdit } from '@app/hooks/use-inline-edit';
import {
  useAddDomainResource,
  useRemoveDomainResource,
  useUpdateDomainResource,
} from '@app/authorization/hooks/use-system-suite';
import { ListToolbar } from '@shared/components/ListToolbar';
import { EntityRow } from '@shared/components/EntityRow';

interface SystemSuiteDomainResourcesPanelProps {
  systemSuite: SystemSuite;
}

interface DomainResourceDraft {
  name: string;
  description: string;
}

export const SystemSuiteDomainResourcesPanel: React.FC<SystemSuiteDomainResourcesPanelProps> = ({
  systemSuite,
}) => {
  const t = useI18n();
  const suiteId = systemSuite.systemSuiteId;

  const addDomainResourceMutation = useAddDomainResource(suiteId);
  const removeDomainResourceMutation = useRemoveDomainResource(suiteId);
  const updateDomainResourceMutation = useUpdateDomainResource(suiteId);

  const [isAddingResource, setIsAddingResource] = useState(false);
  const [resType, setResType] = useState<'Aggregate' | 'Entity' | 'DomainMethod'>('Aggregate');
  const [resModuleId, setResModuleId] = useState<string>('');
  const [resParentId, setResParentId] = useState<string>('');
  const [resCode, setResCode] = useState('');
  const [resName, setResName] = useState('');
  const [resDesc, setResDesc] = useState('');
  const [resError, setResError] = useState('');

  const [viewMode, setViewMode] = useState<'list' | 'thumbnail'>('thumbnail');
  const [activeFilter, setActiveFilter] = useState('all');
  const [sortBy, setSortBy] = useState('name');
  const [sortOrder, setSortOrder] = useState<'asc' | 'desc'>('asc');

  const edit = useInlineEdit<DomainResourceDraft>(['name', 'description']);

  const handleAddResource = async (e: React.FormEvent) => {
    e.preventDefault();
    setResError('');
    if (!resCode.trim()) {
      setResError('Código requerido');
      return;
    }
    if (!resName.trim()) {
      setResError('Nombre requerido');
      return;
    }

    try {
      await addDomainResourceMutation.mutateAsync({
        moduleId: resModuleId || null,
        parentResourceId: resParentId || null,
        type: resType,
        code: formatSystemCode(resCode),
        name: resName.trim(),
        description: resDesc.trim(),
      });
      setResCode('');
      setResName('');
      setResDesc('');
      setResModuleId('');
      setResParentId('');
      setIsAddingResource(false);
    } catch {
      /* handled by hook */
    }
  };

  const handleUpdateResource = async (e: React.FormEvent, resourceId: string) => {
    e.preventDefault();
    const name = edit.draft.name?.trim() ?? '';
    const description = edit.draft.description?.trim() ?? '';
    if (!name) return;

    try {
      await updateDomainResourceMutation.mutateAsync({
        domainResourceId: resourceId,
        name,
        description,
      });
      edit.cancelEdit();
    } catch {
      /* handled by hook */
    }
  };

  const domainResources = systemSuite.domainResources || [];

  let filteredResources = domainResources;
  if (activeFilter !== 'all') {
    filteredResources = filteredResources.filter(r => r.type === activeFilter);
  }
  filteredResources = [...filteredResources].sort((a, b) => {
    let cmp = 0;
    if (sortBy === 'name') cmp = a.name.localeCompare(b.name);
    else if (sortBy === 'code') cmp = a.code.localeCompare(b.code);
    else if (sortBy === 'type') cmp = a.type.localeCompare(b.type);
    return sortOrder === 'asc' ? cmp : -cmp;
  });

  return (
    <div className="space-y-1.5">
      <ListToolbar
        viewMode={viewMode}
        onViewModeChange={setViewMode}
        filterOptions={[
          { label: 'Todos', value: 'all' },
          { label: 'Agregados', value: 'Aggregate' },
          { label: 'Entidades', value: 'Entity' },
          { label: 'Métodos', value: 'DomainMethod' },
        ]}
        activeFilter={activeFilter}
        onFilterChange={setActiveFilter}
        sortOptions={[
          { label: 'Nombre', value: 'name' },
          { label: 'Código', value: 'code' },
          { label: 'Tipo', value: 'type' },
        ]}
        sortBy={sortBy}
        onSortByChange={setSortBy}
        sortOrder={sortOrder}
        onSortOrderToggle={() => setSortOrder(o => (o === 'asc' ? 'desc' : 'asc'))}
        itemCount={domainResources.length}
        itemLabel="Recurso"
        onAdd={() => {
          setIsAddingResource(true);
          setResError('');
        }}
        addLabel="Nuevo Recurso de Dominio"
      />

      <InlineAddForm
        isOpen={isAddingResource}
        onToggle={open => {
          setIsAddingResource(open);
          if (!open) setResError('');
        }}
        onSubmit={handleAddResource}
        addLabel="+"
        title="Nuevo Recurso de Dominio"
        cancelLabel={t.cancelEdit}
        submitLabel="Guardar Recurso"
        isLoading={addDomainResourceMutation.isPending}
        triggerEmphasis="none"
        error={resError || undefined}
      >
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div className="space-y-1">
            <label className="text-[11px] font-medium text-m3-on-surface">Tipo de Recurso</label>
            <select
              value={resType}
              onChange={e => {
                setResType(e.target.value as 'Aggregate' | 'Entity' | 'DomainMethod');
                setResParentId('');
              }}
              className="w-full h-10 px-3 rounded-lg bg-m3-surface border border-m3-outline/20 text-sm focus:border-m3-primary focus:ring-1 focus:ring-m3-primary outline-none"
            >
              <option value="Aggregate">Agregado (Aggregate Root)</option>
              <option value="Entity">Entidad (Entity)</option>
              <option value="DomainMethod">Método de Dominio (DomainMethod)</option>
            </select>
          </div>
          <div className="space-y-1">
            <label className="text-[11px] font-medium text-m3-on-surface">Módulo (Opcional)</label>
            <select
              value={resModuleId}
              onChange={e => setResModuleId(e.target.value)}
              className="w-full h-10 px-3 rounded-lg bg-m3-surface border border-m3-outline/20 text-sm focus:border-m3-primary focus:ring-1 focus:ring-m3-primary outline-none"
            >
              <option value="">[Sin Módulo / Global]</option>
              {systemSuite.modules?.map(m => (
                <option key={m.id} value={m.id}>
                  {m.name} ({m.code})
                </option>
              ))}
            </select>
          </div>
        </div>
        {(resType === 'DomainMethod' || resType === 'Entity') && (
          <div className="space-y-1">
            <label className="text-[11px] font-medium text-m3-on-surface">
              Recurso Padre{resType === 'DomainMethod' ? ' *' : ' (Opcional)'}
            </label>
            <select
              value={resParentId}
              onChange={e => setResParentId(e.target.value)}
              required={resType === 'DomainMethod'}
              className="w-full h-10 px-3 rounded-lg bg-m3-surface border border-m3-outline/20 text-sm focus:border-m3-primary focus:ring-1 focus:ring-m3-primary outline-none"
            >
              <option value="">[Sin Recurso Padre]</option>
              {domainResources
                .filter(r => r.type === 'Aggregate' || r.type === 'Entity')
                .map(r => (
                  <option key={r.id} value={r.id}>
                    {r.name} ({r.type === 'Aggregate' ? 'Agregado' : 'Entidad'})
                  </option>
                ))}
            </select>
          </div>
        )}
        <M3TextField
          label="Código"
          required
          value={resCode}
          onChange={e => setResCode(e.target.value)}
          placeholder="e.g. INVOICE"
        />
        <M3TextField
          label="Nombre del Recurso"
          required
          value={resName}
          onChange={e => setResName(e.target.value)}
          placeholder="e.g. Facturas"
        />
        <M3TextField
          label="Descripción"
          value={resDesc}
          onChange={e => setResDesc(e.target.value)}
          placeholder="e.g. Gestión de facturas y pagos"
        />
      </InlineAddForm>

      {domainResources.length === 0 ? (
        <div className="flex flex-col items-center justify-center p-8 text-center border border-dashed border-m3-outline/25 rounded-xl bg-m3-surface-container/10 animate-fadeIn">
          <Database className="w-8 h-8 text-m3-secondary/50 mb-2" />
          <p className="text-sm font-medium text-m3-on-surface">
            No hay recursos de dominio configurados
          </p>
          <p className="text-xs text-m3-secondary mt-1 max-w-sm">
            Los recursos de dominio (agregados y entidades) permiten controlar el acceso a nivel de
            datos y lógica de negocio, no solo navegación.
          </p>
        </div>
      ) : filteredResources.length === 0 ? (
        <div className="flex flex-col items-center justify-center p-6 text-center border border-dashed border-m3-outline/25 rounded-xl bg-m3-surface-container/10 animate-fadeIn">
          <p className="text-sm font-medium text-m3-on-surface">
            No hay recursos que coincidan con el filtro
          </p>
        </div>
      ) : viewMode === 'list' ? (
        <div className="flex flex-col gap-1 animate-fadeIn">
          {filteredResources.map(res => {
            const isEditing = edit.isEditing(res.id);
            const Icon =
              res.type === 'Aggregate'
                ? Layers
                : res.type === 'Entity'
                  ? Component
                  : FunctionSquare;
            const typeLabel =
              res.type === 'Aggregate'
                ? 'Agregado'
                : res.type === 'Entity'
                  ? 'Entidad'
                  : 'Método de Dominio';

            if (isEditing) {
              return (
                <form
                  key={res.id}
                  onSubmit={e => handleUpdateResource(e, res.id)}
                  className="p-3 rounded-lg border border-m3-primary/30 bg-m3-surface-container/20 space-y-2 animate-fadeIn shadow-sm"
                >
                  <div className="flex items-center gap-2 mb-2">
                    <Icon className="w-4 h-4 text-m3-primary" />
                    <span className="text-[11px] font-bold text-m3-on-surface">
                      Editando {typeLabel}: {res.code}
                    </span>
                  </div>
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-2">
                    <M3TextField
                      label="Nombre"
                      required
                      value={edit.draft.name ?? ''}
                      onChange={e => edit.setField('name', e.target.value)}
                    />
                    <M3TextField
                      label="Descripción"
                      value={edit.draft.description ?? ''}
                      onChange={e => edit.setField('description', e.target.value)}
                    />
                  </div>
                  <div className="flex gap-1.5 justify-end mt-2">
                    <button
                      type="button"
                      onClick={edit.cancelEdit}
                      className="text-[11px] px-3 py-1.5 rounded-md text-m3-secondary hover:bg-m3-surface-variant/20 transition-colors"
                    >
                      Cancelar
                    </button>
                    <button
                      type="submit"
                      disabled={updateDomainResourceMutation.isPending}
                      className="text-[11px] px-3 py-1.5 rounded-md bg-m3-primary text-m3-on-primary hover:bg-m3-primary/80 disabled:opacity-50 transition-colors"
                    >
                      {updateDomainResourceMutation.isPending ? 'Guardando…' : 'Guardar Cambios'}
                    </button>
                  </div>
                </form>
              );
            }

            return (
              <EntityRow
                key={res.id}
                leading={
                  <div
                    className={`p-1.5 rounded ${res.type === 'Aggregate' ? 'bg-indigo-500/10 text-indigo-500' : res.type === 'Entity' ? 'bg-emerald-500/10 text-emerald-500' : 'bg-orange-500/10 text-orange-500'}`}
                  >
                    <Icon className="w-4 h-4" />
                  </div>
                }
                trailingColumns={[
                  {
                    content:
                      res.moduleId && systemSuite.modules?.some(m => m.id === res.moduleId) ? (
                        <span
                          className="text-[9px] font-medium text-m3-secondary bg-m3-surface-variant/50 px-1.5 py-0.5 rounded"
                          title="Módulo Asignado"
                        >
                          {systemSuite.modules.find(m => m.id === res.moduleId)?.code}
                        </span>
                      ) : null,
                    width: 'w-20',
                    align: 'end',
                  },
                  {
                    content: <CodeBadge code={res.code} size="xs" />,
                    width: 'w-28',
                    align: 'end',
                  },
                ]}
                trailing={
                  <div className="flex items-center gap-0.5 opacity-0 group-hover/row:opacity-100 transition-opacity">
                    <IconButton
                      tooltip="Editar Recurso"
                      onClick={() =>
                        edit.openEdit(res.id, { name: res.name, description: res.description })
                      }
                      className="hover:text-m3-primary hover:bg-m3-primary/10"
                    >
                      <Pencil className="w-3.5 h-3.5" />
                    </IconButton>
                    <IconButton
                      tooltip="Remover Recurso"
                      onClick={() => removeDomainResourceMutation.mutate(res.id)}
                      disabled={removeDomainResourceMutation.isPending}
                      className="hover:text-m3-error hover:bg-m3-error/10"
                    >
                      <Trash2 className="w-3.5 h-3.5" />
                    </IconButton>
                  </div>
                }
              >
                <p className="text-xs font-semibold text-m3-on-surface truncate" title={res.name}>
                  {res.name}
                </p>
                <p className="text-[10px] text-m3-secondary">{typeLabel}</p>
                {res.description && (
                  <p
                    className="text-[10px] text-m3-secondary/80 line-clamp-1"
                    title={res.description}
                  >
                    {res.description}
                  </p>
                )}
              </EntityRow>
            );
          })}
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-3 animate-fadeIn">
          {filteredResources.map(res => {
            const isEditing = edit.isEditing(res.id);
            const Icon =
              res.type === 'Aggregate'
                ? Layers
                : res.type === 'Entity'
                  ? Component
                  : FunctionSquare;
            const typeLabel =
              res.type === 'Aggregate'
                ? 'Agregado Root'
                : res.type === 'Entity'
                  ? 'Entidad de Dominio'
                  : 'Método de Dominio';

            if (isEditing) {
              return (
                <form
                  key={res.id}
                  onSubmit={e => handleUpdateResource(e, res.id)}
                  className="p-3 rounded-lg border border-m3-primary/30 bg-m3-surface-container/20 space-y-2 animate-fadeIn shadow-sm col-span-1 md:col-span-2"
                >
                  <div className="flex items-center gap-2 mb-2">
                    <Icon className="w-4 h-4 text-m3-primary" />
                    <span className="text-[11px] font-bold text-m3-on-surface">
                      Editando {typeLabel}: {res.code}
                    </span>
                  </div>
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-2">
                    <M3TextField
                      label="Nombre"
                      required
                      value={edit.draft.name ?? ''}
                      onChange={e => edit.setField('name', e.target.value)}
                    />
                    <M3TextField
                      label="Descripción"
                      value={edit.draft.description ?? ''}
                      onChange={e => edit.setField('description', e.target.value)}
                    />
                  </div>
                  <div className="flex gap-1.5 justify-end mt-2">
                    <button
                      type="button"
                      onClick={edit.cancelEdit}
                      className="text-[11px] px-3 py-1.5 rounded-md text-m3-secondary hover:bg-m3-surface-variant/20 transition-colors"
                    >
                      Cancelar
                    </button>
                    <button
                      type="submit"
                      disabled={updateDomainResourceMutation.isPending}
                      className="text-[11px] px-3 py-1.5 rounded-md bg-m3-primary text-m3-on-primary hover:bg-m3-primary/80 disabled:opacity-50 transition-colors"
                    >
                      {updateDomainResourceMutation.isPending ? 'Guardando…' : 'Guardar Cambios'}
                    </button>
                  </div>
                </form>
              );
            }

            return (
              <div
                key={res.id}
                className="group/res flex items-start justify-between p-3 rounded-lg border border-m3-outline/15 bg-m3-surface-container/5 hover:bg-m3-surface-container/10 hover:border-m3-outline/30 hover:-translate-y-[1px] transition-all duration-200 shadow-sm"
              >
                <div className="flex items-start gap-3 flex-1 min-w-0">
                  <div
                    className={`p-2 rounded mt-0.5 ${res.type === 'Aggregate' ? 'bg-indigo-500/10 text-indigo-500' : res.type === 'Entity' ? 'bg-emerald-500/10 text-emerald-500' : 'bg-orange-500/10 text-orange-500'}`}
                  >
                    <Icon className="w-4 h-4" />
                  </div>
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2">
                      <p
                        className="text-xs font-semibold text-m3-on-surface truncate flex-1 min-w-0"
                        title={res.name}
                      >
                        {res.name}
                      </p>
                      <div className="flex items-center gap-1.5 flex-shrink-0">
                        <CodeBadge code={res.code} size="xs" />
                        {res.moduleId && systemSuite.modules?.some(m => m.id === res.moduleId) && (
                          <span
                            className="text-[9px] font-medium text-m3-secondary bg-m3-surface-variant/50 px-1.5 py-0.5 rounded"
                            title="Módulo Asignado"
                          >
                            {systemSuite.modules.find(m => m.id === res.moduleId)?.code}
                          </span>
                        )}
                      </div>
                    </div>
                    <p className="text-[10px] text-m3-secondary mt-1">{typeLabel}</p>
                    {res.description && (
                      <p
                        className="text-[10px] text-m3-secondary/80 mt-1 line-clamp-2"
                        title={res.description}
                      >
                        {res.description}
                      </p>
                    )}
                  </div>
                </div>
                <div className="flex flex-col gap-1 items-end opacity-0 group-hover/res:opacity-100 transition-opacity pl-2">
                  <IconButton
                    tooltip="Editar Recurso"
                    onClick={() => {
                      edit.openEdit(res.id, { name: res.name, description: res.description });
                    }}
                    className="hover:text-m3-primary hover:bg-m3-primary/10"
                  >
                    <Pencil className="w-3.5 h-3.5" />
                  </IconButton>
                  <IconButton
                    tooltip="Remover Recurso"
                    onClick={() => removeDomainResourceMutation.mutate(res.id)}
                    disabled={removeDomainResourceMutation.isPending}
                    className="hover:text-m3-error hover:bg-m3-error/10"
                  >
                    <Trash2 className="w-3.5 h-3.5" />
                  </IconButton>
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
};
