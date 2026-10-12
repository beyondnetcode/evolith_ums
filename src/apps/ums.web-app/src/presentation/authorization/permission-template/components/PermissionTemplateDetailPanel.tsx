/**
 * PermissionTemplateDetailPanel
 *
 * Three clearly separated permission sections at same hierarchy level:
 *   1. Modules - Navigation/UI permission structure (Module > Menu > SubMenu > Option)
 *   2. Domain Resources - Business/domain permission structure (Aggregates/Entities with CRUD + custom actions)
 *   3. System Actions - General system-level actions not tied to modules or domain resources
 */
import React, { useState, useCallback } from 'react';
import {
  ShieldCheck,
  SendHorizonal,
  Ban,
  Trash2,
  Info,
  AlertTriangle,
  Shield,
  Database,
  Blocks,
  CheckCircle2,
  XCircle,
  MinusCircle,
  Layers,
  Component,
} from 'lucide-react';
import type {
  PermissionTemplateDetail,
  ExclusiveArcTarget,
  PermissionEffect,
} from '@domain/authorization/models/permission-template.model';
import { itemEffect, primaryItem } from '@domain/authorization/models/permission-template.model';
import { useGetSystemSuite } from '@app/authorization/hooks/use-system-suite';
import { DetailPanelShell, type DetailTab } from '@shared/components/DetailPanelShell';
import { StatusBadge } from '@shared/components/StatusBadge';
import { M3Button } from '@shared/components/M3Button';
import { KeyValueRow } from '@shared/components/KeyValueRow';
import {
  usePublishPermissionTemplate,
  useDeprecatePermissionTemplate,
  useDeletePermissionTemplate,
  useApplyTemplateItemEffect,
} from '@app/authorization/hooks/use-permission-template';
import type { SystemSuite } from '@domain/authorization/models/system-suite.model';
import type { PermissionTemplateItem } from '@domain/authorization/models/permission-template.model';

import { ModulePermissionsPanel, type ModulePermNode } from './tree/ModulePermissionsPanel';
import { DomainResourcesPanel, type DomainResourceNode } from './tree/DomainResourcesPanel';
import { SystemActionsPanel } from './tree/SystemActionsPanel';
import {
  getAscendantsWithTypes,
  getSiblingViewOptions,
} from '../../../../application/authorization/utils/permission-cascade';
import { useNotificationStore } from '@app/stores/notification.store';
import { STATUS_COLORS, getStatusLabel } from '@shared/utils/status-utils';

type TemplateTab = 'overview' | 'modules' | 'domain-resources' | 'actions';

interface Props {
  template: PermissionTemplateDetail | undefined;
  isLoading: boolean;
  onDeleted?: () => void;
}

const STATUS_COLOR_MAP = {
  Published: STATUS_COLORS.Published,
  Deprecated: STATUS_COLORS.Deprecated,
  Draft: STATUS_COLORS.Draft,
};

export const PermissionTemplateDetailPanel: React.FC<Props> = ({
  template,
  isLoading,
  onDeleted,
}) => {
  const [activeTab, setActiveTab] = useState<TemplateTab>('overview');
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
  const [selectedModuleNode, setSelectedModuleNode] = useState<ModulePermNode | null>(null);
  const [selectedDomainResourceNode, setSelectedDomainResourceNode] =
    useState<DomainResourceNode | null>(null);

  const publish = usePublishPermissionTemplate(template?.templateId ?? '');
  const deprecate = useDeprecatePermissionTemplate(template?.templateId ?? '');
  const deleteTpl = useDeletePermissionTemplate();

  const { data: suite, isLoading: loadingSuite } = useGetSystemSuite(
    template?.systemSuiteId ?? null
  );

  const isDraft = template?.status === 'Draft';
  const isPublished = template?.status === 'Published';

  const tabs: DetailTab<TemplateTab>[] = [
    { key: 'overview', label: 'General', icon: <Info className="w-3.5 h-3.5" /> },
    { key: 'modules', label: 'Módulos', icon: <Shield className="w-3.5 h-3.5" /> },
    {
      key: 'domain-resources',
      label: 'Recursos de Dominio',
      icon: <Database className="w-3.5 h-3.5" />,
    },
    { key: 'actions', label: 'Acciones de Sistema', icon: <Blocks className="w-3.5 h-3.5" /> },
  ];

  const handleDelete = useCallback(async () => {
    if (!template) return;
    await deleteTpl.mutateAsync(template.templateId);
    setShowDeleteConfirm(false);
    onDeleted?.();
  }, [template, deleteTpl, onDeleted]);

  const header = template ? (
    <div className="p-4 rounded-xl border border-m3-outline/20 bg-m3-surface-container/30 space-y-3">
      <div className="flex items-start justify-between gap-3">
        <div className="flex items-center gap-2.5 min-w-0">
          <div className="p-2 rounded-lg bg-m3-primary/10 shrink-0">
            <ShieldCheck className="w-5 h-5 text-m3-primary" />
          </div>
          <div className="min-w-0">
            <p className="text-sm font-bold text-m3-on-surface leading-tight">
              Plantilla <span className="text-m3-primary">v{template.version}</span>
            </p>
            <p className="text-[11px] text-m3-secondary mt-0.5">
              {template.items.length} {template.items.length === 1 ? 'ítem' : 'ítems'}
            </p>
          </div>
        </div>
        <StatusBadge
          status={template.status}
          label={getStatusLabel(template.status)}
          colorMap={STATUS_COLOR_MAP}
        />
      </div>

      <div className="flex flex-wrap gap-2">
        <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full bg-m3-surface-container/60 border border-m3-outline/20 text-[10px] text-m3-secondary">
          <span className="font-semibold text-[9px] uppercase tracking-wide">Rol</span>
          <span>{template.roleName}</span>
        </span>
        <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full bg-m3-surface-container/60 border border-m3-outline/20 text-[10px] text-m3-secondary">
          <span className="font-semibold text-[9px] uppercase tracking-wide">Suite</span>
          <span>{template.systemSuiteName}</span>
        </span>
      </div>

      <div className="flex items-center gap-2 flex-wrap pt-0.5">
        {isDraft && (
          <M3Button
            variant="tonal"
            icon={<SendHorizonal className="w-3.5 h-3.5" />}
            disabled={publish.isPending}
            onClick={() => publish.mutate()}
            className="text-xs h-8 px-3"
          >
            {publish.isPending ? 'Publicando…' : 'Publicar'}
          </M3Button>
        )}
        {isPublished && (
          <M3Button
            variant="outlined"
            icon={<Ban className="w-3.5 h-3.5" />}
            disabled={deprecate.isPending}
            onClick={() => deprecate.mutate()}
            className="text-xs h-8 px-3 border-rose-500/40 text-rose-500 hover:bg-rose-500/10"
          >
            {deprecate.isPending ? 'Descontinuando…' : 'Descontinuar'}
          </M3Button>
        )}

        <span className="flex-1" />

        {isDraft && !showDeleteConfirm && (
          <button
            type="button"
            onClick={() => setShowDeleteConfirm(true)}
            className="inline-flex items-center gap-1 text-[11px] text-m3-secondary/50 hover:text-rose-500 transition-colors px-2 py-1 rounded hover:bg-rose-500/10"
            title="Eliminar plantilla (solo Borradores)"
          >
            <Trash2 className="w-3.5 h-3.5" />
            Eliminar
          </button>
        )}

        {isDraft && showDeleteConfirm && (
          <div className="flex items-center gap-2 px-2 py-1 rounded-lg bg-rose-500/10 border border-rose-500/25 text-[11px]">
            <AlertTriangle className="w-3.5 h-3.5 text-rose-500 shrink-0" />
            <span className="text-rose-600 dark:text-rose-400 font-medium">
              ¿Eliminar esta plantilla?
            </span>
            <button
              type="button"
              onClick={() => setShowDeleteConfirm(false)}
              className="text-m3-secondary hover:text-m3-on-surface px-1.5 py-0.5 rounded hover:bg-m3-surface-variant/20 transition-colors"
            >
              No
            </button>
            <button
              type="button"
              onClick={handleDelete}
              disabled={deleteTpl.isPending}
              className="text-rose-600 font-semibold px-1.5 py-0.5 rounded bg-rose-500/15 hover:bg-rose-500/25 transition-colors disabled:opacity-50"
            >
              {deleteTpl.isPending ? 'Eliminando…' : 'Sí, eliminar'}
            </button>
          </div>
        )}
      </div>
    </div>
  ) : undefined;

  const overviewContent = template ? (
    <dl className="space-y-0.5">
      <KeyValueRow label="Estado" value={getStatusLabel(template.status)} />
      <KeyValueRow label="Versión" value={`v${template.version}`} />
      <KeyValueRow label="Ítems" value={String(template.items.length)} />
      <KeyValueRow label="Rol" value={template.roleName} />
      <KeyValueRow label="Suite" value={template.systemSuiteName} bordered={false} />
    </dl>
  ) : null;

  const modulesContent = template ? (
    <div className="flex h-[500px] -mx-4 -mb-4 overflow-hidden rounded-b-xl border-t border-m3-outline/20">
      <div className="w-[50%] h-full border-r border-m3-outline/20">
        {loadingSuite ? (
          <div className="flex items-center justify-center h-full text-m3-secondary/50 text-xs">
            Cargando estructura...
          </div>
        ) : (
          <ModulePermissionsPanel
            suite={suite}
            items={template.items}
            templateId={template.templateId}
            isDraft={isDraft}
            onNodeSelect={setSelectedModuleNode}
            selectedNodeId={selectedModuleNode?.id ?? null}
          />
        )}
      </div>
      <div className="w-[50%] h-full bg-m3-surface-container/10 overflow-y-auto">
        {selectedModuleNode ? (
          <ModuleNodeDetailPanel
            node={selectedModuleNode}
            suite={suite}
            templateId={template?.templateId ?? ''}
            isDraft={isDraft}
            allItems={template?.items ?? []}
          />
        ) : (
          <div className="flex flex-col items-center justify-center h-full text-m3-secondary/50 p-6 text-center">
            <Shield className="w-12 h-12 mb-3 opacity-20" />
            <p className="text-sm font-medium">Seleccione un elemento del árbol</p>
            <p className="text-xs mt-1 max-w-[250px]">
              Explore la jerarquía de módulos, menús, submenús y opciones para configurar permisos.
            </p>
          </div>
        )}
      </div>
    </div>
  ) : null;

  const domainResourcesContent = template ? (
    <div className="flex h-[500px] -mx-4 -mb-4 overflow-hidden rounded-b-xl border-t border-m3-outline/20">
      <div className="w-[50%] h-full border-r border-m3-outline/20">
        {loadingSuite ? (
          <div className="flex items-center justify-center h-full text-m3-secondary/50 text-xs">
            Cargando recursos...
          </div>
        ) : (
          <DomainResourcesPanel
            suite={suite}
            items={template.items}
            templateId={template.templateId}
            isDraft={isDraft}
            onResourceSelect={setSelectedDomainResourceNode}
            selectedNodeId={selectedDomainResourceNode?.id ?? null}
          />
        )}
      </div>
      <div className="w-[50%] h-full bg-m3-surface-container/10 overflow-y-auto">
        {selectedDomainResourceNode && suite ? (
          <DomainResourceDetailPanel
            node={selectedDomainResourceNode}
            suite={suite}
            items={template.items}
            templateId={template.templateId}
            isDraft={isDraft}
          />
        ) : (
          <div className="flex flex-col items-center justify-center h-full text-m3-secondary/50 p-6 text-center">
            <Database className="w-12 h-12 mb-3 opacity-20" />
            <p className="text-sm font-medium">Seleccione un recurso de dominio</p>
            <p className="text-xs mt-1 max-w-[250px]">
              Los recursos de dominio (agregados y entidades) permiten controlar el acceso a nivel
              de datos con operaciones CRUD y acciones personalizadas.
            </p>
          </div>
        )}
      </div>
    </div>
  ) : null;

  const actionsContent = template ? (
    <div className="min-h-[300px]">
      <SystemActionsPanel
        suite={suite}
        items={template.items}
        templateId={template.templateId}
        isDraft={isDraft}
      />
    </div>
  ) : null;

  return (
    <DetailPanelShell<TemplateTab>
      isLoading={isLoading}
      isEmpty={!template}
      loadingLabel="Cargando plantilla…"
      emptyLabel="Seleccione una plantilla en el panel izquierdo para ver sus detalles."
      header={header}
      headerCollapsible
      tabs={tabs}
      activeTab={activeTab}
      onTabChange={tab => {
        setActiveTab(tab);
        setShowDeleteConfirm(false);
      }}
      entityKey={template?.templateId}
    >
      {activeTab === 'overview' && overviewContent}
      {activeTab === 'modules' && modulesContent}
      {activeTab === 'domain-resources' && domainResourcesContent}
      {activeTab === 'actions' && actionsContent}
    </DetailPanelShell>
  );
};

// ─── Module Node Detail Panel ─────────────────────────────────────────────────

interface ModuleNodeDetailPanelProps {
  node: ModulePermNode;
  suite: SystemSuite | undefined | null;
  templateId: string;
  isDraft: boolean;
  allItems: PermissionTemplateItem[];
}

const ModuleNodeDetailPanel: React.FC<ModuleNodeDetailPanelProps> = ({
  node,
  suite,
  templateId,
  isDraft,
  allItems,
}) => {
  const { applyEffect, isPending } = useApplyTemplateItemEffect(templateId);
  const addNotification = useNotificationStore(s => s.addNotification);

  // Manda la fila vigente; la retirada solo se conserva para poder reactivarla (ADR-0164).
  const selfItem = primaryItem(node.items);
  const selfEffect: PermissionEffect = selfItem ? itemEffect(selfItem) : 'Neutral';

  const computeNodeEffectiveState = (): 'Allow' | 'Deny' | 'Neutral' => {
    const selfEffects = node.items.map(itemEffect);
    const hasAllow = selfEffects.includes('Allow');
    const hasDeny = selfEffects.includes('Deny');
    if (hasAllow) return 'Allow';
    if (hasDeny) return 'Deny';
    return 'Neutral';
  };
  const effectiveState = computeNodeEffectiveState();

  const StateIcon =
    effectiveState === 'Allow' ? CheckCircle2 : effectiveState === 'Deny' ? XCircle : MinusCircle;

  const stateColor =
    effectiveState === 'Allow'
      ? 'text-emerald-500 bg-emerald-500/10'
      : effectiveState === 'Deny'
        ? 'text-rose-500 bg-rose-500/10'
        : 'text-m3-secondary bg-m3-surface-variant';

  const stateLabel =
    effectiveState === 'Allow'
      ? 'Permitido'
      : effectiveState === 'Deny'
        ? 'Denegado'
        : 'No configurado (Heredado)';

  const mapTypeToTarget = (type: string): ExclusiveArcTarget => {
    if (type === 'Module') return 'Module';
    if (type === 'Menu') return 'Submodule';
    return 'Option';
  };

  const resolveActionId = (): string => {
    const byCode = node.actionCode
      ? suite?.actions.find(a => a.code === node.actionCode)?.id
      : undefined;
    return byCode ?? suite?.actions[0]?.id ?? '00000000-0000-0000-0000-000000000000';
  };

  const handleApplyEffect = async (effect: PermissionEffect) => {
    if (!isDraft) return;
    // El adaptador elige el verbo: alta, efecto o reactivación del ítem retirado (ADR-0164).
    await applyEffect({
      effect,
      item: selfItem,
      target: {
        targetType: mapTypeToTarget(node.type),
        targetId: node.id,
        actionId: resolveActionId(),
      },
    });

    if (effect === 'Allow' && suite) {
      const ascendants = getAscendantsWithTypes(suite, node.id);
      const siblingViews = getSiblingViewOptions(suite, node.id);
      let changedParents = false;
      const elementsToProcess = [...ascendants, ...siblingViews];
      for (const asc of elementsToProcess) {
        // Los ascendientes pasan por el mismo adaptador: si alguno está retirado hay que
        // reactivarlo, porque volver a darlo de alta chocaría con su clave (409).
        const parentItem = primaryItem(allItems.filter(i => i.targetId === asc.id));
        const changed = await applyEffect({
          effect: 'Allow',
          item: parentItem,
          target: {
            targetType: asc.type,
            targetId: asc.id,
            actionId: '00000000-0000-0000-0000-000000000000',
          },
        });
        changedParents = changedParents || changed;
      }
      if (changedParents) {
        addNotification({
          title: 'Jerarquía actualizada',
          message: 'Se habilitaron automáticamente las lecturas y niveles superiores.',
          type: 'info',
        });
      }
    }
  };

  return (
    <div className="p-5">
      <div className="mb-6">
        <h3 className="text-sm font-bold text-m3-on-surface flex items-center gap-2">
          {node.label}
          <span
            className={`text-[10px] font-semibold uppercase px-2.5 py-0.5 rounded-full border border-current ${stateColor}`}
          >
            {node.type}
          </span>
        </h3>
        {node.description && <p className="text-xs text-m3-secondary mt-1.5">{node.description}</p>}
        <p className="text-[10px] font-mono text-m3-secondary/60 mt-1">{node.code}</p>
      </div>

      <div className="p-4 rounded-xl border border-m3-outline/20 bg-m3-surface-container/30 mb-6 flex items-start gap-3">
        <StateIcon className={`w-6 h-6 shrink-0 mt-0.5 ${stateColor.split(' ')[0]}`} />
        <div>
          <p className="text-xs font-bold text-m3-on-surface">Estado efectivo: {stateLabel}</p>
          <p className="text-[11px] text-m3-secondary mt-1 leading-relaxed">
            {effectiveState === 'Partial'
              ? 'Algunos elementos secundarios tienen configuraciones diferentes.'
              : effectiveState === 'Neutral'
                ? 'No hay reglas directas. El acceso depende de contenedores superiores.'
                : `El acceso está ${effectiveState === 'Allow' ? 'permitido' : 'denegado'}.`}
          </p>
        </div>
      </div>

      <div className="space-y-3">
        <h4 className="text-xs font-bold uppercase tracking-wider text-m3-secondary">
          Regla Directa
        </h4>

        {!isDraft && (
          <div className="flex items-center gap-2 text-[11px] text-m3-secondary/70 bg-m3-surface-variant/30 p-2 rounded">
            <Info className="w-3.5 h-3.5" />
            Solo puedes modificar reglas en plantillas Borrador.
          </div>
        )}

        <div className="flex gap-2">
          <button
            onClick={() => handleApplyEffect('Allow')}
            disabled={!isDraft || isPending}
            className={`flex-1 py-2 px-3 rounded-lg border text-xs font-semibold flex items-center justify-center gap-2 transition-colors disabled:opacity-50
              ${
                selfEffect === 'Allow'
                  ? 'bg-emerald-500/15 border-emerald-500/30 text-emerald-600'
                  : 'bg-transparent border-m3-outline/20 text-m3-secondary hover:border-emerald-500/30 hover:text-emerald-600'
              }`}
          >
            <CheckCircle2 className="w-4 h-4" /> Permitir
          </button>
          <button
            onClick={() => handleApplyEffect('Deny')}
            disabled={!isDraft || isPending}
            className={`flex-1 py-2 px-3 rounded-lg border text-xs font-semibold flex items-center justify-center gap-2 transition-colors disabled:opacity-50
              ${
                selfEffect === 'Deny'
                  ? 'bg-rose-500/15 border-rose-500/30 text-rose-600'
                  : 'bg-transparent border-m3-outline/20 text-m3-secondary hover:border-rose-500/30 hover:text-rose-600'
              }`}
          >
            <XCircle className="w-4 h-4" /> Denegar
          </button>
          <button
            onClick={() => handleApplyEffect('Neutral')}
            disabled={!isDraft || selfEffect === 'Neutral' || isPending}
            className={`flex-1 py-2 px-3 rounded-lg border text-xs font-semibold flex items-center justify-center gap-2 transition-colors disabled:opacity-50
              ${
                selfEffect === 'Neutral'
                  ? 'bg-m3-surface-variant border-m3-outline/20 text-m3-on-surface/80'
                  : 'bg-transparent border-m3-outline/20 text-m3-secondary hover:border-m3-outline/40 hover:text-m3-on-surface'
              }`}
          >
            <MinusCircle className="w-4 h-4" /> Heredar
          </button>
        </div>
      </div>
    </div>
  );
};

// ─── Domain Resource Detail Panel ─────────────────────────────────────────────

interface DomainResourceDetailPanelProps {
  node: DomainResourceNode;
  suite: SystemSuite;
  items: PermissionTemplateItem[];
  templateId: string;
  isDraft: boolean;
}

const DomainResourceDetailPanel: React.FC<DomainResourceDetailPanelProps> = ({
  node,
  suite,
  templateId,
  isDraft,
}) => {
  const { applyEffect, isPending } = useApplyTemplateItemEffect(templateId);

  const selfItem = primaryItem(node.items);
  const selfEffect: PermissionEffect = selfItem ? itemEffect(selfItem) : 'Neutral';
  const effects = node.items.map(itemEffect);
  const effectiveState =
    effects.includes('Allow') && !effects.includes('Deny')
      ? 'Allow'
      : effects.includes('Deny') && !effects.includes('Allow')
        ? 'Deny'
        : 'Neutral';

  const StateIcon =
    effectiveState === 'Allow' ? CheckCircle2 : effectiveState === 'Deny' ? XCircle : MinusCircle;
  const stateColor =
    effectiveState === 'Allow'
      ? 'text-emerald-500 bg-emerald-500/10'
      : effectiveState === 'Deny'
        ? 'text-rose-500 bg-rose-500/10'
        : 'text-m3-secondary bg-m3-surface-variant';
  const stateLabel =
    effectiveState === 'Allow'
      ? 'Permitido'
      : effectiveState === 'Deny'
        ? 'Denegado'
        : 'No configurado';

  const getIcon = () => {
    if (node.type === 'Aggregate') return Layers;
    if (node.type === 'Entity') return Component;
    return Component;
  };
  const Icon = getIcon();

  const mapTypeToTarget = (type: string): ExclusiveArcTarget => {
    if (type === 'Aggregate') return 'Aggregate';
    if (type === 'Entity') return 'Entity';
    return 'Entity';
  };

  const handleApplyEffect = async (effect: PermissionEffect) => {
    if (!isDraft) return;
    // Mismo adaptador que el resto de la pantalla: Neutral no borra y lo retirado se reactiva.
    await applyEffect({
      effect,
      item: selfItem,
      target: {
        targetType: mapTypeToTarget(node.type),
        targetId: node.id,
        actionId: suite.actions[0]?.id || '00000000-0000-0000-0000-000000000000',
      },
    });
  };

  const parentResource = node.parentId
    ? suite.domainResources?.find(r => r.id === node.parentId)
    : null;

  return (
    <div className="p-5">
      <div className="mb-6">
        <div className="flex items-center gap-2 mb-1">
          <div
            className={`p-1.5 rounded ${
              node.type === 'Aggregate'
                ? 'bg-purple-500/10 text-purple-500'
                : node.type === 'Entity'
                  ? 'bg-emerald-500/10 text-emerald-500'
                  : node.type === 'CrudOperation'
                    ? 'bg-blue-500/10 text-blue-500'
                    : 'bg-amber-500/10 text-amber-500'
            }`}
          >
            <Icon className="w-4 h-4" />
          </div>
          <h3 className="text-sm font-bold text-m3-on-surface">{node.label}</h3>
        </div>
        <p className="text-[10px] font-mono text-m3-secondary/60">{node.code}</p>
        {node.description && <p className="text-xs text-m3-secondary mt-1.5">{node.description}</p>}
        {parentResource && (
          <p className="text-[10px] text-m3-secondary mt-1">
            Recurso padre: <span className="font-semibold">{parentResource.name}</span>
            <span
              className={`ml-1 text-[8px] font-bold uppercase px-1 py-0.5 rounded ${
                parentResource.type === 'Aggregate'
                  ? 'bg-purple-500/10 text-purple-500'
                  : 'bg-emerald-500/10 text-emerald-500'
              }`}
            >
              {parentResource.type}
            </span>
          </p>
        )}
      </div>

      <div className="p-4 rounded-xl border border-m3-outline/20 bg-m3-surface-container/30 mb-6 flex items-start gap-3">
        <StateIcon className={`w-6 h-6 shrink-0 mt-0.5 ${stateColor.split(' ')[0]}`} />
        <div>
          <p className="text-xs font-bold text-m3-on-surface">Estado: {stateLabel}</p>
          <p className="text-[11px] text-m3-secondary mt-1">
            {effectiveState === 'Neutral'
              ? 'No hay reglas asignadas. El acceso dependerá de permisos superiores.'
              : `El acceso a este recurso está ${effectiveState === 'Allow' ? 'permitido' : 'denegado'}.`}
          </p>
        </div>
      </div>

      <div className="space-y-3">
        <h4 className="text-xs font-bold uppercase tracking-wider text-m3-secondary">
          Regla Directa
        </h4>
        {!isDraft && (
          <div className="flex items-center gap-2 text-[11px] text-m3-secondary/70 bg-m3-surface-variant/30 p-2 rounded">
            <Info className="w-3.5 h-3.5" />
            Solo puedes modificar reglas en plantillas Borrador.
          </div>
        )}
        <div className="flex gap-2">
          <button
            onClick={() => handleApplyEffect('Allow')}
            disabled={!isDraft || isPending}
            className={`flex-1 py-2 px-3 rounded-lg border text-xs font-semibold flex items-center justify-center gap-2 transition-colors disabled:opacity-50
              ${selfEffect === 'Allow' ? 'bg-emerald-500/15 border-emerald-500/30 text-emerald-600' : 'bg-transparent border-m3-outline/20 text-m3-secondary hover:border-emerald-500/30 hover:text-emerald-600'}`}
          >
            <CheckCircle2 className="w-4 h-4" /> Permitir
          </button>
          <button
            onClick={() => handleApplyEffect('Deny')}
            disabled={!isDraft || isPending}
            className={`flex-1 py-2 px-3 rounded-lg border text-xs font-semibold flex items-center justify-center gap-2 transition-colors disabled:opacity-50
              ${selfEffect === 'Deny' ? 'bg-rose-500/15 border-rose-500/30 text-rose-600' : 'bg-transparent border-m3-outline/20 text-m3-secondary hover:border-rose-500/30 hover:text-rose-600'}`}
          >
            <XCircle className="w-4 h-4" /> Denegar
          </button>
          <button
            onClick={() => handleApplyEffect('Neutral')}
            disabled={!isDraft || selfEffect === 'Neutral' || isPending}
            className={`flex-1 py-2 px-3 rounded-lg border text-xs font-semibold flex items-center justify-center gap-2 transition-colors disabled:opacity-50
              ${selfEffect === 'Neutral' ? 'bg-m3-surface-variant border-m3-outline/20 text-m3-on-surface/80' : 'bg-transparent border-m3-outline/20 text-m3-secondary hover:border-m3-outline/40 hover:text-m3-on-surface'}`}
          >
            <MinusCircle className="w-4 h-4" /> Heredar
          </button>
        </div>
      </div>
    </div>
  );
};
