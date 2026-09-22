using LogLens.Core.Models;
using LogLens.Core.Parsing;
using LogLens.Core.Tests.Fixtures;

namespace LogLens.Core.Tests.Parsing;

public sealed class LogParseServiceTests
{
    private const string AccessLine =
        """10.0.1.9 - - [27/Aug/2026:10:16:58 +0000] "GET / HTTP/1.1" 200 8123 "-" "Chrome" "203.0.113.10" """;

    [Fact]
    public async Task Unbekannte_Zeilen_werden_gezaehlt_und_als_Beispiel_gesammelt()
    {
        var text = string.Join('\n', AccessLine.TrimEnd(), "völliger Unsinn", "noch mehr Unsinn");

        var result = await FixtureLog.CreateService().ParseAsync(
            LogFileSource.FromText("test.log", text),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Diagnostics.UnknownLines);
        Assert.Equal([2, 3], result.Diagnostics.UnknownSamples.Select(s => s.LineNumber));
        Assert.Equal("völliger Unsinn", result.Diagnostics.UnknownSamples[0].Text);
    }

    [Fact]
    public async Task Beispiele_unbekannter_Zeilen_sind_begrenzt()
    {
        var options = new ParserOptions { MaxUnknownSamples = 3 };
        var service = new LogParseService(
            new LogLineParserChain(LogLineParserChain.CreateDefaultParsers()),
            new ErrorLineDeduplicator(options),
            options);

        var text = string.Join('\n', Enumerable.Repeat("Unsinn", 10));

        var result = await service.ParseAsync(
            LogFileSource.FromText("test.log", text),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(10, result.Diagnostics.UnknownLines);
        Assert.Equal(3, result.Diagnostics.UnknownSamples.Count);
    }

    [Fact]
    public async Task Leerzeilen_werden_gezaehlt_aber_nicht_als_unbekannt_gewertet()
    {
        var text = string.Join('\n', AccessLine.TrimEnd(), "", "   ");

        var result = await FixtureLog.CreateService().ParseAsync(
            LogFileSource.FromText("test.log", text),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(3, result.Diagnostics.TotalLines);
        Assert.Equal(2, result.Diagnostics.BlankLines);
        Assert.Equal(0, result.Diagnostics.UnknownLines);
    }

    [Fact]
    public async Task Fortschritt_wird_gemeldet()
    {
        var reports = new List<ParseProgress>();
        var progress = new SynchronousProgress<ParseProgress>(reports.Add);
        var options = new ParserOptions { YieldInterval = 2 };
        var service = new LogParseService(
            new LogLineParserChain(LogLineParserChain.CreateDefaultParsers()),
            new ErrorLineDeduplicator(options),
            options);

        await service.ParseAsync(
            LogFileSource.FromText(FixtureLog.Name, await File.ReadAllTextAsync(FixtureLog.FullPath, TestContext.Current.CancellationToken)),
            progress,
            TestContext.Current.CancellationToken);

        Assert.NotEmpty(reports);
        Assert.Equal(32, reports[^1].LinesRead);
        Assert.Equal(FixtureLog.Name, reports[^1].FileName);
        Assert.Equal(1, reports[^1].FileCount);
    }

    [Fact]
    public async Task Abbruch_wird_beachtet()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            FixtureLog.CreateService().ParseAsync(
                LogFileSource.FromText("test.log", AccessLine),
                cancellationToken: cts.Token));
    }

    [Fact]
    public async Task Identische_Zeilen_aus_mehreren_Dateien_werden_einmal_gezaehlt()
    {
        var text = await File.ReadAllTextAsync(FixtureLog.FullPath, TestContext.Current.CancellationToken);

        var result = await FixtureLog.CreateService().ParseAsync(
            [LogFileSource.FromText("a.log", text), LogFileSource.FromText("b.log", text)],
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(29, result.AccessEntries.Count);
        Assert.Equal(32, result.Diagnostics.IdenticalLinesRemoved);
        Assert.Equal(2, result.Diagnostics.Files.Count);
    }

    [Fact]
    public async Task Identische_Zeilen_innerhalb_einer_Datei_bleiben_erhalten()
    {
        // Zwei echte Anfragen können zeichengleich sein; innerhalb einer Datei
        // wird deshalb nicht dedupliziert.
        var text = string.Join('\n', AccessLine.TrimEnd(), AccessLine.TrimEnd());

        var result = await FixtureLog.CreateService().ParseAsync(
            LogFileSource.FromText("test.log", text),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, result.AccessEntries.Count);
        Assert.Equal(0, result.Diagnostics.IdenticalLinesRemoved);
    }

    [Fact]
    public async Task Zeilennummern_laufen_ueber_mehrere_Dateien_durch()
    {
        var result = await FixtureLog.CreateService().ParseAsync(
            [
                LogFileSource.FromText("a.log", AccessLine.TrimEnd()),
                LogFileSource.FromText("b.log", string.Join('\n', "Unsinn", AccessLine.TrimEnd().Replace("203.0.113.10", "203.0.113.11"))),
            ],
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal([1, 3], result.AccessEntries.Select(e => e.LineNumber));
        Assert.Equal(2, result.Diagnostics.Files[1].FirstLineNumber);
    }
}
