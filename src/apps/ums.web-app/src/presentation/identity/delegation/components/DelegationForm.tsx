import React, { useState } from 'react';
import { useCreateDelegation } from '@app/identity/hooks/use-delegation';
import { useI18n } from '@app/i18n/use-i18n';
import { M3Dialog } from '@shared/components/M3Dialog';
import { FormField, FormInput, FormSelect, FormButton, FieldSelect } from '@shared/components';
import { CreateDelegationPayloadSchema } from '@domain/identity/schemas/delegation.schema';
import { Shield } from 'lucide-react';

const SCOPE_TYPES = ['Tenant', 'Organization', 'Department', 'System', 'Team'] as const;
const DELEGATED_ACTIONS = [
  'CreateUser',
  'BlockUser',
  'AssignProfile',
  'ResetPassword',
  'RevokeMfa',
] as const;

const EVOLITH_USERS: Record<string, string> = {
  '5f4e3d01-1b0a-9f8e-7d6c-543210987654': 'Gerente EVOLITH',
  '5f4e3d02-1b0a-9f8e-7d6c-543210987654': 'Analista EVOLITH',
  '5f4e3d05-1b0a-9f8e-7d6c-543210987654': 'Socio EVOLITH',
};

interface DelegationFormProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: () => void;
  defaultTenantId?: string;
  defaultDelegatingAdminId?: string;
}

export const DelegationForm: React.FC<DelegationFormProps> = ({
  isOpen,
  onClose,
  onSuccess,
  defaultTenantId = '',
  defaultDelegatingAdminId = '',
}) => {
  const createDelegationMutation = useCreateDelegation();
  const t = useI18n();

  const [tenantId] = useState(defaultTenantId);
  const [delegatingAdminId] = useState(defaultDelegatingAdminId);
  const [delegatedAdminId, setDelegatedAdminId] = useState('5f4e3d02-1b0a-9f8e-7d6c-543210987654');
  const [scopeType, setScopeType] = useState<(typeof SCOPE_TYPES)[number]>('Tenant');
  const [validFrom, setValidFrom] = useState('');
  const [validUntil, setValidUntil] = useState('');
  const [requiresApproval, setRequiresApproval] = useState(false);
  const [selectedActions, setSelectedActions] = useState<string[]>(['CreateUser']);
  const [errors, setErrors] = useState<Record<string, string>>({});

  const toggleAction = (action: string) => {
    setSelectedActions(prev =>
      prev.includes(action) ? prev.filter(a => a !== action) : [...prev, action]
    );
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrors({});

    const payload = {
      tenantId,
      delegatingAdminId,
      delegatedAdminId,
      scopeType: scopeType as (typeof SCOPE_TYPES)[number],
      allowedActions: selectedActions as Array<
        'CreateUser' | 'BlockUser' | 'AssignProfile' | 'ResetPassword' | 'RevokeMfa'
      >,
      validFrom,
      validUntil,
      requiresApproval,
    };

    const result = CreateDelegationPayloadSchema.safeParse(payload);
    if (!result.success) {
      const fieldErrors: Record<string, string> = {};
      const flattened = result.error.flatten();
      Object.entries(flattened.fieldErrors).forEach(([key, msgs]) => {
        if (msgs?.[0]) fieldErrors[key] = msgs[0];
      });
      setErrors(fieldErrors);
      return;
    }

    try {
      await createDelegationMutation.mutateAsync(payload);
      setDelegatedAdminId('');
      setScopeType('Tenant');
      setValidFrom('');
      setValidUntil('');
      setRequiresApproval(false);
      setSelectedActions(['CreateUser']);
      onSuccess();
    } catch {
      // Handled by mutation hook
    }
  };

  const userOptions = Object.entries(EVOLITH_USERS).map(([id, name]) => ({
    value: id,
    label: name,
  }));
  const scopeOptions = SCOPE_TYPES.map(s => ({ value: s, label: s }));

  return (
    <M3Dialog
      open={isOpen}
      onScrimClick={onClose}
      title={t.createDelegation ?? 'Crear Delegación'}
      actions={[
        { label: t.cancelBtn ?? 'Cancelar', variant: 'outlined', onClick: onClose },
        {
          label: t.createBtn ?? 'Crear',
          variant: 'filled',
          onClick: handleSubmit,
          loading: createDelegationMutation.isPending,
        },
      ]}
    >
      <form onSubmit={handleSubmit} className="flex flex-col gap-4 pt-2">
        <FieldSelect
          label={t.delegatedAdminId ?? 'Delegar a (Usuario)'}
          options={userOptions}
          placeholder="Seleccione un usuario..."
          value={delegatedAdminId}
          onChange={setDelegatedAdminId}
        />

        <FieldSelect
          label={t.scopeType ?? 'Alcance (Scope)'}
          options={scopeOptions}
          value={scopeType}
          onChange={v => setScopeType(v as typeof scopeType)}
        />

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <FormField label={t.validFrom ?? 'Válido desde'} required error={errors.validFrom}>
            <FormInput
              type="datetime-local"
              value={validFrom}
              onChange={e => setValidFrom(e.target.value)}
            />
          </FormField>
          <FormField label={t.validUntil ?? 'Válido hasta'} required error={errors.validUntil}>
            <FormInput
              type="datetime-local"
              value={validUntil}
              onChange={e => setValidUntil(e.target.value)}
            />
          </FormField>
        </div>

        <div>
          <p className="text-[10px] font-medium text-m3-on-surface-variant uppercase tracking-wide mb-2">
            {t.allowedActions ?? 'Acciones Permitidas'}
          </p>
          <div className="flex flex-wrap gap-1.5">
            {DELEGATED_ACTIONS.map(action => (
              <button
                key={action}
                type="button"
                onClick={() => toggleAction(action)}
                className={`px-2.5 py-1 text-[10px] rounded-full border transition-colors font-medium ${
                  selectedActions.includes(action)
                    ? 'bg-m3-primary/10 text-m3-primary border-m3-primary/30'
                    : 'bg-transparent text-m3-secondary border-m3-outline/30 hover:bg-m3-surface-variant'
                }`}
              >
                {action}
              </button>
            ))}
          </div>
          {errors.allowedActions && (
            <p className="text-[10px] text-rose-500 mt-1">{errors.allowedActions}</p>
          )}
        </div>

        <div className="flex items-center gap-2 p-2.5 bg-m3-surface-container/30 rounded-lg border border-m3-outline/10">
          <input
            id="requiresApproval"
            type="checkbox"
            checked={requiresApproval}
            onChange={e => setRequiresApproval(e.target.checked)}
            className="w-3.5 h-3.5 text-m3-primary border-m3-outline/30 rounded focus:ring-m3-primary/40"
          />
          <label
            htmlFor="requiresApproval"
            className="text-[11px] font-medium text-m3-on-surface cursor-pointer select-none"
          >
            {t.requiresApproval ?? 'Requiere Aprobación'}
          </label>
        </div>
      </form>
    </M3Dialog>
  );
};
