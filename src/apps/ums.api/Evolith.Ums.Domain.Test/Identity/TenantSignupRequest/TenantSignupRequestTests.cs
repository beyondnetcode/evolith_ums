namespace Evolith.Ums.Domain.Test.Identity.TenantSignupRequest;

using BeyondNetCode.Shell.Ddd.ValueObjects.Audit;
using Evolith.Ums.Domain.Identity.TenantSignupRequest;
using Xunit;

public class TenantSignupRequestTests
{
    private static readonly Name ValidCompanyName = Name.Create("Comercializadora del Sur S.A.");
    private static readonly CompanyReference ValidCompanyReference = CompanyReference.Create("RUC-20100412447");
    private static readonly Name ValidContactName = Name.Create("Ana Torres");
    private static readonly Email ValidContactEmail = Email.Create("ana.torres@empresa.com");
    private static readonly ActorId ValidActor = ActorId.Create("user-001");

    #region Create

    [Fact]
    public void Create_WithValidData_ReturnsSuccessInPendingStatus()
    {
        var result = TenantSignupRequest.Create(
            ValidCompanyName, ValidCompanyReference, ValidContactName, ValidContactEmail, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(TenantSignupRequestStatus.Pending, result.Value.Status);
        Assert.Null(result.Value.ApprovedTenantId);
        Assert.Equal(ValidCompanyName, result.Value.CompanyName);
        Assert.Equal(ValidContactEmail, result.Value.ContactEmail);
    }

    [Fact]
    public void Create_WithEmptyCompanyName_ReturnsFailure()
    {
        var result = TenantSignupRequest.Create(
            Name.Create(""), ValidCompanyReference, ValidContactName, ValidContactEmail, ValidActor);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Create_WithEmptyContactName_ReturnsFailure()
    {
        var result = TenantSignupRequest.Create(
            ValidCompanyName, ValidCompanyReference, Name.Create(""), ValidContactEmail, ValidActor);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Create_WithInvalidContactEmail_ReturnsFailure()
    {
        var result = TenantSignupRequest.Create(
            ValidCompanyName, ValidCompanyReference, ValidContactName, Email.Create("no-es-un-correo"), ValidActor);

        Assert.True(result.IsFailure);
    }

    #endregion

    #region Approve

    [Fact]
    public void Approve_WhenPending_TransitionsToApprovedAndFixesTenantId()
    {
        var request = TenantSignupRequest.Create(
            ValidCompanyName, ValidCompanyReference, ValidContactName, ValidContactEmail, ValidActor).Value;
        var tenantId = TenantId.Create();

        var result = request.Approve(tenantId, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(TenantSignupRequestStatus.Approved, request.Status);
        Assert.NotNull(request.ApprovedTenantId);
        Assert.Equal(tenantId.GetValue(), request.ApprovedTenantId!.GetValue());
    }

    [Fact]
    public void Approve_WhenAlreadyApproved_ReturnsFailureAndKeepsFirstTenantId()
    {
        var request = TenantSignupRequest.Create(
            ValidCompanyName, ValidCompanyReference, ValidContactName, ValidContactEmail, ValidActor).Value;
        var firstTenantId = TenantId.Create();
        request.Approve(firstTenantId, ValidActor);

        var secondTenantId = TenantId.Create();
        var result = request.Approve(secondTenantId, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Tenant.SignupRequestNotPending, result.Error);
        Assert.Equal(TenantSignupRequestStatus.Approved, request.Status);
        Assert.Equal(firstTenantId.GetValue(), request.ApprovedTenantId!.GetValue());
    }

    [Fact]
    public void Approve_WhenRejected_ReturnsFailure()
    {
        var props = new TenantSignupRequestProps(
            IdValueObject.Create(),
            ValidCompanyName,
            ValidCompanyReference,
            ValidContactName,
            ValidContactEmail,
            TenantSignupRequestStatus.Rejected,
            null,
            AuditValueObject.Create(ValidActor.GetValue()));
        var request = new TenantSignupRequest(props);

        var result = request.Approve(TenantId.Create(), ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Tenant.SignupRequestNotPending, result.Error);
        Assert.Null(request.ApprovedTenantId);
    }

    #endregion
}
