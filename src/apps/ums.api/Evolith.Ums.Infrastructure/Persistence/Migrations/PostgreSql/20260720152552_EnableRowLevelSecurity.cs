using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Evolith.Ums.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnableRowLevelSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var tables = new (string Schema, string TableName, bool IsGlobalNullable)[]
            {
                ("ums_identity", "TenantBranches", false),
                ("ums_identity", "TenantParameters", false),
                ("ums_identity", "TenantIdentityProviders", false),
                ("ums_identity", "UserAccounts", false),
                ("ums_authorization", "Profiles", false),
                ("ums_authorization", "Roles", false),
                ("ums_authorization", "SystemSuites", false),
                ("ums_authorization", "PermissionTemplates", false),
                ("ums_identity", "UserManagementDelegations", false),
                ("ums_configuration", "IdpConfigurations", false),
                ("ums_configuration", "AppConfigurations", true),
                ("ums_configuration", "ParameterTenantValues", false),
                ("approvals", "ApprovalWorkflows", false),
                ("approvals", "NotificationRules", false),
                ("approvals", "DocumentTypes", false),
                ("approvals", "AccessEnforcementPolicies", false),
                ("iga", "RoleMaturityStatuses", false),
                ("iga", "RolePromotionRequests", false)
            };

            foreach (var t in tables)
            {
                migrationBuilder.Sql($@"ALTER TABLE ""{t.Schema}"".""{t.TableName}"" ENABLE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($@"ALTER TABLE ""{t.Schema}"".""{t.TableName}"" FORCE ROW LEVEL SECURITY;");

                string policyUsing = t.IsGlobalNullable
                    ? @"current_setting('app.current_organization_id', true) = '' OR ""TenantId"" IS NULL OR ""TenantId""::text = current_setting('app.current_organization_id', true)"
                    : @"current_setting('app.current_organization_id', true) = '' OR ""TenantId""::text = current_setting('app.current_organization_id', true)";

                migrationBuilder.Sql($@"
                    CREATE POLICY tenant_isolation_policy ON ""{t.Schema}"".""{t.TableName}""
                    FOR ALL
                    USING ({policyUsing});
                ");
            }
        }

        /// <inheritdoc />
        #pragma warning disable S1186
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            var tables = new (string Schema, string TableName)[]
            {
                ("ums_identity", "TenantBranches"),
                ("ums_identity", "TenantParameters"),
                ("ums_identity", "TenantIdentityProviders"),
                ("ums_identity", "UserAccounts"),
                ("ums_authorization", "Profiles"),
                ("ums_authorization", "Roles"),
                ("ums_authorization", "SystemSuites"),
                ("ums_authorization", "PermissionTemplates"),
                ("ums_identity", "UserManagementDelegations"),
                ("ums_configuration", "IdpConfigurations"),
                ("ums_configuration", "AppConfigurations"),
                ("ums_configuration", "ParameterTenantValues"),
                ("approvals", "ApprovalWorkflows"),
                ("approvals", "NotificationRules"),
                ("approvals", "DocumentTypes"),
                ("approvals", "AccessEnforcementPolicies"),
                ("iga", "RoleMaturityStatuses"),
                ("iga", "RolePromotionRequests")
            };

            foreach (var t in tables)
            {
                migrationBuilder.Sql($@"DROP POLICY IF EXISTS tenant_isolation_policy ON ""{t.Schema}"".""{t.TableName}"";");
                migrationBuilder.Sql($@"ALTER TABLE ""{t.Schema}"".""{t.TableName}"" DISABLE ROW LEVEL SECURITY;");
            }
        }
#pragma warning restore S1186
    }
}
