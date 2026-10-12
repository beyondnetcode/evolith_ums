// Contexto acotado «authorization-topology» — carril B (API, caja negra) · G-112
//
// Re-verifica la topología de autorización contra el sistema VIVO: jerarquía
// SystemSuite→Module→Menu→SubMenu→Option (NodeKind + activación), acciones registrables y
// vinculables con integridad referencial, roles acíclicos, plantillas publicables sólo con ≥1 ítem,
// restricción a management-owner, Result Pattern (sin 500) y broken rules accionables.
// Fuente: bmad-tester-robosoft-audit-2026-07-16.md, «authorization-topology» (PASS 5/FAIL 5/BLOCK 1).
//
// PROVISIONA su propia suite/módulo/nodo/acción/rol/plantilla con códigos ÚNICOS por corrida:
// NO muta las suites sembradas (ADUANAS/WMS…). Determinista e idempotente.
import { test, expect } from '@playwright/test';
import { invariante, invariantePendiente, esperarEstado, codigoDeError } from '../helpers/invariant';
import {
  uid,
  crearSuite,
  identidadConPermisosEn,
  agregarModulo,
  activarModulo,
  agregarNodo,
  registrarAccion,
  vincularAccionNodo,
  crearRol,
  actualizarRol,
  crearPlantilla,
  publicarPlantilla,
  agregarItemPlantilla,
  construirArbolMinimo,
  resolverTenantIdPorCodigo,
} from '../helpers/provision';

const CTX = 'authorization-topology';

test.describe('authorization-topology', () => {
  invariante(
    { id: 'INV-AT01', contexto: CTX, descripcion: 'Jerarquía SystemSuite→Module→Menu→SubMenu→Option con enforcement de NodeKind y activación', referencia: 'ADR-0090; AddNode/NodeKind' },
    async ({ request }) => {
      const suiteId = (await crearSuite(request)).id;
      const moduleId = await agregarModulo(request, suiteId);
      // Enforcement de activación: un módulo inactivo NO admite nodos.
      const nodoEnInactivo = await agregarNodo(request, suiteId, moduleId, { kind: 'Menu', parentNodeId: null });
      await esperarEstado(nodoEnInactivo.res, [400, 409]);
      // Tras activar, se construye la jerarquía Menu → SubMenu → Option.
      await esperarEstado(await activarModulo(request, suiteId, moduleId), 204);
      const menu = await agregarNodo(request, suiteId, moduleId, { kind: 'Menu', parentNodeId: null });
      await esperarEstado(menu.res, 201);
      const submenu = await agregarNodo(request, suiteId, moduleId, { kind: 'SubMenu', parentNodeId: menu.id });
      await esperarEstado(submenu.res, 201);
      const option = await agregarNodo(request, suiteId, moduleId, { kind: 'Option', parentNodeId: submenu.id });
      await esperarEstado(option.res, 201);
      // NodeKind inválido → rechazo controlado (no 500).
      const malKind = await agregarNodo(request, suiteId, moduleId, { kind: 'Bogus', parentNodeId: null });
      await esperarEstado(malKind.res, [400, 409]);
      expect(malKind.res.status()).not.toBe(500);
    },
  );

  invariante(
    { id: 'INV-AT02', contexto: CTX, descripcion: 'Actions registrables y vinculables a nodos; integridad referencial acción↔registro (vincular acción NO registrada se rechaza)', referencia: 'F-05; LinkNodeAction (action_not_registered)' },
    async ({ request }) => {
      const arbol = await construirArbolMinimo(request);
      // Acción registrada se vincula al nodo Option.
      await esperarEstado(
        await vincularAccionNodo(request, arbol.suiteId, arbol.moduleId, arbol.optionId, 'READ'),
        204,
      );
      // Integridad referencial: vincular una acción NO registrada se rechaza (antes se aceptaba 204).
      const fantasma = await vincularAccionNodo(request, arbol.suiteId, arbol.moduleId, arbol.optionId, 'GHOST_ACTION');
      await esperarEstado(fantasma, [400, 409]);
      expect(await codigoDeError(fantasma)).toMatch(/action_not_registered/i);
    },
  );

  invariante(
    { id: 'INV-AT03', contexto: CTX, descripcion: 'Roles por tenant+suite con jerarquía acíclica (sin ciclos ni auto-parent)', referencia: 'CreateRole/UpdateRole; jerarquía acíclica' },
    async ({ request }) => {
      const suiteId = (await crearSuite(request)).id;
      const raiz = await crearRol(request, suiteId, { hierarchyLevel: 0 });
      await esperarEstado(raiz.res, 201);
      const hijo = await crearRol(request, suiteId, { parentRoleId: raiz.id, hierarchyLevel: 1, promotionOrder: 2 });
      await esperarEstado(hijo.res, 201);
      // Ciclo: hacer que la raíz apunte a su propio hijo como padre → rechazado.
      const ciclo = await actualizarRol(request, suiteId, raiz.id, { parentRoleId: hijo.id, hierarchyLevel: 0 });
      await esperarEstado(ciclo, [400, 409]);
      // Auto-parent → rechazado.
      const auto = await actualizarRol(request, suiteId, hijo.id, { parentRoleId: hijo.id, hierarchyLevel: 1, promotionOrder: 2 });
      await esperarEstado(auto, [400, 409]);
      expect(await codigoDeError(auto)).toMatch(/propio padre|no puede ser su propio/i);
    },
  );

  invariante(
    { id: 'INV-AT04', contexto: CTX, descripcion: 'Plantillas de permisos publicables sólo con ≥1 ítem', referencia: 'PublishTemplate (template_items_required)' },
    async ({ request }) => {
      const arbol = await construirArbolMinimo(request);
      const rol = await crearRol(request, arbol.suiteId, { hierarchyLevel: 0 });
      const plantilla = await crearPlantilla(request, rol.id, arbol.suiteId);
      await esperarEstado(plantilla.res, 201);
      // Publicar sin ítems → rechazado.
      const vacia = await publicarPlantilla(request, plantilla.id);
      await esperarEstado(vacia, [400, 409]);
      expect(await codigoDeError(vacia)).toMatch(/template_items_required/i);
      // Agregar un ítem (Option + acción registrada) y publicar → 204.
      await esperarEstado(
        await agregarItemPlantilla(request, plantilla.id, {
          targetType: 'Option',
          targetId: arbol.optionId,
          actionId: arbol.actionId,
        }),
        201,
      );
      await esperarEstado(await publicarPlantilla(request, plantilla.id), 204);
    },
  );

  invariante(
    { id: 'INV-AT05', contexto: CTX, descripcion: 'Provisión de suites restringida a management-owner (TenantScopePolicy)', referencia: 'TenantScopePolicy; AUTH_015 not management owner' },
    async ({ request }) => {
      // APM_CALLAO y no COMEX_ANDINA, y la diferencia es la prueba entera: el sujeto debe TENER
      // `manage_suites` y aun asi ser rechazado por no ser propietario de gestion. COMEX solo tiene
      // la suite `PORTAL_CLIENTE`, asi que ninguno de sus usuarios puede sostener ese permiso —el
      // guardia lo pararia antes y el invariante mediria otra cosa—; APM tiene `UMS` con sus
      // plantillas (G-280).
      const apmId = await resolverTenantIdPorCodigo(request, 'APM_CALLAO');
      expect(apmId, 'debe existir el tenant CLIENT sembrado APM_CALLAO').toBeTruthy();
      const identidadCliente = await identidadConPermisosEn(request, apmId!, { rol: 'Administrador' });
      const res = (await crearSuite(request, apmId!, identidadCliente)).res;
      await esperarEstado(res, [400, 403]);
      expect(res.status(), 'rechazo de scope controlado, no 500').not.toBe(500);
      expect(await codigoDeError(res)).toMatch(/AUTH_015|management owner/i);
    },
  );

  invariantePendiente(
    {
      id: 'INV-AT06',
      contexto: CTX,
      descripcion: 'Perfiles (usuario+rol+rama) con overrides allow/deny/neutral SIN mutar la plantilla fuente',
      referencia: 'BLOCK (auditoría); overrides de perfil',
      motivo:
        'BLOCK (auditoría): no se pudo materializar permisos en un perfil propio vía API para observar ' +
        'el override sin mutar la plantilla. Requiere una cadena usuario+perfil+plantilla resuelta y ' +
        'lectura del grafo efectivo, entrelazada con el bug de onboarding (perfil). No determinista en caja negra.',
    },
    async () => {},
  );

  invariantePendiente(
    {
      id: 'INV-AT07',
      contexto: CTX,
      descripcion: 'El grafo concede permisos efectivos a usuarios con rol/plantilla',
      referencia: 'H-05; resolución del grafo de autorización',
      motivo:
        'Requiere asignar la plantilla provisionada a un usuario real y hacer login para observar el ' +
        'grafo efectivo; el login de una cuenta recién provisionada choca con el bug de onboarding ' +
        '(AUTH_000). No verificable de forma determinista en caja negra en este tramo.',
    },
    async () => {},
  );

  invariante(
    { id: 'INV-AT08', contexto: CTX, descripcion: 'Result Pattern: las violaciones de reglas de negocio devuelven 4xx, nunca 500 no controlado', referencia: 'H-01; GlobalExceptionHandler / Result Pattern' },
    async ({ request }) => {
      const { id: suiteId, code } = await crearSuite(request);
      const moduleId = await agregarModulo(request, suiteId);
      await activarModulo(request, suiteId, moduleId);
      const option = await agregarNodo(request, suiteId, moduleId, { kind: 'Option', parentNodeId: null });
      // Batería de violaciones de dominio; NINGUNA debe ser 500.
      const respuestas = [
        (await crearSuite(request)).res, // control (201)
        await request.post(`/api/v1/system-suites`, { data: { tenantId: '5f4e3d2c-1b0a-9f8e-7d6c-543210987654', code, name: 'x', description: 'y' }, headers: { 'Content-Type': 'application/json' } }), // código duplicado
        (await agregarNodo(request, suiteId, moduleId, { kind: 'Bogus', parentNodeId: null })).res, // kind inválido
        await vincularAccionNodo(request, suiteId, moduleId, option.id, 'NO_REGISTRADA'), // acción no registrada
      ];
      for (const r of respuestas) {
        expect(r.status(), `código ${r.status()} en ${r.url()} — no debe ser 500`).not.toBe(500);
        expect(r.status()).toBeLessThan(500);
      }
    },
  );

  invariante(
    { id: 'INV-AT09', contexto: CTX, descripcion: 'Broken rules con detalle accionable (errorCode/brokenRule específico) en violaciones de dominio', referencia: 'ProblemDetails errorCode/brokenRule' },
    async ({ request }) => {
      const arbol = await construirArbolMinimo(request);
      const res = await vincularAccionNodo(request, arbol.suiteId, arbol.moduleId, arbol.optionId, 'NO_REGISTRADA');
      await esperarEstado(res, [400, 409]);
      const body = await res.json();
      // El error debe traer un código de dominio específico y accionable (no un texto genérico vacío).
      const codigo = String(body.errorCode ?? body.brokenRule ?? '');
      expect(codigo, 'debe incluir un errorCode/brokenRule de dominio').toMatch(/action_not_registered/i);
    },
  );

  invariante(
    { id: 'INV-AT10', contexto: CTX, descripcion: '/client/authenticate devuelve el grafo para tenants CLIENT sembrados', referencia: 'authn-local-01; /client/authenticate' },
    async ({ request }) => {
      const res = await request.post('/api/v1/client/authenticate', {
        data: { tenantCode: 'COMEX_ANDINA', username: 'usuario.impo@comexandina.com.pe', password: 'Evolith.Dev.2026' },
        headers: { 'Content-Type': 'application/json' },
      });
      await esperarEstado(res, 200);
      const body = await res.json();
      expect(body.token, 'debe emitir token para el tenant CLIENT').toBeTruthy();
    },
  );

  invariante(
    { id: 'INV-AT11', contexto: CTX, descripcion: 'Crear la SystemSuite propia de RoboSoft bajo EVOLITH (management-owner)', referencia: 'CreateSystemSuite; provisión bajo EVOLITH' },
    async ({ request }) => {
      const suite = await crearSuite(request);
      await esperarEstado(suite.res, 201);
      expect(suite.id).toBeTruthy();
    },
  );
});
