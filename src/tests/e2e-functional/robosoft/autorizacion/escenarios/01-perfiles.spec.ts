import { test, expect } from '@playwright/test';
import { autenticar, claveDe, claims, SUJETOS, type Grafo } from '../fixtures/entorno';

const menus = (g: Grafo) => g.menuAccess.map((m) => m.code).sort();

test.describe('E-01…E-04 · Lo que cada perfil concede', () => {
  test('E-01 el sujeto multi-perfil recibe sus dos perfiles, de rol distinto', async () => {
    const { grafo } = await autenticar(SUJETOS.multiPerfil, claveDe(SUJETOS.multiPerfil));

    expect(grafo.accessState).toBe('Granted');
    expect(grafo.profiles).toHaveLength(2);

    // Dos perfiles del MISMO rol no prueban nada: el grafo seria identico se elija el que se elija.
    const roles = grafo.profiles.map((p) => p.role.code);
    expect(new Set(roles).size).toBe(2);
    expect(roles).toEqual(expect.arrayContaining(['AUDITOR', 'DESARROLLADOR']));
  });

  test('E-02 exactamente uno viene marcado como el perfil actual', async () => {
    const { grafo } = await autenticar(SUJETOS.multiPerfil, claveDe(SUJETOS.multiPerfil));
    expect(grafo.profiles.filter((p) => p.isCurrent)).toHaveLength(1);
  });

  test('E-03 los dos perfiles NO conceden lo mismo', async () => {
    const { token, grafo: conAuditor } = await autenticar(SUJETOS.multiPerfil, claveDe(SUJETOS.multiPerfil));
    const otro = conAuditor.profiles.find((p) => !p.isCurrent)!;
    const { cambiarPerfil } = await import('../fixtures/entorno');
    const r = await cambiarPerfil(token, otro.id);
    expect(r.estado).toBe(200);
    const conEquipo: Grafo = JSON.parse(r.cuerpo.graph);

    // Es LA comprobacion del carril: si los dos grafos coincidieran, el perfil no estaria
    // gobernando nada y la autorizacion seria decorativa.
    expect(menus(conEquipo)).not.toEqual(menus(conAuditor));
    expect(conAuditor.menuAccess.length).toBeGreaterThan(conEquipo.menuAccess.length);
  });

  test('E-04 el auditor solo recibe permisos de lectura', async () => {
    const { grafo } = await autenticar(SUJETOS.multiPerfil, claveDe(SUJETOS.multiPerfil));
    const actual = grafo.profiles.find((p) => p.isCurrent)!;
    test.skip(actual.role.code !== 'AUDITOR', 'el perfil por defecto no es el de auditor');

    // Un auditor de cumplimiento que pudiera crear o borrar dejaria de ser auditor.
    const escritura = grafo.scopes.filter((s) => /\.(create|update|delete|approve|execute)$/.test(s));
    expect(escritura, `el auditor recibe permisos de escritura: ${escritura.join(', ')}`).toEqual([]);
    expect(grafo.scopes.length).toBeGreaterThan(0);
  });

  test('E-05 el token declara cual es el perfil activo', async () => {
    const { token, grafo } = await autenticar(SUJETOS.multiPerfil, claveDe(SUJETOS.multiPerfil));
    const actual = grafo.profiles.find((p) => p.isCurrent)!;
    const c = claims(token);

    // Sin `profile_id` en el token, el sistema cliente no puede saber con que sombrero entro
    // el usuario, y tendria que fiarse del grafo, que viaja aparte.
    expect(c.profile_id).toBe(actual.id);
    expect(c.sys_suite).toBe('SDLC');
  });
});
