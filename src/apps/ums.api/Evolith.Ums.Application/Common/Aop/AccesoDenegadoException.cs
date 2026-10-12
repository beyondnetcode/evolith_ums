namespace Evolith.Ums.Application.Common.Aop;

/// <summary>
/// El sujeto está identificado y NO tiene el permiso que la operación exige.
///
/// <para>Existe para no seguir diciendo 401 donde corresponde 403. El aspecto lanzaba
/// <see cref="UnauthorizedAccessException"/>, que el borde traduce a «debe iniciar sesión de
/// nuevo» — y eso es mentira dos veces: la sesión es válida y volver a entrar no cambiaría nada.
/// Quien recibe un 401 reintenta autenticándose; quien recibe un 403 sabe que tiene que pedir
/// acceso. La diferencia no es de etiqueta: cambia lo que hace el cliente y lo que investiga
/// quien soporta.</para>
///
/// <para>Es la misma regla que ADR-UMS-100 fijó para el login —el estado no puede afirmar algo
/// distinto de lo que ocurrió—, aplicada al otro lado de la autenticación.</para>
/// </summary>
public sealed class AccesoDenegadoException : Exception
{
    /// <summary>Permiso que faltaba, en la forma `recurso:accion`. Viaja para el registro, no para el cliente.</summary>
    public string Permiso { get; }

    public AccesoDenegadoException(string permiso)
        : base($"Access denied. Required permission: '{permiso}'.")
        => Permiso = permiso;
}
