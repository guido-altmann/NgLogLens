using LogLens.Core;
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
}
