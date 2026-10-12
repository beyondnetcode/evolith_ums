import React, { useState } from 'react';
import { ChevronRight, ChevronDown } from 'lucide-react';

export interface TreeItem {
  id: string;
  label: string | React.ReactNode;
  icon?: React.ReactNode;
  children?: TreeItem[];
  defaultExpanded?: boolean;
  trailing?: React.ReactNode;
}

export interface M3TreeAccordionProps {
  items: TreeItem[];
  level?: number;
  className?: string;
  indentWidth?: number; // pixels to indent per level
}

export const M3TreeAccordion: React.FC<M3TreeAccordionProps> = ({
  items,
  level = 0,
  className = '',
  indentWidth = 16,
}) => {
  if (!items || items.length === 0) return null;

  return (
    <ul className={`flex flex-col ${className}`}>
      {items.map(item => (
        <M3TreeAccordionNode key={item.id} item={item} level={level} indentWidth={indentWidth} />
      ))}
    </ul>
  );
};

const M3TreeAccordionNode: React.FC<{
  item: TreeItem;
  level: number;
  indentWidth: number;
}> = ({ item, level, indentWidth }) => {
  const [isExpanded, setIsExpanded] = useState(item.defaultExpanded ?? false);
  const hasChildren = item.children && item.children.length > 0;

  const toggleExpand = () => {
    if (hasChildren) setIsExpanded(!isExpanded);
  };

  return (
    <li className="flex flex-col">
      <div
        className={[
          'flex items-center w-full min-h-[40px] px-2 py-1.5 transition-colors group',
          hasChildren ? 'cursor-pointer hover:bg-m3-surface-container/50' : '',
          level > 0 ? 'border-l border-m3-outline/10 ml-2' : '',
        ].join(' ')}
        style={{ paddingLeft: `${level * indentWidth + 8}px` }}
        onClick={toggleExpand}
      >
        {/* Expand/Collapse Chevron */}
        <div className="w-6 flex items-center justify-center flex-shrink-0 text-m3-secondary">
          {hasChildren ? (
            isExpanded ? (
              <ChevronDown className="w-4 h-4" />
            ) : (
              <ChevronRight className="w-4 h-4" />
            )
          ) : (
            <span className="w-4 h-4" /> // Placeholder for alignment
          )}
        </div>

        {/* Node Icon */}
        {item.icon && (
          <div className="mr-2 flex items-center justify-center text-m3-secondary group-hover:text-m3-primary transition-colors">
            {item.icon}
          </div>
        )}

        {/* Node Label */}
        <div className="flex-1 text-sm font-medium text-m3-on-surface truncate">{item.label}</div>

        {/* Trailing actions (toggles, badges) */}
        {item.trailing && (
          <div className="ml-2 flex-shrink-0" onClick={e => e.stopPropagation()}>
            {item.trailing}
          </div>
        )}
      </div>

      {/* Children */}
      {hasChildren && isExpanded && (
        <div className="animate-fadeIn">
          <M3TreeAccordion
            items={item.children ?? []}
            level={level + 1}
            indentWidth={indentWidth}
          />
        </div>
      )}
    </li>
  );
};
