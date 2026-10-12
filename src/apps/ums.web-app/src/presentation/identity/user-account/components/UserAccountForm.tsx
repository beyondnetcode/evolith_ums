/**
 * UserAccountForm
 * Create new user account - minimalist professional design
 */
import React, { useState } from 'react';
import { useCreateUserAccount } from '@app/identity/hooks/use-user-account';
import { useI18n } from '@app/i18n/use-i18n';
import { useFormValidation } from '@app/hooks';
import { useAuthStore } from '@app/stores/auth.store';
import { M3FormDialog } from '@shared/components/M3FormDialog';
import { FormField, FormInput, FormSelect, FormButton } from '@shared/components/form';
import {
  USER_CATEGORIES,
  IDENTITY_REFERENCE_TYPES,
} from '@domain/identity/constants/user-account.constants';
import { CreateUserAccountPayloadSchema } from '@domain/identity/schemas/user-account.schema';
import { UserPlus, Check } from 'lucide-react';

interface UserAccountFormProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: () => void;
  tenantId?: string;
}

export const UserAccountForm: React.FC<UserAccountFormProps> = ({
  isOpen,
  onClose,
  onSuccess,
  tenantId,
}) => {
  const createUserAccountMutation = useCreateUserAccount();
  const t = useI18n();

  const sessionTenantId = useAuthStore(state => state.user?.tenantId);
  const effectiveTenantId = tenantId || sessionTenantId;

  const [email, setEmail] = useState('');
  const [category, setCategory] = useState('Internal');
  const [identityReference, setIdentityReference] = useState('');
  const [identityReferenceType, setIdentityReferenceType] = useState('HrId');

  const { errors, validate, clearErrors } = useFormValidation(CreateUserAccountPayloadSchema);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!effectiveTenantId) return;

    const payload = {
      tenantId: effectiveTenantId,
      branchId: null,
      email,
      category: category as 'Internal' | 'External' | 'B2B' | 'Partner' | 'ServiceAccount',
      identityReference: identityReference || undefined,
      identityReferenceType: identityReference
        ? (identityReferenceType as 'HrId' | 'VendorCode' | 'GovernmentId' | 'PartnerRef')
        : undefined,
    };

    const validData = validate(payload);
    if (!validData) return;

    try {
      await createUserAccountMutation.mutateAsync(validData);
      setEmail('');
      setCategory('Internal');
      setIdentityReference('');
      setIdentityReferenceType('HrId');
      clearErrors();
      onSuccess();
      onClose();
    } catch {}
  };

  return (
    <M3FormDialog
      open={isOpen}
      onClose={onClose}
      title={t.createUserAccountTitle}
      icon={<UserPlus className="w-4 h-4 text-m3-primary" />}
      footer={
        <div className="flex items-center gap-2">
          <FormButton variant="text" onClick={onClose} type="button">
            {t.cancelBtn}
          </FormButton>
          <FormButton
            variant="filled"
            onClick={handleSubmit}
            loading={createUserAccountMutation.isPending}
            icon={<Check className="w-3.5 h-3.5" />}
          >
            {t.createUserBtn}
          </FormButton>
        </div>
      }
    >
      <form onSubmit={handleSubmit} className="space-y-4">
        <FormField label={t.userEmail} required error={errors.email}>
          <FormInput
            type="email"
            value={email}
            onChange={e => setEmail(e.target.value.toLowerCase())}
            placeholder="user@company.com"
          />
        </FormField>

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <FormField label={t.userCategory} error={errors.category}>
            <FormSelect value={category} onChange={e => setCategory(e.target.value)}>
              {USER_CATEGORIES.map(c => (
                <option key={c} value={c}>
                  {c}
                </option>
              ))}
            </FormSelect>
          </FormField>

          <FormField label={t.identityReferenceType} error={errors.identityReferenceType}>
            <FormSelect
              value={identityReferenceType}
              onChange={e => setIdentityReferenceType(e.target.value)}
            >
              {IDENTITY_REFERENCE_TYPES.map(type => (
                <option key={type} value={type}>
                  {type}
                </option>
              ))}
            </FormSelect>
          </FormField>
        </div>

        <FormField label={t.identityReference} error={errors.identityReference}>
          <FormInput
            value={identityReference}
            onChange={e => setIdentityReference(e.target.value)}
            placeholder="EMP-12345"
          />
        </FormField>
      </form>
    </M3FormDialog>
  );
};
