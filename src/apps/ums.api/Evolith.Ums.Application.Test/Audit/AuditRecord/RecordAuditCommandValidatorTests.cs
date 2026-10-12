namespace Evolith.Ums.Application.Test.Audit.AuditRecord;

using Evolith.Ums.Application.Audit.AuditRecord.Commands;
using Xunit;

// G-040: el Metadata de auditoría, si viene, debe estar acotado (≤4000) y ser JSON bien formado.
public sealed class RecordAuditCommandValidatorTests
{
    private readonly RecordAuditCommandValidator _validator = new();

    private static RecordAuditCommand WithMetadata(string? metadata) => new(
        WhoActed: Guid.NewGuid(),
        SubjectType: "User",
        WhatChanged: "cambio",
        EventType: "User.Updated",
        AuditResult: "Success",
        AffectedEntityId: Guid.NewGuid(),
        AffectedEntityType: "User",
        RootTenantId: Guid.NewGuid(),
        Metadata: metadata);

    private bool MetadataHasError(string? metadata)
        => _validator.Validate(WithMetadata(metadata))
                     .Errors.Exists(e => e.PropertyName == nameof(RecordAuditCommand.Metadata));

    [Fact]
    public void NullMetadata_IsAccepted() => Assert.False(MetadataHasError(null));

    [Fact]
    public void WellFormedJson_IsAccepted() => Assert.False(MetadataHasError("{\"key\":\"value\",\"n\":1}"));

    [Fact]
    public void MalformedJson_IsRejected() => Assert.True(MetadataHasError("{not valid json"));

    [Fact]
    public void OversizedMetadata_IsRejected()
        => Assert.True(MetadataHasError("\"" + new string('a', 4001) + "\"")); // JSON válido pero >4000
}
