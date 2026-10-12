import { describe, expect, it } from 'vitest';
import { authorizationTranslations } from './authorization.translations';

describe('authorizationTranslations', () => {
  describe('spanish', () => {
    it('has authorizationContext', () => {
      expect(authorizationTranslations.es.authorizationContext).toBe('Autorización');
    });

    it('has systemSuites and permissionTemplates', () => {
      expect(authorizationTranslations.es.systemSuites).toBe('Suites del Sistema');
      expect(authorizationTranslations.es.permissionTemplates).toBe('Plantillas de Permisos');
    });

    it('has template status labels', () => {
      expect(authorizationTranslations.es.templateDraft).toBe('Borrador');
      expect(authorizationTranslations.es.templatePublished).toBe('Publicada');
      expect(authorizationTranslations.es.templateDeprecated).toBe('Descontinuada');
    });

    it('has effect labels', () => {
      expect(authorizationTranslations.es.effectAllow).toBe('Permitir');
      expect(authorizationTranslations.es.effectDeny).toBe('Denegar');
      expect(authorizationTranslations.es.effectNeutral).toBe('Neutro');
    });

    it('has system suite form labels', () => {
      expect(authorizationTranslations.es.createSystemSuiteTitle).toBe(
        'Crear Nuevo Suite del Sistema'
      );
      expect(authorizationTranslations.es.systemSuiteCode).toBe('Código del Suite');
      expect(authorizationTranslations.es.systemSuiteName).toBe('Nombre del Suite');
      expect(authorizationTranslations.es.registerSystemSuiteBtn).toBe('Registrar Suite');
    });

    it('has tab labels', () => {
      expect(authorizationTranslations.es.modules).toBe('Módulos');
      expect(authorizationTranslations.es.domainResources).toBe('Recursos de Dominio');
      expect(authorizationTranslations.es.systemActions).toBe('Acciones de Sistema');
      expect(authorizationTranslations.es.actions).toBe('Acciones');
      expect(authorizationTranslations.es.roles).toBe('Roles');
    });

    it('has permission tree labels', () => {
      expect(authorizationTranslations.es.inheritedPermission).toBe('Heredado');
      expect(authorizationTranslations.es.directPermission).toBe('Directo');
      expect(authorizationTranslations.es.partialPermission).toBe('Parcial');
      expect(authorizationTranslations.es.noPermission).toBe('Sin configurar');
    });

    it('has CRUD labels', () => {
      expect(authorizationTranslations.es.crudCreate).toBe('Crear');
      expect(authorizationTranslations.es.crudRead).toBe('Leer/Obtener');
      expect(authorizationTranslations.es.crudSearch).toBe('Buscar/Listar');
      expect(authorizationTranslations.es.crudUpdate).toBe('Actualizar');
      expect(authorizationTranslations.es.crudDelete).toBe('Eliminar/Desactivar');
    });

    it('has domain resource labels', () => {
      expect(authorizationTranslations.es.aggregate).toBe('Agregado');
      expect(authorizationTranslations.es.entity).toBe('Entidad');
    });

    it('has role labels', () => {
      expect(authorizationTranslations.es.addRole).toBe('Agregar rol');
      expect(authorizationTranslations.es.roleCode).toBe('Código');
      expect(authorizationTranslations.es.roleValue).toBe('Nombre visible');
      expect(authorizationTranslations.es.parentRole).toBe('Rol padre');
    });

    it('has status control labels', () => {
      expect(authorizationTranslations.es.maintenanceBtn).toBe('Mantenimiento');
      expect(authorizationTranslations.es.deprecateBtn).toBe('Descontinuar');
      expect(authorizationTranslations.es.maintenance).toBe('Mantenimiento');
      expect(authorizationTranslations.es.deprecated).toBe('Descontinuado');
    });

    it('has notification labels', () => {
      expect(authorizationTranslations.es.notifSystemSuiteCreated).toBe('Suite del Sistema Creado');
      expect(authorizationTranslations.es.notifSystemSuiteUpdated).toBe(
        'Suite del Sistema Actualizado'
      );
      expect(authorizationTranslations.es.notifStatusChanged).toBe('Estado Cambiado');
    });

    it('has notifSystemSuiteCreatedMsg function', () => {
      const msg = authorizationTranslations.es.notifSystemSuiteCreatedMsg('abc-123');
      expect(msg).toContain('abc-123');
    });

    it('has notifSystemSuiteUpdatedMsg function', () => {
      const msg = authorizationTranslations.es.notifSystemSuiteUpdatedMsg('Test Suite');
      expect(msg).toContain('Test Suite');
    });

    it('has notifStatusSetTo function', () => {
      const msg = authorizationTranslations.es.notifStatusSetTo('Maintenance');
      expect(msg).toContain('Maintenance');
    });

    it('has role notification labels', () => {
      expect(authorizationTranslations.es.notifRoleCreated).toBe('Rol registrado');
      expect(authorizationTranslations.es.notifRoleUpdated).toBe('Rol actualizado');
      expect(authorizationTranslations.es.notifRoleStatusChanged).toBe(
        'Estado del rol actualizado'
      );
    });

    it('has feature flag labels', () => {
      expect(authorizationTranslations.es.featureFlags).toBe('Feature Flags');
      expect(authorizationTranslations.es.flagCode).toBe('Código del Flag');
      expect(authorizationTranslations.es.flagType).toBe('Tipo de Flag');
      expect(authorizationTranslations.es.flagStatus).toBe('Estado');
    });

    it('has flag status labels', () => {
      expect(authorizationTranslations.es.flagInactive).toBe('Inactivo');
      expect(authorizationTranslations.es.flagActive).toBe('Activo');
      expect(authorizationTranslations.es.flagArchived).toBe('Archivado');
    });

    it('has criteria labels', () => {
      expect(authorizationTranslations.es.criteriaType).toBe('Tipo de Criterio');
      expect(authorizationTranslations.es.criteriaOperator).toBe('Operador');
      expect(authorizationTranslations.es.criteriaValue).toBe('Valor');
      expect(authorizationTranslations.es.addCriteria).toBe('Agregar Criterio');
    });
  });

  describe('english', () => {
    it('has authorizationContext', () => {
      expect(authorizationTranslations.en.authorizationContext).toBe('Authorization');
    });

    it('has systemSuites and permissionTemplates', () => {
      expect(authorizationTranslations.en.systemSuites).toBe('System Suites');
      expect(authorizationTranslations.en.permissionTemplates).toBe('Permission Templates');
    });

    it('has template status labels', () => {
      expect(authorizationTranslations.en.templateDraft).toBe('Draft');
      expect(authorizationTranslations.en.templatePublished).toBe('Published');
      expect(authorizationTranslations.en.templateDeprecated).toBe('Deprecated');
    });

    it('has effect labels', () => {
      expect(authorizationTranslations.en.effectAllow).toBe('Allow');
      expect(authorizationTranslations.en.effectDeny).toBe('Deny');
      expect(authorizationTranslations.en.effectNeutral).toBe('Neutral');
    });

    it('has system suite form labels', () => {
      expect(authorizationTranslations.en.createSystemSuiteTitle).toBe('Create New System Suite');
      expect(authorizationTranslations.en.systemSuiteCode).toBe('System Suite Code');
      expect(authorizationTranslations.en.systemSuiteName).toBe('System Suite Name');
      expect(authorizationTranslations.en.registerSystemSuiteBtn).toBe('Register System Suite');
    });

    it('has tab labels', () => {
      expect(authorizationTranslations.en.modules).toBe('Modules');
      expect(authorizationTranslations.en.domainResources).toBe('Domain Resources');
      expect(authorizationTranslations.en.systemActions).toBe('System Actions');
      expect(authorizationTranslations.en.actions).toBe('Actions');
      expect(authorizationTranslations.en.roles).toBe('Roles');
    });

    it('has permission tree labels', () => {
      expect(authorizationTranslations.en.inheritedPermission).toBe('Inherited');
      expect(authorizationTranslations.en.directPermission).toBe('Direct');
      expect(authorizationTranslations.en.partialPermission).toBe('Partial');
      expect(authorizationTranslations.en.noPermission).toBe('Not configured');
    });

    it('has CRUD labels', () => {
      expect(authorizationTranslations.en.crudCreate).toBe('Create');
      expect(authorizationTranslations.en.crudRead).toBe('Read/Get');
      expect(authorizationTranslations.en.crudSearch).toBe('Search/List');
      expect(authorizationTranslations.en.crudUpdate).toBe('Update');
      expect(authorizationTranslations.en.crudDelete).toBe('Delete/Deactivate');
    });

    it('has domain resource labels', () => {
      expect(authorizationTranslations.en.aggregate).toBe('Aggregate');
      expect(authorizationTranslations.en.entity).toBe('Entity');
    });

    it('has role labels', () => {
      expect(authorizationTranslations.en.addRole).toBe('Add role');
      expect(authorizationTranslations.en.roleCode).toBe('Code');
      expect(authorizationTranslations.en.roleValue).toBe('Display name');
      expect(authorizationTranslations.en.parentRole).toBe('Parent role');
    });

    it('has status control labels', () => {
      expect(authorizationTranslations.en.maintenanceBtn).toBe('Maintenance');
      expect(authorizationTranslations.en.deprecateBtn).toBe('Deprecate');
      expect(authorizationTranslations.en.maintenance).toBe('Maintenance');
      expect(authorizationTranslations.en.deprecated).toBe('Deprecated');
    });

    it('has notification labels', () => {
      expect(authorizationTranslations.en.notifSystemSuiteCreated).toBe('System Suite Created');
      expect(authorizationTranslations.en.notifSystemSuiteUpdated).toBe('System Suite Updated');
      expect(authorizationTranslations.en.notifStatusChanged).toBe('Status Changed');
    });

    it('has notifSystemSuiteCreatedMsg function', () => {
      const msg = authorizationTranslations.en.notifSystemSuiteCreatedMsg('abc-123');
      expect(msg).toContain('abc-123');
    });

    it('has notifSystemSuiteUpdatedMsg function', () => {
      const msg = authorizationTranslations.en.notifSystemSuiteUpdatedMsg('Test Suite');
      expect(msg).toContain('Test Suite');
    });

    it('has notifStatusSetTo function', () => {
      const msg = authorizationTranslations.en.notifStatusSetTo('Maintenance');
      expect(msg).toContain('Maintenance');
    });

    it('has role notification labels', () => {
      expect(authorizationTranslations.en.notifRoleCreated).toBe('Role registered');
      expect(authorizationTranslations.en.notifRoleUpdated).toBe('Role updated');
      expect(authorizationTranslations.en.notifRoleStatusChanged).toBe('Role status updated');
    });

    it('has feature flag labels', () => {
      expect(authorizationTranslations.en.featureFlags).toBe('Feature Flags');
      expect(authorizationTranslations.en.flagCode).toBe('Flag Code');
      expect(authorizationTranslations.en.flagType).toBe('Flag Type');
      expect(authorizationTranslations.en.flagStatus).toBe('Status');
    });

    it('has flag status labels', () => {
      expect(authorizationTranslations.en.flagInactive).toBe('Inactive');
      expect(authorizationTranslations.en.flagActive).toBe('Active');
      expect(authorizationTranslations.en.flagArchived).toBe('Archived');
    });

    it('has criteria labels', () => {
      expect(authorizationTranslations.en.criteriaType).toBe('Criteria Type');
      expect(authorizationTranslations.en.criteriaOperator).toBe('Operator');
      expect(authorizationTranslations.en.criteriaValue).toBe('Value');
      expect(authorizationTranslations.en.addCriteria).toBe('Add Criteria');
    });
  });
});
