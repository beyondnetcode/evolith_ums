using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Evolith.Ums.Presentation.Bootstrapping;

/// <summary>
/// Filtro de operación de Swagger que añade la cabecera opcional <c>X-Language</c>
/// a cada endpoint documentado (selección de idioma; cae a Accept-Language y luego 'en').
/// </summary>
internal sealed class LanguageHeaderOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        operation.Parameters ??= new List<OpenApiParameter>();
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-Language",
            In = ParameterLocation.Header,
            Required = false,
            Description = "Language code (e.g. 'en', 'es'). Falls back to Accept-Language then 'en'.",
            Schema = new OpenApiSchema { Type = "string", Default = new OpenApiString("en") },
        });
    }
}
