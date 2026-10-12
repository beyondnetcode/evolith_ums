namespace Evolith.Ums.Application.Test.Configuration.Services;

using System.Text.Json;
using Evolith.Ums.Application.Common.Aop;
using Evolith.Ums.Application.Configuration.Services;
using FluentAssertions;
using Xunit;

/// <summary>
/// G-105 (residual de G-040#5): el saneador por-clave <see cref="AuditMetadataSanitizer"/> redacta por
/// <b>nombre de clave</b> y NO cubre el valor de un parámetro secreto, que aterriza bajo las claves
/// <c>PreviousValue</c>/<c>NewValue</c> (no sensibles). <see cref="ConfigurationAuditService"/> debe
/// redactar ese valor <b>por clasificación</b> (parámetro cifrado/secreto) ANTES de escribirlo en la
/// metadata de la traza inmutable (G-081), y dejar intacto el valor de los parámetros no secretos
/// (no-regresión: la auditoría de cambios de configuración sigue siendo útil).
/// </summary>
public sealed class ConfigurationAuditServiceTests
{
    private const string Redacted = AuditMetadataSanitizer.RedactionPlaceholder;
    private const string SecretPlaintext = "S3cr3t-Api-Key-DO-NOT-LEAK";

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
    private static readonly Guid RootTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    // Captura la única entrada publicada al sumidero para inspeccionar su metadata.
    private static (Mock<IAuditTrailSink> sink, Func<AuditTrailEntry> captured) MakeSink()
    {
        AuditTrailEntry? entry = null;
        var sink = new Mock<IAuditTrailSink>();
        sink.Setup(s => s.PublishAsync(It.IsAny<AuditTrailEntry>(), It.IsAny<CancellationToken>()))
            .Callback<AuditTrailEntry, CancellationToken>((e, _) => entry = e)
            .Returns(Task.CompletedTask);
        return (sink, () => entry ?? throw new InvalidOperationException("No se publicó ninguna entrada."));
    }

    private static JsonElement Metadata(AuditTrailEntry entry)
        => JsonDocument.Parse(entry.Metadata!).RootElement;

    // ── Parámetro SECRETO → el valor se redacta, el claro no aparece en la traza ──

    [Fact]
    public async Task SecretParameter_Modified_RedactsBothPreviousAndNewValue()
    {
        var (sink, captured) = MakeSink();
        var service = new ConfigurationAuditService(sink.Object);

        await service.RecordConfigurationChangeAsync(
            UserId,
            "SMTP-PASSWORD",
            TenantId,
            previousValue: "old-" + SecretPlaintext,
            newValue: "new-" + SecretPlaintext,
            operationType: "UPDATE",
            RootTenantId,
            isEncrypted: true);

        var entry = captured();
        var meta = Metadata(entry);

        meta.GetProperty("PreviousValue").GetString().Should().Be(Redacted);
        meta.GetProperty("NewValue").GetString().Should().Be(Redacted);
        entry.Metadata.Should().NotContain(SecretPlaintext, "el valor secreto en claro nunca debe llegar a la traza inmutable");
    }

    [Fact]
    public async Task SecretParameter_Created_RedactsNewValue_PreviousNullStaysNull()
    {
        var (sink, captured) = MakeSink();
        var service = new ConfigurationAuditService(sink.Object);

        await service.RecordConfigurationChangeAsync(
            UserId,
            "SMTP-PASSWORD",
            TenantId,
            previousValue: null,
            newValue: SecretPlaintext,
            operationType: "CREATE",
            RootTenantId,
            isEncrypted: true);

        var meta = Metadata(captured());

        // Un valor nulo no tiene nada que ocultar → se deja nulo (no se finge un valor redactado).
        meta.GetProperty("PreviousValue").ValueKind.Should().Be(JsonValueKind.Null);
        meta.GetProperty("NewValue").GetString().Should().Be(Redacted);
    }

    [Fact]
    public async Task SecretParameter_Deleted_RedactsPreviousValue_AndKeepsDeletedResult()
    {
        var (sink, captured) = MakeSink();
        var service = new ConfigurationAuditService(sink.Object);

        await service.RecordConfigurationChangeAsync(
            UserId,
            "SMTP-PASSWORD",
            TenantId,
            previousValue: SecretPlaintext,
            newValue: null,
            operationType: "DELETE",
            RootTenantId,
            isEncrypted: true);

        var entry = captured();
        var meta = Metadata(entry);

        meta.GetProperty("PreviousValue").GetString().Should().Be(Redacted);
        meta.GetProperty("NewValue").ValueKind.Should().Be(JsonValueKind.Null);
        // La redacción no altera la clasificación del evento: newValue nulo ⇒ DELETED se preserva.
        entry.AuditResult.Should().Be("DELETED");
        entry.Metadata.Should().NotContain(SecretPlaintext);
    }

    [Fact]
    public async Task SecretParameter_Override_IsRedacted()
    {
        var (sink, captured) = MakeSink();
        var service = new ConfigurationAuditService(sink.Object);

        await service.RecordParameterOverrideAsync(
            UserId,
            "SMTP-PASSWORD",
            TenantId,
            previousValue: SecretPlaintext,
            newValue: "otro-" + SecretPlaintext,
            RootTenantId,
            isEncrypted: true);

        var entry = captured();
        var meta = Metadata(entry);

        entry.EventType.Should().Be("OVERRIDE");
        meta.GetProperty("PreviousValue").GetString().Should().Be(Redacted);
        meta.GetProperty("NewValue").GetString().Should().Be(Redacted);
        entry.Metadata.Should().NotContain(SecretPlaintext);
    }

    // ── Parámetro NORMAL → el valor se registra tal cual (no-regresión) ──

    [Fact]
    public async Task NonSecretParameter_RecordsValuesVerbatim()
    {
        var (sink, captured) = MakeSink();
        var service = new ConfigurationAuditService(sink.Object);

        await service.RecordConfigurationChangeAsync(
            UserId,
            "SESSION-TIMEOUT",
            TenantId,
            previousValue: "3600",
            newValue: "7200",
            operationType: "UPDATE",
            RootTenantId,
            isEncrypted: false);

        var meta = Metadata(captured());

        meta.GetProperty("PreviousValue").GetString().Should().Be("3600", "un parámetro no secreto conserva su valor auditable");
        meta.GetProperty("NewValue").GetString().Should().Be("7200");
        meta.GetProperty("PreviousValue").GetString().Should().NotBe(Redacted);
        meta.GetProperty("NewValue").GetString().Should().NotBe(Redacted);
    }

    [Fact]
    public async Task NonSecretParameter_Override_RecordsValueVerbatim()
    {
        var (sink, captured) = MakeSink();
        var service = new ConfigurationAuditService(sink.Object);

        await service.RecordParameterOverrideAsync(
            UserId,
            "SESSION-TIMEOUT",
            TenantId,
            previousValue: "3600",
            newValue: "1800",
            RootTenantId,
            isEncrypted: false);

        var meta = Metadata(captured());

        meta.GetProperty("NewValue").GetString().Should().Be("1800");
        meta.GetProperty("PreviousValue").GetString().Should().Be("3600");
    }
}
