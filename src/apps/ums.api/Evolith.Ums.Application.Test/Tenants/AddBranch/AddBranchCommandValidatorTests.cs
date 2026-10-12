namespace Evolith.Ums.Application.Test.Tenants.AddBranch;

using Evolith.Ums.Application.Identity.Tenant.Branch.Commands;
using FluentValidation.TestHelper;

public class AddBranchCommandValidatorTests
{
    private readonly AddBranchCommandValidator _validator;

    public AddBranchCommandValidatorTests()
    {
        _validator = new AddBranchCommandValidator();
    }

    #region TenantId Validation

    [Fact]
    public void TenantId_WhenEmptyGuid_HasValidationError()
    {
        var command = new AddBranchCommand(Guid.Empty, "BR-001", "Branch One", null);

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.TenantId);
    }

    [Fact]
    public void TenantId_WhenValidGuid_PassesValidation()
    {
        var command = new AddBranchCommand(Guid.NewGuid(), "BR-001", "Branch One", null);

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(c => c.TenantId);
    }

    #endregion

    #region Code Validation

    [Fact]
    public void Code_WhenEmpty_HasValidationError()
    {
        var command = new AddBranchCommand(Guid.NewGuid(), "", "Branch One", null);

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Code);
    }

    [Fact]
    public void Code_WhenExceedsMaxLength_HasValidationError()
    {
        var command = new AddBranchCommand(Guid.NewGuid(), new string('A', 51), "Branch One", null);

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Code);
    }

    [Fact]
    public void Code_WhenValid_PassesValidation()
    {
        var command = new AddBranchCommand(Guid.NewGuid(), "BR-001", "Branch One", null);

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(c => c.Code);
    }

    #endregion

    #region Name Validation

    [Fact]
    public void Name_WhenEmpty_HasValidationError()
    {
        var command = new AddBranchCommand(Guid.NewGuid(), "BR-001", "", null);

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Name);
    }

    [Fact]
    public void Name_WhenExceedsMaxLength_HasValidationError()
    {
        var command = new AddBranchCommand(Guid.NewGuid(), "BR-001", new string('A', 151), null);

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Name);
    }

    [Fact]
    public void Name_WhenValid_PassesValidation()
    {
        var command = new AddBranchCommand(Guid.NewGuid(), "BR-001", "Branch One", null);

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(c => c.Name);
    }

    #endregion

    #region GeofencingMetadata Validation

    [Fact]
    public void GeofencingMetadata_WhenExceedsMaxLength_HasValidationError()
    {
        var command = new AddBranchCommand(Guid.NewGuid(), "BR-001", "Branch One", new string('A', 1001));

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.GeofencingMetadata);
    }

    [Fact]
    public void GeofencingMetadata_WhenNull_PassesValidation()
    {
        var command = new AddBranchCommand(Guid.NewGuid(), "BR-001", "Branch One", null);

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(c => c.GeofencingMetadata);
    }

    [Fact]
    public void GeofencingMetadata_WhenValid_PassesValidation()
    {
        var command = new AddBranchCommand(Guid.NewGuid(), "BR-001", "Branch One", "{\"lat\": 40.7128}");

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(c => c.GeofencingMetadata);
    }

    #endregion

    #region Full Command Validation

    [Fact]
    public void Validate_WithAllValidFields_ReturnsValid()
    {
        var command = new AddBranchCommand(Guid.NewGuid(), "BR-001", "Branch One", "{\"lat\": 40.7128}");

        var result = _validator.TestValidate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WithMultipleErrors_ReturnsAllErrors()
    {
        var command = new AddBranchCommand(Guid.Empty, "", "", new string('A', 2000));

        var result = _validator.TestValidate(command);

        Assert.True(result.Errors.Count >= 4);
    }

    #endregion
}
