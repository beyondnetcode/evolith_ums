namespace Evolith.Ums.Infrastructure.Approvals.ApprovalRequest;

using BeyondNetCode.Shell.Factory.Impl;

internal sealed class ApprovalRequestCreationStrategyFactorySetup : AbstractFactorySetupSource
{
    public ApprovalRequestCreationStrategyFactorySetup()
    {
        For<ApprovalRequestCreationStrategyCriteria, IApprovalRequestCreationStrategy>()
            .Create<ManualApprovalRequestCreationStrategy>()
            .When(criteria => criteria.RequiresApproval);

        For<ApprovalRequestCreationStrategyCriteria, IApprovalRequestCreationStrategy>()
            .Create<AutoApproveApprovalRequestCreationStrategy>()
            .When(criteria => !criteria.RequiresApproval);
    }
}
