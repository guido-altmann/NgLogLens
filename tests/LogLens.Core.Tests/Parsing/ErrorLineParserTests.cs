using LogLens.Core;
using LogLens.Core.Models;
using LogLens.Core.Parsing;
using LogLens.Core.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace LogLens.Core.Tests.Parsing;

public sealed class ErrorLineParserTests
{
    private static readonly LogLineParserChain Chain =
        new ServiceCollection().AddLogLensCore().BuildServiceProvider()
            .GetRequiredService<LogLineParserChain>();

    [Fact]
    public void Error_Zeile_wird_in_alle_Felder_zerlegt()
    {
        var parsed = Chain.Parse(FixtureLog.Line(2), 2);

        var entry = Assert.IsType<ParsedErrorLine>(parsed).Entry;
        Assert.Equal(2, entry.LineNumber);
        Assert.Equal(new DateTimeOffset(2026, 8, 27, 10, 16, 58, TimeSpan.Zero), entry.Timestamp);
        Assert.Equal(TimePrecision.Second, entry.TimePrecision);
        Assert.Equal("error", entry.Level);
        Assert.Equal("10.0.1.9", entry.ClientIp);
        Assert.Equal("GET /0x.php HTTP/1.1", entry.Request);
        Assert.Equal("example.de", entry.Host);
        Assert.StartsWith("""open() "/usr/share/nginx/html/404.html" failed""", entry.Message);
        Assert.False(entry.IsTruncated);
    }

    [Fact]
    public void Error_Zeile_ohne_Zusatzfelder_bleibt_lesbar()
    {
        var parsed = Chain.Parse(
            "2026/09/02 11:00:00 [warn] 30#30: *12 upstream server temporarily disabled", 1);

        var entry = Assert.IsType<ParsedErrorLine>(parsed).Entry;
        Assert.Equal("warn", entry.Level);
        Assert.Equal("upstream server temporarily disabled", entry.Message);
        Assert.Null(entry.ClientIp);
        Assert.Null(entry.Request);
        Assert.Null(entry.Host);
    }
}
