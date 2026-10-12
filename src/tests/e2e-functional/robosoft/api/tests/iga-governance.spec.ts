// Contexto acotado «iga» (Identity Governance & Administration) — carril B (API, caja negra) · G-112
//
// Re-verifica contra el sistema VIVO las invariantes de gobernanza de identidad del PRD:
//   • Promoción de rol (FSM auditada Draft→…→Approved→Executed→Verified, ADR-UMS-093).
//   • Delegación de gestión (FSM auditada con compuerta de aprobación, ADR-UMS-086, análogo IGA).
// Fuente: reference/qa/e2e-certification-matrix.md (sección iga) y la auditoría RoboSoft 2026-07-16.
//
// Restricción de caja negra: el `RoleMaturityStatus` (fuente de verdad de elegibilidad) es de
// SOLO LECTURA por API — no hay endpoint de escritura para sembrarlo. Por tanto el happy-path de
// promoción sólo es determinista hasta la evaluación de elegibilidad (fail-closed → Rejected); la
// SoD de 3 partes en manager/security/execute y el aislamiento cross-tenant quedan PENDIENTES
// (requieren fixture elegible / identidad de otro inquilino), documentados como brecha de cobertura.
import { test, expect } from '@playwright/test';
import { invariante, invariantePendiente, esperarEstado, codigoDeError } from '../helpers/invariant';
import {
  identidadConPermisos,
  uid,
  provisionarUsuarioInterno,
  provisionarRolReal,
  crearPromocionRol,
  accionPromocion,
  obtenerPromocion,
  crearDelegacion,
  accionDelegacion,
  obtenerDelegacion,
} from '../helpers/provision';

const CTX = 'iga';

// ══════════════════════════ Promoción de rol (FSM · RiskScore · elegibilidad · SoD) ══════════════════════════
test.describe('RolePromotion', () => {
  // Provisiona una promoción en estado Draft con objetivo y roles reales, solicitante ≠ objetivo.
  async function nuevaPromocionDraft(request: import('@playwright/test').APIRequestContext) {
    const targetUserId = await provisionarUsuarioInterno(request);
    const currentRoleId = await provisionarRolReal(request);
    const targetRoleId = await provisionarRolReal(request);
    const solicitante = await identidadConPermisos(request, { nueva: true });
    const { id, res } = await crearPromocionRol(request, { targetUserId, currentRoleId, targetRoleId, solicitante });
    await esperarEstado(res, 201);
    return { id, targetUserId, solicitante };
  }

  invariante(
    { id: 'INV-IGA1', contexto: CTX, descripcion: 'Promoción de rol por FSM auditada y guardada por estado (Draft→PendingEligibilityCheck; transición inválida rechazada)', referencia: 'RolePromotionRequest.cs (FSM, ADR-UMS-093, FR-060)' },
    async ({ request }) => {
      const { id } = await nuevaPromocionDraft(request);
      const operador = await identidadConPermisos(request, { nueva: true });
      // Recién creada: Draft, sin RiskScore aún.
      const draft = await obtenerPromocion(request, id);
      expect(draft?.status).toBe('Draft');
      expect(draft?.riskScore).toBeNull();
      // Transición inválida: no se puede aprobar (manager) desde Draft → 4xx controlado (guard de estado).
      const saltoInvalido = await accionPromocion(request, id, 'manager-approve', operador);
      await esperarEstado(saltoInvalido, [400, 409]);
      // Transición válida: submit → PendingEligibilityCheck.
      await esperarEstado(await accionPromocion(request, id, 'submit', operador), 204);
      expect((await obtenerPromocion(request, id))?.status).toBe('PendingEligibilityCheck');
    },
  );

  invariante(
    { id: 'INV-PIA1', contexto: CTX, descripcion: 'Análisis de riesgo: el RiskScore se congela en Submit (0..100) y es inmutable', referencia: 'RolePromotionRequest.Submit / RiskScore (INV-RPR2, FR-061)' },
    async ({ request }) => {
      const { id } = await nuevaPromocionDraft(request);
      const operador = await identidadConPermisos(request, { nueva: true });
      await esperarEstado(await accionPromocion(request, id, 'submit', operador), 204);
      const congelado = await obtenerPromocion(request, id);
      expect(congelado?.riskScore, 'el RiskScore debe congelarse en Submit').not.toBeNull();
      expect(congelado!.riskScore!).toBeGreaterThanOrEqual(0);
      expect(congelado!.riskScore!).toBeLessThanOrEqual(100);
      // Inmutable: reintentar Submit se rechaza y el score no cambia.
      const reSubmit = await accionPromocion(request, id, 'submit', operador);
      await esperarEstado(reSubmit, [400, 409]);
      expect((await obtenerPromocion(request, id))?.riskScore).toBe(congelado!.riskScore);
    },
  );

  invariante(
    { id: 'INV-RMS3', contexto: CTX, descripcion: 'Elegibilidad fail-closed: sin RoleMaturityStatus del objetivo, la promoción se rechaza (no avanza a aprobación)', referencia: 'ConfirmRolePromotionEligibilityCommandHandler.cs (INV-RPR4, FR-062)' },
    async ({ request }) => {
      // Objetivo recién provisionado ⇒ no tiene RoleMaturityStatus ⇒ no elegible (fail-closed).
      const { id } = await nuevaPromocionDraft(request);
      const operador = await identidadConPermisos(request, { nueva: true });
      await esperarEstado(await accionPromocion(request, id, 'submit', operador), 204);
      await esperarEstado(await accionPromocion(request, id, 'confirm-eligibility', operador), 204);
      // La FSM NO avanza a PendingManagerApproval: queda Rejected.
      expect((await obtenerPromocion(request, id))?.status).toBe('Rejected');
    },
  );

  invariante(
    { id: 'INV-RPR3', contexto: CTX, descripcion: 'SoD en la creación: el solicitante no puede ser el usuario objetivo de la promoción', referencia: 'CreateRolePromotionRequestCommandHandler.cs (SoD, INV-RPR3)' },
    async ({ request }) => {
      // Una identidad REAL, usada como solicitante Y como objetivo: la violacion que esta prueba
      // busca es la de segregacion de funciones, no la de permisos. Con un `uid()` al azar el
      // guardia responde 403 antes de que la regla de SoD llegue a opinar, y el invariante medía
      // otra cosa (G-280).
      const identidad = await identidadConPermisos(request, { nueva: true });
      const mismoUsuario = identidad.userId!;
      const currentRoleId = await provisionarRolReal(request);
      const targetRoleId = await provisionarRolReal(request);
      // targetUserId == solicitante ⇒ violación de segregación de funciones.
      const { res } = await crearPromocionRol(request, {
        targetUserId: mismoUsuario,
        currentRoleId,
        targetRoleId,
        solicitante: { userId: mismoUsuario },
      });
      await esperarEstado(res, [400, 409]);
      expect(await codigoDeError(res)).toMatch(/segrega|segregation|sod/i);
    },
  );

  invariantePendiente(
    { id: 'INV-IGA-SOD3', contexto: CTX, descripcion: 'SoD 3-partes en promoción: aprobador ≠ objetivo ≠ auditor (manager/security/execute/verify)', referencia: 'RolePromotionRequest ManagerApprove/SecurityApprove/Execute/Verify (INV-RPR3 endurecida, ADR-UMS-096)', motivo: 'Alcanzar PendingManagerApproval/Approved exige un RoleMaturityStatus ELEGIBLE del objetivo, y no existe API de escritura para sembrarlo (solo lectura). La SoD de 3 partes está cubierta por pruebas unitarias del agregado; su verificación de caja negra requiere fixture elegible aislado.' },
    async ({ request }) => {
      const { id, targetUserId } = await nuevaPromocionDraft(request);
      await accionPromocion(request, id, 'submit');
      await accionPromocion(request, id, 'confirm-eligibility');
      // (Inalcanzable sin fixture elegible) el aprobador no puede ser el objetivo.
      const autoAprob = await accionPromocion(request, id, 'manager-approve', { userId: targetUserId });
      await esperarEstado(autoAprob, [400, 409]);
    },
  );
});

// ══════════════════════════ Delegación de gestión (FSM · SoD 2-partes · compuerta de aprobación · aislamiento) ══════════════════════════
test.describe('Delegation', () => {
  invariante(
    { id: 'INV-DEL2', contexto: CTX, descripcion: 'SoD 2-partes en Delegation: el administrador delegante no puede delegarse a sí mismo', referencia: 'UserManagementDelegation.Create (SoD, DelegatingAdminId != DelegatedAdminId)' },
    async ({ request }) => {
      const admin = await provisionarUsuarioInterno(request);
      // delegante == delegado ⇒ violación de segregación de funciones.
      const { res } = await crearDelegacion(request, { delegatingAdminId: admin, delegatedAdminId: admin });
      await esperarEstado(res, [400, 409]);
    },
  );

  invariante(
    { id: 'INV-DEL-FSM', contexto: CTX, descripcion: 'FSM auditada de Delegation: Draft→Active→Revoked; transición inválida rechazada', referencia: 'UserManagementDelegation FSM (activate/revoke; ADR-UMS-086)' },
    async ({ request }) => {
      const delegante = await provisionarUsuarioInterno(request);
      const delegado = await provisionarUsuarioInterno(request);
      const idDelegante = { userId: delegante };
      // requiresApproval=false ⇒ activable directamente.
      const { id, res } = await crearDelegacion(request, { delegatingAdminId: delegante, delegatedAdminId: delegado });
      await esperarEstado(res, 201);
      expect((await obtenerDelegacion(request, id, idDelegante))?.status).toBe('Draft');
      // Transición inválida: aprobar una delegación que no está PendingApproval → 4xx.
      await esperarEstado(await accionDelegacion(request, id, 'approve', idDelegante), [400, 409]);
      // Draft → Active.
      await esperarEstado(await accionDelegacion(request, id, 'activate', idDelegante), 204);
      expect((await obtenerDelegacion(request, id, idDelegante))?.status).toBe('Active');
      // Active → Revoked.
      await esperarEstado(await accionDelegacion(request, id, 'revoke', idDelegante, `reason=${encodeURIComponent('fin de la prueba')}`), 204);
      expect((await obtenerDelegacion(request, id, idDelegante))?.status).toBe('Revoked');
    },
  );

  invariante(
    { id: 'INV-DEL-APR', contexto: CTX, descripcion: 'Compuerta de aprobación de Delegation: RequiresApproval ⇒ Draft→PendingApproval→Approve→Active', referencia: 'SubmitDelegationForApproval / ApproveDelegation (ADR-UMS-086, G-148)' },
    async ({ request }) => {
      const delegante = await provisionarUsuarioInterno(request);
      const delegado = await provisionarUsuarioInterno(request);
      const idDelegante = { userId: delegante };
      const { id, res } = await crearDelegacion(request, { delegatingAdminId: delegante, delegatedAdminId: delegado, requiresApproval: true });
      await esperarEstado(res, 201);
      expect((await obtenerDelegacion(request, id, idDelegante))?.status).toBe('Draft');
      // Draft → PendingApproval.
      await esperarEstado(await accionDelegacion(request, id, 'submit-for-approval', idDelegante), 204);
      expect((await obtenerDelegacion(request, id, idDelegante))?.status).toBe('PendingApproval');
      // PendingApproval → Active: la aprobación exige un tercero (SoD: aprobador ≠ delegante).
      const aprobador = await identidadConPermisos(request, { nueva: true });
      await esperarEstado(await accionDelegacion(request, id, 'approve', aprobador), 204);
      expect((await obtenerDelegacion(request, id, idDelegante))?.status).toBe('Active');
    },
  );

  invariantePendiente(
    { id: 'INV-DEL-ISO', contexto: CTX, descripcion: 'Aislamiento por inquilino en las consultas de gobernanza (Delegations): un inquilino no ve las delegaciones de otro', referencia: 'GetAllDelegations (RLS por inquilino, G-041/G-022)', motivo: 'La identidad DevAuth por defecto es administrador interno (management-owner) y ve todos los inquilinos por diseño; probar el aislamiento cross-tenant exige una identidad de administrador de un inquilino CLIENT distinto, no aprovisionable de forma determinista en este arnés.' },
    async ({ request }) => {
      const res = await request.get(`/api/v1/delegations`, { headers: { 'X-User-Id': uid() } });
      expect(res.status()).toBe(200);
    },
  );
});
