import React, { useState, useMemo } from 'react';
import {
  Layers,
  Component,
  CheckCircle2,
  XCircle,
  MinusCircle,
  Database,
  ChevronRight,
  ChevronDown,
  Plus,
  Eye,
  ListFilter,
  Pencil,
  Trash2,
  Zap,
  FunctionSquare,
} from 'lucide-react';
import type { SystemSuite } from '@domain/authorization/models/system-suite.model';
import type { ProfilePermission } from '@domain/authorization/models/profile.model';
import { PermissionSectionToolbar } from '@shared/components/PermissionSectionToolbar';
import { CodeBadge } from '@shared/components/CodeBadge';

export type DomainResourceNode = {
  id: string;
  type: 'Aggregate' | 'Entity' | 'DomainMethod' | 'CrudOperation' | 'CustomAction';
  label: string;
  code: string;
  description: string;
  parentId?: string;
  parentType?: 'Aggregate' | 'Entity' | 'DomainMethod';
  children: DomainResourceNode[];
  permissions: ProfilePermission[];
  level: number;
};

interface ProfileDomainResourcesPanelProps {
  suite: SystemSuite | undefined | null;
  permissions: ProfilePermission[];
  renderInlineActions: (node: DomainResourceNode) => React.ReactNode;
}

const CRUD_OPERATIONS: Omit<SystemSuiteCrudOperation, 'id'>[] = [
  { code: 'Create', name: 'Create', description: 'Create new records', sortOrder: 1 },
  { code: 'Read', name: 'Read/Get', description: 'Read single record', sortOrder: 2 },
  { code: 'Search', name: 'Search/List', description: 'Search and list records', sortOrder: 3 },
  { code: 'Update', name: 'Update', description: 'Update existing records', sortOrder: 4 },
  {
    code: 'Delete',
    name: 'Delete/Deactivate',
    description: 'Delete or deactivate records',
    sortOrder: 5,
  },
];

const CRUD_ICON: Record<string, React.ReactNode> = {
  Create: <Plus className="w-3 h-3" />,
  Read: <Eye className="w-3 h-3" />,
  Search: <ListFilter className="w-3 h-3" />,
  Update: <Pencil className="w-3 h-3" />,
  Delete: <Trash2 className="w-3 h-3" />,
};

const CRUD_COLOR: Record<string, string> = {
  Create: 'bg-emerald-500/10 text-emerald-500',
  Read: 'bg-blue-500/10 text-blue-500',
  Search: 'bg-sky-500/10 text-sky-500',
  Update: 'bg-amber-500/10 text-amber-500',
  Delete: 'bg-rose-500/10 text-rose-500',
};

function buildDomainResourceTree(
  suite: SystemSuite | undefined | null,
  permissions: ProfilePermission[]
): DomainResourceNode[] {
  if (!suite) return [];

  const permissionsByTargetId = permissions.reduce(
    (acc, p) => {
      if (!acc[p.targetId]) acc[p.targetId] = [];
      acc[p.targetId].push(p);
      return acc;
    },
    {} as Record<string, ProfilePermission[]>
  );

  const resources = suite.domainResources ?? [];

  const buildLeafChildren = (
    resourceId: string,
    resourceCode: string,
    resourceType: 'Aggregate' | 'Entity' | 'DomainMethod',
    baseLevel: number
  ): DomainResourceNode[] => {
    const crudOps: DomainResourceNode[] = CRUD_OPERATIONS.map(op => ({
      id: `${resourceId}:crud:${op.code}`,
      type: 'CrudOperation' as const,
      label: op.name,
      code: `${resourceCode}.${op.code}`,
      description: op.description,
      parentId: resourceId,
      parentType: resourceType,
      level: baseLevel,
      permissions: permissionsByTargetId[`${resourceId}:crud:${op.code}`] || [],
      children: [],
    }));

    const domainMethods: DomainResourceNode[] = resources
      .filter(r => r.type === 'DomainMethod' && r.parentResourceId === resourceId)
      .map(r => ({
        id: r.id,
        type: 'DomainMethod' as const,
        label: r.name,
        code: r.code,
        description: r.description,
        parentId: resourceId,
        parentType: resourceType,
        level: baseLevel,
        permissions: permissionsByTargetId[r.id] || [],
        children: [],
      }));

    return [...crudOps, ...domainMethods];
  };

  const rootResources = resources.filter(r => !r.parentResourceId && r.type !== 'DomainMethod');

  return rootResources.map(resource => {
    const childEntities: DomainResourceNode[] = resources
      .filter(r => r.parentResourceId === resource.id && r.type === 'Entity')
      .map(child => ({
        id: child.id,
        type: 'Entity' as const,
        label: child.name,
        code: child.code,
        description: child.description,
        parentId: resource.id,
        parentType: resource.type as 'Aggregate' | 'Entity' | 'DomainMethod',
        level: 1,
        permissions: permissionsByTargetId[child.id] || [],
        children: buildLeafChildren(child.id, child.code, 'Entity', 2),
      }));

    const ownLeaves = buildLeafChildren(
      resource.id,
      resource.code,
      resource.type as 'Aggregate' | 'Entity' | 'DomainMethod',
      1
    );

    return {
      id: resource.id,
      type: resource.type as 'Aggregate' | 'Entity' | 'DomainMethod',
      label: resource.name,
      code: resource.code,
      description: resource.description,
      level: 0,
      permissions: permissionsByTargetId[resource.id] || [],
      children: [...childEntities, ...ownLeaves],
    };
  });
}

function flattenTree(nodes: DomainResourceNode[]): DomainResourceNode[] {
  const result: DomainResourceNode[] = [];
  const walk = (ns: DomainResourceNode[]) => {
    for (const n of ns) {
      result.push(n);
      if (n.children.length > 0) walk(n.children);
    }
  };
  walk(nodes);
  return result;
}

function getAllIds(nodes: DomainResourceNode[]): string[] {
  return nodes.flatMap(n => [n.id, ...getAllIds(n.children)]);
}

function computeNodeState(node: DomainResourceNode): 'Allow' | 'Deny' | 'Partial' | 'Neutral' {
  const hasAllow = node.permissions.some(p => p.isAllowed && !p.isDenied);
  const hasDeny = node.permissions.some(p => !p.isAllowed && p.isDenied);

  if (node.children.length === 0) {
    if (hasAllow && !hasDeny) return 'Allow';
    if (hasDeny && !hasAllow) return 'Deny';
    return 'Neutral';
  }

  let childAllows = 0;
  let childDenies = 0;
  const check = (n: DomainResourceNode) => {
    n.permissions.forEach(p => {
      if (p.isAllowed && !p.isDenied) childAllows++;
      if (!p.isAllowed && p.isDenied) childDenies++;
    });
    n.children.forEach(check);
  };
  check(node);

  const totalAllows = (hasAllow ? 1 : 0) + childAllows;
  const totalDenies = (hasDeny ? 1 : 0) + childDenies;

  if (totalAllows > 0 && totalDenies === 0) return hasAllow ? 'Allow' : 'Partial';
  if (totalDenies > 0 && totalAllows === 0) return hasDeny ? 'Deny' : 'Partial';
  if (totalAllows > 0 && totalDenies > 0) return 'Partial';
  return 'Neutral';
}

const TYPE_ICON: Record<string, React.ReactNode> = {
  Aggregate: <Layers className="w-3 h-3" />,
  Entity: <Component className="w-3 h-3" />,
  DomainMethod: <FunctionSquare className="w-3 h-3" />,
  CrudOperation: <Zap className="w-3 h-3" />,
  CustomAction: <Zap className="w-3 h-3" />,
};

const TYPE_COLOR: Record<string, string> = {
  Aggregate: 'bg-purple-500/10 text-purple-500',
  Entity: 'bg-emerald-500/10 text-emerald-500',
  DomainMethod: 'bg-orange-500/10 text-orange-500',
  CrudOperation: 'bg-blue-500/10 text-blue-500',
  CustomAction: 'bg-amber-500/10 text-amber-500',
};

const STATE_ICON: Record<string, { icon: React.ReactNode; color: string }> = {
  Allow: { icon: <CheckCircle2 className="w-3.5 h-3.5" />, color: 'text-emerald-500' },
  Deny: { icon: <XCircle className="w-3.5 h-3.5" />, color: 'text-rose-500' },
  Partial: { icon: <CheckCircle2 className="w-3.5 h-3.5" />, color: 'text-amber-500' },
  Neutral: { icon: <MinusCircle className="w-3.5 h-3.5" />, color: 'text-m3-secondary/30' },
};

const DomainResourceRow: React.FC<{
  node: DomainResourceNode;
  isExpanded: boolean;
  isSelected: boolean;
  hasChildren: boolean;
  onToggle: () => void;
  renderInlineActions: (node: DomainResourceNode) => React.ReactNode;
}> = ({ node, isExpanded, isSelected, hasChildren, onToggle, renderInlineActions }) => {
  const state = computeNodeState(node);
  const stateInfo = STATE_ICON[state];

  const icon =
    node.type === 'CrudOperation'
      ? (CRUD_ICON[node.code.split('.').pop() ?? ''] ?? TYPE_ICON[node.type])
      : TYPE_ICON[node.type];

  const color =
    node.type === 'CrudOperation'
      ? (CRUD_COLOR[node.code.split('.').pop() ?? ''] ?? TYPE_COLOR[node.type])
      : TYPE_COLOR[node.type];

  return (
    <div
      className={`flex items-center gap-2 py-1.5 px-2 rounded-md cursor-pointer transition-colors group ${
        isSelected ? 'bg-m3-primary/10' : 'hover:bg-m3-surface-container/50'
      }`}
      style={{ paddingLeft: `${node.level * 16 + 4}px` }}
    >
      <div
        className={`w-5 h-5 flex items-center justify-center shrink-0 ${hasChildren ? 'text-m3-secondary/50 hover:text-m3-on-surface cursor-pointer' : 'text-transparent'}`}
        onClick={e => {
          e.stopPropagation();
          hasChildren && onToggle();
        }}
      >
        {hasChildren ? (
          isExpanded ? (
            <ChevronDown className="w-3.5 h-3.5" />
          ) : (
            <ChevronRight className="w-3.5 h-3.5" />
          )
        ) : (
          <MinusCircle className="w-2 h-2 opacity-30" />
        )}
      </div>

      <div className={`p-1 rounded ${color}`}>{icon}</div>

      <span
        className={`text-xs truncate flex-1 ${isSelected ? 'font-semibold text-m3-primary' : 'text-m3-on-surface/80'}`}
      >
        {node.label}
      </span>

      <CodeBadge code={node.code} size="xs" />

      {node.permissions.length > 0 && (
        <span className="text-[9px] text-m3-secondary bg-m3-surface-variant/50 px-1 rounded-full">
          {node.permissions.length}
        </span>
      )}

      <span
        className={`text-[8px] font-bold uppercase px-1 py-0.5 rounded border border-current ${color}`}
      >
        {node.type === 'CrudOperation'
          ? node.code.split('.').pop()
          : node.type === 'DomainMethod'
            ? 'METHOD'
            : node.type}
      </span>

      <div className={`shrink-0 ${stateInfo.color} mr-2`} title={`Estado Efectivo: ${state}`}>
        {stateInfo.icon}
      </div>

      <div onClick={e => e.stopPropagation()}>{renderInlineActions(node)}</div>
    </div>
  );
};

export const ProfileDomainResourcesPanel: React.FC<ProfileDomainResourcesPanelProps> = ({
  suite,
  permissions,
  renderInlineActions,
}) => {
  const [viewMode, setViewMode] = useState<'list' | 'thumbnail' | 'tree'>('tree');
  const [activeFilter, setActiveFilter] = useState('all');
  const [sortBy, setSortBy] = useState('order');
  const [sortOrder, setSortOrder] = useState<'asc' | 'desc'>('asc');
  const [expandedNodes, setExpandedNodes] = useState<Set<string>>(new Set());

  const tree = useMemo(() => buildDomainResourceTree(suite, permissions), [suite, permissions]);
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

  const filteredTree = useMemo(() => {
    if (activeFilter === 'all') return tree;
    return tree.filter(node => node.type === activeFilter);
  }, [tree, activeFilter]);

  const filteredFlat = useMemo(() => {
    if (activeFilter === 'all') return flatList;
    return flatList.filter(node => node.type === activeFilter || node.parentType === activeFilter);
  }, [flatList, activeFilter]);

  const renderTree = (nodes: DomainResourceNode[]) =>
    nodes.map(node => {
      const isExpanded = expandedNodes.has(node.id);
      const isSelected = false;
      const hasChildren = node.children.length > 0;
      return (
        <div key={node.id}>
          <DomainResourceRow
            node={node}
            isExpanded={isExpanded}
            isSelected={false}
            hasChildren={hasChildren}
            onToggle={() => toggleNode(node.id)}
            renderInlineActions={renderInlineActions}
          />
          {isExpanded && hasChildren && renderTree(node.children)}
        </div>
      );
    });

  const renderList = () => (
    <div className="flex flex-col gap-0.5">
      {filteredFlat.map(node => (
        <DomainResourceRow
          key={node.id}
          node={node}
          isExpanded={false}
          isSelected={false}
          hasChildren={false}
          onToggle={() => {}}
          renderInlineActions={renderInlineActions}
        />
      ))}
    </div>
  );

  return (
    <div className="flex flex-col h-full">
      <PermissionSectionToolbar
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
          { label: 'Jerarquía', value: 'order' },
          { label: 'Nombre', value: 'name' },
          { label: 'Código', value: 'code' },
          { label: 'Tipo', value: 'type' },
        ]}
        sortBy={sortBy}
        onSortByChange={setSortBy}
        sortOrder={sortOrder}
        onSortOrderToggle={() => setSortOrder(o => (o === 'asc' ? 'desc' : 'asc'))}
        itemCount={flatList.length}
        itemLabel="recurso"
        showExpandCollapse
        allExpanded={allExpanded}
        onToggleExpandAll={toggleAll}
      />

      <div className="flex-1 overflow-y-auto px-1 pb-2">
        {flatList.length === 0 ? (
          <div className="flex flex-col items-center justify-center py-12 text-center">
            <Database className="w-8 h-8 text-m3-secondary/30 mb-2" />
            <p className="text-xs text-m3-secondary">
              No hay recursos de dominio configurados en esta suite.
            </p>
          </div>
        ) : viewMode === 'tree' ? (
          renderTree(filteredTree)
        ) : (
          renderList()
        )}
      </div>
    </div>
  );
};
