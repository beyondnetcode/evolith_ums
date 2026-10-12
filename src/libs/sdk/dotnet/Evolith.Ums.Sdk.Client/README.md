# Evolith.Ums.Sdk.Client

> Part of the [UMS SDK](https://github.com/beyondnetcode/ums/tree/main/docs/sdk).

Typed HTTP client for `POST /api/v1/client/authenticate`. Calls UMS, deserializes the `AuthorizationGraph`, validates schema compatibility and returns a typed `Result<ClientAuthResult>`.

## Install

```bash
dotnet add package Evolith.Ums.Sdk.Client
```

## Use

```csharp
builder.Services.AddUmsSdkClient(opt => opt.BaseAddress = new Uri("https://ums.example.com"));

public class Login(IUmsAuthClient client)
{
    public async Task<Result<ClientAuthResult>> AuthenticateAsync(string tenant, string user, string password)
        => await client.AuthenticateAsync(new ClientAuthRequest(tenant, user, password));
}
```

The response includes the JWT (`Token`), the `AuthorizationGraph` and the schema compat outcome.

## License

MIT — see [LICENSE](https://github.com/beyondnetcode/ums/blob/main/src/libs/sdk/LICENSE).
