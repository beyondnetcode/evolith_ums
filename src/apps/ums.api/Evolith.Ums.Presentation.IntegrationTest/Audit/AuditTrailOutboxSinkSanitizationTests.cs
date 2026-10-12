using System;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Evolith.Ums.Application.Common.Aop;
using Evolith.Ums.Infrastructure.Aop;

namespace Evolith.Ums.Presentation.IntegrationTest.Audit;

/// <summary>
/// G-040 (residual #5, FR-072): el sink de la vía AUTOMÁTICA (AuditTrailOutboxSink) es el único punto
/// por el que pasan todas las emisiones automáticas de la traza (AuditTrailAspect y
/// ConfigurationAuditService). Debe desinfectar la metadata ANTES de encolarla por el outbox, de modo
/// que un secreto no llegue ni a la tabla de salida ni a la traza inmutable (G-081). Prueba unitaria
/// pura (sin Docker).
/// </summary>
public sealed class AuditTrailOutboxSinkSanitizationTests
{
    [Fact]
    public async Task PublishAsync_WithSecretInMetadata_PublishesSanitizedMetadata()
    {
        AuditTrailEntry? published = null;

        var publishEndpoint = new Mock<IPublishEndpoint>();
        publishEndpoint
            .Setup(p => p.Publish(It.IsAny<AuditTrailEntry>(), It.IsAny<CancellationToken>()))
            .Callback<AuditTrailEntry, CancellationToken>((entry, _) => published = entry)
            .Returns(Task.CompletedTask);

        // Sin UmsPlatformDbContext registrado → el flush del bus-outbox se omite (modo en memoria).
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(s => s.GetService(It.IsAny<Type>())).Returns((object?)null);

        var functionalTransaction = new Mock<Evolith.Ums.Application.Common.Interfaces.IFunctionalTransaction>();

        var sink = new AuditTrailOutboxSink(
            publishEndpoint.Object,
            serviceProvider.Object,
            NullLogger<AuditTrailOutboxSink>.Instance,
            functionalTransaction.Object);

        var entry = new AuditTrailEntry(
            WhoActed: Guid.NewGuid(),
            SubjectType: "AppConfiguration",
            WhatChanged: "OVERRIDE",
            EventType: "OVERRIDE",
            AuditResult: "MODIFIED",
            AffectedEntityId: Guid.Empty,
            AffectedEntityType: "AppConfiguration",
            RootTenantId: Guid.NewGuid(),
            Metadata: "{\"parameterCode\":\"smtp\",\"secret\":\"p@ssw0rd\",\"apiKey\":\"sk-live-xyz\"}");

        await sink.PublishAsync(entry, CancellationToken.None);

        published.Should().NotBeNull("el sink debe publicar la traza");
        published!.Metadata.Should().NotContain("p@ssw0rd").And.NotContain("sk-live-xyz");
        published.Metadata.Should().Contain("[REDACTED]");
        // La clave no sensible se conserva; los campos no-metadata no se tocan.
        published.Metadata.Should().Contain("smtp");
        published.EventType.Should().Be("OVERRIDE");
        published.AffectedEntityType.Should().Be("AppConfiguration");
    }
}
