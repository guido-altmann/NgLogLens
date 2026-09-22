using LogLens.Core.Aggregation;
using LogLens.Core.Models;
using LogLens.Core.Tests.Fixtures;

namespace LogLens.Core.Tests.Aggregation;

/// <summary>Filter der Rohdaten-Ansicht (SPEC 8.8): Klasse, Status, IP, Pfad, Zeitraum.</summary>
public sealed class EntryFilterTests
{
    [Fact]
    public async Task Leerer_Filter_laesst_alles_durch()
    {
        var result = await FixtureLog.AnalyzeAsync(TestContext.Current.CancellationToken);

        Assert.True(EntryFilter.None.IsEmpty);
        Assert.Equal(29, EntryFilter.None.Apply(result.Entries).Count);
    }

    [Fact]
    public async Task Filter_nach_Klasse()
    {
        var result = await FixtureLog.AnalyzeAsync(TestContext.Current.CancellationToken);

        var filter = new EntryFilter { Classes = new HashSet<TrafficClass> { TrafficClass.Bot, TrafficClass.AiAgent } };

        Assert.Equal(7, filter.Apply(result.Entries).Count);
    }

    [Theory]
    [InlineData("404", 10)]
    [InlineData("4xx", 13)]
    [InlineData("4XX", 13)]
    [InlineData("2xx, 405", 17)]
    [InlineData("5xx", 0)]
    public async Task Filter_nach_Status(string text, int expected)
    {
        var result = await FixtureLog.AnalyzeAsync(TestContext.Current.CancellationToken);

        Assert.True(StatusFilter.TryParse(text, out var status));

        Assert.Equal(expected, new EntryFilter { Status = status }.Apply(result.Entries).Count);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("4x")]
    [InlineData("99")]
    [InlineData("6xx")]
    public void Ungueltiger_Status_wird_erkannt(string text)
    {
        Assert.False(StatusFilter.TryParse(text, out _));
    }

    [Fact]
    public void Leerer_Status_ist_kein_Filter()
    {
        Assert.True(StatusFilter.TryParse("  ", out var status));
        Assert.Null(status);
    }

    [Theory]
    [InlineData("203.0.113.55", 4)]
    [InlineData("203.0.113", 8)]
    [InlineData("203.0.113.0/24", 8)]
    [InlineData("198.51.101.0/24", 2)]
    public async Task Filter_nach_IP_als_Teiltext_oder_Netz(string text, int expected)
    {
        var result = await FixtureLog.AnalyzeAsync(TestContext.Current.CancellationToken);

        Assert.Equal(expected, new EntryFilter { Ip = text }.Apply(result.Entries).Count);
    }

    [Theory]
    [InlineData("impressum", 2)]
    [InlineData("IMPRESSUM", 2)]
    [InlineData("../", 1)]
    [InlineData("gscan", 1)]
    public async Task Filter_nach_Pfad_im_Rohwert_und_dekodiert_samt_Query(string text, int expected)
    {
        var result = await FixtureLog.AnalyzeAsync(TestContext.Current.CancellationToken);

        Assert.Equal(expected, new EntryFilter { Path = text }.Apply(result.Entries).Count);
    }

    [Fact]
    public async Task Filter_nach_Zeitraum_schliesst_beide_Tage_ein()
    {
        var result = await FixtureLog.AnalyzeAsync(TestContext.Current.CancellationToken);

        var filter = new EntryFilter { From = new DateOnly(2026, 8, 28), To = new DateOnly(2026, 8, 30) };

        // 28.08.: 7 Anfragen (inkl. gekürzter Zeile 31), 30.08.: 4.
        Assert.Equal(11, filter.Apply(result.Entries).Count);
    }

    [Fact]
    public async Task Filter_werden_kombiniert()
    {
        var result = await FixtureLog.AnalyzeAsync(TestContext.Current.CancellationToken);

        var filter = new EntryFilter
        {
            Classes = new HashSet<TrafficClass> { TrafficClass.Attack },
            Ip = "203.0.113.10",
            Status = StatusFilter.TryParse("404", out var status) ? status : null,
        };

        Assert.Equal([1, 3, 5], filter.Apply(result.Entries).Select(e => e.Entry.LineNumber));
        Assert.False(filter.IsEmpty);
    }
}
