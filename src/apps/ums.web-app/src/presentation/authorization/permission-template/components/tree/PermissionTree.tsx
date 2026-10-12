import React, { useState, useMemo } from 'react';
import type { SystemSuite } from '@domain/authorization/models/system-suite.model';
import type { SystemSuiteNode } from '@domain/authorization/schemas/system-suite.schema';
import type { PermissionTemplateItem } from '@domain/authorization/models/permission-template.model';
import { TreeNode, UITreeNodeData, HierarchyType } from './TreeNode';
import { Search, FolderOpen, FolderClosed } from 'lucide-react';

interface PermissionTreeProps {
  suite: SystemSuite | undefined | null;
  items: PermissionTemplateItem[];
  selectedNodeId: string | null;
  onSelectNode: (node: UITreeNodeData | null) => void;
}

function buildTreeData(
  suite: SystemSuite | undefined | null,
  items: PermissionTemplateItem[]
): UITreeNodeData[] {
  if (!suite) return [];

  const itemsByTargetId = items.reduce(
    (acc, item) => {
      if (!acc[item.targetId]) acc[item.targetId] = [];
      acc[item.targetId].push(item);
      return acc;
    },
    {} as Record<string, PermissionTemplateItem[]>
  );

  const globalResources = suite.domainResources?.filter(dr => !dr.moduleId) || [];
  const moduleResourcesMap = (suite.domainResources || []).reduce(
    (acc, dr) => {
      if (dr.moduleId) {
        if (!acc[dr.moduleId]) acc[dr.moduleId] = [];
        acc[dr.moduleId].push(dr);
      }
      return acc;
    },
    {} as Record<string, typeof suite.domainResources>
  );

  // Mapea recursivamente un nodo del árbol del SystemSuite (ADR-0090) al nodo
  // de UI del árbol de permisos. El rol del nodo (kind) fija el tipo visual.
  const mapNode = (node: SystemSuiteNode): UITreeNodeData => ({
    id: node.id,
    type: node.kind as HierarchyType,
    label: node.label,
    description: node.description,
    actionCode: node.actionCodes[0],
    items: itemsByTargetId[node.id] || [],
    children: [...(node.children ?? [])]
      .sort((a, b) => a.sortOrder - b.sortOrder)
      .map(mapNode),
  });

  const moduleNodes = suite.modules.map(mod => {
    const modResources = moduleResourcesMap[mod.id] || [];
    const resourceNodes: UITreeNodeData[] = modResources.map(res => ({
      id: res.id,
      type: res.type,
      label: res.name,
      description: res.description || '',
      items: itemsByTargetId[res.id] || [],
      children: [],
    }));

    const modNode: UITreeNodeData = {
      id: mod.id,
      type: 'Module',
      label: mod.name,
      description: mod.description,
      items: itemsByTargetId[mod.id] || [],
      children: [
        ...resourceNodes,
        ...[...(mod.nodes ?? [])].sort((a, b) => a.sortOrder - b.sortOrder).map(mapNode),
      ],
    };
    return modNode;
  });

  const globalResourceNodes: UITreeNodeData[] = globalResources.map(res => ({
    id: res.id,
    type: res.type,
    label: res.name,
    description: res.description || '',
    items: itemsByTargetId[res.id] || [],
    children: [],
  }));

  return [...globalResourceNodes, ...moduleNodes];
}

function filterTree(nodes: UITreeNodeData[], query: string): UITreeNodeData[] {
  if (!query) return nodes;
  const q = query.toLowerCase();

  return nodes
    .map(node => {
      const isMatch = node.label.toLowerCase().includes(q) || node.type.toLowerCase().includes(q);
      const filteredChildren = filterTree(node.children, query);
      if (isMatch || filteredChildren.length > 0) {
        return { ...node, children: filteredChildren };
      }
      return null;
    })
    .filter(Boolean) as UITreeNodeData[];
}

function getAllNodeIds(nodes: UITreeNodeData[]): string[] {
  return nodes.flatMap(n => [n.id, ...getAllNodeIds(n.children)]);
}

export const PermissionTree: React.FC<PermissionTreeProps> = ({
  suite,
  items,
  selectedNodeId,
  onSelectNode,
}) => {
  const [searchQuery, setSearchQuery] = useState('');
  const [expandedNodes, setExpandedNodes] = useState<Set<string>>(new Set());

  const fullTree = useMemo(() => buildTreeData(suite, items), [suite, items]);
  const filteredTree = useMemo(() => filterTree(fullTree, searchQuery), [fullTree, searchQuery]);

  // Auto-expand when searching
  React.useEffect(() => {
    if (searchQuery) {
      setExpandedNodes(new Set(getAllNodeIds(filteredTree)));
    }
  }, [searchQuery, filteredTree]);

  const toggleExpand = (id: string) => {
    setExpandedNodes(prev => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  };

  const expandAll = () => setExpandedNodes(new Set(getAllNodeIds(fullTree)));
  const collapseAll = () => setExpandedNodes(new Set());

  return (
    <div className="flex flex-col h-full bg-m3-surface-container/20 border-r border-m3-outline/20">
      <div className="p-3 border-b border-m3-outline/20 space-y-3">
        <div className="flex items-center gap-2">
          <div className="relative flex-1">
            <Search className="w-4 h-4 absolute left-2.5 top-1/2 -translate-y-1/2 text-m3-secondary/50" />
            <input
              type="text"
              placeholder="Buscar en el árbol..."
              value={searchQuery}
              onChange={e => setSearchQuery(e.target.value)}
              className="w-full bg-m3-surface-container/50 border border-m3-outline/20 rounded-lg pl-9 pr-3 py-1.5 text-xs text-m3-on-surface focus:outline-none focus:border-m3-primary/50 transition-colors"
            />
          </div>
          <div className="flex items-center gap-1 shrink-0">
            <button
              onClick={expandAll}
              className="p-1.5 rounded bg-m3-surface-variant/50 text-m3-secondary hover:text-m3-on-surface transition-colors"
              title="Expandir todo"
            >
              <FolderOpen className="w-3.5 h-3.5" />
            </button>
            <button
              onClick={collapseAll}
              className="p-1.5 rounded bg-m3-surface-variant/50 text-m3-secondary hover:text-m3-on-surface transition-colors"
              title="Colapsar todo"
            >
              <FolderClosed className="w-3.5 h-3.5" />
            </button>
          </div>
        </div>
      </div>
      <div className="flex-1 overflow-y-auto py-2">
        {filteredTree.length === 0 ? (
          <p className="text-center text-xs text-m3-secondary/50 mt-4">
            No se encontraron resultados.
          </p>
        ) : (
          filteredTree.map(node => (
            <TreeNode
              key={node.id}
              node={node}
              level={0}
              selectedNodeId={selectedNodeId}
              onSelect={onSelectNode}
              expandedNodes={expandedNodes}
              toggleExpand={toggleExpand}
            />
          ))
        )}
      </div>
    </div>
  );
};
