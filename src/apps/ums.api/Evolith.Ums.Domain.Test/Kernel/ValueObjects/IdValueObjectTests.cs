namespace Evolith.Ums.Domain.Test.Kernel.ValueObjects;

using Xunit;

public class IdValueObjectTests
{
    private static readonly Guid TestGuid = Guid.Parse("12345678-1234-1234-1234-123456789abc");

    #region AccessEnforcementPolicyId

    [Fact]
    public void AccessEnforcementPolicyId_Create_GeneratesNewGuid()
    {
        var id1 = AccessEnforcementPolicyId.Create();
        var id2 = AccessEnforcementPolicyId.Create();

        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void AccessEnforcementPolicyId_Load_FromGuid_ReturnsSameGuid()
    {
        var id = AccessEnforcementPolicyId.Load(TestGuid);

        Assert.Equal(TestGuid, id.GetValue());
    }

    [Fact]
    public void AccessEnforcementPolicyId_Load_FromString_ReturnsParsedGuid()
    {
        var id = AccessEnforcementPolicyId.Load("12345678-1234-1234-1234-123456789abc");

        Assert.Equal(TestGuid, id.GetValue());
    }

    #endregion

    #region AppConfigurationId

    [Fact]
    public void AppConfigurationId_Create_GeneratesNewGuid()
    {
        var id1 = AppConfigurationId.Create();
        var id2 = AppConfigurationId.Create();

        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void AppConfigurationId_Load_FromGuid_ReturnsSameGuid()
    {
        var id = AppConfigurationId.Load(TestGuid);

        Assert.Equal(TestGuid, id.GetValue());
    }

    [Fact]
    public void AppConfigurationId_Load_FromString_ReturnsParsedGuid()
    {
        var id = AppConfigurationId.Load("12345678-1234-1234-1234-123456789abc");

        Assert.Equal(TestGuid, id.GetValue());
    }

    #endregion

    #region ApprovalRequestId

    [Fact]
    public void ApprovalRequestId_Create_GeneratesNewGuid()
    {
        var id1 = ApprovalRequestId.Create();
        var id2 = ApprovalRequestId.Create();

        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void ApprovalRequestId_Load_FromGuid_ReturnsSameGuid()
    {
        var id = ApprovalRequestId.Load(TestGuid);

        Assert.Equal(TestGuid, id.GetValue());
    }

    [Fact]
    public void ApprovalRequestId_Load_FromString_ReturnsParsedGuid()
    {
        var id = ApprovalRequestId.Load("12345678-1234-1234-1234-123456789abc");

        Assert.Equal(TestGuid, id.GetValue());
    }

    #endregion

    #region ApprovalRequiredDocumentId

    [Fact]
    public void ApprovalRequiredDocumentId_Create_GeneratesNewGuid()
    {
        var id1 = ApprovalRequiredDocumentId.Create();
        var id2 = ApprovalRequiredDocumentId.Create();

        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void ApprovalRequiredDocumentId_Load_FromGuid_ReturnsSameGuid()
    {
        var id = ApprovalRequiredDocumentId.Load(TestGuid);

        Assert.Equal(TestGuid, id.GetValue());
    }

    [Fact]
    public void ApprovalRequiredDocumentId_Load_FromString_ReturnsParsedGuid()
    {
        var id = ApprovalRequiredDocumentId.Load("12345678-1234-1234-1234-123456789abc");

        Assert.Equal(TestGuid, id.GetValue());
    }

    #endregion

    #region AuditRecordId

    [Fact]
    public void AuditRecordId_Create_GeneratesNewGuid()
    {
        var id1 = AuditRecordId.Create();
        var id2 = AuditRecordId.Create();

        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void AuditRecordId_Load_FromGuid_ReturnsSameGuid()
    {
        var id = AuditRecordId.Load(TestGuid);

        Assert.Equal(TestGuid, id.GetValue());
    }

    [Fact]
    public void AuditRecordId_Load_FromString_ReturnsParsedGuid()
    {
        var id = AuditRecordId.Load("12345678-1234-1234-1234-123456789abc");

        Assert.Equal(TestGuid, id.GetValue());
    }

    #endregion

    #region DelegationId

    [Fact]
    public void DelegationId_Create_GeneratesNewGuid()
    {
        var id1 = DelegationId.Create();
        var id2 = DelegationId.Create();

        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void DelegationId_Load_FromGuid_ReturnsSameGuid()
    {
        var id = DelegationId.Load(TestGuid);

        Assert.Equal(TestGuid, id.GetValue());
    }

    [Fact]
    public void DelegationId_Load_FromString_ReturnsParsedGuid()
    {
        var id = DelegationId.Load("12345678-1234-1234-1234-123456789abc");

        Assert.Equal(TestGuid, id.GetValue());
    }

    #endregion

    #region FeatureFlagId

    [Fact]
    public void FeatureFlagId_Create_GeneratesNewGuid()
    {
        var id1 = FeatureFlagId.Create();
        var id2 = FeatureFlagId.Create();

        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void FeatureFlagId_Load_FromGuid_ReturnsSameGuid()
    {
        var id = FeatureFlagId.Load(TestGuid);

        Assert.Equal(TestGuid, id.GetValue());
    }

    [Fact]
    public void FeatureFlagId_Load_FromString_ReturnsParsedGuid()
    {
        var id = FeatureFlagId.Load("12345678-1234-1234-1234-123456789abc");

        Assert.Equal(TestGuid, id.GetValue());
    }

    #endregion

    #region IdpConfigurationId

    [Fact]
    public void IdpConfigurationId_Create_GeneratesNewGuid()
    {
        var id1 = IdpConfigurationId.Create();
        var id2 = IdpConfigurationId.Create();

        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void IdpConfigurationId_Load_FromGuid_ReturnsSameGuid()
    {
        var id = IdpConfigurationId.Load(TestGuid);

        Assert.Equal(TestGuid, id.GetValue());
    }

    [Fact]
    public void IdpConfigurationId_Load_FromString_ReturnsParsedGuid()
    {
        var id = IdpConfigurationId.Load("12345678-1234-1234-1234-123456789abc");

        Assert.Equal(TestGuid, id.GetValue());
    }

    #endregion

    #region MfaEnrollmentId

    [Fact]
    public void MfaEnrollmentId_Create_GeneratesNewGuid()
    {
        var id1 = MfaEnrollmentId.Create();
        var id2 = MfaEnrollmentId.Create();

        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void MfaEnrollmentId_Load_FromGuid_ReturnsSameGuid()
    {
        var id = MfaEnrollmentId.Load(TestGuid);

        Assert.Equal(TestGuid, id.GetValue());
    }

    [Fact]
    public void MfaEnrollmentId_Load_FromString_ReturnsParsedGuid()
    {
        var id = MfaEnrollmentId.Load("12345678-1234-1234-1234-123456789abc");

        Assert.Equal(TestGuid, id.GetValue());
    }

    #endregion

    #region NotificationRuleId

    [Fact]
    public void NotificationRuleId_Create_GeneratesNewGuid()
    {
        var id1 = NotificationRuleId.Create();
        var id2 = NotificationRuleId.Create();

        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void NotificationRuleId_Load_FromGuid_ReturnsSameGuid()
    {
        var id = NotificationRuleId.Load(TestGuid);

        Assert.Equal(TestGuid, id.GetValue());
    }

    [Fact]
    public void NotificationRuleId_Load_FromString_ReturnsParsedGuid()
    {
        var id = NotificationRuleId.Load("12345678-1234-1234-1234-123456789abc");

        Assert.Equal(TestGuid, id.GetValue());
    }

    #endregion

    #region OptionId

    [Fact]
    public void OptionId_Create_GeneratesNewGuid()
    {
        var id1 = OptionId.Create();
        var id2 = OptionId.Create();

        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void OptionId_Load_FromGuid_ReturnsSameGuid()
    {
        var id = OptionId.Load(TestGuid);

        Assert.Equal(TestGuid, id.GetValue());
    }

    [Fact]
    public void OptionId_Load_FromString_ReturnsParsedGuid()
    {
        var id = OptionId.Load("12345678-1234-1234-1234-123456789abc");

        Assert.Equal(TestGuid, id.GetValue());
    }

    #endregion

    #region OrganizationId

    [Fact]
    public void OrganizationId_Create_GeneratesNewGuid()
    {
        var id1 = OrganizationId.Create();
        var id2 = OrganizationId.Create();

        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void OrganizationId_Load_FromGuid_ReturnsSameGuid()
    {
        var id = OrganizationId.Load(TestGuid);

        Assert.Equal(TestGuid, id.GetValue());
    }

    [Fact]
    public void OrganizationId_Load_FromString_ReturnsParsedGuid()
    {
        var id = OrganizationId.Load("12345678-1234-1234-1234-123456789abc");

        Assert.Equal(TestGuid, id.GetValue());
    }

    #endregion

    #region PasswordCredentialId

    [Fact]
    public void PasswordCredentialId_Create_GeneratesNewGuid()
    {
        var id1 = PasswordCredentialId.Create();
        var id2 = PasswordCredentialId.Create();

        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void PasswordCredentialId_Load_FromGuid_ReturnsSameGuid()
    {
        var id = PasswordCredentialId.Load(TestGuid);

        Assert.Equal(TestGuid, id.GetValue());
    }

    [Fact]
    public void PasswordCredentialId_Load_FromString_ReturnsParsedGuid()
    {
        var id = PasswordCredentialId.Load("12345678-1234-1234-1234-123456789abc");

        Assert.Equal(TestGuid, id.GetValue());
    }

    #endregion

    #region PermissionTemplateItemId

    [Fact]
    public void PermissionTemplateItemId_Create_GeneratesNewGuid()
    {
        var id1 = PermissionTemplateItemId.Create();
        var id2 = PermissionTemplateItemId.Create();

        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void PermissionTemplateItemId_Load_FromGuid_ReturnsSameGuid()
    {
        var id = PermissionTemplateItemId.Load(TestGuid);

        Assert.Equal(TestGuid, id.GetValue());
    }

    [Fact]
    public void PermissionTemplateItemId_Load_FromString_ReturnsParsedGuid()
    {
        var id = PermissionTemplateItemId.Load("12345678-1234-1234-1234-123456789abc");

        Assert.Equal(TestGuid, id.GetValue());
    }

    #endregion

    #region ProfileId

    [Fact]
    public void ProfileId_Create_GeneratesNewGuid()
    {
        var id1 = ProfileId.Create();
        var id2 = ProfileId.Create();

        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void ProfileId_Load_FromGuid_ReturnsSameGuid()
    {
        var id = ProfileId.Load(TestGuid);

        Assert.Equal(TestGuid, id.GetValue());
    }

    [Fact]
    public void ProfileId_Load_FromString_ReturnsParsedGuid()
    {
        var id = ProfileId.Load("12345678-1234-1234-1234-123456789abc");

        Assert.Equal(TestGuid, id.GetValue());
    }

    #endregion

    #region ProfilePermissionId

    [Fact]
    public void ProfilePermissionId_Create_GeneratesNewGuid()
    {
        var id1 = ProfilePermissionId.Create();
        var id2 = ProfilePermissionId.Create();

        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void ProfilePermissionId_Load_FromGuid_ReturnsSameGuid()
    {
        var id = ProfilePermissionId.Load(TestGuid);

        Assert.Equal(TestGuid, id.GetValue());
    }

    [Fact]
    public void ProfilePermissionId_Load_FromString_ReturnsParsedGuid()
    {
        var id = ProfilePermissionId.Load("12345678-1234-1234-1234-123456789abc");

        Assert.Equal(TestGuid, id.GetValue());
    }

    #endregion

    #region PromotionImpactAnalysisId

    [Fact]
    public void PromotionImpactAnalysisId_Create_GeneratesNewGuid()
    {
        var id1 = PromotionImpactAnalysisId.Create();
        var id2 = PromotionImpactAnalysisId.Create();

        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void PromotionImpactAnalysisId_Load_FromGuid_ReturnsSameGuid()
    {
        var id = PromotionImpactAnalysisId.Load(TestGuid);

        Assert.Equal(TestGuid, id.GetValue());
    }

    [Fact]
    public void PromotionImpactAnalysisId_Load_FromString_ReturnsParsedGuid()
    {
        var id = PromotionImpactAnalysisId.Load("12345678-1234-1234-1234-123456789abc");

        Assert.Equal(TestGuid, id.GetValue());
    }

    #endregion

    #region UserDocumentId

    [Fact]
    public void UserDocumentId_Create_GeneratesNewGuid()
    {
        var id1 = UserDocumentId.Create();
        var id2 = UserDocumentId.Create();

        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void UserDocumentId_Load_FromGuid_ReturnsSameGuid()
    {
        var id = UserDocumentId.Load(TestGuid);

        Assert.Equal(TestGuid, id.GetValue());
    }

    [Fact]
    public void UserDocumentId_Load_FromString_ReturnsParsedGuid()
    {
        var id = UserDocumentId.Load("12345678-1234-1234-1234-123456789abc");

        Assert.Equal(TestGuid, id.GetValue());
    }

    #endregion

    #region RoleId

    [Fact]
    public void RoleId_Create_GeneratesNewGuid()
    {
        var id1 = RoleId.Create();
        var id2 = RoleId.Create();

        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void RoleId_Load_FromGuid_ReturnsSameGuid()
    {
        var id = RoleId.Load(TestGuid);

        Assert.Equal(TestGuid, id.GetValue());
    }

    [Fact]
    public void RoleId_Load_FromString_ReturnsParsedGuid()
    {
        var id = RoleId.Load("12345678-1234-1234-1234-123456789abc");

        Assert.Equal(TestGuid, id.GetValue());
    }

    #endregion

    #region UserId

    [Fact]
    public void UserId_Create_GeneratesNewGuid()
    {
        var id1 = UserId.Create();
        var id2 = UserId.Create();

        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void UserId_Load_FromGuid_ReturnsSameGuid()
    {
        var id = UserId.Load(TestGuid);

        Assert.Equal(TestGuid, id.GetValue());
    }

    [Fact]
    public void UserId_Load_FromString_ReturnsParsedGuid()
    {
        var id = UserId.Load("12345678-1234-1234-1234-123456789abc");

        Assert.Equal(TestGuid, id.GetValue());
    }

    #endregion

    #region SystemId

    [Fact]
    public void SystemId_Create_GeneratesNewGuid()
    {
        var id1 = SystemId.Create();
        var id2 = SystemId.Create();

        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void SystemId_Load_FromGuid_ReturnsSameGuid()
    {
        var id = SystemId.Load(TestGuid);

        Assert.Equal(TestGuid, id.GetValue());
    }

    [Fact]
    public void SystemId_Load_FromString_ReturnsParsedGuid()
    {
        var id = SystemId.Load("12345678-1234-1234-1234-123456789abc");

        Assert.Equal(TestGuid, id.GetValue());
    }

    #endregion

    #region TemplateId

    [Fact]
    public void TemplateId_Create_GeneratesNewGuid()
    {
        var id1 = TemplateId.Create();
        var id2 = TemplateId.Create();

        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void TemplateId_Load_FromGuid_ReturnsSameGuid()
    {
        var id = TemplateId.Load(TestGuid);

        Assert.Equal(TestGuid, id.GetValue());
    }

    [Fact]
    public void TemplateId_Load_FromString_ReturnsParsedGuid()
    {
        var id = TemplateId.Load("12345678-1234-1234-1234-123456789abc");

        Assert.Equal(TestGuid, id.GetValue());
    }

    #endregion

    #region ActionId

    [Fact]
    public void ActionId_Create_GeneratesNewGuid()
    {
        var id1 = ActionId.Create();
        var id2 = ActionId.Create();

        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void ActionId_Load_FromGuid_ReturnsSameGuid()
    {
        var id = ActionId.Load(TestGuid);

        Assert.Equal(TestGuid, id.GetValue());
    }

    [Fact]
    public void ActionId_Load_FromString_ReturnsParsedGuid()
    {
        var id = ActionId.Load("12345678-1234-1234-1234-123456789abc");

        Assert.Equal(TestGuid, id.GetValue());
    }

    #endregion

    #region BranchId

    [Fact]
    public void BranchId_Create_GeneratesNewGuid()
    {
        var id1 = BranchId.Create();
        var id2 = BranchId.Create();

        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void BranchId_Load_FromGuid_ReturnsSameGuid()
    {
        var id = BranchId.Load(TestGuid);

        Assert.Equal(TestGuid, id.GetValue());
    }

    [Fact]
    public void BranchId_Load_FromString_ReturnsParsedGuid()
    {
        var id = BranchId.Load("12345678-1234-1234-1234-123456789abc");

        Assert.Equal(TestGuid, id.GetValue());
    }

    #endregion

    #region DocumentTypeId

    [Fact]
    public void DocumentTypeId_Create_GeneratesNewGuid()
    {
        var id1 = DocumentTypeId.Create();
        var id2 = DocumentTypeId.Create();

        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void DocumentTypeId_Load_FromGuid_ReturnsSameGuid()
    {
        var id = DocumentTypeId.Load(TestGuid);

        Assert.Equal(TestGuid, id.GetValue());
    }

    [Fact]
    public void DocumentTypeId_Load_FromString_ReturnsParsedGuid()
    {
        var id = DocumentTypeId.Load("12345678-1234-1234-1234-123456789abc");

        Assert.Equal(TestGuid, id.GetValue());
    }

    #endregion

    #region IdentityProviderId

    [Fact]
    public void IdentityProviderId_Create_GeneratesNewGuid()
    {
        var id1 = IdentityProviderId.Create();
        var id2 = IdentityProviderId.Create();

        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void IdentityProviderId_Load_FromGuid_ReturnsSameGuid()
    {
        var id = IdentityProviderId.Load(TestGuid);

        Assert.Equal(TestGuid, id.GetValue());
    }

    [Fact]
    public void IdentityProviderId_Load_FromString_ReturnsParsedGuid()
    {
        var id = IdentityProviderId.Load("12345678-1234-1234-1234-123456789abc");

        Assert.Equal(TestGuid, id.GetValue());
    }

    #endregion

    #region MenuId

    [Fact]
    public void MenuId_Create_GeneratesNewGuid()
    {
        var id1 = MenuId.Create();
        var id2 = MenuId.Create();

        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void MenuId_Load_FromGuid_ReturnsSameGuid()
    {
        var id = MenuId.Load(TestGuid);

        Assert.Equal(TestGuid, id.GetValue());
    }

    [Fact]
    public void MenuId_Load_FromString_ReturnsParsedGuid()
    {
        var id = MenuId.Load("12345678-1234-1234-1234-123456789abc");

        Assert.Equal(TestGuid, id.GetValue());
    }

    #endregion

    #region ModuleId

    [Fact]
    public void ModuleId_Create_GeneratesNewGuid()
    {
        var id1 = ModuleId.Create();
        var id2 = ModuleId.Create();

        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void ModuleId_Load_FromGuid_ReturnsSameGuid()
    {
        var id = ModuleId.Load(TestGuid);

        Assert.Equal(TestGuid, id.GetValue());
    }

    [Fact]
    public void ModuleId_Load_FromString_ReturnsParsedGuid()
    {
        var id = ModuleId.Load("12345678-1234-1234-1234-123456789abc");

        Assert.Equal(TestGuid, id.GetValue());
    }

    #endregion

    #region SubMenuId

    [Fact]
    public void SubMenuId_Create_GeneratesNewGuid()
    {
        var id1 = SubMenuId.Create();
        var id2 = SubMenuId.Create();

        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void SubMenuId_Load_FromGuid_ReturnsSameGuid()
    {
        var id = SubMenuId.Load(TestGuid);

        Assert.Equal(TestGuid, id.GetValue());
    }

    [Fact]
    public void SubMenuId_Load_FromString_ReturnsParsedGuid()
    {
        var id = SubMenuId.Load("12345678-1234-1234-1234-123456789abc");

        Assert.Equal(TestGuid, id.GetValue());
    }

    #endregion

    #region SystemSuiteId

    [Fact]
    public void SystemSuiteId_Create_GeneratesNewGuid()
    {
        var id1 = SystemSuiteId.Create();
        var id2 = SystemSuiteId.Create();

        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void SystemSuiteId_Load_FromGuid_ReturnsSameGuid()
    {
        var id = SystemSuiteId.Load(TestGuid);

        Assert.Equal(TestGuid, id.GetValue());
    }

    [Fact]
    public void SystemSuiteId_Load_FromString_ReturnsParsedGuid()
    {
        var id = SystemSuiteId.Load("12345678-1234-1234-1234-123456789abc");

        Assert.Equal(TestGuid, id.GetValue());
    }

    #endregion

    #region UserAccountId

    [Fact]
    public void UserAccountId_Create_GeneratesNewGuid()
    {
        var id1 = UserAccountId.Create();
        var id2 = UserAccountId.Create();

        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void UserAccountId_Load_FromGuid_ReturnsSameGuid()
    {
        var id = UserAccountId.Load(TestGuid);

        Assert.Equal(TestGuid, id.GetValue());
    }

    [Fact]
    public void UserAccountId_Load_FromString_ReturnsParsedGuid()
    {
        var id = UserAccountId.Load("12345678-1234-1234-1234-123456789abc");

        Assert.Equal(TestGuid, id.GetValue());
    }

    #endregion

    #region ApprovalWorkflowId

    [Fact]
    public void ApprovalWorkflowId_Create_GeneratesNewGuid()
    {
        var id1 = ApprovalWorkflowId.Create();
        var id2 = ApprovalWorkflowId.Create();

        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void ApprovalWorkflowId_Load_FromGuid_ReturnsSameGuid()
    {
        var id = ApprovalWorkflowId.Load(TestGuid);

        Assert.Equal(TestGuid, id.GetValue());
    }

    [Fact]
    public void ApprovalWorkflowId_Load_FromString_ReturnsParsedGuid()
    {
        var id = ApprovalWorkflowId.Load("12345678-1234-1234-1234-123456789abc");

        Assert.Equal(TestGuid, id.GetValue());
    }

    #endregion

    #region ActorId

    [Fact]
    public void ActorId_Create_WithValidString_ReturnsActorId()
    {
        var id = ActorId.Create("user-001");

        Assert.Equal("user-001", id.GetValue());
    }

    [Fact]
    public void ActorId_Create_TrimsValue()
    {
        var id = ActorId.Create("  user-001  ");

        Assert.Equal("user-001", id.GetValue());
    }

    [Fact]
    public void ActorId_Create_WithNull_ReturnsEmptyActorId()
    {
        var id = ActorId.Create(null!);

        Assert.Equal(string.Empty, id.GetValue());
    }

    [Fact]
    public void ActorId_Default_ReturnsEmptyActorId()
    {
        var id = ActorId.Default();

        Assert.Equal(string.Empty, id.GetValue());
    }

    #endregion
}
