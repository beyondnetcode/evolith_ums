using Evolith.Ums.Application.Identity.Tenant.IdentityProvider.DTOs;



namespace Evolith.Ums.Application.Identity.Tenant.IdentityProvider.Commands;


public sealed record ActivateIdentityProviderCommand(
    Guid TenantId,
    Guid IdentityProviderId) : ICommand<ActivateIdentityProviderResponse>;
