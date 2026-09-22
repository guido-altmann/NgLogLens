using LogLens.Core;
using LogLens.Core.Models;
using LogLens.Core.Parsing;
using LogLens.Core.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace LogLens.Core.Tests.Parsing;

public sealed class TruncatedLineParserTests
{
    private static readonly LogLineParserChain Chain =
        new ServiceCollection().AddLogLensCore().BuildServiceProvider()
            .GetRequiredService<LogLineParserChain>();

    [Fact]
    public void Gekuerzte_Access_Zeile_liefert_Datum_Proxy_und_Client()
    {
        var parsed = Chain.Parse(FixtureLog.Line(31), 31);

        var entry = Assert.IsType<ParsedAccessLine>(parsed).Entry;
        Assert.True(entry.IsTruncated);
        Assert.Equal("10.0.1.9", entry.ProxyIp);
        Assert.Equal("216.73.216.15", entry.ClientIp);
        Assert.Equal(new DateTimeOffset(2026, 8, 28, 0, 0, 0, TimeSpan.Zero), entry.Timestamp);
        Assert.Equal(TimePrecision.Day, entry.TimePrecision);
        Assert.Null(entry.Status);
        Assert.Null(entry.Path);
        Assert.Null(entry.Method);
    }

    [Fact]
    public void Gekuerzte_Access_Zeile_behaelt_den_Resttext_fuer_die_Agent_Erkennung()
    {
        var parsed = Chain.Parse(FixtureLog.Line(31), 31);

        var entry = Assert.IsType<ParsedAccessLine>(parsed).Entry;
        Assert.Contains("anthropic.com", entry.UserAgent);
    }

    [Fact]
    public void Gekuerzte_Access_Zeile_mit_Uhrzeit_behaelt_die_Stunde()
    {
        var parsed = Chain.Parse(
            """10.0.1.9 - - [28/Aug/2026:03:<REDACTED>@anthropic.com)" "216.73.216.15" """.TrimEnd(), 1);

        var entry = Assert.IsType<ParsedAccessLine>(parsed).Entry;
        Assert.Equal(new DateTimeOffset(2026, 8, 28, 3, 0, 0, TimeSpan.Zero), entry.Timestamp);
        Assert.Equal(TimePrecision.Hour, entry.TimePrecision);
    }

    [Fact]
    public void Gekuerzte_Error_Zeile_behaelt_die_bekannte_Stunde()
    {
        var parsed = Chain.Parse(FixtureLog.Line(32), 32);

        var entry = Assert.IsType<ParsedErrorLine>(parsed).Entry;
        Assert.True(entry.IsTruncated);
        Assert.Equal(new DateTimeOffset(2026, 9, 1, 2, 0, 0, TimeSpan.Zero), entry.Timestamp);
        Assert.Equal(TimePrecision.Hour, entry.TimePrecision);
    }

    [Fact]
    public void Zeile_ohne_gueltige_Schluss_IP_ist_keine_gekuerzte_Access_Zeile()
    {
        var parsed = Chain.Parse(
            """10.0.1.9 - - [28/Aug/2026:03:<REDACTED>@anthropic.com)" "kein-ip-wert" """.TrimEnd(), 1);

        Assert.Null(parsed);
    }
}
