namespace Evolith.Ums.Domain.Events;

public abstract record UmsDomainEvent(Guid TenantId) : DomainEvent;

public sealed record TenantCreatedEvent(Guid TenantId, string Code, string Name) : UmsDomainEvent(TenantId);
public sealed record TenantUpdatedEvent(Guid TenantId) : UmsDomainEvent(TenantId);
public sealed record TenantSuspendedEvent(Guid TenantId) : UmsDomainEvent(TenantId);
public sealed record TenantActivatedEvent(Guid TenantId) : UmsDomainEvent(TenantId);
public sealed record TenantArchivedEvent(Guid TenantId) : UmsDomainEvent(TenantId);

public sealed record BranchCreatedEvent(Guid TenantId, Guid BranchId, string Code) : UmsDomainEvent(TenantId);
public sealed record BranchUpdatedEvent(Guid TenantId, Guid BranchId) : UmsDomainEvent(TenantId);
// ADR-0164 §2.1: `BranchRemovedEvent` desaparece con el borrado físico que anunciaba. Lo sustituye
// el cierre definitivo, que además lleva el código porque quien lo consuma necesita saber QUÉ código
// queda ocupado para siempre y no puede resolverlo releyendo una fila que ya no debe listarse.
public sealed record BranchClosedEvent(Guid TenantId, Guid BranchId, string Code) : UmsDomainEvent(TenantId);
public sealed record BranchDeactivatedEvent(Guid TenantId, Guid BranchId) : UmsDomainEvent(TenantId);
public sealed record BranchReactivatedEvent(Guid TenantId, Guid BranchId) : UmsDomainEvent(TenantId);

public sealed record IdentityProviderRegisteredEvent(Guid TenantId, Guid IdentityProviderId, string Code, string Strategy) : UmsDomainEvent(TenantId);
public sealed record IdentityProviderActivatedEvent(Guid TenantId, Guid IdentityProviderId, string Code) : UmsDomainEvent(TenantId);
public sealed record IdentityProviderDeactivatedEvent(Guid TenantId, Guid IdentityProviderId, string Code) : UmsDomainEvent(TenantId);
public sealed record IdentityProviderRemovedEvent(Guid TenantId, Guid IdentityProviderId) : UmsDomainEvent(TenantId);


public sealed record TenantParameterCreatedEvent(Guid TenantId, Guid ParameterId, string Code, string Category) : UmsDomainEvent(TenantId);
public sealed record TenantParameterUpdatedEvent(Guid TenantId, Guid ParameterId, string Code, string OldValue, string NewValue) : UmsDomainEvent(TenantId);
public sealed record TenantParameterDeactivatedEvent(Guid TenantId, Guid ParameterId, string Code) : UmsDomainEvent(TenantId);
public sealed record TenantParameterReactivatedEvent(Guid TenantId, Guid ParameterId, string Code) : UmsDomainEvent(TenantId);
/// <summary>Borrado LÓGICO de un parámetro de inquilino: la fila permanece, deja de ser visible.</summary>
public sealed record TenantParameterDeletedEvent(Guid TenantId, Guid ParameterId, string Code) : UmsDomainEvent(TenantId);

public sealed record UserRegisteredEvent(Guid UserId, Guid TenantId, Guid? BranchId, string Category, string? IdentityReference) : UmsDomainEvent(TenantId);
public sealed record UserActivatedEvent(Guid UserId, Guid TenantId) : UmsDomainEvent(TenantId);
// G-202: llevan `Subject` ademas del id. El token semantico de cliente NO porta identificadores
// internos --su `sub` es el nombre de usuario-- y la lista de revocacion se indexaba solo por id,
// de modo que bloquear o eliminar una cuenta NO invalidaba su token de satelite hasta que caducaba.
// El sujeto tiene que viajar EN EL EVENTO y no consultarse despues: el borrado anonimiza los datos
// personales, asi que para cuando el manejador corre ya no hay a quien preguntar.
public sealed record UserBlockedEvent(Guid UserId, Guid TenantId, string Reason, string? Subject = null) : UmsDomainEvent(TenantId);
public sealed record UserRestoredEvent(Guid UserId, Guid TenantId) : UmsDomainEvent(TenantId);
// EP-09: raised when a tenant admin explicitly denies a pending user signup request
public sealed record UserSignupDeniedEvent(Guid UserId, Guid TenantId, string? Reason) : UmsDomainEvent(TenantId);
// REC-16: GDPR — raised when a user account is soft-deleted and PII is scheduled for anonymization
public sealed record UserDeletedEvent(Guid UserId, Guid TenantId, string? Subject = null) : UmsDomainEvent(TenantId);
public sealed record MfaEnrolledEvent(Guid UserId, Guid TenantId, string Method) : UmsDomainEvent(TenantId);
public sealed record MfaVerifiedEvent(Guid UserId, Guid TenantId, string Method) : UmsDomainEvent(TenantId);
public sealed record MfaEnrollmentRevokedEvent(Guid UserId, Guid TenantId, Guid EnrollmentId, string Method) : UmsDomainEvent(TenantId);
public sealed record ValidityPeriodModifiedEvent(Guid UserId, Guid TenantId, DateTimeOffset? PreviousExpiresAt, DateTimeOffset NewExpiresAt) : UmsDomainEvent(TenantId);
public sealed record AuthenticationAttemptedEvent(Guid? UserId, Guid TenantId, bool Success, string Reason, string IpAddress) : UmsDomainEvent(TenantId);

public abstract record AuthorizationDomainEvent : DomainEvent;

public sealed record SystemSuiteRegisteredEvent(Guid SystemSuiteId, Guid TenantId, string Code) : AuthorizationDomainEvent;
public sealed record SystemSuiteStatusChangedEvent(Guid SystemSuiteId, string Status) : AuthorizationDomainEvent;
/// <summary>El sistema se eliminó lógicamente (G-246). La fila sigue en la base; deja de leerse.</summary>
public sealed record SystemSuiteDeletedEvent(Guid SystemSuiteId, Guid TenantId, string Code) : AuthorizationDomainEvent;
public sealed record SystemSuiteModuleAddedEvent(Guid SystemSuiteId, Guid ModuleId, string Code) : AuthorizationDomainEvent;
public sealed record SystemSuiteModuleRemovedEvent(Guid SystemSuiteId, Guid ModuleId) : AuthorizationDomainEvent;
public sealed record SystemSuiteModuleStatusChangedEvent(Guid SystemSuiteId, Guid ModuleId, string NewStatus) : AuthorizationDomainEvent;
public sealed record SystemSuiteActionRegisteredEvent(Guid SystemSuiteId, string ActionCode) : AuthorizationDomainEvent;
public sealed record SystemSuiteActionRemovedEvent(Guid SystemSuiteId, string ActionCode) : AuthorizationDomainEvent;

public sealed record PermissionTemplateCreatedEvent(Guid TemplateId, Guid TenantId, Guid RoleId, Guid SystemSuiteId, string Version) : AuthorizationDomainEvent;
public sealed record PermissionTemplatePublishedEvent(Guid TemplateId, string Version) : AuthorizationDomainEvent;
public sealed record PermissionTemplateMutatedEvent(Guid TemplateId, string Version) : AuthorizationDomainEvent;
public sealed record PermissionTemplateDeprecatedEvent(Guid TemplateId, string Version) : AuthorizationDomainEvent;
public sealed record PermissionTemplateDeletedEvent(Guid TemplateId, string Version) : AuthorizationDomainEvent;

public sealed record ProfileCreatedEvent(Guid ProfileId, Guid TenantId, Guid UserId, Guid RoleId, Guid? BranchId) : AuthorizationDomainEvent;
public sealed record TemplateLinkedToProfileEvent(Guid ProfileId, Guid TemplateId) : AuthorizationDomainEvent;

/// <summary>
/// Una plantilla deja de conceder en un perfil (ADR-UMS-106). Se emite tanto cuando un operador la
/// desliga como cuando una version nueva de su misma linea la supersede.
/// </summary>
public sealed record TemplateUnlinkedFromProfileEvent(Guid ProfileId, Guid TemplateId) : AuthorizationDomainEvent;
public sealed record PermissionOverriddenEvent(Guid ProfileId, Guid PermissionId, string Effect) : AuthorizationDomainEvent;
public sealed record PermissionStatusChangedEvent(Guid ProfileId, Guid PermissionId, string Status) : AuthorizationDomainEvent;
public sealed record ProfileDeactivatedEvent(Guid ProfileId) : AuthorizationDomainEvent;
public sealed record ProfileActivatedEvent(Guid ProfileId) : AuthorizationDomainEvent;
// ADR-UMS-096: efecto real de la promoción de rol IGA — reasignación del rol del perfil (Autorización).
public sealed record ProfileRoleChangedEvent(Guid ProfileId, Guid TenantId, Guid UserId, Guid PreviousRoleId, Guid NewRoleId) : AuthorizationDomainEvent;

public sealed record AssignmentRuleCreatedEvent(Guid RuleId, Guid TenantId, Guid TemplateId, Guid RoleId, int Priority) : AuthorizationDomainEvent;
public sealed record AssignmentRuleDeactivatedEvent(Guid RuleId) : AuthorizationDomainEvent;
public sealed record AssignmentRuleReactivatedEvent(Guid RuleId) : AuthorizationDomainEvent;
public sealed record TemplateAutoAssignedEvent(Guid ProfileId, Guid TemplateId, Guid RuleId) : AuthorizationDomainEvent;

public sealed record RoleCreatedEvent(Guid RoleId, Guid TenantId, Guid SystemSuiteId, string Code, Guid? ParentRoleId) : AuthorizationDomainEvent;
public sealed record RoleUpdatedEvent(Guid RoleId) : AuthorizationDomainEvent;
public sealed record RoleActivatedEvent(Guid RoleId) : AuthorizationDomainEvent;
public sealed record RoleDeactivatedEvent(Guid RoleId) : AuthorizationDomainEvent;
public sealed record RoleActionGrantedEvent(Guid RoleId, string ActionCode) : AuthorizationDomainEvent;
public sealed record RoleActionRevokedEvent(Guid RoleId, string ActionCode) : AuthorizationDomainEvent;

public abstract record ConfigurationDomainEvent : DomainEvent;

public sealed record IdpConfigRegisteredEvent(Guid ConfigId, Guid TenantId, string ProviderType, int Version) : ConfigurationDomainEvent;
public sealed record IdpConfigActivatedEvent(Guid ConfigId, Guid TenantId) : ConfigurationDomainEvent;
public sealed record IdpConfigUpdatedEvent(Guid ConfigId, Guid TenantId, int Version) : ConfigurationDomainEvent;
public sealed record IdpConfigDeactivatedEvent(Guid ConfigId, Guid TenantId) : ConfigurationDomainEvent;

public sealed record AppConfigCreatedEvent(Guid ConfigId, string Scope, string Code, string Version) : ConfigurationDomainEvent;
public sealed record AppConfigPublishedEvent(Guid ConfigId, string Code, string Version) : ConfigurationDomainEvent;
public sealed record AppConfigArchivedEvent(Guid ConfigId, string Code) : ConfigurationDomainEvent;
/// <summary>Borrado LÓGICO de una configuración: la fila permanece, el estado pasa a Deleted.</summary>
public sealed record AppConfigDeletedEvent(Guid ConfigId, string Code) : ConfigurationDomainEvent;
public sealed record AppConfigUpdatedEvent(Guid ConfigId, string Code, string NewVersion) : ConfigurationDomainEvent;

public sealed record FeatureFlagCreatedEvent(Guid FlagId, string FlagCode, string Type, Guid SystemSuiteId) : ConfigurationDomainEvent;
public sealed record FeatureFlagActivatedEvent(Guid FlagId, string FlagCode) : ConfigurationDomainEvent;
public sealed record FeatureFlagDeactivatedEvent(Guid FlagId, string FlagCode) : ConfigurationDomainEvent;
public sealed record FeatureFlagArchivedEvent(Guid FlagId, string FlagCode) : ConfigurationDomainEvent;
public sealed record FeatureFlagStateChangedEvent(Guid FlagId, string FlagCode, string NewStatus) : ConfigurationDomainEvent;
public sealed record FlagEvaluatedEvent(Guid FlagId, string FlagCode, bool Result, string Context) : ConfigurationDomainEvent;
public sealed record FeatureFlagTargetingRulesUpdatedEvent(Guid FlagId, string FlagCode, Guid SystemSuiteId) : ConfigurationDomainEvent;
public sealed record FeatureFlagCriteriaAddedEvent(Guid FlagId, string FlagCode, string CriteriaType) : ConfigurationDomainEvent;
public sealed record FeatureFlagCriteriaRemovedEvent(Guid FlagId, string FlagCode, Guid CriteriaId) : ConfigurationDomainEvent;

public abstract record ComplianceDomainEvent : DomainEvent;

public sealed record DocumentTypeRegisteredEvent(Guid DocumentTypeId, string Criticity, Guid TenantId) : ComplianceDomainEvent;

public sealed record DocumentUploadedEvent(Guid DocumentId, Guid UserId, Guid DocumentTypeId, DateTime ExpirationDate) : ComplianceDomainEvent;
public sealed record DocumentValidatedEvent(Guid DocumentId, Guid UserId, string ValidatedBy) : ComplianceDomainEvent;
public sealed record DocumentRejectedEvent(Guid DocumentId, Guid UserId, string RejectionReason) : ComplianceDomainEvent;
public sealed record DocumentExpiredEvent(Guid DocumentId, Guid UserId, Guid DocumentTypeId, string Criticity) : ComplianceDomainEvent;
public sealed record DocumentNearExpirationEvent(Guid DocumentId, Guid UserId, Guid DocumentTypeId, int DaysRemaining, int Step) : ComplianceDomainEvent;
public sealed record EnforcementExecutedEvent(Guid DocumentId, Guid UserId, string Action, DateTime ExecutedAt) : ComplianceDomainEvent;

public abstract record ApprovalDomainEvent : DomainEvent;

public sealed record ApprovalRequestApprovedEvent(Guid RequestId, Guid WorkflowId, string ApprovedBy, DateTime ApprovedAt) : ApprovalDomainEvent;
public sealed record ApprovalRequestRejectedEvent(Guid RequestId, Guid WorkflowId, string RejectedBy, string Reason, DateTime RejectedAt) : ApprovalDomainEvent;
public sealed record ProfileAssignedToUserEvent(Guid TenantId, Guid RequestId, Guid UserId, Guid ProfileId, Guid GrantedRoleId, Guid? BranchId, string AssignedBy, DateTime AssignedAt) : UmsDomainEvent(TenantId);
public sealed record ApprovalRequestCancelledEvent(Guid RequestId, Guid WorkflowId, string CancelledBy, string Reason, DateTime CancelledAt) : ApprovalDomainEvent;

public sealed record ApprovalWorkflowCreatedEvent(Guid WorkflowId, Guid TenantId, string Code, string Name, string TargetUserCategory) : ApprovalDomainEvent;
public sealed record ApprovalWorkflowDocumentAddedEvent(Guid WorkflowId, Guid DocumentTypeId, bool IsRequired) : ApprovalDomainEvent;
public sealed record ApprovalWorkflowDocumentRemovedEvent(Guid WorkflowId, Guid DocumentTypeId) : ApprovalDomainEvent;
public sealed record ApprovalWorkflowActivatedEvent(Guid WorkflowId) : ApprovalDomainEvent;
public sealed record ApprovalWorkflowDeactivatedEvent(Guid WorkflowId) : ApprovalDomainEvent;

public sealed record DocumentTypeUpdatedEvent(Guid DocumentTypeId, string OldName, string NewName, string UpdatedBy) : ComplianceDomainEvent;

public abstract record IdentityDelegationDomainEvent(Guid TenantId) : UmsDomainEvent(TenantId);
public sealed record DelegationCreatedEvent(Guid DelegationId, Guid TenantId, Guid DelegatingAdminId, Guid DelegatedAdminId, string ScopeType, string AllowedActions) : IdentityDelegationDomainEvent(TenantId);
public sealed record DelegationActivatedEvent(Guid DelegationId, Guid TenantId, DateTimeOffset ValidFrom, DateTimeOffset ValidUntil) : IdentityDelegationDomainEvent(TenantId);
public sealed record DelegationRevokedEvent(Guid DelegationId, Guid TenantId, Guid RevokedBy, string Reason) : IdentityDelegationDomainEvent(TenantId);
public sealed record DelegationExpiredEvent(Guid DelegationId, Guid TenantId, DateTimeOffset ExpiredAt) : IdentityDelegationDomainEvent(TenantId);
public sealed record DelegationRejectedEvent(Guid DelegationId, Guid TenantId, string Reason) : IdentityDelegationDomainEvent(TenantId);
public sealed record DelegationArchivedEvent(Guid DelegationId, Guid TenantId, string PreviousStatus) : IdentityDelegationDomainEvent(TenantId);

// ── Contexto acotado IGA (ADR-UMS-093) ─────────────────────────────────────────
public abstract record IgaDomainEvent(Guid TenantId) : UmsDomainEvent(TenantId);

// RoleMaturityStatus — agregado de elegibilidad (INV-RMS1..3)
public sealed record RoleMaturityStatusCreatedEvent(Guid TenantId, Guid StatusId, Guid UserId, Guid RoleId, string CurrentLevel) : IgaDomainEvent(TenantId);
public sealed record RoleMaturityLevelChangedEvent(Guid TenantId, Guid StatusId, string FromLevel, string ToLevel) : IgaDomainEvent(TenantId);
public sealed record RoleMaturityPerformanceScoreUpdatedEvent(Guid TenantId, Guid StatusId, decimal PreviousScore, decimal NewScore) : IgaDomainEvent(TenantId);
public sealed record RoleMaturityComplianceIssueMarkedEvent(Guid TenantId, Guid StatusId, string BlockingFactor) : IgaDomainEvent(TenantId);
public sealed record RoleMaturityComplianceIssueResolvedEvent(Guid TenantId, Guid StatusId) : IgaDomainEvent(TenantId);
public sealed record RoleMaturityEligibilityConfirmedEvent(Guid TenantId, Guid StatusId, Guid UserId, string CurrentLevel, string NextEligibleLevel, DateTime EligibleAt) : IgaDomainEvent(TenantId);

// RolePromotionRequest — máquina de estados de promoción (INV-RPR1..5)
public sealed record RolePromotionRequestedEvent(Guid TenantId, Guid RequestId, Guid TargetUserId, Guid RequesterId, Guid CurrentRoleId, Guid TargetRoleId) : IgaDomainEvent(TenantId);
public sealed record RolePromotionSubmittedEvent(Guid TenantId, Guid RequestId, int RiskScore) : IgaDomainEvent(TenantId);
public sealed record RolePromotionEligibilityConfirmedEvent(Guid TenantId, Guid RequestId) : IgaDomainEvent(TenantId);
public sealed record RolePromotionManagerApprovedEvent(Guid TenantId, Guid RequestId, Guid ApproverId, int RiskScore, string NextState) : IgaDomainEvent(TenantId);
public sealed record RolePromotionSecurityApprovedEvent(Guid TenantId, Guid RequestId, Guid SecurityReviewerId) : IgaDomainEvent(TenantId);
// ADR-UMS-096: evento autocontenido — lleva objetivo y roles origen/destino para que el paso de
// aplicación (post-commit, D-016) reasigne el Profile del usuario objetivo sin recargar la solicitud.
public sealed record RolePromotionExecutedEvent(Guid TenantId, Guid RequestId, Guid TargetUserId, Guid CurrentRoleId, Guid TargetRoleId, Guid ExecutorId) : IgaDomainEvent(TenantId);
public sealed record RolePromotionVerifiedEvent(Guid TenantId, Guid RequestId, Guid VerifierId) : IgaDomainEvent(TenantId);
public sealed record RolePromotionRejectedEvent(Guid TenantId, Guid RequestId, string Stage, string Reason) : IgaDomainEvent(TenantId);
public sealed record RolePromotionCancelledEvent(Guid TenantId, Guid RequestId, string Reason) : IgaDomainEvent(TenantId);
