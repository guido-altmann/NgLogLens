using LogLens.Core;
using LogLens.Web.Services;

namespace LogLens.Web.Tests;

/// <summary>Maskierung der Besucher-IPs (CLAUDE.md, Datenschutz).</summary>
public sealed class IpDisplayTests
{
    private readonly ViewPreferences _preferences = new();

    private IpDisplay Display => new(new AggregationOptions(), _preferences);

    [Theory]
    [InlineData("198.51.100.23", "198.51.100.0/24")]
    [InlineData("2001:db8:1234:5678::1", "2001:db8:1234::/48")]
    public void Besucher_IPs_werden_maskiert(string ip, string expected)
    {
        Assert.Equal(expected, Display.Format(ip, isScanner: false));
    }

    [Fact]
    public void Scanner_IPs_bleiben_vollstaendig()
    {
        Assert.Equal("203.0.113.10", Display.Format("203.0.113.10", isScanner: true));
    }

    [Fact]
    public void Maskierung_ist_abschaltbar()
    {
        _preferences.ShowFullVisitorIps = true;

        Assert.Equal("198.51.100.23", Display.Format("198.51.100.23", isScanner: false));
    }

    [Fact]
    public void Unbrauchbare_Adressen_bleiben_wie_sie_sind()
    {
        Assert.Equal("unknown", Display.Format("unknown", isScanner: false));
    }
}
