import React from 'react';
import { SectionHeader } from './SectionHeader';

interface DetailSectionProps {
  title: string;
  subtitle?: string;
  actions?: React.ReactNode;
  content: React.ReactNode;
  noPadding?: boolean;
}

export function DetailSection({
  title,
  subtitle,
  actions,
  content,
  noPadding = false,
}: DetailSectionProps): React.JSX.Element {
  return (
    <div className="space-y-3">
      <SectionHeader title={title} subtitle={subtitle} actions={actions} />
      <div
        className={
          noPadding ? '' : 'p-4 bg-m3-surface-container/10 rounded-xl border border-m3-outline/15'
        }
      >
        {content}
      </div>
    </div>
  );
}
