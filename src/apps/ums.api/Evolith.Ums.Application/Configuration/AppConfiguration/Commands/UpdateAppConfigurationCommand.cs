namespace Evolith.Ums.Application.Configuration.AppConfiguration.Commands;

public sealed record UpdateAppConfigurationCommand(
    Guid AppConfigurationId,
    string Value,
    string Description,
    /// <summary>
    /// REC-10: Optional RowVersion received from the client's If-Match header (decoded from base64).
    /// When provided, EF Core checks this value against the current DB RowVersion before saving.
    /// If the row was modified concurrently, EF throws DbUpdateConcurrencyException → HTTP 409.
    /// </summary>
    byte[]? RowVersion = null) : ICommand;
