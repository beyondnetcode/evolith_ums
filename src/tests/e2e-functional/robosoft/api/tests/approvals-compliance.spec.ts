// Contexto acotado «approvals-compliance» — carril B (API, caja negra) · G-112
//
// Re-verifica contra el sistema VIVO las 20 invariantes del PRD del contexto de aprobaciones y
// cumplimiento documental. Fuente: reference/qa/bmad-tester-robosoft-audit-2026-07-16.md.
// Varias invariantes marcadas FAIL en julio ya fueron corregidas (G-051, G-117, G-118, G-119,
// G-120); este arnés lo confirma ejerciendo el API real. Cada test auto-aprovisiona su data con
// ids/códigos únicos por corrida → determinista e idempotente.
import { test, expect } from '@playwright/test';
import { invariante, esperarEstado, codigoDeError } from '../helpers/invariant';
import {
  identidadConPermisos,
  uid,
  provisionarUsuarioInterno,
  provisionarRolReal,
  crearTipoDocumento,
  subirDocumentoUsuario,
  validarDocumento,
  rechazarDocumento,
  expirarDocumento,
  reSubirDocumento,
  estadoDocumento,
  crearWorkflow,
  agregarDocumentoRequerido,
  quitarDocumentoRequerido,
  obtenerWorkflow,
  crearSolicitud,
  aprobarSolicitud,
  rechazarSolicitud,
  listarSolicitudesDeUsuario,
  crearPolitica,
  desactivarPolitica,
  obtenerPolitica,
} from '../helpers/provision';

const CTX = 'approvals-compliance';

// ══════════════════════════ ApprovalRequest (INV-AR1..AR4) ══════════════════════════
test.describe('ApprovalRequest', () => {
  invariante(
    { id: 'INV-AR1', contexto: CTX, descripcion: 'Approve/Reject solo desde Pending (transición irreversible)', referencia: 'ApprovalRequest.cs Approve/Reject (guard RequestNotPending)' },
    async ({ request }) => {
      const workflowId = await crearWorkflow(request); // requiresApproval=true por defecto: la solicitud queda Pending hasta la aprobacion humana
      const { id, roleId, res } = await crearSolicitud(request, {
        workflowId,
        targetUserId: await provisionarUsuarioInterno(request),
        requestedRoleId: await provisionarRolReal(request), // G-160: el rol concedido debe existir
        solicitante: await identidadConPermisos(request, { nueva: true }),
      });
      await esperarEstado(res, 201);
      // Aprobar (aprobador = identidad DevAuth por defecto, distinta del solicitante → sin SoD).
      await esperarEstado(await aprobarSolicitud(request, id, roleId), 204);
      // Re-aprobar una solicitud ya Approved debe bloquearse (no vuelve a Pending).
      const reAprobar = await aprobarSolicitud(request, id, roleId);
      await esperarEstado(reAprobar, [400, 409]);
      expect(await codigoDeError(reAprobar)).toMatch(/request_not_pending/i);
    },
  );

  invariante(
    { id: 'INV-AR2', contexto: CTX, descripcion: 'Decisión terminal: no se puede re-decidir una request Approved/Rejected', referencia: 'ApprovalRequest.cs guard RequestNotPending' },
    async ({ request }) => {
      const workflowId = await crearWorkflow(request); // requiresApproval=true por defecto: la solicitud queda Pending hasta la aprobacion humana
      const { id, roleId } = await crearSolicitud(request, {
        workflowId,
        targetUserId: await provisionarUsuarioInterno(request),
        requestedRoleId: await provisionarRolReal(request), // G-160: el rol concedido debe existir
        solicitante: await identidadConPermisos(request, { nueva: true }),
      });
      await esperarEstado(await aprobarSolicitud(request, id, roleId), 204);
      // Ya Approved: tanto rechazar como re-aprobar quedan bloqueados (decisión terminal).
      // Se asevera por código de respuesta (la lista /approval-requests no filtra por id — bug de
      // contrato — y crece sin límite, por lo que leer el estado desde ella no es determinista).
      const rechazo = await rechazarSolicitud(request, id);
      await esperarEstado(rechazo, [400, 409]);
      expect(await codigoDeError(rechazo)).toMatch(/request_not_pending/i);
      const reAprob = await aprobarSolicitud(request, id, roleId);
      await esperarEstado(reAprob, [400, 409]);
      expect(await codigoDeError(reAprob)).toMatch(/request_not_pending/i);
    },
  );

  invariante(
    { id: 'INV-AR3', contexto: CTX, descripcion: 'Segregación de deberes: el solicitante (createdBy) NO puede aprobar su propia request', referencia: 'ApproveRequestCommandHandler.cs (G-119, SelfApprovalNotAllowed)' },
    async ({ request }) => {
      const workflowId = await crearWorkflow(request);
      const solicitante = await identidadConPermisos(request, { nueva: true });
      const { id, roleId } = await crearSolicitud(request, {
        workflowId,
        targetUserId: await provisionarUsuarioInterno(request),
        requestedRoleId: await provisionarRolReal(request), // G-160: el rol concedido debe existir
        solicitante,
      });
      // El MISMO usuario que creó intenta aprobar → debe rechazarse por SoD.
      const autoAprob = await aprobarSolicitud(request, id, roleId, solicitante);
      await esperarEstado(autoAprob, [400, 409]);
      expect(await codigoDeError(autoAprob)).toMatch(/self_approval_not_allowed/i);
      // La solicitud NO fue consumida: sigue aprobable por un tercero (aprobador ≠ solicitante) → 204.
      await esperarEstado(await aprobarSolicitud(request, id, roleId), 204);
    },
  );

  invariante(
    { id: 'INV-AR4', contexto: CTX, descripcion: 'Flujo de aprobación (happy-path) completa Pending→Approved', referencia: 'ApproveRequestCommandHandler.cs (G-117, ExecutionStrategy)' },
    async ({ request }) => {
      const workflowId = await crearWorkflow(request); // requiresApproval=true por defecto: la solicitud queda Pending hasta la aprobacion humana
      const { id, roleId, res } = await crearSolicitud(request, {
        workflowId,
        targetUserId: await provisionarUsuarioInterno(request),
        requestedRoleId: await provisionarRolReal(request), // G-160: el rol concedido debe existir
        solicitante: await identidadConPermisos(request, { nueva: true }),
      });
      await esperarEstado(res, 201);
      // Recién creada está Pending: aprobar (aprobador ≠ solicitante) transiciona a Approved → 204
      // (el guard de Approve solo opera desde Pending). Que un segundo intento devuelva 409
      // RequestNotPending confirma el estado terminal Approved sin depender de la lista.
      await esperarEstado(await aprobarSolicitud(request, id, roleId), 204);
      const segundo = await aprobarSolicitud(request, id, roleId);
      await esperarEstado(segundo, [400, 409]);
      expect(await codigoDeError(segundo)).toMatch(/request_not_pending/i);
    },
  );

  invariante(
    { id: 'INV-AR5', contexto: CTX, descripcion: 'El filtro userId aísla las solicitudes por usuario objetivo (no fuga solicitudes ajenas)', referencia: 'GetAllApprovalRequestsQueryHandler.cs (G-159, filtro TargetUserId)' },
    async ({ request }) => {
      // Usuario objetivo recién provisionado con UNA sola solicitud a su nombre.
      const targetUserId = await provisionarUsuarioInterno(request);
      const workflowId = await crearWorkflow(request);
      const { res } = await crearSolicitud(request, {
        workflowId,
        targetUserId,
        requestedRoleId: await provisionarRolReal(request),
        solicitante: await identidadConPermisos(request, { nueva: true }),
      });
      await esperarEstado(res, 201);
      // La lista filtrada por ese userId debe devolver SOLO sus solicitudes (antes fugaba todas
      // las del tenant: ~107 ítems ajenos). Se asevera aislamiento, no un conteo exacto.
      const { status, targetUserIds } = await listarSolicitudesDeUsuario(request, targetUserId);
      expect(status).toBe(200);
      expect(targetUserIds.length).toBeGreaterThanOrEqual(1);
      expect(targetUserIds.every((u) => u === targetUserId), 'la lista no debe contener solicitudes de otros usuarios').toBe(true);
    },
  );
});

// ══════════════════════════ ApprovalWorkflow (INV-WF1..WF5) ══════════════════════════
test.describe('ApprovalWorkflow', () => {
  invariante(
    { id: 'INV-WF1', contexto: CTX, descripcion: 'El workflow declara checklist de documentos requeridos (alta)', referencia: 'ApprovalWorkflow.AddRequiredDocument' },
    async ({ request }) => {
      const workflowId = await crearWorkflow(request, { requiresApproval: true });
      const dt = await crearTipoDocumento(request);
      await esperarEstado(await agregarDocumentoRequerido(request, workflowId, dt, true), 204);
    },
  );

  invariante(
    { id: 'INV-WF2', contexto: CTX, descripcion: 'El checklist de documentos requeridos es legible por el cliente', referencia: 'ApprovalWorkflowDto.RequiredDocuments (G-118)' },
    async ({ request }) => {
      const workflowId = await crearWorkflow(request, { requiresApproval: true });
      const dt = await crearTipoDocumento(request);
      await esperarEstado(await agregarDocumentoRequerido(request, workflowId, dt, true), 204);
      const wf = await obtenerWorkflow(request, workflowId);
      expect(wf?.requiredDocuments, 'el DTO debe proyectar requiredDocuments').toBeDefined();
      const doc = wf!.requiredDocuments!.find((d) => d.documentTypeId === dt);
      expect(doc, 'el documento requerido debe listarse con su requiredDocumentId').toBeTruthy();
      expect(doc!.requiredDocumentId).toBeTruthy();
      expect(doc!.isMandatory).toBe(true);
    },
  );

  invariante(
    { id: 'INV-WF3', contexto: CTX, descripcion: 'Un documento requerido puede eliminarse vía API (por requiredDocumentId)', referencia: 'RemoveRequiredDocument + FindRequiredDocument (G-118)' },
    async ({ request }) => {
      // requiresApproval=false: permite quedar sin documentos requeridos tras el borrado
      // (con requiresApproval=true, el dominio protege el último documento — regla correcta).
      const workflowId = await crearWorkflow(request, { requiresApproval: false });
      const dt = await crearTipoDocumento(request);
      await esperarEstado(await agregarDocumentoRequerido(request, workflowId, dt, true), 204);
      const wf = await obtenerWorkflow(request, workflowId);
      const requiredId = wf!.requiredDocuments!.find((d) => d.documentTypeId === dt)!.requiredDocumentId;
      await esperarEstado(await quitarDocumentoRequerido(request, workflowId, requiredId), 204);
      const wf2 = await obtenerWorkflow(request, workflowId);
      expect(wf2!.requiredDocuments?.some((d) => d.documentTypeId === dt)).toBeFalsy();
    },
  );

  invariante(
    { id: 'INV-WF4', contexto: CTX, descripcion: 'Alta duplicada de documento requerido se maneja limpiamente (Result Pattern, sin 500)', referencia: 'ApprovalWorkflow.AddRequiredDocument (DocumentTypeAlreadyRequired)' },
    async ({ request }) => {
      const workflowId = await crearWorkflow(request, { requiresApproval: true });
      const dt = await crearTipoDocumento(request);
      await esperarEstado(await agregarDocumentoRequerido(request, workflowId, dt, true), 204);
      const dup = await agregarDocumentoRequerido(request, workflowId, dt, true);
      await esperarEstado(dup, [400, 409]);
      expect(dup.status(), 'no debe ser 500').not.toBe(500);
      expect(await codigoDeError(dup)).toMatch(/document_type_already_required/i);
    },
  );

  invariante(
    { id: 'INV-WF5', contexto: CTX, descripcion: 'Documentos requeridos se exigen como precondición al aprobar (fail-closed)', referencia: 'ApproveRequestCommandHandler.EnsureRequiredDocumentsComplete (G-051 F4)' },
    async ({ request }) => {
      const workflowId = await crearWorkflow(request, { requiresApproval: true });
      const dt = await crearTipoDocumento(request, 'Critical');
      await esperarEstado(await agregarDocumentoRequerido(request, workflowId, dt, true), 204);
      // El usuario objetivo NO tiene documento Valid del tipo requerido (dt es nuevo por corrida).
      const { id, roleId } = await crearSolicitud(request, {
        workflowId,
        targetUserId: await provisionarUsuarioInterno(request),
        solicitante: await identidadConPermisos(request, { nueva: true }),
      });
      const aprob = await aprobarSolicitud(request, id, roleId);
      await esperarEstado(aprob, [400, 409]);
      expect(await codigoDeError(aprob)).toMatch(/required_documents_incomplete/i);
      // Fail-closed persistente: un segundo intento sigue bloqueado por el checklist incompleto,
      // lo que confirma que la solicitud NO progresó (sigue Pending) sin depender de la lista.
      const segundo = await aprobarSolicitud(request, id, roleId);
      await esperarEstado(segundo, [400, 409]);
      expect(await codigoDeError(segundo)).toMatch(/required_documents_incomplete/i);
    },
  );
});

// ══════════════════════════ UserDocument (INV-UD1..UD7) ══════════════════════════
test.describe('UserDocument', () => {
  invariante(
    { id: 'INV-UD1', contexto: CTX, descripcion: 'Upload inicia en PendingReview y exige ExpirationDate > IssueDate', referencia: 'UserDocument.Upload (INV-UD1)' },
    async ({ request }) => {
      const dt = await crearTipoDocumento(request);
      const ok = await subirDocumentoUsuario(request, { documentTypeId: dt });
      await esperarEstado(ok.res, 201);
      expect(await estadoDocumento(request, ok.userId, ok.id)).toBe('PENDING_REVIEW');
      // expiration <= issue → rechazo de validación.
      const malo = await subirDocumentoUsuario(request, {
        documentTypeId: dt,
        issueDate: new Date(Date.now() + 2 * 864e5).toISOString(),
        expirationDate: new Date(Date.now() + 864e5).toISOString(),
      });
      await esperarEstado(malo.res, 400);
    },
  );

  invariante(
    { id: 'INV-UD2', contexto: CTX, descripcion: 'PendingReview→Valid (validate); Valid es terminal para validate/reject', referencia: 'UserDocument.Validate' },
    async ({ request }) => {
      const dt = await crearTipoDocumento(request);
      const { id, userId } = await subirDocumentoUsuario(request, { documentTypeId: dt });
      await esperarEstado(await validarDocumento(request, id), 204);
      expect(await estadoDocumento(request, userId, id)).toBe('Valid');
      await esperarEstado(await validarDocumento(request, id), [400, 409]); // segunda validación
      await esperarEstado(await rechazarDocumento(request, id), [400, 409]); // reject sobre Valid
    },
  );

  invariante(
    { id: 'INV-UD3', contexto: CTX, descripcion: 'PendingReview→Rejected (reject)', referencia: 'UserDocument.Reject' },
    async ({ request }) => {
      const dt = await crearTipoDocumento(request);
      const { id, userId } = await subirDocumentoUsuario(request, { documentTypeId: dt });
      await esperarEstado(await rechazarDocumento(request, id, 'Firma no coincide'), 204);
      expect(await estadoDocumento(request, userId, id)).toBe('Rejected');
    },
  );

  invariante(
    { id: 'INV-UD4', contexto: CTX, descripcion: 'Expired solo se alcanza desde Valid (no desde PendingReview ni Rejected)', referencia: 'UserDocument.Expire (corregido; antes F3)' },
    async ({ request }) => {
      const dt = await crearTipoDocumento(request);
      // (a) expirar desde PendingReview → bloqueado.
      const pend = await subirDocumentoUsuario(request, { documentTypeId: dt });
      await esperarEstado(await expirarDocumento(request, pend.id), [400, 409]);
      expect(await estadoDocumento(request, pend.userId, pend.id)).toBe('PENDING_REVIEW');
      // (b) expirar desde Valid → permitido.
      const val = await subirDocumentoUsuario(request, { documentTypeId: dt });
      await esperarEstado(await validarDocumento(request, val.id), 204);
      await esperarEstado(await expirarDocumento(request, val.id), 204);
      expect(await estadoDocumento(request, val.userId, val.id)).toBe('Expired');
    },
  );

  invariante(
    { id: 'INV-UD5', contexto: CTX, descripcion: 'Expire es idempotente/terminal (no se re-expira)', referencia: 'DomainErrorStatusMapper DocumentAlreadyExpired→409' },
    async ({ request }) => {
      const dt = await crearTipoDocumento(request);
      const { id } = await subirDocumentoUsuario(request, { documentTypeId: dt });
      await esperarEstado(await validarDocumento(request, id), 204);
      await esperarEstado(await expirarDocumento(request, id), 204);
      await esperarEstado(await expirarDocumento(request, id), [400, 409]); // segundo expire
    },
  );

  invariante(
    { id: 'INV-UD6', contexto: CTX, descripcion: 'ReUpload solo desde Expired/Rejected, con nuevo checksum → vuelve a PendingReview', referencia: 'UserDocument.ReUpload' },
    async ({ request }) => {
      const dt = await crearTipoDocumento(request);
      const { id, userId } = await subirDocumentoUsuario(request, { documentTypeId: dt });
      await esperarEstado(await rechazarDocumento(request, id), 204);
      await esperarEstado(await reSubirDocumento(request, id), 204);
      expect(await estadoDocumento(request, userId, id)).toBe('PENDING_REVIEW');
    },
  );

  invariante(
    { id: 'INV-UD7', contexto: CTX, descripcion: 'Checksum obligatorio en upload', referencia: 'UploadUserDocumentCommandValidator FileChecksum NotEmpty' },
    async ({ request }) => {
      const dt = await crearTipoDocumento(request);
      const vacio = await subirDocumentoUsuario(request, { documentTypeId: dt, fileChecksum: '' });
      await esperarEstado(vacio.res, 400);
    },
  );
});

// ══════════════════════════ AccessEnforcementPolicy (INV-AEP1..AEP4) ══════════════════════════
test.describe('AccessEnforcementPolicy', () => {
  invariante(
    { id: 'INV-AEP1', contexto: CTX, descripcion: 'La política de enforcement exige profileId o roleId', referencia: 'AccessEnforcementPolicy.Create (PolicyRequiresProfileOrRole)' },
    async ({ request }) => {
      const conRol = await crearPolitica(request, { roleId: uid(), enforcementAction: 'RestrictProfile' });
      await esperarEstado(conRol.res, 201);
      const sinNada = await crearPolitica(request, { roleId: null, profileId: null, enforcementAction: 'BlockUser' });
      await esperarEstado(sinNada.res, 400);
    },
  );

  invariante(
    { id: 'INV-AEP2', contexto: CTX, descripcion: 'Acción de enforcement = bloqueo/degradación (BlockUser/RestrictProfile/LogOnly)', referencia: 'AccessEnforcementAction enum' },
    async ({ request }) => {
      for (const action of ['BlockUser', 'RestrictProfile', 'LogOnly'] as const) {
        const p = await crearPolitica(request, { roleId: uid(), enforcementAction: action });
        await esperarEstado(p.res, 201);
        const dto = await obtenerPolitica(request, p.id);
        expect(dto?.enforcementAction).toBe(action);
      }
    },
  );

  invariante(
    { id: 'INV-AEP3', contexto: CTX, descripcion: 'Política de documentos críticos con periodo de gracia', referencia: 'CreateAccessEnforcementPolicyCommand.GracePeriodDays (G-120 / FR-053)' },
    async ({ request }) => {
      const p = await crearPolitica(request, { roleId: uid(), enforcementAction: 'RestrictProfile', gracePeriodDays: 7 });
      await esperarEstado(p.res, 201);
      const dto = await obtenerPolitica(request, p.id);
      expect(dto, 'la política debe modelar un periodo de gracia').toBeDefined();
      expect(dto!.gracePeriodDays, 'gracePeriodDays debe persistir y ser legible').toBe(7);
    },
  );

  invariante(
    { id: 'INV-AEP4', contexto: CTX, descripcion: 'Deactivate es idempotente (segunda desactivación se bloquea)', referencia: 'DeactivateAccessEnforcementPolicy (PolicyAlreadyInactive)' },
    async ({ request }) => {
      const p = await crearPolitica(request, { roleId: uid(), enforcementAction: 'LogOnly' });
      await esperarEstado(p.res, 201);
      await esperarEstado(await desactivarPolitica(request, p.id), 204);
      await esperarEstado(await desactivarPolitica(request, p.id), [400, 409]);
    },
  );
});

// ══════════════════════════ Result Pattern transversal (INV-RP) ══════════════════════════
test.describe('ResultPattern', () => {
  invariante(
    { id: 'INV-RP', contexto: CTX, descripcion: 'Entradas de enum inválidas producen 4xx limpio (no 500) — Result Pattern', referencia: 'Validadores Upload/CreatePolicy/CreateDocumentType (antes F5)' },
    async ({ request }) => {
      // (a) upload con criticity inválida.
      const doc = await subirDocumentoUsuario(request, { documentTypeId: uid(), criticity: 'Bogus' as never });
      expect(doc.res.status(), 'enum inválido NO debe ser 500').not.toBe(500);
      await esperarEstado(doc.res, 400);
      // (b) create policy con enforcementAction inválida.
      const pol = await crearPolitica(request, { roleId: uid(), enforcementAction: 'Foo' as never });
      expect(pol.res.status(), 'enum inválido NO debe ser 500').not.toBe(500);
      await esperarEstado(pol.res, 400);
    },
  );
});
