using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Evolith.Ums.Infrastructure.Migrations.SqlServer.Plataforma
{
    /// <inheritdoc />
    public partial class InitSqlServerPlatform : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "approvals");

            migrationBuilder.EnsureSchema(
                name: "ums_configuration");

            migrationBuilder.EnsureSchema(
                name: "audit");

            migrationBuilder.EnsureSchema(
                name: "ums_platform");

            migrationBuilder.EnsureSchema(
                name: "ums_identity");

            migrationBuilder.EnsureSchema(
                name: "ums_authorization");

            migrationBuilder.EnsureSchema(
                name: "iga");

            migrationBuilder.CreateTable(
                name: "AccessEnforcementPolicies",
                schema: "approvals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EnforcementActionId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    GracePeriodDays = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
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
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SystemSuiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ScopeId = table.Column<int>(type: "int", nullable: false),
                    IsInheritable = table.Column<bool>(type: "bit", nullable: false),
                    IsEncrypted = table.Column<bool>(type: "bit", nullable: false),
                    IsNonOverridable = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StatusId = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
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
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StatusId = table.Column<int>(type: "int", nullable: false),
                    RequestedSystemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedBranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestedRoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Justification = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    GrantedRoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecisionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
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
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SystemSuiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    TargetUserCategoryId = table.Column<int>(type: "int", nullable: false),
                    RequiresApproval = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
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
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WhoActed = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubjectTypeId = table.Column<int>(type: "int", nullable: false),
                    WhenOccurred = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WhatChanged = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    AuditResultId = table.Column<int>(type: "int", nullable: false),
                    AffectedEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AffectedEntityType = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    RootTenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Metadata = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
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
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CriticityId = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
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
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FlagCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FlagTypeId = table.Column<int>(type: "int", nullable: false),
                    FlagTargets = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    StatusId = table.Column<int>(type: "int", nullable: false),
                    LinkedResourceTypeId = table.Column<int>(type: "int", nullable: true),
                    LinkedResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RolloutPercentage = table.Column<int>(type: "int", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SystemSuiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
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
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SystemSuiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderTypeId = table.Column<int>(type: "int", nullable: false),
                    DomainHintsJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    ConfigPayload = table.Column<string>(type: "nvarchar(max)", maxLength: 20000, nullable: false),
                    SecretRef = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    StatusId = table.Column<int>(type: "int", nullable: false),
                    ResolutionPriority = table.Column<int>(type: "int", nullable: false),
                    FallbackToId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdpConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InboxState",
                schema: "ums_platform",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConsumerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LockId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    Received = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReceiveCount = table.Column<int>(type: "int", nullable: false),
                    ExpirationTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Consumed = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Delivered = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSequenceNumber = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxState", x => x.Id);
                    table.UniqueConstraint("AK_InboxState_MessageId_ConsumerId", x => new { x.MessageId, x.ConsumerId });
                });

            migrationBuilder.CreateTable(
                name: "NotificationRules",
                schema: "approvals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChannelId = table.Column<int>(type: "int", nullable: false),
                    Recipient = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxState",
                schema: "ums_platform",
                columns: table => new
                {
                    OutboxId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LockId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    Created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Delivered = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSequenceNumber = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxState", x => x.OutboxId);
                });

            migrationBuilder.CreateTable(
                name: "ParameterDefinitions",
                schema: "ums_configuration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DataTypeId = table.Column<int>(type: "int", nullable: false),
                    DefaultValue = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    ScopeId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsMandatory = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Version = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
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
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParameterDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EffectiveValue = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    StatusId = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
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
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParameterDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OverrideValue = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    StatusId = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParameterTenantValues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PasswordResetTokens",
                schema: "ums_identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConsumedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InvalidatedReason = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordResetTokens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Profiles",
                schema: "ums_authorization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ScopeId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Profiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                schema: "ums_identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FamilyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RenewalCount = table.Column<int>(type: "int", nullable: false),
                    ReplacedByTokenId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedReason = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RoleMaturityStatuses",
                schema: "iga",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrentMaturityLevel = table.Column<int>(type: "int", nullable: false),
                    NextEligibleMaturityLevel = table.Column<int>(type: "int", nullable: true),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CurrentLevelSince = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EligibleForPromotionAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedCertificationsCount = table.Column<int>(type: "int", nullable: false),
                    CompletedTrainingsCount = table.Column<int>(type: "int", nullable: false),
                    PerformanceScore = table.Column<decimal>(type: "numeric(4,2)", nullable: false),
                    HasNoComplianceIssues = table.Column<bool>(type: "bit", nullable: false),
                    BlockingFactor = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LastReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleMaturityStatuses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RolePromotionRequests",
                schema: "iga",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequesterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrentRoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetRoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StatusId = table.Column<int>(type: "int", nullable: false),
                    RiskScore = table.Column<int>(type: "int", nullable: true),
                    ApproverId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SecurityReviewerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExecutorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VerifierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecisionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePromotionRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemSuites",
                schema: "ums_authorization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    StatusId = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
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
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    StatusId = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "nvarchar(max)", nullable: false)
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
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    ValueTypeId = table.Column<int>(type: "int", nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsSensitive = table.Column<bool>(type: "bit", nullable: false),
                    DefaultValue = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    AllowedValues = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
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
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OrganizationTypeId = table.Column<int>(type: "int", nullable: false),
                    IdpStrategyId = table.Column<int>(type: "int", nullable: false),
                    CompanyReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ParentTenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsManagementOwner = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DefaultSystemSuiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StatusId = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
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
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CompanyReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ContactName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContactEmail = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    StatusId = table.Column<int>(type: "int", nullable: false),
                    ApprovedTenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
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
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    StatusId = table.Column<int>(type: "int", nullable: false),
                    IdentityReference = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    IdentityReferenceTypeId = table.Column<int>(type: "int", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailedLoginAttempts = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    LockedUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AnonymizedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
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
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IssueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StatusId = table.Column<int>(type: "int", nullable: false),
                    CriticityId = table.Column<int>(type: "int", nullable: false),
                    FileStoragePath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    FileChecksum = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    NotificationStep = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
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
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DelegatingAdminId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DelegatedAdminId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScopeTypeId = table.Column<int>(type: "int", nullable: false),
                    ScopeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AllowedActionsJson = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ValidFrom = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ValidUntil = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    MaxDurationDays = table.Column<int>(type: "int", nullable: true),
                    RequiresApproval = table.Column<bool>(type: "bit", nullable: false),
                    ApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StatusId = table.Column<int>(type: "int", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevokedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RevocationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
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
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsMandatory = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
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
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FeatureFlagId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriteriaType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Operator = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
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
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FeatureFlagId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvaluatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Result = table.Column<bool>(type: "bit", nullable: false),
                    Context = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    EvaluatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
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
                name: "OutboxMessage",
                schema: "ums_platform",
                columns: table => new
                {
                    SequenceNumber = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EnqueueTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Headers = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Properties = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InboxMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InboxConsumerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OutboxId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    MessageType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InitiatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceAddress = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    DestinationAddress = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ResponseAddress = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    FaultAddress = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ExpirationTime = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessage", x => x.SequenceNumber);
                    table.ForeignKey(
                        name: "FK_OutboxMessage_InboxState_InboxMessageId_InboxConsumerId",
                        columns: x => new { x.InboxMessageId, x.InboxConsumerId },
                        principalSchema: "ums_platform",
                        principalTable: "InboxState",
                        principalColumns: new[] { "MessageId", "ConsumerId" });
                    table.ForeignKey(
                        name: "FK_OutboxMessage_OutboxState_OutboxId",
                        column: x => x.OutboxId,
                        principalSchema: "ums_platform",
                        principalTable: "OutboxState",
                        principalColumn: "OutboxId");
                });

            migrationBuilder.CreateTable(
                name: "ProfilePermissions",
                schema: "ums_authorization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetTypeId = table.Column<int>(type: "int", nullable: false),
                    TargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsAllowed = table.Column<bool>(type: "bit", nullable: false),
                    IsDenied = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsOverride = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
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
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SystemSuiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentRoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    HierarchyLevel = table.Column<int>(type: "int", nullable: false),
                    PromotionOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
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
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SystemSuiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
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
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SystemSuiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConfigKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ConfigValue = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    ScopeId = table.Column<int>(type: "int", nullable: false),
                    IsClientVisible = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
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
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SystemSuiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ParentResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
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
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SystemSuiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    StatusId = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Icon = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
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
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    GeofencingMetadata = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsClosed = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
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
                name: "TenantIdentityProviders",
                schema: "ums_identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    StrategyId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
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
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MethodId = table.Column<int>(type: "int", nullable: false),
                    StatusId = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
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
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
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
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Step = table.Column<int>(type: "int", nullable: false),
                    ChannelId = table.Column<int>(type: "int", nullable: false),
                    DaysRemaining = table.Column<int>(type: "int", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: false)
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
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SystemSuiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StatusId = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
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
                name: "SystemSuiteNodes",
                schema: "ums_authorization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NodeKindId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    StatusId = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Icon = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Route = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Responsable = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Criticidad = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ProductoImpactado = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ComponenteTecnico = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Dependencias = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Evidencias = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TrazabilidadSdlc = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSuiteNodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SystemSuiteNodes_SystemSuiteModules_ModuleId",
                        column: x => x.ModuleId,
                        principalSchema: "ums_authorization",
                        principalTable: "SystemSuiteModules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SystemSuiteNodes_SystemSuiteNodes_ParentNodeId",
                        column: x => x.ParentNodeId,
                        principalSchema: "ums_authorization",
                        principalTable: "SystemSuiteNodes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TenantBranchLifecycleEntries",
                schema: "ums_identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EpisodeId = table.Column<int>(type: "int", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActorId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    GeofencingSnapshot = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantBranchLifecycleEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantBranchLifecycleEntries_TenantBranches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "ums_identity",
                        principalTable: "TenantBranches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PermissionTemplateItems",
                schema: "ums_authorization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetTypeId = table.Column<int>(type: "int", nullable: false),
                    TargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsAllowed = table.Column<bool>(type: "bit", nullable: false),
                    IsDenied = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
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
                name: "SystemSuiteNodeActions",
                schema: "ums_authorization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActionCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSuiteNodeActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SystemSuiteNodeActions_SystemSuiteNodes_NodeId",
                        column: x => x.NodeId,
                        principalSchema: "ums_authorization",
                        principalTable: "SystemSuiteNodes",
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
                filter: "[ProfileId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AccessEnforcementPolicies_TenantId_RoleId",
                schema: "approvals",
                table: "AccessEnforcementPolicies",
                columns: new[] { "TenantId", "RoleId" },
                filter: "[RoleId] IS NOT NULL");

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
                unique: true,
                filter: "[StatusId] <> 4");

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
                name: "IX_ApprovalRequests_TargetUserId_RequestedSystemId_RequestedBranchId_StatusId",
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
                unique: true,
                filter: "[StatusId] <> 3");

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
                name: "IX_InboxState_Delivered",
                schema: "ums_platform",
                table: "InboxState",
                column: "Delivered");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationRules_TenantId",
                schema: "approvals",
                table: "NotificationRules",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_EnqueueTime",
                schema: "ums_platform",
                table: "OutboxMessage",
                column: "EnqueueTime");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_ExpirationTime",
                schema: "ums_platform",
                table: "OutboxMessage",
                column: "ExpirationTime");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_InboxMessageId_InboxConsumerId_SequenceNumber",
                schema: "ums_platform",
                table: "OutboxMessage",
                columns: new[] { "InboxMessageId", "InboxConsumerId", "SequenceNumber" },
                unique: true,
                filter: "[InboxMessageId] IS NOT NULL AND [InboxConsumerId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_OutboxId_SequenceNumber",
                schema: "ums_platform",
                table: "OutboxMessage",
                columns: new[] { "OutboxId", "SequenceNumber" },
                unique: true,
                filter: "[OutboxId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxState_Created",
                schema: "ums_platform",
                table: "OutboxState",
                column: "Created");

            migrationBuilder.CreateIndex(
                name: "IX_ParameterDefinitions_Code",
                schema: "ums_configuration",
                table: "ParameterDefinitions",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ParameterDefinitions_IsActive",
                schema: "ums_configuration",
                table: "ParameterDefinitions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ParameterDefinitions_IsDeleted",
                schema: "ums_configuration",
                table: "ParameterDefinitions",
                column: "IsDeleted");

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
                unique: true,
                filter: "[StatusId] <> 4");

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
                unique: true,
                filter: "[StatusId] <> 4");

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetTokens_ExpiresAtUtc",
                schema: "ums_identity",
                table: "PasswordResetTokens",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetTokens_TenantId_UserId",
                schema: "ums_identity",
                table: "PasswordResetTokens",
                columns: new[] { "TenantId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetTokens_TokenHash",
                schema: "ums_identity",
                table: "PasswordResetTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PermissionTemplateItems_TargetId_IsActive",
                schema: "ums_authorization",
                table: "PermissionTemplateItems",
                columns: new[] { "TargetId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_PermissionTemplateItems_TemplateId_TargetTypeId_TargetId_ActionId",
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
                name: "IX_ProfilePermissions_TemplateId",
                schema: "ums_authorization",
                table: "ProfilePermissions",
                column: "TemplateId");

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
                name: "IX_Profiles_UserId_Active",
                schema: "ums_authorization",
                table: "Profiles",
                column: "UserId",
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_ExpiresAtUtc",
                schema: "ums_identity",
                table: "RefreshTokens",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_FamilyId",
                schema: "ums_identity",
                table: "RefreshTokens",
                column: "FamilyId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_TenantId_UserId",
                schema: "ums_identity",
                table: "RefreshTokens",
                columns: new[] { "TenantId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_TokenHash",
                schema: "ums_identity",
                table: "RefreshTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoleMaturityStatuses_TenantId",
                schema: "iga",
                table: "RoleMaturityStatuses",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_RoleMaturityStatuses_TenantId_UserId",
                schema: "iga",
                table: "RoleMaturityStatuses",
                columns: new[] { "TenantId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_RoleMaturityStatuses_TenantId_UserId_RoleId",
                schema: "iga",
                table: "RoleMaturityStatuses",
                columns: new[] { "TenantId", "UserId", "RoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RolePromotionRequests_TargetUserId",
                schema: "iga",
                table: "RolePromotionRequests",
                column: "TargetUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePromotionRequests_TenantId",
                schema: "iga",
                table: "RolePromotionRequests",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePromotionRequests_TenantId_StatusId",
                schema: "iga",
                table: "RolePromotionRequests",
                columns: new[] { "TenantId", "StatusId" });

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
                name: "IX_SystemSuiteDomainResources_ModuleId",
                schema: "ums_authorization",
                table: "SystemSuiteDomainResources",
                column: "ModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemSuiteDomainResources_SystemSuiteId_Code",
                schema: "ums_authorization",
                table: "SystemSuiteDomainResources",
                columns: new[] { "SystemSuiteId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SystemSuiteModules_SystemSuiteId_Code",
                schema: "ums_authorization",
                table: "SystemSuiteModules",
                columns: new[] { "SystemSuiteId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SystemSuiteNodeActions_NodeId_ActionCode",
                schema: "ums_authorization",
                table: "SystemSuiteNodeActions",
                columns: new[] { "NodeId", "ActionCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SystemSuiteNodes_ModuleId_ParentNodeId_Code",
                schema: "ums_authorization",
                table: "SystemSuiteNodes",
                columns: new[] { "ModuleId", "ParentNodeId", "Code" },
                unique: true,
                filter: "[ParentNodeId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SystemSuiteNodes_ParentNodeId",
                schema: "ums_authorization",
                table: "SystemSuiteNodes",
                column: "ParentNodeId");

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
                name: "IX_TenantBranches_IsClosed",
                schema: "ums_identity",
                table: "TenantBranches",
                column: "IsClosed",
                filter: "[IsClosed] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_TenantBranches_TenantId_Code",
                schema: "ums_identity",
                table: "TenantBranches",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantBranchLifecycleEntries_BranchId_OccurredAtUtc",
                schema: "ums_identity",
                table: "TenantBranchLifecycleEntries",
                columns: new[] { "BranchId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TenantBranchLifecycleEntries_TenantId",
                schema: "ums_identity",
                table: "TenantBranchLifecycleEntries",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantIdentityProviders_TenantId_Code",
                schema: "ums_identity",
                table: "TenantIdentityProviders",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantParameters_IsDeleted",
                schema: "ums_identity",
                table: "TenantParameters",
                column: "IsDeleted",
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_TenantParameters_TenantId_Code_IsActive",
                schema: "ums_identity",
                table: "TenantParameters",
                columns: new[] { "TenantId", "Code", "IsActive" },
                unique: true,
                filter: "[IsActive] = 1");

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
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_ParentTenantId",
                schema: "ums_identity",
                table: "Tenants",
                column: "ParentTenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_SingleManagementOwner",
                schema: "ums_identity",
                table: "Tenants",
                column: "IsManagementOwner",
                unique: true,
                filter: "[IsManagementOwner] = 1");

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
                filter: "[IsDeleted] = 0");

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
                name: "OutboxMessage",
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
                name: "PasswordResetTokens",
                schema: "ums_identity");

            migrationBuilder.DropTable(
                name: "PermissionTemplateItems",
                schema: "ums_authorization");

            migrationBuilder.DropTable(
                name: "ProfilePermissions",
                schema: "ums_authorization");

            migrationBuilder.DropTable(
                name: "RefreshTokens",
                schema: "ums_identity");

            migrationBuilder.DropTable(
                name: "RoleMaturityStatuses",
                schema: "iga");

            migrationBuilder.DropTable(
                name: "RolePromotionRequests",
                schema: "iga");

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
                name: "SystemSuiteNodeActions",
                schema: "ums_authorization");

            migrationBuilder.DropTable(
                name: "TemplateAssignmentRules",
                schema: "ums_platform");

            migrationBuilder.DropTable(
                name: "TenantBranchLifecycleEntries",
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
                name: "InboxState",
                schema: "ums_platform");

            migrationBuilder.DropTable(
                name: "OutboxState",
                schema: "ums_platform");

            migrationBuilder.DropTable(
                name: "PermissionTemplates",
                schema: "ums_authorization");

            migrationBuilder.DropTable(
                name: "Profiles",
                schema: "ums_authorization");

            migrationBuilder.DropTable(
                name: "SystemSuiteNodes",
                schema: "ums_authorization");

            migrationBuilder.DropTable(
                name: "TenantBranches",
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
                name: "SystemSuiteModules",
                schema: "ums_authorization");

            migrationBuilder.DropTable(
                name: "Tenants",
                schema: "ums_identity");

            migrationBuilder.DropTable(
                name: "SystemSuites",
                schema: "ums_authorization");
        }
    }
}
