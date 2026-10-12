/**
 * FeatureFlagForm
 * Create new feature flag - minimalist professional design
 */
import React, { useState } from 'react';
import { Flag, Check } from 'lucide-react';
import { M3FormDialog } from '@shared/components/M3FormDialog';
import { FormField, FormInput, FormSelect, FormButton } from '@shared/components/form';
import { useCreateFeatureFlag } from '@app/configuration/hooks/use-feature-flag';
import { useGetAllSystemSuites } from '@app/authorization/hooks/use-system-suite';
import { FLAG_TYPE_LABELS } from '@domain/configuration/constants/feature-flag.constants';
import type {
  CreateFeatureFlagPayload,
  FlagType,
} from '@domain/configuration/models/feature-flag.model';

interface Props {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: (featureFlagId: string) => void;
}

export const FeatureFlagForm: React.FC<Props> = ({ isOpen, onClose, onSuccess }) => {
  const [flagCode, setFlagCode] = useState('');
  const [flagType, setFlagType] = useState<'Boolean' | 'Variant' | 'Percentage'>('Boolean');
  const [systemSuiteId, setSystemSuiteId] = useState('');
  const [flagTargets, setFlagTargets] = useState('*');
  const [rolloutPercentage, setRolloutPercentage] = useState('');
  const [error, setError] = useState('');

  const createMutation = useCreateFeatureFlag();
  const { data: suitesPage, isLoading: loadingSuites } = useGetAllSystemSuites({
    page: 1,
    pageSize: 100,
  });

  const suiteOptions = (suitesPage?.items ?? []).map(s => ({
    value: s.systemSuiteId,
    label: `${s.name} (${s.code})`,
  }));

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    if (!flagCode.trim()) {
      setError('Código requerido');
      return;
    }
    if (!systemSuiteId.trim()) {
      setError('Suite requerida');
      return;
    }

    const payload: CreateFeatureFlagPayload = {
      flagCode: flagCode.trim(),
      flagType,
      systemSuiteId: systemSuiteId.trim(),
      flagTargets: flagTargets.trim() || '*',
      ...(rolloutPercentage ? { rolloutPercentage: parseInt(rolloutPercentage, 10) } : {}),
    };

    try {
      const result = await createMutation.mutateAsync(payload);
      onSuccess(result.featureFlagId);
      setFlagCode('');
      setFlagType('Boolean');
      setSystemSuiteId('');
      setFlagTargets('*');
      setRolloutPercentage('');
      onClose();
    } catch {}
  };

  return (
    <M3FormDialog
      open={isOpen}
      onClose={onClose}
      title="Nuevo Feature Flag"
      icon={<Flag className="w-4 h-4 text-m3-primary" />}
      maxWidth="max-w-md"
      footer={
        <div className="flex items-center gap-2">
          <FormButton
            type="button"
            variant="text"
            onClick={onClose}
            disabled={createMutation.isPending}
          >
            Cancelar
          </FormButton>
          <FormButton
            type="submit"
            form="feature-flag-form"
            variant="filled"
            loading={createMutation.isPending}
            icon={<Check className="w-3.5 h-3.5" />}
          >
            {createMutation.isPending ? 'Creando…' : 'Crear'}
          </FormButton>
        </div>
      }
    >
      <form id="feature-flag-form" onSubmit={handleSubmit} className="space-y-4">
        <p className="text-[11px] text-m3-secondary">
          El flag se crea en estado <span className="font-medium text-amber-500">Inactivo</span>.
          Active después de configurar targeting.
        </p>

        <FormField label="Código del Flag" required error={!flagCode && error ? error : undefined}>
          <FormInput
            value={flagCode}
            onChange={e => {
              setFlagCode(e.target.value);
              setError('');
            }}
            placeholder="dark-mode, new-checkout"
          />
        </FormField>

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <FormField label="Tipo">
            <FormSelect value={flagType} onChange={e => setFlagType(e.target.value as FlagType)}>
              {Object.entries(FLAG_TYPE_LABELS).map(([k, v]) => (
                <option key={k} value={k}>
                  {v}
                </option>
              ))}
            </FormSelect>
          </FormField>

          <FormField
            label={loadingSuites ? 'Cargando…' : 'Suite del Sistema'}
            required
            error={!systemSuiteId && error ? error : undefined}
          >
            <FormSelect
              value={systemSuiteId}
              onChange={e => {
                setSystemSuiteId(e.target.value);
                setError('');
              }}
              disabled={loadingSuites}
            >
              <option value="">— Seleccionar —</option>
              {suiteOptions.map(o => (
                <option key={o.value} value={o.value}>
                  {o.label}
                </option>
              ))}
            </FormSelect>
          </FormField>
        </div>

        <FormField label="Targeting Rules">
          <FormInput
            value={flagTargets}
            onChange={e => setFlagTargets(e.target.value)}
            placeholder="* (all), role:admin"
          />
        </FormField>

        <FormField label="Rollout % (0-100)">
          <FormInput
            type="number"
            value={rolloutPercentage}
            onChange={e => setRolloutPercentage(e.target.value)}
            placeholder="50"
            className="w-20"
            min={0}
            max={100}
          />
        </FormField>

        {error && <p className="text-[10px] text-rose-500">{error}</p>}
      </form>
    </M3FormDialog>
  );
};
