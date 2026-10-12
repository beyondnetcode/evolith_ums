namespace Evolith.Ums.Application.Test.Tenants.SuspendTenant;

using FluentValidation.TestHelper;
using Evolith.Ums.Application.Identity.Tenant.Commands;

public class SuspendTenantCommandValidatorTests
{
    private readonly SuspendTenantCommandValidator _validator;

    public SuspendTenantCommandValidatorTests()
    {
        _validator = new SuspendTenantCommandValidator();
    }

    [Fact]
    public void TenantId_WhenEmptyGuid_HasValidationError()
    {
        var command = new SuspendTenantCommand(Guid.Empty);

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.TenantId);
    }

    [Fact]
    public void TenantId_WhenValidGuid_PassesValidation()
    {
        var command = new SuspendTenantCommand(Guid.NewGuid());

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(c => c.TenantId);
    }
}
