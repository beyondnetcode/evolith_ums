/**
 * TenantForm
 * Create new tenant - minimalist professional design
 */
import React, { useState } from 'react';
import { useCreateTenant } from '@app/identity/hooks/use-tenant';
import { useI18n } from '@app/i18n/use-i18n';
import { useFormValidation } from '@app/hooks';
import { M3FormDialog } from '@shared/components/M3FormDialog';
import { FormField, FormInput, FormSelect, FormButton } from '@shared/components/form';
import { TENANT_TYPES } from '@domain/identity/constants/tenant.constants';
import { CreateTenantPayloadSchema } from '@domain/identity/schemas/tenant.schema';
import { Building, Check } from 'lucide-react';

interface TenantFormProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: (tenantId: string) => void;
}

export const TenantForm: React.FC<TenantFormProps> = ({ isOpen, onClose, onSuccess }) => {
  const createTenantMutation = useCreateTenant();
  const t = useI18n();

  const [code, setCode] = useState('');
  const [name, setName] = useState('');
  const [type, setType] = useState('INTERNAL');
  const [companyReference, setCompanyReference] = useState('');

  const { errors, validate, clearErrors } = useFormValidation(CreateTenantPayloadSchema);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const payload = { code, name, type, companyReference: companyReference || undefined };
    const validData = validate(payload);
    if (!validData) return;
    try {
      const response = await createTenantMutation.mutateAsync(validData);
      setCode('');
      setName('');
      setType('INTERNAL');
      setCompanyReference('');
      clearErrors();
      onSuccess(response.tenantId);
      onClose();
    } catch {}
  };

  return (
    <M3FormDialog
      open={isOpen}
      onClose={onClose}
      title={t.createTenantTitle}
      icon={<Building className="w-4 h-4 text-m3-primary" />}
      footer={
        <div className="flex items-center gap-2">
          <FormButton variant="text" onClick={onClose} type="button">
            {t.cancelBtn}
          </FormButton>
          <FormButton
            variant="filled"
            onClick={handleSubmit}
            loading={createTenantMutation.isPending}
            icon={<Check className="w-3.5 h-3.5" />}
          >
            {t.registerTenantBtn}
          </FormButton>
        </div>
      }
    >
      <form onSubmit={handleSubmit} className="space-y-4">
        <FormField label={t.tenantCode} required error={errors.code}>
          <FormInput
            value={code}
            onChange={e => setCode(e.target.value.toUpperCase())}
            placeholder="TRANS_LIMA"
          />
        </FormField>

        <FormField label={t.tenantName} required error={errors.name}>
          <FormInput
            value={name}
            onChange={e => setName(e.target.value)}
            placeholder="Transportes Lima S.A.C."
          />
        </FormField>

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <FormField label={t.companyReference} error={errors.companyReference}>
            <FormInput
              value={companyReference}
              onChange={e => setCompanyReference(e.target.value)}
              placeholder="RUC-20512345678"
            />
          </FormField>

          <FormField label={t.tenantType} error={errors.type}>
            <FormSelect value={type} onChange={e => setType(e.target.value)}>
              {TENANT_TYPES.map(tp => (
                <option key={tp} value={tp}>
                  {tp}
                </option>
              ))}
            </FormSelect>
          </FormField>
        </div>
      </form>
    </M3FormDialog>
  );
};
