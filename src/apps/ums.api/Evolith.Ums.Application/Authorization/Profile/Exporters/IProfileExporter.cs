using Evolith.Ums.Application.Authorization.Profile.DTOs;

namespace Evolith.Ums.Application.Authorization.Profile.Exporters;

public interface IProfileExporter
{
    string Export(ProfileDto profile, ExportConfiguration? configuration = null);
    string ContentType { get; }
    string FileExtension { get; }
}
