using BeyondNetCode.Shell.Factory.Impl;
using Evolith.Ums.Application.Authorization.Profile.Exporters;

namespace Evolith.Ums.Infrastructure.Persistence.Authorization.Exporters;

internal sealed class ProfileExportFactorySetup : AbstractFactorySetupSource
{
    public ProfileExportFactorySetup()
    {
        For<ProfileExportCriteria, IProfileExporter>().Create<ProfileJsonExporter>().When(x => x.Format == "JSON");
        For<ProfileExportCriteria, IProfileExporter>().Create<ProfileXmlExporter>().When(x => x.Format == "XML");
        For<ProfileExportCriteria, IProfileExporter>().Create<ProfileYamlExporter>().When(x => x.Format == "YAML");
        For<ProfileExportCriteria, IProfileExporter>().Create<ProfileCsvExporter>().When(x => x.Format == "CSV");
    }
}
