namespace Evolith.Ums.Application;

using Evolith.Ums.Application.Common.Behaviors;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.IGA.Services;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(configuration => configuration.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        // Después de validar: solo invalida lo que llegó a ejecutarse y salió bien. Es el ÚNICO
        // punto donde se invalida el grafo retenido, a propósito — ver InvalidacionDeGrafoBehavior.
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(InvalidacionDeGrafoBehavior<,>));
        services.AddScoped<ITenantScopePolicy, TenantScopePolicy>();
        services.AddScoped<IUserManagementDelegationAccessService, UserManagementDelegationAccessService>();

        // IGA (ADR-UMS-093): puntuación versionada de RiskScore, detrás del punto de extensión.
        //
        // Aquí colgaba `HeuristicRiskScoreCalculator` (iga-risk-heuristic-v1), que puntuaba por el
        // SALTO EN EL ORGANIGRAMA. Ese número ordena al revés justo donde importa: «auditor de solo
        // lectura → administrador con todas las escrituras» es un salto de nivel pequeño y salía 15,
        // mientras una promoción lateral que no gana casi nada salía 20. Con el umbral en 70, la
        // promoción más grande de la suite se libraba de la revisión reforzada.
        //
        // `RiesgoPorDiferencialDePermisos` (iga-risk-graph-v2) puntúa lo que se GANA: escrituras
        // nuevas sobre la superficie de escritura de la suite, más un salto de banda cuando el
        // destino reúne editar y aprobar el mismo artefacto. La v1 se conserva intacta —no está
        // registrada, pero sigue compilando y con sus pruebas— para poder comparar corridas.
        services.AddScoped<IRiskScoreCalculator, RiesgoPorDiferencialDePermisos>();

        return services;
    }
}
