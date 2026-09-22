using LogLens.Core;
using LogLens.Core.Models;
using LogLens.Core.Parsing;
using Microsoft.Extensions.DependencyInjection;

namespace LogLens.Core.Tests.Fixtures;

/// <summary>
/// Zugriff auf <c>tests/fixtures/sample-nginx.log</c>. Sollwerte stehen in
/// <c>tests/fixtures/sample-expected.md</c> und werden dort nicht angepasst.
/// </summary>
internal static class FixtureLog
{
    public const string Name = "sample-nginx.log";

    public static string FullPath { get; } =
        Path.Combine(AppContext.BaseDirectory, "fixtures", Name);

    public static string[] Lines { get; } = File.ReadAllLines(FullPath);

    /// <summary>Eine Zeile, 1-basiert wie in sample-expected.md.</summary>
    public static string Line(int lineNumber) => Lines[lineNumber - 1];

    public static LogParseService CreateService()
        => new ServiceCollection().AddLogLensCore().BuildServiceProvider()
            .GetRequiredService<LogParseService>();

    public static Task<ParseResult> ParseAsync(CancellationToken cancellationToken = default)
        => CreateService().ParseAsync(
            LogFileSource.FromText(Name, File.ReadAllText(FullPath)),
            cancellationToken: cancellationToken);
}
