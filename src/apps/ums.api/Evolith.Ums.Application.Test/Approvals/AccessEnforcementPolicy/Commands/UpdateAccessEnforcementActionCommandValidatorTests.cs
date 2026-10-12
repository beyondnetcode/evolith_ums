namespace Evolith.Ums.Application.Test.Approvals.AccessEnforcementPolicy.Commands;

using Evolith.Ums.Application.Approvals.AccessEnforcementPolicy.Commands;
using FluentValidation.TestHelper;
using Xunit;

public sealed class UpdateAccessEnforcementActionCommandValidatorTests
{
    private readonly UpdateAccessEnforcementActionCommandValidator _validator = new();

    [Theory]
    [InlineData("BlockUser")]
    [InlineData("RestrictProfile")]
    [InlineData("LogOnly")]
    [InlineData("blockuser")]
    public void NewAction_AllowsConfiguredDomainValues(string action)
    {
        var command = new UpdateAccessEnforcementActionCommand(Guid.NewGuid(), action);

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.NewAction);
    }

    [Theory]
    [InlineData("AuditOnly")]
    [InlineData("SoftBlock")]
    [InlineData("HardBlock")]
    public void NewAction_RejectsLegacyLabels(string action)
    {
        var command = new UpdateAccessEnforcementActionCommand(Guid.NewGuid(), action);

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.NewAction)
            .WithErrorMessage("NewAction must be one of: BlockUser, RestrictProfile, LogOnly.");
    }
}
