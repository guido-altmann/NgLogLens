using LogLens.Core;
using LogLens.Core.Models;
using LogLens.Core.Parsing;
using LogLens.Core.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace LogLens.Core.Tests.Parsing;

public sealed class AccessLineParserTests
{
    private static readonly LogLineParserChain Chain =
        new ServiceCollection().AddLogLensCore().BuildServiceProvider()
            .GetRequiredService<LogLineParserChain>();

    private static AccessEntry Parse(string line, int lineNumber = 1)
    {
        var parsed = Chain.Parse(line, lineNumber);
        var access = Assert.IsType<ParsedAccessLine>(parsed);
        return access.Entry;
    }

    [Fact]
    public void Vollstaendige_Zeile_wird_in_alle_Felder_zerlegt()
    {
        var entry = Parse(FixtureLog.Line(1), 1);

        Assert.Equal(1, entry.LineNumber);
        Assert.Equal(new DateTimeOffset(2026, 8, 27, 10, 16, 58, TimeSpan.Zero), entry.Timestamp);
        Assert.Equal(TimePrecision.Second, entry.TimePrecision);
        Assert.Equal("10.0.1.9", entry.ProxyIp);
        Assert.Equal("203.0.113.10", entry.ClientIp);
        Assert.Equal("GET", entry.Method);
        Assert.Equal("/0x.php", entry.Path);
        Assert.Null(entry.Query);
        Assert.Equal("HTTP/1.1", entry.Protocol);
        Assert.Equal(404, entry.Status);
        Assert.Equal(555, entry.Bytes);
        Assert.Null(entry.Referer);
        Assert.StartsWith("Mozilla/5.0 (Windows NT 10.0", entry.UserAgent);
        Assert.False(entry.IsTruncated);
    }

    [Fact]
    public void Zeitstempel_wird_nach_UTC_umgerechnet()
    {
        var entry = Parse(
            """10.0.1.9 - - [27/Aug/2026:12:16:58 +0200] "GET / HTTP/1.1" 200 8123 "-" "Chrome" "203.0.113.10" """.TrimEnd());

        Assert.Equal(new DateTimeOffset(2026, 8, 27, 10, 16, 58, TimeSpan.Zero), entry.Timestamp);
        Assert.Equal(TimeSpan.Zero, entry.Timestamp.Offset);
    }

    [Fact]
    public void Pfad_und_Query_werden_getrennt_gespeichert()
    {
        var entry = Parse(FixtureLog.Line(19), 19);

        Assert.Equal("/@fs/root/.aws/credentials", entry.Path);
        Assert.Equal("raw??", entry.Query);
    }

    [Fact]
    public void Query_bleibt_im_Rohzustand()
    {
        var entry = Parse(FixtureLog.Line(23), 23);

        Assert.Equal("/", entry.Path);
        Assert.Equal("command=%60echo+GSCAN_CMDI%60", entry.Query);
        Assert.Equal(200, entry.Status);
    }

    [Fact]
    public void Leerer_Referer_und_User_Agent_werden_zu_null()
    {
        var entry = Parse(FixtureLog.Line(21), 21);

        Assert.Null(entry.Referer);
        Assert.Null(entry.UserAgent);
        Assert.Equal(400, entry.Status);
    }

    [Fact]
    public void Referer_wird_uebernommen()
    {
        var entry = Parse(FixtureLog.Line(9), 9);

        Assert.Equal("https://www.google.com/", entry.Referer);
    }

    [Fact]
    public void Erste_IP_der_Weiterleitungskette_gilt_als_Client()
    {
        var entry = Parse(
            """10.0.1.9 - - [27/Aug/2026:10:16:58 +0000] "GET / HTTP/1.1" 200 8123 "-" "Chrome" "203.0.113.10, 198.51.100.7" """.TrimEnd());

        Assert.Equal("203.0.113.10", entry.ClientIp);
    }

    [Fact]
    public void Ohne_Weiterleitungsfeld_gilt_die_Proxy_IP_als_Client()
    {
        var entry = Parse(
            """203.0.113.10 - - [27/Aug/2026:10:16:58 +0000] "GET / HTTP/1.1" 200 8123 "-" "Chrome" """.TrimEnd());

        Assert.Equal("203.0.113.10", entry.ProxyIp);
        Assert.Equal("203.0.113.10", entry.ClientIp);
        Assert.False(entry.HasClientIpHeader);
    }

    [Fact]
    public void IPv6_Clients_werden_uebernommen()
    {
        var entry = Parse(
            """10.0.1.9 - - [27/Aug/2026:10:16:58 +0000] "GET / HTTP/1.1" 200 8123 "-" "Chrome" "2001:db8::1" """.TrimEnd());

        Assert.Equal("2001:db8::1", entry.ClientIp);
    }

    [Theory]
    [InlineData("-")]
    [InlineData("\\x16\\x03\\x01")]
    [InlineData("GET")]
    public void Ungueltige_Request_Zeilen_bleiben_als_Anfrage_ohne_Methode_erhalten(string request)
    {
        var entry = Parse(
            $"""10.0.1.9 - - [27/Aug/2026:10:16:58 +0000] "{request}" 400 157 "-" "-" "203.0.113.10" """.TrimEnd());

        Assert.Null(entry.Method);
        Assert.Null(entry.Path);
        Assert.Null(entry.Query);
        Assert.Equal(400, entry.Status);
        Assert.False(entry.IsTruncated);
    }

    [Fact]
    public void Fehlende_Groesse_und_fehlender_Status_werden_zu_null()
    {
        var entry = Parse(
            """10.0.1.9 - - [27/Aug/2026:10:16:58 +0000] "GET / HTTP/1.1" - - "-" "Chrome" "203.0.113.10" """.TrimEnd());

        Assert.Null(entry.Status);
        Assert.Null(entry.Bytes);
    }
}
