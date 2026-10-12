using BeyondNetCode.Shell.Factory.Installer.Extensions;
using BeyondNetCode.Shell.Factory.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Evolith.Ums.Application.Approvals.ApprovalRequest.Services;
using Evolith.Ums.Application.Approvals.NotificationRule.Services;
using Evolith.Ums.Application.Authorization.Graph.Serializers;
using Evolith.Ums.Application.Authorization.Profile.Exporters;
using Evolith.Ums.Domain.Identity.Tenant.IdentityProvider;
using Evolith.Ums.Infrastructure.Aop;
using Evolith.Ums.Infrastructure.Approvals.ApprovalRequest;
using Evolith.Ums.Infrastructure.Approvals.NotificationRule;
using Evolith.Ums.Infrastructure.Authorization.Graph;
using Evolith.Ums.Infrastructure.Configuration.IdpResolution;
using Evolith.Ums.Infrastructure.Identity.Auth;
using Evolith.Ums.Domain.Identity.Auth;
using Evolith.Ums.Infrastructure.Persistence.Authorization.Exporters;

namespace Evolith.Ums.Infrastructure;

internal static class DependencyInjectionFactories
{
    public static IServiceCollection AddUmsFactories(this IServiceCollection services, bool isNotProduction)
    {
        // Shell.Factory.Create<T>() resolves each selected implementation by concrete type.
        services.AddTransient<EmailNotificationRecipientStrategy>();
        services.AddTransient<SmsNotificationRecipientStrategy>();
        services.AddTransient<InAppNotificationRecipientStrategy>();
        services.AddTransient<ManualApprovalRequestCreationStrategy>();
        services.AddTransient<AutoApproveApprovalRequestCreationStrategy>();
        services.AddTransient<InternalBcryptIdpResolutionStrategy>();
        services.AddTransient<ZitadelIdpResolutionStrategy>();
        services.AddTransient<AzureAdIdpResolutionStrategy>();
        services.AddTransient<OktaIdpResolutionStrategy>();
        services.AddTransient<KeycloakIdpResolutionStrategy>();
        services.AddTransient<Auth0IdpResolutionStrategy>();
        services.AddTransient<GoogleIdpResolutionStrategy>();
        services.AddTransient<LdapIdpResolutionStrategy>();
        services.AddTransient<Saml2IdpResolutionStrategy>();
        services.AddTransient<GenericOidcIdpResolutionStrategy>();
        services.AddTransient<ProfileJsonExporter>();
        services.AddTransient<ProfileXmlExporter>();
        services.AddTransient<ProfileYamlExporter>();
        services.AddTransient<ProfileCsvExporter>();
        services.AddTransient<JsonAuthorizationGraphSerializer>();
        services.AddTransient<XmlAuthorizationGraphSerializer>();
        services.AddTransient<YamlAuthorizationGraphSerializer>();
        services.AddTransient<CsvAuthorizationGraphSerializer>();

        if (isNotProduction)
        {
            services.AddTransient<StubIdpAuthAdapter>();
        }

        services.AddFactory(builder =>
        {
            // --- Approvals ---
            builder.AddTransient<INotificationRecipientStrategy, EmailNotificationRecipientStrategy>();
            builder.AddTransient<INotificationRecipientStrategy, SmsNotificationRecipientStrategy>();
            builder.AddTransient<INotificationRecipientStrategy, InAppNotificationRecipientStrategy>();
            builder.AddSource<NotificationRecipientStrategyFactorySetup>();

            builder.AddTransient<IApprovalRequestCreationStrategy, ManualApprovalRequestCreationStrategy>();
            builder.AddTransient<IApprovalRequestCreationStrategy, AutoApproveApprovalRequestCreationStrategy>();
            builder.AddSource<ApprovalRequestCreationStrategyFactorySetup>();

            // --- Identity ---
            builder.AddTransient<IIdpResolutionStrategy, InternalBcryptIdpResolutionStrategy>();
            builder.AddTransient<IIdpResolutionStrategy, ZitadelIdpResolutionStrategy>();
            builder.AddTransient<IIdpResolutionStrategy, AzureAdIdpResolutionStrategy>();
            builder.AddTransient<IIdpResolutionStrategy, OktaIdpResolutionStrategy>();
            builder.AddTransient<IIdpResolutionStrategy, KeycloakIdpResolutionStrategy>();
            builder.AddTransient<IIdpResolutionStrategy, Auth0IdpResolutionStrategy>();
            builder.AddTransient<IIdpResolutionStrategy, GoogleIdpResolutionStrategy>();
            builder.AddTransient<IIdpResolutionStrategy, LdapIdpResolutionStrategy>();
            builder.AddTransient<IIdpResolutionStrategy, Saml2IdpResolutionStrategy>();
            builder.AddTransient<IIdpResolutionStrategy, GenericOidcIdpResolutionStrategy>();
            builder.AddSource<IdpResolutionStrategyFactorySetup>();

            // --- Authorization ---
            builder.AddTransient<IProfileExporter, ProfileJsonExporter>();
            builder.AddTransient<IProfileExporter, ProfileXmlExporter>();
            builder.AddTransient<IProfileExporter, ProfileYamlExporter>();
            builder.AddTransient<IProfileExporter, ProfileCsvExporter>();
            builder.AddSource<ProfileExportFactorySetup>();

            builder.AddTransient<IAuthorizationGraphSerializer, JsonAuthorizationGraphSerializer>();
            builder.AddTransient<IAuthorizationGraphSerializer, XmlAuthorizationGraphSerializer>();
            builder.AddTransient<IAuthorizationGraphSerializer, YamlAuthorizationGraphSerializer>();
            builder.AddTransient<IAuthorizationGraphSerializer, CsvAuthorizationGraphSerializer>();
            builder.AddSource<AuthorizationGraphSerializerFactorySetup>();

            // --- IDP Adapters ---
            if (isNotProduction)
            {

                builder.AddSource<IdpAuthAdapterStubFactorySetup>();
            }
            else
            {
                builder.AddSource<IdpAuthAdapterFactorySetup>();
            }
        });

        services.AddSingleton<IFactoryInterceptor, FactoryLoggingInterceptor>();

        return services;
    }
}
