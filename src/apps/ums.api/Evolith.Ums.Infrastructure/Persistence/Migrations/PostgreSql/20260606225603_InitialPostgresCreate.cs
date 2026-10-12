using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Evolith.Ums.Infrastructure.Persistence.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class InitialPostgresCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pgcrypto", ",,");

            migrationBuilder.EnsureSchema(
                name: "approvals");

            migrationBuilder.EnsureSchema(
                name: "ums_configuration");

            migrationBuilder.EnsureSchema(
                name: "audit");

            migrationBuilder.EnsureSchema(
                name: "ums_platform");

            migrationBuilder.EnsureSchema(
                name: "ums_authorization");

            migrationBuilder.EnsureSchema(
                name: "ums_identity");

            migrationBuilder.CreateTable(
                name: "AccessEnforcementPolicies",
                schema: "approvals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: true),
                    EnforcementActionId = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessEnforcementPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppConfigurations",
                schema: "ums_configuration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    SystemSuiteId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModuleId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ScopeId = table.Column<int>(type: "integer", nullable: false),
                    IsInheritable = table.Column<bool>(type: "boolean", nullable: false),
                    IsEncrypted = table.Column<bool>(type: "boolean", nullable: false),
                    IsNonOverridable = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StatusId = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApprovalRequests",
                schema: "approvals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    StatusId = table.Column<int>(type: "integer", nullable: false),
                    RequestedSystemId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedBranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestedRoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Justification = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    GrantedRoleId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecisionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApprovalWorkflows",
                schema: "approvals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SystemSuiteId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    TargetUserCategoryId = table.Column<int>(type: "integer", nullable: false),
                    RequiresApproval = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalWorkflows", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditRecords",
                schema: "audit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WhoActed = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectTypeId = table.Column<int>(type: "integer", nullable: false),
                    WhenOccurred = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    WhatChanged = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    EventType = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    AuditResultId = table.Column<int>(type: "integer", nullable: false),
                    AffectedEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    AffectedEntityType = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    RootTenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Metadata = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentTypes",
                schema: "approvals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CriticityId = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FeatureFlags",
                schema: "ums_configuration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FlagCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FlagTypeId = table.Column<int>(type: "integer", nullable: false),
                    FlagTargets = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    StatusId = table.Column<int>(type: "integer", nullable: false),
                    LinkedResourceTypeId = table.Column<int>(type: "integer", nullable: true),
                    LinkedResourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    RolloutPercentage = table.Column<int>(type: "integer", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false),
                    SystemSuiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeatureFlags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IdpConfigurations",
                schema: "ums_configuration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SystemSuiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderTypeId = table.Column<int>(type: "integer", nullable: false),
                    DomainHintsJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ConfigPayload = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    SecretRef = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    StatusId = table.Column<int>(type: "integer", nullable: false),
                    ResolutionPriority = table.Column<int>(type: "integer", nullable: false),
                    FallbackToId = table.Column<Guid>(type: "uuid", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdpConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationRules",
                schema: "approvals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChannelId = table.Column<int>(type: "integer", nullable: false),
                    Recipient = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxDeadLetters",
                schema: "ums_platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalMessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    AggregateId = table.Column<Guid>(type: "uuid", nullable: false),
                    AggregateName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EventName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EventType = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    OccurredOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeadLetteredOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RetryCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    LastError = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ReplayedSuccessfully = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ReplayedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxDeadLetters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "ums_platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AggregateId = table.Column<Guid>(type: "uuid", nullable: false),
                    AggregateName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EventName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EventType = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    OccurredOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    LastError = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    LockedUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LockedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ParameterDefinitions",
                schema: "ums_configuration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    DataTypeId = table.Column<int>(type: "integer", nullable: false),
                    DefaultValue = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ScopeId = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsMandatory = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParameterDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ParameterGlobalValues",
                schema: "ums_configuration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ParameterDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    EffectiveValue = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    StatusId = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParameterGlobalValues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ParameterTenantValues",
                schema: "ums_configuration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParameterDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OverrideValue = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    StatusId = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParameterTenantValues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Profiles",
                schema: "ums_authorization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    ScopeId = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Profiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemSuites",
                schema: "ums_authorization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    StatusId = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSuites", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TemplateAssignmentRules",
                schema: "ums_platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    StatusId = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateAssignmentRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TenantParameters",
                schema: "ums_identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Value = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ValueTypeId = table.Column<int>(type: "integer", nullable: false),
                    CategoryId = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsSensitive = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultValue = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    AllowedValues = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantParameters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tenants",
                schema: "ums_identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OrganizationTypeId = table.Column<int>(type: "integer", nullable: false),
                    IdpStrategyId = table.Column<int>(type: "integer", nullable: false),
                    CompanyReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ParentTenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsManagementOwner = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    StatusId = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TenantSignupRequests",
                schema: "ums_identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CompanyReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ContactName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContactEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    StatusId = table.Column<int>(type: "integer", nullable: false),
                    ApprovedTenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantSignupRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserAccounts",
                schema: "ums_identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CategoryId = table.Column<int>(type: "integer", nullable: false),
                    StatusId = table.Column<int>(type: "integer", nullable: false),
                    IdentityReference = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    IdentityReferenceTypeId = table.Column<int>(type: "integer", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AnonymizedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserDocuments",
                schema: "approvals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    IssueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpirationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StatusId = table.Column<int>(type: "integer", nullable: false),
                    CriticityId = table.Column<int>(type: "integer", nullable: false),
                    FileStoragePath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    FileChecksum = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    NotificationStep = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDocuments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserManagementDelegations",
                schema: "ums_identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DelegatingAdminId = table.Column<Guid>(type: "uuid", nullable: false),
                    DelegatedAdminId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeTypeId = table.Column<int>(type: "integer", nullable: false),
                    ScopeId = table.Column<Guid>(type: "uuid", nullable: true),
                    AllowedActionsJson = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ValidFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ValidUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    MaxDurationDays = table.Column<int>(type: "integer", nullable: true),
                    RequiresApproval = table.Column<bool>(type: "boolean", nullable: false),
                    ApprovalRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    StatusId = table.Column<int>(type: "integer", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RevocationReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserManagementDelegations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApprovalRequiredDocuments",
                schema: "approvals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsMandatory = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalRequiredDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApprovalRequiredDocuments_ApprovalWorkflows_WorkflowId",
                        column: x => x.WorkflowId,
                        principalSchema: "approvals",
                        principalTable: "ApprovalWorkflows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FeatureFlagCriteria",
                schema: "ums_configuration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FeatureFlagId = table.Column<Guid>(type: "uuid", nullable: false),
                    CriteriaType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Operator = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Value = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeatureFlagCriteria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeatureFlagCriteria_FeatureFlags_FeatureFlagId",
                        column: x => x.FeatureFlagId,
                        principalSchema: "ums_configuration",
                        principalTable: "FeatureFlags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FeatureFlagEvaluationLogs",
                schema: "ums_configuration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FeatureFlagId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvaluatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    Result = table.Column<bool>(type: "boolean", nullable: false),
                    Context = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    EvaluatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeatureFlagEvaluationLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeatureFlagEvaluationLogs_FeatureFlags_FeatureFlagId",
                        column: x => x.FeatureFlagId,
                        principalSchema: "ums_configuration",
                        principalTable: "FeatureFlags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProfilePermissions",
                schema: "ums_authorization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetTypeId = table.Column<int>(type: "integer", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsAllowed = table.Column<bool>(type: "boolean", nullable: false),
                    IsDenied = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsOverride = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfilePermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProfilePermissions_Profiles_ProfileId",
                        column: x => x.ProfileId,
                        principalSchema: "ums_authorization",
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                schema: "ums_authorization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SystemSuiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentRoleId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Value = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    HierarchyLevel = table.Column<int>(type: "integer", nullable: false),
                    PromotionOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Roles_Roles_ParentRoleId",
                        column: x => x.ParentRoleId,
                        principalSchema: "ums_authorization",
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Roles_SystemSuites_SystemSuiteId",
                        column: x => x.SystemSuiteId,
                        principalSchema: "ums_authorization",
                        principalTable: "SystemSuites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SystemSuiteActions",
                schema: "ums_authorization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SystemSuiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModuleId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSuiteActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SystemSuiteActions_SystemSuites_SystemSuiteId",
                        column: x => x.SystemSuiteId,
                        principalSchema: "ums_authorization",
                        principalTable: "SystemSuites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SystemSuiteAppSettings",
                schema: "ums_authorization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SystemSuiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfigKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ConfigValue = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ScopeId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSuiteAppSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SystemSuiteAppSettings_SystemSuites_SystemSuiteId",
                        column: x => x.SystemSuiteId,
                        principalSchema: "ums_authorization",
                        principalTable: "SystemSuites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SystemSuiteDomainResources",
                schema: "ums_authorization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SystemSuiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModuleId = table.Column<Guid>(type: "uuid", nullable: true),
                    ParentResourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSuiteDomainResources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SystemSuiteDomainResources_SystemSuites_SystemSuiteId",
                        column: x => x.SystemSuiteId,
                        principalSchema: "ums_authorization",
                        principalTable: "SystemSuites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SystemSuiteModules",
                schema: "ums_authorization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SystemSuiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    StatusId = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSuiteModules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SystemSuiteModules_SystemSuites_SystemSuiteId",
                        column: x => x.SystemSuiteId,
                        principalSchema: "ums_authorization",
                        principalTable: "SystemSuites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TenantBranches",
                schema: "ums_identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    GeofencingMetadata = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantBranches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantBranches_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "ums_identity",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TenantBrandings",
                schema: "ums_identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Logo = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    LogoFormatId = table.Column<int>(type: "integer", nullable: false),
                    PrimaryColor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BackgroundStyleId = table.Column<int>(type: "integer", nullable: false),
                    HeadlineText = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SecondaryText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    PrimaryButtonLabel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FooterText = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CustomDomain = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DnsVerificationStatusId = table.Column<int>(type: "integer", nullable: false),
                    DnsCnameTarget = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    MagicLinkFallbackEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantBrandings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantBrandings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "ums_identity",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TenantIdentityProviders",
                schema: "ums_identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    StrategyId = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantIdentityProviders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantIdentityProviders_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "ums_identity",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserAccountMfaEnrollments",
                schema: "ums_identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    MethodId = table.Column<int>(type: "integer", nullable: false),
                    StatusId = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAccountMfaEnrollments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserAccountMfaEnrollments_UserAccounts_UserAccountId",
                        column: x => x.UserAccountId,
                        principalSchema: "ums_identity",
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserAccountPasswordCredentials",
                schema: "ums_identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAccountPasswordCredentials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserAccountPasswordCredentials_UserAccounts_UserAccountId",
                        column: x => x.UserAccountId,
                        principalSchema: "ums_identity",
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserDocumentNotifications",
                schema: "approvals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Step = table.Column<int>(type: "integer", nullable: false),
                    ChannelId = table.Column<int>(type: "integer", nullable: false),
                    DaysRemaining = table.Column<int>(type: "integer", nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDocumentNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserDocumentNotifications_UserDocuments_UserDocumentId",
                        column: x => x.UserDocumentId,
                        principalSchema: "approvals",
                        principalTable: "UserDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PermissionTemplates",
                schema: "ums_authorization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    SystemSuiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StatusId = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermissionTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PermissionTemplates_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "ums_authorization",
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PermissionTemplates_SystemSuites_SystemSuiteId",
                        column: x => x.SystemSuiteId,
                        principalSchema: "ums_authorization",
                        principalTable: "SystemSuites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SystemSuiteMenus",
                schema: "ums_authorization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ModuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSuiteMenus", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SystemSuiteMenus_SystemSuiteModules_ModuleId",
                        column: x => x.ModuleId,
                        principalSchema: "ums_authorization",
                        principalTable: "SystemSuiteModules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PermissionTemplateItems",
                schema: "ums_authorization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetTypeId = table.Column<int>(type: "integer", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsAllowed = table.Column<bool>(type: "boolean", nullable: false),
                    IsDenied = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermissionTemplateItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PermissionTemplateItems_PermissionTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalSchema: "ums_authorization",
                        principalTable: "PermissionTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SystemSuiteSubMenus",
                schema: "ums_authorization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MenuId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSuiteSubMenus", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SystemSuiteSubMenus_SystemSuiteMenus_MenuId",
                        column: x => x.MenuId,
                        principalSchema: "ums_authorization",
                        principalTable: "SystemSuiteMenus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SystemSuiteOptions",
                schema: "ums_authorization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubMenuId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ActionCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSuiteOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SystemSuiteOptions_SystemSuiteSubMenus_SubMenuId",
                        column: x => x.SubMenuId,
                        principalSchema: "ums_authorization",
                        principalTable: "SystemSuiteSubMenus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccessEnforcementPolicies_TenantId",
                schema: "approvals",
                table: "AccessEnforcementPolicies",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AccessEnforcementPolicies_TenantId_ProfileId",
                schema: "approvals",
                table: "AccessEnforcementPolicies",
                columns: new[] { "TenantId", "ProfileId" },
                filter: "\"ProfileId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AccessEnforcementPolicies_TenantId_RoleId",
                schema: "approvals",
                table: "AccessEnforcementPolicies",
                columns: new[] { "TenantId", "RoleId" },
                filter: "\"RoleId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AppConfigurations_ScopeId",
                schema: "ums_configuration",
                table: "AppConfigurations",
                column: "ScopeId");

            migrationBuilder.CreateIndex(
                name: "IX_AppConfigurations_StatusId",
                schema: "ums_configuration",
                table: "AppConfigurations",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_AppConfigurations_TenantId_SystemSuiteId_ModuleId_Code",
                schema: "ums_configuration",
                table: "AppConfigurations",
                columns: new[] { "TenantId", "SystemSuiteId", "ModuleId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_TargetProfileId",
                schema: "approvals",
                table: "ApprovalRequests",
                column: "TargetProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_TargetUserId",
                schema: "approvals",
                table: "ApprovalRequests",
                column: "TargetUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_TargetUserId_RequestedSystemId_RequestedBr~",
                schema: "approvals",
                table: "ApprovalRequests",
                columns: new[] { "TargetUserId", "RequestedSystemId", "RequestedBranchId", "StatusId" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_WorkflowId",
                schema: "approvals",
                table: "ApprovalRequests",
                column: "WorkflowId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequiredDocuments_DocumentTypeId",
                schema: "approvals",
                table: "ApprovalRequiredDocuments",
                column: "DocumentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequiredDocuments_WorkflowId",
                schema: "approvals",
                table: "ApprovalRequiredDocuments",
                column: "WorkflowId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequiredDocuments_WorkflowId_DocumentTypeId",
                schema: "approvals",
                table: "ApprovalRequiredDocuments",
                columns: new[] { "WorkflowId", "DocumentTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalWorkflows_Code",
                schema: "approvals",
                table: "ApprovalWorkflows",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalWorkflows_TenantId",
                schema: "approvals",
                table: "ApprovalWorkflows",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalWorkflows_TenantId_Code",
                schema: "approvals",
                table: "ApprovalWorkflows",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditRecords_AffectedEntityId",
                schema: "audit",
                table: "AuditRecords",
                column: "AffectedEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditRecords_AffectedEntityId_AffectedEntityType",
                schema: "audit",
                table: "AuditRecords",
                columns: new[] { "AffectedEntityId", "AffectedEntityType" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditRecords_EventType",
                schema: "audit",
                table: "AuditRecords",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_AuditRecords_RootTenantId",
                schema: "audit",
                table: "AuditRecords",
                column: "RootTenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditRecords_WhoActed",
                schema: "audit",
                table: "AuditRecords",
                column: "WhoActed");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTypes_TenantId",
                schema: "approvals",
                table: "DocumentTypes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTypes_TenantId_Code",
                schema: "approvals",
                table: "DocumentTypes",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FeatureFlagCriteria_FeatureFlagId",
                schema: "ums_configuration",
                table: "FeatureFlagCriteria",
                column: "FeatureFlagId");

            migrationBuilder.CreateIndex(
                name: "IX_FeatureFlagEvaluationLogs_EvaluatedAtUtc",
                schema: "ums_configuration",
                table: "FeatureFlagEvaluationLogs",
                column: "EvaluatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_FeatureFlagEvaluationLogs_FeatureFlagId",
                schema: "ums_configuration",
                table: "FeatureFlagEvaluationLogs",
                column: "FeatureFlagId");

            migrationBuilder.CreateIndex(
                name: "IX_FeatureFlags_FlagTypeId",
                schema: "ums_configuration",
                table: "FeatureFlags",
                column: "FlagTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_FeatureFlags_StatusId",
                schema: "ums_configuration",
                table: "FeatureFlags",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_FeatureFlags_SystemSuiteId",
                schema: "ums_configuration",
                table: "FeatureFlags",
                column: "SystemSuiteId");

            migrationBuilder.CreateIndex(
                name: "IX_FeatureFlags_SystemSuiteId_FlagCode",
                schema: "ums_configuration",
                table: "FeatureFlags",
                columns: new[] { "SystemSuiteId", "FlagCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IdpConfigurations_ProviderTypeId",
                schema: "ums_configuration",
                table: "IdpConfigurations",
                column: "ProviderTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_IdpConfigurations_SystemSuiteId",
                schema: "ums_configuration",
                table: "IdpConfigurations",
                column: "SystemSuiteId");

            migrationBuilder.CreateIndex(
                name: "IX_IdpConfigurations_TenantId",
                schema: "ums_configuration",
                table: "IdpConfigurations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_IdpConfigurations_TenantId_SystemSuiteId_ResolutionPriority",
                schema: "ums_configuration",
                table: "IdpConfigurations",
                columns: new[] { "TenantId", "SystemSuiteId", "ResolutionPriority" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationRules_TenantId",
                schema: "approvals",
                table: "NotificationRules",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxDeadLetters_DeadLetteredOnUtc",
                schema: "ums_platform",
                table: "OutboxDeadLetters",
                column: "DeadLetteredOnUtc");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxDeadLetters_OriginalMessageId",
                schema: "ums_platform",
                table: "OutboxDeadLetters",
                column: "OriginalMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxDeadLetters_PendingReplay",
                schema: "ums_platform",
                table: "OutboxDeadLetters",
                columns: new[] { "ReplayedSuccessfully", "DeadLetteredOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxDeadLetters_TenantId",
                schema: "ums_platform",
                table: "OutboxDeadLetters",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Aggregate",
                schema: "ums_platform",
                table: "OutboxMessages",
                columns: new[] { "AggregateName", "AggregateId" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Dispatch",
                schema: "ums_platform",
                table: "OutboxMessages",
                columns: new[] { "ProcessedOnUtc", "OccurredOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_TenantId",
                schema: "ums_platform",
                table: "OutboxMessages",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ParameterDefinitions_Code",
                schema: "ums_configuration",
                table: "ParameterDefinitions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParameterDefinitions_IsActive",
                schema: "ums_configuration",
                table: "ParameterDefinitions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ParameterDefinitions_ScopeId",
                schema: "ums_configuration",
                table: "ParameterDefinitions",
                column: "ScopeId");

            migrationBuilder.CreateIndex(
                name: "IX_ParameterGlobalValues_ParameterDefinitionId",
                schema: "ums_configuration",
                table: "ParameterGlobalValues",
                column: "ParameterDefinitionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParameterGlobalValues_StatusId",
                schema: "ums_configuration",
                table: "ParameterGlobalValues",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_ParameterTenantValues_StatusId",
                schema: "ums_configuration",
                table: "ParameterTenantValues",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_ParameterTenantValues_TenantId",
                schema: "ums_configuration",
                table: "ParameterTenantValues",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ParameterTenantValues_TenantId_ParameterDefinitionId",
                schema: "ums_configuration",
                table: "ParameterTenantValues",
                columns: new[] { "TenantId", "ParameterDefinitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PermissionTemplateItems_TemplateId_TargetTypeId_TargetId_Ac~",
                schema: "ums_authorization",
                table: "PermissionTemplateItems",
                columns: new[] { "TemplateId", "TargetTypeId", "TargetId", "ActionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PermissionTemplates_RoleId",
                schema: "ums_authorization",
                table: "PermissionTemplates",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_PermissionTemplates_SystemSuiteId",
                schema: "ums_authorization",
                table: "PermissionTemplates",
                column: "SystemSuiteId");

            migrationBuilder.CreateIndex(
                name: "IX_PermissionTemplates_TenantId",
                schema: "ums_authorization",
                table: "PermissionTemplates",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PermissionTemplates_TenantId_RoleId_SystemSuiteId_Version",
                schema: "ums_authorization",
                table: "PermissionTemplates",
                columns: new[] { "TenantId", "RoleId", "SystemSuiteId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProfilePermissions_ProfileId",
                schema: "ums_authorization",
                table: "ProfilePermissions",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ProfilePermissions_ProfileId_TemplateId_ActionId_TargetId",
                schema: "ums_authorization",
                table: "ProfilePermissions",
                columns: new[] { "ProfileId", "TemplateId", "ActionId", "TargetId" });

            migrationBuilder.CreateIndex(
                name: "IX_Profiles_TenantId",
                schema: "ums_authorization",
                table: "Profiles",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Profiles_TenantId_UserId_RoleId_BranchId",
                schema: "ums_authorization",
                table: "Profiles",
                columns: new[] { "TenantId", "UserId", "RoleId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_Profiles_UserId",
                schema: "ums_authorization",
                table: "Profiles",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_ParentRoleId",
                schema: "ums_authorization",
                table: "Roles",
                column: "ParentRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_SystemSuiteId_Code",
                schema: "ums_authorization",
                table: "Roles",
                columns: new[] { "SystemSuiteId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Roles_TenantId",
                schema: "ums_authorization",
                table: "Roles",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemSuiteActions_SystemSuiteId_Code",
                schema: "ums_authorization",
                table: "SystemSuiteActions",
                columns: new[] { "SystemSuiteId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SystemSuiteActions_TenantId",
                schema: "ums_authorization",
                table: "SystemSuiteActions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemSuiteAppSettings_SystemSuiteId_ConfigKey_ScopeId",
                schema: "ums_authorization",
                table: "SystemSuiteAppSettings",
                columns: new[] { "SystemSuiteId", "ConfigKey", "ScopeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SystemSuiteDomainResources_SystemSuiteId_Code",
                schema: "ums_authorization",
                table: "SystemSuiteDomainResources",
                columns: new[] { "SystemSuiteId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SystemSuiteMenus_ModuleId_Code",
                schema: "ums_authorization",
                table: "SystemSuiteMenus",
                columns: new[] { "ModuleId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SystemSuiteModules_SystemSuiteId_Code",
                schema: "ums_authorization",
                table: "SystemSuiteModules",
                columns: new[] { "SystemSuiteId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SystemSuiteOptions_SubMenuId_Code",
                schema: "ums_authorization",
                table: "SystemSuiteOptions",
                columns: new[] { "SubMenuId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SystemSuites_TenantId",
                schema: "ums_authorization",
                table: "SystemSuites",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemSuites_TenantId_Code",
                schema: "ums_authorization",
                table: "SystemSuites",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SystemSuiteSubMenus_MenuId_Code",
                schema: "ums_authorization",
                table: "SystemSuiteSubMenus",
                columns: new[] { "MenuId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantBranches_TenantId_Code",
                schema: "ums_identity",
                table: "TenantBranches",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantBrandings_CustomDomain",
                schema: "ums_identity",
                table: "TenantBrandings",
                column: "CustomDomain",
                unique: true,
                filter: "\"CustomDomain\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TenantBrandings_TenantId",
                schema: "ums_identity",
                table: "TenantBrandings",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantIdentityProviders_TenantId_Code",
                schema: "ums_identity",
                table: "TenantIdentityProviders",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantParameters_TenantId_Code_IsActive",
                schema: "ums_identity",
                table: "TenantParameters",
                columns: new[] { "TenantId", "Code", "IsActive" },
                unique: true,
                filter: "\"IsActive\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Code",
                schema: "ums_identity",
                table: "Tenants",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_IsDeleted",
                schema: "ums_identity",
                table: "Tenants",
                column: "IsDeleted",
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_ParentTenantId",
                schema: "ums_identity",
                table: "Tenants",
                column: "ParentTenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantSignupRequests_CompanyReference",
                schema: "ums_identity",
                table: "TenantSignupRequests",
                column: "CompanyReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantSignupRequests_ContactEmail",
                schema: "ums_identity",
                table: "TenantSignupRequests",
                column: "ContactEmail");

            migrationBuilder.CreateIndex(
                name: "IX_TenantSignupRequests_StatusId",
                schema: "ums_identity",
                table: "TenantSignupRequests",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAccountMfaEnrollments_UserAccountId_MethodId",
                schema: "ums_identity",
                table: "UserAccountMfaEnrollments",
                columns: new[] { "UserAccountId", "MethodId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserAccountPasswordCredentials_UserAccountId",
                schema: "ums_identity",
                table: "UserAccountPasswordCredentials",
                column: "UserAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAccounts_Email",
                schema: "ums_identity",
                table: "UserAccounts",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_UserAccounts_IsDeleted",
                schema: "ums_identity",
                table: "UserAccounts",
                column: "IsDeleted",
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_UserAccounts_TenantId",
                schema: "ums_identity",
                table: "UserAccounts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAccounts_TenantId_Email",
                schema: "ums_identity",
                table: "UserAccounts",
                columns: new[] { "TenantId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserDocumentNotifications_UserDocumentId",
                schema: "approvals",
                table: "UserDocumentNotifications",
                column: "UserDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_UserDocumentNotifications_UserDocumentId_Step",
                schema: "approvals",
                table: "UserDocumentNotifications",
                columns: new[] { "UserDocumentId", "Step" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserDocuments_ExpirationDate",
                schema: "approvals",
                table: "UserDocuments",
                column: "ExpirationDate");

            migrationBuilder.CreateIndex(
                name: "IX_UserDocuments_StatusId",
                schema: "approvals",
                table: "UserDocuments",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_UserDocuments_UserId",
                schema: "approvals",
                table: "UserDocuments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserDocuments_UserId_DocumentTypeId",
                schema: "approvals",
                table: "UserDocuments",
                columns: new[] { "UserId", "DocumentTypeId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserManagementDelegations_DelegatedAdminId",
                schema: "ums_identity",
                table: "UserManagementDelegations",
                column: "DelegatedAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_UserManagementDelegations_DelegatingAdminId",
                schema: "ums_identity",
                table: "UserManagementDelegations",
                column: "DelegatingAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_UserManagementDelegations_TenantId",
                schema: "ums_identity",
                table: "UserManagementDelegations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_UserManagementDelegations_TenantId_StatusId",
                schema: "ums_identity",
                table: "UserManagementDelegations",
                columns: new[] { "TenantId", "StatusId" });
        }

        /// <inheritdoc />
        #pragma warning disable S1186
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccessEnforcementPolicies",
                schema: "approvals");

            migrationBuilder.DropTable(
                name: "AppConfigurations",
                schema: "ums_configuration");

            migrationBuilder.DropTable(
                name: "ApprovalRequests",
                schema: "approvals");

            migrationBuilder.DropTable(
                name: "ApprovalRequiredDocuments",
                schema: "approvals");

            migrationBuilder.DropTable(
                name: "AuditRecords",
                schema: "audit");

            migrationBuilder.DropTable(
                name: "DocumentTypes",
                schema: "approvals");

            migrationBuilder.DropTable(
                name: "FeatureFlagCriteria",
                schema: "ums_configuration");

            migrationBuilder.DropTable(
                name: "FeatureFlagEvaluationLogs",
                schema: "ums_configuration");

            migrationBuilder.DropTable(
                name: "IdpConfigurations",
                schema: "ums_configuration");

            migrationBuilder.DropTable(
                name: "NotificationRules",
                schema: "approvals");

            migrationBuilder.DropTable(
                name: "OutboxDeadLetters",
                schema: "ums_platform");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "ums_platform");

            migrationBuilder.DropTable(
                name: "ParameterDefinitions",
                schema: "ums_configuration");

            migrationBuilder.DropTable(
                name: "ParameterGlobalValues",
                schema: "ums_configuration");

            migrationBuilder.DropTable(
                name: "ParameterTenantValues",
                schema: "ums_configuration");

            migrationBuilder.DropTable(
                name: "PermissionTemplateItems",
                schema: "ums_authorization");

            migrationBuilder.DropTable(
                name: "ProfilePermissions",
                schema: "ums_authorization");

            migrationBuilder.DropTable(
                name: "SystemSuiteActions",
                schema: "ums_authorization");

            migrationBuilder.DropTable(
                name: "SystemSuiteAppSettings",
                schema: "ums_authorization");

            migrationBuilder.DropTable(
                name: "SystemSuiteDomainResources",
                schema: "ums_authorization");

            migrationBuilder.DropTable(
                name: "SystemSuiteOptions",
                schema: "ums_authorization");

            migrationBuilder.DropTable(
                name: "TemplateAssignmentRules",
                schema: "ums_platform");

            migrationBuilder.DropTable(
                name: "TenantBranches",
                schema: "ums_identity");

            migrationBuilder.DropTable(
                name: "TenantBrandings",
                schema: "ums_identity");

            migrationBuilder.DropTable(
                name: "TenantIdentityProviders",
                schema: "ums_identity");

            migrationBuilder.DropTable(
                name: "TenantParameters",
                schema: "ums_identity");

            migrationBuilder.DropTable(
                name: "TenantSignupRequests",
                schema: "ums_identity");

            migrationBuilder.DropTable(
                name: "UserAccountMfaEnrollments",
                schema: "ums_identity");

            migrationBuilder.DropTable(
                name: "UserAccountPasswordCredentials",
                schema: "ums_identity");

            migrationBuilder.DropTable(
                name: "UserDocumentNotifications",
                schema: "approvals");

            migrationBuilder.DropTable(
                name: "UserManagementDelegations",
                schema: "ums_identity");

            migrationBuilder.DropTable(
                name: "ApprovalWorkflows",
                schema: "approvals");

            migrationBuilder.DropTable(
                name: "FeatureFlags",
                schema: "ums_configuration");

            migrationBuilder.DropTable(
                name: "PermissionTemplates",
                schema: "ums_authorization");

            migrationBuilder.DropTable(
                name: "Profiles",
                schema: "ums_authorization");

            migrationBuilder.DropTable(
                name: "SystemSuiteSubMenus",
                schema: "ums_authorization");

            migrationBuilder.DropTable(
                name: "Tenants",
                schema: "ums_identity");

            migrationBuilder.DropTable(
                name: "UserAccounts",
                schema: "ums_identity");

            migrationBuilder.DropTable(
                name: "UserDocuments",
                schema: "approvals");

            migrationBuilder.DropTable(
                name: "Roles",
                schema: "ums_authorization");

            migrationBuilder.DropTable(
                name: "SystemSuiteMenus",
                schema: "ums_authorization");

            migrationBuilder.DropTable(
                name: "SystemSuiteModules",
                schema: "ums_authorization");

            migrationBuilder.DropTable(
                name: "SystemSuites",
                schema: "ums_authorization");
        }
#pragma warning restore S1186
    }
}
