import React, { useState } from 'react';
import {
  Flag,
  Trash2,
  Plus,
  CheckCircle2,
  Archive,
  ToggleLeft,
  ToggleRight,
  Info,
} from 'lucide-react';
import type {
  FeatureFlag,
  FeatureFlagCriteria,
} from '@domain/configuration/models/feature-flag.model';
import {
  FLAG_TYPE_LABELS,
  CRITERIA_TYPE_LABELS,
  CRITERIA_OPERATOR_LABELS,
} from '@domain/configuration/constants/feature-flag.constants';

import { CodeBadge } from '@shared/components/CodeBadge';
import { KeyValueRow } from '@shared/components/KeyValueRow';
import { M3Button } from '@shared/components/M3Button';
import { M3TextField } from '@shared/components/M3TextField';
import { EmptyDetailState } from '@shared/components/EmptyDetailState';
import { DetailSection } from '@shared/components/DetailSection';
import { IconButton, Tooltip } from '@shared/components/Tooltip';
import { AddButtonInline } from '@shared/components/AddButton';
import { STATUS_COLORS } from '@shared/utils/status-utils';
import {
  useActivateFlag,
  useDeactivateFlag,
  useArchiveFlag,
  useAddFeatureFlagCriteria,
  useRemoveFeatureFlagCriteria,
} from '@app/configuration/hooks/use-feature-flag';

interface Props {
  flag: FeatureFlag | undefined;
}

const TYPE_COLOR: Record<string, string> = {
  Boolean: 'bg-blue-500/10 text-blue-500',
  Variant: 'bg-purple-500/10 text-purple-500',
  Percentage: 'bg-emerald-500/10 text-emerald-500',
};

const getSystemSuiteLabel = (flag: FeatureFlag): string => {
  const suiteName = flag.systemSuiteName?.trim();
  const suiteCode = flag.systemSuiteCode?.trim();

  if (suiteName && suiteCode) return `${suiteName} (${suiteCode})`;
  if (suiteName) return suiteName;
  if (suiteCode) return suiteCode;
  return 'Sistema desconocido';
};

const CriteriaRow: React.FC<{
  criteria: FeatureFlagCriteria;
  onRemove: () => void;
  isDraft: boolean;
}> = ({ criteria, onRemove, isDraft }) => (
  <div className="flex items-center gap-3 p-3 rounded-lg border border-m3-outline/10 bg-m3-surface-container/5">
    <div className="flex-1 grid grid-cols-1 sm:grid-cols-3 gap-3 text-[11px]">
      <div>
        <span className="text-[10px] font-semibold text-m3-secondary/60 uppercase tracking-wider">
          Tipo
        </span>
        <p className="text-m3-on-surface font-medium mt-0.5">
          {CRITERIA_TYPE_LABELS[criteria.criteriaType] ?? criteria.criteriaType}
        </p>
      </div>
      <div>
        <span className="text-[10px] font-semibold text-m3-secondary/60 uppercase tracking-wider">
          Operador
        </span>
        <p className="text-m3-on-surface font-medium mt-0.5">
          {CRITERIA_OPERATOR_LABELS[criteria.operator] ?? criteria.operator}
        </p>
      </div>
      <div>
        <span className="text-[10px] font-semibold text-m3-secondary/60 uppercase tracking-wider">
          Valor
        </span>
        <div className="mt-0.5">
          <CodeBadge code={criteria.value} size="sm" />
        </div>
      </div>
    </div>
    {isDraft && (
      <IconButton
        tooltip="Eliminar criterio"
        onClick={onRemove}
        className="hover:text-rose-500 hover:bg-rose-500/10"
      >
        <Trash2 className="w-4 h-4" />
      </IconButton>
    )}
  </div>
);

export const FeatureFlagDetailPanel: React.FC<Props> = ({ flag }) => {
  const [criteriaType, setCriteriaType] = useState('TenantId');
  const [criteriaOperator, setCriteriaOperator] = useState('Equals');
  const [criteriaValue, setCriteriaValue] = useState('');
  const [criteriaError, setCriteriaError] = useState('');

  const activateFlag = useActivateFlag(flag?.featureFlagId ?? '');
  const deactivateFlag = useDeactivateFlag(flag?.featureFlagId ?? '');
  const archiveFlag = useArchiveFlag(flag?.featureFlagId ?? '');
  const addCriteria = useAddFeatureFlagCriteria(flag?.featureFlagId ?? '');
  const removeCriteria = useRemoveFeatureFlagCriteria(flag?.featureFlagId ?? '');

  if (!flag) {
    return (
      <EmptyDetailState
        icon={Flag}
        title="Seleccione un feature flag"
        description="Los feature flags permiten controlar funcionalidades por tenant, rol, ambiente y más."
      />
    );
  }

  const isDraft = flag.status === 'Inactive';
  const StatusIcon =
    flag.status === 'Active' ? CheckCircle2 : flag.status === 'Archived' ? Archive : ToggleLeft;
  const statusColor = STATUS_COLORS[flag.status] ?? STATUS_COLORS.Draft;

  const handleAddCriteria = async (e: React.FormEvent) => {
    e.preventDefault();
    setCriteriaError('');
    if (!criteriaValue.trim()) {
      setCriteriaError('Valor requerido');
      return;
    }
    try {
      await addCriteria.mutateAsync({
        criteriaType: criteriaType as FeatureFlagCriteria['criteriaType'],
        operator: criteriaOperator as FeatureFlagCriteria['operator'],
        value: criteriaValue.trim(),
      });
      setCriteriaValue('');
    } catch {
      /* handled by hook */
    }
  };

  return (
    <div className="p-4 space-y-4">
      {/* Header */}
      <div className="flex items-center gap-3">
        <div className={`p-2 rounded-lg ${TYPE_COLOR[flag.flagType]}`}>
          <Flag className="w-5 h-5" />
        </div>
        <div className="flex-1 min-w-0">
          <h3 className="text-base font-bold text-m3-on-surface truncate">{flag.flagCode}</h3>
          <div className="flex items-center gap-2 mt-1">
            <span
              className={`text-[10px] font-bold uppercase px-1.5 py-0.5 rounded ${TYPE_COLOR[flag.flagType]}`}
            >
              {FLAG_TYPE_LABELS[flag.flagType]}
            </span>
          </div>
        </div>
      </div>

      {/* Status */}
      <div className={`p-4 rounded-xl border flex items-start gap-3 ${statusColor}`}>
        <StatusIcon className="w-5 h-5 shrink-0 mt-0.5" />
        <div>
          <p className="text-[12px] font-medium text-m3-on-surface">Estado: {flag.status}</p>
          <p className="text-[12px] text-m3-secondary mt-0.5">
            {flag.status === 'Active' && 'El flag está activo y evaluando targeting rules.'}
            {flag.status === 'Inactive' && 'El flag está inactivo. Puede ser activado o archivado.'}
            {flag.status === 'Archived' &&
              'El flag está archivado (estado terminal). No puede ser reactivado.'}
          </p>
        </div>
      </div>

      {/* Info */}
      <DetailSection
        title="Información"
        content={
          <div className="space-y-2">
            <KeyValueRow label="System Suite" value={getSystemSuiteLabel(flag)} />
            <KeyValueRow label="Flag Type" value={FLAG_TYPE_LABELS[flag.flagType]} />
            <KeyValueRow label="Targeting" value={flag.flagTargets} />
            {flag.rolloutPercentage != null && (
              <KeyValueRow label="Rollout %" value={`${flag.rolloutPercentage}%`} />
            )}
            {flag.linkedResourceType && (
              <KeyValueRow label="Linked Resource" value={flag.linkedResourceType} />
            )}
          </div>
        }
      />

      {/* Actions */}
      <DetailSection
        title="Acciones"
        content={
          <div className="flex gap-2">
            {flag.status === 'Inactive' && (
              <>
                <M3Button
                  variant="filled"
                  icon={<ToggleRight className="w-4 h-4" />}
                  onClick={() => activateFlag.mutate()}
                  disabled={activateFlag.isPending}
                  className="flex-1"
                >
                  Activar
                </M3Button>
                <M3Button
                  variant="outlined"
                  icon={<Archive className="w-4 h-4" />}
                  onClick={() => archiveFlag.mutate()}
                  disabled={archiveFlag.isPending}
                  className="flex-1"
                >
                  Archivar
                </M3Button>
              </>
            )}
            {flag.status === 'Active' && (
              <M3Button
                variant="tonal"
                icon={<ToggleLeft className="w-4 h-4" />}
                onClick={() => deactivateFlag.mutate()}
                disabled={deactivateFlag.isPending}
                className="flex-1"
              >
                Desactivar
              </M3Button>
            )}
            {flag.status === 'Archived' && (
              <div className="flex-1 py-2.5 px-4 rounded-full border border-m3-outline/20 text-[11px] text-m3-secondary/50 text-center">
                Archivado — acción terminal
              </div>
            )}
          </div>
        }
      />

      {/* Criteria */}
      <DetailSection
        title={`Criterios de Evaluación (${flag.criteria.length})`}
        actions={
          isDraft && (
            <Tooltip content="Agregar criterio">
              <AddButtonInline onClick={() => {}} />
            </Tooltip>
          )
        }
        content={
          <>
            {flag.criteria.length === 0 && (
              <div className="flex items-center gap-2 text-[11px] text-m3-secondary/70 bg-m3-surface-variant/20 p-3 rounded-lg">
                <Info className="w-4 h-4" />
                Sin criterios. El flag se evalúa solo por targeting rules.
              </div>
            )}

            <div className="space-y-2">
              {flag.criteria.map(c => (
                <CriteriaRow
                  key={c.criteriaId}
                  criteria={c}
                  onRemove={() => removeCriteria.mutate(c.criteriaId)}
                  isDraft={isDraft}
                />
              ))}
            </div>

            {isDraft && (
              <form
                onSubmit={handleAddCriteria}
                className="mt-3 p-3 rounded-lg border border-m3-outline/10 bg-m3-surface-container/10 space-y-3"
              >
                <p className="text-[12px] font-medium text-m3-primary flex items-center gap-1.5">
                  <Plus className="w-4 h-4" />
                </p>
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
                  <select
                    value={criteriaType}
                    onChange={e => setCriteriaType(e.target.value)}
                    className="w-full h-9 px-3 rounded-lg bg-m3-surface border border-m3-outline/20 text-[11px] focus:border-m3-primary outline-none"
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
                    className="w-full h-9 px-3 rounded-lg bg-m3-surface border border-m3-outline/20 text-[11px] focus:border-m3-primary outline-none"
                  >
                    {Object.entries(CRITERIA_OPERATOR_LABELS).map(([k, v]) => (
                      <option key={k} value={k}>
                        {v}
                      </option>
                    ))}
                  </select>
                </div>
                <M3TextField
                  label="Valor"
                  value={criteriaValue}
                  onChange={e => {
                    setCriteriaValue(e.target.value);
                    setCriteriaError('');
                  }}
                  placeholder="e.g. tenant-id, role-code, percentage"
                  size="sm"
                />
                {criteriaError && <p className="text-[11px] text-m3-error">{criteriaError}</p>}
                <div className="flex justify-end">
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
          </>
        }
      />
    </div>
  );
};
