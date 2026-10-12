namespace Evolith.Ums.Domain.Test.Configuration.IdpConfiguration;

using Evolith.Ums.Domain.Configuration.IdpConfiguration;
using Xunit;

public class IdpConfigurationTests
{
    private static readonly TenantId ValidTenantId = TenantId.Load(Guid.NewGuid().ToString());
    private static readonly SystemSuiteId ValidSystemSuiteId = SystemSuiteId.Load(Guid.NewGuid().ToString());
    private static readonly ProviderType ValidProviderType = ProviderType.GenericOidc;
    private static readonly string[] ValidDomainHints = new[] { "example.com" };
    private static readonly string ValidConfigPayload = "{\"issuer\": \"https://example.com\"}";
    private static readonly string ValidSecretRef = "vault/secret/idp"; // gitleaks:allow -- fake secret reference used by a unit test
    private static readonly int ValidResolutionPriority = 1;
    private static readonly ActorId ValidActor = ActorId.Create("user-001");
    #region Create

    [Fact]
    public void Create_WithValidData_ReturnsSuccess()
    {
        var result = IdpConfiguration.Create(
            ValidTenantId, ValidSystemSuiteId, ValidProviderType, ValidDomainHints,
            ValidConfigPayload, ValidSecretRef, ValidResolutionPriority, null, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(ValidTenantId, result.Value.TenantId);
        Assert.Equal(ValidSystemSuiteId, result.Value.SystemSuiteId);
        Assert.Equal(ValidProviderType, result.Value.ProviderType);
        Assert.Equal(IdpConfigStatus.Draft, result.Value.Status);
        Assert.Equal(ValidResolutionPriority, result.Value.ResolutionPriority);
    }

    [Fact]
    public void Create_WithEmptyConfigPayload_ReturnsFailure()
    {
        var result = IdpConfiguration.Create(
            ValidTenantId, ValidSystemSuiteId, ValidProviderType, ValidDomainHints,
            "", ValidSecretRef, ValidResolutionPriority, null, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Configuration.IdpConfigPayloadInvalid, result.Error);
    }

    [Fact]
    public void Create_WithWhitespaceConfigPayload_ReturnsFailure()
    {
        var result = IdpConfiguration.Create(
            ValidTenantId, ValidSystemSuiteId, ValidProviderType, ValidDomainHints,
            "   ", ValidSecretRef, ValidResolutionPriority, null, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Configuration.IdpConfigPayloadInvalid, result.Error);
    }

    [Fact]
    public void Create_WithFallbackId_ReturnsSuccess()
    {
        var fallbackId = Guid.NewGuid();

        var result = IdpConfiguration.Create(
            ValidTenantId, ValidSystemSuiteId, ValidProviderType, ValidDomainHints,
            ValidConfigPayload, ValidSecretRef, ValidResolutionPriority, fallbackId, ValidActor);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Create_RaisesIdpConfigRegisteredEvent()
    {
        var result = IdpConfiguration.Create(
            ValidTenantId, ValidSystemSuiteId, ValidProviderType, ValidDomainHints,
            ValidConfigPayload, ValidSecretRef, ValidResolutionPriority, null, ValidActor);

        Assert.True(result.IsSuccess);
        var events = result.Value.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Single(events);
        Assert.IsType<IdpConfigRegisteredEvent>(events[0]);
    }

    #endregion

    #region Activate

    [Fact]
    public void Activate_WhenNotActive_ReturnsSuccess()
    {
        var config = IdpConfiguration.Create(
            ValidTenantId, ValidSystemSuiteId, ValidProviderType, ValidDomainHints,
            ValidConfigPayload, ValidSecretRef, ValidResolutionPriority, null, ValidActor).Value;

        var result = config.Activate(ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(IdpConfigStatus.Active, config.Status);
    }

    [Fact]
    public void Activate_WhenAlreadyActive_ReturnsFailure()
    {
        var config = IdpConfiguration.Create(
            ValidTenantId, ValidSystemSuiteId, ValidProviderType, ValidDomainHints,
            ValidConfigPayload, ValidSecretRef, ValidResolutionPriority, null, ValidActor).Value;
        config.Activate(ValidActor);

        var result = config.Activate(ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Configuration.IdpConfigAlreadyActive, result.Error);
    }

    [Fact]
    public void Activate_RaisesIdpConfigActivatedEvent()
    {
        var config = IdpConfiguration.Create(
            ValidTenantId, ValidSystemSuiteId, ValidProviderType, ValidDomainHints,
            ValidConfigPayload, ValidSecretRef, ValidResolutionPriority, null, ValidActor).Value;

        config.Activate(ValidActor);

        var events = config.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Contains(events, e => e is IdpConfigActivatedEvent);
    }

    #endregion

    #region Deactivate

    [Fact]
    public void Deactivate_WhenActive_ReturnsSuccess()
    {
        var config = IdpConfiguration.Create(
            ValidTenantId, ValidSystemSuiteId, ValidProviderType, ValidDomainHints,
            ValidConfigPayload, ValidSecretRef, ValidResolutionPriority, null, ValidActor).Value;
        config.Activate(ValidActor);

        var result = config.Deactivate(ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(IdpConfigStatus.Inactive, config.Status);
    }

    [Fact]
    public void Deactivate_WhenAlreadyInactive_ReturnsFailure()
    {
        var config = IdpConfiguration.Create(
            ValidTenantId, ValidSystemSuiteId, ValidProviderType, ValidDomainHints,
            ValidConfigPayload, ValidSecretRef, ValidResolutionPriority, null, ValidActor).Value;

        var result = config.Deactivate(ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Configuration.IdpConfigAlreadyInactive, result.Error);
    }

    [Fact]
    public void Deactivate_RaisesIdpConfigDeactivatedEvent()
    {
        var config = IdpConfiguration.Create(
            ValidTenantId, ValidSystemSuiteId, ValidProviderType, ValidDomainHints,
            ValidConfigPayload, ValidSecretRef, ValidResolutionPriority, null, ValidActor).Value;
        config.Activate(ValidActor);

        config.Deactivate(ValidActor);

        var events = config.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Contains(events, e => e is IdpConfigDeactivatedEvent);
    }

    #endregion

    #region Update

    [Fact]
    public void Update_WhenDraft_ReturnsSuccess()
    {
        var config = IdpConfiguration.Create(
            ValidTenantId, ValidSystemSuiteId, ValidProviderType, ValidDomainHints,
            ValidConfigPayload, ValidSecretRef, ValidResolutionPriority, null, ValidActor).Value;
        var newPayload = "{\"issuer\": \"https://new.example.com\"}";
        var newSecretRef = "vault/secret/new-idp";
        var newDomainHints = new[] { "new.example.com" };

        var result = config.Update(newPayload, newSecretRef, newDomainHints, ValidActor);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Update_WhenInactive_ReturnsSuccess()
    {
        var config = IdpConfiguration.Create(
            ValidTenantId, ValidSystemSuiteId, ValidProviderType, ValidDomainHints,
            ValidConfigPayload, ValidSecretRef, ValidResolutionPriority, null, ValidActor).Value;
        config.Activate(ValidActor);
        config.Deactivate(ValidActor);
        var newPayload = "{\"issuer\": \"https://new.example.com\"}";
        var newSecretRef = "vault/secret/new-idp";
        var newDomainHints = new[] { "new.example.com" };

        var result = config.Update(newPayload, newSecretRef, newDomainHints, ValidActor);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Update_WhenActive_ReturnsFailure()
    {
        var config = IdpConfiguration.Create(
            ValidTenantId, ValidSystemSuiteId, ValidProviderType, ValidDomainHints,
            ValidConfigPayload, ValidSecretRef, ValidResolutionPriority, null, ValidActor).Value;
        config.Activate(ValidActor);
        var newPayload = "{\"issuer\": \"https://new.example.com\"}";
        var newSecretRef = "vault/secret/new-idp";
        var newDomainHints = new[] { "new.example.com" };

        var result = config.Update(newPayload, newSecretRef, newDomainHints, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Configuration.IdpConfigNotDraft, result.Error);
    }

    [Fact]
    public void Update_WithEmptyPayload_ReturnsFailure()
    {
        var config = IdpConfiguration.Create(
            ValidTenantId, ValidSystemSuiteId, ValidProviderType, ValidDomainHints,
            ValidConfigPayload, ValidSecretRef, ValidResolutionPriority, null, ValidActor).Value;

        var result = config.Update("", ValidSecretRef, ValidDomainHints, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Configuration.IdpConfigPayloadInvalid, result.Error);
    }

    [Fact]
    public void Update_IncrementsVersion()
    {
        var config = IdpConfiguration.Create(
            ValidTenantId, ValidSystemSuiteId, ValidProviderType, ValidDomainHints,
            ValidConfigPayload, ValidSecretRef, ValidResolutionPriority, null, ValidActor).Value;
        var initialVersion = config.Version;

        config.Update(ValidConfigPayload, ValidSecretRef, ValidDomainHints, ValidActor);

        Assert.Equal(initialVersion + 1, config.Version);
    }

    [Fact]
    public void Update_RaisesIdpConfigUpdatedEvent()
    {
        var config = IdpConfiguration.Create(
            ValidTenantId, ValidSystemSuiteId, ValidProviderType, ValidDomainHints,
            ValidConfigPayload, ValidSecretRef, ValidResolutionPriority, null, ValidActor).Value;

        config.Update(ValidConfigPayload, ValidSecretRef, ValidDomainHints, ValidActor);

        var events = config.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Contains(events, e => e is IdpConfigUpdatedEvent);
    }

    #endregion
}
