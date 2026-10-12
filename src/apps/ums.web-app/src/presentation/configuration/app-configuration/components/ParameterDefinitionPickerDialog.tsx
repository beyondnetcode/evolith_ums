/**
 * ParameterDefinitionPickerDialog
 * Modal to select parameter definitions from catalog to add to AppConfiguration
 * - Only Global (1) and Global&Tenant (3) scopes are selectable
 * - Multi-select enabled
 * - Shows only available (non-linked) parameters
 */
import React, { useState, useEffect } from 'react';
import { Search, Tag, Check } from 'lucide-react';
import { useI18n } from '@app/i18n/use-i18n';
import { M3FormDialog } from '@shared/components/M3FormDialog';
import { M3Button } from '@shared/components/M3Button';
import { StatusBadge } from '@shared/components/StatusBadge';
import { CodeBadge } from '@shared/components/CodeBadge';
import {
  DataTypeLabels,
  ScopeLabels,
  type ParameterDefinition,
} from '@domain/configuration/schemas/parameter-catalog/parameter-definition.schema';
import { parameterCatalogService } from '@infra/configuration/services/parameter-catalog/parameter-catalog.service';

interface ParameterDefinitionPickerDialogProps {
  isOpen: boolean;
  onClose: () => void;
  onSelect: (parameters: ParameterDefinition[]) => void;
  existingCodes: string[];
}

const ALLOWED_SCOPES = [1, 3];

export function ParameterDefinitionPickerDialog({
  isOpen,
  onClose,
  onSelect,
  existingCodes = [],
}: ParameterDefinitionPickerDialogProps): React.JSX.Element {
  const t = useI18n();
  const [parameters, setParameters] = useState<(ParameterDefinition & { isLinked?: boolean })[]>(
    []
  );
  const [isLoading, setIsLoading] = useState(false);
  const [searchTerm, setSearchTerm] = useState('');
  const [dataTypeFilter, setDataTypeFilter] = useState<string>('all');
  const [statusFilter, setStatusFilter] = useState<string>('all');
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());

  useEffect(() => {
    if (isOpen) {
      loadParameters();
    }
  }, [isOpen, searchTerm, dataTypeFilter, statusFilter]);

  useEffect(() => {
    if (!isOpen) {
      setSearchTerm('');
      setDataTypeFilter('all');
      setStatusFilter('all');
      setSelectedIds(new Set());
    }
  }, [isOpen]);

  const loadParameters = async () => {
    setIsLoading(true);
    try {
      const filter: Record<string, string> = {};
      if (searchTerm) filter.search = searchTerm;
      if (dataTypeFilter !== 'all') filter.dataTypeId = dataTypeFilter;
      if (statusFilter !== 'all') filter.isActive = statusFilter;

      const result = await parameterCatalogService.getAll(filter);
      const processed = result.items.map(p => ({
        ...p,
        isLinked: existingCodes.includes(p.code),
      }));
      setParameters(processed);
    } catch (error) {
      console.error('Failed to load parameters', error);
    } finally {
      setIsLoading(false);
    }
  };

  const toggleSelect = (id: string) => {
    const newSelected = new Set(selectedIds);
    if (newSelected.has(id)) {
      newSelected.delete(id);
    } else {
      newSelected.add(id);
    }
    setSelectedIds(newSelected);
  };

  const handleSelect = () => {
    const selected = parameters.filter(p => selectedIds.has(p.id));
    if (selected.length > 0) {
      onSelect(selected);
      onClose();
    }
  };

  const selectableParams = parameters.filter(
    p =>
      !p.isLinked &&
      ALLOWED_SCOPES.includes(p.scopeId) &&
      p.defaultValue !== null &&
      p.defaultValue.trim() !== ''
  );
  const paramsWithoutDefault = parameters.filter(
    p =>
      !p.isLinked &&
      ALLOWED_SCOPES.includes(p.scopeId) &&
      (p.defaultValue === null || p.defaultValue.trim() === '')
  );
  const hasLinkedParams = parameters.some(p => p.isLinked);
  const selectedCount = selectedIds.size;

  return (
    <M3FormDialog
      open={isOpen}
      onClose={onClose}
      title={t.selectParameters ?? 'Select Parameters'}
      icon={<Tag className="w-5 h-5" />}
      footer={
        <>
          <M3Button variant="text" onClick={onClose}>
            {t.cancelBtn ?? 'Cancel'}
          </M3Button>
          <M3Button variant="filled" onClick={handleSelect} disabled={selectedCount === 0}>
            {selectedCount > 0
              ? `${t.addParameters ?? 'Add'} (${selectedCount})`
              : (t.addParameters ?? 'Add Parameters')}
          </M3Button>
        </>
      }
    >
      <div className="space-y-4">
        <div className="relative">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-m3-outline" />
          <input
            type="text"
            value={searchTerm}
            onChange={e => setSearchTerm(e.target.value)}
            placeholder={t.searchParameters ?? 'Search parameters...'}
            className="w-full pl-10 pr-4 py-2.5 text-[12px] border border-m3-outline/30 rounded-lg bg-m3-surface focus:outline-none focus:ring-2 focus:ring-m3-primary/30"
          />
        </div>

        <div className="flex gap-2 flex-wrap">
          <select
            value={dataTypeFilter}
            onChange={e => setDataTypeFilter(e.target.value)}
            className="text-[11px] px-2 py-1.5 border border-m3-outline/30 rounded-lg bg-m3-surface"
          >
            <option value="all">{t.allDataTypes ?? 'All Types'}</option>
            <option value="1">{DataTypeLabels[1]}</option>
            <option value="2">{DataTypeLabels[2]}</option>
            <option value="3">{DataTypeLabels[3]}</option>
            <option value="4">{DataTypeLabels[4]}</option>
          </select>

          <select
            value={statusFilter}
            onChange={e => setStatusFilter(e.target.value)}
            className="text-[11px] px-2 py-1.5 border border-m3-outline/30 rounded-lg bg-m3-surface"
          >
            <option value="all">{t.allStatuses ?? 'All Statuses'}</option>
            <option value="true">{t.active ?? 'Active'}</option>
            <option value="false">{t.inactive ?? 'Inactive'}</option>
          </select>
        </div>

        <div className="text-[12px] text-m3-secondary">
          {t.onlyGlobalParameters ?? 'Only Global and Global&Tenant parameters can be selected'}
        </div>

        <div className="border border-m3-outline/20 rounded-lg overflow-hidden max-h-[320px] overflow-y-auto">
          {isLoading ? (
            <div className="p-8 text-center text-[12px] text-m3-secondary">
              {t.loading ?? 'Loading...'}
            </div>
          ) : selectableParams.length === 0 ? (
            <div className="p-8 text-center text-[12px] text-m3-secondary">
              {paramsWithoutDefault.length > 0
                ? (t.parametersNeedDefaultValue ??
                  'Some parameters need a default value defined first')
                : hasLinkedParams
                  ? (t.allParametersLinked ?? 'All available parameters are already linked')
                  : (t.noParametersAvailable ?? 'No parameters available')}
            </div>
          ) : (
            <div className="divide-y divide-m3-outline/10">
              {selectableParams.map(param => {
                const isSelected = selectedIds.has(param.id);

                return (
                  <button
                    key={param.id}
                    onClick={() => toggleSelect(param.id)}
                    className={`w-full px-4 py-3 text-left transition-colors flex items-center justify-between gap-3 ${
                      isSelected ? 'bg-m3-primary/10' : 'hover:bg-m3-surface-variant'
                    }`}
                  >
                    <div className="flex items-center gap-3">
                      <div
                        className={`w-5 h-5 rounded border-2 flex items-center justify-center transition-colors ${
                          isSelected ? 'bg-m3-primary border-m3-primary' : 'border-m3-outline'
                        }`}
                      >
                        {isSelected && <Check className="w-3 h-3 text-white" />}
                      </div>
                      <div className="flex-1 min-w-0">
                        <div className="flex items-center gap-2 flex-wrap">
                          <span className="text-[12px] font-medium text-m3-on-surface">
                            {param.name}
                          </span>
                          <CodeBadge code={param.code} size="xs" />
                          <span className="text-[10px] px-1.5 py-0.5 rounded bg-blue-50 text-blue-700">
                            {ScopeLabels[param.scopeId]}
                          </span>
                          <span className="text-[10px] px-1.5 py-0.5 rounded bg-slate-100 text-slate-700">
                            {DataTypeLabels[param.dataTypeId]}
                          </span>
                          <StatusBadge
                            status={param.isActive ? 'Active' : 'Inactive'}
                            label={param.isActive ? 'Activo' : 'Inactivo'}
                            size="xs"
                          />
                        </div>
                        {param.description && (
                          <p className="text-[12px] text-m3-on-surface-variant mt-0.5 truncate">
                            {param.description}
                          </p>
                        )}
                      </div>
                    </div>
                  </button>
                );
              })}
            </div>
          )}
        </div>
      </div>
    </M3FormDialog>
  );
}
