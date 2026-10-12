namespace Evolith.Ums.Infrastructure.Persistence;

/// <summary>
/// Selector de proveedor para las fábricas de tiempo de DISEÑO (<c>dotnet ef migrations</c>).
/// No interviene en runtime: allí el proveedor sale de <c>Persistence:Provider</c>.
///
/// <para>Uso: <c>UMS_MIGRATIONS_PROVIDER=SqlServer dotnet ef migrations add ...</c></para>
/// </summary>
public static class MigrationsDesignTime
{
    /// <summary>Ensamblado que alberga las migraciones de SQL Server.</summary>
    public const string EnsambladoSqlServer = "Evolith.Ums.Infrastructure.Migrations.SqlServer";

    private const string Variable = "UMS_MIGRATIONS_PROVIDER";

    /// <summary>
    /// <c>true</c> cuando el tooling debe trabajar contra SQL Server. Por defecto <c>false</c>,
    /// de modo que un `dotnet ef` sin preparar sigue generando migraciones de PostgreSQL y no
    /// contamina el juego existente.
    /// </summary>
    public static bool EsSqlServer =>
        string.Equals(
            Environment.GetEnvironmentVariable(Variable),
            "SqlServer",
            StringComparison.OrdinalIgnoreCase);
}
