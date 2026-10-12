namespace Evolith.Ums.Application.Identity.Tenant.IdentityProvider.DTOs;

public sealed record IdentityProviderDto(
    Guid IdentityProviderId,
    string Code,
    string Name,
    string Description,
    string Strategy,
    bool IsActive);
