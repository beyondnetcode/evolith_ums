namespace Evolith.Ums.Domain.Configuration.Parameter.ValueObjects;

public sealed class ParameterDataType : DomainEnumeration
{
    public static readonly ParameterDataType String = new(1, nameof(String));
    public static readonly ParameterDataType Number = new(2, nameof(Number));
    public static readonly ParameterDataType Boolean = new(3, nameof(Boolean));
    public static readonly ParameterDataType Json = new(4, nameof(Json));

    private ParameterDataType(int value, string name) : base(value, name) { }

    public static IEnumerable<ParameterDataType> AllTypes => [String, Number, Boolean, Json];

    public static ParameterDataType FromValue(int value) =>
        AllTypes.FirstOrDefault(t => t.Id == value)
        ?? throw new ArgumentOutOfRangeException(nameof(value), $"Invalid data type value: {value}");
}