import React from 'react';
import { ChevronRight, ChevronDown, CheckCircle2, XCircle, MinusCircle } from 'lucide-react';
import type { PermissionTemplateItem } from '@domain/authorization/models/permission-template.model';
import { itemEffect, isRetiredItem } from '@domain/authorization/models/permission-template.model';

export type HierarchyType = 'Module' | 'Menu' | 'SubMenu' | 'Option' | 'Aggregate' | 'Entity';

export interface UITreeNodeData {
  id: string;
  type: HierarchyType;
  label: string;
  description: string;
  actionCode?: string;
  children: UITreeNodeData[];
  items: PermissionTemplateItem[];
}

interface TreeNodeProps {
  node: UITreeNodeData;
  level: number;
  selectedNodeId: string | null;
  onSelect: (node: UITreeNodeData) => void;
  expandedNodes: Set<string>;
  toggleExpand: (nodeId: string) => void;
}

// Compute the effective state of a node based on its items and children's items.
// Simplification for the tree visualization.
export function computeEffectiveState(
  node: UITreeNodeData
): 'Allow' | 'Deny' | 'Neutral' | 'Partial' {
  const selfEffects = node.items.map(itemEffect);

  const hasAllow = selfEffects.includes('Allow');
  const hasDeny = selfEffects.includes('Deny');

  if (hasAllow && !hasDeny && node.children.length === 0) return 'Allow';
  if (hasDeny && !hasAllow && node.children.length === 0) return 'Deny';

  // If it has children, we need to check if any child has Allow or Deny
  let childAllows = 0;
  let childDenies = 0;

  const checkChildren = (n: UITreeNodeData) => {
    n.items.forEach(i => {
      const e = itemEffect(i);
      if (e === 'Allow') childAllows++;
      if (e === 'Deny') childDenies++;
    });
    n.children.forEach(checkChildren);
  };

  checkChildren(node);

  const totalAllows = (hasAllow ? 1 : 0) + childAllows;
  const totalDenies = (hasDeny ? 1 : 0) + childDenies;

  if (totalAllows > 0 && totalDenies === 0) return hasAllow ? 'Allow' : 'Partial';
  if (totalDenies > 0 && totalAllows === 0) return hasDeny ? 'Deny' : 'Partial';
  if (totalAllows > 0 && totalDenies > 0) return 'Partial';

  return 'Neutral';
}

export const TreeNode: React.FC<TreeNodeProps> = ({
  node,
  level,
  selectedNodeId,
  onSelect,
  expandedNodes,
  toggleExpand,
}) => {
  const isExpanded = expandedNodes.has(node.id);
  const isSelected = selectedNodeId === node.id;
  const hasChildren = node.children.length > 0;
  const state = computeEffectiveState(node);
  // Una fila retirada ya no es una regla: no se cuenta en la insignia (ADR-0164).
  const ruleCount = node.items.filter(item => !isRetiredItem(item)).length;

  const StateIcon =
    state === 'Allow'
      ? CheckCircle2
      : state === 'Deny'
        ? XCircle
        : state === 'Partial'
          ? CheckCircle2
          : MinusCircle;

  const stateColor =
    state === 'Allow'
      ? 'text-emerald-500'
      : state === 'Deny'
        ? 'text-rose-500'
        : state === 'Partial'
          ? 'text-amber-500'
          : 'text-m3-secondary/30';

  const typeColorMap: Record<HierarchyType, string> = {
    Module: 'bg-indigo-500/10 text-indigo-500',
    Menu: 'bg-blue-500/10 text-blue-500',
    SubMenu: 'bg-sky-500/10 text-sky-500',
    Option: 'bg-slate-500/10 text-slate-500',
    Aggregate: 'bg-purple-500/10 text-purple-500',
    Entity: 'bg-emerald-500/10 text-emerald-500',
  };

  return (
    <div>
      <div
        className={`flex items-center group cursor-pointer border-b border-m3-outline/5 transition-colors ${
          isSelected ? 'bg-m3-primary/10' : 'hover:bg-m3-surface-container/50'
        }`}
        style={{ paddingLeft: `${level * 16}px` }}
        onClick={() => onSelect(node)}
      >
        <div
          className="w-6 h-8 flex items-center justify-center shrink-0 cursor-pointer text-m3-secondary/50 hover:text-m3-on-surface"
          onClick={e => {
            e.stopPropagation();
            hasChildren && toggleExpand(node.id);
          }}
        >
          {hasChildren &&
            (isExpanded ? (
              <ChevronDown className="w-4 h-4" />
            ) : (
              <ChevronRight className="w-4 h-4" />
            ))}
        </div>

        <div className="flex-1 min-w-0 py-1.5 pr-2 flex items-center gap-2">
          <span
            className={`text-[10px] font-semibold uppercase tracking-wider px-2.5 py-0.5 rounded-full ${typeColorMap[node.type]}`}
          >
            {node.type}
          </span>
          <span
            className={`text-xs truncate ${isSelected ? 'font-semibold text-m3-primary' : 'text-m3-on-surface/80'}`}
          >
            {node.label}
          </span>
          {ruleCount > 0 && (
            <span className="text-[10px] text-m3-secondary bg-m3-surface-variant/50 px-1.5 rounded-full">
              {ruleCount} reglas
            </span>
          )}
          <span className="flex-1" />
          <StateIcon className={`w-4 h-4 shrink-0 ${stateColor}`} title={`Estado: ${state}`} />
        </div>
      </div>

      {isExpanded && hasChildren && (
        <div>
          {node.children.map(child => (
            <TreeNode
              key={child.id}
              node={child}
              level={level + 1}
              selectedNodeId={selectedNodeId}
              onSelect={onSelect}
              expandedNodes={expandedNodes}
              toggleExpand={toggleExpand}
            />
          ))}
        </div>
      )}
    </div>
  );
};
