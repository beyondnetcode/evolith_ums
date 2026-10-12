namespace Evolith.Ums.Application.Audit.AuditRecord.Commands;

using FluentValidation;

public sealed class RecordAuditCommandValidator : AbstractValidator<RecordAuditCommand>
{
    public RecordAuditCommandValidator()
    {
        RuleFor(command => command.WhoActed).NotEmpty();
        RuleFor(command => command.WhatChanged).NotEmpty().MaximumLength(2000);
        RuleFor(command => command.EventType).NotEmpty().MaximumLength(100);
        RuleFor(command => command.AffectedEntityId).NotEmpty();
        RuleFor(command => command.AffectedEntityType).NotEmpty().MaximumLength(100);
        RuleFor(command => command.RootTenantId).NotEmpty();

        // G-040: el Metadata de auditoría es opcional pero, si viene, debe estar acotado en tamaño y ser
        // JSON válido — evita registros de auditoría con payloads gigantes o malformados/falsificables.
        RuleFor(command => command.Metadata)
            .MaximumLength(4000)
            .Must(BeValidJsonWhenPresent)
            .WithMessage("Metadata must be well-formed JSON when provided.");
    }

    private static bool BeValidJsonWhenPresent(string? metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata))
        {
            return true;
        }

        try
        {
            using var _ = System.Text.Json.JsonDocument.Parse(metadata);
            return true;
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }
}
