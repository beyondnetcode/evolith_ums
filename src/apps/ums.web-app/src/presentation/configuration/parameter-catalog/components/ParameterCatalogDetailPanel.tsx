/**
 * ParameterCatalogDetailPanel
 * Detail view for a parameter definition - minimalist design
 */
import React from 'react';
import { Edit2, Trash2 } from 'lucide-react';
import type { ParameterDefinition } from '@domain/configuration/schemas/parameter-catalog/parameter-definition.schema';
import {
  DataTypeLabels,
  ScopeLabels,
} from '@domain/configuration/schemas/parameter-catalog/parameter-definition.schema';
import { M3Card } from '@shared/components/M3Card';
import { useI18n } from '@app/i18n/use-i18n';
import { LoadingSpinner } from '@shared/components/LoadingSpinner';

interface ParameterCatalogDetailPanelProps {
  definitionId?: string;
  activeParameter?: ParameterDefinition;
  isLoading: boolean;
  onEdit?: () => void;
  onDelete?: () => void;
}

export const ParameterCatalogDetailPanel: React.FC<ParameterCatalogDetailPanelProps> = ({
  definitionId,
  activeParameter,
  isLoading,
  onEdit,
  onDelete,
}) => {
  const t = useI18n();

  if (isLoading) {
    return (
      <div className="h-full flex items-center justify-center">
        <LoadingSpinner />
      </div>
    );
  }

  if (!activeParameter) {
    return (
      <div className="h-full flex items-center justify-center p-8">
        <p className="text-[12px] text-m3-secondary">
          {t.selectParameterToView ?? 'Select a parameter to view details'}
        </p>
      </div>
    );
  }

  return (
    <div className="h-full flex flex-col">
      <div className="flex items-center justify-between px-4 py-3 border-b border-m3-outline/10">
        <div className="flex items-center gap-3 min-w-0">
          <span className="text-[12px] font-medium text-m3-on-surface truncate">
            {activeParameter.name}
          </span>
          <span
            className={`text-[10px] px-1.5 py-0.5 rounded shrink-0 ${
              activeParameter.isActive
                ? 'bg-emerald-100 text-emerald-700'
                : 'bg-amber-100 text-amber-700'
            }`}
          >
            {activeParameter.isActive ? (t.active ?? 'Active') : (t.inactive ?? 'Inactive')}
          </span>
        </div>
        <div className="flex items-center gap-0.5 shrink-0">
          {onEdit && (
            <button
              onClick={onEdit}
              className="p-1.5 rounded hover:bg-m3-surface-variant text-m3-secondary hover:text-m3-primary transition-colors"
              title={t.editBtn ?? 'Edit'}
            >
              <Edit2 className="w-4 h-4" />
            </button>
          )}
          {onDelete && (
            <button
              onClick={onDelete}
              className="p-1.5 rounded hover:bg-rose-50 text-m3-secondary hover:text-rose-500 transition-colors"
              title={t.deleteBtn ?? 'Delete'}
            >
              <Trash2 className="w-4 h-4" />
            </button>
          )}
        </div>
      </div>

      <div className="flex-1 overflow-y-auto p-4 space-y-4">
        <div className="text-[11px] text-m3-secondary font-mono">{activeParameter.code}</div>

        {activeParameter.description && (
          <p className="text-[12px] text-m3-on-surface-variant">{activeParameter.description}</p>
        )}

        <div className="flex flex-wrap gap-4">
          <div>
            <span className="text-[10px] text-m3-secondary/60 uppercase">Type</span>
            <p className="text-[12px] font-medium text-m3-on-surface">
              {DataTypeLabels[activeParameter.dataTypeId] ?? String(activeParameter.dataTypeId)}
            </p>
          </div>
          <div>
            <span className="text-[10px] text-m3-secondary/60 uppercase">Scope</span>
            <p className="text-[12px] font-medium text-m3-on-surface">
              {ScopeLabels[activeParameter.scopeId] ?? String(activeParameter.scopeId)}
            </p>
          </div>
          <div>
            <span className="text-[10px] text-m3-secondary/60 uppercase">Default</span>
            <p className="text-[12px] font-mono text-m3-on-surface">
              {activeParameter.defaultValue || '-'}
            </p>
          </div>
        </div>

        <div className="flex gap-6 pt-2 border-t border-m3-outline/10">
          <div>
            <span className="text-[10px] text-m3-secondary/60 uppercase">Order</span>
            <p className="text-[12px] text-m3-on-surface">{activeParameter.displayOrder}</p>
          </div>
          <div>
            <span className="text-[10px] text-m3-secondary/60 uppercase">Version</span>
            <p className="text-[12px] text-m3-on-surface font-mono">{activeParameter.version}</p>
          </div>
          {activeParameter.isMandatory && (
            <div>
              <span className="text-[10px] text-m3-secondary/60 uppercase">Required</span>
              <p className="text-[12px] text-orange-600">Yes</p>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};
