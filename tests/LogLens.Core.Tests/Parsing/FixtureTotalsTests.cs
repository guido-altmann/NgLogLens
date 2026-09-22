using LogLens.Core.Models;
using LogLens.Core.Tests.Fixtures;

namespace LogLens.Core.Tests.Parsing;

/// <summary>
/// Zählwerte aus sample-expected.md (Abschnitte „Zeilentypen" und „Aggregate").
/// </summary>
public sealed class FixtureTotalsTests
{
    [Fact]
    public async Task Zeilentypen_werden_wie_erwartet_gezaehlt()
    {
        var result = await FixtureLog.ParseAsync(TestContext.Current.CancellationToken);
        var diagnostics = result.Diagnostics;

        Assert.Equal(32, diagnostics.TotalLines);
        Assert.Equal(28, diagnostics.AccessLines);
        Assert.Equal(1, diagnostics.TruncatedAccessLines);
        Assert.Equal(2, diagnostics.ErrorLines);
        Assert.Equal(1, diagnostics.TruncatedErrorLines);
        Assert.Equal(0, diagnostics.UnknownLines);
        Assert.Empty(diagnostics.UnknownSamples);
    }

    [Fact]
    public async Task Anfragen_gesamt_sind_29()
    {
        var result = await FixtureLog.ParseAsync(TestContext.Current.CancellationToken);

        Assert.Equal(29, result.AccessEntries.Count);
    }

    [Fact]
    public async Task Die_beiden_404_Folgefehler_werden_entfernt_aber_gezaehlt()
    {
        var result = await FixtureLog.ParseAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Diagnostics.NotFoundPageErrorLines);
        Assert.Single(result.ErrorEntries);                       // nur die gekürzte Error-Zeile
        Assert.True(result.ErrorEntries[0].IsTruncated);
    }

    [Fact]
    public async Task Alle_Zeilen_tragen_eine_Client_IP_aus_dem_Header()
    {
        var result = await FixtureLog.ParseAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, result.Diagnostics.LinesWithoutClientIpHeader);
        Assert.False(result.Diagnostics.ClientIpHeaderMissing);
    }

    [Fact]
    public async Task Statuscodes_der_vollstaendigen_Zeilen_stimmen()
    {
        var result = await FixtureLog.ParseAsync(TestContext.Current.CancellationToken);

        var byStatus = result.AccessEntries
            .Where(e => e.Status is not null)
            .GroupBy(e => e.Status!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        Assert.Equal(
            new Dictionary<int, int> { [200] = 14, [404] = 10, [405] = 2, [400] = 1, [206] = 1 },
            byStatus);
    }

    [Fact]
    public async Task Zeitraum_umfasst_den_27_August_bis_22_September_2026()
    {
        var result = await FixtureLog.ParseAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            new DateTimeOffset(2026, 8, 27, 10, 16, 58, TimeSpan.Zero),
            result.AccessEntries.Min(e => e.Timestamp));
        Assert.Equal(
            new DateTimeOffset(2026, 9, 22, 7, 45, 51, TimeSpan.Zero),
            result.AccessEntries.Max(e => e.Timestamp));
    }

    [Fact]
    public async Task Zeilennummern_bleiben_1_basiert_und_erhalten()
    {
        var result = await FixtureLog.ParseAsync(TestContext.Current.CancellationToken);

        int[] expected = [.. Enumerable.Range(1, 30).Where(n => n is not (2 or 4)), 31];

        Assert.Equal(expected, result.AccessEntries.Select(e => e.LineNumber));
    }
}
