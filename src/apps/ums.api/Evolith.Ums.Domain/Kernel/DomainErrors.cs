namespace Evolith.Ums.Domain.Kernel;

public static class DomainErrors
{
    public static class Common
    {
        public const string Required = "common.required";
        public const string Invalid = "common.invalid";
        public const string NotFound = "common.not_found";
        public const string Duplicate = "common.duplicate";
    }

    public static class Tenant
    {
        public const string Required = "user.tenant.required";
        public const string NotFound = "user.tenant.not_found";
        public const string SignupRequestNotPending = "tenant.signup_request_not_pending";
        public const string SignupRequestAlreadyProcessed = "tenant.signup_request_already_processed";
        public const string SignupRequestAlreadyExists = "tenant.signup_request_already_exists";
        public const string BranchCodeNotUnique = "tenant.branch_code_not_unique";
        public const string BranchNotFound = "tenant.branch_not_found";
        public const string BranchActive = "tenant.branch_active";
        // ADR-0164: cierre definitivo de sucursal (verbo terminal del ciclo de vida).
        /// <summary>La sucursal ya está cerrada: reintentar el cierre choca con su estado actual.</summary>
        public const string BranchAlreadyClosed = "tenant.branch_already_closed";
        /// <summary>La sucursal está cerrada y por eso no admite desactivarse ni reactivarse.</summary>
        public const string BranchClosed = "tenant.branch_closed";
        public const string ArchivedCannotSuspend = "tenant.archived_cannot_suspend";
        public const string ArchivedCannotActivate = "tenant.archived_cannot_activate";
        public const string AlreadyActive = "tenant.already_active";
        public const string AlreadySuspended = "tenant.already_suspended";
        public const string ArchivedCannotArchive = "tenant.archived_cannot_archive";
        // ── Dependency guard errors ──────────────────────────────────────────
        public const string HasActiveUsers      = "TENANT_HAS_ACTIVE_USERS";
        public const string HasActiveBranches   = "TENANT_HAS_ACTIVE_BRANCHES";
        public const string BranchHasActiveUsers = "BRANCH_HAS_ACTIVE_USERS";
        /// <summary>
        /// ADR-0164 §2.2: no se cierra una sucursal con referencias VIVAS. Es un código único aunque
        /// las referencias sean de dos clases (cuentas y perfiles) porque el desglose de QUÉ bloquea
        /// viaja aparte, en las <c>BlockingDependency</c>: así la respuesta nombra las dos a la vez
        /// en lugar de obligar a quien opera a descubrirlas de una en una.
        /// </summary>
        public const string BranchHasLiveReferences = "BRANCH_HAS_LIVE_REFERENCES";
        public const string HasActiveIdpConfig  = "TENANT_HAS_ACTIVE_IDP";
        public const string IdpCodeNotUnique = "tenant.idp_code_not_unique";
        public const string IdpNotFound = "tenant.idp_not_found";
        public const string IdpAlreadyActive = "tenant.idp_already_active";
        public const string IdpAlreadyInactive = "tenant.idp_already_inactive";
        public const string NoActiveIdp = "tenant.no_active_idp";
        // G-037/G-045: la propiedad de gestión (management owner) es única en todo el sistema.
        // Otorgarla a un segundo inquilino debe resolver 409 con este código estable y su
        // regla de negocio, no una violación de índice único que colapsa a 500.
        public const string ManagementOwnerAlreadyExists = "TENANT_OWNER_ALREADY_EXISTS";
    }

    public static class UserAccount
    {
        public const string InvalidEmail = "user_account.invalid_email";
        public const string CannotActivate = "user_account.cannot_activate";
        public const string AlreadyBlocked = "user_account.already_blocked";
        public const string CannotRestore = "user_account.cannot_restore";
        public const string BlockedCannotUpdateCredentials = "user_account.blocked_cannot_update_credentials";
        public const string FederatedCannotUseLocalPassword = "user_account.federated_cannot_use_local_password";
        public const string PasswordHashRequired = "user_account.password_hash_required";
        public const string BlockedCannotEnrollMfa = "user_account.blocked_cannot_enroll_mfa";
        public const string MfaAlreadyEnrolled = "user_account.mfa_already_enrolled";
        public const string MfaAlreadyVerified = "user_account.mfa_already_verified";
        public const string MfaEnrollmentNotFound = "user_account.mfa_enrollment_not_found";
        public const string ValidityPeriodExceedsMaximum = "user_account.validity_period_exceeds_maximum";
        public const string ValidityPeriodInPast = "user_account.validity_period_in_past";
        public const string InternalRequiresHrId = "user_account.internal_requires_hr_id";
        public const string EmailNotUnique = "user_account.email_not_unique";
        public const string CannotRemoveLastPassword = "user_account.cannot_remove_last_password";
        // REC-16: soft-delete + GDPR
        public const string AlreadyDeleted = "user_account.already_deleted";
        // EP-09: explicit denial of pending signup request
        public const string CannotDeny = "user_account.cannot_deny";
        // Una cuenta eliminada tiene el correo anonimizado (REC-16): escribirle datos personales
        // reintroduciría lo que el borrado se llevó a propósito.
        public const string CannotUpdateDeleted = "user_account.cannot_update_deleted";
        // ── Dependency guard errors ──────────────────────────────────────────
        public const string HasActiveProfiles   = "USER_HAS_ACTIVE_PROFILES";
    }

    public static class SystemSuite
    {
        public const string ConfigurationKeyTooLong = "system_suite.configuration_key_too_long";
        public const string ConfigurationValueTooLong = "system_suite.configuration_value_too_long";
        public const string ActionCodeTooLong = "system_suite.action_code_too_long";
        public const string OptionCodeNotUnique = "system_suite.option_code_not_unique";
        public const string SubMenuCodeNotUnique = "system_suite.submenu_code_not_unique";
        public const string MenuCodeNotUnique = "system_suite.menu_code_not_unique";
        public const string ModuleAlreadyActive = "system_suite.module_already_active";
        public const string ModuleAlreadyInactive = "system_suite.module_already_inactive";
        public const string ModuleInactiveCannotAddMenu = "system_suite.module_inactive_cannot_add_menu";
        public const string ModuleCodeNotUnique = "system_suite.module_code_not_unique";
        public const string RoleAlreadyActive = "system_suite.role_already_active";
        public const string RoleAlreadyInactive = "system_suite.role_already_inactive";
        public const string ActionAlreadyGranted = "system_suite.action_already_granted";
        public const string ActionNotGranted = "system_suite.action_not_granted";
        public const string ActionNotRegistered = "system_suite.action_not_registered";
        public const string ConfigurationKeyAlreadyExists = "system_suite.configuration_key_already_exists";
        public const string ConfigurationKeyNotFound = "system_suite.configuration_key_not_found";
        public const string ActionRequiresOwner = "system_suite.action_requires_owner";
        public const string ActionXorViolation = "system_suite.action_xor_violation";
    }

    public static class Authorization
    {
        public const string InvalidTemplateVersion = "authorization.invalid_template_version";
        public const string TemplateNotDraft = "authorization.template_not_draft";
        public const string TemplateNotPublished = "authorization.template_not_published";
        public const string TemplateAlreadyPublished = "authorization.template_already_published";
        public const string TemplateAlreadyDeprecated = "authorization.template_already_deprecated";
        public const string TemplateNotDeletable = "authorization.template_not_deletable";
        /// <summary>La plantilla ya está en el estado terminal Deleted: el borrado lógico no es repetible.</summary>
        public const string TemplateAlreadyDeleted = "authorization.template_already_deleted";
        public const string TemplateItemTargetAlreadyExists = "authorization.template_item_target_already_exists";
        /// <summary>
        /// Ya existe un ítem RETIRADO para esa terna (destino, tipo de destino, acción) en la plantilla.
        ///
        /// <para>Es un conflicto distinto de <see cref="TemplateItemTargetAlreadyExists"/> a propósito:
        /// la clave natural del ítem no se libera al retirarlo (ADR-0164 §2.3), así que el alta no puede
        /// proceder; pero el operador no está ante un duplicado sino ante una concesión suya que
        /// desactivó, y el verbo que la devuelve es reactivar, no volver a añadir. Un único mensaje para
        /// los dos casos le diría «ya existe» sobre algo que no ve en la lista de vigentes.</para>
        /// </summary>
        public const string TemplateItemTargetRetired = "authorization.template_item_target_retired";
        public const string InvalidPermissionEffect = "authorization.invalid_permission_effect";
        /// <summary>
        /// Ya existe un perfil ACTIVO del mismo usuario, rol y sucursal en el inquilino.
        ///
        /// <para>No existía, y nada lo impedía: cada llamada a `POST /profiles` con los mismos
        /// datos creaba otro perfil. Un aprovisionamiento reejecutado dejaba al usuario con dos
        /// perfiles del mismo rol, uno con permisos y otro vacío —porque la plantilla solo se
        /// asigna a uno—, y el selector de perfil le ofrecía ambos. Elegir el vacío es entrar a un
        /// sistema sin una sola concesión (G-215).</para>
        ///
        /// <para>Se restaura tras perderse en la resolución de conflictos del merge de la firma
        /// RS256 (PR #192): el fichero volvió a su versión anterior, pero el manejador que la usa
        /// no, así que `develop` dejó de compilar.</para>
        /// </summary>
        public const string ProfileAlreadyExistsForRole = "authorization.profile_already_exists_for_role";
        public const string ProfileAlreadyActive = "authorization.profile_already_active";
        public const string ProfileAlreadyInactive = "authorization.profile_already_inactive";
        public const string ProfileRoleUnchanged = "authorization.profile_role_unchanged"; // ADR-UMS-096: ChangeRole exige rol distinto
        public const string PermissionAlreadyExists = "authorization.permission_already_exists";
        public const string PermissionNotFound = "authorization.permission_not_found";
        public const string TemplateNotPublishedForProfile = "authorization.template_not_published_for_profile";
        public const string TemplateTenantMismatch = "authorization.template_tenant_mismatch";
        public const string TemplateRoleMismatch = "authorization.template_role_mismatch";
        public const string ProfileTemplateAlreadyLinked = "authorization.profile_template_already_linked";
        // ADR-UMS-106: desligar una plantilla que el perfil no tiene viva no es un no-op silencioso.
        public const string ProfileTemplateNotLinked = "authorization.profile_template_not_linked";
        public const string RoleAlreadyActive = "authorization.role_already_active";
        public const string RoleAlreadyInactive = "authorization.role_already_inactive";
        public const string RoleHierarchyLevelInvalid = "authorization.role_hierarchy_level_invalid";
        public const string RootRoleHierarchyLevelInvalid = "authorization.root_role_hierarchy_level_invalid";
        public const string ChildRoleHierarchyLevelInvalid = "authorization.child_role_hierarchy_level_invalid";
        public const string RolePromotionOrderInvalid = "authorization.role_promotion_order_invalid";
        public const string ActionAlreadyGranted = "authorization.action_already_granted";
        public const string ActionNotGranted = "authorization.action_not_granted";
        public const string DomainMethodRequiresParent   = "authorization.domain_method_requires_parent";
        public const string ParentResourceNotFound       = "authorization.parent_resource_not_found";
        public const string DomainMethodCannotBeParent   = "authorization.domain_method_cannot_be_parent";
        public const string AggregateRequiresChildEntity = "authorization.aggregate_requires_child_entity";
        // ── Dependency guard errors ──────────────────────────────────────────
        public const string RoleHasActiveProfiles        = "ROLE_HAS_ACTIVE_PROFILES";
        public const string RoleHasActiveChildRoles      = "ROLE_HAS_ACTIVE_CHILD_ROLES";
        public const string TemplateHasActiveProfiles    = "TEMPLATE_HAS_ACTIVE_PROFILES";
        public const string DomainResourceHasTemplateItems = "DOMAIN_RESOURCE_HAS_TEMPLATE_ITEMS";
        public const string ModuleHasActiveMenus         = "MODULE_HAS_ACTIVE_MENUS";
        // G-246: eliminación LÓGICA de un sistema. El borrado físico no existe: se hacen consultas
        // sobre datos antiguos y una fila borrada de verdad no se recupera.
        //   · NotDeprecated  → hay que archivar antes de eliminar; un sistema en servicio no se va
        //                      por un DELETE suelto.
        //   · HasDependents  → regla de cascada: algo VIVO todavía apunta al sistema. La respuesta
        //                      409 enumera qué, para que el llamador sepa qué eliminar primero.
        //   · AlreadyDeleted → ya está eliminado lógicamente; el estado es terminal e idempotente.
        public const string SystemSuiteNotDeprecated     = "SYSTEM_SUITE_NOT_DEPRECATED";
        public const string SystemSuiteHasDependents     = "SYSTEM_SUITE_HAS_DEPENDENTS";
        public const string SystemSuiteAlreadyDeleted    = "SYSTEM_SUITE_ALREADY_DELETED";
        // Dos puertas que hay que cerrar para que la guarda de cascada no se pueda esquivar por
        // `PUT /system-suites/{id}/status`: ni se entra a «eliminado» por ahí, ni se sale de él.
        public const string SystemSuiteDeletedNotSettable = "SYSTEM_SUITE_DELETED_NOT_SETTABLE";
        public const string SystemSuiteDeletedIsTerminal  = "SYSTEM_SUITE_DELETED_IS_TERMINAL";
        public const string TemplateItemsRequired        = "authorization.template_items_required";
        public const string AssignmentRulePriorityMustBePositive = "authorization.assignment_rule_priority_must_be_positive";
        public const string AssignmentRuleAlreadyActive   = "authorization.assignment_rule_already_active";
        public const string AssignmentRuleAlreadyInactive = "authorization.assignment_rule_already_inactive";
        public const string AssignmentRuleDuplicatePriority = "authorization.assignment_rule_duplicate_priority";
    }

    public static class Approvals
    {
        public const string DocumentTypeAlreadyRequired = "approvals.document_type_already_required";
        public const string RequestNotPending = "approvals.request_not_pending";
        public const string DocumentAlreadyExpired = "approvals.document_already_expired";
        public const string PolicyRequiresProfileOrRole = "approvals.policy_requires_profile_or_role";
        public const string PolicyAlreadyInactive = "approvals.policy_already_inactive";
        public const string PolicyInactiveCannotUpdate = "approvals.policy_inactive_cannot_update";
        public const string RuleAlreadyInactive = "approvals.rule_already_inactive";
        public const string RuleAlreadyActive = "approvals.rule_already_active";
        public const string DuplicateNotificationRule = "approvals.duplicate_notification_rule";
        public const string RequiresDocumentsIfApprovalRequired = "approvals.requires_documents_if_approval_required";
        public const string WorkflowNotAllowedForUserCategory   = "approvals.workflow_not_allowed_for_user_category";
        public const string RequiredDocumentsIncomplete         = "approvals.required_documents_incomplete";
        public const string SelfApprovalNotAllowed              = "approvals.self_approval_not_allowed";
        public const string GracePeriodInvalid                  = "approvals.grace_period_invalid";
    }

    public static class Configuration
    {
        public const string IdpConfigNotDraft           = "configuration.idp_config_not_draft";
        public const string IdpConfigNotActive          = "configuration.idp_config_not_active";
        public const string IdpConfigAlreadyActive      = "configuration.idp_config_already_active";
        public const string IdpConfigAlreadyInactive    = "configuration.idp_config_already_inactive";
        public const string IdpConfigAlreadyArchived    = "configuration.idp_config_already_archived";
        public const string IdpConfigArchivedCannotChange = "configuration.idp_config_archived_cannot_change";
        public const string IdpConfigPayloadInvalid     = "configuration.idp_config_payload_invalid";
        public const string IdpFallbackNotFound         = "configuration.idp_fallback_not_found";
        public const string AppConfigNotDraft           = "configuration.app_config_not_draft";
        public const string AppConfigNotPublished       = "configuration.app_config_not_published";
        public const string AppConfigAlreadyArchived    = "configuration.app_config_already_archived";
        public const string AppConfigAlreadyDeleted     = "configuration.app_config_already_deleted";
        public const string AppConfigNonOverridable     = "configuration.app_config_non_overridable";
        public const string FlagArchivedCannotChange    = "configuration.flag_archived_cannot_change";
        public const string FlagAlreadyActive           = "configuration.flag_already_active";
        public const string FlagAlreadyInactive         = "configuration.flag_already_inactive";
        public const string FlagPercentageOutOfRange    = "configuration.flag_percentage_out_of_range";
        public const string SystemSuiteIdRequired       = "configuration.system_suite_id_required";
        public const string DuplicateCriteria           = "configuration.duplicate_criteria";
        public const string CriteriaNotFound            = "configuration.criteria_not_found";
        public const string CriteriaTypeRequired        = "configuration.criteria_type_required";
        public const string CriteriaOperatorRequired    = "configuration.criteria_operator_required";
        public const string CriteriaValueRequired       = "configuration.criteria_value_required";
        public const string ParameterCodeNotUnique      = "configuration.parameter_code_not_unique";
        public const string ParameterHasActiveValues    = "configuration.parameter_has_active_values";
        public const string ParameterAlreadyDeleted     = "configuration.parameter_already_deleted";
        public const string ParameterValueInvalidType   = "configuration.parameter_value_invalid_type";
        public const string ParameterOverrideNotAllowed = "configuration.parameter_override_not_allowed";
        public const string ParameterGlobalValueInUse   = "configuration.parameter_global_value_in_use";
    }

    public static class Audit
    {
        public const string WhatChangedRequired = "audit.what_changed_required";
        public const string AffectedEntityRequired = "audit.affected_entity_required";
    }

    public static class Compliance
    {
        public const string ExpirationBeforeIssueDate          = "compliance.expiration_before_issue_date";
        public const string DocumentCannotTransition            = "compliance.document_cannot_transition";
        public const string DocumentNotPendingReview            = "compliance.document_not_pending_review";
        public const string DocumentAlreadyExpired              = "compliance.document_already_expired";
    }

    public static class Delegation
    {
        public const string SelfDelegationNotAllowed         = "delegation.self_delegation_not_allowed";
        public const string ValidUntilMustBeAfterValidFrom   = "delegation.valid_until_must_be_after_valid_from";
        public const string AllowedActionsRequired           = "delegation.allowed_actions_required";
        public const string CannotActivateFromCurrentStatus  = "delegation.cannot_activate_from_current_status";
        public const string CannotRevokeFromCurrentStatus    = "delegation.cannot_revoke_from_current_status";
        public const string RevocationReasonRequired         = "delegation.revocation_reason_required";
        public const string CannotArchiveFromCurrentStatus   = "delegation.cannot_archive_from_current_status";
        public const string NotActive                        = "delegation.not_active";
        public const string ApprovalRequired                 = "delegation.approval_required";
        public const string SelfApprovalNotAllowed           = "delegation.self_approval_not_allowed";
    }

    public static class TenantParameter
    {
        public const string CodeNotUnique = "tenant_parameter.code_not_unique";
        public const string NotFound = "tenant_parameter.not_found";
        public const string InvalidValueType = "tenant_parameter.invalid_value_type";
        public const string ValueNotInAllowedList = "tenant_parameter.value_not_in_allowed_list";
        public const string CannotDeactivateActive = "tenant_parameter.cannot_deactivate_active";
        public const string CannotDeleteWithChildren = "tenant_parameter.cannot_delete_with_children";
        /// <summary>El parámetro ya está en el estado terminal de borrado lógico.</summary>
        public const string AlreadyDeleted = "tenant_parameter.already_deleted";
        // ── Dependency guard errors ──────────────────────────────────────────
        /// <summary>
        /// Guardia de cascada del borrado lógico: un parámetro ACTIVO es una referencia VIVA de la
        /// configuración del inquilino —<c>ITenantParameterProvider</c> lo resuelve ahora mismo por su
        /// código y ocupa el índice único parcial IX_TenantParameters_TenantId_Code_IsActive—. Primero
        /// hay que desactivarlo, que es la eliminación lógica de ese vínculo. Se nombra en MAYÚSCULAS
        /// porque es un código de operación bloqueada (409 + BlockedOperationResponse), no un error de
        /// validación de dominio.
        /// </summary>
        public const string HasActiveBinding = "TENANT_PARAMETER_HAS_ACTIVE_BINDING";
    }

    public static class ValueObject
    {
        public const string EmailRequired = "value_object.email_required";
        public const string PropertyRequired = "value_object.property_required";
        public const string DateRangeInvalid = "value_object.date_range_invalid";
    }

    /// <summary>
    /// Errores del contexto acotado IGA (Identity Governance &amp; Administration), ADR-UMS-093.
    /// Cubren los invariantes del agregado de elegibilidad (RoleMaturityStatus, INV-RMS1..3)
    /// y del agregado de promoción (RolePromotionRequest, INV-RPR1..5).
    /// </summary>
    public static class IGA
    {
        // ── RoleMaturityStatus (INV-RMS1..3) ─────────────────────────────────
        public const string InvalidPerformanceScore     = "iga.invalid_performance_score";        // INV-RMS1
        public const string MaturityLevelUnchanged       = "iga.maturity_level_unchanged";          // INV-RMS2
        public const string MaturityLevelAlreadyMax      = "iga.maturity_level_already_max";         // INV-RMS3 (Principal no promocionable)
        public const string ComplianceIssuesBlockPromotion = "iga.compliance_issues_block_promotion"; // INV-RMS3
        public const string InsufficientPerformanceScore = "iga.insufficient_performance_score";     // INV-RMS3
        public const string InsufficientTimeInLevel      = "iga.insufficient_time_in_level";         // INV-RMS3
        public const string BlockingFactorRequired       = "iga.blocking_factor_required";

        // ── RolePromotionRequest (INV-RPR1..5) ───────────────────────────────
        public const string InvalidStateTransition       = "iga.invalid_state_transition";          // INV-RPR1 / INV-RPR5
        public const string RiskScoreOutOfRange          = "iga.risk_score_out_of_range";            // VO RiskScore
        public const string RiskScoreAlreadyFrozen       = "iga.risk_score_already_frozen";          // INV-RPR2
        public const string SegregationOfDutiesViolation = "iga.segregation_of_duties_violation";    // INV-RPR3
        public const string NotEligibleForPromotion      = "iga.not_eligible_for_promotion";         // INV-RPR4 (fail-closed)
        public const string SelfPromotionNotAllowed      = "iga.self_promotion_not_allowed";         // INV-RPR3 (solicitante ≠ objetivo)
        public const string DecisionReasonRequired       = "iga.decision_reason_required";
        public const string SameRolePromotion            = "iga.same_role_promotion";
        // G-100: código estable e idioma-agnóstico para «solicitud de promoción no hallada por id».
        // Reemplaza el mensaje en español que los handlers devolvían crudo y que el mapeador HTTP,
        // al buscar el substring en inglés «not found», clasificaba erróneamente como 400 en vez de 404.
        public const string RolePromotionRequestNotFound  = "iga.role_promotion_request_not_found";
    }
}
