/**
 * ParameterDefinitionProfileCard
 * Header card for parameter definition detail view
 */
import React from 'react';
import { Tag, Edit2 } from 'lucide-react';
import type { ParameterDefinition } from '@domain/configuration/schemas/parameter-catalog/parameter-definition.schema';
import {
  DataTypeLabels,
  ScopeLabels,
} from '@domain/configuration/schemas/parameter-catalog/parameter-definition.schema';
import { StatusBadge } from '@shared/components/StatusBadge';
import { CodeBadge } from '@shared/components/CodeBadge';
import { M3Card } from '@shared/components/M3Card';
import { useI18n } from '@app/i18n/use-i18n';
import { STATUS_COLORS } from '@shared/utils/status-utils';

const STATUS_COLOR_MAP = {
  Active: STATUS_COLORS.Active,
  Inactive: STATUS_COLORS.Inactive,
};

const SCOPE_COLOR: Record<number, string> = {
  1: 'bg-blue-500/10 text-blue-500 border border-blue-500/20',
  2: 'bg-green-500/10 text-green-500 border border-green-500/20',
  3: 'bg-purple-500/10 text-purple-500 border border-purple-500/20',
};

const DATA_TYPE_COLOR: Record<number, string> = {
  1: 'bg-slate-500/10 text-slate-500',
  2: 'bg-orange-500/10 text-orange-500',
  3: 'bg-cyan-500/10 text-cyan-500',
  4: 'bg-rose-500/10 text-rose-500',
};

interface ParameterDefinitionProfileCardProps {
  parameter: ParameterDefinition;
  onEdit?: () => void;
}

export const ParameterDefinitionProfileCard: React.FC<ParameterDefinitionProfileCardProps> = ({
  parameter,
  onEdit,
}) => {
  const t = useI18n();

  return (
    <M3Card variant="elevated" className="p-4 border border-m3-outline/25 bg-m3-surface">
      <div className="flex items-start justify-between gap-4">
        <div className="flex items-start gap-3">
          <div className="p-2.5 rounded-xl bg-m3-primary/10">
            <Tag className="w-5 h-5 text-m3-primary" />
          </div>
          <div className="flex flex-col gap-1">
            <h2 className="text-lg font-semibold text-m3-on-surface">{parameter.name}</h2>
            <div className="flex items-center gap-2 flex-wrap">
              <CodeBadge code={parameter.code} size="sm" />
              <StatusBadge
                status={parameter.isActive ? 'Active' : 'Inactive'}
                label={parameter.isActive ? t.active : t.inactive}
                colorMap={STATUS_COLOR_MAP}
              />
              {parameter.isMandatory && (
                <span className="text-[10px] font-medium text-orange-600 bg-orange-50 px-2.5 py-0.5 rounded-full border border-orange-200">
                  {t.mandatory ?? 'Required'}
                </span>
              )}
            </div>
          </div>
        </div>
        {onEdit && (
          <button
            onClick={onEdit}
            className="p-2 rounded-lg hover:bg-m3-surface-variant transition-colors text-m3-secondary"
            title={t.editBtn ?? 'Edit'}
          >
            <Edit2 className="w-4 h-4" />
          </button>
        )}
      </div>

      {parameter.description && (
        <p className="mt-3 text-[12px] text-m3-on-surface-variant">{parameter.description}</p>
      )}

      <div className="flex items-center gap-3 mt-4 pt-3 border-t border-m3-outline/20 flex-wrap">
        <div className="flex items-center gap-1.5">
          <span className="text-[10px] font-semibold uppercase tracking-wide text-m3-secondary/60">
            Type
          </span>
          <span
            className={`text-[11px] font-bold uppercase px-2 py-0.5 rounded ${DATA_TYPE_COLOR[parameter.dataTypeId]}`}
          >
            {DataTypeLabels[parameter.dataTypeId]}
          </span>
        </div>
        <div className="flex items-center gap-1.5">
          <span className="text-[10px] font-semibold uppercase tracking-wide text-m3-secondary/60">
            Scope
          </span>
          <span
            className={`text-[11px] font-bold uppercase px-2 py-0.5 rounded ${SCOPE_COLOR[parameter.scopeId]}`}
          >
            {ScopeLabels[parameter.scopeId]}
          </span>
        </div>
        <div className="flex items-center gap-1.5">
          <span className="text-[10px] font-semibold uppercase tracking-wide text-m3-secondary/60">
            Order
          </span>
          <span className="text-[11px] font-medium text-m3-on-surface">
            {parameter.displayOrder}
          </span>
        </div>
        <div className="flex items-center gap-1.5">
          <span className="text-[10px] font-semibold uppercase tracking-wide text-m3-secondary/60">
            Version
          </span>
          <span className="text-[11px] font-mono text-m3-on-surface">{parameter.version}</span>
        </div>
      </div>
    </M3Card>
  );
};
