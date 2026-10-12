namespace Evolith.Ums.Domain.Test.Kernel.ValueObjects;

using Xunit;

public class GenericStringValidatorTests
{
    [Fact]
    public void AddRules_WithRequiredAndEmptyValue_AddsBrokenRule()
    {
        var vo = Name.Create("");
        var validator = new GenericStringValidator(vo, nameof(Name), isRequired: true);

        validator.AddRules(null);
        var brokenRules = vo.BrokenRules.GetBrokenRules();

        Assert.NotEmpty(brokenRules);
        Assert.Contains(DomainErrors.ValueObject.PropertyRequired, brokenRules[0].Message);
    }

    [Fact]
    public void AddRules_WithRequiredAndWhitespaceValue_AddsBrokenRule()
    {
        var vo = Name.Create("   ");
        var validator = new GenericStringValidator(vo, nameof(Name), isRequired: true);

        validator.AddRules(null);
        var brokenRules = vo.BrokenRules.GetBrokenRules();

        Assert.NotEmpty(brokenRules);
    }

    [Fact]
    public void AddRules_WithRequiredAndValidValue_NoBrokenRules()
    {
        var vo = Name.Create("Valid Name");
        var validator = new GenericStringValidator(vo, nameof(Name), isRequired: true);

        validator.AddRules(null);
        var brokenRules = vo.BrokenRules.GetBrokenRules();

        Assert.Empty(brokenRules);
    }

    [Fact]
    public void AddRules_WithNotRequiredAndEmptyValue_NoBrokenRules()
    {
        var vo = Description.Create("");
        var validator = new GenericStringValidator(vo, nameof(Description), isRequired: false);

        validator.AddRules(null);
        var brokenRules = vo.BrokenRules.GetBrokenRules();

        Assert.Empty(brokenRules);
    }

    [Fact]
    public void AddRules_WithNotRequiredAndValidValue_NoBrokenRules()
    {
        var vo = Description.Create("Valid description");
        var validator = new GenericStringValidator(vo, nameof(Description), isRequired: false);

        validator.AddRules(null);
        var brokenRules = vo.BrokenRules.GetBrokenRules();

        Assert.Empty(brokenRules);
    }
}
