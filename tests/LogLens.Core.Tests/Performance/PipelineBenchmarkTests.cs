using System.Diagnostics;
using LogLens.Core.Models;
using LogLens.Core.Parsing;
using Microsoft.Extensions.DependencyInjection;

namespace LogLens.Core.Tests.Performance;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class BenchmarkCollection
{
    /// <summary>Ohne parallel laufende Tests, sonst misst der Benchmark die Nachbarn mit.</summary>
    public const string Name = "Benchmark";
}

/// <summary>
/// Nicht-funktionale Anforderung aus SPEC 9: ein Log von 10 MB (~50.000 Zeilen) ist in
/// unter 5 Sekunden verarbeitet. Gemessen wird die komplette Pipeline aus der DI, wie
/// die Oberfläche sie aufruft – ohne Aufwärmlauf, denn den hat im Browser auch niemand.
/// </summary>
/// <remarks>
/// Der Test läuft auf der JIT-Runtime des Test-Hosts. Im Browser interpretiert die
/// WebAssembly-Runtime ohne AOT; dort ist dieselbe Arbeit langsamer. Die Upload-Seite
/// zeigt deshalb die gemessene Dauer an, damit sich der Wert im Browser nachprüfen lässt.
/// </remarks>
[Collection(BenchmarkCollection.Name)]
public sealed class PipelineBenchmarkTests
{
    private const long TenMegabytes = 10L * 1024 * 1024;

    private const int FiftyThousandLines = 50_000;

    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Zehn_Megabyte_Log_ist_in_unter_fuenf_Sekunden_ausgewertet()
    {
        var log = GeneratedLog.Create(TenMegabytes, FiftyThousandLines);
        var pipeline = new ServiceCollection().AddLogLensCore().BuildServiceProvider()
            .GetRequiredService<LogAnalysisPipeline>();

        var stopwatch = Stopwatch.StartNew();
        var result = await pipeline.AnalyzeAsync(
            LogFileSource.FromText("generated.log", log.Text),
            cancellationToken: TestContext.Current.CancellationToken);
        stopwatch.Stop();

        TestContext.Current.TestOutputHelper?.WriteLine(
            $"{log.Lines:N0} Zeilen, {log.Bytes / 1024d / 1024d:N1} MB, {result.Requests:N0} Anfragen: "
            + $"{stopwatch.Elapsed.TotalMilliseconds:N0} ms");

        Assert.Equal(log.Lines, result.Diagnostics.TotalLines);
        Assert.True(
            stopwatch.Elapsed < Budget,
            $"Auswertung dauerte {stopwatch.Elapsed.TotalSeconds:N2} s, erlaubt sind {Budget.TotalSeconds:N0} s.");
    }

    [Fact]
    public async Task Generiertes_Log_deckt_alle_Stufen_der_Pipeline_ab()
    {
        // Ein Bruchteil genügt: geprüft wird die Mischung, nicht die Zeit.
        var log = GeneratedLog.Create(TenMegabytes / 20);
        var pipeline = new ServiceCollection().AddLogLensCore().BuildServiceProvider()
            .GetRequiredService<LogAnalysisPipeline>();

        var result = await pipeline.AnalyzeAsync(
            LogFileSource.FromText("generated.log", log.Text),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.All(Enum.GetValues<TrafficClass>(), c => Assert.True(result.Count(c) > 0, $"Keine Anfrage der Klasse {c}."));
        Assert.True(result.Diagnostics.NotFoundPageErrorLines > 0);
        Assert.True(result.Diagnostics.TruncatedAccessLines > 0);
        Assert.True(result.Diagnostics.UnknownLines > 0);
        Assert.NotEmpty(result.Scanners);
        Assert.NotEmpty(result.Findings);
    }
}
