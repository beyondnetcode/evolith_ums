using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Evolith.Ums.Application.Authorization.Profile.Commands;
using Evolith.Ums.Application.Common.Behaviors;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Configuration.AppConfiguration.Commands;
using Evolith.Ums.Domain.Authorization.Graph;
using Evolith.Ums.Domain.Kernel;
using Xunit;

namespace Evolith.Ums.Application.Test.Common.Behaviors;

/// <summary>
/// El ÚNICO punto donde se invalida el grafo retenido, y por tanto donde se decide el alcance.
///
/// <para>Las pruebas usan comandos REALES y no dobles: lo que se está fijando es a qué contador va
/// cada familia de comandos, y eso depende de su espacio de nombres de verdad. Con un doble, la
/// prueba pasaría aunque alguien moviera los comandos de sitio — que es justo el cambio que
/// rompería el enrutado en silencio.</para>
/// </summary>
public sealed class InvalidacionDeGrafoBehaviorTests
{
    private static readonly Guid Inquilino = Guid.NewGuid();

    [Fact]
    public async Task Un_comando_de_autorizacion_invalida_solo_su_inquilino()
    {
        var cache = new CacheEspia();
        var behavior = Crear<AssignTemplateToProfileCommand>(cache, new InquilinoFalso(Inquilino));

        await behavior.Handle(
            new AssignTemplateToProfileCommand(Guid.NewGuid(), Guid.NewGuid()),
            () => Task.FromResult(Result.Success()),
            CancellationToken.None);

        cache.InquilinosInvalidados.Should().Equal(Inquilino);
        cache.VecesGlobal.Should().Be(0,
            "el modelo de autorización está acotado al inquilino: tirar los grafos de los demás sería gratuito");
    }

    [Fact]
    public async Task Un_comando_de_configuracion_invalida_a_todos()
    {
        // `FeatureFlagProps.TenantId` y `AppConfiguration.TenantId` son ANULABLES: sin inquilino,
        // el cambio alcanza a todos, y acotarlo al del contexto dejaría grafos obsoletos vivos.
        var cache = new CacheEspia();
        var behavior = Crear<ArchiveAppConfigurationCommand>(cache, new InquilinoFalso(Inquilino));

        await behavior.Handle(
            new ArchiveAppConfigurationCommand(Guid.NewGuid()),
            () => Task.FromResult(Result.Success()),
            CancellationToken.None);

        cache.VecesGlobal.Should().Be(1);
        cache.InquilinosInvalidados.Should().BeEmpty();
    }

    [Fact]
    public async Task Sin_inquilino_en_contexto_se_invalida_a_todos()
    {
        // Administrador interno operando entre inquilinos: no hay uno al que acotar, así que se
        // invalida de más. Es el lado seguro del error.
        var cache = new CacheEspia();
        var behavior = Crear<AssignTemplateToProfileCommand>(cache, new InquilinoFalso(null));

        await behavior.Handle(
            new AssignTemplateToProfileCommand(Guid.NewGuid(), Guid.NewGuid()),
            () => Task.FromResult(Result.Success()),
            CancellationToken.None);

        cache.VecesGlobal.Should().Be(1);
    }

    [Fact]
    public async Task Un_comando_que_falla_no_invalida_nada()
    {
        var cache = new CacheEspia();
        var behavior = Crear<AssignTemplateToProfileCommand>(cache, new InquilinoFalso(Inquilino));

        await behavior.Handle(
            new AssignTemplateToProfileCommand(Guid.NewGuid(), Guid.NewGuid()),
            () => Task.FromResult(Result.Failure("no se pudo")),
            CancellationToken.None);

        cache.VecesGlobal.Should().Be(0);
        cache.InquilinosInvalidados.Should().BeEmpty(
            "un comando que falló no cambió nada; invalidar regalaría trabajo a cada intento inválido");
    }

    [Fact]
    public async Task Una_consulta_no_invalida_nada()
    {
        var cache = new CacheEspia();
        var behavior = new InvalidacionDeGrafoBehavior<ConsultaCualquiera, Result>(
            cache, new InquilinoFalso(Inquilino),
            NullLogger<InvalidacionDeGrafoBehavior<ConsultaCualquiera, Result>>.Instance);

        await behavior.Handle(
            new ConsultaCualquiera(), () => Task.FromResult(Result.Success()), CancellationToken.None);

        cache.VecesGlobal.Should().Be(0);
        cache.InquilinosInvalidados.Should().BeEmpty();
    }

    [Fact]
    public async Task Si_la_invalidacion_revienta_el_comando_sigue_siendo_correcto()
    {
        // El comando YA se confirmó. Propagar aquí un fallo del almacén devolvería un error por algo
        // que sí ocurrió, y el llamante lo reintentaría —aplicándolo dos veces—.
        var behavior = Crear<AssignTemplateToProfileCommand>(new CacheQueRevienta(), new InquilinoFalso(Inquilino));

        var resultado = await behavior.Handle(
            new AssignTemplateToProfileCommand(Guid.NewGuid(), Guid.NewGuid()),
            () => Task.FromResult(Result.Success()),
            CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
    }

    private static InvalidacionDeGrafoBehavior<TComando, Result> Crear<TComando>(
        ICacheDeGrafo cache, ITenantContext inquilino)
        where TComando : notnull
        => new(cache, inquilino, NullLogger<InvalidacionDeGrafoBehavior<TComando, Result>>.Instance);

    private sealed record ConsultaCualquiera;

    private sealed class CacheEspia : ICacheDeGrafo
    {
        public List<Guid> InquilinosInvalidados { get; } = [];

        public int VecesGlobal { get; private set; }

        public Task InvalidarInquilinoAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            InquilinosInvalidados.Add(tenantId);
            return Task.CompletedTask;
        }

        public Task InvalidarGlobalAsync(CancellationToken cancellationToken = default)
        {
            VecesGlobal++;
            return Task.CompletedTask;
        }

        public Task InvalidarPerfilAsync(Guid profileId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<string?> ObtenerAsync(string claveDeGrafo, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);

        public Task GuardarAsync(string claveDeGrafo, AuthorizationGraph grafo, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<string> ClaveAsync(
            Guid? tenantId, Guid profileId, string? systemCode, string? proyeccion, CancellationToken cancellationToken = default)
            => Task.FromResult("clave");
    }

    private sealed class CacheQueRevienta : ICacheDeGrafo
    {
        public Task InvalidarInquilinoAsync(Guid tenantId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("el almacén no responde");

        public Task InvalidarGlobalAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("el almacén no responde");

        public Task InvalidarPerfilAsync(Guid profileId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("el almacén no responde");

        public Task<string?> ObtenerAsync(string claveDeGrafo, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);

        public Task GuardarAsync(string claveDeGrafo, AuthorizationGraph grafo, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<string> ClaveAsync(
            Guid? tenantId, Guid profileId, string? systemCode, string? proyeccion, CancellationToken cancellationToken = default)
            => Task.FromResult("clave");
    }

    private sealed class InquilinoFalso(Guid? organizacion) : ITenantContext
    {
        public Guid? OrganizationId { get; private set; } = organizacion;

        public Guid? OriginalTenantId => OrganizationId;

        public bool IsInternalAdmin => OrganizationId is null;

        public void SetOrganizationId(Guid organizationId) => OrganizationId = organizationId;

        public void EnableCrossTenantAccess() => OrganizationId = null;

        public void DisableCrossTenantAccess() { }

        public void Initialize(Guid userTenantId, bool isInternalAdmin) => OrganizationId = userTenantId;
    }
}
