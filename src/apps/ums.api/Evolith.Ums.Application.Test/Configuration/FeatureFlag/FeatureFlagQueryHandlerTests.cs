namespace Evolith.Ums.Application.Test.Configuration.FeatureFlag;

using Evolith.Ums.Application.Configuration.FeatureFlag.Queries;
using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Configuration;
using Evolith.Ums.Domain.Configuration.FeatureFlag;
using Evolith.Ums.Domain.Kernel;
using Moq;
using Xunit;
using SystemSuiteAggregate = Evolith.Ums.Domain.Authorization.SystemSuite.SystemSuite;

public class FeatureFlagQueryHandlerTests
{
    private readonly Mock<IFeatureFlagRepository> _flagRepo = new();
    private readonly Mock<ISystemSuiteRepository> _suiteRepo = new();

    private static SystemSuiteAggregate MakeSystemSuite(string code, string name)
    {
        return SystemSuiteAggregate.Create(
            TenantId.Load(Guid.NewGuid()),
            Code.Create(code),
            Name.Create(name),
            Description.Create("Description"),
            ActorId.Create("user-001")).Value;
    }

    private static FeatureFlag MakeFlag(SystemSuiteAggregate suite, string flagCode)
    {
        return FeatureFlag.Create(
            IdValueObject.Load(suite.Props.Id.GetValue()),
            null,
            flagCode,
            global::Evolith.Ums.Domain.Enums.FlagType.Boolean,
            "all",
            null,
            null,
            null,
            ActorId.Create("user-001")).Value;
    }

    [Fact]
    public async Task GetById_ReturnsSemanticSuiteLabels()
    {
        var suite = MakeSystemSuite("SUITE-01", "Suite Alpha");
        var flag = MakeFlag(suite, "FLAG-001");

        _flagRepo.Setup(r => r.GetByIdAsync(flag.Props.Id.GetValue(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(flag);
        _suiteRepo.Setup(r => r.GetByIdAsync(suite.Props.Id.GetValue(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(suite);

        var handler = new GetFeatureFlagByIdQueryHandler(_flagRepo.Object, _suiteRepo.Object);
        var result = await handler.Handle(new GetFeatureFlagByIdQuery(flag.Props.Id.GetValue()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("SUITE-01", result.Value.SystemSuiteCode);
        Assert.Equal("Suite Alpha", result.Value.SystemSuiteName);
        Assert.Equal("FLAG-001", result.Value.FlagCode);
    }

    [Fact]
    public async Task GetBySystemSuite_ReturnsSemanticSuiteLabels()
    {
        var suite = MakeSystemSuite("SUITE-02", "Suite Beta");
        var flag = MakeFlag(suite, "FLAG-002");

        _flagRepo.Setup(r => r.GetBySystemSuiteIdAsync(suite.Props.Id.GetValue(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<FeatureFlag> { flag });
        _suiteRepo.Setup(r => r.GetByIdAsync(suite.Props.Id.GetValue(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(suite);

        var handler = new GetFeatureFlagsBySystemSuiteQueryHandler(_flagRepo.Object, _suiteRepo.Object);
        var result = await handler.Handle(new GetFeatureFlagsBySystemSuiteQuery(suite.Props.Id.GetValue()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal("SUITE-02", result.Value[0].SystemSuiteCode);
        Assert.Equal("Suite Beta", result.Value[0].SystemSuiteName);
    }

    [Fact]
    public async Task GetAll_ReturnsSemanticSuiteLabels()
    {
        var suite = MakeSystemSuite("SUITE-03", "Suite Gamma");
        var flag = MakeFlag(suite, "FLAG-003");

        _flagRepo.Setup(r => r.GetAllAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<FeatureFlag> { flag });
        _suiteRepo.Setup(r => r.GetAllAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new List<SystemSuiteAggregate> { suite });

        var handler = new GetAllFeatureFlagsQueryHandler(_flagRepo.Object, _suiteRepo.Object);
        var result = await handler.Handle(new GetAllFeatureFlagsQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Items);
        Assert.Equal("SUITE-03", result.Value.Items[0].SystemSuiteCode);
        Assert.Equal("Suite Gamma", result.Value.Items[0].SystemSuiteName);
    }
}
