// Contexto acotado «configuration» — carril B (API, caja negra) · G-112
//
// Re-verifica la configuración jerárquica de la plataforma (FR-040) y los feature flags (FR-041):
// resolución en cascada por código, FSM Draft→Published→Archived, versionado, y flags acotados a
// suite con evaluación fail-closed. Fuente: reference/qa/e2e-certification-matrix.md (configuration).
//
// Los invariantes de PRECEDENCIA multi-ámbito contestada (CF02/CF06/CF07), evaluación de criterios
// AND/OR y porcentaje (CF09/CF10/CF12) y los de effectiveConfig en el grafo (CF13/CF14/CF15) quedan
// PEND: dependen de semántica de precedencia aún en disputa (CF02 FAIL en auditoría) o del grafo de
// autenticación entregado en login, fuera del alcance determinista de este contexto.
import { test, expect, type APIRequestContext } from '@playwright/test';
import { invariante, invariantePendiente, esperarEstado } from '../helpers/invariant';
import { API, EVOLITH_TENANT_ID , devAuthHeaders } from '../helpers/auth';
import { uid, codigoUnico, crearSuite , identidadConPermisos } from '../helpers/provision';

const CTX = 'configuration';
// G-280: la identidad que ACTUA tiene que existir y traer permisos. Era `X-User-Id: uid()`, un
// GUID al azar que solo funcionaba mientras el atajo de Admin Root dejaba pasar sin mirar scopes.
const H = async (request: APIRequestContext) => ({
  ...devAuthHeaders(await identidadConPermisos(request)),
  'Content-Type': 'application/json',
});

test.describe('Configuration', () => {
  invariante(
    { id: 'INV-CF01', contexto: CTX, descripcion: 'Configuración jerárquica: una config Global publicada se resuelve por código en cascada', referencia: 'ResolveAppConfigurationQuery (BR-1, FR-040)' },
    async ({ request }) => {
      const code = codigoUnico('CFG');
      const crear = await request.post(`${API}/app-configurations`, {
        headers: await H(request),
        data: { tenantId: null, systemSuiteId: null, moduleId: null, code, value: 'valor-global', description: 'cfg global', isInheritable: true, isEncrypted: false },
      });
      await esperarEstado(crear, 201);
      const id = (await crear.json()).appConfigurationId as string;
      await esperarEstado(await request.post(`${API}/app-configurations/${id}/publish`, { headers: await H(request) }), 204);
      // Resolución en cascada por código → encuentra el valor Global.
      const res = await request.get(`${API}/app-configurations/resolve?code=${code}`, { headers: await H(request) });
      expect(res.status()).toBe(200);
      const dto = await res.json();
      expect(dto.found).toBe(true);
      expect(dto.value).toBe('valor-global');
      expect(dto.resolvedScope).toBe('Global');
    },
  );

  invariante(
    { id: 'INV-CF03', contexto: CTX, descripcion: 'FSM Draft → Published → Archived; una transición terminal (publicar Archived) se bloquea', referencia: 'AppConfiguration FSM (FR-040)' },
    async ({ request }) => {
      const code = codigoUnico('CFG');
      const crear = await request.post(`${API}/app-configurations`, {
        headers: await H(request), data: { tenantId: null, systemSuiteId: null, moduleId: null, code, value: 'v', description: 'd', isInheritable: true, isEncrypted: false },
      });
      await esperarEstado(crear, 201);
      const id = (await crear.json()).appConfigurationId as string;
      const estado = async () => (await (await request.get(`${API}/app-configurations/${id}`, { headers: await H(request) })).json()).status;
      expect(await estado()).toBe('Draft');
      await esperarEstado(await request.post(`${API}/app-configurations/${id}/publish`, { headers: await H(request) }), 204);
      expect(await estado()).toBe('Published');
      await esperarEstado(await request.post(`${API}/app-configurations/${id}/archive`, { headers: await H(request) }), 204);
      expect(await estado()).toBe('Archived');
      // Terminal: publicar una config Archived debe rechazarse (no vuelve a Published).
      const rePublicar = await request.post(`${API}/app-configurations/${id}/publish`, { headers: await H(request) });
      expect([400, 409]).toContain(rePublicar.status());
    },
  );

  invariante(
    { id: 'INV-CF04', contexto: CTX, descripcion: 'Solo las configs Published participan en la resolución (Draft excluida)', referencia: 'ResolveAppConfigurationQuery (solo Published, FR-040)' },
    async ({ request }) => {
      const code = codigoUnico('CFG');
      const crear = await request.post(`${API}/app-configurations`, {
        headers: await H(request), data: { tenantId: null, systemSuiteId: null, moduleId: null, code, value: 'solo-si-publicada', description: 'd', isInheritable: true, isEncrypted: false },
      });
      await esperarEstado(crear, 201);
      const id = (await crear.json()).appConfigurationId as string;
      // En Draft: NO se resuelve.
      const draft = await (await request.get(`${API}/app-configurations/resolve?code=${code}`, { headers: await H(request) })).json();
      expect(draft.found).toBe(false);
      // Publicada: sí se resuelve.
      await esperarEstado(await request.post(`${API}/app-configurations/${id}/publish`, { headers: await H(request) }), 204);
      const pub = await (await request.get(`${API}/app-configurations/resolve?code=${code}`, { headers: await H(request) })).json();
      expect(pub.found).toBe(true);
      expect(pub.value).toBe('solo-si-publicada');
    },
  );

  invariante(
    { id: 'INV-CF05', contexto: CTX, descripcion: 'Versionado: editar una config incrementa su versión', referencia: 'UpdateAppConfigurationCommand (versionado, FR-040)' },
    async ({ request }) => {
      const code = codigoUnico('CFG');
      const crear = await request.post(`${API}/app-configurations`, {
        headers: await H(request), data: { tenantId: null, systemSuiteId: null, moduleId: null, code, value: 'v1', description: 'd', isInheritable: true, isEncrypted: false },
      });
      await esperarEstado(crear, 201);
      const id = (await crear.json()).appConfigurationId as string;
      const versionInicial = (await (await request.get(`${API}/app-configurations/${id}`, { headers: await H(request) })).json()).version as string;
      await esperarEstado(await request.put(`${API}/app-configurations/${id}`, { headers: await H(request), data: { value: 'v2', description: 'd2' } }), 204);
      const versionNueva = (await (await request.get(`${API}/app-configurations/${id}`, { headers: await H(request) })).json()).version as string;
      expect(versionNueva).not.toBe(versionInicial);
    },
  );

  invariante(
    { id: 'INV-CF08', contexto: CTX, descripcion: 'Feature flag acotado a la suite: la suite es obligatoria (nunca global)', referencia: 'CreateFeatureFlagCommand (SystemSuiteId obligatorio, FR-041)' },
    async ({ request }) => {
      const suite = await crearSuite(request);
      await esperarEstado(suite.res, 201);
      // Con suite válida → 201.
      const ok = await request.post(`${API}/feature-flags`, {
        headers: await H(request), data: { systemSuiteId: suite.id, tenantId: null, flagCode: codigoUnico('FLAG'), flagType: 'Boolean', flagTargets: 'all', linkedResourceType: null, linkedResourceId: null, rolloutPercentage: null },
      });
      await esperarEstado(ok, 201);
      // Sin suite (Guid vacío) → rechazado: el flag nunca es global.
      const sinSuite = await request.post(`${API}/feature-flags`, {
        headers: await H(request), data: { systemSuiteId: '00000000-0000-0000-0000-000000000000', tenantId: null, flagCode: codigoUnico('FLAG'), flagType: 'Boolean', flagTargets: 'all', linkedResourceType: null, linkedResourceId: null, rolloutPercentage: null },
      });
      expect([400, 404, 409]).toContain(sinSuite.status());
    },
  );

  invariante(
    { id: 'INV-CF11', contexto: CTX, descripcion: 'Fail-closed respecto al ciclo de vida del flag: un flag desactivado evalúa como deshabilitado', referencia: 'EvaluateFeatureFlagCommand (fail-closed por estado, FR-041)' },
    async ({ request }) => {
      const suite = await crearSuite(request);
      await esperarEstado(suite.res, 201);
      const crear = await request.post(`${API}/feature-flags`, {
        headers: await H(request), data: { systemSuiteId: suite.id, tenantId: null, flagCode: codigoUnico('FLAG'), flagType: 'Boolean', flagTargets: 'all', linkedResourceType: null, linkedResourceId: null, rolloutPercentage: null },
      });
      await esperarEstado(crear, 201);
      const id = (await crear.json()).featureFlagId as string;
      // Activar y luego desactivar → al evaluar debe quedar deshabilitado (fail-closed).
      await esperarEstado(await request.post(`${API}/feature-flags/${id}/activate`, { headers: await H(request) }), 204);
      await esperarEstado(await request.post(`${API}/feature-flags/${id}/deactivate`, { headers: await H(request) }), 204);
      const evalRes = await request.post(`${API}/feature-flags/${id}/evaluate`, { headers: await H(request), data: { tenantId: EVOLITH_TENANT_ID } });
      expect(evalRes.status()).toBe(200);
      expect((await evalRes.json()).isEnabled).toBe(false);
    },
  );

  // ── PEND: precedencia contestada, criterios/porcentaje y effectiveConfig del grafo ──
  const pend = [
    { id: 'INV-CF02', d: "Precedencia efectiva multi-ámbito (Global/Suite/Tenant/Module)", m: 'La semántica de precedencia sigue en disputa (CF02 FAIL en la auditoría; el orden documentado y el observado no coinciden). Requiere una decisión de arquitectura (ADR) antes de aseverarla.' },
    { id: 'INV-CF06', d: 'Suceder un valor Archived con nueva versión del mismo (scope,code)', m: 'Depende de la resolución de la precedencia/versionado multi-ámbito (relacionado con CF02); no aseverable de forma estable hasta fijar la semántica.' },
    { id: 'INV-CF07', d: 'Configs Suite/Module sin tenantId resolubles entre tenants', m: 'Depende de la semántica de precedencia multi-ámbito en disputa (CF02).' },
    { id: 'INV-CF09', d: 'Intra-tipo OR, entre-tipos AND en la evaluación de criterios', m: 'Requiere sembrar múltiples criterios y contextos de evaluación combinados; se aborda en una iteración dedicada de feature flags.' },
    { id: 'INV-CF10', d: 'Fail-closed ante contexto de evaluación incompleto', m: 'Requiere un flag con criterios que exijan contexto para observar el fail-closed por contexto (distinto del fail-closed por estado, cubierto en CF11).' },
    { id: 'INV-CF12', d: 'El tipo Percentage / rolloutPercentage se aplica en la evaluación', m: 'La evaluación por porcentaje es probabilística/determinística por hash de sujeto; requiere un diseño de muestreo estable no cubierto en esta iteración.' },
    { id: 'INV-CF13', d: 'effectiveConfig presente en el grafo con precedencia Tenant-override sobre Global', m: 'effectiveConfig se entrega en el grafo de autenticación (login); se verifica en el contexto authorization-graph, no aquí.' },
    { id: 'INV-CF14', d: 'El grafo/effectiveConfig cumple el contrato auth-graph.schema.json', m: 'Validación de contrato del grafo de login; corresponde al contexto authorization-graph.' },
    { id: 'INV-CF15', d: 'effectiveConfig de un tenant CLIENT vía /client/authenticate', m: 'Depende del grafo de /client/authenticate para tenants CLIENT; se verifica en authorization-graph/authn.' },
  ];
  for (const p of pend) {
    invariantePendiente({ id: p.id, contexto: CTX, descripcion: p.d, referencia: 'configuration / authorization-graph', motivo: p.m }, async () => { /* PEND */ });
  }
});
