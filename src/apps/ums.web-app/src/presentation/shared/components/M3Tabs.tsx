import React, { useState } from 'react';

export interface TabItem {
  id: string;
  label: string;
  icon?: React.ReactNode;
  content: React.ReactNode;
  badge?: React.ReactNode;
}

export interface M3TabsProps {
  tabs: TabItem[];
  defaultTabId?: string;
  onChange?: (tabId: string) => void;
  className?: string;
}

export const M3Tabs: React.FC<M3TabsProps> = ({ tabs, defaultTabId, onChange, className = '' }) => {
  const [activeTabId, setActiveTabId] = useState(defaultTabId || tabs[0]?.id);

  const handleTabClick = (id: string) => {
    setActiveTabId(id);
    if (onChange) onChange(id);
  };

  const activeContent = tabs.find(t => t.id === activeTabId)?.content;

  return (
    <div className={`flex flex-col w-full h-full ${className}`}>
      {/* Tab Header (Scrollable horizontally) */}
      <div className="flex items-center gap-1 overflow-x-auto border-b border-m3-outline/20 mb-4 px-2 no-scrollbar">
        {tabs.map(tab => {
          const isActive = tab.id === activeTabId;
          return (
            <button
              key={tab.id}
              onClick={() => handleTabClick(tab.id)}
              className={[
                'relative flex items-center justify-center gap-1.5 min-w-max px-3 py-2 text-[11px] font-medium transition-colors',
                isActive
                  ? 'text-m3-primary'
                  : 'text-m3-secondary hover:text-m3-on-surface hover:bg-m3-surface-container/40',
              ].join(' ')}
              role="tab"
              aria-selected={isActive}
            >
              {tab.icon && (
                <span className={isActive ? 'text-m3-primary' : 'text-m3-secondary/70'}>
                  {tab.icon}
                </span>
              )}
              {tab.label}
              {tab.badge && <span className="ml-1">{tab.badge}</span>}

              {/* Active Indicator Line */}
              {isActive && (
                <div className="absolute bottom-0 left-0 right-0 h-0.5 bg-m3-primary rounded-t-full" />
              )}
            </button>
          );
        })}
      </div>

      {/* Tab Content (renders the active tab content but preserves others to avoid unmounting if needed? We render only active for now per standard Tabs) */}
      <div className="flex-1 overflow-y-auto w-full h-full">
        <div className="animate-fadeIn">{activeContent}</div>
      </div>
    </div>
  );
};
