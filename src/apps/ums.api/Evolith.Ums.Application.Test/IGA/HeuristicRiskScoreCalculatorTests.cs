namespace Evolith.Ums.Application.Test.IGA;

using System;
using System.Threading;
using System.Threading.Tasks;
using Evolith.Ums.Application.IGA.Services;
using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Kernel.ValueObjects;
using Moq;
using Xunit;
using RoleAggregate = Evolith.Ums.Domain.Authorization.Role.Role;

/// <summary>
/// Pruebas de la heurística versionada de RiskScore (IGA, ADR-UMS-093, FR-061). La fórmula es
/// determinista: verificamos el enrutamiento de riesgo alto (≥ umbral 70) frente a riesgo bajo,
/// la versión del modelo y el fallo cuando falta un rol.
/// </summary>
public class HeuristicRiskScoreCalculatorTests
{
    private readonly Mock<IRoleRepository> _roleRepo = new();

    private static RoleAggregate MakeRole(int hierarchyLevel, int promotionOrder)
    {
        RoleId? parent = hierarchyLevel == 0 ? null : RoleId.Load(Guid.NewGuid());
        return RoleAggregate.Create(
            TenantId.Load(Guid.NewGuid()),
            SystemSuiteId.Create(),
            Code.Create("ROLE1"),
            Name.Create("Rol"),
            Description.Create("Rol de prueba"),
            parent,
            hierarchyLevel,
            promotionOrder,
            ActorId.Create("sistema")).Value;
    }

    [Fact]
    public async Task Calculate_ConGranEscalacion_ProduceRiesgoAlto()
    {
        var currentRole = Guid.NewGuid();
        var targetRole = Guid.NewGuid();
        _roleRepo.Setup(r => r.GetByIdAsync(currentRole, It.IsAny<CancellationToken>())).ReturnsAsync(MakeRole(1, 1));
        _roleRepo.Setup(r => r.GetByIdAsync(targetRole, It.IsAny<CancellationToken>())).ReturnsAsync(MakeRole(4, 4));

        var calc = new HeuristicRiskScoreCalculator(_roleRepo.Object);
        var result = await calc.CalculateAsync(
            new RolePromotionRiskContext(Guid.NewGuid(), Guid.NewGuid(), currentRole, targetRole),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Score >= 70, $"Se esperaba riesgo alto, fue {result.Value.Score}.");
        Assert.Equal(HeuristicRiskScoreCalculator.ModelVersion, result.Value.RiskModelVersion);
        Assert.NotEmpty(result.Value.Factors);
    }

    [Fact]
    public async Task Calculate_ConEscalacionMinima_ProduceRiesgoBajo()
    {
        var currentRole = Guid.NewGuid();
        var targetRole = Guid.NewGuid();
        _roleRepo.Setup(r => r.GetByIdAsync(currentRole, It.IsAny<CancellationToken>())).ReturnsAsync(MakeRole(1, 1));
        _roleRepo.Setup(r => r.GetByIdAsync(targetRole, It.IsAny<CancellationToken>())).ReturnsAsync(MakeRole(2, 2));

        var calc = new HeuristicRiskScoreCalculator(_roleRepo.Object);
        var result = await calc.CalculateAsync(
            new RolePromotionRiskContext(Guid.NewGuid(), Guid.NewGuid(), currentRole, targetRole),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Score < 70, $"Se esperaba riesgo bajo, fue {result.Value.Score}.");
        Assert.InRange(result.Value.Score, RiskScore.Min, RiskScore.Max);
    }

    [Fact]
    public async Task Calculate_CuandoFaltaUnRol_DevuelveFallo()
    {
        var currentRole = Guid.NewGuid();
        var targetRole = Guid.NewGuid();
        _roleRepo.Setup(r => r.GetByIdAsync(currentRole, It.IsAny<CancellationToken>())).ReturnsAsync(MakeRole(1, 1));
        _roleRepo.Setup(r => r.GetByIdAsync(targetRole, It.IsAny<CancellationToken>())).ReturnsAsync((RoleAggregate?)null);

        var calc = new HeuristicRiskScoreCalculator(_roleRepo.Object);
        var result = await calc.CalculateAsync(
            new RolePromotionRiskContext(Guid.NewGuid(), Guid.NewGuid(), currentRole, targetRole),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("rol objetivo", result.Error, StringComparison.OrdinalIgnoreCase);
    }
}
