import { describe, expect, it } from 'vitest';
import { sharedTranslations } from './shared.translations';

describe('sharedTranslations', () => {
  describe('spanish', () => {
    it('has appName', () => {
      expect(sharedTranslations.es.appName).toBe('UMS EMPRESARIAL');
    });

    it('has appSubtitle', () => {
      expect(sharedTranslations.es.appSubtitle).toBe('Monolito Modular');
    });

    it('has portalFooter', () => {
      expect(sharedTranslations.es.portalFooter).toBe('Portal de Identidad UMS');
    });

    it('has toggleTheme', () => {
      expect(sharedTranslations.es.toggleTheme).toBe('Cambiar tema claro/oscuro');
    });

    it('has toggleLanguage', () => {
      expect(sharedTranslations.es.toggleLanguage).toBe('Cambiar idioma del sistema');
    });

    it('has logoutBtn', () => {
      expect(sharedTranslations.es.logoutBtn).toBe('Cerrar Sesión');
    });

    it('has searchPlaceholder', () => {
      expect(sharedTranslations.es.searchPlaceholder).toBe('Ingrese parámetro de búsqueda...');
    });

    it('has dataViewEmptyTitle', () => {
      expect(sharedTranslations.es.dataViewEmptyTitle).toBe('Sin resultados');
    });

    it('has noRecords', () => {
      expect(sharedTranslations.es.noRecords).toContain('No se encontraron registros');
    });

    it('has showing and of', () => {
      expect(sharedTranslations.es.showing).toBe('Mostrando');
      expect(sharedTranslations.es.of).toBe('de');
    });

    it('has clearFilter', () => {
      expect(sharedTranslations.es.clearFilter).toBe('Limpiar Filtro');
    });

    it('has newBtn, cancelEdit, saveBtn', () => {
      expect(sharedTranslations.es.newBtn).toBe('Nuevo');
      expect(sharedTranslations.es.cancelEdit).toBe('Cancelar');
      expect(sharedTranslations.es.saveBtn).toBe('Guardar');
    });

    it('has active and suspended', () => {
      expect(sharedTranslations.es.active).toBe('Activo');
      expect(sharedTranslations.es.suspended).toBe('Suspendido');
    });

    it('has sessionExpired and sessionExpiredMsg', () => {
      expect(sharedTranslations.es.sessionExpired).toBe('Sesión Expirada');
      expect(sharedTranslations.es.sessionExpiredMsg).toContain('ha expirado');
    });

    it('has error translations', () => {
      expect(sharedTranslations.es.errorGenericTitle).toBe('Algo salió mal');
      expect(sharedTranslations.es.errorNetworkTitle).toBe('Error de Red o API');
      expect(sharedTranslations.es.errorRetry).toBe('Intentar de nuevo');
    });

    it('has notifDrawerUnread function', () => {
      expect(sharedTranslations.es.notifDrawerUnread(10)).toBe('10 operaciones sin leer');
    });

    it('has errorSupportReference function', () => {
      const result = sharedTranslations.es.errorSupportReference('ERR-001');
      expect(result).toContain('ERR-001');
    });
  });

  describe('english', () => {
    it('has appName', () => {
      expect(sharedTranslations.en.appName).toBe('UMS ENTERPRISE');
    });

    it('has appSubtitle', () => {
      expect(sharedTranslations.en.appSubtitle).toBe('Modular Monolith');
    });

    it('has portalFooter', () => {
      expect(sharedTranslations.en.portalFooter).toBe('UMS Identity Portal');
    });

    it('has toggleTheme', () => {
      expect(sharedTranslations.en.toggleTheme).toBe('Toggle light/dark theme');
    });

    it('has toggleLanguage', () => {
      expect(sharedTranslations.en.toggleLanguage).toBe('Toggle system language');
    });

    it('has logoutBtn', () => {
      expect(sharedTranslations.en.logoutBtn).toBe('Log out session');
    });

    it('has searchPlaceholder', () => {
      expect(sharedTranslations.en.searchPlaceholder).toBe('Enter search parameter...');
    });

    it('has dataViewEmptyTitle', () => {
      expect(sharedTranslations.en.dataViewEmptyTitle).toBe('No results found');
    });

    it('has noRecords', () => {
      expect(sharedTranslations.en.noRecords).toContain('No matching records');
    });

    it('has showing and of', () => {
      expect(sharedTranslations.en.showing).toBe('Showing');
      expect(sharedTranslations.en.of).toBe('of');
    });

    it('has clearFilter', () => {
      expect(sharedTranslations.en.clearFilter).toBe('Clear Criteria Filter');
    });

    it('has newBtn, cancelEdit, saveBtn', () => {
      expect(sharedTranslations.en.newBtn).toBe('New');
      expect(sharedTranslations.en.cancelEdit).toBe('Cancel');
      expect(sharedTranslations.en.saveBtn).toBe('Save');
    });

    it('has active and suspended', () => {
      expect(sharedTranslations.en.active).toBe('Active');
      expect(sharedTranslations.en.suspended).toBe('Suspended');
    });

    it('has sessionExpired and sessionExpiredMsg', () => {
      expect(sharedTranslations.en.sessionExpired).toBe('Session Expired');
      expect(sharedTranslations.en.sessionExpiredMsg).toContain('expired due to inactivity');
    });

    it('has error translations', () => {
      expect(sharedTranslations.en.errorGenericTitle).toBe('Something went wrong');
      expect(sharedTranslations.en.errorNetworkTitle).toBe('Network or API Error');
      expect(sharedTranslations.en.errorRetry).toBe('Try again');
    });

    it('has notifDrawerUnread function', () => {
      expect(sharedTranslations.en.notifDrawerUnread(7)).toBe('7 unread operations');
    });

    it('has errorSupportReference function', () => {
      const result = sharedTranslations.en.errorSupportReference('ERR-002');
      expect(result).toContain('ERR-002');
    });
  });
});
