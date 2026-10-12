import { describe, expect, it } from 'vitest';
import { identityTranslations } from './identity.translations';

describe('identityTranslations', () => {
  describe('spanish', () => {
    it('has identityContext', () => {
      expect(identityTranslations.es.identityContext).toBe('Identidad');
    });

    it('has tenant and tenants', () => {
      expect(identityTranslations.es.tenant).toBe('Tenant');
      expect(identityTranslations.es.tenants).toBe('Tenants');
    });

    it('has userAccounts', () => {
      expect(identityTranslations.es.userAccounts).toBe('Cuentas de Usuario');
    });

    it('has tenantMaintenance', () => {
      expect(identityTranslations.es.tenantMaintenance).toBe('Mantenimiento de Tenants');
    });

    it('has createTenantTitle', () => {
      expect(identityTranslations.es.createTenantTitle).toBe('Crear Nuevo Tenant');
    });

    it('has tenantCode and tenantName', () => {
      expect(identityTranslations.es.tenantCode).toBe('Código del Tenant');
      expect(identityTranslations.es.tenantName).toBe('Nombre del Tenant');
    });

    it('has cancelBtn and registerTenantBtn', () => {
      expect(identityTranslations.es.cancelBtn).toBe('Cancelar');
      expect(identityTranslations.es.registerTenantBtn).toBe('Registrar Tenant');
    });

    it('has colTenantName, colCode, colStatus, colAction', () => {
      expect(identityTranslations.es.colTenantName).toBe('Nombre del Tenant');
      expect(identityTranslations.es.colCode).toBe('Código');
      expect(identityTranslations.es.colStatus).toBe('Estado');
      expect(identityTranslations.es.colAction).toBe('Acción');
    });

    it('has pending', () => {
      expect(identityTranslations.es.pending).toBe('Pendiente');
    });

    it('has suspendBtn and activateBtn', () => {
      expect(identityTranslations.es.suspendBtn).toBe('Suspender');
      expect(identityTranslations.es.activateBtn).toBe('Activar');
    });

    it('has tab labels', () => {
      expect(identityTranslations.es.tabLocations).toBe('Ubicaciones');
      expect(identityTranslations.es.tabAuthIdps).toBe('Prov. Identidad');
    });

    it('has branch labels', () => {
      expect(identityTranslations.es.branchCode).toBe('Código de Sucursal');
      expect(identityTranslations.es.branchName).toBe('Nombre de la Sucursal');
      expect(identityTranslations.es.addLocation).toBe('Agregar Ubicación');
    });

    it('has identity provider labels', () => {
      expect(identityTranslations.es.identityProviders).toBe('Proveedores de Identidad');
      expect(identityTranslations.es.addProvider).toBe('Agregar Proveedor');
      expect(identityTranslations.es.saveProvider).toBe('Guardar Proveedor');
    });

    it('has IdP strategy labels', () => {
      expect(identityTranslations.es.strategyOIDC).toBe('OpenID Connect (OIDC)');
      expect(identityTranslations.es.strategySAML2).toBe('SAML 2.0 Empresarial');
      expect(identityTranslations.es.strategyOAuth2).toBe('OAuth 2.0 Genérico');
    });

    it('has edit labels', () => {
      expect(identityTranslations.es.editBtn).toBe('Editar');
      expect(identityTranslations.es.unsavedChanges).toBe('Cambios sin guardar');
      expect(identityTranslations.es.discardChanges).toBe('Descartar y continuar');
    });

    it('has notification labels', () => {
      expect(identityTranslations.es.notifTenantCreated).toBe('Tenant Creado');
      expect(identityTranslations.es.notifActivated).toBe('Tenant Activado');
      expect(identityTranslations.es.notifSuspended).toBe('Tenant Suspendido');
    });

    it('has notifTenantCreatedMsg function', () => {
      const msg = identityTranslations.es.notifTenantCreatedMsg('Test Corp', 'TEST');
      expect(msg).toContain('Test Corp');
      expect(msg).toContain('TEST');
    });

    it('has notifProviderAddedMsg function', () => {
      const msg = identityTranslations.es.notifProviderAddedMsg('Azure AD', 'OIDC');
      expect(msg).toContain('Azure AD');
      expect(msg).toContain('OIDC');
    });

    it('has notifStatusSetTo function', () => {
      const msg = identityTranslations.es.notifStatusSetTo('Active');
      expect(msg).toContain('Active');
    });

    it('has user account labels', () => {
      expect(identityTranslations.es.createUserAccountTitle).toBe('Crear Nueva Cuenta de Usuario');
      expect(identityTranslations.es.createUserBtn).toBe('Crear Usuario');
      expect(identityTranslations.es.userEmail).toBe('Correo Electrónico');
    });

    it('has block and restore labels', () => {
      expect(identityTranslations.es.blockBtn).toBe('Bloquear');
      expect(identityTranslations.es.restoreBtn).toBe('Restaurar');
      expect(identityTranslations.es.blocked).toBe('Bloqueado');
    });

    it('has password management labels', () => {
      expect(identityTranslations.es.passwordManagement).toBe('Gestión de Contraseña');
      expect(identityTranslations.es.savePassword).toBe('Guardar contraseña');
      expect(identityTranslations.es.passwordMinLength).toContain('12');
    });

    it('has notifPasswordUpdated labels', () => {
      expect(identityTranslations.es.notifPasswordUpdated).toBe('Contraseña Actualizada');
      expect(identityTranslations.es.notifPasswordUpdateFailed).toBe(
        'Error al Actualizar Contraseña'
      );
    });
  });

  describe('english', () => {
    it('has identityContext', () => {
      expect(identityTranslations.en.identityContext).toBe('Identity');
    });

    it('has tenant and tenants', () => {
      expect(identityTranslations.en.tenant).toBe('Tenant');
      expect(identityTranslations.en.tenants).toBe('Tenants');
    });

    it('has userAccounts', () => {
      expect(identityTranslations.en.userAccounts).toBe('User Accounts');
    });

    it('has tenantMaintenance', () => {
      expect(identityTranslations.en.tenantMaintenance).toBe('Tenant Maintenance');
    });

    it('has createTenantTitle', () => {
      expect(identityTranslations.en.createTenantTitle).toBe('Create New Tenant');
    });

    it('has tenantCode and tenantName', () => {
      expect(identityTranslations.en.tenantCode).toBe('Tenant Code');
      expect(identityTranslations.en.tenantName).toBe('Tenant Name');
    });

    it('has cancelBtn and registerTenantBtn', () => {
      expect(identityTranslations.en.cancelBtn).toBe('Cancel');
      expect(identityTranslations.en.registerTenantBtn).toBe('Register Tenant');
    });

    it('has colTenantName, colCode, colStatus, colAction', () => {
      expect(identityTranslations.en.colTenantName).toBe('Tenant Name');
      expect(identityTranslations.en.colCode).toBe('Code');
      expect(identityTranslations.en.colStatus).toBe('Status');
      expect(identityTranslations.en.colAction).toBe('Action');
    });

    it('has pending', () => {
      expect(identityTranslations.en.pending).toBe('Pending');
    });

    it('has suspendBtn and activateBtn', () => {
      expect(identityTranslations.en.suspendBtn).toBe('Suspend');
      expect(identityTranslations.en.activateBtn).toBe('Activate');
    });

    it('has tab labels', () => {
      expect(identityTranslations.en.tabLocations).toBe('Locations');
      expect(identityTranslations.en.tabAuthIdps).toBe('Auth IDPs');
    });

    it('has branch labels', () => {
      expect(identityTranslations.en.branchCode).toBe('Branch Code');
      expect(identityTranslations.en.branchName).toBe('Branch Name');
      expect(identityTranslations.en.addLocation).toBe('Add Location');
    });

    it('has identity provider labels', () => {
      expect(identityTranslations.en.identityProviders).toBe('Identity Providers');
      expect(identityTranslations.en.addProvider).toBe('Add Provider');
      expect(identityTranslations.en.saveProvider).toBe('Save Provider');
    });

    it('has IdP strategy labels', () => {
      expect(identityTranslations.en.strategyOIDC).toBe('OpenID Connect (OIDC)');
      expect(identityTranslations.en.strategySAML2).toBe('SAML 2.0 Enterprise');
      expect(identityTranslations.en.strategyOAuth2).toBe('OAuth 2.0 Generic');
    });

    it('has edit labels', () => {
      expect(identityTranslations.en.editBtn).toBe('Edit');
      expect(identityTranslations.en.unsavedChanges).toBe('Unsaved changes');
      expect(identityTranslations.en.discardChanges).toBe('Discard & continue');
    });

    it('has notification labels', () => {
      expect(identityTranslations.en.notifTenantCreated).toBe('Tenant Created');
      expect(identityTranslations.en.notifActivated).toBe('Tenant Activated');
      expect(identityTranslations.en.notifSuspended).toBe('Tenant Suspended');
    });

    it('has notifTenantCreatedMsg function', () => {
      const msg = identityTranslations.en.notifTenantCreatedMsg('Test Corp', 'TEST');
      expect(msg).toContain('Test Corp');
      expect(msg).toContain('TEST');
    });

    it('has notifProviderAddedMsg function', () => {
      const msg = identityTranslations.en.notifProviderAddedMsg('Azure AD', 'OIDC');
      expect(msg).toContain('Azure AD');
      expect(msg).toContain('OIDC');
    });

    it('has notifStatusSetTo function', () => {
      const msg = identityTranslations.en.notifStatusSetTo('Active');
      expect(msg).toContain('Active');
    });

    it('has user account labels', () => {
      expect(identityTranslations.en.createUserAccountTitle).toBe('Create New User Account');
      expect(identityTranslations.en.createUserBtn).toBe('Create User');
    });

    it('has block and restore labels', () => {
      expect(identityTranslations.en.blockBtn).toBe('Block');
      expect(identityTranslations.en.restoreBtn).toBe('Restore');
      expect(identityTranslations.en.blocked).toBe('Blocked');
    });

    it('has password management labels', () => {
      expect(identityTranslations.en.passwordManagement).toBe('Password Management');
      expect(identityTranslations.en.savePassword).toBe('Save password');
      expect(identityTranslations.en.passwordMinLength).toContain('12');
    });

    it('has notifPasswordUpdated labels', () => {
      expect(identityTranslations.en.notifPasswordUpdated).toBe('Password Updated');
      expect(identityTranslations.en.notifPasswordUpdateFailed).toBe('Password Update Failed');
    });
  });
});
