namespace Evolith.Ums.Domain.Configuration.FeatureFlag.Criteria;

public sealed class FeatureFlagCriteria : Entity<FeatureFlagCriteria, FeatureFlagCriteriaProps>
{
    private FeatureFlagCriteria(FeatureFlagCriteriaProps props) : base(props) { }

    public string CriteriaType => Props.CriteriaType;
    public string Operator => Props.Operator;
    public string Value => Props.Value;
    public DateTime CreatedAtUtc => Props.CreatedAtUtc;

    public static Result<FeatureFlagCriteria> Create(string criteriaType, string @operator, string value)
    {
        if (string.IsNullOrWhiteSpace(criteriaType))
            return Result<FeatureFlagCriteria>.Failure(DomainErrors.Configuration.CriteriaTypeRequired);
        if (string.IsNullOrWhiteSpace(@operator))
            return Result<FeatureFlagCriteria>.Failure(DomainErrors.Configuration.CriteriaOperatorRequired);
        if (string.IsNullOrWhiteSpace(value))
            return Result<FeatureFlagCriteria>.Failure(DomainErrors.Configuration.CriteriaValueRequired);

        var props = new FeatureFlagCriteriaProps(IdValueObject.Create(), criteriaType, @operator, value);
        return Result<FeatureFlagCriteria>.Success(new FeatureFlagCriteria(props));
    }

    public static FeatureFlagCriteria Load(Guid id, string criteriaType, string @operator, string value, DateTime createdAtUtc)
    {
        var props = new FeatureFlagCriteriaProps(IdValueObject.Load(id), criteriaType, @operator, value)
        {
            CreatedAtUtc = createdAtUtc
        };
        return new FeatureFlagCriteria(props);
    }
}
