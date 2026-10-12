import React, { useState } from 'react';
import { Pencil, ShieldCheck, ShieldOff } from 'lucide-react';
import { InlineAddForm } from '@shared/components/InlineAddForm';
import { M3TextField } from '@shared/components/M3TextField';
import { M3Select } from '@shared/components/M3Select';
import { CodeBadge } from '@shared/components/CodeBadge';
import { IconButton } from '@shared/components/Tooltip';
import { EmptyState } from '@shared/components/EmptyState';
import { Shield } from 'lucide-react';
import { formatSystemCode } from '@app/utils/security';
import { useI18n } from '@app/i18n/use-i18n';
import {
  useCreateRole,
  useRolesBySystemSuite,
  useSetRoleActive,
  useUpdateRole,
} from '@app/authorization/hooks/use-role';
import type { Role } from '@domain/authorization/schemas/role.schema';
import { ListToolbar } from '@shared/components/ListToolbar';

interface Props {
  systemSuiteId: string;
}

interface Draft {
  value: string;
  description: string;
  parentRoleId: string;
  promotionOrder: string;
}

const blankDraft = (): Draft => ({
  value: '',
  description: '',
  parentRoleId: '',
  promotionOrder: '0',
});

export const SystemSuiteRolesPanel: React.FC<Props> = ({ systemSuiteId }) => {
  const t = useI18n();
  const { data: roles = [], isLoading } = useRolesBySystemSuite(systemSuiteId);
  const createRole = useCreateRole(systemSuiteId);
  const setActive = useSetRoleActive(systemSuiteId);
  const [adding, setAdding] = useState(false);
  const [code, setCode] = useState('');
  const [draft, setDraft] = useState<Draft>(blankDraft);
  const [error, setError] = useState('');

  const [viewMode, setViewMode] = useState<'list' | 'thumbnail'>('list');
  const [activeFilter, setActiveFilter] = useState('all');
  const [sortBy, setSortBy] = useState('value');
  const [sortOrder, setSortOrder] = useState<'asc' | 'desc'>('asc');

  const hierarchyLevelFor = (parentId: string) => {
    const parent = roles.find(role => role.roleId === parentId);
    return parent ? parent.hierarchyLevel + 1 : 0;
  };

  const handleCreate = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!code.trim() || !draft.value.trim()) {
      setError(t.roleCodeAndValueRequired);
      return;
    }
    try {
      await createRole.mutateAsync({
        code: formatSystemCode(code),
        value: draft.value.trim(),
        description: draft.description.trim(),
        parentRoleId: draft.parentRoleId || null,
        hierarchyLevel: hierarchyLevelFor(draft.parentRoleId),
        promotionOrder: Number(draft.promotionOrder) || 0,
      });
      setCode('');
      setDraft(blankDraft());
      setAdding(false);
      setError('');
    } catch {
      /* notification provides the user-safe cause and support ID */
    }
  };

  if (isLoading) {
    return <p className="text-[12px] text-m3-secondary">{t.loading}</p>;
  }

  let filteredRoles = roles;
  if (activeFilter !== 'all') {
    filteredRoles = filteredRoles.filter(r =>
      activeFilter === 'active' ? r.isActive : !r.isActive
    );
  }
  filteredRoles = [...filteredRoles].sort((a, b) => {
    let cmp = 0;
    if (sortBy === 'value') cmp = a.value.localeCompare(b.value);
    else if (sortBy === 'code') cmp = a.code.localeCompare(b.code);
    else if (sortBy === 'level') cmp = a.hierarchyLevel - b.hierarchyLevel;
    return sortOrder === 'asc' ? cmp : -cmp;
  });

  return (
    <div className="space-y-1.5">
      <ListToolbar
        viewMode={viewMode}
        onViewModeChange={setViewMode}
        filterOptions={[
          { label: 'Todos', value: 'all' },
          { label: 'Activos', value: 'active' },
          { label: 'Inactivos', value: 'inactive' },
        ]}
        activeFilter={activeFilter}
        onFilterChange={setActiveFilter}
        sortOptions={[
          { label: 'Valor', value: 'value' },
          { label: 'Código', value: 'code' },
          { label: 'Nivel', value: 'level' },
        ]}
        sortBy={sortBy}
        onSortByChange={setSortBy}
        sortOrder={sortOrder}
        onSortOrderToggle={() => setSortOrder(o => (o === 'asc' ? 'desc' : 'asc'))}
        itemCount={roles.length}
        itemLabel="Rol"
        onAdd={() => {
          setAdding(true);
          setError('');
        }}
        addLabel={t.newRole}
      />

      <InlineAddForm
        isOpen={adding}
        onToggle={open => {
          setAdding(open);
          if (!open) setError('');
        }}
        onSubmit={handleCreate}
        addLabel="+"
        title={t.newRole}
        cancelLabel={t.cancelEdit}
        submitLabel={t.saveRole}
        isLoading={createRole.isPending}
        triggerEmphasis="none"
        error={error || undefined}
      >
        <M3TextField
          label={t.roleCode}
          required
          value={code}
          onChange={event => setCode(event.target.value)}
          placeholder="SECURITY_ADMIN"
        />
        <M3TextField
          label={t.roleValue}
          required
          value={draft.value}
          onChange={event => setDraft(value => ({ ...value, value: event.target.value }))}
        />
        <M3TextField
          label={t.description}
          value={draft.description}
          onChange={event => setDraft(value => ({ ...value, description: event.target.value }))}
        />
        <M3Select
          compact
          label={t.parentRole}
          value={draft.parentRoleId}
          onChange={event => setDraft(value => ({ ...value, parentRoleId: event.target.value }))}
        >
          <option value="">{t.rootRole}</option>
          {roles
            .filter(role => role.isActive)
            .map(role => (
              <option key={role.roleId} value={role.roleId}>
                {role.value}
              </option>
            ))}
        </M3Select>
        <M3TextField
          label={t.promotionOrder}
          type="number"
          value={draft.promotionOrder}
          onChange={event => setDraft(value => ({ ...value, promotionOrder: event.target.value }))}
        />
      </InlineAddForm>

      {roles.length === 0 ? (
        <EmptyState
          icon={<Shield className="w-6 h-6" />}
          message={t.noRolesConfigured ?? 'No hay roles configurados'}
        />
      ) : filteredRoles.length === 0 ? (
        <EmptyState
          icon={<Shield className="w-6 h-6" />}
          message="No hay roles que coincidan con el filtro"
        />
      ) : viewMode === 'list' ? (
        <div className="flex flex-col gap-1">
          {filteredRoles.map(role => (
            <RoleRow
              key={role.roleId}
              role={role}
              roles={roles}
              systemSuiteId={systemSuiteId}
              onStatusChange={isActive => setActive.mutate({ roleId: role.roleId, isActive })}
              changingStatus={setActive.isPending}
            />
          ))}
        </div>
      ) : (
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
          {filteredRoles.map(role => (
            <RoleRow
              key={role.roleId}
              role={role}
              roles={roles}
              systemSuiteId={systemSuiteId}
              onStatusChange={isActive => setActive.mutate({ roleId: role.roleId, isActive })}
              changingStatus={setActive.isPending}
            />
          ))}
        </div>
      )}
    </div>
  );
};

interface RowProps {
  role: Role;
  roles: Role[];
  systemSuiteId: string;
  onStatusChange: (active: boolean) => void;
  changingStatus: boolean;
}

const RoleRow: React.FC<RowProps> = ({
  role,
  roles,
  systemSuiteId,
  onStatusChange,
  changingStatus,
}) => {
  const t = useI18n();
  const updateRole = useUpdateRole(systemSuiteId, role.roleId);
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState<Draft>({
    value: role.value,
    description: role.description,
    parentRoleId: role.parentRoleId ?? '',
    promotionOrder: String(role.promotionOrder),
  });

  const handleSave = async (event: React.FormEvent) => {
    event.preventDefault();
    const parent = roles.find(candidate => candidate.roleId === draft.parentRoleId);
    try {
      await updateRole.mutateAsync({
        value: draft.value.trim(),
        description: draft.description.trim(),
        parentRoleId: draft.parentRoleId || null,
        hierarchyLevel: parent ? parent.hierarchyLevel + 1 : 0,
        promotionOrder: Number(draft.promotionOrder) || 0,
      });
      setEditing(false);
    } catch {
      /* notification provides the user-safe cause and support ID */
    }
  };

  if (editing) {
    return (
      <form onSubmit={handleSave} className="space-y-2 rounded-lg border border-m3-primary/25 p-3">
        <M3TextField
          label={t.roleValue}
          required
          value={draft.value}
          onChange={event => setDraft(value => ({ ...value, value: event.target.value }))}
        />
        <M3TextField
          label={t.description}
          value={draft.description}
          onChange={event => setDraft(value => ({ ...value, description: event.target.value }))}
        />
        <M3Select
          compact
          label={t.parentRole}
          value={draft.parentRoleId}
          onChange={event => setDraft(value => ({ ...value, parentRoleId: event.target.value }))}
        >
          <option value="">{t.rootRole}</option>
          {roles
            .filter(candidate => candidate.roleId !== role.roleId)
            .map(candidate => (
              <option key={candidate.roleId} value={candidate.roleId}>
                {candidate.value}
              </option>
            ))}
        </M3Select>
        <div className="flex justify-end gap-2">
          <button
            type="button"
            className="px-3 py-1 text-[11px] text-m3-secondary"
            onClick={() => setEditing(false)}
          >
            {t.cancelEdit}
          </button>
          <button
            type="submit"
            disabled={updateRole.isPending}
            className="rounded-md bg-m3-primary px-3 py-1 text-[11px] text-m3-on-primary"
          >
            {t.saveRole}
          </button>
        </div>
      </form>
    );
  }

  return (
    <div
      data-testid="entity-row"
      data-entity-id={role.roleId}
      className="group flex items-start justify-between rounded-lg border border-m3-outline/15 bg-m3-surface-container/10 p-3"
    >
      <div className="min-w-0 space-y-1">
        <div className="flex items-center gap-2">
          <span className="text-[12px] font-medium text-m3-on-surface">{role.value}</span>
          <CodeBadge code={role.code} size="xs" />
          <span
            data-testid="status-badge"
            data-status={role.isActive ? 'Active' : 'Inactive'}
            className={`rounded-full px-2.5 py-0.5 text-[10px] font-medium ${role.isActive ? 'bg-emerald-500/10 text-emerald-500 border border-emerald-500/20' : 'bg-m3-outline/10 text-m3-secondary border border-m3-outline/20'}`}
          >
            {role.isActive ? t.active : t.inactive}
          </span>
        </div>
        <p className="truncate text-[10px] text-m3-secondary">
          {role.description || t.noDescription}
        </p>
        <p className="text-[9px] text-m3-secondary">
          {t.roleLevel}: {role.hierarchyLevel}
        </p>
      </div>
      <div className="flex gap-1 opacity-0 transition-opacity group-hover:opacity-100">
        <IconButton tooltip={t.edit} onClick={() => setEditing(true)}>
          <Pencil className="h-3.5 w-3.5" />
        </IconButton>
        <IconButton
          tooltip={role.isActive ? t.deactivate : t.activate}
          disabled={changingStatus}
          onClick={() => onStatusChange(!role.isActive)}
        >
          {role.isActive ? (
            <ShieldOff className="h-3.5 w-3.5" />
          ) : (
            <ShieldCheck className="h-3.5 w-3.5" />
          )}
        </IconButton>
      </div>
    </div>
  );
};
