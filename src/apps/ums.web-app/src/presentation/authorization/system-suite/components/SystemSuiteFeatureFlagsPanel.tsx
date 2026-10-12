import React, { useState } from 'react';
import {
  Flag,
  Plus,
  Trash2,
  ToggleLeft,
  ToggleRight,
  Archive,
  Info,
  ChevronDown,
  ChevronRight,
} from 'lucide-react';
import { useGetFeatureFlagsBySystemSuite } from '@app/configuration/hooks/use-feature-flag';
import {
  useActivateFlag,
  useDeactivateFlag,
  useArchiveFlag,
  useAddFeatureFlagCriteria,
  useRemoveFeatureFlagCriteria,
} from '@app/configuration/hooks/use-feature-flag';
import { StatusBadge } from '@shared/components/StatusBadge';
import { CodeBadge } from '@shared/components/CodeBadge';
import { M3Button } from '@shared/components/M3Button';
import { ListToolbar } from '@shared/components/ListToolbar';
import { EmptyState } from '@shared/components/EmptyState';
import { AddButtonInline } from '@shared/components/AddButton';
import { getHttpErrorMessage, getSupportReferenceId } from '@app/errors/http-error';
import {
  FLAG_TYPE_LABELS,
  CRITERIA_TYPE_LABELS,
  CRITERIA_OPERATOR_LABELS,
} from '@domain/configuration/constants/feature-flag.constants';
import type {
  FeatureFlag,
  FeatureFlagCriteria,
} from '@domain/configuration/models/feature-flag.model';
import { STATUS_COLORS, getStatusLabel } from '@shared/utils/status-utils';

interface SystemSuiteFeatureFlagsPanelProps {
  systemSuiteId: string;
}

const STATUS_COLOR_MAP = {
  Active: STATUS_COLORS.Active,
  Inactive: STATUS_COLORS.Inactive,
  Archived: STATUS_COLORS.Archived,
};

const TYPE_COLOR: Record<string, string> = {
  Boolean: 'bg-blue-500/10 text-blue-500',
  Variant: 'bg-purple-500/10 text-purple-500',
  Percentage: 'bg-emerald-500/10 text-emerald-500',
};

const CriteriaRow: React.FC<{
  criteriaType: string;
  operator: string;
  value: string;
  onRemove: () => void;
}> = ({ criteriaType, operator, value, onRemove }) => (
  <div className="flex items-center gap-3 p-1.5 rounded border border-m3-outline/10 bg-m3-surface-container/5">
    <div className="flex-1 grid grid-cols-1 sm:grid-cols-3 gap-2 text-[10px]">
      <div>
        <span className="text-[8px] font-semibold uppercase tracking-wide text-m3-secondary/50">
          Tipo
        </span>
        <p className="text-m3-on-surface">{CRITERIA_TYPE_LABELS[criteriaType] ?? criteriaType}</p>
      </div>
      <div>
        <span className="text-[8px] font-semibold uppercase tracking-wide text-m3-secondary/50">
          Operador
        </span>
        <p className="text-m3-on-surface">{CRITERIA_OPERATOR_LABELS[operator] ?? operator}</p>
      </div>
      <div>
        <span className="text-[8px] font-semibold uppercase tracking-wide text-m3-secondary/50">
          Valor
        </span>
        <CodeBadge code={value} size="xs" />
      </div>
    </div>
    <button
      onClick={onRemove}
      className="p-0.5 rounded text-m3-secondary hover:text-rose-500 hover:bg-rose-500/10 transition-colors"
      title="Eliminar criterio"
    >
      <Trash2 className="w-3 h-3" />
    </button>
  </div>
);

const FlagRow: React.FC<{
  flag: FeatureFlag;
}> = ({ flag }) => {
  const [expanded, setExpanded] = useState(false);
  const [criteriaType, setCriteriaType] = useState('TenantId');
  const [criteriaOperator, setCriteriaOperator] = useState('Equals');
  const [criteriaValue, setCriteriaValue] = useState('');
  const [showCriteriaForm, setShowCriteriaForm] = useState(false);

  const isDraft = flag.status === 'Inactive';
  const activateFlag = useActivateFlag(flag.featureFlagId);
  const deactivateFlag = useDeactivateFlag(flag.featureFlagId);
  const archiveFlag = useArchiveFlag(flag.featureFlagId);
  const addCriteria = useAddFeatureFlagCriteria(flag.featureFlagId);
  const removeCriteria = useRemoveFeatureFlagCriteria(flag.featureFlagId);

  const handleAddCriteria = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!criteriaValue.trim()) return;
    try {
      await addCriteria.mutateAsync({
        criteriaType: criteriaType as FeatureFlagCriteria['criteriaType'],
        operator: criteriaOperator as FeatureFlagCriteria['operator'],
        value: criteriaValue.trim(),
      });
      setCriteriaValue('');
      setShowCriteriaForm(false);
    } catch {
      /* handled by hook */
    }
  };

  return (
    <div className="border border-m3-outline/10 rounded-lg bg-m3-surface-container/5 overflow-hidden">
      <div
        className="flex items-center gap-3 p-3 cursor-pointer hover:bg-m3-surface-container/10 transition-colors"
        onClick={() => setExpanded(!expanded)}
      >
        <div className={`p-1.5 rounded ${TYPE_COLOR[flag.flagType]}`}>
          <Flag className="w-3.5 h-3.5" />
        </div>
        <div className="flex-1 min-w-0">
          <p className="text-[12px] font-medium text-m3-on-surface truncate">{flag.flagCode}</p>
          <div className="flex items-center gap-2 mt-0.5">
            <span className="text-[10px] text-m3-secondary">Target: {flag.flagTargets}</span>
            {flag.rolloutPercentage != null && (
              <>
                <span className="text-[8px] text-m3-secondary/30">·</span>
                <span className="text-[10px] text-m3-secondary">
                  Rollout: {flag.rolloutPercentage}%
                </span>
              </>
            )}
            {flag.criteria.length > 0 && (
              <>
                <span className="text-[8px] text-m3-secondary/30">·</span>
                <span className="text-[10px] text-m3-secondary">
                  {flag.criteria.length} criterio{flag.criteria.length > 1 ? 's' : ''}
                </span>
              </>
            )}
          </div>
        </div>
        <span
          className={`text-[10px] font-semibold uppercase px-2.5 py-0.5 rounded-full flex-shrink-0 ${TYPE_COLOR[flag.flagType]}`}
        >
          {FLAG_TYPE_LABELS[flag.flagType]}
        </span>
        <StatusBadge
          status={flag.status}
          label={getStatusLabel(flag.status)}
          colorMap={STATUS_COLOR_MAP}
        />
        <div className="w-4 h-4 flex items-center justify-center text-m3-secondary/50">
          {expanded ? (
            <ChevronDown className="w-3.5 h-3.5" />
          ) : (
            <ChevronRight className="w-3.5 h-3.5" />
          )}
        </div>
      </div>

      {expanded && (
        <div className="px-3 pb-3 border-t border-m3-outline/10 bg-m3-surface-container/10">
          {/* Actions */}
          <div className="flex gap-1.5 mt-2 mb-3">
            {flag.status === 'Inactive' && (
              <>
                <button
                  onClick={e => {
                    e.stopPropagation();
                    activateFlag.mutate();
                  }}
                  disabled={activateFlag.isPending}
                  className="flex items-center gap-1 py-1 px-2 rounded border border-emerald-500/30 text-[10px] font-semibold text-emerald-600 bg-emerald-500/10 hover:bg-emerald-500/20 transition-colors disabled:opacity-50"
                >
                  <ToggleRight className="w-3 h-3" /> Activar
                </button>
                <button
                  onClick={e => {
                    e.stopPropagation();
                    archiveFlag.mutate();
                  }}
                  disabled={archiveFlag.isPending}
                  className="flex items-center gap-1 py-1 px-2 rounded border border-rose-500/30 text-[10px] font-semibold text-rose-600 bg-rose-500/10 hover:bg-rose-500/20 transition-colors disabled:opacity-50"
                >
                  <Archive className="w-3 h-3" /> Archivar
                </button>
              </>
            )}
            {flag.status === 'Active' && (
              <button
                onClick={e => {
                  e.stopPropagation();
                  deactivateFlag.mutate();
                }}
                disabled={deactivateFlag.isPending}
                className="flex items-center gap-1 py-1 px-2 rounded border border-amber-500/30 text-[10px] font-semibold text-amber-600 bg-amber-500/10 hover:bg-amber-500/20 transition-colors disabled:opacity-50"
              >
                <ToggleLeft className="w-3 h-3" /> Desactivar
              </button>
            )}
            {flag.status === 'Archived' && (
              <span className="text-[10px] text-m3-secondary/50 py-1 px-2">
                Archivado — acción terminal
              </span>
            )}
          </div>

          {/* Criteria */}
          <div className="space-y-1.5">
            <div className="flex items-center justify-between">
              <h5 className="text-[10px] font-bold uppercase tracking-wider text-m3-secondary">
                Criterios ({flag.criteria.length})
              </h5>
              {isDraft && (
                <AddButtonInline
                  onClick={e => {
                    e.stopPropagation();
                    setShowCriteriaForm(!showCriteriaForm);
                  }}
                  title="Agregar criterio"
                />
              )}
            </div>

            {flag.criteria.length === 0 && !showCriteriaForm && (
              <div className="flex items-center gap-1.5 text-[10px] text-m3-secondary/60 bg-m3-surface-variant/20 p-1.5 rounded">
                <Info className="w-3 h-3" />
                Sin criterios. El flag se evalúa solo por targeting rules.
              </div>
            )}

            {flag.criteria.map(c => (
              <CriteriaRow
                key={c.criteriaId}
                criteriaType={c.criteriaType}
                operator={c.operator}
                value={c.value}
                onRemove={() => removeCriteria.mutate(c.criteriaId)}
              />
            ))}

            {showCriteriaForm && isDraft && (
              <form
                onSubmit={handleAddCriteria}
                className="p-2 rounded border border-m3-outline/20 bg-m3-surface/50 space-y-1.5"
              >
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-1.5">
                  <select
                    value={criteriaType}
                    onChange={e => setCriteriaType(e.target.value)}
                    className="w-full h-7 px-1.5 rounded bg-m3-surface border border-m3-outline/20 text-[10px] focus:border-m3-primary outline-none"
                  >
                    {Object.entries(CRITERIA_TYPE_LABELS).map(([k, v]) => (
                      <option key={k} value={k}>
                        {v}
                      </option>
                    ))}
                  </select>
                  <select
                    value={criteriaOperator}
                    onChange={e => setCriteriaOperator(e.target.value)}
                    className="w-full h-7 px-1.5 rounded bg-m3-surface border border-m3-outline/20 text-[10px] focus:border-m3-primary outline-none"
                  >
                    {Object.entries(CRITERIA_OPERATOR_LABELS).map(([k, v]) => (
                      <option key={k} value={k}>
                        {v}
                      </option>
                    ))}
                  </select>
                </div>
                <div className="flex gap-1.5">
                  <input
                    type="text"
                    value={criteriaValue}
                    onChange={e => setCriteriaValue(e.target.value)}
                    placeholder="Valor..."
                    className="flex-1 h-7 px-1.5 rounded bg-m3-surface border border-m3-outline/20 text-[10px] focus:border-m3-primary outline-none"
                  />
                  <M3Button
                    type="submit"
                    variant="filled"
                    size="sm"
                    disabled={addCriteria.isPending || !criteriaValue.trim()}
                  >
                    {addCriteria.isPending ? '...' : '+'}
                  </M3Button>
                </div>
              </form>
            )}
          </div>
        </div>
      )}
    </div>
  );
};

export const SystemSuiteFeatureFlagsPanel: React.FC<SystemSuiteFeatureFlagsPanelProps> = ({
  systemSuiteId,
}) => {
  const { data: flags = [], isLoading, error } = useGetFeatureFlagsBySystemSuite(systemSuiteId);
  const [viewMode, setViewMode] = useState<'list' | 'thumbnail'>('list');
  const [activeFilter, setActiveFilter] = useState('all');
  const [sortBy, setSortBy] = useState('flagCode');
  const [sortOrder, setSortOrder] = useState<'asc' | 'desc'>('asc');

  let filteredFlags = [...flags];
  if (activeFilter !== 'all') {
    filteredFlags = filteredFlags.filter(f => f.status === activeFilter);
  }
  filteredFlags = filteredFlags.sort((a, b) => {
    let cmp = 0;
    if (sortBy === 'flagCode') cmp = a.flagCode.localeCompare(b.flagCode);
    else if (sortBy === 'flagType') cmp = a.flagType.localeCompare(b.flagType);
    else if (sortBy === 'status') cmp = a.status.localeCompare(b.status);
    return sortOrder === 'asc' ? cmp : -cmp;
  });

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-8 text-m3-secondary/50 text-[12px]">
        Cargando feature flags...
      </div>
    );
  }

  if (error) {
    const errorMessage = getHttpErrorMessage(
      error,
      'No se pudieron cargar los feature flags. Intente nuevamente.'
    );
    const supportReferenceId = getSupportReferenceId(error);

    return (
      <div className="flex items-center justify-center py-8 text-rose-500 text-[12px]">
        {supportReferenceId ? `${errorMessage} Referencia: ${supportReferenceId}` : errorMessage}
      </div>
    );
  }

  return (
    <div className="space-y-3">
      <ListToolbar
        viewMode={viewMode}
        onViewModeChange={setViewMode}
        filterOptions={[
          { label: 'Todos', value: 'all' },
          { label: 'Activo', value: 'Active' },
          { label: 'Inactivo', value: 'Inactive' },
          { label: 'Archivado', value: 'Archived' },
        ]}
        activeFilter={activeFilter}
        onFilterChange={setActiveFilter}
        sortOptions={[
          { label: 'Código', value: 'flagCode' },
          { label: 'Tipo', value: 'flagType' },
          { label: 'Estado', value: 'status' },
        ]}
        sortBy={sortBy}
        onSortByChange={setSortBy}
        sortOrder={sortOrder}
        onSortOrderToggle={() => setSortOrder(o => (o === 'asc' ? 'desc' : 'asc'))}
        itemCount={flags.length}
        itemLabel="flag"
        itemLabelPlural="flags"
      />

      {filteredFlags.length === 0 ? (
        <EmptyState
          icon={<Flag className="w-6 h-6" />}
          message="No hay feature flags para este suite"
          tooltip="Cree feature flags desde la sección de Feature Flags para controlar funcionalidades de este suite."
        />
      ) : (
        <div className="space-y-2">
          {filteredFlags.map(flag => (
            <FlagRow key={flag.featureFlagId} flag={flag} />
          ))}
        </div>
      )}
    </div>
  );
};
