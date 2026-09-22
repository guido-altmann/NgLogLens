using LogLens.Core;
using LogLens.Core.Parsing;
using LogLens.Core.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace LogLens.Core.Tests.Parsing;

/// <summary>
/// Tabelle „Zeilentypen" aus sample-expected.md: 28 vollständige Access-Zeilen,
/// 1 gekürzte Access-Zeile, 2 Error-Duplikate, 1 gekürzte Error-Zeile, 0 unbekannt.
/// </summary>
public sealed class LineKindTests
{
    private static readonly LogLineParserChain Chain =
        new ServiceCollection().AddLogLensCore().BuildServiceProvider()
            .GetRequiredService<LogLineParserChain>();

    public static TheoryData<int, LogLineKind> ErwarteteZeilentypen()
    {
        var data = new TheoryData<int, LogLineKind>();

        foreach (var lineNumber in Enumerable.Range(1, 32))
        {
            var kind = lineNumber switch
            {
                2 or 4 => LogLineKind.Error,
                31 => LogLineKind.AccessTruncated,
                32 => LogLineKind.ErrorTruncated,
                _ => LogLineKind.Access,
            };

            data.Add(lineNumber, kind);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ErwarteteZeilentypen))]
    public void Jede_Fixture_Zeile_wird_als_erwarteter_Typ_erkannt(int lineNumber, LogLineKind expected)
    {
        var parsed = Chain.Parse(FixtureLog.Line(lineNumber), lineNumber);

        Assert.Equal(expected, parsed?.Kind ?? LogLineKind.Unknown);
    }

    [Fact]
    public void Gekuerzte_Error_Zeile_wird_nicht_als_Access_Zeile_gelesen()
    {
        // Zeile 32 beginnt wie eine Error-Zeile und endet trotzdem auf "<IP>".
        var parsed = Chain.Parse(FixtureLog.Line(32), 32);

        Assert.Equal(LogLineKind.ErrorTruncated, parsed?.Kind);
    }
}
