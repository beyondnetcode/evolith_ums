/**
 * SystemSuiteForm
 * Create new system suite - minimalist professional design
 */
import React, { useState } from 'react';
import { useCreateSystemSuite } from '@app/authorization/hooks/use-system-suite';
import { useI18n } from '@app/i18n/use-i18n';
import { useFormValidation } from '@app/hooks';
import { useEffectiveTenant } from '@app/shared/hooks/use-effective-tenant';
import { M3FormDialog } from '@shared/components/M3FormDialog';
import { FormField, FormInput, FormButton } from '@shared/components/form';
import { CreateSystemSuitePayloadSchema } from '@domain/authorization/schemas/system-suite.schema';
import { Box, Check } from 'lucide-react';

interface SystemSuiteFormProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: () => void;
  tenantId?: string;
}

export const SystemSuiteForm: React.FC<SystemSuiteFormProps> = ({
  isOpen,
  onClose,
  onSuccess,
  tenantId,
}) => {
  const createSystemSuiteMutation = useCreateSystemSuite();
  const t = useI18n();

  const effectiveTenantId = useEffectiveTenant(tenantId);

  const [code, setCode] = useState('');
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');

  const { errors, validate, clearErrors } = useFormValidation(CreateSystemSuitePayloadSchema);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!effectiveTenantId) return;

    const payload = {
      tenantId: effectiveTenantId,
      code,
      name,
      // Enviamos la cadena (recortada) — no `undefined` — para que la validación
      // requerida de descripción se dispare antes del POST y no en un 400 (G-107).
      description: description.trim(),
    };
    const validData = validate(payload);
    if (!validData) return;

    try {
      await createSystemSuiteMutation.mutateAsync(validData);
      setCode('');
      setName('');
      setDescription('');
      clearErrors();
      onSuccess();
      onClose();
    } catch {}
  };

  return (
    <M3FormDialog
      open={isOpen}
      onClose={onClose}
      title={t.createSystemSuiteTitle}
      icon={<Box className="w-4 h-4 text-m3-primary" />}
      footer={
        <div className="flex items-center gap-2">
          <FormButton variant="text" onClick={onClose} type="button">
            {t.cancelBtn}
          </FormButton>
          <FormButton
            variant="filled"
            onClick={handleSubmit}
            loading={createSystemSuiteMutation.isPending}
            icon={<Check className="w-3.5 h-3.5" />}
          >
            {t.registerSystemSuiteBtn}
          </FormButton>
        </div>
      }
    >
      <form onSubmit={handleSubmit} className="space-y-4">
        <FormField label={t.systemSuiteCode} required error={errors.code}>
          <FormInput
            value={code}
            onChange={e => setCode(e.target.value.toUpperCase())}
            placeholder="SUITE_CRM"
          />
        </FormField>

        <FormField label={t.systemSuiteName} required error={errors.name}>
          <FormInput
            value={name}
            onChange={e => setName(e.target.value)}
            placeholder="CRM System Suite"
          />
        </FormField>

        <FormField label={t.description} required error={errors.description}>
          <FormInput
            value={description}
            onChange={e => setDescription(e.target.value)}
            error={!!errors.description}
            placeholder="Customer relationship management module"
          />
        </FormField>
      </form>
    </M3FormDialog>
  );
};
