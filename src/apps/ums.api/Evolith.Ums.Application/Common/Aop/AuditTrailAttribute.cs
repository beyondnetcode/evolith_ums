namespace Evolith.Ums.Application.Common.Aop;

[AttributeUsage(AttributeTargets.Method)]
public sealed class AuditTrailAttribute : AbstractAspectAttribute
{
    public string? EventType { get; init; }

    public string? AffectedEntityType { get; init; }

    public string? WhatChanged { get; init; }

    public string? SubjectType { get; init; }

    public AuditTrailAttribute()
    {
        Order = 100;
    }
}
