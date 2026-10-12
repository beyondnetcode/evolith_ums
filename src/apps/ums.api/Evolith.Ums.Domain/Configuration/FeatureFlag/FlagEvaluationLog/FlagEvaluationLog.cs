namespace Evolith.Ums.Domain.Configuration.FeatureFlag.FlagEvaluationLog;

public sealed class FlagEvaluationLog : Entity<FlagEvaluationLog, FlagEvaluationLogProps>
{
    private FlagEvaluationLog(FlagEvaluationLogProps props) : base(props) { }

    public Guid EvaluatedBy => Props.EvaluatedBy;
    public bool Result => Props.Result;
    public string Context => Props.Context;
    public DateTime EvaluatedAt => Props.EvaluatedAt;

    public static FlagEvaluationLog Record(Guid evaluatedBy, bool result, string context)
    {
        var props = new FlagEvaluationLogProps(IdValueObject.Create(), evaluatedBy, result, context);
        return new FlagEvaluationLog(props);
    }
}
