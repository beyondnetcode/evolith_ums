import { renderHook } from '@testing-library/react';
import { useAccessResolution } from './use-access-resolution';
import { useAuthStore } from '@app/stores/auth.store';
import { AccessEffect } from '@domain/authorization/schemas/authorization-graph.schema';
import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('@app/stores/auth.store');

describe('useAccessResolution', () => {
  /**
   * FORMA CANÓNICA: menuAccess[].nodes[].children[], arbol de profundidad libre (ADR-0156).
   *
   * G-262: esta prueba construia el grafo con menus/subMenus/options, que el productor dejo
   * de emitir. Verificaba una ficción, y por eso el defecto del consumidor pasó inadvertido: las
   * dos partes coincidían en una forma que ya no existía en producción.
   */
  const mockGraph = {
    menuAccess: [
      {
        code: 'sys',
        value: 'Sistema',
        sortOrder: 1,
        status: 'Active',
        icon: null,
        nodes: [
          {
            code: 'profiles',
            value: 'Perfiles',
            kind: 'Menu',
            sortOrder: 1,
            icon: null,
            route: '/profiles',
            actions: [{ actionCode: 'read', effect: AccessEffect.Allow, source: 'Template' }],
            children: [
              {
                code: 'profiles-list',
                value: 'Listado',
                kind: 'SubMenu',
                sortOrder: 1,
                icon: null,
                route: null,
                actions: [],
                children: [
                  { code: 'view', value: 'Ver', kind: 'Option', sortOrder: 1, icon: null, route: null,
                    actions: [{ actionCode: 'read', effect: AccessEffect.Allow, source: 'Template' }], children: [] },
                  { code: 'create', value: 'Crear', kind: 'Option', sortOrder: 2, icon: null, route: null,
                    actions: [{ actionCode: 'create', effect: AccessEffect.Deny, source: 'Template' }], children: [] },
                  { code: 'delete', value: 'Borrar', kind: 'Option', sortOrder: 3, icon: null, route: null,
                    actions: [{ actionCode: 'delete', effect: AccessEffect.NotGranted, source: 'Template' }], children: [] },
                ],
              },
            ],
          },
        ],
      },
    ],
    featureFlags: [
      { flagCode: 'new-ui', isEnabled: true },
      { flagCode: 'beta-feature', isEnabled: false },
    ],
  };

  beforeEach(() => {
    vi.mocked(useAuthStore).mockImplementation((selector: any) =>
      selector({
        user: { authorizationGraph: mockGraph },
      })
    );
  });

  it('should return true for hasModuleAccess if module exists and is Active', () => {
    const { result } = renderHook(() => useAccessResolution());
    expect(result.current.hasModuleAccess('sys')).toBe(true);
  });

  it('should return false for hasModuleAccess if module does not exist', () => {
    const { result } = renderHook(() => useAccessResolution());
    expect(result.current.hasModuleAccess('nonexistent')).toBe(false);
  });

  it('concede el menú cuando él o algún descendiente conceden', () => {
    const { result } = renderHook(() => useAccessResolution());
    // Ya no basta con que EXISTA: el grafo trae también los nodos NotGranted, porque el cliente
    // decide si los oculta o los deshabilita. Con la sola presencia como criterio, un menú
    // explícitamente no concedido pasaba por accesible.
    expect(result.current.hasMenuAccess('sys', 'profiles')).toBe(true);
  });

  it('should correctly evaluate option access based on precedence (Allow)', () => {
    const { result } = renderHook(() => useAccessResolution());
    expect(result.current.hasOptionAccess('profiles', 'view')).toBe(true);
    expect(result.current.hasOptionAccess('profiles-list', 'view')).toBe(true);
  });

  it('should correctly evaluate option access based on precedence (Deny)', () => {
    const { result } = renderHook(() => useAccessResolution());
    expect(result.current.hasOptionAccess('profiles', 'create')).toBe(false);
  });

  it('should correctly evaluate option access based on precedence (NotGranted)', () => {
    const { result } = renderHook(() => useAccessResolution());
    expect(result.current.hasOptionAccess('profiles', 'delete')).toBe(false);
  });

  it('should correctly evaluate feature flags', () => {
    const { result } = renderHook(() => useAccessResolution());
    expect(result.current.hasFeatureFlag('new-ui')).toBe(true);
    expect(result.current.hasFeatureFlag('beta-feature')).toBe(false);
  });
});
