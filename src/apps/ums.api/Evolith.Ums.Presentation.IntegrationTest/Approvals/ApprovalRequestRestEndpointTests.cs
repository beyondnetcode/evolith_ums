using System.Net;
using System.Net.Http.Json;
using Evolith.Ums.Application.Approvals.ApprovalRequest.DTOs;
using Evolith.Ums.Presentation.IntegrationTest.Infrastructure;

namespace Evolith.Ums.Presentation.IntegrationTest.Approvals;

public sealed class ApprovalRequestRestEndpointTests : IClassFixture<UmsApiWebApplicationFactory>
{
    private static readonly Guid SeededTenantId      = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
    private static readonly Guid ManualWorkflowId  = Guid.Parse("88888888-1111-1111-1111-111111111111");
    private static readonly Guid AutoWorkflowId    = Guid.Parse("88888888-2222-2222-2222-222222222222");
    private static readonly Guid DemoSystemSuiteId = Guid.Parse("dddd0001-0000-0000-0000-000000000001");
    private static readonly Guid DemoAdminRoleId   = Guid.Parse("aaaa0001-0000-0000-0000-000000000001");
    private static readonly Guid SeededTargetUserId = Guid.Parse("3fa85f01-5717-4562-b3fc-2c963f66afa6");
    private static readonly Guid SeededAnalystUserId = Guid.Parse("3fa85f02-5717-4562-b3fc-2c963f66afa6");
    private static readonly Guid SeededSupervisorUserId = Guid.Parse("3fa85f06-5717-4562-b3fc-2c963f66afa6");
    private readonly HttpClient _client;

    public ApprovalRequestRestEndpointTests(UmsApiWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-User-Id", TestAuthHandler.AdministradorSembrado);
        // La cuenta sembrada ES administradora interna; decirlo permite resolver su grafo cuando
        // el inquilino en cabecera no es el suyo. Con el atajo apagado no concede nada por si sola.
        _client.DefaultRequestHeaders.Add("X-Is-Internal-Admin", "true");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", SeededTenantId.ToString());
    }

    [Fact]
    public async Task Create_WithApprovalRequiredWorkflow_ShouldRemainPending()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var command = new
        {
            WorkflowId         = ManualWorkflowId,
            TargetUserId       = SeededSupervisorUserId,
            TargetProfileId    = (Guid?)null,
            RequestedSystemId  = DemoSystemSuiteId,
            RequestedBranchId  = (Guid?)null,
            RequestedRoleId    = DemoAdminRoleId,
            Justification      = "Integration test — manual workflow",
        };

        var createResponse = await _client.PostAsJsonAsync("/api/v1/approval-requests", command, cancellationToken);
        createResponse.EnsureSuccessStatusCode();

        var created = await createResponse.Content.ReadFromJsonAsync<CreateApprovalRequestResponse>(cancellationToken);
        created.Should().NotBeNull();

        var getResponse = await _client.GetAsync($"/api/v1/approval-requests/{created!.ApprovalRequestId}", cancellationToken);
        getResponse.EnsureSuccessStatusCode();

        var payload = await getResponse.Content.ReadFromJsonAsync<ApprovalRequestDto>(cancellationToken);
        payload.Should().NotBeNull();
        payload!.Status.Should().Be("Pending");
        payload.RequestedSystemId.Should().Be(DemoSystemSuiteId);
        payload.RequestedRoleId.Should().Be(DemoAdminRoleId);
    }

    [Fact]
    public async Task Create_WithAutoApproveWorkflow_ShouldBeApprovedImmediately()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var command = new
        {
            WorkflowId         = AutoWorkflowId,
            TargetUserId       = SeededAnalystUserId,
            TargetProfileId    = (Guid?)null,
            RequestedSystemId  = DemoSystemSuiteId,
            RequestedBranchId  = (Guid?)null,
            RequestedRoleId    = DemoAdminRoleId,
            Justification      = "Integration test — auto-approve workflow",
        };

        var createResponse = await _client.PostAsJsonAsync("/api/v1/approval-requests", command, cancellationToken);
        createResponse.EnsureSuccessStatusCode();

        var created = await createResponse.Content.ReadFromJsonAsync<CreateApprovalRequestResponse>(cancellationToken);
        created.Should().NotBeNull();

        var getResponse = await _client.GetAsync($"/api/v1/approval-requests/{created!.ApprovalRequestId}", cancellationToken);
        getResponse.EnsureSuccessStatusCode();

        var payload = await getResponse.Content.ReadFromJsonAsync<ApprovalRequestDto>(cancellationToken);
        payload.Should().NotBeNull();
        payload!.Status.Should().Be("Approved");
        payload.GrantedRoleId.Should().Be(DemoAdminRoleId);
    }

    [Fact]
    public async Task Create_DuplicatePendingScope_ShouldReturnFailure()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var command = new
        {
            WorkflowId         = ManualWorkflowId,
            TargetUserId       = SeededTargetUserId,
            TargetProfileId    = (Guid?)null,
            RequestedSystemId  = DemoSystemSuiteId,
            RequestedBranchId  = (Guid?)null,
            RequestedRoleId    = DemoAdminRoleId,
            Justification      = "First request",
        };

        var first = await _client.PostAsJsonAsync("/api/v1/approval-requests", command, cancellationToken);
        first.EnsureSuccessStatusCode();

        var second = await _client.PostAsJsonAsync("/api/v1/approval-requests", command, cancellationToken);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict, "duplicate pending scope should be rejected");
    }
}
