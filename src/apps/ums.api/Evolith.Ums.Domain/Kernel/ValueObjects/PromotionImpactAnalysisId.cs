namespace Evolith.Ums.Domain.Kernel.ValueObjects;

public class PromotionImpactAnalysisId : IdValueObject
{
    private PromotionImpactAnalysisId(Guid value) : base(value) { }
    public static new PromotionImpactAnalysisId Create() => new PromotionImpactAnalysisId(Guid.NewGuid());
    public static new PromotionImpactAnalysisId Load(Guid value) => new PromotionImpactAnalysisId(value);
    public static new PromotionImpactAnalysisId Load(string value) => new PromotionImpactAnalysisId(Guid.Parse(value));
}
