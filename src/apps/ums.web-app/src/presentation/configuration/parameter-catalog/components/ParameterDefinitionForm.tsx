/**
 * ParameterDefinitionForm
 * Create/Edit form for parameter definitions - minimalist professional design
 */
import React, { useState, useEffect } from 'react';
import { Tag, Check } from 'lucide-react';
import { useI18n } from '@app/i18n/use-i18n';
import { M3FormDialog } from '@shared/components/M3FormDialog';
import { FormField, FormInput, FormSelect, Toggle, FormButton } from '@shared/components/form';
import {
  CreateParameterDefinitionSchema,
  type ParameterDefinition,
} from '@domain/configuration/schemas/parameter-catalog/parameter-definition.schema';
import {
  DataTypeLabels,
  ScopeLabels,
} from '@domain/configuration/schemas/parameter-catalog/parameter-definition.schema';
import { z } from 'zod';

interface ParameterDefinitionFormProps {
  isOpen: boolean;
  onClose: () => void;
  onSubmit: (data: z.infer<typeof CreateParameterDefinitionSchema>) => Promise<void>;
  editingParameter?: ParameterDefinition;
  isLoading?: boolean;
}

export const ParameterDefinitionForm: React.FC<ParameterDefinitionFormProps> = ({
  isOpen,
  onClose,
  onSubmit,
  editingParameter,
  isLoading = false,
}) => {
  const t = useI18n();
  const isEditing = !!editingParameter;

  const [code, setCode] = useState('');
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [dataTypeId, setDataTypeId] = useState(1);
  const [defaultValue, setDefaultValue] = useState('');
  const [scopeId, setScopeId] = useState(1);
  const [isActive, setIsActive] = useState(true);
  const [isMandatory, setIsMandatory] = useState(false);
  const [displayOrder, setDisplayOrder] = useState(0);
  const [errors, setErrors] = useState<Record<string, string>>({});

  useEffect(() => {
    if (editingParameter) {
      setCode(editingParameter.code);
      setName(editingParameter.name);
      setDescription(editingParameter.description ?? '');
      setDataTypeId(editingParameter.dataTypeId);
      setDefaultValue(editingParameter.defaultValue);
      setScopeId(editingParameter.scopeId);
      setIsActive(editingParameter.isActive);
      setIsMandatory(editingParameter.isMandatory);
      setDisplayOrder(editingParameter.displayOrder);
    } else {
      setCode('');
      setName('');
      setDescription('');
      setDataTypeId(1);
      setDefaultValue('');
      setScopeId(1);
      setIsActive(true);
      setIsMandatory(false);
      setDisplayOrder(0);
    }
    setErrors({});
  }, [editingParameter, isOpen]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const payload = {
      code,
      name,
      description: description || null,
      dataTypeId,
      defaultValue,
      scopeId,
      isActive,
      isMandatory,
      displayOrder,
    };
    const result = CreateParameterDefinitionSchema.safeParse(payload);
    if (!result.success) {
      const fieldErrors: Record<string, string> = {};
      for (const err of result.error.errors) {
        fieldErrors[err.path.join('.')] = err.message;
      }
      setErrors(fieldErrors);
      return;
    }
    setErrors({});
    try {
      await onSubmit(result.data);
      onClose();
    } catch {}
  };

  return (
    <M3FormDialog
      open={isOpen}
      onClose={onClose}
      title={
        isEditing
          ? (t.editParameter ?? 'Edit Parameter')
          : (t.createParameter ?? 'Create Parameter')
      }
      icon={<Tag className="w-4 h-4 text-m3-primary" />}
      footer={
        <div className="flex items-center gap-2">
          <FormButton variant="text" onClick={onClose} type="button">
            {t.cancelBtn ?? 'Cancel'}
          </FormButton>
          <FormButton
            variant="filled"
            type="submit"
            form="parameter-form"
            loading={isLoading}
            icon={<Check className="w-3.5 h-3.5" />}
          >
            {isEditing ? (t.saveChanges ?? 'Save') : (t.createParameter ?? 'Create')}
          </FormButton>
        </div>
      }
    >
      <form id="parameter-form" onSubmit={handleSubmit} className="space-y-4">
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <FormField label={t.parameterCode ?? 'Code'} required error={errors.code}>
            <FormInput
              value={code}
              onChange={e => setCode(e.target.value.toUpperCase())}
              placeholder="SESSION_TIMEOUT"
              disabled={isEditing}
            />
          </FormField>

          <FormField label={t.parameterName ?? 'Name'} required error={errors.name}>
            <FormInput
              value={name}
              onChange={e => setName(e.target.value)}
              placeholder="Session Timeout"
            />
          </FormField>
        </div>

        <FormField label={t.description ?? 'Description'} error={errors.description}>
          <FormInput
            value={description}
            onChange={e => setDescription(e.target.value)}
            placeholder="Optional description..."
          />
        </FormField>

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <FormField label={t.dataType ?? 'Type'} error={errors.dataTypeId}>
            <FormSelect
              value={String(dataTypeId)}
              onChange={e => setDataTypeId(Number(e.target.value))}
            >
              {Object.entries(DataTypeLabels).map(([id, label]) => (
                <option key={id} value={id}>
                  {label}
                </option>
              ))}
            </FormSelect>
          </FormField>

          <FormField label={t.scope ?? 'Scope'} error={errors.scopeId}>
            <FormSelect value={String(scopeId)} onChange={e => setScopeId(Number(e.target.value))}>
              {Object.entries(ScopeLabels).map(([id, label]) => (
                <option key={id} value={id}>
                  {label}
                </option>
              ))}
            </FormSelect>
          </FormField>
        </div>

        <FormField label={t.defaultValue ?? 'Default Value'} required error={errors.defaultValue}>
          <FormInput
            value={defaultValue}
            onChange={e => setDefaultValue(e.target.value)}
            placeholder="Enter default value"
          />
        </FormField>

        <div className="flex items-end gap-6 pt-1">
          <FormField label={t.displayOrder ?? 'Order'} error={errors.displayOrder}>
            <FormInput
              type="number"
              value={String(displayOrder)}
              onChange={e => setDisplayOrder(Number(e.target.value) || 0)}
              className="w-16 text-center"
              min={0}
            />
          </FormField>

          <div className="flex items-center gap-4 pb-1">
            <Toggle checked={isActive} onChange={setIsActive} label={t.active ?? 'Active'} />
            <Toggle
              checked={isMandatory}
              onChange={setIsMandatory}
              label={t.mandatory ?? 'Required'}
            />
          </div>
        </div>
      </form>
    </M3FormDialog>
  );
};
