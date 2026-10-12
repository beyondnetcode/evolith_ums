namespace Evolith.Ums.Domain.Authorization.SystemSuite.AppSetting;

public class AppSetting
{
    public ConfigurationKey Key { get; }
    public ConfigurationValue Value { get; }
    public ConfigurationScope Scope { get; }

    /// <summary>
    /// Si este ajuste puede viajar al cliente en el grafo de autorización.
    ///
    /// El default es <c>false</c> y eso es deliberado (fail-closed). `AppSetting` es una bolsa
    /// clave/valor sin tipo: junto al color de la marca puede haber una cadena de conexión o el
    /// secreto de una integración. Volcarla entera al cliente por comodidad sería publicar
    /// secretos por omisión — que un ajuste no esté marcado significa «nadie ha decidido que sea
    /// público», no «no pasa nada» (G-178).
    ///
    /// A diferencia de <c>AppConfiguration</c> y <c>TenantParameter</c>, que sí distinguen lo
    /// sensible, aquí no había ninguna marca.
    /// </summary>
    public bool IsClientVisible { get; }

    private AppSetting(ConfigurationKey key, ConfigurationValue value, ConfigurationScope scope, bool isClientVisible)
    {
        Key = key;
        Value = value;
        Scope = scope;
        IsClientVisible = isClientVisible;
    }

    public static Result<AppSetting> Create(
        ConfigurationKey key,
        ConfigurationValue value,
        ConfigurationScope scope,
        bool isClientVisible = false)
    {
        if (string.IsNullOrWhiteSpace(key.GetValue()))
        {
            return Result<AppSetting>.Failure(DomainErrors.ValueObject.PropertyRequired);
        }

        if (string.IsNullOrWhiteSpace(value.GetValue()))
        {
            return Result<AppSetting>.Failure(DomainErrors.ValueObject.PropertyRequired);
        }

        return Result<AppSetting>.Success(new AppSetting(key, value, scope, isClientVisible));
    }

    /// <summary>Cambia el valor CONSERVANDO la marca de exposición: cambiar un valor no es decidir publicarlo.</summary>
    public AppSetting WithValue(ConfigurationValue newValue) => new(Key, newValue, Scope, IsClientVisible);

    public override bool Equals(object? obj)
    {
        if (obj is not AppSetting other) return false;
        return Key.GetValue() == other.Key.GetValue() && Scope.Id == other.Scope.Id;
    }

    public override int GetHashCode() => HashCode.Combine(Key.GetValue(), Scope.Id);
}
