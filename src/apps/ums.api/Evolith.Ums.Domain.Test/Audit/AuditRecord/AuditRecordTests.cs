namespace Evolith.Ums.Domain.Test.Audit.AuditRecord;

using Evolith.Ums.Domain.Audit.AuditRecord;
using Xunit;

public class AuditRecordTests
{
    private static readonly Guid ValidWhoActed = Guid.NewGuid();
    private static readonly SubjectType ValidSubjectType = SubjectType.User;
    private static readonly string ValidWhatChanged = "Updated user profile";
    private static readonly string ValidEventType = "UserUpdated";
    private static readonly AuditResult ValidAuditResult = AuditResult.Success;
    private static readonly Guid ValidAffectedEntityId = Guid.NewGuid();
    private static readonly string ValidAffectedEntityType = "UserAccount";
    private static readonly Guid ValidRootTenantId = Guid.NewGuid();
    #region Record

    [Fact]
    public void Record_WithValidData_ReturnsSuccess()
    {
        var result = AuditRecord.Record(
            ValidWhoActed,
            ValidSubjectType,
            ValidWhatChanged,
            ValidEventType,
            ValidAuditResult,
            ValidAffectedEntityId,
            ValidAffectedEntityType,
            ValidRootTenantId);

        Assert.True(result.IsSuccess);
        Assert.Equal(ValidWhoActed, result.Value.WhoActed);
        Assert.Equal(ValidSubjectType, result.Value.SubjectType);
        Assert.Equal(ValidWhatChanged, result.Value.WhatChanged);
        Assert.Equal(ValidEventType, result.Value.EventType);
        Assert.Equal(ValidAuditResult, result.Value.AuditResult);
        Assert.Equal(ValidAffectedEntityId, result.Value.AffectedEntityId);
        Assert.Equal(ValidAffectedEntityType, result.Value.AffectedEntityType);
        Assert.Equal(ValidRootTenantId, result.Value.RootTenantId);
    }

    [Fact]
    public void Record_WithEmptyWhoActed_ReturnsFailure()
    {
        var result = AuditRecord.Record(
            Guid.Empty,
            ValidSubjectType,
            ValidWhatChanged,
            ValidEventType,
            ValidAuditResult,
            ValidAffectedEntityId,
            ValidAffectedEntityType,
            ValidRootTenantId);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Audit.WhatChangedRequired, result.Error);
    }

    [Fact]
    public void Record_WithEmptyWhatChanged_ReturnsFailure()
    {
        var result = AuditRecord.Record(
            ValidWhoActed,
            ValidSubjectType,
            "",
            ValidEventType,
            ValidAuditResult,
            ValidAffectedEntityId,
            ValidAffectedEntityType,
            ValidRootTenantId);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Audit.WhatChangedRequired, result.Error);
    }

    [Fact]
    public void Record_WithWhitespaceWhatChanged_ReturnsFailure()
    {
        var result = AuditRecord.Record(
            ValidWhoActed,
            ValidSubjectType,
            "   ",
            ValidEventType,
            ValidAuditResult,
            ValidAffectedEntityId,
            ValidAffectedEntityType,
            ValidRootTenantId);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Record_WithEmptyAffectedEntityId_ReturnsFailure()
    {
        var result = AuditRecord.Record(
            ValidWhoActed,
            ValidSubjectType,
            ValidWhatChanged,
            ValidEventType,
            ValidAuditResult,
            Guid.Empty,
            ValidAffectedEntityType,
            ValidRootTenantId);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Audit.AffectedEntityRequired, result.Error);
    }

    [Fact]
    public void Record_WithEmptyAffectedEntityType_ReturnsFailure()
    {
        var result = AuditRecord.Record(
            ValidWhoActed,
            ValidSubjectType,
            ValidWhatChanged,
            ValidEventType,
            ValidAuditResult,
            ValidAffectedEntityId,
            "",
            ValidRootTenantId);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Audit.AffectedEntityRequired, result.Error);
    }

    [Fact]
    public void Record_WithMetadata_IncludesMetadata()
    {
        var metadata = "{\"ip\": \"192.168.1.1\"}";

        var result = AuditRecord.Record(
            ValidWhoActed,
            ValidSubjectType,
            ValidWhatChanged,
            ValidEventType,
            ValidAuditResult,
            ValidAffectedEntityId,
            ValidAffectedEntityType,
            ValidRootTenantId,
            metadata);

        Assert.True(result.IsSuccess);
        Assert.Equal(metadata, result.Value.Metadata);
    }

    [Fact]
    public void Record_WithoutMetadata_SetsMetadataToNull()
    {
        var result = AuditRecord.Record(
            ValidWhoActed,
            ValidSubjectType,
            ValidWhatChanged,
            ValidEventType,
            ValidAuditResult,
            ValidAffectedEntityId,
            ValidAffectedEntityType,
            ValidRootTenantId);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.Metadata);
    }

    #endregion

    #region Immutability

    [Fact]
    public void Record_HasNoMutationMethods_AppendOnly()
    {
        // Smoke: el agregado se construye; la aserción verifica la forma del tipo
        // (sin métodos de mutación), por lo que la instancia se descarta.
        _ = AuditRecord.Record(
            ValidWhoActed,
            ValidSubjectType,
            ValidWhatChanged,
            ValidEventType,
            ValidAuditResult,
            ValidAffectedEntityId,
            ValidAffectedEntityType,
            ValidRootTenantId).Value;

        var methods = typeof(AuditRecord).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)
            .Where(m => m.Name != "GetId" && m.Name != "Equals" && m.Name != "GetHashCode" && m.Name != "ToString" && m.Name != "GetType");

        Assert.Empty(methods);
    }

    #endregion
}
