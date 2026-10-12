namespace Evolith.Ums.Application.Identity.UserAccount.DTOs;

public sealed record MfaEnrollmentDto(
    Guid EnrollmentId,
    string Method,
    string Status,
    DateTime EnrolledAt);
