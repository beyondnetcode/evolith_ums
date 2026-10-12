import { request, type APIRequestContext } from '@playwright/test';

/**
 * Borra lo que una prueba creó. Sin esto, el carril A es una suite ADITIVA sobre un clúster
 * compartido: cada corrida deja residuo, el catálogo crece, y las pruebas que buscan una fila o
 * abren un desplegable tardan un poco más cada vez. Un día una tarda de más y falla —una vez—, y a
 * la siguiente pasa. Es la forma exacta del parpadeo que documenta G-245.
 *
 * Medido el 2026-08-03 antes de existir este helper: 40 sistemas y 40 cuentas de usuario en el
 * clúster eran residuo de corridas anteriores (15% y 10% de sus catálogos).
 */

const BASE = process.env.E2E_BASE_URL ?? 'http://localhost:8080';
const TENANT = process.env.E2E_TENANT ?? 'EVOLITH';
const USUARIO = process.env.E2E_ADMIN_USER ?? 'admin@evolith.example';
const CLAVE = process.env.E2E_ADMIN_PASS ?? 'Evolith.Dev.2026';

async function contexto(): Promise<APIRequestContext> {
  const ctx = await request.newContext({ baseURL: BASE });
  await ctx.post('/api/v1/auth/login', {
    data: { username: USUARIO, password: CLAVE, tenantCode: TENANT },
  });
  return ctx;
}

/**
 * Borra los registros de `recurso` cuyo nombre o código case con `patron`.
 *
 * NUNCA lanza: la limpieza que rompe la suite es peor que el residuo que evita. Un fallo al
 * limpiar se avisa por consola y se sigue — el `afterAll` no debe convertir una corrida verde en
 * roja por no haber podido borrar una fila.
 */
export async function borrarResiduo(recurso: string, patron: RegExp): Promise<number> {
  let borrados = 0;
  try {
    const ctx = await contexto();
    const r = await ctx.get(`/api/v1/${recurso}?page=1&pageSize=200`);
    if (!r.ok()) return 0;
    const items: Array<Record<string, unknown>> = (await r.json()).items ?? [];
    for (const it of items) {
      const texto = ['code', 'name', 'email', 'displayName'].map((k) => String(it[k] ?? '')).join(' ');
      if (!patron.test(texto)) continue;
      const id = it[`${recurso.replace(/s$/, '').replace(/-([a-z])/g, (_, c) => c.toUpperCase())}Id`]
        ?? it.id ?? it.userAccountId;
      if (!id) continue;
      const d = await ctx.delete(`/api/v1/${recurso}/${id}`);
      if (d.ok()) borrados += 1;
    }
    await ctx.dispose();
  } catch (e) {
    console.warn(`  limpieza de ${recurso}: ${(e as Error).message}`);
  }
  if (borrados) console.log(`  limpieza: ${borrados} ${recurso} borrado(s)`);
  return borrados;
}

/**
 * Marca única por corrida. Nombrar con ella permite borrar DESPUÉS exactamente lo de esta corrida
 * y no lo de otra que pudiera estar ejecutándose en paralelo.
 */
export const MARCA = `E2E${Date.now()}`;
