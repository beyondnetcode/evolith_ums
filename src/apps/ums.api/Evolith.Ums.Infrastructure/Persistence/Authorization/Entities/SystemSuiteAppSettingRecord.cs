namespace Evolith.Ums.Infrastructure.Persistence.Authorization.Entities;

public sealed class SystemSuiteAppSettingRecord
{
    public Guid Id { get; set; }
    public Guid SystemSuiteId { get; set; }
    public string ConfigKey { get; set; } = string.Empty;
    public string ConfigValue { get; set; } = string.Empty;
    public int ScopeId { get; set; }

    /// <summary>Marca de exposición al cliente (G-178). Falso por defecto: fail-closed.</summary>
    public bool IsClientVisible { get; set; }

    public SystemSuiteRecord SystemSuite { get; set; } = null!;
}
