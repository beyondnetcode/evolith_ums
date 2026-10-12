namespace Evolith.Ums.Application.Test.Authorization.Template;

using System;
using System.Linq;
using BeyondNetCode.Shell.Ddd;
using Evolith.Ums.Application.Authorization.Template.Commands;
using Evolith.Ums.Domain.Enums;
using Xunit;

/// <summary>
/// G-192 — el validador del alta de ítems de plantilla NO puede tener su propia idea de qué
/// destinos existen. La verdad la fija <see cref="ExclusiveArcTarget"/>: el agregado
/// <c>PermissionTemplate.AddItem</c> acepta cualquiera de sus valores, <c>Profile.AssignTemplate</c>
/// los copia al permiso del perfil y <c>BuildDomainPermissions</c> proyecta los que apuntan a un
/// recurso de dominio. La copia a mano que vivía aquí negaba <c>Aggregate</c> y <c>Entity</c>, y con
/// ello dejaba sin camino de alta a toda concesión sobre objetos de dominio.
/// </summary>
public class AddTemplateItemCommandValidatorTests
{
    private readonly AddTemplateItemCommandValidator _validator = new();

    private static AddTemplateItemCommand Comando(string targetType) => new(
        TemplateId: Guid.NewGuid(),
        TargetType: targetType,
        TargetId: Guid.NewGuid(),
        ActionId: Guid.NewGuid(),
        IsAllowed: true,
        IsDenied: false);

    public static TheoryData<string> DestinosDelDominio()
    {
        var data = new TheoryData<string>();
        foreach (var nombre in DomainEnumeration.GetAll<ExclusiveArcTarget>().Select(t => t.Name))
        {
            data.Add(nombre);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(DestinosDelDominio))]
    public void Validate_AdmiteTodoDestinoDeclaradoPorElDominio(string targetType)
    {
        var resultado = _validator.Validate(Comando(targetType));

        Assert.True(resultado.IsValid, $"El destino '{targetType}' lo declara el dominio y el validador lo rechazó: {resultado}");
    }

    [Theory]
    [InlineData("Aggregate")]
    [InlineData("Entity")]
    public void Validate_AdmiteLosObjetosDeDominio(string targetType)
    {
        // Caso concreto de G-192: son los dos destinos que la lista copiada había dejado fuera.
        Assert.True(_validator.Validate(Comando(targetType)).IsValid);
    }

    [Theory]
    [InlineData("aggregate")]
    [InlineData("  Entity  ")]
    public void Validate_NoDistingueMayusculasNiEspacios_IgualQueElManejador(string targetType)
    {
        // El validador y el manejador deben resolver el destino con el MISMO criterio: si el
        // validador fuese más laxo, el alta pasaría el 400 y moriría después con un error opaco.
        Assert.True(_validator.Validate(Comando(targetType)).IsValid);
        Assert.NotNull(Evolith.Ums.Application.Common.DomainEnumerationParser.FromName<ExclusiveArcTarget>(targetType));
    }

    [Theory]
    [InlineData("DomainResource")]   // no es un destino del arco: el recurso se apunta como Aggregate o Entity
    [InlineData("Page")]
    [InlineData("")]
    public void Validate_RechazaDestinosQueElDominioNoDeclara(string targetType)
    {
        Assert.False(_validator.Validate(Comando(targetType)).IsValid);
    }

    [Fact]
    public void Mensaje_EnumeraLosDestinosQueElDominioDeclara()
    {
        // El mensaje se DERIVA del enumerado: si mañana el dominio incorpora un destino, el texto
        // lo refleja solo. Antes recitaba cuatro nombres fijos que ya no eran la verdad.
        var resultado = _validator.Validate(Comando("NoExiste"));

        var mensaje = string.Join(" ", resultado.Errors.Select(e => e.ErrorMessage));
        foreach (var nombre in DomainEnumeration.GetAll<ExclusiveArcTarget>().Select(t => t.Name))
        {
            Assert.Contains(nombre, mensaje, StringComparison.Ordinal);
        }
    }
}
