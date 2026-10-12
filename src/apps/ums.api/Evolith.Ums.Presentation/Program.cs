using Asp.Versioning;
using Azure.Core;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using Evolith.Ums.Presentation.Bootstrapping;
using Evolith.Ums.Presentation.Extensions;
using Serilog;

// REC-14: Bootstrap Serilog immediately so startup errors are structured.
// Host.UseSerilog() replaces the default Microsoft.Extensions.Logging providers.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);

// Replace built-in logging with Serilog; reads appsettings Serilog section.
builder.Host.UseSerilog((ctx, cfg) => cfg.ConfigureUmsSerilog(ctx));

ConfigureSecrets(builder);

builder.Services.AddUmsApiServiceBootstrappers(builder.Configuration, builder.Environment);

var app = builder.Build();
await app.InitializeUmsPlatformAsync();

// Modo «migra y sal» (G-169): el proceso ya migró y sembró en InitializeUmsPlatformAsync, así que
// termina aquí sin levantar el servidor. Lo usa el Job previo al despliegue, para que la migración
// ocurra UNA vez y no en el arranque de cada réplica.
if (app.Configuration.GetValue<bool>("Persistence:MigrateAndExit"))
{
    Log.Information("Persistence:MigrateAndExit — plataforma inicializada; el proceso termina sin servir tráfico.");
    await Log.CloseAndFlushAsync();
    return;
}

app.UseUmsApiPipeline();
app.MapUmsApiSurface();

await app.RunAsync();

static void ConfigureSecrets(WebApplicationBuilder builder)
{
    var secretSource = builder.Configuration["Secrets:Source"] ?? "AppSettings";

    switch (secretSource)
    {
        case "UserSecrets":
            if (builder.Environment.IsDevelopment())
            {
                builder.Configuration.AddUserSecrets<Program>(optional: true, reloadOnChange: true);
            }
            break;

        case "KeyVault":
            var vaultUrl = builder.Configuration["Secrets:KeyVault:Url"];
            if (!string.IsNullOrEmpty(vaultUrl))
            {
                var credential = builder.Configuration["Secrets:KeyVault:UseManagedIdentity"] == "true"
                    ? (TokenCredential)new ManagedIdentityCredential()
                    : new DefaultAzureCredential();

                builder.Configuration.AddAzureKeyVault(
                    new Uri(vaultUrl),
                    credential,
                    new KeyVaultSecretManager());
            }
            break;

        case "Environment":
            break;

        case "AppSettings":
        default:
            break;
    }
}

public partial class Program;
