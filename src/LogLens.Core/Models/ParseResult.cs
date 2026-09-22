namespace LogLens.Core.Models;

/// <summary>Beispiel einer nicht erkannten Zeile für die Parser-Diagnose (SPEC 2.4).</summary>
public sealed record UnknownLineSample(int LineNumber, string FileName, string Text);

/// <summary>Eine eingelesene Datei und ihr Bereich in der fortlaufenden Zeilenzählung.</summary>
public sealed record SourceFileInfo(int Index, string Name, int FirstLineNumber, int LastLineNumber, int Lines);

/// <summary>Zählwerte des Einlesens. Keine Zeile wird ohne Zählung verworfen (SPEC 2.4).</summary>
public sealed record ParseDiagnostics(
    int TotalLines,
    int AccessLines,
    int TruncatedAccessLines,
    int ErrorLines,
    int TruncatedErrorLines,
    int NotFoundPageErrorLines,
    int IdenticalLinesRemoved,
    int BlankLines,
    int UnknownLines,
    int LinesWithoutClientIpHeader,
    IReadOnlyList<UnknownLineSample> UnknownSamples,
    IReadOnlyList<SourceFileInfo> Files)
{
    /// <summary>Auslöser für das Finding „Keine Client-IP im Log" (SPEC 2.1).</summary>
    public bool ClientIpHeaderMissing => LinesWithoutClientIpHeader > 0;

    /// <summary>Anfragen insgesamt: vollständige und gekürzte Access-Zeilen (SPEC 3).</summary>
    public int Requests => AccessLines + TruncatedAccessLines;
}

/// <summary>Ergebnis der Stufen Parse und Dedupe.</summary>
public sealed record ParseResult(
    IReadOnlyList<AccessEntry> AccessEntries,
    IReadOnlyList<ErrorEntry> ErrorEntries,
    ParseDiagnostics Diagnostics);

/// <summary>Fortschritt während des Einlesens; im Browser alle paar tausend Zeilen gemeldet.</summary>
public sealed record ParseProgress(string FileName, int FileIndex, int FileCount, int LinesRead);
