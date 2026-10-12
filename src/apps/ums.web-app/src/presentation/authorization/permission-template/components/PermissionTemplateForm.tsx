/**
 * PermissionTemplateForm — creates a new permission template (Draft).
 * Uses M3Dialog with shared form components.
 */
import React, { useState } from 'react';
import { ShieldPlus } from 'lucide-react';
import { M3Dialog, FieldSelect } from '@shared/components';
import { useCreatePermissionTemplate } from '@app/authorization/hooks/use-permission-template';
import { useGetAllSystemSuites } from '@app/authorization/hooks/use-system-suite';
import { useRolesBySystemSuite } from '@app/authorization/hooks/use-role';
import { useEffectiveTenant } from '@app/shared/hooks/use-effective-tenant';

interface Props {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: (templateId: string) => void;
  tenantId?: string;
}

export const PermissionTemplateForm: React.FC<Props> = ({
  isOpen,
  onClose,
  onSuccess,
  tenantId,
}) => {
  const effectiveTenantId = useEffectiveTenant(tenantId);

  const [systemSuiteIdVal, setSystemSuiteIdVal] = useState('');
  const [roleIdVal, setRoleIdVal] = useState('');
  const [error, setError] = useState('');

  const createMutation = useCreatePermissionTemplate();

  const { data: suitesPage, isLoading: loadingSuites } = useGetAllSystemSuites({
    page: 1,
    pageSize: 100,
    tenantId: effectiveTenantId,
  });

  const { data: roles = [], isLoading: loadingRoles } = useRolesBySystemSuite(systemSuiteIdVal);

  const suiteOptions = (suitesPage?.items ?? []).map(s => ({
    value: s.systemSuiteId,
    label: s.name,
  }));

  const roleOptions = roles.map(r => ({
    value: r.roleId,
    label: r.value,
  }));

  const handleSuiteChange = (id: string) => {
    setSystemSuiteIdVal(id);
    setRoleIdVal('');
    setError('');
  };

  const handleSubmit = async () => {
    setError('');

    if (!effectiveTenantId) {
      setError('Tenant context no disponible');
      return;
    }
    if (!systemSuiteIdVal.trim()) {
      setError('Suite del Sistema es requerida');
      return;
    }
    if (!roleIdVal.trim()) {
      setError('Rol es requerido');
      return;
    }

    try {
      const result = await createMutation.mutateAsync({
        tenantId: effectiveTenantId,
        systemSuiteId: systemSuiteIdVal.trim(),
        roleId: roleIdVal.trim(),
      });
      onSuccess(result.templateId);
    } catch {
      /* handled by hook */
    }
  };

  return (
    <M3Dialog
      open={isOpen}
      onScrimClick={onClose}
      title="Nueva Plantilla de Permisos"
      actions={[
        {
          label: 'Cancelar',
          variant: 'outlined',
          onClick: onClose,
          disabled: createMutation.isPending,
        },
        {
          label: createMutation.isPending ? 'Creando…' : 'Crear Plantilla',
          variant: 'filled',
          onClick: handleSubmit,
          loading: createMutation.isPending,
        },
      ]}
    >
      <form
        onSubmit={e => {
          e.preventDefault();
          handleSubmit();
        }}
        className="space-y-4"
      >
        <p className="text-[11px] text-m3-secondary">
          La plantilla se crea en estado <span className="font-bold text-amber-500">Borrador</span>.
          Agrega los ítems de permiso antes de publicarla.
        </p>

        <FieldSelect
          label={loadingSuites ? 'Cargando suites…' : 'Suite del Sistema'}
          value={systemSuiteIdVal}
          onChange={handleSuiteChange}
          options={suiteOptions}
          placeholder="— Seleccionar —"
          disabled={loadingSuites}
          required
        />

        <FieldSelect
          label={
            !systemSuiteIdVal
              ? 'Rol (selecciona una Suite primero)'
              : loadingRoles
                ? 'Cargando roles…'
                : 'Rol'
          }
          value={roleIdVal}
          onChange={v => {
            setRoleIdVal(v);
            setError('');
          }}
          options={roleOptions}
          placeholder="— Seleccionar —"
          disabled={!systemSuiteIdVal || loadingRoles}
          required
        />

        {error && <p className="text-[11px] text-m3-error">{error}</p>}
      </form>
    </M3Dialog>
  );
};
