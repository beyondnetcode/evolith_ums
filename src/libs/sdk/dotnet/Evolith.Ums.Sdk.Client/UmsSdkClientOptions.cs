namespace Evolith.Ums.Sdk.Client;

/// <summary>Options controlling the typed HTTP client behavior.</summary>
public sealed class UmsSdkClientOptions
{
    /// <summary>Base address of the UMS API (e.g. <c>https://ums.example.com</c>). Required.</summary>
    public Uri? BaseAddress { get; set; }

    /// <summary>Path of the client-auth endpoint. Defaults to <c>/api/v1/client/authenticate</c>.</summary>
    public string AuthenticatePath { get; set; } = "/api/v1/client/authenticate";

    /// <summary>HTTP request timeout. Defaults to 30 seconds.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}
