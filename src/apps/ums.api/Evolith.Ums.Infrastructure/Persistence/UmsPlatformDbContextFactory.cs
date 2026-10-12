#pragma warning disable S1144
using System;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Logging.Abstractions;
using BeyondNetCode.Shell.Ddd.Interfaces;
using Evolith.Ums.Domain.Identity;

namespace Evolith.Ums.Infrastructure.Persistence;

public class UmsPlatformDbContextFactory : IDesignTimeDbContextFactory<UmsPlatformDbContext>
{
    public UmsPlatformDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<UmsPlatformDbContext>();

        // Solo para la generación de migraciones con el tooling de EF Core.
        //
        // El proveedor se elige con UMS_MIGRATIONS_PROVIDER en vez de con un segundo
        // IDesignTimeDbContextFactory: EF falla con «More than one DbContext factory found» si
        // encuentra dos para el mismo contexto, así que una única fábrica conmutable es la forma
        // de sostener los dos juegos de migraciones. Sin la variable, PostgreSQL — el proveedor
        // por defecto del proyecto.
        //
        // MigrationsHistoryTable debe declararse aquí con el MISMO esquema que el runtime
        // (DependencyInjection: "__EFMigrationsHistory" en UmsPlatformDbContext.DefaultSchema).
        // Si el diseño lo omite, el tooling asume el esquema por defecto — dbo en SQL Server —
        // y `dotnet ef migrations script --idempotent` genera guardas contra
        // [dbo].[__EFMigrationsHistory], que en una base real no contiene las migraciones de
        // plataforma (viven en ums_platform.__EFMigrationsHistory). El script concluye que no se
        // aplicó ninguna y emite el CREATE SCHEMA / CREATE TABLE del esquema completo contra una
        // base que ya lo tiene. El script se genera sin error: el fallo solo aparece al ejecutarlo.
#pragma warning disable S2068 // Cadenas de DISEÑO (EF Core tooling), solo locales; el runtime resuelve por configuración. No son secretos de producción.
        if (MigrationsDesignTime.EsSqlServer)
            optionsBuilder.UseSqlServer(
                "Server=localhost;Database=ums_test;Trusted_Connection=True;TrustServerCertificate=True",
                o =>
                {
                    o.MigrationsAssembly(MigrationsDesignTime.EnsambladoSqlServer);
                    o.MigrationsHistoryTable("__EFMigrationsHistory", UmsPlatformDbContext.DefaultSchema);
                });
        else
            optionsBuilder.UseNpgsql(
                "Host=localhost;Database=ums_test;Username=postgres;Password=postgres",
                o => o.MigrationsHistoryTable("__EFMigrationsHistory", UmsPlatformDbContext.DefaultSchema));
#pragma warning restore S2068

        return new UmsPlatformDbContext(
            optionsBuilder.Options,
            new DesignTimeTenantContext(),
            new DesignTimePublisher(),
            NullLogger<UmsPlatformDbContext>.Instance);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Minor Code Smell", "S2325:Methods and properties that don't access instance data should be static",
        Justification = "Stub de solo diseño (EF Core tooling). Sus miembros replican la forma de " +
                        "ITenantContext y deben permanecer de instancia para sustituir al contrato en runtime.")]
    private sealed class DesignTimeTenantContext : ITenantContext
    {
        public Guid? OrganizationId => null;
        public Guid? UserId => null;
        public string? SessionId => null;
        public string? Email => null;
        public string? Roles => null;
        public string? Permissions => null;
        public bool IsAuthenticated => false;
        public string Token => string.Empty;
        public Guid? OriginalTenantId => null;
        public bool IsInternalAdmin => false;

        public void SetOrganizationId(Guid organizationId) { }
        public void EnableCrossTenantAccess() { }
        public void DisableCrossTenantAccess() { }
        public void Initialize(Guid userTenantId, bool isInternalAdmin) { }
    }

    private sealed class DesignTimePublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
    }
}

#pragma warning restore S1144
