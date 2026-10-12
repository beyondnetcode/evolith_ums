namespace Evolith.Ums.Domain.Configuration.FeatureFlag.Criteria;

public class FeatureFlagCriteriaProps : IProps
{
    public IdValueObject Id { get; set; }
    public string CriteriaType { get; set; }
    public string Operator { get; set; }
    public string Value { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public FeatureFlagCriteriaProps(IdValueObject id, string criteriaType, string @operator, string value)
    {
        Id = id;
        CriteriaType = criteriaType;
        Operator = @operator;
        Value = value;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public object Clone() => MemberwiseClone();
}
