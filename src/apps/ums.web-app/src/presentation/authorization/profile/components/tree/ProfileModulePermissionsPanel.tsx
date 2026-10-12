import React, { useState, useMemo } from 'react';
import {
  Shield,
  Menu,
  List,
  Key,
  ChevronRight,
  ChevronDown,
  CheckCircle2,
  XCircle,
  MinusCircle,
} from 'lucide-react';
import type { SystemSuite } from '@domain/authorization/models/system-suite.model';
import type { SystemSuiteNode } from '@domain/authorization/schemas/system-suite.schema';
import { PermissionSectionToolbar } from '@shared/components/PermissionSectionToolbar';
import { CodeBadge } from '@shared/components/CodeBadge';

export type ProfilePermItem = {
  targetType: string;
  targetId: string;
  targetName: string;
  actionId: string;
  actionName: string;
  isAllowed: boolean;
  isDenied: boolean;
  isActive: boolean;
  isOverride?: boolean;
  permissionId?: string;
  originalFromTemplate?: any;
};

export type ModulePermNode = {
  id: string;
  type: 'Module' | 'Menu' | 'SubMenu' | 'Option';
  label: string;
  code: string;
  description: string;
  actionCode?: string;
  children: ModulePermNode[];
  items: ProfilePermItem[];
  level: number;
};

interface ProfileModulePermissionsPanelProps {
  suite: SystemSuite | undefined | null;
  items: ProfilePermItem[];
  renderActions?: (node: ModulePermNode) => React.ReactNode;
}

function itemEffect(
  item: Pick<ProfilePermItem, 'isAllowed' | 'isDenied'>
): 'Allow' | 'Deny' | 'Neutral' {
  if (item.isAllowed) return 'Allow';
  if (item.isDenied) return 'Deny';
  return 'Neutral';
}

function buildModuleTree(
  suite: SystemSuite | undefined | null,
  items: ProfilePermItem[]
): ModulePermNode[] {
  if (!suite) return [];
  const itemsByTargetId = items.reduce(
    (acc, item) => {
      if (!acc[item.targetId]) acc[item.targetId] = [];
      acc[item.targetId].push(item);
      return acc;
    },
    {} as Record<string, ProfilePermItem[]>
  );

  // Mapea recursivamente los nodos del árbol del SystemSuite (ADR-0090).
  const buildNodeTree = (nodes: SystemSuiteNode[], level: number): ModulePermNode[] =>
    [...nodes]
      .sort((a, b) => a.sortOrder - b.sortOrder)
      .map(node => ({
        id: node.id,
        type: node.kind as ModulePermNode['type'],
        label: node.label,
        code: node.code,
        description: node.description,
        actionCode: node.actionCodes[0],
        level,
        items: itemsByTargetId[node.id] || [],
        children: buildNodeTree(node.children ?? [], level + 1),
      }));

  return suite.modules.map(mod => ({
    id: mod.id,
    type: 'Module' as const,
    label: mod.name,
    code: mod.code,
    description: mod.description,
    level: 0,
    items: itemsByTargetId[mod.id] || [],
    children: buildNodeTree(mod.nodes ?? [], 1),
  }));
}

function flattenTree(nodes: ModulePermNode[]): ModulePermNode[] {
  const result: ModulePermNode[] = [];
  const walk = (ns: ModulePermNode[]) => {
    for (const n of ns) {
      result.push(n);
      if (n.children.length > 0) walk(n.children);
    }
  };
  walk(nodes);
  return result;
}

function getAllIds(nodes: ModulePermNode[]): string[] {
  return nodes.flatMap(n => [n.id, ...getAllIds(n.children)]);
}

function computeNodeState(node: ModulePermNode): 'Allow' | 'Deny' | 'Neutral' {
  const selfEffects = node.items.map(itemEffect);
  const hasAllow = selfEffects.includes('Allow');
  const hasDeny = selfEffects.includes('Deny');

  let childAllows = 0;
  let childDenies = 0;
  const check = (n: ModulePermNode) => {
    n.items.forEach(i => {
      const e = itemEffect(i);
      if (e === 'Allow') childAllows++;
      if (e === 'Deny') childDenies++;
    });
    n.children.forEach(check);
  };
  check(node);
  const totalAllows = (hasAllow ? 1 : 0) + childAllows;
  const totalDenies = (hasDeny ? 1 : 0) + childDenies;
  if (totalAllows > 0) return 'Allow';
  if (totalDenies > 0) return 'Deny';
  return 'Neutral';
}

const TYPE_ICON: Record<string, React.ReactNode> = {
  Module: <Shield className="w-3 h-3" />,
  Menu: <Menu className="w-3 h-3" />,
  SubMenu: <List className="w-3 h-3" />,
  Option: <Key className="w-3 h-3" />,
};

const TYPE_COLOR: Record<string, string> = {
  Module: 'bg-indigo-500/10 text-indigo-500',
  Menu: 'bg-blue-500/10 text-blue-500',
  SubMenu: 'bg-sky-500/10 text-sky-500',
  Option: 'bg-slate-500/10 text-slate-500',
};

const STATE_ICON: Record<string, { icon: React.ReactNode; color: string }> = {
  Allow: { icon: <CheckCircle2 className="w-3.5 h-3.5" />, color: 'text-emerald-500' },
  Deny: { icon: <XCircle className="w-3.5 h-3.5" />, color: 'text-rose-500' },
  Neutral: { icon: <MinusCircle className="w-3.5 h-3.5" />, color: 'text-m3-secondary/30' },
};

const ModuleRow: React.FC<{
  node: ModulePermNode;
  isExpanded: boolean;
  hasChildren: boolean;
  onToggle: () => void;
  renderActions?: (node: ModulePermNode) => React.ReactNode;
  renderExpandedContent?: (node: ModulePermNode) => React.ReactNode;
}> = ({ node, isExpanded, hasChildren, onToggle, renderActions, renderExpandedContent }) => {
  const state = computeNodeState(node);
  const stateInfo = STATE_ICON[state];

  const hasDirectPermission = node.items.length > 0;
  const isInherited = !hasDirectPermission && state !== 'Neutral';

  const hasOverride = node.items.some(i => i.isOverride);
  const hasInactive = node.items.some(i => !i.isActive);

  return (
    <div className="flex flex-col">
      <div
        className="flex items-center justify-between gap-3 py-1.5 px-2 rounded-md transition-colors hover:bg-m3-surface-container/50 border-b border-m3-outline/5"
        style={{ paddingLeft: `${node.level * 16 + 4}px` }}
      >
        <div className="flex items-center gap-2 flex-1 min-w-0">
          <div
            className="w-5 h-5 flex items-center justify-center shrink-0 cursor-pointer text-m3-secondary/50 hover:text-m3-on-surface"
            onClick={e => {
              e.stopPropagation();
              hasChildren && onToggle();
            }}
          >
            {hasChildren &&
              (isExpanded ? (
                <ChevronDown className="w-3.5 h-3.5" />
              ) : (
                <ChevronRight className="w-3.5 h-3.5" />
              ))}
          </div>

          {node.level > 0 && (
            <div className="w-3 h-3 flex items-center justify-center shrink-0">
              <div className="w-px h-3 bg-m3-outline/30" />
            </div>
          )}

          <div className={`p-1 rounded shrink-0 ${TYPE_COLOR[node.type]}`}>
            {TYPE_ICON[node.type]}
          </div>

          <div className="flex flex-col min-w-0">
            <div className="flex items-center gap-2">
              <span className="text-xs truncate font-medium text-m3-on-surface/90">
                {node.label}
              </span>
              {hasOverride && (
                <span className="text-[8px] font-bold uppercase px-1 py-0.5 rounded bg-amber-500/10 text-amber-500 border border-amber-500/20 shrink-0">
                  Override
                </span>
              )}
              {hasInactive && (
                <span className="text-[8px] font-bold uppercase px-1 py-0.5 rounded bg-rose-500/10 text-rose-500 border border-rose-500/20 shrink-0">
                  Inactivo
                </span>
              )}
              <div className="flex items-center gap-1 shrink-0">
                {isInherited && (
                  <span className="text-[8px] text-m3-secondary/60 italic" title="Heredado">
                    (H)
                  </span>
                )}
                <div className={stateInfo.color}>{stateInfo.icon}</div>
              </div>
            </div>
            <div className="flex items-center gap-2 mt-0.5">
              <CodeBadge code={node.code} size="xs" />
              <span className="text-[10px] text-m3-secondary/60 truncate">{node.description}</span>
            </div>
          </div>
        </div>

        {renderActions && (
          <div className="shrink-0 flex items-center gap-2 ml-4">{renderActions(node)}</div>
        )}
      </div>
      {renderExpandedContent && renderExpandedContent(node)}
    </div>
  );
};

export const ProfileModulePermissionsPanel: React.FC<
  ProfileModulePermissionsPanelProps & {
    renderExpandedContent?: (node: ModulePermNode) => React.ReactNode;
  }
> = ({ suite, items, renderActions, renderExpandedContent }) => {
  const [viewMode, setViewMode] = useState<'list' | 'tree'>('tree');
  const [activeFilter, setActiveFilter] = useState('all');
  const [sortBy, setSortBy] = useState('order');
  const [sortOrder, setSortOrder] = useState<'asc' | 'desc'>('asc');
  const [expandedNodes, setExpandedNodes] = useState<Set<string>>(new Set());

  const tree = useMemo(() => buildModuleTree(suite, items), [suite, items]);
  const flatList = useMemo(() => flattenTree(tree), [tree]);
  const allIds = useMemo(() => getAllIds(tree), [tree]);

  const anyCollapsed = allIds.some(id => !expandedNodes.has(id));
  const allExpanded = !anyCollapsed && allIds.length > 0;

  const toggleNode = (id: string) => {
    setExpandedNodes(prev => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  };

  const toggleAll = () => {
    if (allExpanded) setExpandedNodes(new Set());
    else setExpandedNodes(new Set(allIds));
  };

  const renderTree = (nodes: ModulePermNode[]) =>
    nodes.map(node => {
      const isExpanded = expandedNodes.has(node.id);
      const hasChildren = node.children.length > 0;
      return (
        <div key={node.id}>
          <ModuleRow
            node={node}
            isExpanded={isExpanded}
            hasChildren={hasChildren}
            onToggle={() => toggleNode(node.id)}
            renderActions={renderActions}
            renderExpandedContent={renderExpandedContent}
          />
          {isExpanded && hasChildren && renderTree(node.children)}
        </div>
      );
    });

  const renderList = () => (
    <div className="flex flex-col gap-0.5">
      {flatList.map(node => (
        <ModuleRow
          key={node.id}
          node={node}
          isExpanded={false}
          hasChildren={false}
          onToggle={() => {}}
          renderActions={renderActions}
          renderExpandedContent={renderExpandedContent}
        />
      ))}
    </div>
  );

  return (
    <div className="flex flex-col h-full w-full">
      <PermissionSectionToolbar
        viewMode={viewMode}
        onViewModeChange={m => setViewMode(m as any)}
        filterOptions={[
          { label: 'Todos', value: 'all' },
          { label: 'Módulos', value: 'Module' },
          { label: 'Menús', value: 'Menu' },
          { label: 'Submenús', value: 'SubMenu' },
          { label: 'Opciones', value: 'Option' },
        ]}
        activeFilter={activeFilter}
        onFilterChange={setActiveFilter}
        sortOptions={[
          { label: 'Jerarquía', value: 'order' },
          { label: 'Nombre', value: 'name' },
          { label: 'Código', value: 'code' },
        ]}
        sortBy={sortBy}
        onSortByChange={setSortBy}
        sortOrder={sortOrder}
        onSortOrderToggle={() => setSortOrder(o => (o === 'asc' ? 'desc' : 'asc'))}
        itemCount={flatList.length}
        itemLabel="elemento"
        showExpandCollapse
        allExpanded={allExpanded}
        onToggleExpandAll={toggleAll}
      />

      <div className="flex items-center justify-between px-2 py-1.5 bg-m3-surface-container/20 border-b border-m3-outline/10">
        <div className="flex items-center gap-3 text-[10px] text-m3-secondary">
          <span className="font-semibold uppercase tracking-wider text-m3-on-surface/80">
            Leyenda:
          </span>
          <span className="flex items-center gap-1">
            <span className="text-emerald-500">
              <CheckCircle2 className="w-3.5 h-3.5 inline" />
            </span>{' '}
            Permitido
          </span>
          <span className="flex items-center gap-1">
            <span className="text-rose-500">
              <XCircle className="w-3.5 h-3.5 inline" />
            </span>{' '}
            Denegado
          </span>
          <span className="flex items-center gap-1">
            <span className="text-m3-secondary/40">
              <MinusCircle className="w-3.5 h-3.5 inline" />
            </span>{' '}
            Neutro/Heredado
          </span>
          <span className="text-m3-secondary/50 italic">(H) = Heredado</span>
        </div>
      </div>

      <div className="flex-1 overflow-y-auto px-1 py-2">
        {flatList.length === 0 ? (
          <div className="flex flex-col items-center justify-center py-12 text-center">
            <Shield className="w-10 h-10 text-m3-secondary/20 mb-3" />
            <p className="text-xs text-m3-secondary font-medium">
              No hay estructura configurada en esta suite.
            </p>
          </div>
        ) : viewMode === 'tree' ? (
          <div className="space-y-0.5 pb-4">{renderTree(tree)}</div>
        ) : (
          <div className="space-y-0.5 pb-4">{renderList()}</div>
        )}
      </div>
    </div>
  );
};
