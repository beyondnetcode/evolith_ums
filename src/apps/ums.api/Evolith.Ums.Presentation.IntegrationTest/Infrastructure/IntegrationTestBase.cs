using Evolith.Ums.Presentation.IntegrationTest.Infrastructure;
using System;
using System.Net.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;

namespace Evolith.Ums.Presentation.IntegrationTest.Infrastructure;

/// <summary>
/// Base class for integration tests providing a configured HttpClient and fixture.
/// </summary>
public abstract class IntegrationTestBase : IDisposable
{
    protected readonly PostgreSqlContainerFixture Fixture;
    protected readonly HttpClient Client;

    protected IntegrationTestBase(PostgreSqlContainerFixture fixture)
    {
        Fixture = fixture;
        if (fixture.IsAvailable)
        {
            var factory = new PostgreSqlWebApplicationFactory(fixture.ConnectionString);
            Client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false,
            });
            Client.DefaultRequestHeaders.Add("X-User-Id", TestAuthHandler.AdministradorSembrado);
            // La cuenta sembrada ES administradora interna; decirlo permite resolver su grafo cuando
            // el inquilino en cabecera no es el suyo. Con el atajo apagado no concede nada por si sola.
            Client.DefaultRequestHeaders.Add("X-Is-Internal-Admin", "true");
            Client.DefaultRequestHeaders.Add("X-User-Name", "integration-test");
        }
        else
        {
            Client = new HttpClient();
        }
    }

    public void Dispose()
    {
        Client?.Dispose();
    }
}
