import React from 'react';
import { ChevronRight } from 'lucide-react';
import { useTreeNodes, type TreeNode } from '@app/hooks/use-tree-nodes';

interface HierarchicalListProps<T> {
  items: T[];
  idKey: keyof T;
  parentIdKey: keyof T;
  selectedId: string;
  onSelect: (id: string) => void;
  renderParentRow: (
    node: TreeNode<T>,
    isSelected: boolean,
    isExpanded: boolean,
    onToggle: () => void
  ) => React.ReactNode;
  renderChildRow: (item: T, isSelected: boolean) => React.ReactNode;
  renderParentCard: (
    node: TreeNode<T>,
    isSelected: boolean,
    isExpanded: boolean,
    onToggle: () => void
  ) => React.ReactNode;
  renderChildCard: (item: T, isSelected: boolean) => React.ReactNode;
  viewMode: 'list' | 'thumbnail';
}

export function HierarchicalList<T extends Record<string, unknown>>({
  items,
  idKey,
  parentIdKey,
  selectedId,
  renderParentRow,
  renderChildRow,
  renderParentCard,
  renderChildCard,
  viewMode,
}: HierarchicalListProps<T>) {
  const { treeNodes, toggleExpand, isExpanded, getChildren } = useTreeNodes<T>({
    items,
    parentIdKey,
    idKey,
  });

  if (viewMode === 'list') {
    return (
      <>
        {treeNodes.map(node => {
          const id = String(node.item[idKey]);
          const isSelected = selectedId === id;
          const nodeIsExpanded = isExpanded(id);

          return (
            <React.Fragment key={id}>
              {renderParentRow(node, isSelected, nodeIsExpanded, () => toggleExpand(id))}
              {nodeIsExpanded &&
                getChildren(id).map(child => {
                  const childId = String(child[idKey]);
                  const isChildSelected = selectedId === childId;
                  return (
                    <React.Fragment key={childId}>
                      {renderChildRow(child, isChildSelected)}
                    </React.Fragment>
                  );
                })}
            </React.Fragment>
          );
        })}
      </>
    );
  }

  return (
    <div className="space-y-3">
      {treeNodes.map(node => {
        const id = String(node.item[idKey]);
        const isSelected = selectedId === id;
        const nodeIsExpanded = isExpanded(id);
        const nodeHasChildren = getChildren(id).length > 0;

        return (
          <div key={id}>
            {renderParentCard(node, isSelected, nodeIsExpanded, () => toggleExpand(id))}
            {nodeHasChildren && nodeIsExpanded && (
              <div className="ml-6 mt-2 pl-4 border-l-2 border-m3-outline/20 space-y-2 animate-fadeIn">
                {getChildren(id).map(child => {
                  const childId = String(child[idKey]);
                  const isChildSelected = selectedId === childId;
                  return (
                    <React.Fragment key={childId}>
                      {renderChildCard(child, isChildSelected)}
                    </React.Fragment>
                  );
                })}
              </div>
            )}
          </div>
        );
      })}
    </div>
  );
}

export interface HierarchicalRowProps {
  hasChildren: boolean;
  isExpanded: boolean;
  isChild: boolean;
  onToggleExpand: () => void;
  onClick: () => void;
  isSelected: boolean;
  children: React.ReactNode;
}

export const HierarchicalRow: React.FC<HierarchicalRowProps> = ({
  onClick,
  isSelected,
  isChild,
  children,
}) => {
  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' || e.key === ' ') {
      e.preventDefault();
      onClick();
    }
  };

  return (
    <tr
      onClick={onClick}
      onKeyDown={handleKeyDown}
      tabIndex={0}
      role="row"
      aria-selected={isSelected}
      className={`group cursor-pointer transition-colors duration-150 ${
        isSelected
          ? 'bg-m3-primary-container/30 text-m3-on-primary-container'
          : 'hover:bg-m3-primary/5 text-m3-secondary hover:text-m3-on-surface'
      } ${isChild ? 'bg-m3-surface-container/10' : ''}`}
    >
      {children}
    </tr>
  );
};

export const HierarchicalExpandButton: React.FC<{
  hasChildren: boolean;
  isExpanded: boolean;
  onClick: (e: React.MouseEvent) => void;
}> = ({ hasChildren, isExpanded, onClick }) => (
  <>
    {hasChildren ? (
      <button
        onClick={onClick}
        className={`p-0.5 rounded transition-transform duration-200 ${isExpanded ? 'rotate-90' : ''}`}
      >
        <ChevronRight className="w-3.5 h-3.5 text-m3-secondary" />
      </button>
    ) : (
      <span className="w-4" />
    )}
  </>
);
