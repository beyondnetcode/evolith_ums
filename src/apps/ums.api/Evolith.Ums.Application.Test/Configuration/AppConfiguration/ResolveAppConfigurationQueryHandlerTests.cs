namespace Evolith.Ums.Application.Test.Configuration.AppConfiguration;

using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Configuration.AppConfiguration.Queries;
using Evolith.Ums.Application.Configuration.Services;
using Evolith.Ums.Domain.Enums;
using Xunit;

using AppConfigurationAggregate = Evolith.Ums.Domain.Configuration.AppConfiguration.AppConfiguration;

/// <summary>
/// Tests for ResolveAppConfigurationQueryHandler — hierarchy resolution, not-found
/// semantics, encrypted-value redaction, and admin plaintext access (FS-13, AC-3, BR-5).
/// </summary>
public class ResolveAppConfigurationQueryHandlerTests
{
    private readonly Mock<IConfigurationProvider>  _provider   = new();
    private readonly Mock<ITenantContext>           _tenant     = new();
    private readonly Mock<IValueEncryptionService>  _encryption = new();

    public ResolveAppConfigurationQueryHandlerTests()
    {
        _encryption.Setup(e => e.IsEncryptedValue(It.IsAny<string>())).Returns(false);
        _encryption.Setup(e => e.Decrypt(It.IsAny<string>())).Returns<string>(v => v);
    }

    private ResolveAppConfigurationQueryHandler MakeHandler()
        => new(_provider.Object, _tenant.Object, _encryption.Object);

    private static AppConfigurationAggregate MakeConfig(
        string value,
        Guid? tenantId = null,
        Guid? suiteId  = null,
        Guid? moduleId = null,
        bool encrypted = false)
        => AppConfigurationAggregate.Create(
            tenantId is null ? null : TenantId.Load(tenantId.Value),
            suiteId  is null ? null : SystemSuiteId.Load(suiteId.Value),
            moduleId is null ? null : IdValueObject.Load(moduleId.Value),
            Code.Create("TIMEOUT"),
            ConfigurationValue.Create(value),
            Description.Create("desc"),
            true, encrypted,
            ActorId.Create("admin")).Value;

    // ── Found / not-found ─────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenConfigFound_ReturnsFlaggedAsFound()
    {
        _provider.Setup(p => p.GetWithPrecedence("TIMEOUT", null, null, null))
                 .Returns(MakeConfig("30"));

        var result = await MakeHandler().Handle(
            new ResolveAppConfigurationQuery("TIMEOUT", null, null, null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Found);
        Assert.Equal("30", result.Value.Value);
    }

    [Fact]
    public async Task Handle_WhenConfigNotFound_ReturnsFoundFalse()
    {
        _provider.Setup(p => p.GetWithPrecedence(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>()))
                 .Returns((AppConfigurationAggregate?)null);

        var result = await MakeHandler().Handle(
            new ResolveAppConfigurationQuery("MISSING", null, null, null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Found);
        Assert.Equal(string.Empty, result.Value.Value);
    }

    // ── Scope propagation ─────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_SuiteScoped_ReturnsSuiteScope()
    {
        var suiteId = Guid.NewGuid();
        _provider.Setup(p => p.GetWithPrecedence("TIMEOUT", null, suiteId, null))
                 .Returns(MakeConfig("60", suiteId: suiteId));

        var result = await MakeHandler().Handle(
            new ResolveAppConfigurationQuery("TIMEOUT", null, suiteId, null),
            CancellationToken.None);

        Assert.Equal("Suite", result.Value.ResolvedScope);
        Assert.Equal("60", result.Value.Value);
    }

    // ── Encrypted value redaction ─────────────────────────────────────────────

    [Fact]
    public async Task Handle_EncryptedValue_NonAdmin_ReturnsRedacted()
    {
        _provider.Setup(p => p.GetWithPrecedence(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>()))
                 .Returns(MakeConfig("secret", encrypted: true));
        _tenant.Setup(t => t.IsInternalAdmin).Returns(false);

        var result = await MakeHandler().Handle(
            new ResolveAppConfigurationQuery("TIMEOUT", null, null, null),
            CancellationToken.None);

        Assert.Equal("***", result.Value.Value);
        Assert.True(result.Value.IsEncrypted);
    }

    [Fact]
    public async Task Handle_EncryptedValue_InternalAdmin_ReturnsPlaintext()
    {
        _provider.Setup(p => p.GetWithPrecedence(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>()))
                 .Returns(MakeConfig("secret", encrypted: true));
        _tenant.Setup(t => t.IsInternalAdmin).Returns(true);
        // Cache already stores decrypted value, not AES256: prefix
        _encryption.Setup(e => e.IsEncryptedValue("secret")).Returns(false);

        var result = await MakeHandler().Handle(
            new ResolveAppConfigurationQuery("TIMEOUT", null, null, null),
            CancellationToken.None);

        Assert.Equal("secret", result.Value.Value);
        Assert.True(result.Value.IsEncrypted);
    }
}
