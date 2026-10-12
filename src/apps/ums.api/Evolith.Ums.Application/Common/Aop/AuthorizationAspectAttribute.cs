using System;
using BeyondNetCode.Shell.Aop.Aspects;

namespace Evolith.Ums.Application.Common.Aop;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false)]
public sealed class AuthorizationAspectAttribute : AbstractAspectAttribute
{
    public string? ResourceCode { get; set; }
    public string? ActionCode { get; set; }

    /// <summary>
    /// Evalúa y REGISTRA la denegación, pero deja pasar. Es la única forma honesta de encender la
    /// autorización sobre una API que llevaba 188 de 190 handlers sin comprobar nada: primero se
    /// mide qué se rompería, después se rompe.
    ///
    /// El caso concreto que lo motiva: la cuenta con la que se aprovisiona (`arranque.ums`) NO
    /// TIENE PERFIL — crea suites, roles y cuentas solo porque `is_internal_admin`, derivado del
    /// INQUILINO y no del rol, hace que `HasPermission` devuelva true. Encender la puerta sin
    /// medir antes deja la instancia sin nadie capaz de administrarla.
    ///
    /// Se puede fijar por atributo —para el handler que aún no se confía— o globalmente con
    /// `Authorization:Mode=AuditOnly`, que es como se hará la primera pasada.
    /// </summary>
    public bool AuditOnly { get; set; }

    public AuthorizationAspectAttribute() { }

    public AuthorizationAspectAttribute(string resourceCode, string actionCode)
    {
        ResourceCode = resourceCode;
        ActionCode = actionCode;
    }
}
