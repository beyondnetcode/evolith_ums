namespace Evolith.Ums.Domain.Test.Approvals;

using Evolith.Ums.Domain.Approvals;
using ApprovalWorkflowAggregate = Evolith.Ums.Domain.Approvals.ApprovalWorkflow.ApprovalWorkflow;
using UserDocumentAggregate = Evolith.Ums.Domain.Approvals.UserDocument.UserDocument;
using Xunit;

public class RequiredDocumentChecklistTests
{
    private static readonly ActorId Actor = ActorId.Create("user-001");

    private static ApprovalWorkflowAggregate MakeWorkflow() =>
        ApprovalWorkflowAggregate.Create(
            TenantId.Load(Guid.NewGuid().ToString()),
            Code.Create("WF-001"),
            Name.Create("Onboarding"),
            Description.Create("Onboarding workflow"),
            UserCategory.Internal,
            requiresApproval: true,
            SystemSuiteId.Load(Guid.NewGuid().ToString()),
            Actor,
            requiredDocumentCount: 1).Value;

    private static ApprovalWorkflowAggregate MakeWorkflowRequiring(DocumentTypeId documentTypeId, bool isMandatory = true)
    {
        var workflow = MakeWorkflow();
        workflow.AddRequiredDocument(documentTypeId, isMandatory, Actor);
        return workflow;
    }

    private static UserDocumentAggregate MakeDocument(DocumentTypeId documentTypeId, bool valid)
    {
        var document = UserDocumentAggregate.Upload(
            UserId.Load(Guid.NewGuid().ToString()),
            documentTypeId,
            new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            DocumentCriticity.High,
            TextValueObject.Create("/storage/doc.pdf"),
            "checksum",
            Actor).Value;

        if (valid)
        {
            document.Validate(Actor);
        }

        return document;
    }

    [Fact]
    public void Evaluate_WhenNoRequiredDocuments_ReturnsSuccess()
    {
        var result = RequiredDocumentChecklist.Evaluate(MakeWorkflow(), Array.Empty<UserDocumentAggregate>());

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Evaluate_WhenMandatoryDocumentValid_ReturnsSuccess()
    {
        var docType = DocumentTypeId.Load(Guid.NewGuid());

        var result = RequiredDocumentChecklist.Evaluate(
            MakeWorkflowRequiring(docType),
            new[] { MakeDocument(docType, valid: true) });

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Evaluate_WhenMandatoryDocumentMissing_FailsClosed()
    {
        var docType = DocumentTypeId.Load(Guid.NewGuid());

        var result = RequiredDocumentChecklist.Evaluate(
            MakeWorkflowRequiring(docType),
            Array.Empty<UserDocumentAggregate>());

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Approvals.RequiredDocumentsIncomplete, result.Error);
    }

    [Fact]
    public void Evaluate_WhenMandatoryDocumentPresentButNotValid_FailsClosed()
    {
        var docType = DocumentTypeId.Load(Guid.NewGuid());

        var result = RequiredDocumentChecklist.Evaluate(
            MakeWorkflowRequiring(docType),
            new[] { MakeDocument(docType, valid: false) });

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Approvals.RequiredDocumentsIncomplete, result.Error);
    }

    [Fact]
    public void Evaluate_WhenNonMandatoryDocumentMissing_ReturnsSuccess()
    {
        var docType = DocumentTypeId.Load(Guid.NewGuid());

        var result = RequiredDocumentChecklist.Evaluate(
            MakeWorkflowRequiring(docType, isMandatory: false),
            Array.Empty<UserDocumentAggregate>());

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Evaluate_WhenValidDocumentOfDifferentType_FailsClosed()
    {
        var requiredType = DocumentTypeId.Load(Guid.NewGuid());
        var otherType    = DocumentTypeId.Load(Guid.NewGuid());

        var result = RequiredDocumentChecklist.Evaluate(
            MakeWorkflowRequiring(requiredType),
            new[] { MakeDocument(otherType, valid: true) });

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Approvals.RequiredDocumentsIncomplete, result.Error);
    }
}
