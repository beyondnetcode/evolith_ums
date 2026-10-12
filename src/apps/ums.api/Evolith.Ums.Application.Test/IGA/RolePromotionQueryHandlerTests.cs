namespace Evolith.Ums.Application.Test.IGA;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.IGA.RoleMaturity.Queries;
using Evolith.Ums.Application.IGA.RolePromotion.Queries;
using Evolith.Ums.Domain.Enums;
using Evolith.Ums.Domain.IGA;
using Evolith.Ums.Domain.Kernel.ValueObjects;
using Moq;
using Xunit;
using RoleMaturityStatusAggregate = Evolith.Ums.Domain.IGA.RoleMaturityStatus.RoleMaturityStatus;
using RolePromotionRequestAggregate = Evolith.Ums.Domain.IGA.RolePromotionRequest.RolePromotionRequest;

/// <summary>Pruebas de los handlers de consulta IGA (ADR-UMS-093), incluida la acotación por inquilino.</summary>
public class RolePromotionQueryHandlerTests
{
    private readonly Mock<IRolePromotionRequestRepository> _repo = new();
    private readonly Mock<IRoleMaturityStatusRepository> _maturityRepo = new();
    private readonly Mock<ITenantScopePolicy> _scope = new();

    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _target = Guid.NewGuid();
    private readonly Guid _requester = Guid.NewGuid();
    private readonly Guid _currentRole = Guid.NewGuid();
    private readonly Guid _targetRole = Guid.NewGuid();

    private RolePromotionRequestAggregate NewRequest() =>
        RolePromotionRequestAggregate.Create(
            TenantId.Load(_tenant),
            UserId.Load(_target),
            UserId.Load(_requester),
            RoleId.Load(_currentRole),
            RoleId.Load(_targetRole),
            ActorId.Create(_requester.ToString())).Value;

    private RoleMaturityStatusAggregate NewMaturity() =>
        RoleMaturityStatusAggregate.Create(
            TenantId.Load(_tenant),
            UserId.Load(_target),
            RoleId.Load(_currentRole),
            RoleMaturityLevel.Junior,
            DateTime.UtcNow.AddYears(-1),
            ActorId.Create("sistema")).Value;

    [Fact]
    public async Task GetById_DentroDelAlcance_DevuelveDto()
    {
        _scope.Setup(s => s.ResolveQueryScope()).Returns((Guid?)null);
        var req = NewRequest();
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(req);

        var handler = new GetRolePromotionRequestByIdQueryHandler(_repo.Object, _scope.Object);
        var result = await handler.Handle(new GetRolePromotionRequestByIdQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(_tenant, result.Value.TenantId);
        Assert.Equal("Draft", result.Value.Status);
    }

    [Fact]
    public async Task GetById_FueraDeAlcance_DevuelveFallo()
    {
        // El solicitante pertenece a otro inquilino distinto del de la solicitud.
        _scope.Setup(s => s.ResolveQueryScope()).Returns(Guid.NewGuid());
        var req = NewRequest();
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(req);

        var handler = new GetRolePromotionRequestByIdQueryHandler(_repo.Object, _scope.Object);
        var result = await handler.Handle(new GetRolePromotionRequestByIdQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("alcance", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetById_CuandoNoExiste_DevuelveCodigoDeNoEncontrado()
    {
        // G-100: sin la solicitud, el handler debe devolver el CÓDIGO estable de dominio
        // (idioma-agnóstico), no el texto en español, para que el mapeador HTTP resuelva 404 y no 400.
        _scope.Setup(s => s.ResolveQueryScope()).Returns((Guid?)null);
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RolePromotionRequestAggregate?)null);

        var handler = new GetRolePromotionRequestByIdQueryHandler(_repo.Object, _scope.Object);
        var result = await handler.Handle(new GetRolePromotionRequestByIdQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrors.IGA.RolePromotionRequestNotFound, result.Error);
    }

    [Fact]
    public async Task List_PorInquilinoYEstado_UsaConsultaFiltrada()
    {
        _scope.Setup(s => s.ResolveQueryScope()).Returns(_tenant);
        _repo.Setup(r => r.GetByTenantAndStatusAsync(_tenant, "Draft", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RolePromotionRequestAggregate> { NewRequest() });

        var handler = new ListRolePromotionRequestsQueryHandler(_repo.Object, _scope.Object);
        var result = await handler.Handle(new ListRolePromotionRequestsQuery(_tenant, "Draft"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        _repo.Verify(r => r.GetByTenantAndStatusAsync(_tenant, "Draft", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetRoleMaturityStatus_ConRol_DevuelveEstadoUnico()
    {
        _scope.Setup(s => s.ResolveQueryScope()).Returns((Guid?)null);
        _maturityRepo.Setup(r => r.GetByUserAndRoleAsync(_tenant, _target, _currentRole, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewMaturity());

        var handler = new GetRoleMaturityStatusByUserQueryHandler(_maturityRepo.Object, _scope.Object);
        var result = await handler.Handle(
            new GetRoleMaturityStatusByUserQuery(_tenant, _target, _currentRole), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal(_target, result.Value[0].UserId);
    }

    [Fact]
    public async Task GetRoleMaturityStatus_SinRol_DevuelveLista()
    {
        _scope.Setup(s => s.ResolveQueryScope()).Returns((Guid?)null);
        _maturityRepo.Setup(r => r.GetByUserAsync(_tenant, _target, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RoleMaturityStatusAggregate> { NewMaturity() });

        var handler = new GetRoleMaturityStatusByUserQueryHandler(_maturityRepo.Object, _scope.Object);
        var result = await handler.Handle(
            new GetRoleMaturityStatusByUserQuery(_tenant, _target, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
    }
}
