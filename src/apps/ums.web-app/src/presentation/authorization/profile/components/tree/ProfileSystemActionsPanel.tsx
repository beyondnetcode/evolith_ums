import React, { useState } from 'react';
import type { SystemSuite } from '@domain/authorization/models/system-suite.model';
import type { ProfilePermission } from '@domain/authorization/models/profile.model';
import { CheckCircle2, XCircle, MinusCircle, Info, Key } from 'lucide-react';
import { PermissionSectionToolbar } from '@shared/components/PermissionSectionToolbar';
import { CodeBadge } from '@shared/components/CodeBadge';
import { EntityRow } from '@shared/components/EntityRow';
import { EntityCard } from '@shared/components/EntityCard';

interface ProfileSystemActionsPanelProps {
  suite: SystemSuite | undefined | null;
  permissions: ProfilePermission[];
  renderInlineActions: (permission: ProfilePermission) => React.ReactNode;
}

export const ProfileSystemActionsPanel: React.FC<ProfileSystemActionsPanelProps> = ({
  suite,
  permissions,
  renderInlineActions,
}) => {
  const [viewMode, setViewMode] = useState<'list' | 'thumbnail' | 'tree'>('list');
  const [sortBy, setSortBy] = useState('name');
  const [sortOrder, setSortOrder] = useState<'asc' | 'desc'>('asc');

  if (!suite || suite.actions.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center h-full text-m3-secondary/50 p-6 text-center">
        <Info className="w-8 h-8 mb-2 opacity-20" />
        <p className="text-xs">No hay acciones de sistema globales en esta suite.</p>
      </div>
    );
  }

  const systemItems = permissions.filter(
    i => i.targetType === 'SystemSuite' || i.targetId === suite.systemSuiteId
  );

  const getActionState = (actionId: string) => {
    const item = systemItems.find(i => i.actionId === actionId);
    if (!item) return 'Neutral';
    if (item.isAllowed && !item.isDenied) return 'Allow';
    if (!item.isAllowed && item.isDenied) return 'Deny';
    return 'Neutral';
  };

  const sortedActions = [...suite.actions];
  if (sortBy === 'name') {
    sortedActions.sort((a, b) => {
      const cmp = a.name.localeCompare(b.name);
      return sortOrder === 'asc' ? cmp : -cmp;
    });
  } else if (sortBy === 'code') {
    sortedActions.sort((a, b) => {
      const cmp = a.code.localeCompare(b.code);
      return sortOrder === 'asc' ? cmp : -cmp;
    });
  }

  const renderRow = (action: NonNullable<SystemSuite['actions']>[number]) => {
    const state = getActionState(action.id);
    const StateIcon = state === 'Allow' ? CheckCircle2 : state === 'Deny' ? XCircle : MinusCircle;
    const stateColor =
      state === 'Allow'
        ? 'text-emerald-500'
        : state === 'Deny'
          ? 'text-rose-500'
          : 'text-m3-secondary/30';

    return (
      <EntityRow
        key={action.id}
        id={action.id}
        leading={
          <div className="p-1.5 rounded bg-m3-primary/10 text-m3-primary">
            <Key className="w-3.5 h-3.5" />
          </div>
        }
        trailingColumns={[
          { content: <CodeBadge code={action.code} size="xs" />, width: 'w-28' },
          {
            content: (
              <div className="flex gap-2 min-w-[200px] justify-end">
                {renderInlineActions(
                  systemItems.find(i => i.actionId === action.id) as ProfilePermission
                )}
              </div>
            ),
            width: 'w-auto flex-1 justify-end',
          },
        ]}
      >
        <div>
          <span className="text-xs font-semibold text-m3-on-surface">{action.name}</span>
          <div className={`flex items-center gap-1 text-[10px] font-medium ${stateColor}`}>
            <StateIcon className="w-3 h-3" />
            {state === 'Allow' ? 'Permitido' : state === 'Deny' ? 'Denegado' : 'Neutral'}
          </div>
        </div>
      </EntityRow>
    );
  };

  const renderCard = (action: NonNullable<SystemSuite['actions']>[number]) => {
    const state = getActionState(action.id);

    return (
      <EntityCard
        key={action.id}
        icon={<Key className="w-4 h-4" />}
        title={action.name}
        subtitle={<CodeBadge code={action.code} size="xs" />}
        badges={
          <div className="flex justify-end min-w-full mt-2">
            {renderInlineActions(
              systemItems.find(i => i.actionId === action.id) as ProfilePermission
            )}
          </div>
        }
      />
    );
  };

  return (
    <div className="flex flex-col h-full">
      <PermissionSectionToolbar
        viewMode={viewMode}
        onViewModeChange={setViewMode}
        sortOptions={[
          { label: 'Nombre', value: 'name' },
          { label: 'Código', value: 'code' },
        ]}
        sortBy={sortBy}
        onSortByChange={setSortBy}
        sortOrder={sortOrder}
        onSortOrderToggle={() => setSortOrder(o => (o === 'asc' ? 'desc' : 'asc'))}
        itemCount={suite.actions.length}
        itemLabel="acción"
      />

      <div className="flex-1 overflow-y-auto px-1 pb-2">
        <div className="flex items-center gap-2 text-[10px] text-m3-secondary/70 bg-m3-surface-variant/30 p-2 rounded-lg mb-3">
          <Info className="w-3 h-3" />
          Puedes sobreescribir las acciones que vengan como NEUTRAL desde el perfil.
        </div>

        {viewMode === 'list' ? (
          <div className="flex flex-col gap-0.5">{sortedActions.map(renderRow)}</div>
        ) : (
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
            {sortedActions.map(renderCard)}
          </div>
        )}
      </div>
    </div>
  );
};
