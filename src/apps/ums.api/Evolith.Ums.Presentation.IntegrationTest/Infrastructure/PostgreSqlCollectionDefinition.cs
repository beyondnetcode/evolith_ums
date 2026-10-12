namespace Evolith.Ums.Presentation.IntegrationTest.Infrastructure;

/// <summary>
/// REC-15: Collection definition that ensures the SQL Server Testcontainer
/// is started once and shared across all SQL-backed integration tests.
/// </summary>
[CollectionDefinition("PostgreSql")]
public sealed class PostgreSqlCollectionDefinition : ICollectionFixture<PostgreSqlContainerFixture>
{
    // Marker class — xUnit uses the attribute above to wire up the fixture.
}
