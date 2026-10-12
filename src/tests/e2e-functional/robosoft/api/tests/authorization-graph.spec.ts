// Contexto acotado «authorization-graph» — carril B (API, caja negra) · G-112
//
// Re-verifica lo observable del grafo de autorización contra el sistema VIVO. La mayoría de las
// invariantes de RESOLUCIÓN del grafo (deny-wins, override-wins, fail-open de domainPermissions,
// grants efectivos por plantilla) NO son verificables de forma determinista en caja negra: exigen
// construir una cadena usuario+perfil+plantilla y observar el grafo efectivo tras un login, lo que
// se entrelaza con el bug de onboarding (AUTH_000) y muta estado. Esas quedan PEND, documentadas,
// como semilla de extensión. Fuente: bmad-tester-robosoft-audit-2026-07-16.md, «authorization-graph».
import { test, expect } from '@playwright/test';
import { invariante, invariantePendiente, esperarEstado } from '../helpers/invariant';
import { CRED_COMEX, postLogin, postClientAuthenticate } from '../helpers/auth';

const CTX = 'authorization-graph';

// Aplana el árbol de navegación. Desde el contrato v2.0.0 es RECURSIVO: se recorre `children`
// hasta agotarlo en vez de presuponer tres niveles, y cada nodo lleva su lista de `actions`.
function nodosDeMenu(menuAccess: unknown[]): Array<Record<string, unknown>> {
  const nodos: Array<Record<string, unknown>> = [];
  const visitar = (lista: Array<Record<string, unknown>>) => {
    for (const nodo of lista ?? []) {
      nodos.push(nodo);
      visitar((nodo.children as Array<Record<string, unknown>>) ?? []);
    }
  };
  for (const modulo of menuAccess as Array<Record<string, unknown>>) {
    visitar((modulo.nodes as Array<Record<string, unknown>>) ?? []);
  }
  return nodos;
}

test.describe('authorization-graph', () => {
  invariante(
    { id: 'INV-AG04', contexto: CTX, descripcion: 'Ausencia = no concedido en menuAccess (fail-closed): cada opción resuelve un effect/source explícitos', referencia: 'AuthorizationGraphBuilderService.BuildMenuAccess (effect explícito)' },
    async ({ request }) => {
      const res = await postLogin(request, CRED_COMEX);
      await esperarEstado(res, 200);
      const graph = (await res.json()).authorizationGraph;
      const acciones = nodosDeMenu(graph.menuAccess ?? [])
        .flatMap(n => (n.actions as Array<Record<string, unknown>>) ?? []);
      expect(acciones.length, 'el grafo debe traer acciones resueltas').toBeGreaterThan(0);
      // Fail-closed: el builder inicializa effect/source de forma EXPLÍCITA en cada opción
      // (nunca undefined). Ausencia de permiso ⇒ effect explícito, no un hueco fail-open.
      // Desde v2.0.0 solo viajan Allow y Deny: `NotGranted` es lo que significa la ausencia,
      // así que encontrarlo en el cable sería un defecto, no un estado.
      for (const a of acciones) {
        expect(['Allow', 'Deny'], `acción ${a.actionCode}: efecto explícito`).toContain(a.effect);
        expect(['Template', 'Override'], `acción ${a.actionCode}: origen explícito`).toContain(a.source);
      }
    },
  );

  invariante(
    { id: 'INV-AG07', contexto: CTX, descripcion: 'H-05: el grafo concede accesos efectivos a los usuarios sembrados (scopes no vacíos)', referencia: 'H-05; authorizationGraph.scopes' },
    async ({ request }) => {
      const res = await postLogin(request, CRED_COMEX);
      await esperarEstado(res, 200);
      const graph = (await res.json()).authorizationGraph;
      // Antes (H-05): el grafo no concedía NADA (scopes=[]). Re-verificado: entrega scopes efectivos.
      expect(Array.isArray(graph.scopes)).toBe(true);
      expect(graph.scopes.length, 'el usuario sembrado debe recibir scopes efectivos').toBeGreaterThan(0);
    },
  );

  invariante(
    { id: 'INV-AG08', contexto: CTX, descripcion: 'Cliente externo obtiene su grafo vía POST /client/authenticate con credenciales válidas', referencia: 'authn-local-01; /client/authenticate' },
    async ({ request }) => {
      const res = await postClientAuthenticate(request, CRED_COMEX);
      await esperarEstado(res, 200);
      const body = await res.json();
      // El endpoint de integración debe entregar el grafo (o al menos el token que lo porta).
      expect(body.token ?? body.authorizationGraph, 'debe entregar token/grafo al cliente').toBeTruthy();
    },
  );

  const pendientes: Array<{ id: string; descripcion: string; motivo: string }> = [
    {
      id: 'INV-AG01',
      descripcion: 'Opción concedida en plantilla resuelve effect=Allow en el grafo',
      motivo: 'Requiere construir suite+rol+plantilla, asignarla a un usuario y observar su grafo efectivo tras login (entrelazado con el bug de onboarding). No determinista en caja negra.',
    },
    {
      id: 'INV-AG02',
      descripcion: 'Precedencia Deny>Allow: un Deny para (target,action) domina sobre Allow',
      motivo: 'PEND (auditoría): deny-wins es INOBSERVABLE en runtime sin materializar permisos Deny y Allow simultáneos sobre un perfil y leer el grafo resuelto.',
    },
    {
      id: 'INV-AG03',
      descripcion: 'Precedencia Override>Template: un permiso con IsOverride domina sobre la plantilla',
      motivo: 'PEND (auditoría): override-wins inobservable sin materializar overrides de perfil y leer el grafo efectivo.',
    },
    {
      id: 'INV-AG05',
      descripcion: 'Ausencia = no concedido en domainPermissions (fail-closed)',
      motivo: 'domainPermissions llega vacío para los tenants sembrados; observar el fail-open exige registrar un domain-resource y consultar su resolución de permiso. No determinista en caja negra en este tramo.',
    },
    {
      id: 'INV-AG06',
      descripcion: 'La resolución de grants refleja los permisos efectivos del perfil',
      motivo: 'Requiere la cadena perfil→plantilla→grafo efectivo con un usuario loginable propio (bug de onboarding). Extensión futura.',
    },
    {
      id: 'INV-AG09',
      descripcion: 'featureFlags del grafo se evalúan por suite con isEnabled y matchedCriteriaType',
      motivo: 'El grafo de los tenants CLIENT sembrados llega con featureFlags vacío; la evaluación por suite se observó en la auditoría con un admin break-glass (suite UMS) no reproducible de forma determinista aquí.',
    },
    {
      id: 'INV-AG10',
      descripcion: 'El grafo se serializa de forma consistente y en español en los endpoints que lo exponen',
      motivo: 'Inconsistencia de contrato reportada (login: objeto camelCase, enums int; /client: PascalCase, enums string). Re-verificarla exige comparación estructural cruzada estable; se deja como extensión (posible fixme de contrato).',
    },
  ];

  for (const p of pendientes) {
    invariantePendiente({ id: p.id, contexto: CTX, descripcion: p.descripcion, motivo: p.motivo }, async () => {});
  }
});
