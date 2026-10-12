namespace Evolith.Ums.Application.Test.Tenants.ActivateTenant;

using Evolith.Ums.Application.Identity.Tenant.Commands;
using FluentValidation.TestHelper;

public class ActivateTenantCommandValidatorTests
{
    private readonly ActivateTenantCommandValidator _validator;

    public ActivateTenantCommandValidatorTests()
    {
        _validator = new ActivateTenantCommandValidator();
    }

    [Fact]
    public void TenantId_WhenEmptyGuid_HasValidationError()
    {
        var command = new ActivateTenantCommand(Guid.Empty);

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.TenantId);
    }

    [Fact]
    public void TenantId_WhenValidGuid_PassesValidation()
    {
        var command = new ActivateTenantCommand(Guid.NewGuid());

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(c => c.TenantId);
    }
}
