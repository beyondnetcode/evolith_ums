import { test, expect } from '@playwright/test';
import { autenticar, cambiarPerfil, claveDe, SUJETOS, UMS, TENANT, SISTEMA } from '../fixtures/entorno';

test.describe('E-06…E-08 · Lo que NO se concede', () => {
  test('E-06 un usuario sin perfil en el sistema no recibe ninguno', async () => {
    const { usuario, clave } = SUJETOS.sinPerfilEnSistema;
    const { grafo } = await autenticar(usuario, clave);

    // Ni error ni acceso: un tercer estado explicito. Devolver 401 confundiria «no tienes perfil
    // aqui» con «tus credenciales son malas», y el usuario no sabria a quien pedir ayuda.
    expect(grafo.accessState).toBe('NoProfileInSystem');
    expect(grafo.profiles).toHaveLength(0);
    expect(grafo.menuAccess ?? []).toHaveLength(0);
  });

  test('E-07 no se puede tomar el perfil de otro, y la respuesta no delata que existe', async () => {
    const { token } = await autenticar(SUJETOS.multiPerfil, claveDe(SUJETOS.multiPerfil));

    // Perfil real, de OTRO usuario. Se pide su id a proposito: es el caso que un atacante
    // probaria si consiguiera un identificador ajeno.
    const ajeno = await (async () => {
      const r = await fetch(`${UMS}/api/v1/profiles?page=1&pageSize=200`);
      const items = ((await r.json()) as { items: Array<{ profileId: string; userEmail: string; isActive: boolean }> }).items;
      return items.find((p) => p.userEmail !== SUJETOS.multiPerfil && p.isActive)!.profileId;
    })();

    const r = await cambiarPerfil(token, ajeno);
    expect(r.estado).toBe(404);

    // La respuesta dice «no se encontro», no «no es tuyo»: ambas cierran la puerta, pero solo
    // una evita confirmarle al que pregunta que ese identificador existe.
    expect(JSON.stringify(r.cuerpo)).not.toMatch(/no es tuyo|pertenece|otro usuario|forbidden/i);
    expect(r.cuerpo.code).toBe('AUTH_020');
  });

  test('E-08 un perfil inexistente se rechaza EXACTAMENTE igual que uno ajeno', async () => {
    const { token } = await autenticar(SUJETOS.multiPerfil, claveDe(SUJETOS.multiPerfil));

    // Un identificador con forma valida que no corresponde a ningun perfil. No vale el GUID de
    // ceros: el borde lo rechaza antes, como «obligatorio», y eso mide la validacion de entrada,
    // no la fuga de informacion.
    const inexistente = await cambiarPerfil(token, '9f3c1d77-2b4e-4a91-8c05-6de7f1a2b3c4'); // gitleaks:allow -- synthetic test profile ID

    const ajeno = await (async () => {
      const r = await fetch(`${UMS}/api/v1/profiles?page=1&pageSize=200`);
      const items = ((await r.json()) as { items: Array<{ profileId: string; userEmail: string; isActive: boolean }> }).items;
      return cambiarPerfil(token, items.find((p) => p.userEmail !== SUJETOS.multiPerfil && p.isActive)!.profileId);
    })();

    // Si «existe pero no es tuyo» respondiera distinto que «no existe», esa diferencia seria un
    // oraculo: bastaria probar identificadores para averiguar cuales son reales.
    expect(inexistente.estado).toBe(ajeno.estado);
    expect(inexistente.cuerpo.code).toBe(ajeno.cuerpo.code);
    expect(inexistente.estado).toBe(404);
  });

  test('E-09 sin token no se cambia de perfil', async () => {
    const r = await fetch(`${UMS}/api/v1/client/switch-profile`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ profileId: '00000000-0000-0000-0000-000000000000' }),
    });
    expect([401, 403]).toContain(r.status);
  });

  test('E-10 una credencial correcta de otro inquilino no abre este sistema', async () => {
    const r = await fetch(`${UMS}/api/v1/client/authenticate`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        username: SUJETOS.multiPerfil,
        password: claveDe(SUJETOS.multiPerfil),
        tenantCode: 'INQUILINO_QUE_NO_EXISTE',
        systemCode: SISTEMA,
      }),
    });
    expect(r.ok).toBeFalsy();
    expect(TENANT).not.toBe('INQUILINO_QUE_NO_EXISTE');
  });
});
