import { describe, expect, it } from 'vitest';
import translations from './translations';

describe('translations', () => {
  it('exports spanish translations', () => {
    expect(translations.es).toBeDefined();
  });

  it('exports english translations', () => {
    expect(translations.en).toBeDefined();
  });

  it('spanish has appName', () => {
    expect(translations.es.appName).toBe('UMS EMPRESARIAL');
  });

  it('english has appName', () => {
    expect(translations.en.appName).toBe('UMS ENTERPRISE');
  });

  it('spanish has active translation', () => {
    expect(translations.es.active).toBe('Activo');
  });

  it('english has active translation', () => {
    expect(translations.en.active).toBe('Active');
  });

  it('spanish has suspended translation', () => {
    expect(translations.es.suspended).toBe('Suspendido');
  });

  it('english has suspended translation', () => {
    expect(translations.en.suspended).toBe('Suspended');
  });

  it('spanish has logoutBtn translation', () => {
    expect(translations.es.logoutBtn).toBe('Cerrar Sesión');
  });

  it('english has logoutBtn translation', () => {
    expect(translations.en.logoutBtn).toBe('Log out session');
  });

  it('spanish has saveBtn translation', () => {
    expect(translations.es.saveBtn).toBe('Guardar');
  });

  it('english has saveBtn translation', () => {
    expect(translations.en.saveBtn).toBe('Save');
  });

  it('spanish has cancelEdit translation', () => {
    expect(translations.es.cancelEdit).toBe('Cancelar');
  });

  it('english has cancelEdit translation', () => {
    expect(translations.en.cancelEdit).toBe('Cancel');
  });

  it('spanish has newBtn translation', () => {
    expect(translations.es.newBtn).toBe('Nuevo');
  });

  it('english has newBtn translation', () => {
    expect(translations.en.newBtn).toBe('New');
  });

  it('spanish has notifDrawerUnread function', () => {
    expect(typeof translations.es.notifDrawerUnread).toBe('function');
    expect(translations.es.notifDrawerUnread(5)).toBe('5 operaciones sin leer');
  });

  it('english has notifDrawerUnread function', () => {
    expect(typeof translations.en.notifDrawerUnread).toBe('function');
    expect(translations.en.notifDrawerUnread(3)).toBe('3 unread operations');
  });

  it('spanish has errorSupportReference function', () => {
    expect(typeof translations.es.errorSupportReference).toBe('function');
    expect(translations.es.errorSupportReference('ERR-123')).toContain('ERR-123');
  });

  it('english has errorSupportReference function', () => {
    expect(typeof translations.en.errorSupportReference).toBe('function');
    expect(translations.en.errorSupportReference('ERR-456')).toContain('ERR-456');
  });

  it('spanish has sessionExpired translation', () => {
    expect(translations.es.sessionExpired).toBe('Sesión Expirada');
  });

  it('english has sessionExpired translation', () => {
    expect(translations.en.sessionExpired).toBe('Session Expired');
  });

  it('spanish has searchPlaceholder translation', () => {
    expect(translations.es.searchPlaceholder).toBe('Ingrese parámetro de búsqueda...');
  });

  it('english has searchPlaceholder translation', () => {
    expect(translations.en.searchPlaceholder).toBe('Enter search parameter...');
  });
});
