namespace Evolith.Ums.Domain.Configuration.FeatureFlag.FlagEvaluationLog;

public class FlagEvaluationLogProps : IProps
{
    public IdValueObject Id { get; set; }
    public Guid EvaluatedBy { get; set; }
    public bool Result { get; set; }
    public string Context { get; set; }
    public DateTime EvaluatedAt { get; set; }

    public FlagEvaluationLogProps(IdValueObject id, Guid evaluatedBy, bool result, string context)
    {
        Id = id;
        EvaluatedBy = evaluatedBy;
        Result = result;
        Context = context;
        EvaluatedAt = DateTime.UtcNow;
    }

    public object Clone() => MemberwiseClone();
}
