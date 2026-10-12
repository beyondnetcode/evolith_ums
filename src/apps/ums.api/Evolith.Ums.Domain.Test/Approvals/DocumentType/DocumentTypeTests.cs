namespace Evolith.Ums.Domain.Test.Approvals.DocumentType;

using Evolith.Ums.Domain.Approvals.DocumentType;
using Xunit;

public class DocumentTypeTests
{
    private static readonly TenantId ValidTenantId = TenantId.Load(Guid.NewGuid().ToString());
    private static readonly Code ValidCode = Code.Create("DOCTYPE-001");
    private static readonly Name ValidName = Name.Create("Passport");
    private static readonly Description ValidDescription = Description.Create("Government-issued passport");
    private static readonly DocumentCriticity ValidCriticity = DocumentCriticity.High;
    private static readonly ActorId ValidActor = ActorId.Create("user-001");
    #region Create

    [Fact]
    public void Create_WithValidData_ReturnsSuccess()
    {
        var result = DocumentType.Create(ValidTenantId, ValidCode, ValidName, ValidDescription, ValidCriticity, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(ValidTenantId, result.Value.TenantId);
        Assert.Equal(ValidCode, result.Value.Code);
        Assert.Equal(ValidName, result.Value.Name);
        Assert.Equal(ValidDescription, result.Value.Description);
        Assert.Equal(ValidCriticity, result.Value.Criticity);
    }

    [Fact]
    public void Create_RaisesDocumentTypeRegisteredEvent()
    {
        var result = DocumentType.Create(ValidTenantId, ValidCode, ValidName, ValidDescription, ValidCriticity, ValidActor);

        Assert.True(result.IsSuccess);
        var events = result.Value.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Single(events);
        Assert.IsType<DocumentTypeRegisteredEvent>(events[0]);
    }

    #endregion

    #region Update

    [Fact]
    public void Update_WithValidData_ReturnsSuccess()
    {
        var docType = DocumentType.Create(ValidTenantId, ValidCode, ValidName, ValidDescription, ValidCriticity, ValidActor).Value;
        var newName = Name.Create("Updated Passport");
        var newDescription = Description.Create("Updated description");

        var result = docType.Update(newName, newDescription, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(newName, docType.Name);
        Assert.Equal(newDescription, docType.Description);
    }

    #endregion
}
