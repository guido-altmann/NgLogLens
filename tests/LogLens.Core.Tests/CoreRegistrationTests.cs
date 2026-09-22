using LogLens.Core;
using LogLens.Core.Findings;
using Microsoft.Extensions.DependencyInjection;

namespace LogLens.Core.Tests;

public sealed class CoreRegistrationTests
{
    [Fact]
    public void AddLogLensCore_stellt_ParserOptions_bereit()
    {
        var provider = new ServiceCollection().AddLogLensCore().BuildServiceProvider();

        var options = provider.GetRequiredService<ParserOptions>();

        Assert.Equal(2_000, options.YieldInterval);
        Assert.Equal(50, options.MaxUnknownSamples);
    }

    [Fact]
    public void AddLogLensCore_registriert_alle_Findings_Regeln_in_der_Reihenfolge_der_Spezifikation()
    {
        var provider = new ServiceCollection().AddLogLensCore().BuildServiceProvider();

        Assert.Equal(
            [
                "missing-404-page",
                "client-ip-header",
                "scan-burst",
                "llms-txt",
                "agent-discovery",
                "dotnet-config",
                "missing-assets",
                "server-errors",
            ],
            provider.GetRequiredService<IEnumerable<IFindingRule>>().Select(r => r.Id));
    }
}
