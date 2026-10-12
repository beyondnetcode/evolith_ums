namespace Evolith.Ums.Infrastructure.Persistence.Seeders;

using System.Reflection;
using BeyondNetCode.Shell.Ddd;
using Evolith.Ums.Domain.Approvals;
using Evolith.Ums.Domain.Approvals.AccessEnforcementPolicy;
using Evolith.Ums.Domain.Approvals.ApprovalRequest;
using Evolith.Ums.Domain.Approvals.ApprovalWorkflow;
using Evolith.Ums.Domain.Approvals.DocumentType;
using Evolith.Ums.Domain.Approvals.NotificationRule;
using Evolith.Ums.Domain.Approvals.UserDocument;
using Evolith.Ums.Domain.Kernel.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using AccessEnforcementPolicyAggregate = Evolith.Ums.Domain.Approvals.AccessEnforcementPolicy.AccessEnforcementPolicy;
using ApprovalRequestAggregate = Evolith.Ums.Domain.Approvals.ApprovalRequest.ApprovalRequest;
using ApprovalWorkflowAggregate = Evolith.Ums.Domain.Approvals.ApprovalWorkflow.ApprovalWorkflow;
using DocumentTypeAggregate = Evolith.Ums.Domain.Approvals.DocumentType.DocumentType;
using NotificationRuleAggregate = Evolith.Ums.Domain.Approvals.NotificationRule.NotificationRule;
using UserDocumentAggregate = Evolith.Ums.Domain.Approvals.UserDocument.UserDocument;

public static class ApprovalsDevDataSeeder
{
    // Fixed IDs expected by integration tests (ApprovalRequestRestEndpointTests)
    public const string TestManualApprovalWorkflowId  = "88888888-1111-1111-1111-111111111111";
    public const string TestAutoApproveWorkflowId     = "88888888-2222-2222-2222-222222222222";
    public static async Task SeedAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        var wfRepository = serviceProvider.GetService<IApprovalWorkflowRepository>();
        var inMemoryWfRepository = serviceProvider.GetService<InMemoryApprovalWorkflowRepository>();

        var reqRepository = serviceProvider.GetService<IApprovalRequestRepository>();
        var inMemoryReqRepository = serviceProvider.GetService<InMemoryApprovalRequestRepository>();

        var docTypeRepository = serviceProvider.GetService<IDocumentTypeRepository>();
        var inMemoryDocTypeRepository = serviceProvider.GetService<InMemoryDocumentTypeRepository>();

        var userDocRepository = serviceProvider.GetService<IUserDocumentRepository>();
        var inMemoryUserDocRepository = serviceProvider.GetService<InMemoryUserDocumentRepository>();

        var policyRepository = serviceProvider.GetService<IAccessEnforcementPolicyRepository>();
        var inMemoryPolicyRepository = serviceProvider.GetService<InMemoryAccessEnforcementPolicyRepository>();

        var notificationRepository = serviceProvider.GetService<INotificationRuleRepository>();
        var inMemoryNotificationRepository = serviceProvider.GetService<InMemoryNotificationRuleRepository>();

        var actor = ActorId.Create(CoreDevDataSeeder.SystemActorId);
        var ransaTenantId = TenantId.Load(Guid.Parse(CoreDevDataSeeder.RansaTenantId));
        var internalAdminTenantId = TenantId.Load(Guid.Parse(CoreDevDataSeeder.InternalAdminTenantId));
        var evolithTenantId = TenantId.Load(Guid.Parse(CoreDevDataSeeder.EvolithTenantId));
        var adminUserId = UserId.Load(Guid.Parse(CoreDevDataSeeder.RansaAdminUserId));
        var internalAdminUserId = UserId.Load(Guid.Parse(CoreDevDataSeeder.SuperAdminUserId));

        // Document Types
        var docTypes = BuildSeedDocumentTypes(ransaTenantId, actor);
        if (inMemoryDocTypeRepository is not null)
            foreach (var docType in docTypes) inMemoryDocTypeRepository.Seed(docType);
        else if (docTypeRepository is not null)
        {
            var existing = await docTypeRepository.GetByTenantIdAsync(ransaTenantId.GetValue(), cancellationToken);
            if (existing.Count == 0)
            {
                foreach (var docType in docTypes) await docTypeRepository.AddAsync(docType, cancellationToken);
                await docTypeRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
            }
        }

        // Workflows
        var workflows = BuildSeedWorkflows(ransaTenantId, docTypes, actor);
        if (inMemoryWfRepository is not null)
            foreach (var wf in workflows) inMemoryWfRepository.Seed(wf);
        else if (wfRepository is not null)
        {
            var existing = await wfRepository.GetByTenantIdAsync(ransaTenantId.GetValue(), cancellationToken);
            if (existing.Count == 0)
            {
                foreach (var wf in workflows) await wfRepository.AddAsync(wf, cancellationToken);
                await wfRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
            }
        }

        // Requests
        var requests = BuildSeedRequests(adminUserId, workflows, actor);
        if (inMemoryReqRepository is not null)
            foreach (var req in requests) inMemoryReqRepository.Seed(req);
        else if (reqRepository is not null)
        {
            var existing = await reqRepository.GetByTenantIdAsync(ransaTenantId.GetValue(), cancellationToken);
            if (existing.Count == 0)
            {
                foreach (var req in requests) await reqRepository.AddAsync(req, cancellationToken);
                await reqRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
            }
        }

        // Internal Admin inbox demo data
        var internalAdminWorkflows = BuildInternalAdminSeedWorkflows(internalAdminTenantId, actor);
        if (inMemoryWfRepository is not null)
            foreach (var wf in internalAdminWorkflows) inMemoryWfRepository.Seed(wf);
        else if (wfRepository is not null)
        {
            var existing = await wfRepository.GetByTenantIdAsync(internalAdminTenantId.GetValue(), cancellationToken);
            if (existing.Count == 0)
            {
                foreach (var wf in internalAdminWorkflows) await wfRepository.AddAsync(wf, cancellationToken);
                await wfRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
            }
        }

        var internalAdminRequests = BuildInternalAdminSeedRequests(internalAdminUserId, internalAdminWorkflows, actor);
        if (inMemoryReqRepository is not null)
            foreach (var req in internalAdminRequests) inMemoryReqRepository.Seed(req);
        else if (reqRepository is not null)
        {
            var existing = await reqRepository.GetByTenantIdAsync(internalAdminTenantId.GetValue(), cancellationToken);
            if (existing.Count == 0)
            {
                foreach (var req in internalAdminRequests) await reqRepository.AddAsync(req, cancellationToken);
                await reqRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
            }
        }

        // EVOLITH tenant workflows (FS-25 §4.5 / criterio 13): ALTA_CLIENTE y ACCESO_EXPEDIENTE
        var evolithWorkflows = BuildEvolithSeedWorkflows(evolithTenantId, actor);
        if (inMemoryWfRepository is not null)
            foreach (var wf in evolithWorkflows) inMemoryWfRepository.Seed(wf);
        else if (wfRepository is not null)
        {
            var existing = await wfRepository.GetByTenantIdAsync(evolithTenantId.GetValue(), cancellationToken);
            if (existing.Count == 0)
            {
                foreach (var wf in evolithWorkflows) await wfRepository.AddAsync(wf, cancellationToken);
                await wfRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
            }
        }

        // User Documents
        var userDocs = BuildSeedUserDocs(adminUserId, docTypes, actor);
        if (inMemoryUserDocRepository is not null)
            foreach (var doc in userDocs) inMemoryUserDocRepository.Seed(doc);
        else if (userDocRepository is not null)
        {
            var existing = await userDocRepository.GetByUserIdAsync(adminUserId.GetValue(), cancellationToken);
            if (existing.Count == 0)
            {
                foreach (var doc in userDocs) await userDocRepository.AddAsync(doc, cancellationToken);
                await userDocRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
            }
        }

        // Policies
        var policies = BuildSeedPolicies(ransaTenantId, actor);
        if (inMemoryPolicyRepository is not null)
            foreach (var pol in policies) inMemoryPolicyRepository.Seed(pol);
        else if (policyRepository is not null)
        {
            var existing = await policyRepository.GetByTenantIdAsync(ransaTenantId.GetValue(), cancellationToken);
            if (existing.Count == 0)
            {
                foreach (var pol in policies) await policyRepository.AddAsync(pol, cancellationToken);
                await policyRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
            }
        }

        // Notification Rules
        var rules = BuildSeedNotificationRules(ransaTenantId, actor);
        if (inMemoryNotificationRepository is not null)
            foreach (var rule in rules) inMemoryNotificationRepository.Seed(rule);
        else if (notificationRepository is not null)
        {
            var existing = await notificationRepository.GetByTenantIdAsync(ransaTenantId.GetValue(), cancellationToken);
            if (existing.Count == 0)
            {
                foreach (var rule in rules) await notificationRepository.AddAsync(rule, cancellationToken);
                await notificationRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
            }
        }
    }

    private static IReadOnlyList<ApprovalWorkflowAggregate> BuildInternalAdminSeedWorkflows(TenantId tenantId, ActorId actor)
    {
        var systemId = SystemSuiteId.Load(Guid.Parse(CoreDevDataSeeder.DemoSystemSuiteId));

        var workflow = ApprovalWorkflowAggregate.Create(
            tenantId,
            Code.Create("INTERNAL_ADMIN_REVIEW"),
            Name.Create("Internal Admin Review Workflow"),
            Description.Create("Workflow para aprobar solicitudes internas del portal UMS"),
            UserCategory.Internal,
            true,
            systemId,
            actor,
            requiredDocumentCount: 1);

        return workflow.IsSuccess ? new[] { workflow.Value } : Array.Empty<ApprovalWorkflowAggregate>();
    }

    // EVOLITH tenant approval flows (FS-25 §4.5): alta de empresa cliente y acceso externo a expediente.
    private static IReadOnlyList<ApprovalWorkflowAggregate> BuildEvolithSeedWorkflows(TenantId tenantId, ActorId actor)
    {
        var results = new List<ApprovalWorkflowAggregate>();

        var altaCliente = ApprovalWorkflowAggregate.Create(
            tenantId,
            Code.Create("ALTA_CLIENTE"),
            Name.Create("Alta de Empresa Cliente"),
            Description.Create("Aprobación del alta de una empresa cliente en el operador EVOLITH"),
            UserCategory.External,
            true,
            null,
            actor,
            requiredDocumentCount: 1);
        if (altaCliente.IsSuccess) results.Add(altaCliente.Value);

        var accesoExpediente = ApprovalWorkflowAggregate.Create(
            tenantId,
            Code.Create("ACCESO_EXPEDIENTE"),
            Name.Create("Acceso a Expediente"),
            Description.Create("Aprobación de acceso externo a un expediente del operador EVOLITH"),
            UserCategory.External,
            true,
            null,
            actor,
            requiredDocumentCount: 1);
        if (accesoExpediente.IsSuccess) results.Add(accesoExpediente.Value);

        return results;
    }

    private static IReadOnlyList<ApprovalRequestAggregate> BuildInternalAdminSeedRequests(
        UserId requesterId,
        IReadOnlyList<ApprovalWorkflowAggregate> workflows,
        ActorId actor)
    {
        if (workflows.Count == 0) return Array.Empty<ApprovalRequestAggregate>();

        var workflowId = workflows[0].GetId();
        var systemId   = SystemSuiteId.Load(Guid.Parse(CoreDevDataSeeder.DemoSystemSuiteId));
        var adminRole  = RoleId.Load(Guid.Parse(CoreDevDataSeeder.DemoAdminRoleId));

        var pending = ApprovalRequestAggregate.Create(
            workflowId,
            requesterId,
            null,
            systemId,
            null,
            adminRole,
            "Solicito revisión de accesos para el administrador interno.",
            actor);

        return pending.IsSuccess ? new[] { pending.Value } : Array.Empty<ApprovalRequestAggregate>();
    }

    // DocumentType.Create(TenantId, Code, Name, Description, DocumentCriticity, ActorId)
    private static IReadOnlyList<DocumentTypeAggregate> BuildSeedDocumentTypes(TenantId tenantId, ActorId actor)
    {
        var result = DocumentTypeAggregate.Create(
            tenantId,
            Code.Create("DNI"),
            Name.Create("Documento Nacional de Identidad"),
            Description.Create("Documento de identidad nacional peruano"),
            DocumentCriticity.High,
            actor);

        return result.IsSuccess ? new[] { result.Value } : Array.Empty<DocumentTypeAggregate>();
    }

    // ApprovalWorkflow.Create(TenantId, Code, Name, Description, UserCategory, bool requiresApproval, SystemSuiteId?, ActorId)
    private static IReadOnlyList<ApprovalWorkflowAggregate> BuildSeedWorkflows(TenantId tenantId, IReadOnlyList<DocumentTypeAggregate> docTypes, ActorId actor)
    {
        var results = new List<ApprovalWorkflowAggregate>();

        // Primary onboarding workflow (dynamic ID)
        var onboarding = ApprovalWorkflowAggregate.Create(
            tenantId,
            Code.Create("ONBOARDING"),
            Name.Create("Supplier Onboarding Workflow"),
            Description.Create("Workflow para onboarding de proveedores externos"),
            UserCategory.External,
            true,
            null,
            actor,
            requiredDocumentCount: 1);

        if (onboarding.IsSuccess)
        {
            if (docTypes.Count > 0)
                onboarding.Value.AddRequiredDocument(DocumentTypeId.Load(docTypes[0].Props.Id.GetValue()), true, actor);
            results.Add(onboarding.Value);
        }

        // Fixed-ID workflows required by ApprovalRequestRestEndpointTests
        results.Add(CreateWorkflowWithFixedId(
            Guid.Parse(TestManualApprovalWorkflowId),
            tenantId, "MANUAL_REVIEW", "Manual Review Workflow",
            "Requires explicit approver sign-off",
            UserCategory.Internal, requiresApproval: true, actor, requiredDocumentCount: 1));

        results.Add(CreateWorkflowWithFixedId(
            Guid.Parse(TestAutoApproveWorkflowId),
            tenantId, "AUTO_APPROVE", "Auto-Approve Workflow",
            "Approved immediately without human review",
            UserCategory.Internal, requiresApproval: false, actor));

        return results;
    }

    /// <summary>
    /// Creates an ApprovalWorkflow with a specific ID using reflection (same pattern as
    /// ApprovalsAggregateFactory.RehydrateWorkflow) so that integration tests can reference
    /// workflows by well-known IDs without relying on dynamic GUIDs.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Major Code Smell", "S1172:Unused method parameters should be removed",
        Justification = "'requiredDocumentCount' expresa la intención de datos-semilla (MANUAL_REVIEW exige 1 doc); " +
                        "ApprovalWorkflowProps aún no lo modela, se conserva para no perder la intención (ver G-016).")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Security Hotspot", "S3011:Reflection should not be used to increase accessibility of classes, methods, or fields",
        Justification = "ADR-UMS-099: reflexión sancionada en seeder de desarrollo/UAT. Instancia el agregado por su " +
                        "constructor privado para fijar un ID bien conocido; no se ejecuta en producción " +
                        "(SeedDevData && !IsProduction).")]
    private static ApprovalWorkflowAggregate CreateWorkflowWithFixedId(
        Guid fixedId,
        TenantId tenantId,
        string code,
        string name,
        string description,
        UserCategory category,
        bool requiresApproval,
        ActorId actor,
        int requiredDocumentCount = 0)
    {
        var props = new ApprovalWorkflowProps(
            IdValueObject.Load(fixedId),
            tenantId,
            systemSuiteId: null,
            Code.Create(code),
            Name.Create(name),
            Description.Create(description),
            category,
            requiresApproval,
            actor);

        // ApprovalWorkflow constructor is private — use reflection to instantiate
        var ctor = typeof(ApprovalWorkflowAggregate)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .First(c => c.GetParameters().Length == 1);

        var workflow = (ApprovalWorkflowAggregate)ctor.Invoke([props]);
        workflow.DomainEvents.MarkChangesAsCommitted();
        workflow.BrokenRules.Clear();
        return workflow;
    }

    private static IReadOnlyList<ApprovalRequestAggregate> BuildSeedRequests(UserId requesterId, IReadOnlyList<ApprovalWorkflowAggregate> wfs, ActorId actor)
    {
        if (wfs.Count == 0) return Array.Empty<ApprovalRequestAggregate>();

        var workflowId = wfs[0].GetId();
        var systemId   = SystemSuiteId.Load(Guid.Parse(CoreDevDataSeeder.DemoSystemSuiteId));
        var adminRole  = RoleId.Load(Guid.Parse(CoreDevDataSeeder.DemoAdminRoleId));
        var opRole     = RoleId.Load(Guid.Parse(CoreDevDataSeeder.DemoOperatorRoleId));

        // Ransa pending user (coordinador.flota) — DeriveGuid(3)
        var ransaTenantBytes = Guid.Parse(CoreDevDataSeeder.RansaTenantId).ToByteArray();
        ransaTenantBytes[0] = 3;
        var pendingUserId = UserId.Load(new Guid(ransaTenantBytes));

        var results = new List<ApprovalRequestAggregate>();

        // 1. Pending — pending user requests admin role, org-wide (shows in inbox)
        var pending1 = ApprovalRequestAggregate.Create(
            workflowId, pendingUserId, null,
            systemId, null, adminRole,
            "Necesito acceso para gestionar onboarding del equipo.",
            actor);
        if (pending1.IsSuccess) results.Add(pending1.Value);

        // 2. Pending — pending user requests operator role, org-wide (second request, different role)
        var pending2 = ApprovalRequestAggregate.Create(
            workflowId, pendingUserId, null,
            systemId, null, opRole,
            "Solicito perfil operativo para seguimiento de flota.",
            actor);
        if (pending2.IsSuccess) results.Add(pending2.Value);

        // 3. Approved — admin user, requested operator but granted admin (shows role modification)
        var approved1 = ApprovalRequestAggregate.Create(
            workflowId, requesterId,
            ProfileId.Load(Guid.Parse(CoreDevDataSeeder.DemoAdminProfileId)),
            systemId, null, opRole,
            "Acceso para reportes de operaciones.",
            actor);
        if (approved1.IsSuccess)
        {
            approved1.Value.Approve(actor, adminRole, "Rol elevado por política interna — perfil admin asignado.");
            results.Add(approved1.Value);
        }

        // 4. Approved — admin user, same role granted as requested
        var approved2 = ApprovalRequestAggregate.Create(
            workflowId, requesterId, null,
            systemId, null, opRole,
            "Acceso operativo módulo despacho.",
            actor);
        if (approved2.IsSuccess)
        {
            approved2.Value.Approve(actor, opRole);
            results.Add(approved2.Value);
        }

        // 5. Rejected — with explicit reason
        var rejected = ApprovalRequestAggregate.Create(
            workflowId, pendingUserId, null,
            systemId, null, adminRole,
            "Solicitud duplicada por error.",
            actor);
        if (rejected.IsSuccess)
        {
            rejected.Value.Reject(actor, "Solicitud duplicada. El usuario ya tiene una solicitud pendiente para este scope.");
            results.Add(rejected.Value);
        }

        return results;
    }

    // UserDocument.Upload(UserId, DocumentTypeId, DateTime issueDate, DateTime expiration, DocumentCriticity, TextValueObject fileStoragePath, string fileChecksum, ActorId)
    private static IReadOnlyList<UserDocumentAggregate> BuildSeedUserDocs(UserId userId, IReadOnlyList<DocumentTypeAggregate> docTypes, ActorId actor)
    {
        if (docTypes.Count == 0) return Array.Empty<UserDocumentAggregate>();

        var doc = UserDocumentAggregate.Upload(
            userId,
            DocumentTypeId.Load(docTypes[0].Props.Id.GetValue()),
            DateTime.UtcNow.AddMonths(-6),
            DateTime.UtcNow.AddYears(2),
            DocumentCriticity.High,
            TextValueObject.Create("https://storage.ransa.pe/docs/44556677.pdf"),
            "sha256-dev-placeholder",
            actor);

        return doc.IsSuccess ? new[] { doc.Value } : Array.Empty<UserDocumentAggregate>();
    }

    // AccessEnforcementPolicy.Create(TenantId, ProfileId?, RoleId?, AccessEnforcementAction, ActorId)
    private static IReadOnlyList<AccessEnforcementPolicyAggregate> BuildSeedPolicies(TenantId tenantId, ActorId actor)
    {
        var result = AccessEnforcementPolicyAggregate.Create(
            tenantId,
            null,
            null,
            AccessEnforcementAction.BlockUser,
            actor);

        return result.IsSuccess ? new[] { result.Value } : Array.Empty<AccessEnforcementPolicyAggregate>();
    }

    // NotificationRule.Create(TenantId, NotificationChannel, TextValueObject recipient, ActorId)
    private static IReadOnlyList<NotificationRuleAggregate> BuildSeedNotificationRules(TenantId tenantId, ActorId actor)
    {
        var result = NotificationRuleAggregate.Create(
            tenantId,
            NotificationChannel.Email,
            TextValueObject.Create("operaciones@ransa.pe"),
            actor);

        return result.IsSuccess ? new[] { result.Value } : Array.Empty<NotificationRuleAggregate>();
    }
}
