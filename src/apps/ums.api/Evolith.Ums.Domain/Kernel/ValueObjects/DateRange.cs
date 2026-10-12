namespace Evolith.Ums.Domain.Kernel.ValueObjects;

public record DateRange
{
    public DateTimeOffset StartsAt { get; init; }
    public DateTimeOffset? EndsAt { get; init; }

    private DateRange(DateTimeOffset startsAt, DateTimeOffset? endsAt)
    {
        StartsAt = startsAt;
        EndsAt = endsAt;
    }

    public static Result<DateRange> Create(DateTimeOffset startsAt, DateTimeOffset? endsAt = null)
    {
        if (endsAt.HasValue && endsAt.Value < startsAt)
        {
            return Result<DateRange>.Failure(DomainErrors.ValueObject.DateRangeInvalid);
        }
        return Result<DateRange>.Success(new DateRange(startsAt, endsAt));
    }

    public static Result<DateRange> Default()
    {
        return Result<DateRange>.Success(new DateRange(DateTimeOffset.MinValue, null));
    }

    public bool Contains(DateTimeOffset value) => value >= StartsAt && (!EndsAt.HasValue || value <= EndsAt.Value);
}
