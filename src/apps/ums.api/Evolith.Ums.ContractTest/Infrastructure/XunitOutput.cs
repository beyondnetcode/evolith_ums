using PactNet.Infrastructure.Outputters;
using Xunit;

namespace Evolith.Ums.ContractTest.Infrastructure;

public class XunitOutput : IOutput
{
    private readonly ITestOutputHelper _output;

    public XunitOutput(ITestOutputHelper output)
    {
        _output = output;
    }

    public void WriteLine(string line)
    {
        _output.WriteLine(line);
    }
}
