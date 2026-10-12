namespace Evolith.Ums.Application.Test.Common;

using Evolith.Ums.Application.Common;

public class DomainEnumerationParserTests
{
    #region FromName - Success Scenarios

    [Fact]
    public void FromName_WithValidOrganizationTypeInternal_ReturnsCorrectEnum()
    {
        var result = DomainEnumerationParser.FromName<OrganizationType>("INTERNAL");

        Assert.NotNull(result);
        Assert.Equal(OrganizationType.INTERNAL, result);
    }

    [Fact]
    public void FromName_WithValidOrganizationTypeSupplier_ReturnsCorrectEnum()
    {
        var result = DomainEnumerationParser.FromName<OrganizationType>("SUPPLIER");

        Assert.NotNull(result);
        Assert.Equal(OrganizationType.SUPPLIER, result);
    }

    [Fact]
    public void FromName_WithCaseInsensitiveName_ReturnsCorrectEnum()
    {
        var result = DomainEnumerationParser.FromName<OrganizationType>("internal");

        Assert.NotNull(result);
        Assert.Equal(OrganizationType.INTERNAL, result);
    }

    [Fact]
    public void FromName_WithMixedCaseName_ReturnsCorrectEnum()
    {
        var result = DomainEnumerationParser.FromName<OrganizationType>("Internal");

        Assert.NotNull(result);
        Assert.Equal(OrganizationType.INTERNAL, result);
    }

    [Fact]
    public void FromName_WithWhitespaceName_ReturnsCorrectEnum()
    {
        var result = DomainEnumerationParser.FromName<OrganizationType>(" INTERNAL ");

        Assert.NotNull(result);
        Assert.Equal(OrganizationType.INTERNAL, result);
    }

    [Fact]
    public void FromName_WithValidIdpStrategyAzureAd_ReturnsCorrectEnum()
    {
        var result = DomainEnumerationParser.FromName<IdpStrategy>("AzureAd");

        Assert.NotNull(result);
        Assert.Equal(IdpStrategy.AzureAd, result);
    }

    [Fact]
    public void FromName_WithValidIdpStrategyOkta_ReturnsCorrectEnum()
    {
        var result = DomainEnumerationParser.FromName<IdpStrategy>("Okta");

        Assert.NotNull(result);
        Assert.Equal(IdpStrategy.Okta, result);
    }

    [Fact]
    public void FromName_WithValidIdpStrategyKeycloak_ReturnsCorrectEnum()
    {
        var result = DomainEnumerationParser.FromName<IdpStrategy>("Keycloak");

        Assert.NotNull(result);
        Assert.Equal(IdpStrategy.Keycloak, result);
    }

    [Fact]
    public void FromName_WithValidIdpStrategyInternalBcrypt_ReturnsCorrectEnum()
    {
        var result = DomainEnumerationParser.FromName<IdpStrategy>("InternalBcrypt");

        Assert.NotNull(result);
        Assert.Equal(IdpStrategy.InternalBcrypt, result);
    }

    #endregion

    #region FromName - Failure Scenarios

    [Fact]
    public void FromName_WithNullName_ReturnsNull()
    {
        var result = DomainEnumerationParser.FromName<OrganizationType>(null);

        Assert.Null(result);
    }

    [Fact]
    public void FromName_WithEmptyName_ReturnsNull()
    {
        var result = DomainEnumerationParser.FromName<OrganizationType>("");

        Assert.Null(result);
    }

    [Fact]
    public void FromName_WithWhitespaceName_ReturnsNull()
    {
        var result = DomainEnumerationParser.FromName<OrganizationType>("   ");

        Assert.Null(result);
    }

    [Fact]
    public void FromName_WithInvalidName_ReturnsNull()
    {
        var result = DomainEnumerationParser.FromName<OrganizationType>("INVALID_TYPE");

        Assert.Null(result);
    }

    [Fact]
    public void FromName_WithInvalidIdpStrategy_ReturnsNull()
    {
        var result = DomainEnumerationParser.FromName<IdpStrategy>("INVALID_STRATEGY");

        Assert.Null(result);
    }

    #endregion
}
