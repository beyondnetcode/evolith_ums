#pragma warning disable S1144
namespace Evolith.Ums.Application.Test.Authorization.SystemSuite;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Evolith.Ums.Application.Authorization.SystemSuite.DTOs;
using Evolith.Ums.Application.Authorization.SystemSuite.Queries;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Authorization.SystemSuite;
using Evolith.Ums.Domain.Kernel;
using Moq;
using Xunit;

public class SystemSuiteQueryHandlerTests
{
    private readonly Mock<ISystemSuiteRepository> _repo = new();
    private readonly Mock<IUserContext> _userContext = new();
    private readonly Mock<ITenantScopePolicy> _scopePolicy = new();

    private static SystemSuite MakeSystemSuite(string code, string name, string statusName)
    {
        var status = statusName == "Maintenance" ? SystemStatus.Maintenance : SystemStatus.Active;
        var suite = SystemSuite.Create(
            TenantId.Load(Guid.NewGuid()),
            Code.Create(code),
            Name.Create(name),
            Description.Create("Description"),
            ActorId.Create("user-001")).Value;

        if (statusName != "Active")
        {
            suite.SetStatus(status, ActorId.Create("user-001"));
        }

        return suite;
    }

    // =========================================================================
    #region GetSystemSuiteByIdQueryHandler
    // =========================================================================

    [Fact]
    public async Task GetById_WhenFound_ReturnsSuccess()
    {
        var suite = MakeSystemSuite("SUITE-01", "Suite Alpha", "Active");
        var suiteId = suite.Props.Id.GetValue();
        _repo.Setup(r => r.GetByIdAsync(suiteId, It.IsAny<CancellationToken>()))
             .ReturnsAsync(suite);

        var query = new GetSystemSuiteByIdQuery(suiteId);
        var handler = new GetSystemSuiteByIdQueryHandler(_repo.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(suiteId, result.Value.SystemSuiteId);
        Assert.Equal("SUITE-01", result.Value.Code);
        Assert.Equal("Suite Alpha", result.Value.Name);
        Assert.Equal("Active", result.Value.Status);
    }

    [Fact]
    public async Task GetById_WhenNotFound_ReturnsFailure()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((SystemSuite?)null);

        var query = new GetSystemSuiteByIdQuery(Guid.NewGuid());
        var handler = new GetSystemSuiteByIdQueryHandler(_repo.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("suite not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    // =========================================================================
    #region GetAllSystemSuitesQueryHandler
    // =========================================================================

    // ── Paginación en la base (G-179) ────────────────────────────────────────
    //
    // El filtrado, el orden y el recorte se resolvían en memoria sobre TODAS las suites del
    // inquilino, con su árbol completo. Ahora los resuelve la base y el manejador solo traduce
    // los criterios de la petición: eso es lo que estas pruebas verifican. La corrección del
    // filtro SQL en sí es responsabilidad del repositorio, y se cubre contra una base real en
    // las pruebas de integración.

    /// <summary>Prepara el repositorio para devolver una página con las suites indicadas.</summary>
    private void ConPagina(int total, params SystemSuite[] suites)
    {
        var ids = suites.Select(x => x.GetId().GetValue()).ToList();

        _repo.Setup(r => r.GetPageAsync(It.IsAny<SystemSuitePageQuery>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new SystemSuitePage(ids, total));
        _repo.Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(suites.ToList());
    }

    private static GetAllSystemSuitesQuery Consulta(
        Guid? tenantId = null, string criteria = "name", string status = "all",
        string? search = null, string? sortBy = null, string? sortOrder = null,
        int page = 1, int pageSize = 10)
        => new(Page: page, PageSize: pageSize, Search: search, Criteria: criteria,
               Status: status, SortBy: sortBy ?? "name", SortOrder: sortOrder ?? "asc",
               TenantId: tenantId);

    [Fact]
    public async Task GetAll_DevuelveLaPaginaQueResolvioLaBase()
    {
        var s1 = MakeSystemSuite("SUITE-01", "Suite Alpha", "Active");
        var s2 = MakeSystemSuite("SUITE-02", "Suite Beta", "Maintenance");
        ConPagina(total: 37, s1, s2);   // 37 en total, 2 en esta página
        _scopePolicy.Setup(p => p.ResolveQueryScope()).Returns((Guid?)null);

        var handler = new GetAllSystemSuitesQueryHandler(_repo.Object, _scopePolicy.Object);
        var result = await handler.Handle(Consulta(pageSize: 2), CancellationToken.None);

        Assert.True(result.IsSuccess);
        // El total es el de la BASE, no el de los elementos traídos: eso es lo que se rompía al
        // paginar en memoria sobre una lista ya recortada.
        Assert.Equal(37, result.Value.TotalItems);
        Assert.Equal(19, result.Value.TotalPages);
        Assert.Equal(2, result.Value.Items.Count);
    }

    [Fact]
    public async Task GetAll_AcotaLaConsultaAlInquilinoResuelto()
    {
        var tenantId = Guid.NewGuid();
        ConPagina(total: 1, MakeSystemSuite("SUITE-01", "Suite Alpha", "Active"));
        _scopePolicy.Setup(p => p.ResolveQueryScope()).Returns(tenantId);

        var handler = new GetAllSystemSuitesQueryHandler(_repo.Object, _scopePolicy.Object);
        await handler.Handle(Consulta(tenantId: tenantId), CancellationToken.None);

        _repo.Verify(r => r.GetPageAsync(
            It.Is<SystemSuitePageQuery>(q => q.TenantId == tenantId), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAll_ConEstadoTodos_NoFiltraPorEstado()
    {
        ConPagina(total: 0);
        _scopePolicy.Setup(p => p.ResolveQueryScope()).Returns((Guid?)null);

        var handler = new GetAllSystemSuitesQueryHandler(_repo.Object, _scopePolicy.Object);
        await handler.Handle(Consulta(status: "all"), CancellationToken.None);

        // «all» no es un estado: viaja como ausencia de filtro, no como el literal "all".
        _repo.Verify(r => r.GetPageAsync(
            It.Is<SystemSuitePageQuery>(q => q.Status == null), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAll_TrasladaEstadoBusquedaYOrdenALaBase()
    {
        ConPagina(total: 0);
        _scopePolicy.Setup(p => p.ResolveQueryScope()).Returns((Guid?)null);

        var handler = new GetAllSystemSuitesQueryHandler(_repo.Object, _scopePolicy.Object);
        await handler.Handle(
            Consulta(criteria: "code", status: "Maintenance", search: "SUITE-02", sortBy: "code", sortOrder: "desc"),
            CancellationToken.None);

        _repo.Verify(r => r.GetPageAsync(
            It.Is<SystemSuitePageQuery>(q =>
                q.Status == "Maintenance" &&
                q.SearchField == "code" &&
                q.Search == "SUITE-02" &&
                q.SortBy == "code" &&
                q.Descending),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}

#pragma warning restore S1144
