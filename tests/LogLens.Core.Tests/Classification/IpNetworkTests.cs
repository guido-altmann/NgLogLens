using System.Net;
using LogLens.Core.Classification;

namespace LogLens.Core.Tests.Classification;

/// <summary>
/// Bitweiser Netzvergleich ohne DNS und ohne Netzzugriff – Grundlage der KI-Verifikation
/// (SPEC 5.5) und des Monitoring-Verdachts (SPEC 5.8).
/// </summary>
public sealed class IpNetworkTests
{
    [Theory]
    [InlineData("216.73.216.0/22", "216.73.216.232", true)]
    [InlineData("216.73.216.0/22", "216.73.219.255", true)]
    [InlineData("216.73.216.0/22", "216.73.220.0", false)]
    [InlineData("216.73.216.0/22", "192.0.2.201", false)]
    [InlineData("18.97.1.228/30", "18.97.1.231", true)]
    [InlineData("18.97.1.228/30", "18.97.1.232", false)]
    [InlineData("34.162.230.222/32", "34.162.230.222", true)]
    [InlineData("34.162.230.222", "34.162.230.222", true)]
    [InlineData("2001:4860:4801:10::/64", "2001:4860:4801:10::1", true)]
    [InlineData("2001:4860:4801:10::/64", "2001:4860:4801:11::1", false)]
    public void Enthaelt_prueft_den_Netzanteil(string cidr, string address, bool expected)
    {
        var network = IpNetwork.Parse(cidr);

        Assert.Equal(expected, network.Contains(address));
    }

    [Fact]
    public void IPv4_und_IPv6_werden_nie_verwechselt()
    {
        Assert.False(IpNetwork.Parse("0.0.0.0/0").Contains("2001:db8::1"));
        Assert.False(IpNetwork.Parse("::/0").Contains("192.0.2.1"));
    }

    [Theory]
    [InlineData("kein-netz")]
    [InlineData("192.0.2.0/33")]
    [InlineData("192.0.2.0/-1")]
    [InlineData("")]
    [InlineData(null)]
    public void Ungueltige_Eingaben_werden_abgewiesen(string? cidr)
    {
        Assert.False(IpNetwork.TryParse(cidr, out _));
    }

    [Fact]
    public void Gleiches_16er_Netz_wird_erkannt()
    {
        // Das Sondenpaar aus der Fixture (Zeilen 13–16).
        Assert.True(IpNetwork.SameNetwork(
            IPAddress.Parse("198.51.100.40"), IPAddress.Parse("198.51.101.41"), 16));
        Assert.False(IpNetwork.SameNetwork(
            IPAddress.Parse("198.51.100.40"), IPAddress.Parse("198.52.100.40"), 16));
    }

    [Fact]
    public void Netzschluessel_maskieren_wie_in_der_Oberflaeche_gefordert()
    {
        Assert.Equal("198.51.100.0/24", IpNetwork.NetworkKey(IPAddress.Parse("198.51.100.99"), 24));
        Assert.Equal("2001:db8:1::/48", IpNetwork.NetworkKey(IPAddress.Parse("2001:db8:1:2::9"), 48));
    }
}
