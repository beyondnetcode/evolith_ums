namespace Evolith.Ums.Domain.Kernel.ValueObjects;

public class TemplateVersion : StringValueObject, IComparable<TemplateVersion>
{
    private TemplateVersion(string value) : base(value) { }

    public static TemplateVersion Create(int major, int minor, int patch)
    {
        return new TemplateVersion($"{major}.{minor}.{patch}");
    }

    public static TemplateVersion Initial() => new TemplateVersion("0.1.0");

    /// <summary>Segmento MAJOR del semver (0 si el valor no es parseable).</summary>
    public int Major => Segment(0);

    /// <summary>Segmento MINOR del semver (0 si el valor no es parseable).</summary>
    public int Minor => Segment(1);

    /// <summary>Segmento PATCH del semver (0 si el valor no es parseable).</summary>
    public int Patch => Segment(2);

    /// <summary>
    /// Devuelve la versión inmediatamente siguiente incrementando el segmento MINOR y
    /// reiniciando PATCH (p. ej. 0.1.0 → 0.2.0). Cf. G-140 / ADR-UMS-140: el alta de una
    /// plantilla sobre una terna (tenant, rol, suite) ya plantillada genera una revisión nueva.
    /// </summary>
    public TemplateVersion Next() => Create(Major, Minor + 1, 0);

    public int CompareTo(TemplateVersion? other)
    {
        if (other is null) return 1;
        var major = Major.CompareTo(other.Major);
        if (major != 0) return major;
        var minor = Minor.CompareTo(other.Minor);
        if (minor != 0) return minor;
        return Patch.CompareTo(other.Patch);
    }

    private int Segment(int index)
    {
        var value = GetValue();
        if (string.IsNullOrWhiteSpace(value)) return 0;
        var parts = value.Split('.');
        return index < parts.Length && int.TryParse(parts[index], out var n) ? n : 0;
    }

    public override void AddValidators()
    {
        base.AddValidators();
        ValidatorRules.Add(new TemplateVersionValidator(this));
    }
}

public partial class TemplateVersionValidator : AbstractRuleValidator<ValueObject<string>>
{
    public TemplateVersionValidator(ValueObject<string> subject) : base(subject) { }

    public override void AddRules(BeyondNetCode.Shell.Ddd.Rules.RuleContext? context)
    {
        var value = Subject.GetValue();

        if (string.IsNullOrWhiteSpace(value))
        {
            AddBrokenRule(nameof(TemplateVersion), DomainErrors.ValueObject.PropertyRequired);
            return;
        }

        if (!SemVerPattern().IsMatch(value))
        {
            AddBrokenRule(nameof(TemplateVersion), DomainErrors.Authorization.InvalidTemplateVersion);
        }
    }

    [global::System.Text.RegularExpressions.GeneratedRegex(@"^\d+\.\d+\.\d+$")]
    private static partial global::System.Text.RegularExpressions.Regex SemVerPattern();
}
