# Evolith.Ums.Sdk.Authorization.AspNetCore

> Part of the [UMS SDK](https://github.com/beyondnetcode/ums/tree/main/docs/sdk).

ASP.NET Core middleware that places the `AuthorizationGraph` on `HttpContext.Items` for the rest of the pipeline (validator, AOP aspect, controllers).

## Install

```bash
dotnet add package Evolith.Ums.Sdk.Authorization.AspNetCore
```

## Use

```csharp
builder.Services.AddUmsSdkAuthorization();
builder.Services.AddHttpContextAuthGraphAccessor();

var app = builder.Build();

// Decodes JWT body → AuthorizationGraph → HttpContext.Items.
app.UseUmsAuthGraph(options =>
{
    // Obligatorio: sin el secreto no se puede comprobar que el portador lo firmara UMS, y el
    // grafo de autorizacion seria el que quisiera quien llama (G-217).
    options.SigningSecret = builder.Configuration["Jwt:Secret"]!;
    options.JwtBodyClaim = "graph";   // claim name carrying the serialized graph
    options.RejectExpiredGraphs = true;
});

app.UseAuthorization();
```

The middleware reads the `Authorization: Bearer ...` header, decodes the JWT body, and stores the resulting `AuthorizationGraph` instance at `HttpContext.Items["UmsAuthGraph"]`.

## License

MIT — see [LICENSE](https://github.com/beyondnetcode/ums/blob/main/src/libs/sdk/LICENSE).
