/**
 * AppConfigurationForm
 * Create new AppConfiguration by selecting from ParameterDefinition catalog
 */
import React, { useState } from 'react';
import { useI18n } from '@app/i18n/use-i18n';
import { M3FormDialog } from '@shared/components/M3FormDialog';
import { FormField, FormInput, Toggle, FormButton } from '@shared/components/form';
import { useCreateAppConfiguration } from '@app/configuration/hooks/use-app-configuration';
import { useNotificationStore } from '@app/stores/notification.store';

interface AppConfigurationFormProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: (configId: string) => void;
}

export function AppConfigurationForm({
  isOpen,
  onClose,
  onSuccess,
}: AppConfigurationFormProps): React.JSX.Element {
  const t = useI18n();
  const addNotification = useNotificationStore(s => s.addNotification);
  const createMutation = useCreateAppConfiguration();

  const [code, setCode] = useState('');
  const [value, setValue] = useState('');
  const [description, setDescription] = useState('');
  const [isInheritable, setIsInheritable] = useState(false);
  const [isEncrypted, setIsEncrypted] = useState(false);
  const [error, setError] = useState('');

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');

    if (!code.trim()) {
      setError(t.codeRequired ?? 'Code is required');
      return;
    }

    if (!value.trim()) {
      setError(t.valueRequired ?? 'Value is required');
      return;
    }

    try {
      const result = await createMutation.mutateAsync({
        code: code.trim().toUpperCase(),
        value: value.trim(),
        description: description.trim(),
        isInheritable,
        isEncrypted,
      });

      addNotification({
        title: t.parameterCreated ?? 'Parameter Created',
        message: `${code} has been created`,
        type: 'success',
      });

      setCode('');
      setValue('');
      setDescription('');
      setIsInheritable(false);
      setIsEncrypted(false);
      onSuccess(result.appConfigurationId);
    } catch {
      setError(t.createConfigFailed ?? 'Failed to create parameter');
    }
  };

  const handleClose = () => {
    setCode('');
    setValue('');
    setDescription('');
    setIsInheritable(false);
    setIsEncrypted(false);
    setError('');
    onClose();
  };

  return (
    <M3FormDialog
      open={isOpen}
      onClose={handleClose}
      title={t.addParameter ?? 'Add Configuration'}
      icon={<span className="w-4 h-4 text-m3-primary">+</span>}
      footer={
        <div className="flex items-center gap-2">
          <FormButton variant="text" onClick={handleClose}>
            {t.cancelBtn ?? 'Cancel'}
          </FormButton>
          <FormButton
            variant="filled"
            type="submit"
            form="config-form"
            loading={createMutation.isPending}
          >
            {t.saveParameter ?? 'Save'}
          </FormButton>
        </div>
      }
    >
      <form id="config-form" onSubmit={handleSubmit} className="space-y-4">
        <FormField
          label={t.parameterCode ?? 'Code'}
          required
          error={error && !code ? error : undefined}
        >
          <FormInput
            value={code}
            onChange={e => setCode(e.target.value.toUpperCase())}
            placeholder="MAX_VALIDITY_DAYS"
          />
        </FormField>

        <FormField
          label={t.parameterValue ?? 'Value'}
          required
          error={error && !value ? error : undefined}
        >
          <FormInput value={value} onChange={e => setValue(e.target.value)} placeholder="365" />
        </FormField>

        <FormField label={t.description ?? 'Description'}>
          <FormInput
            value={description}
            onChange={e => setDescription(e.target.value)}
            placeholder="Optional description..."
          />
        </FormField>

        <div className="flex items-center gap-6 pt-1">
          <Toggle
            checked={isInheritable}
            onChange={setIsInheritable}
            label={t.inheritable ?? 'Inheritable'}
          />
          <Toggle
            checked={isEncrypted}
            onChange={setIsEncrypted}
            label={t.encrypted ?? 'Encrypted'}
          />
        </div>

        {error && (
          <div className="text-[11px] text-rose-500 bg-rose-50/50 p-2 rounded-md">{error}</div>
        )}
      </form>
    </M3FormDialog>
  );
}
