using LogLens.Core;
using LogLens.Core.Models;
using LogLens.Core.Parsing;

namespace LogLens.Core.Tests.Parsing;

/// <summary>
/// SPEC 3: Error-Zeilen „open() …/404.html failed" sind Folgefehler eines 404 und
/// zählen nicht als eigenes Ereignis. Ihre Anzahl bleibt für ein Finding erhalten.
/// </summary>
public sealed class ErrorLineDeduplicatorTests
{
    private static readonly ErrorLineDeduplicator Deduplicator = new(new ParserOptions());

    private static ErrorEntry Error(int lineNumber, string message) => new(
        lineNumber, new DateTimeOffset(2026, 8, 27, 10, 0, 0, TimeSpan.Zero),
        TimePrecision.Second, "error", message, Request: null, Host: null,
        ClientIp: null, IsTruncated: false);

    [Fact]
    public void Folgefehler_der_fehlenden_404_Seite_werden_aussortiert_und_gezaehlt()
    {
        ErrorEntry[] entries =
        [
            Error(2, """open() "/usr/share/nginx/html/404.html" failed (2: No such file or directory)"""),
            Error(4, """open() "/usr/share/nginx/html/404.html" failed (2: No such file or directory)"""),
            Error(6, "upstream timed out"),
        ];

        var result = Deduplicator.Dedupe(entries);

        Assert.Equal(2, result.NotFoundPageErrorLines);
        Assert.Equal([6], result.Kept.Select(e => e.LineNumber));
    }

    [Fact]
    public void Andere_open_Fehler_bleiben_erhalten()
    {
        ErrorEntry[] entries =
        [
            Error(1, """open() "/usr/share/nginx/html/impressum.html" failed (2: No such file or directory)"""),
        ];

        var result = Deduplicator.Dedupe(entries);

        Assert.Equal(0, result.NotFoundPageErrorLines);
        Assert.Single(result.Kept);
    }
}
