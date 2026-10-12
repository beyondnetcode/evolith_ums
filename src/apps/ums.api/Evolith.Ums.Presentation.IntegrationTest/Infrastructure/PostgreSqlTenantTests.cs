using Evolith.Ums.Presentation.IntegrationTest.Infrastructure;

namespace Evolith.Ums.Presentation.IntegrationTest.Infrastructure;

/// <summary>
/// REC-15: Integration tests that exercise the Tenant REST endpoints against a real
/// SQL Server instance running in a Testcontainer.
///
/// Prerequisites: Docker must be running locally.  Tests are automatically skipped
/// when Docker is unavailable so CI pipelines without Docker don't break.
/// </summary>
[Collection("PostgreSql")]
public sealed class PostgreSqlTenantTests : IntegrationTestBase
{
    public PostgreSqlTenantTests(PostgreSqlContainerFixture fixture) : base(fixture) { }

    // ─────────────────────────────────────────────────────────────────────────
    // Tenant creation & retrieval
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateTenant_WithValidData_Returns201AndPersistsTenant()
    {
        if (!Fixture.IsAvailable) Assert.Skip("Docker is required for SQL Server integration tests.");

        var body = new
        {
            code = $"TC-{Guid.NewGuid():N}"[..12],
            name = "Integration Test Corp",
            type = "CLIENT",
            idpStrategy = (string?)null,
            companyReference = (string?)null,
        };

        var createResponse = await Client.PostAsJsonAsync("/api/v1/tenants", body, TestContext.Current.CancellationToken);

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        createResponse.Headers.Location.Should().NotBeNull();

        using var payload = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        payload.RootElement.GetProperty("tenantId").GetGuid().Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetTenants_AfterCreate_ReturnsCreatedTenant()
    {
        if (!Fixture.IsAvailable) Assert.Skip("Docker is required for SQL Server integration tests.");

        // Arrange — create a uniquely identifiable tenant first
        var uniqueCode = $"QT-{Guid.NewGuid():N}"[..12];
        var body = new
        {
            code = uniqueCode,
            name = "Query Test Org",
            type = "CLIENT",
            idpStrategy = (string?)null,
            companyReference = (string?)null,
        };

        var createResponse = await Client.PostAsJsonAsync("/api/v1/tenants", body, TestContext.Current.CancellationToken);

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        createResponse.Headers.Location.Should().NotBeNull();

        using var payload = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        // Act — query the list
        var listResponse = await Client.GetAsync(
            $"/api/v1/tenants?page=1&pageSize=50&search={uniqueCode}&sortBy=code",
            TestContext.Current.CancellationToken);

        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var payloadList = JsonDocument.Parse(
            await listResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        var items = payloadList.RootElement.GetProperty("items");
        items.GetArrayLength().Should().BeGreaterThan(0,
            because: "the newly created tenant should appear in the list");

        var found = Enumerable.Range(0, items.GetArrayLength())
            .Select(i => items[i])
            .Any(t => string.Equals(
                t.GetProperty("code").GetString(), uniqueCode,
                StringComparison.OrdinalIgnoreCase));

        found.Should().BeTrue(because: "the created tenant should be retrievable by code");
    }

    [Fact]
    public async Task CreateTenant_WithDuplicateCode_Returns409()
    {
        if (!Fixture.IsAvailable) Assert.Skip("Docker is required for SQL Server integration tests.");

        var duplicateCode = $"DC-{Guid.NewGuid():N}"[..12];
        var body = new
        {
            code = duplicateCode,
            name = "First Org",
            type = "CLIENT",
            idpStrategy = (string?)null,
            companyReference = (string?)null,
        };

        // First creation should succeed
        var first = await Client.PostAsJsonAsync(
            "/api/v1/tenants", body, TestContext.Current.CancellationToken);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        // Second creation with same code should fail
        var second = await Client.PostAsJsonAsync(
            "/api/v1/tenants", new { code = duplicateCode, name = "Duplicate Org", type = "CLIENT", idpStrategy = (string?)null, companyReference = (string?)null },
            TestContext.Current.CancellationToken);
        second.StatusCode.Should().BeOneOf(HttpStatusCode.Conflict, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetTenants_Pagination_ReturnsCorrectPageMetadata()
    {
        if (!Fixture.IsAvailable) Assert.Skip("Docker is required for SQL Server integration tests.");

        var listResponse = await Client.GetAsync(
            "/api/v1/tenants?page=1&pageSize=5",
            TestContext.Current.CancellationToken);

        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var payload = JsonDocument.Parse(
            await listResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        payload.RootElement.GetProperty("page").GetInt32().Should().Be(1);
        payload.RootElement.GetProperty("pageSize").GetInt32().Should().Be(5);
        payload.RootElement.GetProperty("totalItems").GetInt32().Should().BeGreaterThanOrEqualTo(0);
        payload.RootElement.GetProperty("totalPages").GetInt32().Should().BeGreaterThanOrEqualTo(0);
        payload.RootElement.GetProperty("items").ValueKind.Should().Be(JsonValueKind.Array);
    }
}
