namespace Evolith.Ums.Application.Common;

using BeyondNetCode.Shell.Ddd;

internal static class DomainEnumerationParser
{
    public static T? FromName<T>(string? name)
        where T : DomainEnumeration
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return DomainEnumeration
            .GetAll<T>()
            .FirstOrDefault(value => string.Equals(value.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Nombres que declara el enumerado de dominio, ordenados por su identificador.
    ///
    /// Existe para que los validadores y los mensajes de error DERIVEN del dominio en vez de
    /// copiarlo a mano: una lista copiada envejece en silencio y acaba negando lo que el dominio
    /// sí admite —G-192, donde <c>ExclusiveArcTarget</c> incorporó <c>Aggregate</c> y <c>Entity</c>
    /// y la copia de la capa de aplicación se quedó en los cuatro destinos de navegación—.
    /// </summary>
    public static IReadOnlyList<string> NamesOf<T>()
        where T : DomainEnumeration
        => DomainEnumeration
            .GetAll<T>()
            .OrderBy(value => value.Id)
            .Select(value => value.Name)
            .ToArray();
}
