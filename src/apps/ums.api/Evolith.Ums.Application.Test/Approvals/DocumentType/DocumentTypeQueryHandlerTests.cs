namespace Evolith.Ums.Application.Test.Approvals.DocumentType;

using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Approvals.DocumentType.Queries;
using Evolith.Ums.Application.Approvals.DocumentType.DTOs;
using Evolith.Ums.Domain.Approvals.DocumentType;
using Evolith.Ums.Domain.Kernel;
using Evolith.Ums.Domain.Enums;
using Evolith.Ums.Domain.Approvals;
using Evolith.Ums.Domain;
using Moq;
using Xunit;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public class DocumentTypeQueryHandlerTests
{
    private readonly Mock<IDocumentTypeRepository> _repo = new();

    private static DocumentType MakeDocumentType(string code = "DOCTYPE-001")
    {
        return DocumentType.Create(
            TenantId.Load(Guid.NewGuid()),
            Code.Create(code),
            Name.Create("Passport"),
            Description.Create("Passport Description"),
            DocumentCriticity.High,
            ActorId.Create("user-001")).Value;
    }

    // =========================================================================
    #region GetDocumentTypeByIdQueryHandler
    // =========================================================================

    [Fact]
    public async Task GetById_WhenFound_ReturnsSuccess()
    {
        var doc = MakeDocumentType();
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(doc);

        var query = new GetDocumentTypeByIdQuery(Guid.NewGuid());
        var handler = new GetDocumentTypeByIdQueryHandler(_repo.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(doc.Props.Code.GetValue(), result.Value.Code);
    }

    [Fact]
    public async Task GetById_WhenNotFound_ReturnsFailure()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((DocumentType?)null);

        var query = new GetDocumentTypeByIdQuery(Guid.NewGuid());
        var handler = new GetDocumentTypeByIdQueryHandler(_repo.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    // =========================================================================
    #region GetAllDocumentTypesQueryHandler
    // =========================================================================

    [Fact]
    public async Task GetAll_WithoutFilters_ReturnsAll()
    {
        var list = new List<DocumentType>
        {
            MakeDocumentType("DOCTYPE-001"),
            MakeDocumentType("DOCTYPE-002")
        };

        _repo.Setup(r => r.GetAllAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(list);

        var query = new GetAllDocumentTypesQuery(
            TenantId: null,
            Page: 1,
            PageSize: 10,
            SortBy: null,
            SortOrder: null,
            Search: null);

        var handler = new GetAllDocumentTypesQueryHandler(_repo.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.TotalItems);
    }

    [Fact]
    public async Task GetAll_WithTenantId_FilterByTenant()
    {
        var tenantId = Guid.NewGuid();
        var list = new List<DocumentType>
        {
            MakeDocumentType()
        };

        _repo.Setup(r => r.GetByTenantIdAsync(tenantId, It.IsAny<CancellationToken>()))
             .ReturnsAsync(list);

        var query = new GetAllDocumentTypesQuery(
            TenantId: tenantId,
            Page: 1,
            PageSize: 10,
            SortBy: null,
            SortOrder: null,
            Search: null);

        var adminCtx = new Mock<Evolith.Ums.Application.Common.Interfaces.ITenantContext>();
        adminCtx.Setup(t => t.IsInternalAdmin).Returns(true);
        var handler = new GetAllDocumentTypesQueryHandler(_repo.Object, adminCtx.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        _repo.Verify(r => r.GetByTenantIdAsync(tenantId, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
